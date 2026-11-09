using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Starflow;

// Reads AndThen rules and StatusShift rules as JSON or as their share codes. Imported flows are off.
// Enums in those files are numbers by default; names are accepted too.
public static class Importers
{
    public static bool TryAndThen(string text, out List<FlowItem> flows, out string message)
    {
        flows = [];
        if (!TryElements(text, "AT1.", "Rules", out var rules, out message)) return false;
        var skipped = new List<string>();
        foreach (var rule in rules) flows.Add(AndThenRule(rule, skipped));
        message = Summary(flows.Count, skipped);
        return flows.Count > 0;
    }

    public static bool TryStatusShift(string text, out List<FlowItem> flows, out string message)
    {
        flows = [];
        if (!TryElements(text, "SS1.", "Rules", out var rules, out message, out var root)) return false;
        var mode = root is { } r && r.ValueKind == JsonValueKind.Object ? Int(r, "ApplyMode", 3) : 3;
        var skipped = new List<string>();
        foreach (var rule in rules) flows.Add(StatusShiftRule(rule, mode, skipped));
        message = Summary(flows.Count, skipped);
        return flows.Count > 0;
    }

    private static string Summary(int count, List<string> skipped)
    {
        var text = $"Imported {count}. They are off.";
        if (skipped.Count > 0) text += " Left out: " + string.Join("; ", skipped.Distinct().Take(8)) + ".";
        return text;
    }

    // ---------- AndThen ----------

    private static readonly string[] AndThenChips =
    [
        "State", "Job", "Role", "Zone", "World", "DataCenter", "PartySize", "Duty", "Group", "Time",
        "Weather", "Nearby", "Place", "Target", "OnlineStatus", "Account", "Housing", "Mount", "Level",
    ];

    private static readonly string[] AndThenRows =
        ["Command", "Wait", "Status", "Config", "Notify", "Logout", "Exit", "PenumbraMod", "PenumbraReset"];

    private static FlowItem AndThenRule(JsonElement rule, List<string> skipped)
    {
        var flow = new FlowItem
        {
            Name = Str(rule, "Name", "Imported rule"),
            Notes = Str(rule, "Notes"),
            Folder = Str(rule, "Folder"),
            Enabled = false,
            Mode = RunMode.Off,
            OnStop = StopMode.Keep,
            HoldSec = (float)Dbl(rule, "DelaySec"),
        };

        var and = Array(rule, "AndChips");
        var or = Array(rule, "OrChips");
        var not = Array(rule, "NotChips");
        foreach (var chip in and) AddChip(flow, AndThenChip(chip, skipped), false);
        foreach (var chip in or) AddChip(flow, AndThenChip(chip, skipped), false);
        foreach (var chip in not) AddChip(flow, AndThenChip(chip, skipped), true);

        if (or.Count > 0 && and.Count == 0 && not.Count == 0) flow.WhenJoin = Join.Any;
        else if (or.Count > 1)
        {
            flow.WhenJoin = Join.All;
            flow.Notes = Line(flow.Notes, $"AndThen needed any one of the last {or.Count} OR chips. Starflow reads them all; switch When to any if that fits.");
        }

        foreach (var row in Array(rule, "Then"))
        {
            var card = AndThenRow(row, skipped);
            if (card is not null) flow.Cards.Add(card);
        }
        return flow;
    }

    private static Card? AndThenChip(JsonElement chip, List<string> skipped)
    {
        var kind = EnumName(chip, "Kind", AndThenChips);
        var value = Str(chip, "Value").Trim();
        Card C(string type, string v) => new() { Section = Section.When, Type = type, Value = v };
        switch (kind)
        {
            case "State": return C(CardType.State, value);
            case "Job": return C(CardType.Job, value);
            case "Role": return C(CardType.Role, value);
            case "Zone": return C(CardType.Zone, value);
            case "World": return C(CardType.World, value);
            case "DataCenter": return C(CardType.DataCenter, value);
            case "PartySize": return C(CardType.Party, value);
            case "Duty": return C(CardType.Duty, value.ToLowerInvariant());
            case "Group": return C(CardType.Group, value.ToLowerInvariant());
            case "Time": return AndThenTime(value);
            case "Weather": return C(CardType.Weather, value);
            case "Nearby": return C(CardType.Nearby, value);
            case "Place": return C(CardType.Place, value);
            case "Target": return C(CardType.Target, value.ToLowerInvariant());
            case "OnlineStatus": return C(CardType.Status, value);
            case "Account": return C(CardType.Account, value.ToLowerInvariant());
            case "Housing": return C(CardType.Housing, value);
            case "Mount": return C(CardType.Mount, value);
            case "Level": return C(CardType.Level, value);
            default:
                skipped.Add("chip " + kind);
                return null;
        }
    }

    // AndThen time chips: a weekday, day/night/dawn/dusk, or a compare on the hour. No prefix is Eorzea.
    private static Card AndThenTime(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        Card C(string type, string v) => new() { Section = Section.When, Type = type, Value = v };
        if (Enum.GetNames<DayOfWeek>().Any(d => d.Equals(text, StringComparison.OrdinalIgnoreCase))) return C(CardType.Weekday, text);
        switch (text)
        {
            case "day": return C(CardType.Time, "et 6-18");
            case "night": return C(CardType.Time, "et 18-6");
            case "dawn": return C(CardType.Time, "et 5-8");
            case "dusk": return C(CardType.Time, "et 17-20");
        }
        var prefix = "et ";
        var rest = text;
        if (rest.StartsWith("local")) { prefix = string.Empty; rest = rest[5..]; }
        else if (rest.StartsWith("lt")) { prefix = string.Empty; rest = rest[2..]; }
        else if (rest.StartsWith("et")) rest = rest[2..];
        var range = HourRange(rest.Trim());
        return C(CardType.Time, range is null ? prefix + rest.Trim() : prefix + range);
    }

    public static string? HourRange(string compare)
    {
        var t = compare.Trim();
        int N(string s) => int.TryParse(s.Trim(), out var n) ? Math.Clamp(n, 0, 24) : -1;
        int n;
        if (t.StartsWith(">=") && (n = N(t[2..])) >= 0) return $"{n}-24";
        if (t.StartsWith("<=") && (n = N(t[2..])) >= 0) return $"0-{Math.Min(24, n + 1)}";
        if (t.StartsWith('>') && (n = N(t[1..])) >= 0) return $"{Math.Min(24, n + 1)}-24";
        if (t.StartsWith('<') && (n = N(t[1..])) >= 0) return $"0-{n}";
        if (t.StartsWith('=')) t = t[1..];
        if ((n = N(t)) >= 0 && !t.Contains('-')) return $"{n}-{Math.Min(24, n + 1)}";
        return null;
    }

    private static Card? AndThenRow(JsonElement row, List<string> skipped)
    {
        var kind = EnumName(row, "Kind", AndThenRows);
        Card D(string type) => new() { Section = Section.Do, Type = type };
        switch (kind)
        {
            case "Command": { var c = D(CardType.Command); c.Value = Str(row, "Value"); c.WaitMs = 0; return c; }
            case "Wait": { var c = D(CardType.Wait); c.WaitMs = Int(row, "WaitMs"); return c; }
            case "Status": { var c = D(CardType.Online); c.Value = StatusWord(Str(row, "Value")); return c; }
            case "Config":
            {
                var c = D(CardType.Setting);
                var section = row.TryGetProperty("Section", out var s) ? s : default;
                c.Option = section.ValueKind == JsonValueKind.Number && section.GetInt32() == 1
                    || section.ValueKind == JsonValueKind.String && section.GetString() == "Ui" ? "Ui" : "System";
                c.Value = Str(row, "Option");
                c.Extra = Str(row, "Value");
                return c;
            }
            case "Notify": { var c = D(CardType.Notice); c.Value = Str(row, "Value"); return c; }
            case "Logout": { var c = D(CardType.Logout); c.Flag = true; return c; }
            case "Exit": { var c = D(CardType.CloseGame); c.Flag = true; return c; }
            case "PenumbraMod":
            {
                var c = D(CardType.PenumbraMod);
                c.Option = Str(row, "Option");
                c.Value = Str(row, "Value");
                c.Flag = Bool(row, "Flag", true);
                c.Inherit = Bool(row, "Inherit");
                c.Permanent = Bool(row, "Permanent");
                c.Number = Int(row, "Number");
                c.Extra = AndThenOptions(Str(row, "Extra"));
                return c;
            }
            case "PenumbraReset":
            {
                var c = D(CardType.PenumbraReset);
                c.Option = Str(row, "Option");
                c.Value = Str(row, "Value");
                return c;
            }
            default:
                skipped.Add("action " + kind);
                return null;
        }
    }

    private static string AndThenOptions(string extra)
    {
        if (string.IsNullOrWhiteSpace(extra)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(extra);
            if (!Prop(doc.RootElement, "Settings", out var settings) || settings.ValueKind != JsonValueKind.Object) return string.Empty;
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var group in settings.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Array) continue;
                map[group.Name] = group.Value.EnumerateArray().Select(o => o.GetString() ?? string.Empty).Where(o => o.Length > 0).ToList();
            }
            return ModOptions.Format(map);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    // ---------- StatusShift ----------

    private static readonly string[] StatusShiftChips =
    [
        "World", "Zone", "Region", "ZoneType", "Residence", "Apartment", "Duty", "Job", "NearbyPlayer", "Emote",
        "Mount", "State", "Contains", "Regex", "DataCenter", "Property", "TellFrom", "Chat", "Accessory", "Status", "Role",
    ];

    private static readonly string[] StatusShiftStatus =
    [
        "LeaveAlone", "Online", "Away", "Busy", "Roleplaying", "LookingToMeld", "LookingForParty",
        "Mentor", "PveMentor", "PvpMentor", "TradeMentor", "Returner", "NewAdventurer",
    ];

    private static readonly string[] StatusShiftModes = ["Confirm", "Auto", "Off", "Selector"];

    private static readonly string[] StatusShiftSchedules = ["Always", "Daily", "Weekly", "OneTime", "Custom"];

    private static readonly string[] Activities =
    [
        "InDuty", "InCombat", "Crafting", "Gathering", "Mounted", "Flying", "Swimming", "WatchingCutscene", "Dead",
        "InParty", "BoundByDuty", "Diving", "WeaponDrawn", "WaitingForDutyFinder", "PvP", "PartyLeader", "InResidence",
        "Sitting", "Casting", "Jumping", "Occupied", "Trading", "BetweenAreas", "Roleplaying", "TargetingPlayer",
        "TargetingEnemy", "TargetedByPlayer", "HelmShown", "WeaponShown", "Walking", "Fishing", "Performing",
        "InSanctuary", "Carrying", "UsingHousing", "FashionAccessory",
    ];

    private static FlowItem StatusShiftRule(JsonElement rule, int globalMode, List<string> skipped)
    {
        var flow = new FlowItem
        {
            Name = Str(rule, "Name", "Imported rule"),
            Notes = Str(rule, "Notes"),
            Folder = Str(rule, "Folder"),
            Enabled = false,
            Priority = Int(rule, "Priority"),
            OnStop = Bool(rule, "RevertWhenFalse", true) ? StopMode.Revert : StopMode.Keep,
        };

        var mode = StatusShiftModes[Math.Clamp(globalMode, 0, StatusShiftModes.Length - 1)];
        flow.Mode = mode switch
        {
            "Auto" => RunMode.Auto,
            "Off" => RunMode.Off,
            _ => RunMode.Ask,
        };

        // Conditions
        var schedule = StatusShiftSchedule(rule);
        if (schedule is not null) AddChip(flow, new Card { Section = Section.When, Type = CardType.Schedule, Value = schedule }, false);

        foreach (var chip in Array(rule, "AndChips")) AddChip(flow, StatusShiftChip(chip, skipped), false);
        var or = Array(rule, "OrChips");
        foreach (var chip in or) AddChip(flow, StatusShiftChip(chip, skipped), false);
        foreach (var chip in Array(rule, "NotChips")) AddChip(flow, StatusShiftChip(chip, skipped), true);
        if (or.Count > 1) flow.Notes = Line(flow.Notes, $"StatusShift needed any one of {or.Count} OR chips. Starflow reads them all; switch When to any if that fits.");

        if (rule.TryGetProperty("InDuty", out var duty) && duty.ValueKind is JsonValueKind.True or JsonValueKind.False)
            AddChip(flow, new Card { Section = Section.When, Type = CardType.Duty, Value = duty.GetBoolean() ? "in" : "out" }, false);
        var jobs = Strings(rule, "JobAbbrs");
        if (jobs.Count > 0) AddChip(flow, new Card { Section = Section.When, Type = CardType.Job, Value = string.Join('|', jobs) }, false);
        var worlds = Strings(rule, "WorldNames");
        if (worlds.Count > 0) AddChip(flow, new Card { Section = Section.When, Type = CardType.World, Value = string.Join('|', worlds) }, false);
        var zones = Strings(rule, "TerritoryNameContains");
        if (zones.Count > 0) AddChip(flow, new Card { Section = Section.When, Type = CardType.Zone, Value = string.Join('|', zones) }, false);

        var states = Array(rule, "States");
        var stateAny = EnumName(rule, "StateMatch", ["Any", "All"]) == "Any";
        var stateCards = 0;
        foreach (var state in states)
        {
            var op = EnumName(state, "Op", ["Any", "Yes", "No"]);
            if (op == "Any") continue;
            var card = Activity(EnumName(state, "Flag", Activities), skipped);
            if (card is null) continue;
            AddChip(flow, card, op == "No");
            stateCards++;
        }
        if (stateAny && stateCards > 1) flow.Notes = Line(flow.Notes, "StatusShift matched any one of the state checks. Starflow reads them all.");

        // Action
        if (mode == "Confirm")
            flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Notice, Value = flow.Name });

        var delay = Int(rule, "CommandDelaySeconds");
        if (Bool(rule, "ChangeSearchComment"))
            flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.SearchComment, Value = Str(rule, "SearchComment") });
        var status = EnumName(rule, "OnlineStatus", StatusShiftStatus);
        var statusWord = StatusWord(status);
        if (status != "LeaveAlone")
        {
            if (statusWord.Length > 0) flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Online, Value = statusWord });
            else skipped.Add("status " + status);
        }
        var command = Str(rule, "Command");
        if (command.Trim().Length > 0)
        {
            if (delay > 0) flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Wait, WaitMs = delay * 1000 });
            flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Command, Value = command.Trim(), WaitMs = 0 });
            if (Bool(rule, "RerunCommand")) skipped.Add("command rerun");
        }

        // Ending
        if (flow.OnStop == StopMode.Revert)
        {
            var fallback = EnumName(rule, "FallbackStatus", StatusShiftStatus);
            var word = StatusWord(fallback);
            if (fallback is not ("LeaveAlone" or "Online") && word.Length > 0)
                flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Online, Value = word });
            if (Bool(rule, "ChangeFallbackComment"))
                flow.Cards.Add(new Card { Section = Section.After, Type = CardType.SearchComment, Value = Str(rule, "FallbackComment") });
            var back = Str(rule, "FallbackCommand").Trim();
            if (back.Length > 0) flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Command, Value = back, WaitMs = 0 });
        }

        if (Str(rule, "CharacterFilter").Trim().Length > 0) skipped.Add("character filter");
        return flow;
    }

    private static string? StatusShiftSchedule(JsonElement rule)
    {
        string Range(JsonElement s) =>
            Bool(s, "AllDay", true) ? string.Empty
            : $" {Int(s, "StartHour", 9):00}:{Int(s, "StartMinute"):00}-{Int(s, "EndHour", 12):00}:{Int(s, "EndMinute"):00}";
        string Days(IEnumerable<JsonElement> days) => string.Join(',', days.Select(d =>
            d.ValueKind == JsonValueKind.Number ? ((DayOfWeek)Math.Clamp(d.GetInt32(), 0, 6)).ToString()[..3].ToLowerInvariant()
            : (d.GetString() ?? string.Empty)[..Math.Min(3, (d.GetString() ?? string.Empty).Length)].ToLowerInvariant()));

        if (Prop(rule, "Schedule", out var s) && s.ValueKind == JsonValueKind.Object)
        {
            var mode = EnumName(s, "Mode", StatusShiftSchedules);
            var range = Range(s);
            var days = Array(s, "Days");
            var start = Str(s, "DateStart");
            var end = Str(s, "DateEnd");
            string Date(string d) => DateTime.TryParse(d, out var x) ? x.ToString("yyyy-MM-dd") : string.Empty;
            switch (mode)
            {
                case "Daily": return "daily" + range;
                case "Weekly" when days.Count > 0: return $"weekly {Days(days)}{range}";
                case "OneTime" or "Custom" when Date(start).Length > 0:
                    var to = Date(end);
                    return $"once {Date(start)}{(to.Length > 0 && to != Date(start) ? ".." + to : string.Empty)}{range}";
            }
        }

        // Older fields
        var oldDays = Array(rule, "Days");
        var from = Str(rule, "TimeStart");
        var until = Str(rule, "TimeEnd");
        var time = from.Length > 0 && until.Length > 0 ? $" {from}-{until}" : string.Empty;
        if (oldDays.Count > 0) return $"weekly {Days(oldDays)}{time}";
        if (time.Length > 0) return "daily" + time;
        return null;
    }

    private static Card? StatusShiftChip(JsonElement chip, List<string> skipped)
    {
        var kind = EnumName(chip, "Kind", StatusShiftChips);
        var value = Str(chip, "Value").Trim();
        Card C(string type, string v) => new() { Section = Section.When, Type = type, Value = v };
        switch (kind)
        {
            case "World": return C(CardType.World, value);
            case "Zone": return C(CardType.Zone, value);
            case "Region": return C(CardType.Region, value);
            case "ZoneType": return C(CardType.Place, value);
            case "Residence" or "Apartment" or "Property": return C(CardType.Housing, value);
            case "Duty": return C(CardType.Duty, value.ToLowerInvariant());
            case "Job": return C(CardType.Job, value);
            case "NearbyPlayer": return C(CardType.NearbyName, value);
            case "Emote": skipped.Add("emote " + value); return C(CardType.State, "Emoting");
            case "Mount": return C(CardType.Mount, value);
            case "State": return C(CardType.State, value);
            case "DataCenter": return C(CardType.DataCenter, value);
            case "Status": return C(CardType.Status, value);
            case "Role": return C(CardType.Role, value);
            default:
                skipped.Add("chip " + kind);
                return null;
        }
    }

    private static Card? Activity(string flag, List<string> skipped)
    {
        Card S(string value) => new() { Section = Section.When, Type = CardType.State, Value = value };
        Card P(string value) => new() { Section = Section.When, Type = CardType.Place, Value = value };
        switch (flag)
        {
            case "InDuty" or "BoundByDuty": return S("InDuty");
            case "InCombat" or "Crafting" or "Gathering" or "Mounted" or "Flying" or "Swimming" or "Dead" or "InParty"
                or "Diving" or "WeaponDrawn" or "PvP" or "Sitting" or "Casting" or "Occupied" or "BetweenAreas"
                or "Fishing" or "Performing":
                return S(flag);
            case "WatchingCutscene": return S("Cutscene");
            case "WaitingForDutyFinder": return S("DutyReady");
            case "Trading": return S("Trade");
            case "Roleplaying": return S("RolePlaying");
            case "UsingHousing": return S("Housing");
            case "InResidence": return P("Housing");
            case "InSanctuary": return P("Sanctuary");
            case "TargetingPlayer": return new Card { Section = Section.When, Type = CardType.Target, Value = "player" };
            case "TargetingEnemy": return new Card { Section = Section.When, Type = CardType.Target, Value = "npc" };
            default:
                skipped.Add("state " + flag);
                return null;
        }
    }

    private static string StatusWord(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "online" => "online",
        "away" or "afk" or "away from keyboard" => "away",
        "busy" => "busy",
        "roleplaying" or "role-playing" or "rp" => "rp",
        "lookingtomeld" or "looking to meld materia" or "meld" => "meld",
        "lookingforparty" or "looking for party" or "lfp" => "lfp",
        _ => string.Empty,
    };

    // ---------- shared ----------

    private static void AddChip(FlowItem flow, Card? card, bool not)
    {
        if (card is null) return;
        card.Section = Section.When;
        card.Not = not;
        flow.Cards.Add(card);
    }

    private static string Line(string notes, string add) => string.IsNullOrWhiteSpace(notes) ? add : notes.TrimEnd() + "\n" + add;

    private static bool TryElements(string text, string codePrefix, string listName, out List<JsonElement> rules, out string error) =>
        TryElements(text, codePrefix, listName, out rules, out error, out _);

    private static bool TryElements(string text, string codePrefix, string listName, out List<JsonElement> rules, out string error, out JsonElement? root)
    {
        rules = [];
        error = string.Empty;
        root = null;
        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            error = "Paste a rule first.";
            return false;
        }

        var docs = new List<string>();
        if (body.StartsWith('{') || body.StartsWith('[')) docs.Add(body);
        else
        {
            foreach (var part in body.Split(['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                var json = Unpack(part, codePrefix);
                if (json is not null) docs.Add(json);
            }
        }
        if (docs.Count == 0)
        {
            error = "That is not JSON or a share code.";
            return false;
        }

        try
        {
            foreach (var json in docs)
            {
                var element = JsonDocument.Parse(json).RootElement.Clone();
                if (element.ValueKind == JsonValueKind.Array) rules.AddRange(element.EnumerateArray());
                else if (Prop(element, listName, out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    root = element;
                    rules.AddRange(list.EnumerateArray());
                }
                else if (element.ValueKind == JsonValueKind.Object) rules.Add(element);
            }
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }

        if (rules.Count == 0)
        {
            error = "No rules in that text.";
            return false;
        }
        return true;
    }

    private static string? Unpack(string code, string prefix)
    {
        try
        {
            var text = code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? code[prefix.Length..] : code;
            text = text.Replace('-', '+').Replace('_', '/');
            while (text.Length % 4 != 0) text += "=";
            using var input = new MemoryStream(Convert.FromBase64String(text));
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static bool Prop(JsonElement e, string name, out JsonElement value)
    {
        value = default;
        if (e.ValueKind != JsonValueKind.Object) return false;
        foreach (var p in e.EnumerateObject())
        {
            if (!p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            value = p.Value;
            return true;
        }
        return false;
    }

    private static string Str(JsonElement e, string name, string fallback = "") =>
        Prop(e, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    private static int Int(JsonElement e, string name, int fallback = 0) =>
        Prop(e, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : fallback;

    private static double Dbl(JsonElement e, string name) =>
        Prop(e, name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static bool Bool(JsonElement e, string name, bool fallback = false) =>
        Prop(e, name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;

    private static List<JsonElement> Array(JsonElement e, string name) =>
        Prop(e, name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : [];

    private static List<string> Strings(JsonElement e, string name) =>
        Array(e, name).Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty).Where(s => s.Length > 0).ToList();

    private static string EnumName(JsonElement e, string name, string[] names)
    {
        if (!Prop(e, name, out var v)) return names[0];
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n >= 0 && n < names.Length ? names[n] : n.ToString();
        if (v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString() ?? string.Empty;
            return names.FirstOrDefault(x => x.Equals(s, StringComparison.OrdinalIgnoreCase)) ?? s;
        }
        return names[0];
    }
}

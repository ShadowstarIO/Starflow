using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace Starflow.Windows;

public sealed partial class MainWindow
{
    // Sunday first, as most calendars show it. The stored text uses the same short names in any order.
    private static readonly string[] DayNames = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
    private static readonly string[] PartyBands = ["solo", "light", "full", "alliance", "party", "custom count"];
    private static readonly string[] PartyBandNames = ["Solo (1)", "Light party (2-4)", "Full party (5-8)", "Alliance", "Any party (2+)", "Custom count"];
    private string optionFilter = string.Empty;
    private string modFilter = string.Empty;
    private string commandFilter = string.Empty;
    private string[]? statusNames;
    private string pickMessage = string.Empty;
    private string probeMessage = string.Empty;

    private void DrawInspector(Configuration cfg)
    {
        var flow = cfg.Flows.Find(f => f.Id == selectedId);
        if (showImport || flow is null)
        {
            DrawImport(cfg);
            return;
        }

        var card = flow.Cards.Find(c => c.Id == selectedCard);
        if (card is null)
        {
            ImGui.TextDisabled("Select a card to edit it.");
            ImGui.Separator();
            Why(flow);
            return;
        }

        var here = plugin.Engine.Last;
        var index = flow.In(card.Section).IndexOf(card);
        ImGui.TextDisabled($"{Catalog.Key(card.Section, Math.Max(0, index))} · {card.Section} · {Describe(card.Type)}");
        ImGui.TextUnformatted(card.Label);
        var error = plugin.Engine.Error(card.Id);
        if (error is not null) Ui.Wrapped(Red, "Last run: " + error);
        var gap = Catalog.Missing(card);
        if (gap is not null) ImGui.TextColored(Amber, "Needs a " + gap);

        // What the game says right now, for every condition, so the fields below have something to compare against.
        if (CardType.IsCondition(card.Type))
        {
            var live = here.LoggedIn && Eval.CardMatches(card, here);
            Ui.Dot(live, live ? Green : Ui.Grey);
            ImGui.SameLine();
            ImGui.TextColored(live ? Green : Ui.Grey, live ? "True right now" : "False right now");
            var now = NowText(card, here);
            if (now.Length > 0) Ui.Wrapped(Teal, "Right now: " + now);
        }
        ImGui.Separator();

        if (CardType.IsCondition(card.Type))
        {
            var not = card.Not;
            if (ImGui.Checkbox("Not", ref not)) { card.Not = not; cfg.Save(); }
            Tip("Match when this is false.");
        }

        ImGui.PushItemWidth(Ui.Wrap ? -Math.Min(110, ImGui.GetContentRegionAvail().X * 0.4f) : -110);
        Edit(cfg, flow, card);
        ImGui.PopItemWidth();

        if (CardType.IsSlotted(card.Type) && card.Section == Section.Do)
        {
            var keep = card.Keep;
            if (ImGui.Checkbox("Keep this one", ref keep)) { card.Keep = keep; cfg.Save(); }
            ImGui.TextDisabled(keep || flow.OnStop == StopMode.Keep
                ? "Left as it is when the flow stops."
                : "Put back when the flow stops, after After runs.");
        }
        if (CardType.IsSlotted(card.Type) && card.Section == Section.After)
            ImGui.TextDisabled("Written on the way out. Revert will not undo it.");

        ImGui.Separator();
        Why(flow);
    }

    private static string Describe(string type)
    {
        if (type == CardType.Party) return "Party size";
        return Catalog.For(Section.When).Concat(Catalog.For(Section.Do)).FirstOrDefault(d => d.Type == type)?.Label ?? type;
    }

    private void Why(FlowItem flow)
    {
        ImGui.TextColored(Teal, "Why this flow is, or is not, running");
        foreach (var line in plugin.Engine.Why(flow))
        {
            if (line.StartsWith("Why:", StringComparison.Ordinal)) ImGui.TextWrapped(line[4..].Trim());
            else if (line.StartsWith("yes", StringComparison.Ordinal)) Ui.Wrapped(Green, line);
            else if (line.StartsWith("no", StringComparison.Ordinal)) Ui.Wrapped(Ui.Grey, line);
            else ImGui.TextDisabled(line);
        }
    }

    private static string NowText(Card card, World w)
    {
        if (!w.LoggedIn) return "not logged in";
        return card.Type switch
        {
            CardType.State => w.States.Count == 0 ? "nothing special" : string.Join(", ", w.States.OrderBy(s => s)),
            CardType.Place => string.Join(", ", w.Places.OrderBy(s => s)) is { Length: > 0 } p ? p : "Overworld",
            CardType.Duty => w.Has("InDuty") ? $"in a duty: {(w.DutyName.Length > 0 ? w.DutyName : w.TerritoryName)}"
                : w.Has("InQueue") || w.Has("DutyReady") ? "in the queue" : "not in a duty",
            CardType.Zone => $"{w.TerritoryName} (#{w.TerritoryId})",
            CardType.Region => w.RegionName,
            CardType.World => w.Account == "visiting" ? $"{w.WorldName} (home {w.HomeWorldName})" : w.WorldName,
            CardType.DataCenter => w.DataCenterName,
            CardType.Account => w.Account == "visiting" ? $"visiting {w.WorldName}, home {w.HomeWorldName}" : $"home, {w.WorldName}",
            CardType.Housing => w.Housing.Line(),
            CardType.Group or CardType.Party => $"{w.PartySize} in party ({w.Group})" + (w.Companions > 0 ? $", plus {w.Companions} companion or pet" : string.Empty),
            CardType.Job => $"{w.JobName} ({w.JobAbbr}), level {w.Level}",
            CardType.Role => w.Role,
            CardType.Level => w.Level.ToString(),
            CardType.Time => $"local {w.LocalHour:00}:{w.LocalMinute:00} · Eorzea {w.EorzeaHour:00}:{w.EorzeaMinute:00}",
            CardType.Weekday or CardType.Schedule => $"{w.Now:dddd HH:mm}",
            CardType.Weather => Weather(w),
            CardType.Mount => w.Has("Mounted") ? (w.Mount.Length > 0 ? w.Mount : "mounted") : "not mounted",
            CardType.Nearby => $"{w.Nearby} players within 30 yalms",
            CardType.NearbyName => w.NearbyNames.Count == 0 ? "nobody within 30 yalms" : $"{w.NearbyNames.Count} nearby",
            CardType.Status => w.OnlineStatus,
            CardType.Target => w.Target == "none" ? "no target" : $"{w.Target}: {w.TargetName}" + (w.TargetJob.Length > 0 ? $" ({w.TargetJob})" : string.Empty),
            CardType.Plugin => Eval.PluginLoaded(card.Value ?? string.Empty) ? "loaded" : "not loaded",
            _ => NowReaction(card, w),
        };
    }

    private static string Weather(World w)
    {
        if (w.Weather.Length == 0) return "unknown here";
        var before = w.WeatherBefore.Length > 0 ? $"was {w.WeatherBefore} · " : string.Empty;
        var next = w.WeatherNext.Length > 0 ? $" · next {w.WeatherNext} in {w.WeatherNextMinutes} min" : string.Empty;
        return $"{before}now {w.Weather}{next}";
    }

    private void Edit(Configuration cfg, FlowItem flow, Card card)
    {
        var here = plugin.Engine.Last;
        switch (card.Type)
        {
            case CardType.State: Combo(cfg, "State", Catalog.States, v => card.Value = v, card.Value); break;
            case CardType.Place: Combo(cfg, "Place", Catalog.Places, v => card.Value = v, card.Value); break;
            case CardType.Duty:
                Combo(cfg, "Kind", Catalog.Duties, v => card.Value = v, card.Value);
                MatchText(cfg, "Or name", v => card.Value = v, card.Value, "duty name, or /regex/");
                if (here.Has("InDuty")) Fill(cfg, card, here.DutyName.Length > 0 ? here.DutyName : here.TerritoryName);
                ImGui.TextDisabled("A name only matches while you are in that duty.");
                break;
            case CardType.Role: EditRoles(cfg, card, here); break;
            case CardType.Target:
                Combo(cfg, "Target", Catalog.Targets, v => card.Value = v, card.Value);
                MatchText(cfg, "Name", v => card.Extra = v, card.Extra, "optional: name, a | b, or /regex/");
                if (here.TargetName.Length > 0 && Ui.Current(here.TargetName, id: "target")) { card.Extra = here.TargetName; cfg.Save(); }
                ImGui.TextDisabled("With a name, the target must also have that name.");
                break;
            case CardType.Group:
            case CardType.Party:
                EditParty(cfg, card);
                break;
            case CardType.Account:
                Combo(cfg, "World", Catalog.Accounts, v => card.Value = v, card.Value);
                if (here.Account.Length > 0) Fill(cfg, card, here.Account);
                break;
            case CardType.Job: EditJobs(cfg, card, here); break;
            case CardType.Zone:
                MatchText(cfg, "Zone", v => card.Value = v, card.Value, "Limsa | #129 | /regex/");
                Fill(cfg, card, here.TerritoryName);
                Ui.Next(Ui.ButtonWidth("Current: id #0000"));
                Fill(cfg, card, "#" + here.TerritoryId, "id #" + here.TerritoryId);
                break;
            case CardType.Region: MatchText(cfg, "Region", v => card.Value = v, card.Value, "La Noscea | /regex/"); Fill(cfg, card, here.RegionName); break;
            case CardType.World: MatchText(cfg, "World", v => card.Value = v, card.Value, "World name"); Fill(cfg, card, here.WorldName); break;
            case CardType.DataCenter: MatchText(cfg, "Data center", v => card.Value = v, card.Value, "Name"); Fill(cfg, card, here.DataCenterName); break;
            case CardType.Plugin:
                Text(cfg, "Plugin", v => card.Value = v, card.Value, "InternalName");
                Pick(cfg, "Installed", Bridges.PluginNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), v => card.Value = v);
                ImGui.TextDisabled("True while that plugin is loaded.");
                break;
            case CardType.Weekday:
            {
                var days = DayChecks(card.Value, out var changed);
                if (changed) { card.Value = days.Length == 0 ? "sat,sun" : days; cfg.Save(); }
                Text(cfg, "Days", v => card.Value = v, card.Value, "sat,sun or weekday");
                break;
            }
            case CardType.Level:
                Text(cfg, "Compare", v => card.Value = v, card.Value, ">=4  <8  =1  1-4");
                break;
            case CardType.Nearby:
                Text(cfg, "Compare", v => card.Value = v, card.Value, ">=4, empty, few, crowded");
                break;
            case CardType.NearbyName:
                MatchText(cfg, "Names", v => card.Value = v, card.Value, "Name | Other name | /regex/");
                if (here.NearbyNames.Count > 0) Pick(cfg, "Nearby now", here.NearbyNames.Distinct().OrderBy(n => n).ToList(), v => card.Value = v);
                break;
            case CardType.Time:
                Text(cfg, "Range", v => card.Value = v, card.Value, "18:00-06:00 or et 17-22");
                break;
            case CardType.Schedule: EditSchedule(cfg, card); break;
            case CardType.Weather:
                MatchText(cfg, "Weather", v => card.Value = v, card.Value, "Clear Skies | Rain | 7 | /regex/");
                Fill(cfg, card, here.Weather);
                Pick(cfg, "All weathers", GameData.Weathers(), v => card.Value = v);
                break;
            case CardType.Mount:
                MatchText(cfg, "Mount", v => card.Value = v, card.Value, "any, none, flying, a name, or /regex/");
                if (here.Mount.Length > 0) Fill(cfg, card, here.Mount);
                break;
            case CardType.Status:
                statusNames ??= StatusNames();
                MatchText(cfg, "Status", v => card.Value = v, card.Value, "Busy | Away from Keyboard");
                Fill(cfg, card, here.OnlineStatus);
                Ui.Next(Ui.ButtonWidth("Names..."));
                Pick(cfg, "Names", statusNames, v => card.Value = v);
                break;
            case CardType.Housing: EditHousing(cfg, card, here); break;

            case CardType.Command: EditCommand(cfg, flow, card, here); break;
            case CardType.Wait: Int(cfg, "Milliseconds", card.WaitMs, v => card.WaitMs = Math.Max(0, v)); break;
            case CardType.Notice:
                Text(cfg, "Text", v => card.Value = v, card.Value, "Entered {zone}");
                ImGui.TextDisabled("Only you see it, in your chat log.");
                TokenHint(card.Value, here);
                break;
            case CardType.SearchComment:
                Text(cfg, "Comment", v => card.Value = v, card.Value, "In {zone} on {world}");
                TokenHint(card.Value, here);
                var filled = Tokens.Fill(card.Value, here);
                if (filled.Length > ChatSender.CommentMax) ImGui.TextColored(Amber, $"Cut to {ChatSender.CommentMax} characters.");
                Ui.Wrapped(Amber, "This is your character's search info comment, the one other players see in /search and on your profile. Starflow cannot read the comment you have now, so it cannot save or put it back. Add a second comment card to After if you want a different one on the way out.");
                break;
            case CardType.Online:
                Combo(cfg, "Status", Catalog.StatusTokens, v => card.Value = v, card.Value);
                ImGui.TextDisabled("Put back on stop. Statuses with no command, such as mentor, cannot be put back.");
                break;
            case CardType.Collection:
                Text(cfg, "Collection", v => card.Value = v, card.Value, "Collection name");
                Flag(cfg, card, "Enable", "Enables the collection.", "Disables the collection.");
                ImGui.TextDisabled("Edge action. Add the opposite card to After to undo it.");
                break;
            case CardType.PluginSet:
                Text(cfg, "Plugin", v => card.Value = v, card.Value, "Plugin name");
                Pick(cfg, "Installed", Bridges.PluginNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), v => card.Value = v);
                Flag(cfg, card, "Enable##plugin", "Enables the plugin.", "Disables the plugin.");
                var keep = card.Permanent;
                if (ImGui.Checkbox("Remember", ref keep)) { card.Permanent = keep; cfg.Save(); }
                ImGui.TextDisabled(keep ? "Saved in the plugin's collection." : "Temporary. Resets when the game restarts.");
                ImGui.TextWrapped("Dalamud refuses a plugin that sits in more than one collection. Toggle the collection instead.");
                break;
            case CardType.Setting: EditSetting(cfg, card); break;
            case CardType.Glamour:
                Text(cfg, "Design", v => card.Value = v, card.Value, "Design name or id");
                Pick(cfg, "Designs", Bridges.GlamourNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), v => card.Value = v);
                ImGui.TextDisabled("Applies to you. Revert on stop asks Glamourer to revert your state.");
                break;
            case CardType.GlamourRevert:
                ImGui.TextDisabled("Asks Glamourer to revert your appearance.");
                break;
            case CardType.PenumbraMod: EditMod(cfg, card); break;
            case CardType.PenumbraCollection:
                Text(cfg, "Collection", v => card.Value = v, card.Value, "collection name");
                Pick(cfg, "Collections", Bridges.PenumbraCollections().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), v => card.Value = v);
                ImGui.TextDisabled("Edge action. Assigns your individual collection.");
                break;
            case CardType.PenumbraReset:
                Text(cfg, "Directory", v => card.Option = v, card.Option, "blank resets every temp change Starflow made");
                Text(cfg, "Name", v => card.Value = v, card.Value, "mod name");
                break;
            case CardType.Logout:
            case CardType.CloseGame:
                Flag(cfg, card, "Prompt first", "Starflow puts it in Prompts and waits for a yes.", "Sends straight away.");
                ImGui.TextDisabled("The game may show its own dialog too.");
                break;
            case CardType.Webhook: EditWebhook(cfg, card, here); break;
            default: EditReaction(cfg, card, here); break;
        }
    }

    // ---------- conditions ----------

    private void EditJobs(Configuration cfg, Card card, World here)
    {
        MatchText(cfg, "Jobs", v => card.Value = v, card.Value, "WHM | SCH, names, or /regex/");
        if (here.JobAbbr.Length > 0 && Ui.Current($"{here.JobName} ({here.JobAbbr})", id: "job"))
        {
            card.Value = AddWord(card.Value, here.JobAbbr);
            cfg.Save();
        }
        ImGui.TextDisabled("True when you are any of the ticked jobs.");
        var picked = Words(card.Value);
        var changed = false;
        foreach (var role in GameData.RoleOrder)
        {
            var jobs = GameData.Jobs().Where(j => j.Role == role).ToList();
            if (jobs.Count == 0) continue;
            if (!ImGui.TreeNodeEx(role + "##jobs", ImGuiTreeNodeFlags.DefaultOpen)) continue;
            var col = 0;
            foreach (var job in jobs)
            {
                var on = picked.Contains(job.Abbr);
                var jobLabel = $"{job.Abbr}  {job.Name}";
                if (col++ > 0) Ui.Next(Ui.CheckWidth(jobLabel));
                if (ImGui.Checkbox($"{jobLabel}##{job.Abbr}", ref on))
                {
                    if (on) picked.Add(job.Abbr); else picked.Remove(job.Abbr);
                    changed = true;
                }
            }
            ImGui.TreePop();
        }
        if (changed) { card.Value = string.Join('|', picked); cfg.Save(); }
    }

    private static void EditRoles(Configuration cfg, Card card, World here)
    {
        var picked = Words(card.Value);
        var changed = false;
        for (var i = 0; i < Catalog.Roles.Length; i++)
        {
            if (i > 0) Ui.Next(Ui.CheckWidth(Catalog.Roles[i]));
            var on = picked.Contains(Catalog.Roles[i]);
            if (!ImGui.Checkbox(Catalog.Roles[i] + "##role", ref on)) continue;
            if (on) picked.Add(Catalog.Roles[i]); else picked.Remove(Catalog.Roles[i]);
            changed = true;
        }
        if (changed) { card.Value = string.Join('|', Catalog.Roles.Where(picked.Contains)); cfg.Save(); }
        if (here.Role.Length > 0 && Ui.Current(here.Role, id: "role")) { card.Value = here.Role; cfg.Save(); }
        ImGui.TextDisabled("True when your role is any of the ticked ones.");
    }

    // One card for party size. A band, or a custom count that may include your chocobo and pet.
    private static void EditParty(Configuration cfg, Card card)
    {
        var at = card.Type == CardType.Party ? PartyBands.Length - 1 : Math.Max(0, Array.IndexOf(PartyBands, (card.Value ?? string.Empty).Trim().ToLowerInvariant()));
        if (Ui.Combo("Size", ref at, PartyBandNames, PartyBandNames.Length))
        {
            if (at == PartyBands.Length - 1)
            {
                card.Type = CardType.Party;
                card.Value = ">=2";
            }
            else
            {
                card.Type = CardType.Group;
                card.Value = PartyBands[at];
            }
            cfg.Save();
        }
        if (card.Type != CardType.Party)
        {
            ImGui.TextDisabled("Alliance zones count as alliance.");
            return;
        }
        Text(cfg, "Count", v => card.Value = v, card.Value ?? string.Empty, ">=4  <8  =1  1-4");
        var pets = card.Option == "pets";
        if (ImGui.Checkbox("Count my chocobo and pet", ref pets)) { card.Option = pets ? "pets" : string.Empty; cfg.Save(); }
        ImGui.TextDisabled("The party list leaves out your chocobo companion and summoned pet. Tick to add them.");
    }

    private static string DayChecks(string current, out bool changed)
    {
        changed = false;
        var set = (current ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.ToLowerInvariant())
            .SelectMany(d => d switch
            {
                "weekend" => new[] { "sat", "sun" },
                "weekday" or "weekdays" => new[] { "mon", "tue", "wed", "thu", "fri" },
                _ => new[] { d[..Math.Min(3, d.Length)] },
            })
            .ToHashSet();
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        for (var i = 0; i < DayNames.Length; i++)
        {
            var label = char.ToUpperInvariant(DayNames[i][0]) + DayNames[i][1..];
            if (i > 0)
            {
                ImGui.SameLine();
                var width = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(label).X;
                if (ImGui.GetCursorScreenPos().X + width > right) ImGui.NewLine();
            }
            var on = set.Contains(DayNames[i]);
            if (!ImGui.Checkbox(label + "##day", ref on)) continue;
            if (on) set.Add(DayNames[i]); else set.Remove(DayNames[i]);
            changed = true;
        }
        return string.Join(',', DayNames.Where(set.Contains));
    }

    private void EditSchedule(Configuration cfg, Card card)
    {
        Match.TryParseSchedule(card.Value, out var s);
        var mode = Math.Max(0, Array.IndexOf(Catalog.ScheduleModes, s.Mode));
        var changed = Ui.Combo("Repeat", ref mode, Catalog.ScheduleModes, Catalog.ScheduleModes.Length);
        var kind = Catalog.ScheduleModes[mode];
        var days = s.Days;
        var from = s.From == default ? DateTime.Today.ToString("yyyy-MM-dd") : s.From.ToString("yyyy-MM-dd");
        var to = s.To == default ? from : s.To.ToString("yyyy-MM-dd");
        var range = s.HasTime ? $"{s.Start / 60:00}:{s.Start % 60:00}-{s.End / 60:00}:{s.End % 60:00}" : string.Empty;

        if (kind == "weekly")
        {
            days = DayChecks(days, out var dayChanged);
            changed |= dayChanged;
            if (days.Length == 0) days = "sat,sun";
        }
        if (kind == "once")
        {
            changed |= ImGui.InputText("From date", ref from, 10);
            changed |= ImGui.InputText("To date", ref to, 10);
        }
        if (kind != "always")
        {
            changed |= ImGui.InputTextWithHint("Hours", "blank is all day, or 20:00-23:00", ref range, 16);
            ImGui.TextDisabled("Local time. A window past midnight belongs to the day it starts.");
        }

        if (changed)
        {
            var time = range.Trim().Length > 0 ? " " + range.Trim() : string.Empty;
            card.Value = kind switch
            {
                "daily" => "daily" + time,
                "weekly" => $"weekly {days}{time}",
                "once" => $"once {from.Trim()}{(to.Trim().Length > 0 && to.Trim() != from.Trim() ? ".." + to.Trim() : string.Empty)}{time}",
                _ => "always",
            };
            cfg.Save();
        }
        Text(cfg, "Text", v => card.Value = v, card.Value, "weekly sat,sun 18:00-02:00");
        var ok = Match.TryParseSchedule(card.Value, out _);
        if (!ok) ImGui.TextColored(Red, "Not a schedule Starflow can read.");
        else ImGui.TextDisabled(Match.Schedule(DateTime.Now, card.Value) ? "Inside the window now." : "Outside the window now.");
    }

    private void EditHousing(Configuration cfg, Card card, World here)
    {
        AddressSpec.TryParse(card.Value, out var kind, out var district, out var ward, out var plot, out var room, out var sub);
        var changed = false;
        var k = Math.Max(0, Array.IndexOf(AddressSpec.Kinds, kind));
        changed |= Ui.Combo("Kind", ref k, AddressSpec.Kinds, AddressSpec.Kinds.Length);
        string[] districts = ["(any district)", .. AddressSpec.Districts];
        var d = Math.Max(0, Array.IndexOf(districts, district));
        changed |= Ui.Combo("District", ref d, districts, districts.Length);
        changed |= ImGui.InputInt("Ward", ref ward);
        var kindName = AddressSpec.Kinds[k];
        var hasPlot = kindName is "House" or "Chamber";
        if (hasPlot) changed |= ImGui.InputInt("Plot", ref plot);
        if (kindName is "Apartment" or "Chamber") changed |= ImGui.InputInt(kindName == "Apartment" ? "Apartment" : "Room", ref room);
        if (hasPlot && plot > 0)
            ImGui.TextDisabled(plot > 30 ? "Plot 31-60: in the subdivision." : "Plot 1-30: in the main ward.");
        else
        {
            changed |= ImGui.Checkbox("Subdivision", ref sub);
            ImGui.TextDisabled(kindName == "Apartment" ? "Tick for the subdivision's apartment building." : "Tick for the ward's second half.");
        }
        ImGui.TextDisabled("0 is any.");
        if (changed)
        {
            card.Value = AddressSpec.Encode(kindName, d == 0 ? string.Empty : districts[d], Math.Clamp(ward, 0, 30), Math.Clamp(plot, 0, 60), Math.Max(0, room), sub);
            cfg.Save();
        }

        if (here.Housing.InWard && Ui.Current(here.Housing.Line(), id: "house"))
        {
            var h = here.Housing;
            var name = h.Kind switch
            {
                Residence.House => "House",
                Residence.Apartment => "Apartment",
                Residence.Chamber => "Chamber",
                _ => "Any",
            };
            card.Value = AddressSpec.Encode(name, h.District, h.Ward, h.Plot, h.Room, h.Subdivision);
            cfg.Save();
        }
    }

    // ---------- actions ----------

    private void EditCommand(Configuration cfg, FlowItem flow, Card card, World here)
    {
        Text(cfg, "Command", v => card.Value = v, card.Value, "/echo {zone}");
        if (ImGui.Button("Browse commands...")) ImGui.OpenPopup("commands");
        Tip("Every game command, emote, and plugin command. Job change, mounts, minions, and emotes are all here.");
        CommandBrowser(cfg, card);
        Int(cfg, "Repeat", card.Repeat, v => card.Repeat = Math.Clamp(v, 1, 30));
        Int(cfg, "Gap ms", card.WaitMs, v => card.WaitMs = Math.Max(0, v));
        if (Catalog.IsReactive(flow) && Catalog.IsCombatCommand(card.Value))
            Ui.Wrapped(Red, "This flow reacts to HP, effects, enmity, damage, or chat, so it cannot send actions or macros. Starflow keeps it off until this card changes.");
        TokenHint(card.Value, here);

        var audience = GameData.Audience(card.Value);
        var repeats = card.Repeat > 1 || flow.Mode == RunMode.Auto;
        if (audience == "public")
            Ui.Wrapped(Red, "This posts in a public channel other players read." + (repeats ? " It can repeat on its own, which reads as spam and can get you reported. Prefer Prompt mode, one repeat, or /echo." : " Prefer /echo for notes to yourself."));
        else if (audience == "group")
            Ui.Wrapped(Amber, "This posts to your party, FC, or a linkshell." + (repeats ? " It can repeat on its own; make sure the group wants that." : string.Empty));
        else if (audience == "tell")
            Ui.Wrapped(Amber, "This sends a tell to another player." + (repeats ? " Repeating tells read as harassment." : string.Empty));
    }

    private void CommandBrowser(Configuration cfg, Card card)
    {
        if (!ImGui.BeginPopup("commands")) return;
        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(360);
        ImGui.InputTextWithHint("##find", "Search commands and descriptions", ref commandFilter, 60);
        var list = GameData.Commands()
            .Where(c => commandFilter.Length == 0 || c.Command.Contains(commandFilter, StringComparison.OrdinalIgnoreCase) || c.Help.Contains(commandFilter, StringComparison.OrdinalIgnoreCase))
            .Take(300)
            .ToList();
        ImGui.BeginChild("cmds", new Vector2(560, 360), false);
        foreach (var command in list)
        {
            if (ImGui.Selectable($"{command.Command}##{command.Source}{command.Command}"))
            {
                card.Value = command.Command + " ";
                cfg.Save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine(150);
            ImGui.TextDisabled(command.Source == "Plugin" ? "[plugin] " + command.Help : command.Help);
        }
        if (list.Count == 0) ImGui.TextDisabled("Nothing matches.");
        ImGui.EndChild();
        ImGui.EndPopup();
    }

    private void EditWebhook(Configuration cfg, Card card, World here)
    {
        Text(cfg, "URL", v => card.Value = v, card.Value, "https://discord.com/api/webhooks/...");
        if (card.Value.Length > 0 && !WebhookText.ValidUrl(card.Value, out var urlError)) ImGui.TextColored(Red, urlError);
        Text(cfg, "Content type", v => card.Option = v, card.Option ?? string.Empty, "application/json");
        ImGui.TextUnformatted("Body");
        var body = card.Extra ?? string.Empty;
        if (ImGui.InputTextMultiline("##body", ref body, 8000, new Vector2(-1, 140))) { card.Extra = body; cfg.Save(); }
        if (ImGui.SmallButton("Discord template")) { card.Extra = WebhookText.DiscordTemplate; card.Option = "application/json"; cfg.Save(); }
        Tip("Discord wants {\"content\": \"...\"}. Anything else that takes a POST works too; write the body it expects.");
        Ui.Next(Ui.ButtonWidth("Send test"));
        if (ImGui.SmallButton("Send test") && WebhookText.ValidUrl(card.Value, out _))
        {
            var sent = Webhook.Send(card, here);
            pickMessage = sent.Ok ? "Sent. A failure would show in chat." : sent.Detail;
        }
        if (pickMessage.Length > 0) ImGui.TextDisabled(pickMessage);
        var type = string.IsNullOrWhiteSpace(card.Option) ? "application/json" : card.Option;
        var json = type.Contains("json", StringComparison.OrdinalIgnoreCase);
        var filled = WebhookText.FillBody(card.Extra ?? string.Empty, here, json);
        var jsonError = WebhookText.JsonError(filled, type);
        if (jsonError is not null) Ui.Wrapped(Red, "Not valid JSON after tokens fill: " + jsonError);
        TokenHint(card.Extra ?? string.Empty, here);
        ImGui.TextDisabled("The URL is a secret. Anyone with it can post there; do not share flows that contain it.");
    }

    private void EditSetting(Configuration cfg, Card card)
    {
        var section = card.Option == "Ui" ? 1 : 0;
        string[] sections = ["System", "Ui"];
        if (Ui.Combo("Section", ref section, sections, sections.Length))
        {
            card.Option = section == 1 ? "Ui" : "System";
            cfg.Save();
        }

        ImGui.InputTextWithHint("Find", "type part of a name", ref optionFilter, 40);
        Tip("Matches the option name or what it does, such as shadow or chat bubble.");
        var names = Actions.OptionNames(card.Option)
            .Where(n => optionFilter.Length == 0 || n.Contains(optionFilter, StringComparison.OrdinalIgnoreCase) || OptionGuide.Describe(n).Text.Contains(optionFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Take(1000)
            .ToArray();
        if (ImGui.BeginCombo("Option", card.Value.Length > 0 ? card.Value : "pick one"))
        {
            foreach (var name in names)
            {
                var guide = OptionGuide.Describe(name);
                if (ImGui.Selectable($"{name}##opt", name == card.Value))
                {
                    card.Value = name;
                    probeMessage = string.Empty;
                    cfg.Save();
                }
                if (ImGui.IsItemHovered()) Ui.Tip($"{guide.Group}: {guide.Text}");
            }
            ImGui.EndCombo();
        }
        Ui.Next(Ui.ButtonWidth("Look up options"));
        if (ImGui.SmallButton("Look up options")) plugin.OpenHelp("options");
        Tip("Every option with what it does, in the help window.");
        Text(cfg, "Name", v => card.Value = v, card.Value, "Config option");
        Text(cfg, "Value", v => card.Extra = v, card.Extra, "0");

        if (card.Value.Length > 0 && !Actions.RealOption(card.Option, card.Value))
            ImGui.TextColored(Red, $"{card.Value} is not a real option.");
        else if (card.Value.Length > 0)
        {
            ImGui.TextDisabled("Now: " + Actions.ReadOption(card.Option, card.Value));
            if (ImGui.SmallButton("Can Starflow write this?"))
            {
                var access = Actions.Probe(card.Option, card.Value, out var detail);
                probeMessage = $"{access}: {detail}";
            }
            Tip("Writes the value that is already there and reads it back. Nothing changes.");
            Ui.Next(Ui.ButtonWidth("Check every option"));
            if (ImGui.SmallButton("Check every option") && ImGui.GetIO().KeyShift) probeMessage = Actions.ProbeAll();
            Tip("Hold Shift and click. Writes every option's own value back and saves a report. Nothing changes, but graphics options may flicker once.");
            if (probeMessage.Length > 0) ImGui.TextWrapped(probeMessage);
        }
        if (card.Value.Length > 0)
        {
            var guide = OptionGuide.Describe(card.Value);
            ImGui.TextDisabled(guide.Group);
            ImGui.TextWrapped(guide.Text);
        }
    }

    private void EditMod(Configuration cfg, Card card)
    {
        ImGui.InputTextWithHint("Find mod", "type part of a name", ref modFilter, 60);
        var mods = Bridges.Mods()
            .Where(m => modFilter.Length == 0 || m.Name.Contains(modFilter, StringComparison.OrdinalIgnoreCase) || m.Dir.Contains(modFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Take(300)
            .ToList();
        var labels = mods.Select(m => m.Name == m.Dir ? m.Name : $"{m.Name}  ·  {m.Dir}").ToArray();
        var at = mods.FindIndex(m => m.Dir == card.Option);
        var pick = Math.Max(0, at);
        if (labels.Length > 0 && Ui.Combo("Mod", ref pick, labels, labels.Length))
        {
            card.Option = mods[pick].Dir;
            card.Value = mods[pick].Name;
            card.Extra = string.Empty;
            cfg.Save();
        }
        if (labels.Length == 0) ImGui.TextDisabled("Penumbra lists no mods, or is not loaded.");
        Text(cfg, "Directory", v => card.Option = v, card.Option, "mod directory");

        var inherit = card.Inherit;
        if (ImGui.Checkbox("Inherit", ref inherit)) { card.Inherit = inherit; cfg.Save(); }
        Tip("Use the parent collection's settings for this mod.");
        if (!card.Inherit)
        {
            Ui.Next(Ui.CheckWidth("Enabled"));
            var on = card.Flag;
            if (ImGui.Checkbox("Enabled", ref on)) { card.Flag = on; cfg.Save(); }
            Int(cfg, "Priority", card.Number, v => card.Number = v);
        }
        var permanent = card.Permanent;
        if (ImGui.Checkbox("Permanent", ref permanent)) { card.Permanent = permanent; cfg.Save(); }
        ImGui.TextDisabled(permanent ? "Writes your collection. Revert writes the old values back." : "Temporary. Revert removes Starflow's temporary setting.");

        if (ImGui.Button("Read current") && card.Option.Length > 0)
        {
            if (Bridges.TryReadMod(card.Option, card.Value, out var now, out var error))
            {
                card.Flag = now.Enabled;
                card.Number = now.Priority;
                card.Inherit = now.Inherit;
                card.Extra = ModOptions.Format(now.Settings);
                pickMessage = "Copied the mod's current settings.";
                cfg.Save();
            }
            else pickMessage = error;
        }
        if (pickMessage.Length > 0) ImGui.TextWrapped(pickMessage);

        if (card.Inherit || card.Option.Length == 0) return;
        var groups = Bridges.ModGroups(card.Option, card.Value);
        if (groups.Count == 0) return;
        ImGui.Separator();
        ImGui.TextDisabled("Options. Unticked groups keep what the mod has.");
        var chosen = ModOptions.Parse(card.Extra);
        var changed = false;
        foreach (var (group, (options, type)) in groups)
        {
            ImGui.PushID(group);
            var use = chosen.ContainsKey(group);
            if (ImGui.Checkbox(group, ref use))
            {
                if (use) chosen[group] = options.Length > 0 && type == 0 ? [options[0]] : [];
                else chosen.Remove(group);
                changed = true;
            }
            if (use && chosen.TryGetValue(group, out var picked))
            {
                ImGui.Indent();
                if (type == 0)
                {
                    var index = Math.Max(0, Array.IndexOf(options, picked.FirstOrDefault()));
                    if (options.Length > 0 && Ui.Combo("##single", ref index, options, options.Length))
                    {
                        chosen[group] = [options[index]];
                        changed = true;
                    }
                }
                else
                {
                    foreach (var option in options)
                    {
                        var on = picked.Contains(option);
                        if (!ImGui.Checkbox(option, ref on)) continue;
                        if (on) picked.Add(option); else picked.Remove(option);
                        changed = true;
                    }
                }
                ImGui.Unindent();
            }
            ImGui.PopID();
        }
        if (changed)
        {
            card.Extra = ModOptions.Format(chosen);
            cfg.Save();
        }
    }

    // ---------- helpers ----------

    private static string[] StatusNames()
    {
        try
        {
            return Plugin.DataManager.GetExcelSheet<OnlineStatus>()
                .Select(r => r.Name.ToString())
                .Where(n => n.Length > 0)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static HashSet<string> Words(string? value) =>
        (value ?? string.Empty).Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string AddWord(string? value, string word)
    {
        var words = Words(Match.IsRegex(value ?? string.Empty, out _) ? string.Empty : value);
        words.Add(word);
        return string.Join('|', words);
    }

    private static void TokenHint(string text, World here)
    {
        ImGui.TextDisabled("Tokens such as {zone} {job} {world} fill when it runs. Help lists all of them.");
        if ((text ?? string.Empty).Contains('{')) ImGui.TextWrapped("Now: " + Tokens.Fill(text ?? string.Empty, here));
    }

    // "Current: ..." fills the card's value with what the game says now.
    private static void Fill(Configuration cfg, Card card, string value, string? shown = null)
    {
        if (!Ui.Current(value, shown, card.Type + (shown ?? string.Empty))) return;
        card.Value = value;
        cfg.Save();
    }

    private static void Pick(Configuration cfg, string label, IReadOnlyList<string> items, Action<string> set)
    {
        if (items.Count == 0) return;
        if (ImGui.Button(label + "...")) ImGui.OpenPopup("pick:" + label);
        if (!ImGui.BeginPopup("pick:" + label)) return;
        ImGui.BeginChild("scroll", new Vector2(320, Math.Min(360, items.Count * ImGui.GetTextLineHeightWithSpacing() + 8)), false);
        foreach (var item in items)
        {
            if (!ImGui.Selectable(item)) continue;
            set(item);
            cfg.Save();
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndChild();
        ImGui.EndPopup();
    }

    private static void Flag(Configuration cfg, Card card, string label, string on, string off)
    {
        var value = card.Flag;
        if (ImGui.Checkbox(label, ref value)) { card.Flag = value; cfg.Save(); }
        ImGui.TextDisabled(value ? on : off);
    }

    private static void Text(Configuration cfg, string label, Action<string> set, string current, string hint)
    {
        var value = current ?? string.Empty;
        if (!ImGui.InputTextWithHint(label, hint, ref value, 240)) return;
        set(value);
        cfg.Save();
    }

    // A text field that matches words or a /regex/. A pattern that does not compile shows why.
    private static void MatchText(Configuration cfg, string label, Action<string> set, string current, string hint)
    {
        Text(cfg, label, set, current, hint);
        var error = Match.RegexError(current ?? string.Empty);
        if (error is not null) Ui.Wrapped(Red, "Regex: " + error);
        else if (Match.IsRegex(current ?? string.Empty, out _)) ImGui.TextDisabled("Regular expression, no case.");
    }

    private static void Int(Configuration cfg, string label, int current, Action<int> set)
    {
        var value = current;
        if (!ImGui.InputInt(label, ref value)) return;
        set(value);
        cfg.Save();
    }

    private static void Combo(Configuration cfg, string label, string[] items, Action<string> set, string current)
    {
        var index = Math.Max(0, Array.FindIndex(items, i => i.Equals(current, StringComparison.OrdinalIgnoreCase)));
        if (!Ui.Combo(label, ref index, items, items.Length)) return;
        set(items[index]);
        cfg.Save();
    }
}

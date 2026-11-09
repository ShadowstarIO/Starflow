using System;
using System.Collections.Generic;

namespace Starflow;

public sealed record CardDesc(string Type, string Label, string Hint);

public static class Catalog
{
    public static readonly string[] States = Alpha(
    [
        "InCombat", "InDuty", "DutyReady", "InQueue", "Cutscene", "Mounted", "Flying",
        "Swimming", "Diving", "Crafting", "Gathering", "Dead", "Occupied", "BetweenAreas",
        "Casting", "Fishing", "PvP", "Housing", "WeaponDrawn", "InParty", "HasTarget",
        "Emoting", "Performing", "Trade", "RolePlaying", "Sitting", "Talking", "DeepDungeon",
        "Town", "Overworld", "Inn", "GoldSaucer",
    ]);

    public static readonly string[] Places = Alpha(
    [
        "Town", "Overworld", "Inn", "Dungeon", "Trial", "Raid", "Alliance", "Housing",
        "PvP", "GoldSaucer", "DeepDungeon", "Field", "Sanctuary", "Indoor",
    ]);

    public static readonly string[] Roles = ["Tank", "Healer", "DPS", "Crafter", "Gatherer"];

    public static string[] Alpha(string[] items)
    {
        Array.Sort(items, StringComparer.OrdinalIgnoreCase);
        return items;
    }

    public static readonly string[] Duties = ["in", "out", "queue", "any", "dungeon", "trial", "raid", "alliance", "pvp", "deep", "field"];

    public static readonly string[] Targets = ["none", "any", "player", "npc"];

    public static readonly string[] Groups = ["solo", "light", "full", "alliance", "party"];

    public static readonly string[] Accounts = ["home", "visiting"];

    public static readonly string[] StatusTokens = ["online", "away", "busy", "rp", "lfp", "meld"];

    public static readonly string[] ScheduleModes = ["always", "daily", "weekly", "once"];

    public static IReadOnlyList<CardDesc> For(Section section)
    {
        if (section is Section.When or Section.Until)
        {
            return Sorted(
            [
                new(CardType.State, "State", "In combat, in duty, mounted, crafting"),
                new(CardType.Duty, "Duty", "In a duty, or a kind of duty"),
                new(CardType.Place, "Place", "Town, inn, dungeon, field"),
                new(CardType.Zone, "Zone", "Zone name or #id"),
                new(CardType.Region, "Region", "Region name"),
                new(CardType.Housing, "Housing address", "District, ward, plot, room"),
                new(CardType.Group, "Party size", "Solo, light, full, alliance, or a count"),
                new(CardType.Job, "Job", "One or more jobs"),
                new(CardType.Role, "Role", "One or more roles"),
                new(CardType.Level, "Level", "Compare the level"),
                new(CardType.World, "World", "Current or home world"),
                new(CardType.Account, "Home or visiting", "Home world versus current world"),
                new(CardType.DataCenter, "Data center", "Data center name"),
                new(CardType.Schedule, "Schedule", "Always, daily, weekly, one date. Local time"),
                new(CardType.Time, "Clock range", "Local, or Eorzea with et"),
                new(CardType.Weekday, "Weekday", "Local day names"),
                new(CardType.Weather, "Weather", "Weather name or id"),
                new(CardType.Mount, "Mount", "Any mount, or a mount name"),
                new(CardType.Nearby, "Nearby count", "Players within 30 yalms"),
                new(CardType.NearbyName, "Nearby name", "A player name within 30 yalms"),
                new(CardType.Status, "Online status", "The status shown on your name"),
                new(CardType.Target, "Target", "None, any, player, NPC, or a name"),
                new(CardType.Plugin, "Plugin loaded", "Plugin name or internal name"),
                new(CardType.Vitals, "HP, MP, GP, CP", "Yours, your target's, or the party's, as a number or percent"),
                new(CardType.Effect, "Effect", "A buff or debuff on you, your target, or the party"),
                new(CardType.Enmity, "Enmity", "Enemies targeting you"),
                new(CardType.Hurt, "Took damage", "Your HP just dropped, from a player or an NPC"),
                new(CardType.PartyDown, "Party down", "Party members at 0 HP"),
                new(CardType.Chat, "Chat message", "Someone said something, in channels you pick"),
            ]);
        }

        return Sorted(
        [
            new(CardType.Command, "Command or macro", "Repeats up to 30 times"),
            new(CardType.Wait, "Wait", "Delay before the next card"),
            new(CardType.Notice, "Chat notice", "Text in your chat log"),
            new(CardType.Setting, "Game setting", "A system or UI option"),
            new(CardType.Online, "Online status", "Away, busy, role-playing"),
            new(CardType.SearchComment, "Search comment", "Your search info comment"),
            new(CardType.Collection, "Plugin collection", "Enable or disable a collection"),
            new(CardType.PluginSet, "Plugin on or off", "One plugin, saved or temporary"),
            new(CardType.Glamour, "Glamourer design", "Apply a design to you"),
            new(CardType.GlamourRevert, "Glamourer revert", "Revert your appearance"),
            new(CardType.PenumbraMod, "Penumbra mod", "Enabled, priority, and options"),
            new(CardType.PenumbraCollection, "Penumbra collection", "Assign your collection"),
            new(CardType.PenumbraReset, "Penumbra reset temp", "One mod, or all Starflow changes"),
            new(CardType.Logout, "Log out", "Prompts first unless you turn that off"),
            new(CardType.CloseGame, "Close game", "Prompts first unless you turn that off"),
            new(CardType.Webhook, "Webhook", "POST to Discord or any URL"),
            new(CardType.Lifestream, "Lifestream travel", "Home, FC, apartment, inn, a world, or an aetheryte"),
        ]);
    }

    private static IReadOnlyList<CardDesc> Sorted(CardDesc[] cards)
    {
        Array.Sort(cards, (a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return cards;
    }

    public static Card Make(Section section, string type)
    {
        var card = new Card { Section = section, Type = type };
        switch (type)
        {
            case CardType.State: card.Value = "InDuty"; break;
            case CardType.Duty: card.Value = "in"; break;
            case CardType.Place: card.Value = "Town"; break;
            case CardType.Party: card.Value = ">=1"; break;
            case CardType.Group: card.Value = "full"; break;
            case CardType.Account: card.Value = "home"; break;
            case CardType.Nearby: card.Value = ">=1"; break;
            case CardType.Level: card.Value = ">=1"; break;
            case CardType.Time: card.Value = "18:00-06:00"; break;
            case CardType.Weekday: card.Value = "sat,sun"; break;
            case CardType.Schedule: card.Value = "daily 18:00-23:00"; break;
            case CardType.Mount: card.Value = "any"; break;
            case CardType.Housing: card.Value = "House|Mist|0|0"; break;
            case CardType.Target: card.Value = "any"; break;
            case CardType.Role: card.Value = "DPS"; break;
            case CardType.Status: card.Value = "Busy"; break;
            case CardType.Command: card.Repeat = 1; card.WaitMs = 400; break;
            case CardType.Wait: card.WaitMs = 500; break;
            case CardType.Webhook: card.Option = "application/json"; card.Extra = WebhookText.DiscordTemplate; break;
            case CardType.Vitals: card.Option = "hp"; card.Extra = "me"; card.Value = "<50%"; break;
            case CardType.Effect: card.Extra = "me"; break;
            case CardType.Enmity: card.Value = ">=1"; break;
            case CardType.Hurt: card.Option = "any"; card.Number = 3; break;
            case CardType.PartyDown: card.Value = ">=1"; break;
            case CardType.Chat: card.Option = "tell"; card.Number = 5; card.Permanent = false; break;
            case CardType.Lifestream: card.Value = "home"; break;
            case CardType.Setting:
                card.Option = "System";
                card.Value = "SoundMaster";
                card.Extra = "100";
                break;
            case CardType.Online: card.Value = "online"; break;
            case CardType.PenumbraMod: card.Flag = true; card.Number = 0; break;
            case CardType.Logout:
            case CardType.CloseGame:
                card.Flag = true;
                break;
        }
        return card;
    }

    // A card that cannot do anything yet. The flow stays off until these are filled.
    public static string? Missing(Card card)
    {
        bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        return card.Type switch
        {
            CardType.Command when Blank(card.Value) => "command",
            CardType.Command when Tokens.HeadIsToken(card.Value) => "command word that is not a token",
            CardType.Collection when Blank(card.Value) => "collection name",
            CardType.PluginSet when Blank(card.Value) => "plugin name",
            CardType.Glamour when Blank(card.Value) => "design",
            CardType.PenumbraMod when Blank(card.Option) => "mod directory",
            CardType.PenumbraCollection when Blank(card.Value) => "collection name",
            CardType.SearchComment when Blank(card.Value) => "comment",
            CardType.Webhook when !WebhookText.ValidUrl(card.Value, out _) => "webhook URL",
            CardType.Lifestream when Blank(card.Value) => "destination",
            CardType.Chat when Blank(card.Option) => "channel",
            CardType.Setting when Blank(card.Value) => "option",
            CardType.Setting when OptionGuide.Blocked(card.Value) => "option Starflow is allowed to write",
            CardType.Housing when !AddressSpec.TryParse(card.Value, out _, out var d, out _, out _, out _, out _)
                || (d.Length == 0 && !card.Value.Trim().Equals("any", StringComparison.OrdinalIgnoreCase)) => "district",
            CardType.Schedule when !Match.TryParseSchedule(card.Value, out _) => "schedule",
            _ when CardType.IsCondition(card.Type) && card.Type is not (CardType.Mount or CardType.Hurt or CardType.Chat or CardType.Enmity or CardType.PartyDown) && Blank(card.Value) => "value",
            _ => null,
        };
    }

    public static List<string> Missing(FlowItem flow)
    {
        var list = new List<string>();
        var reactive = IsReactive(flow);
        foreach (var section in new[] { Section.When, Section.Do, Section.Until, Section.After })
        {
            var cards = flow.In(section);
            for (var i = 0; i < cards.Count; i++)
            {
                var gap = Missing(cards[i]);
                if (gap is not null) list.Add($"{Key(section, i)} needs a {gap}");
                if (reactive && cards[i].Type == CardType.Command && IsCombatCommand(cards[i].Value))
                    list.Add($"{Key(section, i)} uses an action or macro, which a flow that reacts to combat or chat holds off on");
            }
        }
        return list;
    }

    public static string Label(Card card)
    {
        var not = card.Not ? "not " : string.Empty;
        var text = card.Type switch
        {
            CardType.State => $"{not}{Blank(card.Value, "state")}",
            CardType.Job => $"{not}job {Blank(card.Value, "abbr")}",
            CardType.Role => $"{not}role {Blank(card.Value, "role")}",
            CardType.Zone => $"{not}zone {Blank(card.Value, "name")}",
            CardType.Region => $"{not}region {Blank(card.Value, "name")}",
            CardType.World => $"{not}world {Blank(card.Value, "name")}",
            CardType.DataCenter => $"{not}dc {Blank(card.Value, "name")}",
            CardType.Account => $"{not}{Blank(card.Value, "home")} world",
            CardType.Place => $"{not}place {Blank(card.Value, "place")}",
            CardType.Housing => not + AddressSpec.Label(card.Value),
            CardType.Duty => $"{not}duty {Blank(card.Value, "in")}",
            CardType.Party => $"{not}party {Blank(card.Value, ">=1")}",
            CardType.Group => $"{not}group {Blank(card.Value, "size")}",
            CardType.Nearby => $"{not}nearby {Blank(card.Value, ">=1")}",
            CardType.NearbyName => $"{not}near {Blank(card.Value, "name")}",
            CardType.Level => $"{not}level {Blank(card.Value, ">=1")}",
            CardType.Time => $"{not}time {Blank(card.Value, "range")}",
            CardType.Weekday => $"{not}day {Blank(card.Value, "day")}",
            CardType.Schedule => $"{not}{Blank(card.Value, "schedule")}",
            CardType.Weather => $"{not}weather {Blank(card.Value, "name")}",
            CardType.Mount => $"{not}mount {Blank(card.Value, "any")}",
            CardType.Status => $"{not}status {Blank(card.Value, "name")}",
            CardType.Target => $"{not}target {Blank(card.Value, "any")}",
            CardType.Plugin => $"{not}plugin {Blank(card.Value, "name")}",
            CardType.Vitals => $"{not}{Who(card.Extra)}{Blank(card.Option, "hp").ToUpperInvariant()} {Blank(card.Value, "<50%")}",
            CardType.Effect => $"{not}{Who(card.Extra)}effect {Blank(card.Value, "name")}",
            CardType.Enmity => $"{not}enmity {Blank(card.Value, ">=1")}",
            CardType.Hurt => $"{not}hurt" + (card.Option is "player" or "npc" ? $" by {card.Option}" : string.Empty) + (Blank(card.Value) ? string.Empty : $" {card.Value}+") + $" {(card.Number > 0 ? card.Number : 3)}s",
            CardType.PartyDown => $"{not}party down {Blank(card.Value, ">=1")}",
            CardType.Chat => $"{not}chat {Blank(card.Option, "any")}" + (Blank(card.Extra) ? string.Empty : $" from {card.Extra.Trim()}") + (Blank(card.Value) ? string.Empty : $" \"{card.Value.Trim()}\""),
            CardType.Command => CommandLabel(card),
            CardType.Wait => card.WaitMs <= 0 ? "Wait" : $"Wait {card.WaitMs} ms",
            CardType.Notice => string.IsNullOrWhiteSpace(card.Value) ? "Chat notice" : $"Say {card.Value}",
            CardType.Setting => $"{Blank(card.Value, "option")} = {Blank(card.Extra, "?")}",
            CardType.Online => $"Status {Blank(card.Value, "online")}",
            CardType.SearchComment => $"Comment {Blank(card.Value, "text")}",
            CardType.Collection => $"{(card.Flag ? "Enable" : "Disable")} collection {Blank(card.Value, "name")}",
            CardType.PluginSet => $"{(card.Flag ? "Enable" : "Disable")} plugin {Blank(card.Value, "name")}" + (card.Permanent ? "" : " (temp)"),
            CardType.Glamour => $"Glamour {Blank(card.Value, "design")}",
            CardType.GlamourRevert => "Glamour revert",
            CardType.PenumbraMod => PenumbraLabel(card),
            CardType.PenumbraCollection => $"Penumbra collection {Blank(card.Value, "name")}",
            CardType.PenumbraReset => string.IsNullOrWhiteSpace(card.Option) ? "Penumbra reset all temp" : $"Penumbra reset {Blank(card.Value, card.Option)}",
            CardType.Logout => card.Flag ? "Log out (prompts)" : "Log out",
            CardType.CloseGame => card.Flag ? "Close game (prompts)" : "Close game",
            CardType.Webhook => "Webhook " + WebhookText.Host(card.Value),
            CardType.Lifestream => $"Travel {Blank(card.Value, "home")}",
            _ => card.Type,
        };
        return card.Keep && CardType.IsSlotted(card.Type) ? text + " (keep)" : text;
    }

    public static string Key(Section section, int index)
    {
        var prefix = section switch
        {
            Section.When => "W",
            Section.Do => "D",
            Section.Until => "U",
            Section.After => "A",
            _ => "C",
        };
        return prefix + (index + 1);
    }

    // Short words a person types, mapped to a piece of the status name the client shows.
    public static string StatusAlias(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "away" or "afk" => "Away",
        "rp" or "roleplaying" or "role-playing" => "Role",
        "lfp" or "lookingforparty" => "Party",
        "meld" or "lookingtomeld" => "Meld",
        "busy" => "Busy",
        "online" => "Online",
        var other => other,
    };

    public static FlowItem DutyTemplate()
    {
        var flow = new FlowItem
        {
            Name = "Duty",
            Enabled = false,
            Mode = RunMode.Auto,
            OnStop = StopMode.Revert,
            Priority = 10,
            IgnoreQuiet = true,
            Notes = "Fill the collection names, then turn the flow on. Do swaps the town collection for the duty one. After swaps them back. Add settings and macros to Do; Revert puts the settings back when you leave.",
        };
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.State, Value = "InDuty" });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Collection, Value = "", Flag = false });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Collection, Value = "", Flag = true });
        flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Collection, Value = "", Flag = false });
        flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Collection, Value = "", Flag = true });
        return flow;
    }

    public static FlowItem TownTemplate()
    {
        var flow = new FlowItem
        {
            Name = "Town",
            Enabled = false,
            Mode = RunMode.Auto,
            OnStop = StopMode.Revert,
            Priority = 0,
            Notes = "Turns on in town, off in a duty. Fill the comment, or swap it for the status, design, or command you want.",
        };
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.Place, Value = "Town" });
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.State, Value = "InDuty", Not = true });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.SearchComment, Value = "" });
        flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Notice, Value = "Town flow ended." });
        return flow;
    }

    public static FlowItem HousingTemplate()
    {
        var flow = new FlowItem
        {
            Name = "At home",
            Enabled = false,
            Mode = RunMode.Auto,
            OnStop = StopMode.Revert,
            Priority = 5,
            Notes = "Pick the district, ward, and plot, and fill the comment. Then turn the flow on.",
        };
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.Housing, Value = "House||0|0" });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Online, Value = "rp" });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.SearchComment, Value = "" });
        return flow;
    }

    public static FlowItem ScheduleTemplate()
    {
        var flow = new FlowItem
        {
            Name = "Evening",
            Enabled = false,
            Mode = RunMode.Ask,
            OnStop = StopMode.Revert,
            Priority = 0,
            Notes = "Set the window and fill the command. Prompt mode offers it once per window.",
        };
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.Schedule, Value = "weekly mon,tue,wed,thu,fri 20:00-23:00" });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Command, Value = "", Repeat = 1, WaitMs = 400 });
        flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Notice, Value = "Evening window closed." });
        return flow;
    }

    private static string CommandLabel(Card card)
    {
        var cmd = string.IsNullOrWhiteSpace(card.Value) ? "/command" : card.Value.Trim();
        var times = Math.Clamp(card.Repeat, 1, 30);
        return times <= 1 ? cmd : $"{cmd} ×{times}";
    }

    private static string PenumbraLabel(Card card)
    {
        var name = string.IsNullOrWhiteSpace(card.Value) ? card.Option : card.Value;
        if (string.IsNullOrWhiteSpace(name)) name = "mod";
        var state = card.Inherit ? "inherit" : card.Flag ? "on" : "off";
        var how = card.Permanent ? "set" : "temp";
        var opts = string.IsNullOrWhiteSpace(card.Extra) ? string.Empty : " +options";
        return $"Penumbra {how} {name} {state} @{card.Number}{opts}";
    }

    private static string Blank(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static string Who(string who) => who switch
    {
        "target" => "target ",
        "party-any" => "a party member's ",
        "party-all" => "everyone's ",
        "party" => "party ",
        "party-others" => "a party member's ",
        _ => string.Empty,
    };

    // Commands that use actions or run macros. A flow that reacts to HP, effects, enmity, damage, or chat cannot send them,
    // so Starflow stays a setup tool and does not fight for you.
    public static readonly string[] CombatCommands =
    [
        "/ac", "/action", "/pvpaction", "/pvpac", "/blueaction", "/petaction", "/pac", "/gaction", "/generalaction",
        "/macro", "/bac",
    ];

    public static bool IsCombatCommand(string command)
    {
        var head = (command ?? string.Empty).Trim().Split(' ', 2)[0].ToLowerInvariant();
        if (head.Length > 0 && !head.StartsWith('/')) head = "/" + head;
        return Array.IndexOf(CombatCommands, head) >= 0;
    }

    public static bool IsReactive(FlowItem flow) =>
        flow.Cards.Exists(c => c.Section is Section.When or Section.Until && CardType.IsReactive(c.Type));
}

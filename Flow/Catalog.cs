using System;
using System.Collections.Generic;

namespace Flow;

public sealed record CardDesc(string Type, string Label, string Hint);

public sealed record SettingDesc(string Section, string Name, bool Float, string Hint);

public static class Catalog
{
    public static readonly string[] States =
    [
        "InCombat", "InDuty", "DutyReady", "InQueue", "Cutscene", "Mounted", "Flying",
        "Swimming", "Diving", "Crafting", "Gathering", "Dead", "Occupied", "BetweenAreas",
        "Casting", "Fishing", "PvP", "Housing", "WeaponDrawn", "InParty", "HasTarget",
        "Emoting", "Performing", "Trade", "RolePlaying", "Sitting", "Talking", "DeepDungeon",
        "Town", "Overworld", "Inn", "GoldSaucer",
    ];

    public static readonly string[] Places =
    [
        "Town", "Overworld", "Inn", "Dungeon", "Trial", "Raid", "Alliance", "Housing",
        "PvP", "GoldSaucer", "DeepDungeon", "Field", "Sanctuary", "Indoor",
    ];

    public static readonly string[] Roles = ["Tank", "Healer", "DPS", "Crafter", "Gatherer"];

    public static readonly string[] Duties = ["in", "out", "queue", "any", "dungeon", "trial", "raid", "alliance", "pvp", "deep", "field"];

    public static readonly string[] Targets = ["none", "any", "player", "npc"];

    public static readonly string[] Groups = ["solo", "light", "full", "alliance", "party"];

    public static readonly string[] Accounts = ["home", "visiting"];

    public static readonly string[] StatusTokens = ["online", "away", "busy", "rp", "lfp", "meld"];

    public static readonly string[] ScheduleModes = ["always", "daily", "weekly", "once"];

    // Options people actually change. The picker also lists every name in the enum.
    public static readonly SettingDesc[] Settings =
    [
        new("System", "ScreenMode", false, "0 windowed, 1 full screen, 2 borderless. Config only; Flow does not move the window."),
        new("System", "ScreenWidth", false, "Windowed width. Config only; Flow does not resize the live window."),
        new("System", "ScreenHeight", false, "Windowed height. Config only; Flow does not resize the live window."),
        new("System", "FullScreenWidth", false, "Full screen width. Config only; Flow does not resize the live window."),
        new("System", "FullScreenHeight", false, "Full screen height. Config only; Flow does not resize the live window."),
        new("System", "Fps", false, "Frame cap. 0 none, 1 refresh rate, 2 half, 3 quarter"),
        new("System", "FPSInActive", false, "1 caps the frame rate while the window is in the background"),
        new("System", "FPSDownAFK", false, "1 lowers the frame rate while away"),
        new("System", "DisplayObjectLimitType", false, "Character limit. 0 max, 4 min"),
        new("System", "DisplayHouseIndoorPcLimitType", false, "Character limit inside houses"),
        new("System", "GraphicsRezoScale", false, "3D resolution scale, 50 to 100"),
        new("System", "DynamicRezoType", false, "Dynamic resolution. 0 off"),
        new("System", "LodType", false, "LOD on distant objects. Writes the _DX11 twin too"),
        new("System", "GrassQuality", false, "Grass. Writes the _DX11 twin too"),
        new("System", "ShadowVisibilityTypeOther", false, "Other players' shadows. Writes the _DX11 twin too"),
        new("System", "ShadowVisibilityTypeParty", false, "Party shadows. Writes the _DX11 twin too"),
        new("System", "PhysicsTypeOther", false, "Other players' physics. Writes the _DX11 twin too"),
        new("System", "SSAO", false, "Ambient occlusion. Writes the _DX11 twin too"),
        new("System", "DepthOfField", false, "Depth of field. Writes the _DX11 twin too"),
        new("System", "RadialBlur", false, "Radial blur. Writes the _DX11 twin too"),
        new("System", "Gamma", false, "Gamma"),
        new("System", "SoundMaster", false, "Master volume"),
        new("System", "SoundBgm", false, "Music"),
        new("System", "SoundSe", false, "Effects"),
        new("System", "SoundVoice", false, "Voice"),
        new("System", "SoundEnv", false, "Environment"),
        new("System", "SoundSystem", false, "System sounds"),
        new("System", "SoundPerform", false, "Performance"),
        new("System", "IsSndMaster", false, "1 mutes everything"),
        new("System", "IsSndBgm", false, "1 mutes music"),
        new("System", "MouseSpeed", true, "Mouse look speed"),
        new("Ui", "BattleEffectSelf", false, "Own battle effects. 0 all, 1 limited, 2 none"),
        new("Ui", "BattleEffectParty", false, "Party battle effects. 0 all, 1 limited, 2 none"),
        new("Ui", "BattleEffectOther", false, "Other battle effects. 0 all, 1 limited, 2 none"),
        new("Ui", "BattleEffectPvPEnemyPc", false, "PvP enemy battle effects"),
        new("Ui", "NamePlateDispTypeSelf", false, "Own nameplate"),
        new("Ui", "NamePlateDispTypeParty", false, "Party nameplates"),
        new("Ui", "NamePlateDispTypeAlliance", false, "Alliance nameplates"),
        new("Ui", "NamePlateDispTypeOther", false, "Other nameplates"),
        new("Ui", "LogChatBubbleEnableChatBubble", false, "Chat bubbles"),
    ];

    public static IReadOnlyList<CardDesc> For(Section section)
    {
        if (section is Section.When or Section.Until)
        {
            return
            [
                new(CardType.State, "State", "In combat, in duty, mounted, crafting"),
                new(CardType.Duty, "Duty", "In a duty, or a kind of duty"),
                new(CardType.Place, "Place", "Town, inn, dungeon, field"),
                new(CardType.Zone, "Zone", "Zone name or #id"),
                new(CardType.Region, "Region", "Region name"),
                new(CardType.Housing, "Housing address", "District, ward, plot, room"),
                new(CardType.Group, "Group size", "Solo, light, full, alliance"),
                new(CardType.Party, "Party count", "Compare the party count"),
                new(CardType.Job, "Job", "Job abbreviations"),
                new(CardType.Role, "Role", "Tank, healer, DPS, crafter, gatherer"),
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
                new(CardType.Target, "Target", "None, any, player, NPC"),
                new(CardType.Plugin, "Plugin loaded", "Plugin name or internal name"),
            ];
        }

        return
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
            new(CardType.PenumbraReset, "Penumbra reset temp", "One mod, or all Flow changes"),
            new(CardType.Logout, "Log out", "Asks first unless you turn that off"),
            new(CardType.CloseGame, "Close game", "Asks first unless you turn that off"),
        ];
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
            CardType.Collection when Blank(card.Value) => "collection name",
            CardType.PluginSet when Blank(card.Value) => "plugin name",
            CardType.Glamour when Blank(card.Value) => "design",
            CardType.PenumbraMod when Blank(card.Option) => "mod directory",
            CardType.PenumbraCollection when Blank(card.Value) => "collection name",
            CardType.SearchComment when Blank(card.Value) => "comment",
            CardType.Setting when Blank(card.Value) => "option",
            CardType.Housing when !AddressSpec.TryParse(card.Value, out _, out var d, out _, out _, out _, out _)
                || (d.Length == 0 && !card.Value.Trim().Equals("any", StringComparison.OrdinalIgnoreCase)) => "district",
            CardType.Schedule when !Match.TryParseSchedule(card.Value, out _) => "schedule",
            _ when CardType.IsCondition(card.Type) && card.Type != CardType.Mount && Blank(card.Value) => "value",
            _ => null,
        };
    }

    public static List<string> Missing(FlowItem flow)
    {
        var list = new List<string>();
        foreach (var section in new[] { Section.When, Section.Do, Section.Until, Section.After })
        {
            var cards = flow.In(section);
            for (var i = 0; i < cards.Count; i++)
            {
                var gap = Missing(cards[i]);
                if (gap is not null) list.Add($"{Key(section, i)} needs a {gap}");
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
            CardType.Logout => card.Flag ? "Log out (asks)" : "Log out",
            CardType.CloseGame => card.Flag ? "Close game (asks)" : "Close game",
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
            Notes = "Set the window and fill the command. Ask mode offers it once per window.",
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
}

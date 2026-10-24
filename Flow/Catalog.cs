using System;
using System.Collections.Generic;

namespace Flow;

public sealed record CardDesc(string Type, string Label);

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

    public static readonly string[] Duties = ["in", "out", "queue"];

    public static readonly string[] Targets = ["none", "any", "player", "npc"];

    public static readonly SettingDesc[] Settings =
    [
        new("System", "ScreenWidth", false, "Window width"),
        new("System", "ScreenHeight", false, "Window height"),
        new("System", "FullScreenWidth", false, "Fullscreen width"),
        new("System", "FullScreenHeight", false, "Fullscreen height"),
        new("System", "Fps", false, "Frame cap. 0 is uncapped on some clients"),
        new("System", "FPSInActive", false, "Cap while the window is in the background"),
        new("System", "DisplayObjectLimitType", false, "Object count limit"),
        new("System", "Gamma", false, "Gamma"),
        new("System", "SoundMaster", false, "Master volume"),
        new("System", "SoundBgm", false, "Music"),
        new("System", "SoundSe", false, "Effects"),
        new("System", "SoundVoice", false, "Voice"),
        new("System", "SoundEnv", false, "Environment"),
        new("System", "SoundSystem", false, "System"),
        new("System", "SoundPerform", false, "Performance"),
        new("System", "MouseSpeed", true, "Mouse look speed"),
        new("Ui", "BattleEffectSelf", false, "Own battle effects"),
        new("Ui", "BattleEffectParty", false, "Party battle effects"),
        new("Ui", "BattleEffectOther", false, "Other battle effects"),
        new("Ui", "NamePlateDispTypeSelf", false, "Own nameplate"),
        new("Ui", "NamePlateDispTypeParty", false, "Party nameplates"),
        new("Ui", "NamePlateDispTypeOther", false, "Other nameplates"),
    ];

    public static IReadOnlyList<CardDesc> For(Section section)
    {
        if (section is Section.When or Section.Until)
        {
            return
            [
                new(CardType.State, "State"),
                new(CardType.Place, "Place"),
                new(CardType.Duty, "Duty"),
                new(CardType.Zone, "Zone"),
                new(CardType.World, "World"),
                new(CardType.DataCenter, "Data center"),
                new(CardType.Job, "Job"),
                new(CardType.Role, "Role"),
                new(CardType.Party, "Party size"),
                new(CardType.Nearby, "Nearby players"),
                new(CardType.Level, "Level"),
                new(CardType.Time, "Time"),
                new(CardType.Weekday, "Weekday"),
                new(CardType.Status, "Online status"),
                new(CardType.Target, "Target"),
                new(CardType.Plugin, "Plugin loaded"),
            ];
        }

        return
        [
            new(CardType.Command, "Command or macro"),
            new(CardType.Wait, "Wait"),
            new(CardType.Notice, "Chat notice"),
            new(CardType.Setting, "Game setting"),
            new(CardType.Online, "Online status"),
            new(CardType.Collection, "Plugin collection"),
            new(CardType.PluginSet, "Plugin on or off"),
            new(CardType.Glamour, "Glamourer design"),
            new(CardType.GlamourRevert, "Glamourer revert"),
            new(CardType.PenumbraMod, "Penumbra mod"),
            new(CardType.PenumbraCollection, "Penumbra collection"),
            new(CardType.PenumbraReset, "Penumbra clear temp"),
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
            case CardType.Nearby: card.Value = ">=1"; break;
            case CardType.Level: card.Value = ">=1"; break;
            case CardType.Time: card.Value = "18:00-06:00"; break;
            case CardType.Weekday: card.Value = "sat,sun"; break;
            case CardType.Target: card.Value = "any"; break;
            case CardType.Role: card.Value = "DPS"; break;
            case CardType.Command: card.Repeat = 1; card.WaitMs = 400; break;
            case CardType.Wait: card.WaitMs = 500; break;
            case CardType.Setting:
                card.Option = "System";
                card.Value = "SoundMaster";
                card.Extra = "100";
                break;
            case CardType.Online: card.Value = "online"; break;
            case CardType.PenumbraMod: card.Flag = true; card.Number = 0; break;
        }
        return card;
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
            CardType.World => $"{not}world {Blank(card.Value, "name")}",
            CardType.DataCenter => $"{not}dc {Blank(card.Value, "name")}",
            CardType.Place => $"{not}place {Blank(card.Value, "place")}",
            CardType.Duty => $"{not}duty {Blank(card.Value, "in")}",
            CardType.Party => $"{not}party {Blank(card.Value, ">=1")}",
            CardType.Nearby => $"{not}nearby {Blank(card.Value, ">=1")}",
            CardType.Level => $"{not}level {Blank(card.Value, ">=1")}",
            CardType.Time => $"{not}time {Blank(card.Value, "range")}",
            CardType.Weekday => $"{not}day {Blank(card.Value, "day")}",
            CardType.Status => $"{not}status {Blank(card.Value, "name")}",
            CardType.Target => $"{not}target {Blank(card.Value, "any")}",
            CardType.Plugin => $"{not}plugin {Blank(card.Value, "name")}",
            CardType.Command => CommandLabel(card),
            CardType.Wait => card.WaitMs <= 0 ? "Wait" : $"Wait {card.WaitMs} ms",
            CardType.Notice => string.IsNullOrWhiteSpace(card.Value) ? "Chat notice" : $"Say {card.Value}",
            CardType.Setting => $"{Blank(card.Value, "option")} = {Blank(card.Extra, "?")}",
            CardType.Online => $"Status {Blank(card.Value, "online")}",
            CardType.Collection => $"{(card.Flag ? "Enable" : "Disable")} collection {Blank(card.Value, "name")}",
            CardType.PluginSet => $"{(card.Flag ? "Enable" : "Disable")} plugin {Blank(card.Value, "name")}" + (card.Permanent ? "" : " (temp)"),
            CardType.Glamour => $"Glamour {Blank(card.Value, "design")}",
            CardType.GlamourRevert => "Glamour revert",
            CardType.PenumbraMod => PenumbraLabel(card),
            CardType.PenumbraCollection => $"Penumbra collection {Blank(card.Value, "name")}",
            CardType.PenumbraReset => "Penumbra clear temp",
            _ => card.Type,
        };
        return text;
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

    public static FlowItem DutyTemplate()
    {
        var flow = new FlowItem
        {
            Name = "Duty",
            Enabled = false,
            Mode = RunMode.Auto,
            OnStop = StopMode.Revert,
            Priority = 10,
            Notes = "Fill the collection name, then enable the flow. Do enables that collection. After disables it. Add more cards for the plugins and settings you want.",
        };
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.State, Value = "InDuty" });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Collection, Value = "", Flag = true });
        flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Collection, Value = "", Flag = false });
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
            Notes = "Turns on in town, off in a duty. Put the status, design, or command you actually want.",
        };
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.Place, Value = "Town" });
        flow.Cards.Add(new Card { Section = Section.When, Type = CardType.State, Value = "InDuty", Not = true });
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Online, Value = "online" });
        flow.Cards.Add(new Card { Section = Section.After, Type = CardType.Notice, Value = "Town flow ended." });
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
        var state = card.Flag ? "on" : "off";
        var how = card.Permanent ? "set" : "temp";
        return $"Penumbra {how} {name} {state} @{card.Number}";
    }

    private static string Blank(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

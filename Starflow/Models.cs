using System;
using System.Collections.Generic;

namespace Starflow;

public enum Section
{
    When = 0,
    Do = 1,
    Until = 2,
    After = 3,
}

public enum Join
{
    All = 0,
    Any = 1,
}

public enum RunMode
{
    Off = 0,
    Ask = 1,
    Auto = 2,
}

public enum StopMode
{
    Revert = 0,
    Keep = 1,
}

public static class CardType
{
    public const string State = "state";
    public const string Job = "job";
    public const string Role = "role";
    public const string Zone = "zone";
    public const string Region = "region";
    public const string World = "world";
    public const string DataCenter = "dc";
    public const string Account = "account";
    public const string Place = "place";
    public const string Housing = "housing";
    public const string Duty = "duty";
    public const string Party = "party";
    public const string Group = "group";
    public const string Nearby = "nearby";
    public const string NearbyName = "nearby-name";
    public const string Level = "level";
    public const string Time = "time";
    public const string Weekday = "weekday";
    public const string Schedule = "schedule";
    public const string Weather = "weather";
    public const string Mount = "mount";
    public const string Status = "status";
    public const string Target = "target";
    public const string Plugin = "plugin";
    public const string Vitals = "vitals";
    public const string Effect = "effect";
    public const string Enmity = "enmity";
    public const string Hurt = "hurt";
    public const string PartyDown = "party-down";
    public const string Chat = "chat";

    public const string Command = "command";
    public const string Wait = "wait";
    public const string Notice = "notice";
    public const string Setting = "setting";
    public const string Online = "online";
    public const string SearchComment = "search-comment";
    public const string Collection = "collection";
    public const string PluginSet = "plugin-set";
    public const string Glamour = "glamour";
    public const string GlamourRevert = "glamour-revert";
    public const string PenumbraMod = "penumbra-mod";
    public const string PenumbraCollection = "penumbra-collection";
    public const string PenumbraReset = "penumbra-reset";
    public const string Logout = "logout";
    public const string CloseGame = "close-game";
    public const string Webhook = "webhook";
    public const string Lifestream = "lifestream";

    public static bool IsCondition(string type) => type is
        State or Job or Role or Zone or Region or World or DataCenter or Account or Place or Housing or Duty
        or Party or Group or Nearby or NearbyName or Level or Time or Weekday or Schedule or Weather or Mount
        or Status or Target or Plugin or Vitals or Effect or Enmity or Hurt or PartyDown or Chat;

    // Cards that read moment-to-moment play or other people. Their flows cannot send combat actions.
    public static bool IsReactive(string type) => type is Vitals or Effect or Enmity or Hurt or PartyDown or Chat;

    public static bool IsSlotted(string type) => type is
        Setting or Online or Glamour or GlamourRevert or PenumbraMod;
}

[Serializable]
public sealed class Card
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Section Section { get; set; }
    public string Type { get; set; } = CardType.State;
    public string Value { get; set; } = string.Empty;
    public string Option { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
    public int Number { get; set; }
    public int WaitMs { get; set; }
    public int Repeat { get; set; } = 1;
    public bool Flag { get; set; } = true;
    public bool Not { get; set; }
    public bool Permanent { get; set; }
    public bool Inherit { get; set; }
    public bool Keep { get; set; }

    public string Label => Catalog.Label(this);
}

[Serializable]
public sealed class FlowItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New flow";
    public string Notes { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public RunMode Mode { get; set; } = RunMode.Auto;
    public StopMode OnStop { get; set; } = StopMode.Revert;
    public Join WhenJoin { get; set; } = Join.All;
    public Join UntilJoin { get; set; } = Join.Any;
    public float HoldSec { get; set; }
    public bool IgnoreQuiet { get; set; }
    public List<Card> Cards { get; set; } = [];

    public string FolderKey => Folders.Clean(Folder);
    public string Token => (Name ?? string.Empty).Trim();

    public List<Card> In(Section section)
    {
        var list = new List<Card>();
        foreach (var card in Cards)
        {
            if (card.Section == section) list.Add(card);
        }
        return list;
    }
}

public static class Folders
{
    public static string Clean(string? path)
    {
        var parts = (path ?? string.Empty).Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', parts);
    }

    public static string Parent(string path)
    {
        var clean = Clean(path);
        var i = clean.LastIndexOf('/');
        return i < 0 ? string.Empty : clean[..i];
    }

    public static string Leaf(string path)
    {
        var clean = Clean(path);
        var i = clean.LastIndexOf('/');
        return i < 0 ? clean : clean[(i + 1)..];
    }

    public static bool Under(string path, string folder)
    {
        var p = Clean(path);
        var f = Clean(folder);
        if (f.Length == 0) return true;
        return p.Equals(f, StringComparison.OrdinalIgnoreCase)
            || p.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase);
    }
}

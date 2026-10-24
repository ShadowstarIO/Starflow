using System;
using System.Collections.Generic;

namespace Flow;

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
    public const string World = "world";
    public const string DataCenter = "dc";
    public const string Place = "place";
    public const string Duty = "duty";
    public const string Party = "party";
    public const string Nearby = "nearby";
    public const string Level = "level";
    public const string Time = "time";
    public const string Weekday = "weekday";
    public const string Status = "status";
    public const string Target = "target";
    public const string Plugin = "plugin";

    public const string Command = "command";
    public const string Wait = "wait";
    public const string Notice = "notice";
    public const string Setting = "setting";
    public const string Online = "online";
    public const string Collection = "collection";
    public const string PluginSet = "plugin-set";
    public const string Glamour = "glamour";
    public const string GlamourRevert = "glamour-revert";
    public const string PenumbraMod = "penumbra-mod";
    public const string PenumbraCollection = "penumbra-collection";
    public const string PenumbraReset = "penumbra-reset";

    public static bool IsCondition(string type) => type is
        State or Job or Role or Zone or World or DataCenter or Place or Duty
        or Party or Nearby or Level or Time or Weekday or Status or Target or Plugin;
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
    public List<Card> Cards { get; set; } = [];

    public string FolderKey => string.IsNullOrWhiteSpace(Folder) ? string.Empty : Folder.Trim();
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

using System;
using System.Collections.Generic;

namespace Starflow;

// One snapshot. No Dalamud types here, so Eval can be tested. WorldCapture fills it from the game.
public sealed partial class World
{
    public DateTime Now { get; init; } = DateTime.Now;
    public bool LoggedIn { get; init; }
    public uint TerritoryId { get; init; }
    public string TerritoryName { get; init; } = string.Empty;
    public string RegionName { get; init; } = string.Empty;
    public string WorldName { get; init; } = string.Empty;
    public string DataCenterName { get; init; } = string.Empty;
    public string HomeWorldName { get; init; } = string.Empty;
    public string JobAbbr { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    public string CharacterName { get; init; } = string.Empty;
    public string DutyName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int Level { get; init; }
    public int PartySize { get; init; }
    // Chocobo companion and summoned pet, which the party list does not count.
    public int Companions { get; init; }
    public bool Alliance { get; init; }
    public int Nearby { get; init; }
    public List<string> NearbyNames { get; init; } = [];
    public int LocalHour { get; init; }
    public int LocalMinute { get; init; }
    public int EorzeaHour { get; init; }
    public int EorzeaMinute { get; init; }
    public uint WeatherId { get; init; }
    public string Weather { get; init; } = string.Empty;
    public string WeatherBefore { get; init; } = string.Empty;
    public string WeatherNext { get; init; } = string.Empty;
    public int WeatherNextMinutes { get; init; }
    public string Mount { get; init; } = string.Empty;
    public Address Housing { get; init; } = Address.None;
    public string OnlineStatus { get; init; } = string.Empty;
    public string Target { get; init; } = "none";
    public string TargetName { get; init; } = string.Empty;
    public string TargetJob { get; init; } = string.Empty;
    public HashSet<string> States { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Places { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string Group => Match.GroupBand(PartySize, Alliance || Places.Contains("Alliance"));

    public string Account =>
        WorldName.Length == 0 || HomeWorldName.Length == 0 ? string.Empty
        : WorldName.Equals(HomeWorldName, StringComparison.OrdinalIgnoreCase) ? "home" : "visiting";

    public string Line()
    {
        if (!LoggedIn) return "Not logged in";
        var place = PrimaryPlace();
        var visit = Account == "visiting" ? $" (home {HomeWorldName})" : string.Empty;
        var house = Housing.InWard ? " · " + Housing.Line() : string.Empty;
        var weather = Weather.Length > 0 ? " · " + Weather : string.Empty;
        return $"{JobAbbr} {Level} · {Group} · {place} · {WorldName}{visit} · {TerritoryName}{house} · ET {EorzeaHour:00}:{EorzeaMinute:00}{weather} · {OnlineStatus}";
    }

    public string PrimaryPlace()
    {
        foreach (var key in new[] { "Dungeon", "Trial", "Raid", "Alliance", "PvP", "DeepDungeon", "Housing", "Town", "Inn", "GoldSaucer", "Field", "Overworld" })
        {
            if (Places.Contains(key)) return key;
        }
        return "Overworld";
    }

    public bool Has(string state) => States.Contains(state);
}

public sealed partial class World
{
    public Unit Me { get; init; } = Unit.None;
    public Unit TargetUnit { get; init; } = Unit.None;
    // Other party members, you left out.
    public List<Unit> Party { get; init; } = [];
    public int EnemiesOnMe { get; init; }
    public double HurtAgoSec { get; init; } = double.MaxValue;
    public int HurtAmount { get; init; }
    public bool HurtByPlayer { get; init; }
    public List<ChatLine> Chat { get; init; } = [];

    public int PartyDown
    {
        get
        {
            var count = Me.Down ? 1 : 0;
            foreach (var unit in Party)
            {
                if (unit.Down) count++;
            }
            return count;
        }
    }

    public ChatLine? LastChat
    {
        get
        {
            ChatLine? newest = null;
            foreach (var line in Chat)
            {
                if (!line.Mine && (newest is null || line.AgeSec < newest.AgeSec)) newest = line;
            }
            return newest;
        }
    }
}

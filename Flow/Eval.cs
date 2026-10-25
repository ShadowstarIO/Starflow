using System;
using System.Collections.Generic;

namespace Flow;

public static class Eval
{
    // Set by the plugin. Kept as a hook so Eval does not load Dalamud.
    public static Func<string, bool> PluginLoaded { get; set; } = _ => false;

    public static bool CardMatches(Card card, World world)
    {
        var raw = card.Not ? !Positive(card, world) : Positive(card, world);
        return raw;
    }

    public static bool SectionMatches(FlowItem flow, Section section, World world)
    {
        var cards = flow.In(section);
        if (cards.Count == 0) return false;
        var join = section == Section.Until ? flow.UntilJoin : flow.WhenJoin;
        if (join == Join.Any)
        {
            foreach (var card in cards)
            {
                if (CardMatches(card, world)) return true;
            }
            return false;
        }

        foreach (var card in cards)
        {
            if (!CardMatches(card, world)) return false;
        }
        return true;
    }

    public static IEnumerable<string> Explain(FlowItem flow, World world)
    {
        yield return world.Line();
        foreach (var section in new[] { Section.When, Section.Until })
        {
            var cards = flow.In(section);
            if (cards.Count == 0)
            {
                yield return section == Section.When ? "When is empty, so this stays off." : "Until is empty.";
                continue;
            }

            var join = section == Section.Until ? flow.UntilJoin : flow.WhenJoin;
            var whole = SectionMatches(flow, section, world);
            yield return $"{section} {(join == Join.All ? "all" : "any")}: {(whole ? "yes" : "no")}";
            for (var i = 0; i < cards.Count; i++)
            {
                var on = CardMatches(cards[i], world);
                yield return $"{(on ? "yes" : "no")}  {Catalog.Key(section, i)}  {cards[i].Label}";
            }
        }
    }

    private static bool Positive(Card card, World world)
    {
        var value = (card.Value ?? string.Empty).Trim();
        return card.Type switch
        {
            CardType.State => world.Has(value),
            CardType.Job => Match.AnyTerm(world.JobAbbr, value),
            CardType.Role => value.Equals(world.Role, StringComparison.OrdinalIgnoreCase),
            CardType.Zone => Zone(world, value),
            CardType.Region => Match.AnyTerm(world.RegionName, value),
            CardType.World => Match.AnyTerm(world.WorldName, value) || Match.AnyTerm(world.HomeWorldName, value),
            CardType.DataCenter => Match.AnyTerm(world.DataCenterName, value),
            CardType.Account => value.Equals(world.Account, StringComparison.OrdinalIgnoreCase),
            CardType.Place => world.Places.Contains(value) || world.Has(value),
            CardType.Housing => AddressSpec.Matches(value, world.Housing),
            CardType.Duty => Duty(world, value),
            CardType.Party => Match.Number(world.PartySize, value),
            CardType.Group => Match.Group(world.Group, value),
            CardType.Nearby => Match.Nearby(world.Nearby, value),
            CardType.NearbyName => Match.AnyName(world.NearbyNames, value),
            CardType.Level => Match.Number(world.Level, value),
            CardType.Time => Time(world, value),
            CardType.Weekday => Match.Weekday(world.Now, value),
            CardType.Schedule => Match.Schedule(world.Now, value),
            CardType.Weather => Weather(world, value),
            CardType.Mount => Mount(world, value),
            CardType.Status => Status(world, value),
            CardType.Target => Target(world, value),
            CardType.Plugin => PluginLoaded(value),
            _ => false,
        };
    }

    private static bool Zone(World world, string value)
    {
        if (value.StartsWith('#'))
            return uint.TryParse(value.AsSpan(1), out var id) && id == world.TerritoryId;
        return Match.AnyTerm(world.TerritoryName, value) || value == world.TerritoryId.ToString();
    }

    private static bool Duty(World world, string value)
    {
        var text = value.ToLowerInvariant();
        var inDuty = world.Has("InDuty");
        return text switch
        {
            "in" or "yes" or "true" or "solid" => inDuty,
            "out" or "no" or "false" or "none" => !inDuty,
            "queue" => world.Has("InQueue") || world.Has("DutyReady"),
            "any" => inDuty || world.Has("InQueue") || world.Has("DutyReady"),
            "dungeon" => inDuty && world.Places.Contains("Dungeon"),
            "trial" => inDuty && world.Places.Contains("Trial"),
            "raid" => inDuty && world.Places.Contains("Raid"),
            "alliance" => inDuty && (world.Places.Contains("Alliance") || world.Alliance),
            "pvp" => world.Has("PvP"),
            "deep" => world.Has("DeepDungeon"),
            "field" => world.Places.Contains("Field"),
            _ => inDuty && (world.Places.Contains(value) || Match.AnyTerm(world.TerritoryName, value)),
        };
    }

    private static bool Time(World world, string value)
    {
        if (value.StartsWith("et ", StringComparison.OrdinalIgnoreCase))
            return Match.Clock(world.EorzeaHour, world.EorzeaMinute, value);
        return Match.Clock(world.LocalHour, world.LocalMinute, value);
    }

    private static bool Weather(World world, string value)
    {
        if (value.Length == 0) return false;
        foreach (var part in value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (uint.TryParse(part, out var id) ? id == world.WeatherId : world.Weather.Equals(part, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool Mount(World world, string value)
    {
        var mounted = world.Has("Mounted");
        return value.ToLowerInvariant() switch
        {
            "" or "any" => mounted,
            "none" => !mounted,
            "flying" => world.Has("Flying"),
            _ => mounted && Match.AnyTerm(world.Mount, value),
        };
    }

    private static bool Status(World world, string value)
    {
        if (value.Length == 0) return false;
        foreach (var part in value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (world.OnlineStatus.Equals(part, StringComparison.OrdinalIgnoreCase)) return true;
            var alias = Catalog.StatusAlias(part);
            if (alias.Length > 0 && world.OnlineStatus.Contains(alias, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool Target(World world, string value)
    {
        var text = value.ToLowerInvariant();
        if (text is "any") return world.Target != "none";
        return world.Target.Equals(text, StringComparison.OrdinalIgnoreCase);
    }
}

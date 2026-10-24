using System;
using System.Collections.Generic;

namespace Flow;

public static class Eval
{
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
            yield return $"{section} {(join == Join.All ? "all" : "any")}";
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
            CardType.World => Match.AnyTerm(world.WorldName, value) || Match.AnyTerm(world.HomeWorldName, value),
            CardType.DataCenter => Match.AnyTerm(world.DataCenterName, value),
            CardType.Place => world.Places.Contains(value) || world.Has(value),
            CardType.Duty => Duty(world, value),
            CardType.Party => Match.Number(world.PartySize, value),
            CardType.Nearby => Match.Number(world.Nearby, value),
            CardType.Level => Match.Number(world.Level, value),
            CardType.Time => Time(world, value),
            CardType.Weekday => Match.Weekday(world.Now, value),
            CardType.Status => Match.AnyTerm(world.OnlineStatus, value),
            CardType.Target => Target(world, value),
            CardType.Plugin => Bridges.PluginLoaded(value),
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
        return text switch
        {
            "in" or "yes" or "true" => world.Has("InDuty"),
            "out" or "no" or "false" => !world.Has("InDuty"),
            "queue" => world.Has("InQueue") || world.Has("DutyReady"),
            _ => world.Has("InDuty") && (world.Places.Contains(value) || Match.AnyTerm(world.TerritoryName, value)),
        };
    }

    private static bool Time(World world, string value)
    {
        if (value.StartsWith("et ", StringComparison.OrdinalIgnoreCase))
            return Match.Clock(world.EorzeaHour, 0, value);
        return Match.Clock(world.LocalHour, world.LocalMinute, value);
    }

    private static bool Target(World world, string value)
    {
        var text = value.ToLowerInvariant();
        if (text is "any") return world.Target != "none";
        return world.Target.Equals(text, StringComparison.OrdinalIgnoreCase);
    }
}

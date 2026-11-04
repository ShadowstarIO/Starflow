using System;
using System.Collections.Generic;
using System.Linq;

namespace Starflow;

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
        yield return "Now: " + world.Line();
        foreach (var section in new[] { Section.When, Section.Until })
        {
            var cards = flow.In(section);
            if (cards.Count == 0)
            {
                yield return section == Section.When ? "When is empty, so this never starts on its own." : "Until is empty, so only When going false ends it.";
                continue;
            }

            var join = section == Section.Until ? flow.UntilJoin : flow.WhenJoin;
            var whole = SectionMatches(flow, section, world);
            var need = join == Join.All ? "needs all" : "needs any";
            var means = section == Section.When
                ? whole ? "would start or keep it on" : "keeps it from starting"
                : whole ? "would end it now" : "does not end it";
            yield return $"{(whole ? "yes" : "no")}  {section} ({need}) {means}";
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
            CardType.Job => Job(world, value),
            CardType.Role => Words(value).Any(r => r.Equals(world.Role, StringComparison.OrdinalIgnoreCase)),
            CardType.Zone => Zone(world, value),
            CardType.Region => Match.AnyTerm(world.RegionName, value),
            CardType.World => Match.AnyTerm(world.WorldName, value) || Match.AnyTerm(world.HomeWorldName, value),
            CardType.DataCenter => Match.AnyTerm(world.DataCenterName, value),
            CardType.Account => value.Equals(world.Account, StringComparison.OrdinalIgnoreCase),
            CardType.Place => world.Places.Contains(value) || world.Has(value),
            CardType.Housing => AddressSpec.Matches(value, world.Housing),
            CardType.Duty => Duty(world, value),
            CardType.Party => Match.Number(world.PartySize + (card.Option == "pets" ? world.Companions : 0), value),
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
            CardType.Target => Target(world, value) && (string.IsNullOrWhiteSpace(card.Extra) || Match.AnyTerm(world.TargetName, card.Extra.Trim())),
            CardType.Plugin => PluginLoaded(value),
            CardType.Vitals => Vitals(world, card, value),
            CardType.Effect => Effect(world, card, value),
            CardType.Enmity => Match.Number(world.EnemiesOnMe, value.Length == 0 ? ">=1" : value),
            CardType.Hurt => Hurt(world, card, value),
            CardType.PartyDown => Match.Number(world.PartyDown, value.Length == 0 ? ">=1" : value),
            CardType.Chat => Chat(world, card, value),
            _ => false,
        };
    }

    private static string[] Words(string value) =>
        value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    // WHM|SCH, or full names. A regular expression is tried on both.
    private static bool Job(World world, string value)
    {
        if (Match.IsRegex(value, out _)) return Match.AnyTerm(world.JobAbbr, value) || Match.AnyTerm(world.JobName, value);
        foreach (var word in Words(value))
        {
            if (word.Equals(world.JobAbbr, StringComparison.OrdinalIgnoreCase)) return true;
            if (word.Equals(world.JobName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
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
            _ => inDuty && (world.Places.Contains(value) || Match.AnyTerm(world.TerritoryName, value) || Match.AnyTerm(world.DutyName, value)),
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
        if (Match.IsRegex(value, out _)) return Match.AnyTerm(world.Weather, value);
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
        if (Match.IsRegex(value, out _)) return Match.AnyTerm(world.OnlineStatus, value);
        foreach (var part in value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (world.OnlineStatus.Equals(part, StringComparison.OrdinalIgnoreCase)) return true;
            var alias = Catalog.StatusAlias(part);
            if (alias.Length > 0 && world.OnlineStatus.Contains(alias, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // "<50%" compares the percent, "<8000" the number. Who: me, target, party-any (someone else), party-all (everyone, you too).
    public static bool Stat(Unit unit, string stat, string spec)
    {
        if (!unit.Present) return false;
        var (now, max) = unit.Stat(stat);
        if (max <= 0) return false;
        var text = spec.Trim();
        if (text.Contains('%')) return Match.Number((int)Math.Floor(now * 100.0 / max), text.Replace("%", string.Empty));
        return Match.Number(now, text);
    }

    private static bool Vitals(World world, Card card, string value)
    {
        var stat = card.Option.Length == 0 ? "hp" : card.Option;
        return (card.Extra.Length == 0 ? "me" : card.Extra) switch
        {
            "target" => Stat(world.TargetUnit, stat, value),
            "party-any" => world.Party.Any(u => Stat(u, stat, value)),
            "party-all" => Stat(world.Me, stat, value) && world.Party.All(u => !u.Present || Stat(u, stat, value)),
            _ => Stat(world.Me, stat, value),
        };
    }

    public static bool HasEffect(Unit unit, string value)
    {
        if (!unit.Present || value.Length == 0) return false;
        if (Match.IsRegex(value, out _)) return unit.Effects.Any(e => Match.AnyTerm(e, value));
        foreach (var word in Words(value))
        {
            if (unit.Effects.Any(e => e.Equals(word, StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    private static bool Effect(World world, Card card, string value) => (card.Extra.Length == 0 ? "me" : card.Extra) switch
    {
        "target" => HasEffect(world.TargetUnit, value),
        "party" => HasEffect(world.Me, value) || world.Party.Any(u => HasEffect(u, value)),
        "party-others" => world.Party.Any(u => HasEffect(u, value)),
        _ => HasEffect(world.Me, value),
    };

    // Seconds in Number, minimum in Value (blank, 5000, or 10% of max HP), source in Option.
    private static bool Hurt(World world, Card card, string value)
    {
        var window = card.Number > 0 ? card.Number : 3;
        if (world.HurtAgoSec > window) return false;
        if (card.Option == "player" && !world.HurtByPlayer) return false;
        if (card.Option == "npc" && world.HurtByPlayer) return false;
        if (value.Length == 0) return true;
        if (value.EndsWith('%') && int.TryParse(value.TrimEnd('%').Trim(), out var pct))
            return world.Me.MaxHp > 0 && world.HurtAmount * 100 >= pct * world.Me.MaxHp;
        return int.TryParse(value, out var min) && world.HurtAmount >= min;
    }

    // Seconds in Number, channels in Option, sender in Extra, message in Value. Permanent counts your own lines.
    private static bool Chat(World world, Card card, string value)
    {
        var window = card.Number > 0 ? card.Number : 5;
        var channels = Words(card.Option);
        var anyChannel = channels.Length == 0 || channels.Any(c => c.Equals("any", StringComparison.OrdinalIgnoreCase));
        var sender = (card.Extra ?? string.Empty).Trim();
        foreach (var line in world.Chat)
        {
            if (line.AgeSec > window) continue;
            if (line.Mine && !card.Permanent) continue;
            if (!anyChannel && !channels.Any(c => c.Equals(line.Channel, StringComparison.OrdinalIgnoreCase))) continue;
            if (sender.Length > 0 && !Sender(line.Sender, sender)) continue;
            if (value.Length > 0 && !Match.AnyTerm(line.Text, value)) continue;
            return true;
        }
        return false;
    }

    // Exact names, so a sender of Bob does not let Bobby in. A regex is used as written.
    public static bool Sender(string who, string spec)
    {
        if (Match.IsRegex(spec, out _)) return Match.AnyTerm(who, spec);
        var name = who.Split('@')[0].Trim();
        return Words(spec).Any(w => w.Split('@')[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Target(World world, string value)
    {
        var text = value.ToLowerInvariant();
        if (text is "any") return world.Target != "none";
        return world.Target.Equals(text, StringComparison.OrdinalIgnoreCase);
    }
}

using System;
using System.Text;

namespace Starflow;

// {zone}, {job}, {world} and the rest of All. Unknown tokens stay as written.
public static class Tokens
{
    public sealed record Token(string Name, string Hint, Func<World, string> Read);

    // Every value Starflow reads from the game. The help page lists these; commands, notices, comments, and webhooks fill them.
    public static readonly Token[] All =
    [
        new("name", "Your character name", w => w.CharacterName),
        new("job", "Job abbreviation, e.g. WHM", w => w.JobAbbr),
        new("jobname", "Job name, e.g. White Mage", w => w.JobName),
        new("role", "Tank, Healer, DPS, Crafter, Gatherer", w => w.Role),
        new("level", "Your level", w => w.Level > 0 ? w.Level.ToString() : string.Empty),
        new("zone", "Zone name", w => w.TerritoryName),
        new("zoneid", "Territory id", w => w.TerritoryId.ToString()),
        new("region", "Region name", w => w.RegionName),
        new("place", "Town, Dungeon, Housing, Field…", w => w.PrimaryPlace()),
        new("duty", "Duty name while in one", w => w.DutyName),
        new("world", "Current world", w => w.WorldName),
        new("home", "Home world", w => w.HomeWorldName),
        new("dc", "Data center", w => w.DataCenterName),
        new("district", "Housing district", w => w.Housing.InWard ? w.Housing.District : string.Empty),
        new("ward", "Housing ward", w => w.Housing.InWard ? w.Housing.Ward.ToString() : string.Empty),
        new("plot", "Housing plot", w => w.Housing.Plot > 0 ? w.Housing.Plot.ToString() : string.Empty),
        new("room", "Apartment or chamber number", w => w.Housing.Room > 0 ? w.Housing.Room.ToString() : string.Empty),
        new("address", "The whole housing address", w => w.Housing.InWard ? w.Housing.Line() : string.Empty),
        new("party", "Party members, you included", w => w.PartySize.ToString()),
        new("group", "solo, light, full, alliance", w => w.Group),
        new("nearby", "Players within 30 yalms", w => w.Nearby.ToString()),
        new("target", "Target name", w => w.TargetName),
        new("targetjob", "Target's job, if a character", w => w.TargetJob),
        new("mount", "Mount name while mounted", w => w.Mount),
        new("status", "Your online status", w => w.OnlineStatus),
        new("weather", "Weather now", w => w.Weather),
        new("lastweather", "Weather in the window before", w => w.WeatherBefore),
        new("nextweather", "Weather in the next window", w => w.WeatherNext),
        new("time", "Local time, HH:mm", w => w.Now.ToString("HH:mm")),
        new("date", "Local date, yyyy-MM-dd", w => w.Now.ToString("yyyy-MM-dd")),
        new("day", "Local weekday", w => w.Now.DayOfWeek.ToString()),
        new("et", "Eorzea time, HH:mm", w => $"{w.EorzeaHour:00}:{w.EorzeaMinute:00}"),
        new("hp", "Your HP", w => w.Me.Present ? w.Me.Hp.ToString() : string.Empty),
        new("hpp", "Your HP percent", w => w.Me.MaxHp > 0 ? (w.Me.Hp * 100 / w.Me.MaxHp).ToString() : string.Empty),
        new("mp", "Your MP", w => w.Me.Present ? w.Me.Mp.ToString() : string.Empty),
        new("chatsender", "Who sent the latest chat line", w => w.LastChat?.Sender ?? string.Empty),
        new("chatmessage", "The latest chat line", w => w.LastChat?.Text ?? string.Empty),
        new("chatchannel", "Channel of the latest chat line", w => w.LastChat?.Channel ?? string.Empty),
    ];

    public static readonly string[] Names = Array.ConvertAll(All, t => t.Name);

    public static string Fill(string text, World world)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('{')) return text ?? string.Empty;
        return Fill(text, key => Lookup(key, world));
    }

    public static string Fill(string text, Func<string, string?> lookup)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('{')) return text ?? string.Empty;
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf('{', i);
            if (open < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }
            var close = text.IndexOf('}', open + 1);
            if (close < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }
            sb.Append(text, i, open - i);
            var key = text[(open + 1)..close];
            var value = key.Length is > 0 and <= 16 && !key.Contains('{') ? lookup(key.Trim().ToLowerInvariant()) : null;
            if (value is null)
            {
                // Not a token. Keep the brace and look again from the next character, so a JSON
                // body's outer { } does not swallow the tokens inside it.
                sb.Append('{');
                i = open + 1;
                continue;
            }
            sb.Append(value);
            i = close + 1;
        }
        return sb.ToString();
    }

    // "{chatmessage}" or "{x} rest" as a command would let a token pick the command. Only arguments may be tokens.
    public static bool HeadIsToken(string command)
    {
        var head = (command ?? string.Empty).TrimStart().Split(' ', 2)[0];
        return head.Contains('{');
    }

    private static string? Lookup(string key, World w)
    {
        if (key == "homeworld") key = "home";
        foreach (var token in All)
        {
            if (token.Name == key) return token.Read(w);
        }
        return null;
    }
}

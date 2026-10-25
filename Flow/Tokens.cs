using System;
using System.Text;

namespace Flow;

// {zone} {region} {job} {world} {home} {dc} {ward} {plot} {time} {target}. Unknown tokens stay as written.
public static class Tokens
{
    public static readonly string[] Names = ["zone", "region", "job", "world", "home", "dc", "ward", "plot", "time", "target"];

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
            var value = key.Length is > 0 and <= 16 ? lookup(key.Trim().ToLowerInvariant()) : null;
            if (value is null) sb.Append(text, open, close - open + 1);
            else sb.Append(value);
            i = close + 1;
        }
        return sb.ToString();
    }

    private static string? Lookup(string key, World w) => key switch
    {
        "zone" => w.TerritoryName,
        "region" => w.RegionName,
        "job" => w.JobAbbr,
        "world" => w.WorldName,
        "home" or "homeworld" => w.HomeWorldName,
        "dc" => w.DataCenterName,
        "ward" => w.Housing.InWard ? w.Housing.Ward.ToString() : string.Empty,
        "plot" => w.Housing.Plot > 0 ? w.Housing.Plot.ToString() : string.Empty,
        "time" => w.Now.ToString("HH:mm"),
        "target" => w.TargetName,
        _ => null,
    };
}

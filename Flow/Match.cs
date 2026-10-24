using System;
using System.Globalization;

namespace Flow;

public static class Match
{
    public static bool Number(int left, string expr)
    {
        var text = (expr ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        if (text.StartsWith(">=", StringComparison.Ordinal)) return left >= Parse(text[2..]);
        if (text.StartsWith("<=", StringComparison.Ordinal)) return left <= Parse(text[2..]);
        if (text.StartsWith('>')) return left > Parse(text[1..]);
        if (text.StartsWith('<')) return left < Parse(text[1..]);
        if (text.StartsWith('=')) return left == Parse(text[1..]);
        return left == Parse(text);
    }

    public static bool Clock(int hour, int minute, string spec)
    {
        var text = (spec ?? string.Empty).Trim();
        if (text.StartsWith("et ", StringComparison.OrdinalIgnoreCase))
            text = text[3..].Trim();
        var parts = text.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        if (!TryMinutes(parts[0], out var start) || !TryMinutes(parts[1], out var end)) return false;
        var now = hour * 60 + minute;
        if (start == end) return true;
        if (start < end) return now >= start && now < end;
        return now >= start || now < end;
    }

    public static bool Weekday(DateTime when, string spec)
    {
        var text = (spec ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0) return false;
        if (text is "weekend") return when.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        if (text is "weekday") return when.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        var name = when.DayOfWeek.ToString().ToLowerInvariant();
        var shortName = name[..3];
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == name || part == shortName || (part.Length >= 3 && name.StartsWith(part, StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    public static bool AnyTerm(string haystack, string spec)
    {
        var text = (spec ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        var have = haystack ?? string.Empty;
        foreach (var part in text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (have.Contains(part, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int Parse(string text)
    {
        var t = text.Trim();
        return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static bool TryMinutes(string text, out int minutes)
    {
        minutes = 0;
        var t = text.Trim();
        if (t.Contains(':'))
        {
            var bits = t.Split(':', 2);
            if (!int.TryParse(bits[0], out var h) || !int.TryParse(bits[1], out var m)) return false;
            minutes = Math.Clamp(h, 0, 23) * 60 + Math.Clamp(m, 0, 59);
            return true;
        }
        if (!int.TryParse(t, out var hour)) return false;
        minutes = Math.Clamp(hour, 0, 23) * 60;
        return true;
    }
}

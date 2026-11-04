using System;
using System.Globalization;

namespace Starflow;

public static class Match
{
    public static bool Number(int left, string expr)
    {
        var text = (expr ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        if (text.StartsWith(">=", StringComparison.Ordinal)) return TryInt(text[2..], out var a) && left >= a;
        if (text.StartsWith("<=", StringComparison.Ordinal)) return TryInt(text[2..], out var b) && left <= b;
        if (text.StartsWith("!=", StringComparison.Ordinal)) return TryInt(text[2..], out var c) && left != c;
        if (text.StartsWith('>')) return TryInt(text[1..], out var d) && left > d;
        if (text.StartsWith('<')) return TryInt(text[1..], out var e) && left < e;
        if (text.StartsWith('=')) return TryInt(text[1..], out var f) && left == f;
        var dash = text.IndexOf('-', 1);
        if (dash > 0 && TryInt(text[..dash], out var lo) && TryInt(text[(dash + 1)..], out var hi))
            return left >= lo && left <= hi;
        return TryInt(text, out var g) && left == g;
    }

    public static bool Nearby(int count, string expr)
    {
        var text = (expr ?? string.Empty).Trim().ToLowerInvariant();
        return text switch
        {
            "empty" => count == 0,
            "few" => count is > 0 and <= 8,
            "crowded" => count > 8,
            _ => Number(count, text),
        };
    }

    // Local time when there is no prefix. "et " reads the Eorzea clock the caller passes in.
    public static bool Clock(int hour, int minute, string spec)
    {
        var text = (spec ?? string.Empty).Trim();
        if (text.StartsWith("et ", StringComparison.OrdinalIgnoreCase))
            text = text[3..].Trim();
        if (!TryRange(text, out var start, out var end)) return false;
        return InRange(hour * 60 + minute, start, end);
    }

    public static bool Weekday(DateTime when, string spec)
    {
        var text = (spec ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0) return false;
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (DayMatches(when.DayOfWeek, part)) return true;
        }
        return false;
    }

    // always | daily 09:00-17:00 | weekly sat,sun [18:00-02:00] | once 2026-12-24[..2026-12-26] [20:00-23:00]
    // Local time only. A window that wraps midnight belongs to the day it started on.
    public static bool Schedule(DateTime now, string spec)
    {
        if (!TryParseSchedule(spec, out var s)) return false;
        if (s.Mode == "always") return true;

        var minute = now.Hour * 60 + now.Minute;
        if (!s.HasTime) return DayOk(s, now.Date);

        if (s.Start == s.End) return DayOk(s, now.Date);
        if (s.Start < s.End) return minute >= s.Start && minute < s.End && DayOk(s, now.Date);
        if (minute >= s.Start) return DayOk(s, now.Date);
        if (minute < s.End) return DayOk(s, now.Date.AddDays(-1));
        return false;
    }

    public static bool TryParseSchedule(string spec, out ScheduleSpec schedule)
    {
        schedule = new ScheduleSpec();
        var words = (spec ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return false;
        var mode = words[0].ToLowerInvariant();
        var rest = words[1..];
        switch (mode)
        {
            case "always":
                schedule.Mode = mode;
                return true;
            case "daily":
                schedule.Mode = mode;
                return TimeTail(rest, schedule);
            case "weekly":
                if (rest.Length == 0) return false;
                schedule.Mode = mode;
                schedule.Days = rest[0].ToLowerInvariant();
                foreach (var day in schedule.Days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!KnownDay(day)) return false;
                }
                return TimeTail(rest[1..], schedule);
            case "once":
                if (rest.Length == 0) return false;
                schedule.Mode = mode;
                var dates = rest[0].Split("..", 2, StringSplitOptions.TrimEntries);
                if (!TryDate(dates[0], out var from)) return false;
                var to = from;
                if (dates.Length == 2 && !TryDate(dates[1], out to)) return false;
                schedule.From = from;
                schedule.To = to < from ? from : to;
                return TimeTail(rest[1..], schedule);
            default:
                return false;
        }
    }

    public static string GroupBand(int partySize, bool alliance)
    {
        if (alliance) return "alliance";
        if (partySize <= 1) return "solo";
        if (partySize <= 4) return "light";
        return "full";
    }

    public static bool Group(string band, string spec)
    {
        var text = (spec ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0) return false;
        foreach (var part in text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == band) return true;
            if (part == "party" && band is "light" or "full") return true;
        }
        return false;
    }

    // A spec written as /pattern/ or re:pattern is a regular expression, matched without case.
    // Everything else is a list of words split on |, any of which may appear in the text.
    public static bool IsRegex(string spec, out string pattern)
    {
        var text = (spec ?? string.Empty).Trim();
        pattern = string.Empty;
        if (text.StartsWith("re:", StringComparison.OrdinalIgnoreCase)) pattern = text[3..].Trim();
        else if (text.Length >= 2 && text[0] == '/' && text[^1] == '/') pattern = text[1..^1];
        else return false;
        return true;
    }

    public static bool Regex(string haystack, string pattern)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(haystack ?? string.Empty, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(50));
        }
        catch (ArgumentException) { return false; }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { return false; }
    }

    // Null when the spec is fine, otherwise why the regular expression does not compile.
    public static string? RegexError(string spec)
    {
        if (!IsRegex(spec, out var pattern)) return null;
        if (pattern.Length == 0) return "The pattern is empty.";
        try
        {
            _ = new System.Text.RegularExpressions.Regex(pattern);
            return null;
        }
        catch (ArgumentException ex) { return ex.Message; }
    }

    public static bool AnyTerm(string haystack, string spec)
    {
        var text = (spec ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        if (IsRegex(text, out var pattern)) return pattern.Length > 0 && Regex(haystack, pattern);
        var have = haystack ?? string.Empty;
        foreach (var part in text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (have.Contains(part, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static bool AnyName(System.Collections.Generic.IEnumerable<string> names, string spec)
    {
        var text = (spec ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        if (IsRegex(text, out var pattern))
        {
            foreach (var name in names)
            {
                if (pattern.Length > 0 && Regex(name, pattern)) return true;
            }
            return false;
        }
        var parts = text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var name in names)
        {
            foreach (var part in parts)
            {
                if (name.Equals(part, StringComparison.OrdinalIgnoreCase)) return true;
                if (part.Length >= 3 && name.Contains(part, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    public static bool TryRange(string text, out int start, out int end)
    {
        start = end = 0;
        var parts = (text ?? string.Empty).Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        return TryMinutes(parts[0], out start) && TryMinutes(parts[1], out end);
    }

    private static bool InRange(int now, int start, int end)
    {
        if (start == end) return true;
        if (start < end) return now >= start && now < end;
        return now >= start || now < end;
    }

    private static bool TimeTail(string[] rest, ScheduleSpec schedule)
    {
        if (rest.Length == 0) return true;
        if (rest.Length > 1 || !TryRange(rest[0], out var start, out var end)) return false;
        schedule.HasTime = true;
        schedule.Start = start;
        schedule.End = end;
        return true;
    }

    private static bool DayOk(ScheduleSpec s, DateTime day) => s.Mode switch
    {
        "daily" => true,
        "weekly" => Weekday(day, s.Days),
        "once" => day >= s.From && day <= s.To,
        _ => true,
    };

    private static bool DayMatches(DayOfWeek day, string part)
    {
        if (part is "weekend") return day is DayOfWeek.Saturday or DayOfWeek.Sunday;
        if (part is "weekday" or "weekdays") return day is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        var name = day.ToString().ToLowerInvariant();
        return part.Length >= 2 && name.StartsWith(part, StringComparison.Ordinal);
    }

    private static bool KnownDay(string part)
    {
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (DayMatches(day, part)) return true;
        }
        return false;
    }

    private static bool TryDate(string text, out DateTime date) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryInt(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryMinutes(string text, out int minutes)
    {
        minutes = 0;
        var t = text.Trim();
        int h, m = 0;
        if (t.Contains(':'))
        {
            var bits = t.Split(':', 2);
            if (!int.TryParse(bits[0], out h) || !int.TryParse(bits[1], out m)) return false;
        }
        else if (!int.TryParse(t, out h)) return false;

        if (h is < 0 or > 24 || m is < 0 or > 59) return false;
        if (h == 24 && m != 0) return false;
        minutes = h * 60 + m;
        return true;
    }
}

public sealed class ScheduleSpec
{
    public string Mode { get; set; } = "always";
    public string Days { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool HasTime { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
}

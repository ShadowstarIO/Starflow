using System;
using System.Collections.Generic;
using System.Linq;

namespace Starflow;

// Penumbra option groups as card text. One group per line: "Group = Option | Option".
// A group that is not named keeps whatever the mod already has.
public static class ModOptions
{
    public static Dictionary<string, List<string>> Parse(string? text)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var raw in (text ?? string.Empty).Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var group = line[..eq].Trim();
            if (group.Length == 0) continue;
            var options = line[(eq + 1)..]
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            map[group] = options;
        }
        return map;
    }

    public static string Format(IReadOnlyDictionary<string, List<string>> map) =>
        string.Join('\n', map.Select(kv => $"{kv.Key} = {string.Join(" | ", kv.Value)}"));

    // The current groups with the card's groups laid over them.
    public static Dictionary<string, List<string>> Merge(IReadOnlyDictionary<string, List<string>>? current, IReadOnlyDictionary<string, List<string>> changes)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (current is not null)
        {
            foreach (var (group, options) in current) map[group] = [.. options];
        }
        foreach (var (group, options) in changes) map[group] = [.. options];
        return map;
    }
}

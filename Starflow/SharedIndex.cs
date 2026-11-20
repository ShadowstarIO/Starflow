using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Starflow;

// index.json in the repo's flows folder. No Dalamud types here, so tests can read it.
public static class SharedIndex
{
    public const string Folder = "https://raw.githubusercontent.com/ShadowstarIO/Starflow/star/flows/";
    public const string Page = "https://github.com/ShadowstarIO/Starflow/tree/star/flows";

    public sealed class Entry
    {
        public string File { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Needs { get; set; } = [];
        public string Author { get; set; } = string.Empty;

        public string Url => Folder + Uri.EscapeDataString(File);
        public string PageUrl => Page + "/" + Uri.EscapeDataString(File);
    }

    private static readonly JsonSerializerOptions Opt = new() { PropertyNameCaseInsensitive = true };

    // Plain file names only, sorted by name. Anything pointing outside the folder is dropped.
    public static List<Entry> Parse(string text)
    {
        var list = JsonSerializer.Deserialize<List<Entry>>(text, Opt) ?? [];
        list.RemoveAll(e => string.IsNullOrWhiteSpace(e.File) || e.File.Contains('/') || e.File.Contains('\\') || e.File.Contains(".."));
        foreach (var e in list)
        {
            if (string.IsNullOrWhiteSpace(e.Name)) e.Name = e.File;
            e.Needs ??= [];
        }
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }
}

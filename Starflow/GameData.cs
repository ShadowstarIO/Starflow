using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace Starflow;

// Lists read from the game's sheets once, for the pickers.
internal static class GameData
{
    public sealed record JobRow(string Abbr, string Name, string Role);

    public sealed record CommandRow(string Command, string Help, string Source);

    private static List<JobRow>? jobs;
    private static string[]? weathers;
    private static List<CommandRow>? gameCommands;

    public static readonly string[] RoleOrder = ["Tank", "Healer", "DPS", "Crafter", "Gatherer"];

    public static IReadOnlyList<JobRow> Jobs() => jobs ??= LoadJobs();

    public static IReadOnlyList<string> Weathers() => weathers ??= LoadWeathers();

    // Game commands from the TextCommand sheet, which includes emotes, then every plugin command Dalamud knows.
    public static IReadOnlyList<CommandRow> Commands()
    {
        gameCommands ??= LoadGameCommands();
        var list = new List<CommandRow>(gameCommands);
        try
        {
            foreach (var (name, info) in Plugin.CommandManager.Commands)
            {
                if (name is "/starflow" or "/flow" or "/fl") continue;
                list.Add(new CommandRow(name, info.HelpMessage ?? string.Empty, "Plugin"));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Plugin command list failed");
        }
        return list.OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<JobRow> LoadJobs()
    {
        try
        {
            return Plugin.DataManager.GetExcelSheet<ClassJob>()
                .Where(r => r.RowId > 0 && r.Abbreviation.ToString().Length > 0)
                .Select(r =>
                {
                    var abbr = r.Abbreviation.ToString();
                    var name = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(r.Name.ToString());
                    return new JobRow(abbr, name, World.RoleOf(abbr, r.Role));
                })
                .Where(j => j.Role.Length > 0)
                .GroupBy(j => j.Abbr)
                .Select(g => g.First())
                .OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Job list failed");
            return [];
        }
    }

    private static string[] LoadWeathers()
    {
        try
        {
            return Plugin.DataManager.GetExcelSheet<Weather>()
                .Select(r => r.Name.ToString())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Weather list failed");
            return [];
        }
    }

    private static List<CommandRow> LoadGameCommands()
    {
        var list = new List<CommandRow>();
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Plugin.DataManager.GetExcelSheet<TextCommand>())
            {
                var command = row.Command.ToString();
                if (command.Length < 2 || !command.StartsWith('/') || !seen.Add(command)) continue;
                var help = row.Description.ToString().Replace("\r", " ").Replace("\n", " ");
                var alias = row.Alias.ToString();
                if (alias.Length > 1 && !alias.Equals(command, StringComparison.OrdinalIgnoreCase)) help = $"({alias}) {help}";
                list.Add(new CommandRow(command, help, "Game"));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Command list failed");
        }
        return list;
    }

    // Commands that post where other players read it. Group channels reach your party, FC, or linkshell.
    public static string? Audience(string command)
    {
        var word = (command ?? string.Empty).Trim().Split(' ', 2)[0].ToLowerInvariant();
        return word switch
        {
            "/say" or "/s" or "/shout" or "/sh" or "/yell" or "/y" or "/em" or "/emote" or "/novice" or "/beginner" or "/n" => "public",
            "/party" or "/p" or "/alliance" or "/a" or "/freecompany" or "/fc" or "/pvpteam" => "group",
            _ when word.StartsWith("/linkshell", StringComparison.Ordinal) || word.StartsWith("/cwlinkshell", StringComparison.Ordinal)
                || (word.StartsWith("/l", StringComparison.Ordinal) && word.Length == 3 && char.IsDigit(word[2]))
                || (word.StartsWith("/cwl", StringComparison.Ordinal) && word.Length == 5 && char.IsDigit(word[4])) => "group",
            "/tell" or "/t" or "/r" or "/reply" => "tell",
            _ => null,
        };
    }
}

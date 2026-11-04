using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace Starflow;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool NotifyInChat { get; set; } = true;
    public bool OpenUiOnLoad { get; set; }
    public bool QuietInCutscene { get; set; } = true;
    public bool QuietInCombat { get; set; }
    public bool QuietInDuty { get; set; }
    public bool QuietBetweenAreas { get; set; } = true;
    public bool QuietWhenOccupied { get; set; } = true;
    public bool QuietWhenDead { get; set; } = true;
    public float PollSec { get; set; } = 0.4f;
    // 0 sorts the list by priority, then name. 1 sorts by name.
    public int ListSort { get; set; }
    // True wraps and squeezes column contents to fit. False keeps rows on one line with a sideways scroll bar.
    public bool WrapColumns { get; set; } = true;
    public List<FlowItem> Flows { get; set; } = [];
    public List<string> Folders { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    // Loads the saved settings. On the first run after the rename, brings over flows saved under the
    // plugin's earlier name, Flow. The old file is left where it is, as a backup.
    public static Configuration Load()
    {
        if (Plugin.PluginInterface.GetPluginConfig() is Configuration saved) return saved;
        try
        {
            var folder = Plugin.PluginInterface.ConfigFile.DirectoryName;
            var old = folder is null ? null : System.IO.Path.Combine(folder, "Flow.json");
            if (old is null || !System.IO.File.Exists(old)) return new Configuration();
            var text = System.IO.File.ReadAllText(old);
            // Type stamps name the old assembly. The shape is the same, so drop them and read it plainly.
            text = System.Text.RegularExpressions.Regex.Replace(text, "\"\\$type\"\\s*:\\s*\"[^\"]*\"\\s*,?", string.Empty);
            var moved = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(text);
            if (moved is null) return new Configuration();
            moved.Save();
            Plugin.Log.Information("Brought {Count} flows over from Flow.json", moved.Flows.Count);
            return moved;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not bring settings over from Flow.json");
            return new Configuration();
        }
    }
}

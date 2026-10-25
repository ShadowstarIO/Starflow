using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace Flow;

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
    public List<FlowItem> Flows { get; set; } = [];
    public List<string> Folders { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

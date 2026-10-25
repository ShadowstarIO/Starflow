using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Flow.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Flow settings###FlowConfig")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(700, 640),
        };
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var changed = false;
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { cfg.Enabled = enabled; changed = true; }
        var notify = cfg.NotifyInChat;
        if (ImGui.Checkbox("Chat notices", ref notify)) { cfg.NotifyInChat = notify; changed = true; }
        var open = cfg.OpenUiOnLoad;
        if (ImGui.Checkbox("Open the window when the plugin loads", ref open)) { cfg.OpenUiOnLoad = open; changed = true; }
        ImGui.Separator();
        ImGui.TextDisabled("Hold a flow while any of these are true. It will not start, stop, or send its queue.");
        ImGui.TextDisabled("A flow set to ignore quiet options runs anyway. Duty flows usually should.");
        changed |= Flag("Cutscene", cfg.QuietInCutscene, v => cfg.QuietInCutscene = v);
        changed |= Flag("Between areas", cfg.QuietBetweenAreas, v => cfg.QuietBetweenAreas = v);
        changed |= Flag("Occupied", cfg.QuietWhenOccupied, v => cfg.QuietWhenOccupied = v);
        changed |= Flag("Dead", cfg.QuietWhenDead, v => cfg.QuietWhenDead = v);
        changed |= Flag("Combat", cfg.QuietInCombat, v => cfg.QuietInCombat = v);
        changed |= Flag("Duty", cfg.QuietInDuty, v => cfg.QuietInDuty = v);
        ImGui.Separator();
        ImGui.TextDisabled("Logging out drops the queue. A half-sent macro does not resume.");
        var poll = cfg.PollSec;
        if (ImGui.SliderFloat("Check every", ref poll, 0.15f, 5f, "%.2f s"))
        {
            cfg.PollSec = poll;
            changed = true;
        }
        if (changed) cfg.Save();
    }

    private static bool Flag(string label, bool current, Action<bool> set)
    {
        var value = current;
        if (!ImGui.Checkbox(label, ref value)) return false;
        set(value);
        return true;
    }
}

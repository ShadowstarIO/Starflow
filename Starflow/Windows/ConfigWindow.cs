using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Starflow.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Starflow settings###FlowConfig")
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
        var wrap = cfg.WrapColumns;
        if (ImGui.Checkbox("Wrap column contents to fit", ref wrap)) { cfg.WrapColumns = wrap; changed = true; }
        ImGui.TextDisabled(wrap ? "Narrow columns wrap and squeeze their contents." : "Narrow columns keep one line and scroll sideways.");
        changed |= Flag("Remember on/off per character", cfg.PerCharacterOnOff, v => cfg.PerCharacterOnOff = v);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remembers which flows are on for each character. Switching characters switches flows to how you left them on that character.");
        if (cfg.PerCharacterOnOff) ImGui.TextDisabled($"Remembered for {cfg.CharacterOnOff.Count} character{(cfg.CharacterOnOff.Count == 1 ? string.Empty : "s")}. A new character starts from how flows are now.");
        ImGui.Separator();
        ImGui.TextDisabled("Hold a flow while any of these are true. It will not start, stop, or send its queue.");
        ImGui.TextDisabled("A flow set to ignore quiet options runs anyway. Duty flows usually should.");
        changed |= Flag("Cutscene", cfg.QuietInCutscene, v => cfg.QuietInCutscene = v);
        changed |= Flag("Between areas", cfg.QuietBetweenAreas, v => cfg.QuietBetweenAreas = v);
        changed |= Flag("Occupied", cfg.QuietWhenOccupied, v => cfg.QuietWhenOccupied = v);
        changed |= Flag("Dead", cfg.QuietWhenDead, v => cfg.QuietWhenDead = v);
        changed |= Flag("Combat", cfg.QuietInCombat, v => cfg.QuietInCombat = v);
        changed |= Flag("Duty", cfg.QuietInDuty, v => cfg.QuietInDuty = v);
        changed |= Flag("You have a target", cfg.QuietWhenTargeting, v => cfg.QuietWhenTargeting = v);
        changed |= Flag("A player is targeting you", cfg.QuietWhenTargeted, v => cfg.QuietWhenTargeted = v);
        changed |= Flag("Emoting", cfg.QuietWhenEmoting, v => cfg.QuietWhenEmoting = v);
        ImGui.Separator();
        ImGui.TextDisabled("When a flow in Prompt mode waits for a yes:");
        changed |= Flag("Show a toast", cfg.PromptToast, v => cfg.PromptToast = v);
        var sound = cfg.PromptSound;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderInt("Sound", ref sound, 0, 16, sound == 0 ? "none" : "<se.%d>"))
        {
            cfg.PromptSound = Math.Clamp(sound, 0, 16);
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("Play") && cfg.PromptSound > 0) Alerts.Sound(cfg.PromptSound);
        ImGui.TextDisabled("The sound plays only for you.");
        ImGui.Separator();
        changed |= DrawTemplates(cfg);
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

    private string newTemplate = string.Empty;

    // Saved search comments. The Search comment card picks from these.
    private bool DrawTemplates(Configuration cfg)
    {
        var changed = false;
        ImGui.TextDisabled("Search comment templates. Tokens such as {zone} work here.");
        for (var i = 0; i < cfg.CommentTemplates.Count; i++)
        {
            ImGui.PushID(i);
            if (ImGui.SmallButton("x"))
            {
                cfg.CommentTemplates.RemoveAt(i);
                changed = true;
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(cfg.CommentTemplates[i]);
            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(-60);
        ImGui.InputTextWithHint("##newTemplate", "In {zone} on {world}", ref newTemplate, ChatSender.CommentMax * 2);
        ImGui.SameLine();
        if (ImGui.Button("Add") && newTemplate.Trim().Length > 0)
        {
            if (!cfg.CommentTemplates.Contains(newTemplate.Trim())) cfg.CommentTemplates.Add(newTemplate.Trim());
            newTemplate = string.Empty;
            changed = true;
        }
        return changed;
    }

    private static bool Flag(string label, bool current, Action<bool> set)
    {
        var value = current;
        if (!ImGui.Checkbox(label, ref value)) return false;
        set(value);
        return true;
    }
}

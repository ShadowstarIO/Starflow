using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Starflow.Windows;

internal static class Ui
{
    public static readonly Vector4 Green = new(0.45f, 0.9f, 0.55f, 1);
    public static readonly Vector4 Teal = new(0.45f, 0.85f, 0.75f, 1);
    public static readonly Vector4 Amber = new(0.95f, 0.75f, 0.35f, 1);
    public static readonly Vector4 Red = new(0.95f, 0.45f, 0.4f, 1);
    public static readonly Vector4 Grey = new(0.6f, 0.6f, 0.6f, 1);

    // A drawn circle, so it fills whatever the font has. Filled means on (or true); a ring means off.
    public static void Dot(bool filled, Vector4 color, float height = 0)
    {
        var h = height > 0 ? height : ImGui.GetTextLineHeight();
        var pos = ImGui.GetCursorScreenPos();
        var r = ImGui.GetTextLineHeight() * 0.3f;
        var center = pos + new Vector2(r + 1, h * 0.5f);
        var draw = ImGui.GetWindowDrawList();
        var c = ImGui.GetColorU32(color);
        if (filled) draw.AddCircleFilled(center, r, c);
        else draw.AddCircle(center, r, c, 0, 1.5f);
        ImGui.Dummy(new Vector2(r * 2 + 2, h));
    }

    public static float DotWidth => ImGui.GetTextLineHeight() * 0.6f + 2;

    // ImGui.Combo plus Ctrl + mouse wheel over the closed box to step through the items, as Dalamud's own settings do.
    public static bool Combo(string label, ref int index, string[] items, int count)
    {
        var changed = ImGui.Combo(label, ref index, items, count);
        if (count <= 0 || !ImGui.IsItemHovered()) return changed;
        var io = ImGui.GetIO();
        if (!io.KeyCtrl || io.MouseWheel == 0) return changed;
        var next = Math.Clamp(index + (io.MouseWheel > 0 ? -1 : 1), 0, count - 1);
        if (next == index) return changed;
        index = next;
        return true;
    }

    // In place of SameLine for a row that should wrap: stays on this line when the next item fits, else starts a new one.
    // Off keeps every row on one line, as before, and the column scrolls sideways instead.
    public static bool Wrap { get; set; } = true;

    public static void Next(float nextWidth)
    {
        ImGui.SameLine();
        if (!Wrap) return;
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        if (ImGui.GetCursorScreenPos().X + nextWidth > right) ImGui.NewLine();
    }

    public static float CheckWidth(string label) =>
        ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(label.Split("##")[0]).X;

    public static float ButtonWidth(string label) =>
        ImGui.CalcTextSize(label.Split("##")[0]).X + ImGui.GetStyle().FramePadding.X * 2;

    // Width for a labelled field: the space left after its label, but never wider than wanted.
    public static float FieldWidth(string label, float wanted)
    {
        var text = ImGui.CalcTextSize(label.Split("##")[0]).X + ImGui.GetStyle().ItemInnerSpacing.X;
        return Math.Max(60, Math.Min(wanted, ImGui.GetContentRegionAvail().X - text));
    }

    public static void Tip(string text)
    {
        if (!ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    // "Current: Limsa Lominsa". Fills the field with what the game says right now.
    public static bool Current(string value, string? shown = null, string id = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return ImGui.SmallButton($"Current: {shown ?? value}##cur{id}");
    }

    public static void Wrapped(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
}

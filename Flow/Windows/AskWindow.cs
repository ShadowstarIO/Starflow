using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Flow.Windows;

public sealed class AskWindow : Window
{
    private readonly Plugin plugin;

    public AskWindow(Plugin plugin) : base("Flow asks###FlowAsk")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 180),
            MaximumSize = new Vector2(640, 480),
        };
    }

    public override void Draw()
    {
        var engine = plugin.Engine;
        if (engine.Waiting == 0)
        {
            ImGui.TextDisabled("Nothing is waiting.");
            return;
        }

        foreach (var confirm in engine.Confirms.ToArray())
        {
            ImGui.PushID(confirm.Card.Id);
            ImGui.TextUnformatted($"{confirm.FlowName}: {confirm.Card.Label}");
            ImGui.TextDisabled("This card asked for a yes before it runs.");
            if (ImGui.Button("Yes")) engine.Answer(confirm, true);
            ImGui.SameLine();
            if (ImGui.Button("No")) engine.Answer(confirm, false);
            ImGui.Separator();
            ImGui.PopID();
        }

        foreach (var id in engine.Pending.ToArray())
        {
            var flow = plugin.Configuration.Flows.Find(f => f.Id == id);
            if (flow is null) continue;
            ImGui.PushID(id);
            ImGui.TextUnformatted(flow.Name);
            if (!string.IsNullOrWhiteSpace(flow.Notes)) ImGui.TextDisabled(flow.Notes);
            if (ImGui.Button("Run")) engine.Accept(id);
            ImGui.SameLine();
            if (ImGui.Button("Skip")) engine.Decline(id);
            ImGui.SameLine();
            ImGui.TextDisabled("Skip holds it until When goes false.");
            ImGui.Separator();
            ImGui.PopID();
        }
    }
}

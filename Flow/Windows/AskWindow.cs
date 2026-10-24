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
        var pending = plugin.Engine.Pending;
        if (pending.Count == 0)
        {
            ImGui.TextDisabled("Nothing is waiting.");
            return;
        }

        foreach (var id in pending.ToArray())
        {
            var flow = plugin.Configuration.Flows.Find(f => f.Id == id);
            if (flow is null) continue;
            ImGui.TextUnformatted(flow.Name);
            if (!string.IsNullOrWhiteSpace(flow.Notes)) ImGui.TextDisabled(flow.Notes);
            if (ImGui.Button("Run##" + id)) plugin.Engine.Accept(id);
            ImGui.SameLine();
            if (ImGui.Button("Skip##" + id)) plugin.Engine.Decline(id);
            ImGui.Separator();
        }
    }
}

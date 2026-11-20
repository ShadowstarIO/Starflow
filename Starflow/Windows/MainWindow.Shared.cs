using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace Starflow.Windows;

// Shared flows from the GitHub repo's flows folder: a list of names and descriptions, a preview of the
// one you pick, and Add, which brings it in off like any import.
public sealed partial class MainWindow
{
    private bool showShared;
    private string sharedPick = string.Empty;
    private string sharedMessage = string.Empty;

    private void OpenShared()
    {
        showShared = true;
        showImport = false;
        selectedCard = null;
        sharedMessage = string.Empty;
        SharedFlows.Refresh(false);
    }

    private void DrawShared(Configuration cfg)
    {
        ImGui.TextUnformatted("Shared flows");
        ImGui.SameLine();
        if (ImGui.SmallButton(SharedFlows.Loading ? "Loading..." : "Refresh")) SharedFlows.Refresh(true);
        ImGui.SameLine();
        if (ImGui.SmallButton("Open folder")) Util.OpenLink(SharedIndex.Page);
        ImGui.SameLine();
        if (ImGui.SmallButton("Close")) showShared = false;
        ImGui.TextDisabled("From the flows folder on GitHub, made by me and other players. They come in off.");
        ImGui.Separator();

        if (SharedFlows.Error.Length > 0) Ui.Wrapped(Amber, SharedFlows.Error);
        var entries = SharedFlows.Entries;
        if (entries.Count == 0)
        {
            ImGui.TextDisabled(SharedFlows.Loading ? "Loading the list..." : "No shared flows yet.");
            return;
        }

        foreach (var entry in entries)
        {
            var picked = entry.File == sharedPick;
            if (ImGui.Selectable($"{entry.Name}##shared:{entry.File}", picked))
            {
                sharedPick = picked ? string.Empty : entry.File;
                sharedMessage = string.Empty;
                if (!picked) SharedFlows.Fetch(entry);
            }
            if (entry.Description.Length > 0) Ui.Wrapped(Ui.Grey, entry.Description);
            if (entry.Needs.Count > 0) ImGui.TextDisabled("Needs: " + string.Join(", ", entry.Needs));
            if (picked) DrawSharedPick(cfg, entry);
            ImGui.Spacing();
        }
    }

    private void DrawSharedPick(Configuration cfg, SharedIndex.Entry entry)
    {
        ImGui.Indent();
        var flows = SharedFlows.FlowsIn(entry);
        if (flows is null)
        {
            var error = SharedFlows.ErrorFor(entry);
            if (error.Length > 0)
            {
                Ui.Wrapped(Amber, error);
                if (ImGui.SmallButton("Retry")) SharedFlows.Fetch(entry, true);
                ImGui.SameLine();
            }
            else
            {
                // Picked while another file was downloading: start this one once that finishes.
                SharedFlows.Fetch(entry);
                ImGui.TextDisabled("Loading...");
            }
            if (ImGui.SmallButton("Open on GitHub")) Util.OpenLink(entry.PageUrl);
            ImGui.Unindent();
            return;
        }

        foreach (var flow in flows)
        {
            ImGui.TextUnformatted(flow.Name);
            if (flow.Notes.Trim().Length > 0) Ui.Wrapped(Ui.Grey, flow.Notes.Trim());
            foreach (var section in new[] { Section.When, Section.Do, Section.Until, Section.After })
            {
                var cards = flow.In(section);
                if (cards.Count == 0) continue;
                var join = section == Section.When ? (flow.WhenJoin == Join.All ? " (all)" : " (any)") : string.Empty;
                ImGui.TextDisabled($"{section}{join}: " + string.Join(" · ", cards.Select(c => c.Off ? c.Title + " (off)" : c.Title)));
            }
            if (Catalog.IsReactive(flow) || flow.Cards.Any(c => c.Type == CardType.Chat))
                Ui.Wrapped(Amber, "Reacts to chat, combat, or other players. Check who can set it off before turning it on.");
            var missing = flow.Cards.Where(c => c.Type == CardType.PluginSet || c.Type == CardType.Collection).Select(c => c.Value).Where(v => v.Length > 0).Distinct().ToList();
            if (missing.Count > 0) ImGui.TextDisabled("Touches plugins: " + string.Join(", ", missing));
        }

        if (ImGui.Button(flows.Count == 1 ? "Add##shared" : $"Add all {flows.Count}##shared"))
        {
            string last = string.Empty;
            foreach (var shared in flows)
            {
                var json = Share.One(shared);
                if (!Share.TryRead(json, out var copies, out _)) continue;
                var flow = copies[0];
                flow.Id = Guid.NewGuid().ToString("N");
                foreach (var card in flow.Cards) card.Id = Guid.NewGuid().ToString("N");
                flow.Enabled = false;
                cfg.Flows.Add(flow);
                last = flow.Id;
            }
            cfg.Save();
            sharedMessage = $"Added {flows.Count}. {(flows.Count == 1 ? "It is" : "They are")} off.";
            if (last.Length > 0)
            {
                selectedId = last;
                selectedCard = null;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Open on GitHub##shared")) Util.OpenLink(entry.PageUrl);
        if (sharedMessage.Length > 0) ImGui.TextDisabled(sharedMessage);
        ImGui.Unindent();
    }
}

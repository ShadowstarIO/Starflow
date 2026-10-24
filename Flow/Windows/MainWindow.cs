using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Flow.Windows;

public sealed class MainWindow : Window
{
    private readonly Plugin plugin;
    private string filter = string.Empty;
    private string newName = string.Empty;
    private string? selectedId;
    private string? selectedCard;
    private string importText = string.Empty;
    private string importMsg = string.Empty;

    public MainWindow(Plugin plugin) : base($"Flow v{Plugin.AppVersion}###FlowMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1080, 640),
            MaximumSize = new Vector2(1900, 1400),
        };
    }

    public void OpenFlow(string id)
    {
        selectedId = id;
        IsOpen = true;
    }

    public override void Draw()
    {
        WindowName = $"Flow v{Plugin.AppVersion}###FlowMain";
        var cfg = plugin.Configuration;
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { cfg.Enabled = enabled; cfg.Save(); }
        ImGui.SameLine();
        if (ImGui.Button("Ask")) plugin.OpenAsk();
        ImGui.SameLine();
        if (ImGui.Button("Settings")) plugin.ToggleConfig();
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.IsPaused ? "Paused" : plugin.Engine.Last.Line());
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("list", new Vector2(250, avail.Y), true);
        DrawList(cfg);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("graph", new Vector2(Math.Max(360, avail.X - 580), avail.Y), true);
        DrawGraph(cfg);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("card", new Vector2(0, avail.Y), true);
        DrawInspector(cfg);
        ImGui.EndChild();
    }

    private void DrawList(Configuration cfg)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Filter", ref filter, 80);
        ImGui.SetNextItemWidth(-64);
        ImGui.InputTextWithHint("##new", "New flow", ref newName, 80);
        ImGui.SameLine();
        if (ImGui.Button("Add") && !string.IsNullOrWhiteSpace(newName))
        {
            var flow = new FlowItem { Name = newName.Trim(), Enabled = false, Mode = RunMode.Off };
            cfg.Flows.Add(flow);
            cfg.Save();
            newName = string.Empty;
            OpenFlow(flow.Id);
        }

        if (ImGui.Button("Duty template")) Add(cfg, Catalog.DutyTemplate());
        ImGui.SameLine();
        if (ImGui.Button("Town template")) Add(cfg, Catalog.TownTemplate());

        ImGui.Separator();
        foreach (var group in cfg.Flows.GroupBy(f => f.FolderKey).OrderBy(g => g.Key))
        {
            if (group.Key.Length > 0) ImGui.TextDisabled(group.Key);
            foreach (var flow in group)
            {
                if (filter.Length > 0 && !flow.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                var on = plugin.Engine.IsActive(flow.Id);
                if (on) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.45f, 0.85f, 0.75f, 1));
                var label = flow.Enabled ? flow.Name : flow.Name + "  ·  off";
                if (ImGui.Selectable(label + "##" + flow.Id, flow.Id == selectedId))
                {
                    selectedId = flow.Id;
                    selectedCard = null;
                }
                if (on) ImGui.PopStyleColor();
            }
        }
    }

    private void DrawGraph(Configuration cfg)
    {
        var flow = cfg.Flows.Find(f => f.Id == selectedId);
        if (flow is null)
        {
            ImGui.TextDisabled("Pick a flow, or add one.");
            ImGui.TextWrapped("When decides. Do runs on the way in. Until can end it early. After runs on the way out.");
            return;
        }

        var enabled = flow.Enabled;
        if (ImGui.Checkbox("On", ref enabled)) { flow.Enabled = enabled; cfg.Save(); }
        ImGui.SameLine();
        var mode = (int)flow.Mode;
        ImGui.SetNextItemWidth(110);
        string[] modes = ["Off", "Ask", "Auto"];
        if (ImGui.Combo("##mode", ref mode, modes, modes.Length)) { flow.Mode = (RunMode)mode; cfg.Save(); }
        ImGui.SameLine();
        var stop = (int)flow.OnStop;
        ImGui.SetNextItemWidth(110);
        string[] stops = ["Revert", "Keep"];
        if (ImGui.Combo("##stop", ref stop, stops, stops.Length)) { flow.OnStop = (StopMode)stop; cfg.Save(); }
        ImGui.SameLine();
        if (ImGui.Button("Run Do")) plugin.Engine.RunOnce(flow);
        ImGui.SameLine();
        if (ImGui.Button("Stop")) plugin.Engine.Stop(flow, false);

        var name = flow.Name;
        if (ImGui.InputText("Name", ref name, 80)) { flow.Name = name; cfg.Save(); }
        var folder = flow.Folder;
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputText("Folder", ref folder, 60)) { flow.Folder = folder; cfg.Save(); }
        ImGui.SameLine();
        var priority = flow.Priority;
        ImGui.SetNextItemWidth(80);
        if (ImGui.InputInt("Priority", ref priority)) { flow.Priority = priority; cfg.Save(); }
        var hold = flow.HoldSec;
        ImGui.SetNextItemWidth(120);
        if (ImGui.SliderFloat("Hold", ref hold, 0, 30, "%.1f s")) { flow.HoldSec = hold; cfg.Save(); }
        var notes = flow.Notes;
        if (ImGui.InputTextMultiline("##notes", ref notes, 400, new Vector2(-1, 48))) { flow.Notes = notes; cfg.Save(); }

        var whenJoin = flow.WhenJoin == Join.All;
        if (ImGui.Checkbox("When is all", ref whenJoin)) { flow.WhenJoin = whenJoin ? Join.All : Join.Any; cfg.Save(); }
        ImGui.SameLine();
        var untilAny = flow.UntilJoin == Join.Any;
        if (ImGui.Checkbox("Until is any", ref untilAny)) { flow.UntilJoin = untilAny ? Join.Any : Join.All; cfg.Save(); }

        var boxes = new List<(Vector2 Top, Vector2 Bottom)>();
        DrawSection(cfg, flow, Section.When, boxes);
        DrawSection(cfg, flow, Section.Do, boxes);
        DrawSection(cfg, flow, Section.Until, boxes);
        DrawSection(cfg, flow, Section.After, boxes);
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(new Vector4(0.45f, 0.75f, 0.68f, 0.9f));
        for (var i = 1; i < boxes.Count; i++)
            draw.AddLine(boxes[i - 1].Bottom, boxes[i].Top, color, 2f);

        if (ImGui.Button("Copy JSON")) ImGui.SetClipboardText(Share.One(flow));
        ImGui.SameLine();
        if (ImGui.Button("Delete flow"))
        {
            plugin.Engine.Stop(flow, true);
            cfg.Flows.Remove(flow);
            selectedId = null;
            selectedCard = null;
            cfg.Save();
        }
    }

    private void DrawSection(Configuration cfg, FlowItem flow, Section section, List<(Vector2 Top, Vector2 Bottom)> boxes)
    {
        ImGui.PushID((int)section);
        var title = section switch
        {
            Section.When => "When",
            Section.Do => "Do",
            Section.Until => "Until",
            _ => "After",
        };
        ImGui.TextUnformatted(title);
        ImGui.SameLine();
        if (ImGui.Button("+")) ImGui.OpenPopup("add");
        if (ImGui.BeginPopup("add"))
        {
            foreach (var desc in Catalog.For(section))
            {
                if (!ImGui.Selectable(desc.Label)) continue;
                var card = Catalog.Make(section, desc.Type);
                flow.Cards.Add(card);
                selectedCard = card.Id;
                cfg.Save();
            }
            ImGui.EndPopup();
        }

        var cards = flow.In(section);
        var height = 28f + Math.Max(1, cards.Count) * 24f;
        ImGui.BeginChild("node", new Vector2(-1, height), true);
        if (cards.Count == 0) ImGui.TextDisabled("Empty");
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var live = (section is Section.When or Section.Until)
                && plugin.Engine.Last.LoggedIn
                && Eval.CardMatches(card, plugin.Engine.Last);
            if (live) ImGui.TextColored(new Vector4(0.45f, 0.9f, 0.55f, 1), "●");
            else ImGui.TextDisabled("○");
            ImGui.SameLine();
            var key = Catalog.Key(section, i);
            if (ImGui.Selectable($"{key}   {card.Label}", card.Id == selectedCard))
                selectedCard = card.Id;
        }
        ImGui.EndChild();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        boxes.Add((new Vector2((min.X + max.X) * 0.5f, min.Y), new Vector2((min.X + max.X) * 0.5f, max.Y)));
        ImGui.PopID();
    }

    private void DrawInspector(Configuration cfg)
    {
        var flow = cfg.Flows.Find(f => f.Id == selectedId);
        if (flow is null)
        {
            DrawImport(cfg);
            return;
        }

        var card = flow.Cards.Find(c => c.Id == selectedCard);
        if (card is null)
        {
            ImGui.TextDisabled("Select a card.");
            foreach (var line in plugin.Engine.Why(flow))
                ImGui.TextWrapped(line);
            ImGui.Separator();
            DrawImport(cfg);
            return;
        }

        ImGui.TextUnformatted(card.Label);
        var index = flow.Cards.IndexOf(card);
        if (ImGui.Button("Up") && index > 0) Move(cfg, flow, index, -1);
        ImGui.SameLine();
        if (ImGui.Button("Down") && index >= 0 && index < flow.Cards.Count - 1) Move(cfg, flow, index, 1);
        ImGui.SameLine();
        if (ImGui.Button("Remove"))
        {
            flow.Cards.Remove(card);
            selectedCard = null;
            cfg.Save();
            return;
        }

        if (CardType.IsCondition(card.Type))
        {
            var not = card.Not;
            if (ImGui.Checkbox("Not", ref not)) { card.Not = not; cfg.Save(); }
        }

        Edit(cfg, card);
        ImGui.Separator();
        ImGui.TextDisabled("Why");
        foreach (var line in plugin.Engine.Why(flow))
            ImGui.TextWrapped(line);
    }

    private void Edit(Configuration cfg, Card card)
    {
        switch (card.Type)
        {
            case CardType.State:
                Combo(cfg, card, "State", Catalog.States, v => card.Value = v, card.Value);
                break;
            case CardType.Place:
                Combo(cfg, card, "Place", Catalog.Places, v => card.Value = v, card.Value);
                break;
            case CardType.Duty:
                Combo(cfg, card, "Duty", Catalog.Duties, v => card.Value = v, card.Value);
                break;
            case CardType.Role:
                Combo(cfg, card, "Role", Catalog.Roles, v => card.Value = v, card.Value);
                break;
            case CardType.Target:
                Combo(cfg, card, "Target", Catalog.Targets, v => card.Value = v, card.Value);
                break;
            case CardType.Job:
            case CardType.Zone:
            case CardType.World:
            case CardType.DataCenter:
            case CardType.Status:
            case CardType.Plugin:
            case CardType.Weekday:
                Text(cfg, card, "Value", v => card.Value = v, card.Value, "WHM|SCH   Limsa   Aether   sat,sun");
                break;
            case CardType.Party:
            case CardType.Nearby:
            case CardType.Level:
                Text(cfg, card, "Compare", v => card.Value = v, card.Value, ">=4");
                break;
            case CardType.Time:
                Text(cfg, card, "Range", v => card.Value = v, card.Value, "18:00-06:00 or et 17-22");
                break;
            case CardType.Command:
                Text(cfg, card, "Command", v => card.Value = v, card.Value, "/echo hello");
                Int(cfg, card, "Repeat", card.Repeat, v => card.Repeat = Math.Clamp(v, 1, 30));
                Int(cfg, card, "Gap ms", card.WaitMs, v => card.WaitMs = Math.Max(0, v));
                break;
            case CardType.Wait:
                Int(cfg, card, "Milliseconds", card.WaitMs, v => card.WaitMs = Math.Max(0, v));
                break;
            case CardType.Notice:
                Text(cfg, card, "Text", v => card.Value = v, card.Value, "Duty flow");
                break;
            case CardType.Online:
                Combo(cfg, card, "Status", ["online", "away", "busy", "rp", "lfp", "meld"], v => card.Value = v, card.Value);
                break;
            case CardType.Collection:
                Text(cfg, card, "Collection", v => card.Value = v, card.Value, "Collection name");
                var enableCollection = card.Flag;
                if (ImGui.Checkbox("Enable", ref enableCollection)) { card.Flag = enableCollection; cfg.Save(); }
                ImGui.TextDisabled(enableCollection ? "Enables the collection." : "Disables the collection.");
                break;
            case CardType.PluginSet:
                Text(cfg, card, "Plugin", v => card.Value = v, card.Value, "Plugin name");
                var enablePlugin = card.Flag;
                if (ImGui.Checkbox("Enable##plugin", ref enablePlugin)) { card.Flag = enablePlugin; cfg.Save(); }
                var keep = card.Permanent;
                if (ImGui.Checkbox("Remember", ref keep)) { card.Permanent = keep; cfg.Save(); }
                ImGui.TextDisabled(keep ? "Saved in the collection." : "Temporary. Resets when the game restarts.");
                break;
            case CardType.Setting:
                EditSetting(cfg, card);
                break;
            case CardType.Glamour:
                Text(cfg, card, "Design", v => card.Value = v, card.Value, "Design name");
                if (ImGui.Button("List designs"))
                    importMsg = string.Join(", ", Bridges.GlamourNames().Take(12));
                if (importMsg.Length > 0) ImGui.TextWrapped(importMsg);
                break;
            case CardType.PenumbraMod:
                Text(cfg, card, "Directory", v => card.Option = v, card.Option, "mod directory");
                Text(cfg, card, "Name", v => card.Value = v, card.Value, "mod name");
                var on = card.Flag;
                if (ImGui.Checkbox("Enabled", ref on)) { card.Flag = on; cfg.Save(); }
                Int(cfg, card, "Priority", card.Number, v => card.Number = v);
                var permanent = card.Permanent;
                if (ImGui.Checkbox("Permanent", ref permanent)) { card.Permanent = permanent; cfg.Save(); }
                ImGui.TextDisabled(permanent ? "Writes the collection." : "Temporary, cleared with Penumbra clear temp.");
                break;
            case CardType.PenumbraCollection:
                Text(cfg, card, "Collection", v => card.Value = v, card.Value, "collection name");
                break;
            case CardType.PenumbraReset:
                Text(cfg, card, "Directory", v => card.Option = v, card.Option, "blank clears all temp from Flow");
                break;
        }
    }

    private void EditSetting(Configuration cfg, Card card)
    {
        var section = card.Option == "Ui" ? 1 : 0;
        string[] sections = ["System", "Ui"];
        if (ImGui.Combo("Section", ref section, sections, sections.Length))
        {
            card.Option = section == 1 ? "Ui" : "System";
            cfg.Save();
        }

        var names = Catalog.Settings.Where(s => s.Section == card.Option).Select(s => s.Name).ToArray();
        var pick = Math.Max(0, Array.IndexOf(names, card.Value));
        if (names.Length > 0 && ImGui.Combo("Option", ref pick, names, names.Length))
        {
            card.Value = names[pick];
            cfg.Save();
        }
        Text(cfg, card, "Name", v => card.Value = v, card.Value, "Config option");
        Text(cfg, card, "Value", v => card.Extra = v, card.Extra, "0");
        var hint = Catalog.Settings.FirstOrDefault(s => s.Name == card.Value);
        if (hint is not null) ImGui.TextDisabled(hint.Hint);
    }

    private void Text(Configuration cfg, Card card, string label, Action<string> set, string current, string hint)
    {
        var value = current ?? string.Empty;
        if (!ImGui.InputTextWithHint(label, hint, ref value, 240)) return;
        set(value);
        cfg.Save();
    }

    private void Int(Configuration cfg, Card card, string label, int current, Action<int> set)
    {
        var value = current;
        if (!ImGui.InputInt(label, ref value)) return;
        set(value);
        cfg.Save();
    }

    private void Combo(Configuration cfg, Card card, string label, string[] items, Action<string> set, string current)
    {
        var index = Math.Max(0, Array.IndexOf(items, current));
        if (!ImGui.Combo(label, ref index, items, items.Length)) return;
        set(items[index]);
        cfg.Save();
    }

    private void Move(Configuration cfg, FlowItem flow, int index, int delta)
    {
        var card = flow.Cards[index];
        var section = flow.In(card.Section);
        var at = section.IndexOf(card);
        var swap = section[at + delta];
        var i = flow.Cards.IndexOf(card);
        var j = flow.Cards.IndexOf(swap);
        (flow.Cards[i], flow.Cards[j]) = (flow.Cards[j], flow.Cards[i]);
        cfg.Save();
    }

    private void Add(Configuration cfg, FlowItem flow)
    {
        cfg.Flows.Add(flow);
        cfg.Save();
        OpenFlow(flow.Id);
    }

    private void DrawImport(Configuration cfg)
    {
        ImGui.TextDisabled("Paste a flow");
        ImGui.InputTextMultiline("##import", ref importText, 8000, new Vector2(-1, 120));
        if (ImGui.Button("Import"))
        {
            if (Share.TryRead(importText, out var flows, out var error))
            {
                foreach (var flow in flows)
                {
                    flow.Id = Guid.NewGuid().ToString("N");
                    flow.Enabled = false;
                    flow.Mode = RunMode.Off;
                    cfg.Flows.Add(flow);
                }
                cfg.Save();
                importMsg = $"Imported {flows.Count}. They are off.";
                importText = string.Empty;
                if (flows.Count > 0) OpenFlow(cfg.Flows[^1].Id);
            }
            else importMsg = error;
        }
        if (importMsg.Length > 0) ImGui.TextWrapped(importMsg);
    }
}

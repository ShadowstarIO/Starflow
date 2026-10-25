using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Flow.Windows;

public sealed partial class MainWindow : Window
{
    private const string DragType = "FLOW_ITEM";
    private static readonly Vector4 Green = new(0.45f, 0.9f, 0.55f, 1);
    private static readonly Vector4 Teal = new(0.45f, 0.85f, 0.75f, 1);
    private static readonly Vector4 Amber = new(0.95f, 0.75f, 0.35f, 1);
    private static readonly Vector4 Red = new(0.95f, 0.45f, 0.4f, 1);

    private readonly Plugin plugin;
    private string filter = string.Empty;
    private string newName = string.Empty;
    private string newFolder = string.Empty;
    private string? selectedId;
    private string? selectedCard;
    private string? dragId;
    private string? dryFor;
    private bool showImport;
    private int importKind;
    private string importText = string.Empty;
    private string importMsg = string.Empty;

    public MainWindow(Plugin plugin) : base($"Flow v{Plugin.AppVersion}###FlowMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1080, 640),
            MaximumSize = new Vector2(2400, 1600),
        };
    }

    public void OpenFlow(string id)
    {
        selectedId = id;
        selectedCard = null;
        showImport = false;
        IsOpen = true;
    }

    public override void Draw()
    {
        WindowName = $"Flow v{Plugin.AppVersion}###FlowMain";
        var cfg = plugin.Configuration;
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { cfg.Enabled = enabled; cfg.Save(); }
        ImGui.SameLine();
        var waiting = plugin.Engine.Waiting;
        if (waiting > 0) ImGui.PushStyleColor(ImGuiCol.Text, Amber);
        if (ImGui.Button(waiting > 0 ? $"Ask ({waiting})###ask" : "Ask###ask")) plugin.OpenAsk();
        if (waiting > 0) ImGui.PopStyleColor();
        ImGui.SameLine();
        if (ImGui.Button("Settings")) plugin.ToggleConfig();
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.IsPaused ? "Paused" : plugin.Engine.Last.Line());
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("list", new Vector2(270, avail.Y), true);
        DrawList(cfg);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("graph", new Vector2(Math.Max(380, avail.X - 270 - 400), avail.Y), true);
        DrawGraph(cfg);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("card", new Vector2(0, avail.Y), true);
        DrawInspector(cfg);
        ImGui.EndChild();
    }

    // ---------- list ----------

    private void DrawList(Configuration cfg)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Filter", ref filter, 80);
        ImGui.SetNextItemWidth(-64);
        ImGui.InputTextWithHint("##new", "New flow", ref newName, 80);
        ImGui.SameLine();
        if (ImGui.Button("Add") && !string.IsNullOrWhiteSpace(newName))
        {
            var flow = new FlowItem { Name = newName.Trim(), Enabled = false, Mode = RunMode.Auto, Folder = CurrentFolder(cfg) };
            newName = string.Empty;
            Add(cfg, flow);
        }

        if (ImGui.Button("Templates")) ImGui.OpenPopup("templates");
        if (ImGui.BeginPopup("templates"))
        {
            ImGui.TextDisabled("Templates open off. Fill the blank fields to turn them on.");
            if (ImGui.Selectable("Duty: swap plugin collections")) Add(cfg, Catalog.DutyTemplate());
            if (ImGui.Selectable("Town: search comment in town")) Add(cfg, Catalog.TownTemplate());
            if (ImGui.Selectable("Housing: at a plot")) Add(cfg, Catalog.HousingTemplate());
            if (ImGui.Selectable("Schedule: weekday evenings")) Add(cfg, Catalog.ScheduleTemplate());
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Import"))
        {
            showImport = true;
            selectedCard = null;
        }

        ImGui.SetNextItemWidth(-64);
        ImGui.InputTextWithHint("##folder", "New folder (a/b)", ref newFolder, 80);
        ImGui.SameLine();
        if (ImGui.Button("Folder") && Folders.Clean(newFolder).Length > 0)
        {
            var path = Folders.Clean(newFolder);
            if (!cfg.Folders.Contains(path, StringComparer.OrdinalIgnoreCase)) cfg.Folders.Add(path);
            newFolder = string.Empty;
            cfg.Save();
        }

        ImGui.Separator();
        ImGui.BeginChild("tree", Vector2.Zero, false);
        ImGui.TextDisabled("Drag a flow onto a folder.");
        ImGui.Selectable("All flows (top level)##root", false);
        DropTarget(cfg, string.Empty);
        DrawFolder(cfg, string.Empty, AllFolders(cfg));
        ImGui.EndChild();
    }

    private void DrawFolder(Configuration cfg, string path, List<string> all)
    {
        foreach (var child in all.Where(f => Folders.Parent(f).Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            var count = cfg.Flows.Count(f => Folders.Under(f.FolderKey, child));
            var open = ImGui.TreeNodeEx("folder:" + child, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth, $"{Folders.Leaf(child)}  ({count})");
            DropTarget(cfg, child);
            if (ImGui.BeginPopupContextItem("ctx:" + child, ImGuiPopupFlags.MouseButtonRight))
            {
                if (ImGui.Selectable("Remove folder (flows move up)"))
                {
                    var parent = Folders.Parent(child);
                    foreach (var flow in cfg.Flows.Where(f => Folders.Under(f.FolderKey, child)))
                        flow.Folder = Folders.Clean(parent + "/" + flow.FolderKey[child.Length..]);
                    cfg.Folders.RemoveAll(f => Folders.Under(f, child));
                    cfg.Save();
                }
                ImGui.EndPopup();
            }
            if (!open) continue;
            DrawFolder(cfg, child, all);
            ImGui.TreePop();
        }

        foreach (var flow in cfg.Flows.Where(f => f.FolderKey.Equals(path, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(f => f.Priority).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList())
        {
            FlowRow(flow);
        }
    }

    private void FlowRow(FlowItem flow)
    {
        if (filter.Length > 0 && !flow.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
        var engine = plugin.Engine;
        var on = engine.IsActive(flow.Id);
        var asking = engine.Pending.Contains(flow.Id);
        if (on) ImGui.TextColored(Green, "●");
        else if (asking) ImGui.TextColored(Amber, "●");
        else ImGui.TextDisabled("○");
        ImGui.SameLine();

        var tail = !flow.Enabled ? "  · off"
            : on ? "  · on"
            : asking ? "  · asks"
            : engine.IsHeld(flow.Id) ? "  · held"
            : string.Empty;
        if (on) ImGui.PushStyleColor(ImGuiCol.Text, Teal);
        if (ImGui.Selectable($"{flow.Name}{tail}##{flow.Id}", flow.Id == selectedId))
        {
            selectedId = flow.Id;
            selectedCard = null;
            showImport = false;
        }
        if (on) ImGui.PopStyleColor();

        if (ImGui.BeginDragDropSource())
        {
            dragId = flow.Id;
            ImGui.SetDragDropPayload(DragType, ReadOnlySpan<byte>.Empty, ImGuiCond.None);
            ImGui.TextUnformatted(flow.Name);
            ImGui.EndDragDropSource();
        }
    }

    private void DropTarget(Configuration cfg, string folder)
    {
        if (!ImGui.BeginDragDropTarget()) return;
        var payload = ImGui.AcceptDragDropPayload(DragType, ImGuiDragDropFlags.None);
        if (!payload.IsNull && dragId is not null)
        {
            var flow = cfg.Flows.Find(f => f.Id == dragId);
            if (flow is not null)
            {
                flow.Folder = folder;
                cfg.Save();
            }
            dragId = null;
        }
        ImGui.EndDragDropTarget();
    }

    private static List<string> AllFolders(Configuration cfg)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddPath(string path)
        {
            for (var p = Folders.Clean(path); p.Length > 0; p = Folders.Parent(p)) set.Add(p);
        }
        foreach (var f in cfg.Folders) AddPath(f);
        foreach (var flow in cfg.Flows) AddPath(flow.FolderKey);
        return set.ToList();
    }

    private string CurrentFolder(Configuration cfg) => cfg.Flows.Find(f => f.Id == selectedId)?.FolderKey ?? string.Empty;

    // ---------- graph ----------

    private void DrawGraph(Configuration cfg)
    {
        var flow = cfg.Flows.Find(f => f.Id == selectedId);
        if (flow is null)
        {
            ImGui.TextDisabled("Pick a flow, or add one.");
            ImGui.TextWrapped("When decides. Do runs on the way in. Until can end it early. After runs on the way out, then Revert puts slotted values back.");
            return;
        }

        var engine = plugin.Engine;
        var missing = Catalog.Missing(flow);
        var enabled = flow.Enabled;
        if (missing.Count > 0 && !enabled) ImGui.BeginDisabled();
        if (ImGui.Checkbox("On", ref enabled)) { flow.Enabled = enabled; cfg.Save(); }
        if (missing.Count > 0 && !enabled)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Fill the blank fields first.");
        }
        ImGui.SameLine();
        var mode = (int)flow.Mode;
        ImGui.SetNextItemWidth(90);
        string[] modes = ["Off", "Ask", "Auto"];
        if (ImGui.Combo("##mode", ref mode, modes, modes.Length)) { flow.Mode = (RunMode)mode; cfg.Save(); }
        Tip("Off never starts. Ask offers it in the Ask window. Auto starts on its own.");
        ImGui.SameLine();
        var stop = (int)flow.OnStop;
        ImGui.SetNextItemWidth(90);
        string[] stops = ["Revert", "Keep"];
        if (ImGui.Combo("##stop", ref stop, stops, stops.Length)) { flow.OnStop = (StopMode)stop; cfg.Save(); }
        Tip("Revert puts back settings, status, glamour, and Penumbra mods this flow changed, after After runs. Keep leaves them.");
        ImGui.SameLine();
        if (ImGui.Button("Run once")) plugin.Engine.RunOnce(flow);
        Tip("One-shot. Runs Do now without turning the flow on. Slotted cards are skipped unless the flow is on.");
        ImGui.SameLine();
        if (ImGui.Button("Stop")) plugin.Engine.Stop(flow, false);
        Tip("Runs After and reverts. The flow stays off until When goes false.");
        ImGui.SameLine();
        if (ImGui.Button(dryFor == flow.Id ? "Hide dry run" : "Dry run")) dryFor = dryFor == flow.Id ? null : flow.Id;

        var state = engine.IsActive(flow.Id) ? ("On", Green)
            : engine.Pending.Contains(flow.Id) ? ("Waiting for a yes", Amber)
            : engine.IsHeld(flow.Id) ? ("Held until When goes false", Amber)
            : engine.IsRunning(flow.Id) ? ("Finishing", Teal)
            : ("Not running", new Vector4(0.6f, 0.6f, 0.6f, 1));
        ImGui.TextColored(state.Item2, state.Item1);
        foreach (var line in missing) ImGui.TextColored(Amber, line);

        var name = flow.Name;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("Name", ref name, 80)) { flow.Name = name; cfg.Save(); }
        ImGui.SameLine();
        var priority = flow.Priority;
        ImGui.SetNextItemWidth(90);
        if (ImGui.InputInt("Priority", ref priority)) { flow.Priority = priority; cfg.Save(); }
        Tip("Higher priority wins a slot two flows both want.");

        var folder = flow.Folder;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("Folder", ref folder, 80)) { flow.Folder = folder; cfg.Save(); }
        ImGui.SameLine();
        var hold = flow.HoldSec;
        ImGui.SetNextItemWidth(120);
        if (ImGui.SliderFloat("Hold", ref hold, 0, 30, "%.1f s")) { flow.HoldSec = hold; cfg.Save(); }
        Tip("When must stay true this long before Do runs.");

        var quiet = flow.IgnoreQuiet ? 1 : 0;
        string[] quiets = ["Use quiet options", "Ignore quiet options"];
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("Quiet", ref quiet, quiets, quiets.Length)) { flow.IgnoreQuiet = quiet == 1; cfg.Save(); }
        Tip("Quiet options in Settings hold flows in combat, cutscenes, duties, and zone lines. A duty flow should ignore them.");

        var notes = flow.Notes;
        if (ImGui.InputTextMultiline("##notes", ref notes, 600, new Vector2(-1, 44))) { flow.Notes = notes; cfg.Save(); }

        ImGui.Spacing();
        var boxes = new List<(Vector2 Top, Vector2 Bottom)>();
        DrawSection(cfg, flow, Section.When, boxes);
        DrawSection(cfg, flow, Section.Do, boxes);
        DrawSection(cfg, flow, Section.Until, boxes);
        DrawSection(cfg, flow, Section.After, boxes);
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(new Vector4(0.45f, 0.75f, 0.68f, 0.9f));
        for (var i = 1; i < boxes.Count; i++)
        {
            draw.AddLine(boxes[i - 1].Bottom, boxes[i].Top, color, 2f);
            draw.AddTriangleFilled(boxes[i].Top, boxes[i].Top + new Vector2(-5, -7), boxes[i].Top + new Vector2(5, -7), color);
        }

        if (dryFor == flow.Id)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Dry run of Do. Nothing is sent.");
            foreach (var line in engine.Dry(flow)) ImGui.TextUnformatted(line);
        }

        ImGui.Spacing();
        if (ImGui.Button("Copy JSON")) ImGui.SetClipboardText(Share.One(flow));
        ImGui.SameLine();
        if (ImGui.Button("Duplicate") && Share.TryRead(Share.One(flow), out var copies, out _))
        {
            var copy = copies[0];
            copy.Id = Guid.NewGuid().ToString("N");
            foreach (var card in copy.Cards) card.Id = Guid.NewGuid().ToString("N");
            copy.Name += " copy";
            copy.Enabled = false;
            Add(cfg, copy);
        }
        ImGui.SameLine();
        if (ImGui.Button("Delete flow") && ImGui.GetIO().KeyShift)
        {
            plugin.Engine.Forget(flow);
            cfg.Flows.Remove(flow);
            selectedId = null;
            selectedCard = null;
            cfg.Save();
        }
        Tip("Hold Shift and click to delete.");
    }

    private void DrawSection(Configuration cfg, FlowItem flow, Section section, List<(Vector2 Top, Vector2 Bottom)> boxes)
    {
        ImGui.PushID((int)section);
        var cards = flow.In(section);
        var engine = plugin.Engine;
        var world = engine.Last;
        var condition = section is Section.When or Section.Until;

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = ImGui.GetStyle().FramePadding;
        ImGui.SetCursorScreenPos(start + pad);
        ImGui.BeginGroup();

        ImGui.TextUnformatted(section.ToString());
        if (condition && cards.Count > 1)
        {
            ImGui.SameLine();
            var join = section == Section.Until ? flow.UntilJoin : flow.WhenJoin;
            if (ImGui.SmallButton(join == Join.All ? "all of these" : "any of these"))
            {
                var flip = join == Join.All ? Join.Any : Join.All;
                if (section == Section.Until) flow.UntilJoin = flip;
                else flow.WhenJoin = flip;
                cfg.Save();
            }
        }
        if (condition && cards.Count > 0 && world.LoggedIn)
        {
            ImGui.SameLine();
            var whole = Eval.SectionMatches(flow, section, world);
            ImGui.TextColored(whole ? Green : new Vector4(0.6f, 0.6f, 0.6f, 1), whole ? "true now" : "false now");
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("+ add")) ImGui.OpenPopup("add");
        if (ImGui.BeginPopup("add"))
        {
            foreach (var desc in Catalog.For(section))
            {
                if (ImGui.Selectable($"{desc.Label}##{desc.Type}"))
                {
                    var card = Catalog.Make(section, desc.Type);
                    flow.Cards.Add(card);
                    selectedCard = card.Id;
                    cfg.Save();
                }
                ImGui.SameLine(190);
                ImGui.TextDisabled(desc.Hint);
            }
            ImGui.EndPopup();
        }

        if (cards.Count == 0)
        {
            ImGui.TextDisabled(section switch
            {
                Section.When => "Empty, so the flow stays off.",
                Section.Until => "Empty. The flow ends when When goes false.",
                Section.Do => "Empty. Nothing runs on the way in.",
                _ => "Empty. Revert still puts slotted values back.",
            });
        }

        var right = start.X + width - pad.X * 2;
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var label = $"{Catalog.Key(section, i)}  {card.Label}";
            var chipWidth = ChipWidth(label, condition);
            if (i > 0)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorScreenPos().X + chipWidth > right) ImGui.NewLine();
            }
            Chip(cfg, flow, card, label, condition, world, i, cards.Count);
        }

        ImGui.EndGroup();
        var max = ImGui.GetItemRectMax();
        var min = start;
        var bottom = new Vector2(start.X + width, max.Y + pad.Y);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRect(min, bottom, ImGui.GetColorU32(ImGuiCol.Border), 4f);
        ImGui.SetCursorScreenPos(new Vector2(start.X, bottom.Y));
        ImGui.Dummy(new Vector2(width, 18));
        boxes.Add((new Vector2(min.X + width * 0.5f, min.Y), new Vector2(min.X + width * 0.5f, bottom.Y)));
        ImGui.PopID();
    }

    private static float ChipWidth(string label, bool condition)
    {
        var style = ImGui.GetStyle();
        var small = ImGui.CalcTextSize("x").X + style.FramePadding.X * 2 + 2;
        var dot = condition ? ImGui.CalcTextSize("●").X + style.ItemSpacing.X : 0;
        var controls = small * (condition ? 4 : 3);
        return dot + ImGui.CalcTextSize(label).X + style.FramePadding.X * 2 + controls + style.ItemSpacing.X * 2;
    }

    private void Chip(Configuration cfg, FlowItem flow, Card card, string label, bool condition, World world, int index, int count)
    {
        ImGui.PushID(card.Id);
        ImGui.BeginGroup();
        if (condition)
        {
            var live = world.LoggedIn && Eval.CardMatches(card, world);
            if (live) ImGui.TextColored(Green, "●");
            else ImGui.TextDisabled("○");
            ImGui.SameLine(0, 4);
        }

        var error = plugin.Engine.Error(card.Id);
        var missing = Catalog.Missing(card) is not null;
        var selected = card.Id == selectedCard;
        var colors = 0;
        if (selected) { ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive)); colors++; }
        if (error is not null || missing) { ImGui.PushStyleColor(ImGuiCol.Text, error is not null ? Red : Amber); colors++; }
        if (ImGui.Button(label)) selectedCard = card.Id;
        if (colors > 0) ImGui.PopStyleColor(colors);
        if (ImGui.IsItemHovered())
        {
            if (error is not null) ImGui.SetTooltip(error);
            else if (missing) ImGui.SetTooltip("Needs a " + Catalog.Missing(card));
        }

        if (condition)
        {
            ImGui.SameLine(0, 2);
            if (ImGui.SmallButton(card.Not ? "!##not" : "=##not")) { card.Not = !card.Not; cfg.Save(); }
            Tip(card.Not ? "Negated. Click to match normally." : "Click to negate.");
        }
        ImGui.SameLine(0, 2);
        if (ImGui.SmallButton("<") && index > 0) Move(cfg, flow, card, -1);
        ImGui.SameLine(0, 2);
        if (ImGui.SmallButton(">") && index < count - 1) Move(cfg, flow, card, 1);
        ImGui.SameLine(0, 2);
        if (ImGui.SmallButton("x"))
        {
            flow.Cards.Remove(card);
            if (selectedCard == card.Id) selectedCard = null;
            cfg.Save();
        }
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private static void Move(Configuration cfg, FlowItem flow, Card card, int delta)
    {
        var section = flow.In(card.Section);
        var at = section.IndexOf(card);
        if (at + delta < 0 || at + delta >= section.Count) return;
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

    private static void Tip(string text)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    // ---------- import ----------

    private void DrawImport(Configuration cfg)
    {
        ImGui.TextUnformatted("Import");
        ImGui.RadioButton("Flow JSON", ref importKind, 0);
        ImGui.SameLine();
        ImGui.RadioButton("AndThen", ref importKind, 1);
        ImGui.SameLine();
        ImGui.RadioButton("StatusShift", ref importKind, 2);
        ImGui.TextDisabled(importKind switch
        {
            1 => "Paste AndThen rule JSON or an AT1. code. Rules come in off, with Keep.",
            2 => "Paste StatusShift rule JSON, its whole config, or an SS1. code. Rules come in off.",
            _ => "Paste one flow or a list. They come in off.",
        });
        ImGui.InputTextMultiline("##import", ref importText, 64000, new Vector2(-1, 160));
        if (ImGui.Button("Import##go"))
        {
            List<FlowItem> flows;
            string message;
            var ok = importKind switch
            {
                1 => Importers.TryAndThen(importText, out flows, out message),
                2 => Importers.TryStatusShift(importText, out flows, out message),
                _ => Share.TryRead(importText, out flows, out message),
            };
            if (ok)
            {
                foreach (var flow in flows)
                {
                    flow.Id = Guid.NewGuid().ToString("N");
                    foreach (var card in flow.Cards) card.Id = Guid.NewGuid().ToString("N");
                    flow.Enabled = false;
                    if (importKind == 0) flow.Mode = RunMode.Off;
                    cfg.Flows.Add(flow);
                }
                cfg.Save();
                importMsg = importKind == 0 ? $"Imported {flows.Count}. They are off." : message;
                importText = string.Empty;
                if (flows.Count > 0)
                {
                    selectedId = flows[^1].Id;
                    selectedCard = null;
                }
            }
            else importMsg = message;
        }
        ImGui.SameLine();
        if (ImGui.Button("Close")) showImport = false;
        if (importMsg.Length > 0) ImGui.TextWrapped(importMsg);
    }
}

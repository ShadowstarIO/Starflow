using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Starflow.Windows;

public sealed partial class MainWindow : Window
{
    private const string DragFlow = "FLOW_ITEM";
    private const string DragFolder = "FLOW_FOLDER";
    private static readonly Vector4 Green = Ui.Green;
    private static readonly Vector4 Teal = Ui.Teal;
    private static readonly Vector4 Amber = Ui.Amber;
    private static readonly Vector4 Red = Ui.Red;

    private readonly Plugin plugin;
    private string filter = string.Empty;
    private string newName = string.Empty;
    private string newFolder = string.Empty;
    private string? selectedId;
    private string? selectedCard;
    private string? dragId;
    private string? dragFolder;
    private string? menuFolder;
    private string renameText = string.Empty;
    private string subfolderText = string.Empty;
    private string? dryFor;
    private bool showImport;
    private int importKind;
    private string importText = string.Empty;
    private string importMsg = string.Empty;

    public MainWindow(Plugin plugin) : base($"Starflow v{Plugin.AppVersion}###FlowMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(820, 520),
            MaximumSize = new Vector2(3000, 2000),
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
        WindowName = $"Starflow v{Plugin.AppVersion}###FlowMain";
        var cfg = plugin.Configuration;
        Ui.Wrap = cfg.WrapColumns;
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { cfg.Enabled = enabled; cfg.Save(); }
        ImGui.SameLine();
        var waiting = plugin.Engine.Waiting;
        if (waiting > 0) ImGui.PushStyleColor(ImGuiCol.Text, Amber);
        if (ImGui.Button(waiting > 0 ? $"Prompts ({waiting})###ask" : "Prompts###ask")) plugin.OpenAsk();
        if (waiting > 0) ImGui.PopStyleColor();
        Ui.Tip("Flows in Prompt mode, and cards that want a yes, wait here.");
        ImGui.SameLine();
        if (ImGui.Button("Settings")) plugin.ToggleConfig();
        ImGui.SameLine();
        if (ImGui.Button("Help")) plugin.ToggleHelp();
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.IsPaused ? "Paused" : plugin.Engine.Last.Line());
        ImGui.Separator();

        // Three columns. Drag the borders between them to resize.
        var flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV;
        if (!ImGui.BeginTable("layout", 3, flags, ImGui.GetContentRegionAvail())) return;
        ImGui.TableSetupColumn("Flows", ImGuiTableColumnFlags.WidthFixed, 260);
        ImGui.TableSetupColumn("Flow", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Card", ImGuiTableColumnFlags.WidthFixed, 400);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        if (ImGui.BeginChild("list", ImGui.GetContentRegionAvail(), false))
        {
            if (Ui.Wrap) ImGui.PushTextWrapPos(0);
            DrawList(cfg);
            if (Ui.Wrap) ImGui.PopTextWrapPos();
        }
        ImGui.EndChild();
        ImGui.TableNextColumn();
        // Wrap on: text wraps at the column edge and rows flow onto new lines. Off: one line, sideways scroll bar.
        var wrap = cfg.WrapColumns;
        Ui.Wrap = wrap;
        var childFlags = wrap ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar;
        if (ImGui.BeginChild("graph", ImGui.GetContentRegionAvail(), false, childFlags))
        {
            if (wrap) ImGui.PushTextWrapPos(0);
            DrawGraph(cfg);
            if (wrap) ImGui.PopTextWrapPos();
        }
        ImGui.EndChild();
        ImGui.TableNextColumn();
        if (ImGui.BeginChild("card", ImGui.GetContentRegionAvail(), false, childFlags))
        {
            if (wrap) ImGui.PushTextWrapPos(0);
            DrawInspector(cfg);
            if (wrap) ImGui.PopTextWrapPos();
        }
        ImGui.EndChild();
        ImGui.EndTable();
    }

    // SameLine that wraps when the column is too narrow for the next item, if wrapping is on.
    private static void Next(float width) => Ui.Next(width);

    private static void NextButton(string label) => Ui.Next(Ui.ButtonWidth(label));

    private static float Field(string label, float wanted) => Ui.Wrap ? Ui.FieldWidth(label, wanted) : wanted;

    // ---------- list ----------

    private void DrawList(Configuration cfg)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Filter by name or notes", ref filter, 80);
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
            if (ImGui.Selectable("Housing: at a plot")) Add(cfg, Catalog.HousingTemplate());
            if (ImGui.Selectable("Schedule: weekday evenings")) Add(cfg, Catalog.ScheduleTemplate());
            if (ImGui.Selectable("Town: search comment in town")) Add(cfg, Catalog.TownTemplate());
            ImGui.EndPopup();
        }
        NextButton("Import");
        if (ImGui.Button("Import"))
        {
            showImport = true;
            selectedCard = null;
        }
        Next(110);
        var sort = cfg.ListSort;
        string[] sorts = ["By priority", "By name"];
        ImGui.SetNextItemWidth(-1);
        if (Ui.Combo("##sort", ref sort, sorts, sorts.Length)) { cfg.ListSort = sort; cfg.Save(); }

        ImGui.SetNextItemWidth(-64);
        ImGui.InputTextWithHint("##folder", "New folder (a/b nests)", ref newFolder, 80);
        ImGui.SameLine();
        if (ImGui.Button("Folder") && Folders.Clean(newFolder).Length > 0)
        {
            AddFolder(cfg, Folders.Clean(newFolder));
            newFolder = string.Empty;
        }

        ImGui.Separator();
        ImGui.BeginChild("tree", Vector2.Zero, false);
        ImGui.TextDisabled("Drag flows and folders onto a folder. Right-click a folder to rename it.");
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
            var on = cfg.Flows.Count(f => Folders.Under(f.FolderKey, child) && plugin.Engine.IsActive(f.Id));
            var label = on > 0 ? $"{Folders.Leaf(child)}  ({on} on / {count})" : $"{Folders.Leaf(child)}  ({count})";
            var open = ImGui.TreeNodeEx("folder:" + child, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth, label);
            if (ImGui.BeginDragDropSource())
            {
                dragFolder = child;
                dragId = null;
                ImGui.SetDragDropPayload(DragFolder, ReadOnlySpan<byte>.Empty, ImGuiCond.None);
                ImGui.TextUnformatted("Folder " + Folders.Leaf(child));
                ImGui.EndDragDropSource();
            }
            DropTarget(cfg, child);
            FolderMenu(cfg, child);
            if (!open) continue;
            DrawFolder(cfg, child, all);
            ImGui.TreePop();
        }

        var flows = cfg.Flows.Where(f => f.FolderKey.Equals(path, StringComparison.OrdinalIgnoreCase));
        flows = cfg.ListSort == 1
            ? flows.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            : flows.OrderByDescending(f => f.Priority).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var flow in flows.ToList()) FlowRow(flow);
    }

    private void FolderMenu(Configuration cfg, string folder)
    {
        if (!ImGui.BeginPopupContextItem("ctx:" + folder, ImGuiPopupFlags.MouseButtonRight)) return;
        if (menuFolder != folder)
        {
            menuFolder = folder;
            renameText = Folders.Leaf(folder);
            subfolderText = string.Empty;
        }
        ImGui.TextDisabled(folder);
        ImGui.SetNextItemWidth(180);
        ImGui.InputText("##rename", ref renameText, 60);
        ImGui.SameLine();
        if (ImGui.Button("Rename") && Folders.Clean(renameText).Length > 0 && !renameText.Contains('/'))
        {
            Repath(cfg, folder, Folders.Clean(Folders.Parent(folder) + "/" + renameText));
            ImGui.CloseCurrentPopup();
        }
        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##sub", "Subfolder name", ref subfolderText, 60);
        ImGui.SameLine();
        if (ImGui.Button("Add subfolder") && Folders.Clean(subfolderText).Length > 0)
        {
            AddFolder(cfg, Folders.Clean(folder + "/" + subfolderText));
            ImGui.CloseCurrentPopup();
        }
        if (Folders.Parent(folder).Length > 0 && ImGui.Selectable("Move to top level"))
            Repath(cfg, folder, Folders.Leaf(folder));
        ImGui.Separator();
        if (ImGui.Selectable("Remove folder (flows move up a level)"))
        {
            var parent = Folders.Parent(folder);
            foreach (var flow in cfg.Flows.Where(f => Folders.Under(f.FolderKey, folder)))
                flow.Folder = Folders.Clean(parent + "/" + flow.FolderKey[folder.Length..]);
            var kept = cfg.Folders.Where(f => Folders.Under(f, folder) && !Folders.Clean(f).Equals(folder, StringComparison.OrdinalIgnoreCase))
                .Select(f => Folders.Clean(parent + "/" + Folders.Clean(f)[folder.Length..])).ToList();
            cfg.Folders.RemoveAll(f => Folders.Under(f, folder));
            foreach (var f in kept) if (!cfg.Folders.Contains(f, StringComparer.OrdinalIgnoreCase)) cfg.Folders.Add(f);
            cfg.Save();
        }
        ImGui.EndPopup();
    }

    private static void AddFolder(Configuration cfg, string path)
    {
        if (!cfg.Folders.Contains(path, StringComparer.OrdinalIgnoreCase)) cfg.Folders.Add(path);
        cfg.Save();
    }

    // Moves or renames a folder, with everything under it.
    private static void Repath(Configuration cfg, string from, string to)
    {
        from = Folders.Clean(from);
        to = Folders.Clean(to);
        if (to.Length == 0 || from.Equals(to, StringComparison.OrdinalIgnoreCase)) return;
        if (Folders.Under(to, from)) return; // a folder cannot go inside itself
        string Swap(string path) => Folders.Clean(to + "/" + Folders.Clean(path)[from.Length..]);
        foreach (var flow in cfg.Flows.Where(f => Folders.Under(f.FolderKey, from))) flow.Folder = Swap(flow.FolderKey);
        var moved = cfg.Folders.Where(f => Folders.Under(f, from)).Select(Swap).ToList();
        cfg.Folders.RemoveAll(f => Folders.Under(f, from));
        moved.Add(to);
        foreach (var f in moved) if (!cfg.Folders.Contains(f, StringComparer.OrdinalIgnoreCase)) cfg.Folders.Add(f);
        cfg.Save();
    }

    private void FlowRow(FlowItem flow)
    {
        if (filter.Length > 0 && !flow.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            && !flow.Notes.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
        var engine = plugin.Engine;
        var on = engine.IsActive(flow.Id);
        var asking = engine.Pending.Contains(flow.Id);
        Ui.Dot(on || asking, on ? Green : asking ? Amber : Ui.Grey);
        ImGui.SameLine();

        var tail = !flow.Enabled ? "  · off"
            : on ? "  · on"
            : asking ? "  · prompt"
            : engine.IsHeld(flow.Id) ? "  · held"
            : string.Empty;
        var priority = flow.Priority != 0 ? $"  [{flow.Priority}]" : string.Empty;
        if (on) ImGui.PushStyleColor(ImGuiCol.Text, Teal);
        if (ImGui.Selectable($"{flow.Name}{priority}{tail}##{flow.Id}", flow.Id == selectedId))
        {
            selectedId = flow.Id;
            selectedCard = null;
            showImport = false;
        }
        if (on) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered() && !ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24);
            ImGui.TextUnformatted(flow.Name);
            ImGui.TextDisabled($"{ModeName(flow.Mode)} · {(flow.OnStop == StopMode.Revert ? "Revert" : "Keep")} · priority {flow.Priority}");
            if (!string.IsNullOrWhiteSpace(flow.Notes)) ImGui.TextUnformatted(flow.Notes);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        if (ImGui.BeginDragDropSource())
        {
            dragId = flow.Id;
            dragFolder = null;
            ImGui.SetDragDropPayload(DragFlow, ReadOnlySpan<byte>.Empty, ImGuiCond.None);
            ImGui.TextUnformatted(flow.Name);
            ImGui.EndDragDropSource();
        }
    }

    private static string ModeName(RunMode mode) => mode switch
    {
        RunMode.Off => "Off",
        RunMode.Ask => "Prompt",
        _ => "Auto",
    };

    private void DropTarget(Configuration cfg, string folder)
    {
        if (!ImGui.BeginDragDropTarget()) return;
        var flowPayload = ImGui.AcceptDragDropPayload(DragFlow, ImGuiDragDropFlags.None);
        if (!flowPayload.IsNull && dragId is not null)
        {
            var flow = cfg.Flows.Find(f => f.Id == dragId);
            if (flow is not null)
            {
                flow.Folder = folder;
                cfg.Save();
            }
            dragId = null;
        }
        var folderPayload = ImGui.AcceptDragDropPayload(DragFolder, ImGuiDragDropFlags.None);
        if (!folderPayload.IsNull && dragFolder is not null)
        {
            Repath(cfg, dragFolder, Folders.Clean(folder + "/" + Folders.Leaf(dragFolder)));
            dragFolder = null;
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
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Fix what the flow lists first: " + missing[0]);
        }
        Next(90);
        var mode = (int)flow.Mode;
        ImGui.SetNextItemWidth(90);
        string[] modes = ["Off", "Prompt", "Auto"];
        if (Ui.Combo("##mode", ref mode, modes, modes.Length)) { flow.Mode = (RunMode)mode; cfg.Save(); }
        Ui.Tip("Off does not start on its own. Prompt offers it in Prompts and waits for a yes. Auto starts on its own.");
        Next(90);
        var stop = (int)flow.OnStop;
        ImGui.SetNextItemWidth(90);
        string[] stops = ["Revert", "Keep"];
        if (Ui.Combo("##stop", ref stop, stops, stops.Length)) { flow.OnStop = (StopMode)stop; cfg.Save(); }
        Ui.Tip("Revert puts back settings, status, glamour, and Penumbra mods this flow changed, after After runs. Keep leaves them.");
        NextButton("Run once");
        if (ImGui.Button("Run once")) plugin.Engine.RunOnce(flow);
        Ui.Tip("One-shot. Runs Do now without turning the flow on. Slotted cards are skipped unless the flow is on.");
        NextButton("Stop");
        if (ImGui.Button("Stop")) plugin.Engine.Stop(flow, false);
        Ui.Tip("Runs After and reverts. The flow stays off until When goes false.");
        NextButton("Hide dry run");
        if (ImGui.Button(dryFor == flow.Id ? "Hide dry run" : "Dry run")) dryFor = dryFor == flow.Id ? null : flow.Id;
        Ui.Tip("Shows what each stage would send, in order, with timings. Nothing is sent.");

        var on = engine.IsActive(flow.Id);
        var state = on ? ("On", Green)
            : engine.Pending.Contains(flow.Id) ? ("Waiting for a yes in Prompts", Amber)
            : engine.IsHeld(flow.Id) ? ("Held until When goes false", Amber)
            : engine.IsRunning(flow.Id) ? ("Finishing", Teal)
            : ("Not running", Ui.Grey);
        Ui.Dot(on, state.Item2);
        ImGui.SameLine();
        ImGui.TextColored(state.Item2, state.Item1);
        foreach (var line in missing) ImGui.TextColored(Amber, line);

        var name = flow.Name;
        ImGui.SetNextItemWidth(Field("Name", 220));
        if (ImGui.InputText("Name", ref name, 80)) { flow.Name = name; cfg.Save(); }
        Next(90 + ImGui.CalcTextSize("Priority").X + 8);
        var priority = flow.Priority;
        ImGui.SetNextItemWidth(Field("Priority", 90));
        if (ImGui.InputInt("Priority", ref priority)) { flow.Priority = priority; cfg.Save(); }
        Ui.Tip("Higher priority wins a slot two flows both want.");

        var folder = flow.Folder;
        ImGui.SetNextItemWidth(Field("Folder", 220));
        if (ImGui.InputText("Folder", ref folder, 80)) { flow.Folder = folder; cfg.Save(); }
        Next(120 + ImGui.CalcTextSize("Hold").X + 8);
        var hold = flow.HoldSec;
        ImGui.SetNextItemWidth(Field("Hold", 120));
        if (ImGui.SliderFloat("Hold", ref hold, 0, 30, "%.1f s")) { flow.HoldSec = hold; cfg.Save(); }
        Ui.Tip("When must stay true this long before Do runs.");

        var quiet = flow.IgnoreQuiet ? 1 : 0;
        string[] quiets = ["Use quiet options", "Ignore quiet options"];
        ImGui.SetNextItemWidth(Field("Quiet", 220));
        if (Ui.Combo("Quiet", ref quiet, quiets, quiets.Length)) { flow.IgnoreQuiet = quiet == 1; cfg.Save(); }
        Ui.Tip("Quiet options in Settings hold flows in combat, cutscenes, duties, and zone lines. A duty flow should ignore them.");

        var notes = flow.Notes;
        if (ImGui.InputTextMultiline("##notes", ref notes, 600, new Vector2(-1, 44))) { flow.Notes = notes; cfg.Save(); }
        Ui.Tip("Notes. Shown when you hover the flow in the list.");

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
            ImGui.TextDisabled("Dry run. Each stage in the order it would happen. Nothing is sent.");
            foreach (var line in engine.Dry(flow))
            {
                if (line.StartsWith("Stage", StringComparison.Ordinal)) ImGui.TextColored(Teal, line);
                else ImGui.TextUnformatted(line);
            }
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
        Ui.Tip("Hold Shift and click to delete.");
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
            NextButton("any of these");
            var join = section == Section.Until ? flow.UntilJoin : flow.WhenJoin;
            if (ImGui.SmallButton(join == Join.All ? "all of these" : "any of these"))
            {
                var flip = join == Join.All ? Join.Any : Join.All;
                if (section == Section.Until) flow.UntilJoin = flip;
                else flow.WhenJoin = flip;
                cfg.Save();
            }
            Ui.Tip("Click to switch between all and any.");
        }
        if (condition && cards.Count > 0 && world.LoggedIn)
        {
            Next(ImGui.CalcTextSize("false now").X);
            var whole = Eval.SectionMatches(flow, section, world);
            ImGui.TextColored(whole ? Green : Ui.Grey, whole ? "true now" : "false now");
        }
        NextButton("+ add");
        if (ImGui.SmallButton("+ add")) ImGui.OpenPopup("add");
        if (ImGui.BeginPopup("add"))
        {
            ImGui.TextDisabled(condition ? "Conditions. The same list serves When and Until." : "Actions. The same list serves Do and After.");
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
            var chipWidth = ChipWidth(card.Label, condition, i, cards.Count);
            if (i > 0)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorScreenPos().X + chipWidth > right) ImGui.NewLine();
            }
            Chip(cfg, flow, card, condition, world, i, cards.Count);
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

    private static float ChipWidth(string label, bool condition, int index, int count)
    {
        var style = ImGui.GetStyle();
        var small = ImGui.CalcTextSize("x").X + style.FramePadding.X * 2 + 2;
        var dot = condition ? Ui.DotWidth + 4 : 0;
        var arrows = (index > 0 ? 1 : 0) + (index < count - 1 ? 1 : 0);
        return dot + ImGui.CalcTextSize(label).X + style.FramePadding.X * 2 + small * (arrows + 1) + style.ItemSpacing.X * 2;
    }

    // [● name][<][>][x]. The dot fills when the condition is true right now. "not" leads the name when negated.
    // Arrows show only where the card can move. Right-click for the same controls plus negate.
    private void Chip(Configuration cfg, FlowItem flow, Card card, bool condition, World world, int index, int count)
    {
        ImGui.PushID(card.Id);
        ImGui.BeginGroup();
        if (condition)
        {
            var live = world.LoggedIn && Eval.CardMatches(card, world);
            Ui.Dot(live, live ? Green : Ui.Grey, ImGui.GetFrameHeight());
            ImGui.SameLine(0, 4);
        }

        var error = plugin.Engine.Error(card.Id);
        var missing = Catalog.Missing(card) is not null;
        var selected = card.Id == selectedCard;
        var colors = 0;
        if (selected) { ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive)); colors++; }
        if (error is not null || missing) { ImGui.PushStyleColor(ImGuiCol.Text, error is not null ? Red : Amber); colors++; }
        if (ImGui.Button(card.Label)) selectedCard = card.Id;
        if (colors > 0) ImGui.PopStyleColor(colors);
        if (ImGui.IsItemHovered())
        {
            var key = Catalog.Key(card.Section, index);
            if (error is not null) ImGui.SetTooltip($"{key}: {error}");
            else if (missing) ImGui.SetTooltip($"{key} needs a {Catalog.Missing(card)}");
            else ImGui.SetTooltip(key + (condition ? " · right-click to negate, move, or delete" : " · right-click to move or delete"));
        }
        if (ImGui.BeginPopupContextItem("chip"))
        {
            if (condition && ImGui.Selectable(card.Not ? "Match normally" : "Negate (not)")) { card.Not = !card.Not; cfg.Save(); }
            if (index > 0 && ImGui.Selectable("Move left")) Move(cfg, flow, card, -1);
            if (index < count - 1 && ImGui.Selectable("Move right")) Move(cfg, flow, card, 1);
            if (ImGui.Selectable("Delete")) Remove(cfg, flow, card);
            ImGui.EndPopup();
        }

        if (index > 0)
        {
            ImGui.SameLine(0, 2);
            if (ImGui.SmallButton("<")) Move(cfg, flow, card, -1);
        }
        if (index < count - 1)
        {
            ImGui.SameLine(0, 2);
            if (ImGui.SmallButton(">")) Move(cfg, flow, card, 1);
        }
        ImGui.SameLine(0, 2);
        if (ImGui.SmallButton("x")) Remove(cfg, flow, card);
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void Remove(Configuration cfg, FlowItem flow, Card card)
    {
        flow.Cards.Remove(card);
        if (selectedCard == card.Id) selectedCard = null;
        cfg.Save();
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

    private static void Tip(string text) => Ui.Tip(text);

    // ---------- import ----------

    private void DrawImport(Configuration cfg)
    {
        ImGui.TextUnformatted("Import");
        ImGui.RadioButton("Starflow JSON", ref importKind, 0);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace Flow.Windows;

public sealed partial class MainWindow
{
    private static readonly string[] DayNames = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];
    private string optionFilter = string.Empty;
    private string modFilter = string.Empty;
    private string[]? statusNames;
    private string pickMessage = string.Empty;

    private void DrawInspector(Configuration cfg)
    {
        var flow = cfg.Flows.Find(f => f.Id == selectedId);
        if (showImport || flow is null)
        {
            DrawImport(cfg);
            return;
        }

        var card = flow.Cards.Find(c => c.Id == selectedCard);
        if (card is null)
        {
            ImGui.TextDisabled("Select a card to edit it.");
            ImGui.Separator();
            Why(flow);
            return;
        }

        ImGui.TextUnformatted(card.Label);
        ImGui.TextDisabled(Catalog.For(card.Section).FirstOrDefault(d => d.Type == card.Type)?.Hint ?? card.Type);
        var error = plugin.Engine.Error(card.Id);
        if (error is not null) ImGui.TextColored(Red, "Last run: " + error);
        var gap = Catalog.Missing(card);
        if (gap is not null) ImGui.TextColored(Amber, "Needs a " + gap);
        ImGui.Separator();

        if (CardType.IsCondition(card.Type))
        {
            var not = card.Not;
            if (ImGui.Checkbox("Not", ref not)) { card.Not = not; cfg.Save(); }
            Tip("Match when this is false.");
        }

        ImGui.PushItemWidth(-110);
        Edit(cfg, card);
        ImGui.PopItemWidth();

        if (CardType.IsSlotted(card.Type) && card.Section == Section.Do)
        {
            var keep = card.Keep;
            if (ImGui.Checkbox("Keep this one", ref keep)) { card.Keep = keep; cfg.Save(); }
            ImGui.TextDisabled(keep || flow.OnStop == StopMode.Keep
                ? "Left as it is when the flow stops."
                : "Put back when the flow stops, after After runs.");
        }
        if (CardType.IsSlotted(card.Type) && card.Section == Section.After)
            ImGui.TextDisabled("Written on the way out. Revert will not undo it.");

        ImGui.Separator();
        Why(flow);
    }

    private void Why(FlowItem flow)
    {
        ImGui.TextDisabled("Why");
        foreach (var line in plugin.Engine.Why(flow))
        {
            if (line.StartsWith("yes", StringComparison.Ordinal)) ImGui.TextColored(Green, line);
            else ImGui.TextWrapped(line);
        }
    }

    private void Edit(Configuration cfg, Card card)
    {
        var here = plugin.Engine.Last;
        switch (card.Type)
        {
            case CardType.State: Combo(cfg, "State", Catalog.States, v => card.Value = v, card.Value); break;
            case CardType.Place: Combo(cfg, "Place", Catalog.Places, v => card.Value = v, card.Value); break;
            case CardType.Duty:
                Combo(cfg, "Duty", Catalog.Duties, v => card.Value = v, card.Value);
                ImGui.TextDisabled("Or type a duty name below.");
                Text(cfg, "Name", v => card.Value = v, card.Value, "in");
                break;
            case CardType.Role: Combo(cfg, "Role", Catalog.Roles, v => card.Value = v, card.Value); break;
            case CardType.Target: Combo(cfg, "Target", Catalog.Targets, v => card.Value = v, card.Value); break;
            case CardType.Group:
                Combo(cfg, "Group", Catalog.Groups, v => card.Value = v, card.Value);
                ImGui.TextDisabled($"Solo 1, light 2 to 4, full 5 to 8. Alliance zones count as alliance. Now: {here.Group}");
                break;
            case CardType.Account:
                Combo(cfg, "World", Catalog.Accounts, v => card.Value = v, card.Value);
                ImGui.TextDisabled($"Now: {(here.Account.Length == 0 ? "?" : here.Account)}");
                break;
            case CardType.Job: Text(cfg, "Jobs", v => card.Value = v, card.Value, "WHM|SCH|AST"); Use(cfg, card, here.JobAbbr); break;
            case CardType.Zone: Text(cfg, "Zone", v => card.Value = v, card.Value, "Limsa | #129"); Use(cfg, card, "#" + here.TerritoryId, here.TerritoryName); break;
            case CardType.Region: Text(cfg, "Region", v => card.Value = v, card.Value, "La Noscea"); Use(cfg, card, here.RegionName); break;
            case CardType.World: Text(cfg, "World", v => card.Value = v, card.Value, "World name"); Use(cfg, card, here.WorldName); break;
            case CardType.DataCenter: Text(cfg, "Data center", v => card.Value = v, card.Value, "Name"); Use(cfg, card, here.DataCenterName); break;
            case CardType.Plugin:
                Text(cfg, "Plugin", v => card.Value = v, card.Value, "InternalName");
                Pick(cfg, "Installed", Bridges.PluginNames(), v => card.Value = v);
                ImGui.TextDisabled("True while that plugin is loaded.");
                break;
            case CardType.Weekday: Text(cfg, "Days", v => card.Value = v, card.Value, "sat,sun or weekday"); break;
            case CardType.Party:
            case CardType.Level:
                Text(cfg, "Compare", v => card.Value = v, card.Value, ">=4  <8  =1  1-4");
                break;
            case CardType.Nearby:
                Text(cfg, "Compare", v => card.Value = v, card.Value, ">=4, empty, few, crowded");
                ImGui.TextDisabled($"Now: {here.Nearby}");
                break;
            case CardType.NearbyName:
                Text(cfg, "Names", v => card.Value = v, card.Value, "Name | Other name");
                if (here.NearbyNames.Count > 0) Pick(cfg, "Nearby now", here.NearbyNames.Distinct().OrderBy(n => n).ToList(), v => card.Value = v);
                break;
            case CardType.Time:
                Text(cfg, "Range", v => card.Value = v, card.Value, "18:00-06:00 or et 17-22");
                ImGui.TextDisabled($"Local {here.LocalHour:00}:{here.LocalMinute:00}  ·  Eorzea {here.EorzeaHour:00}:{here.EorzeaMinute:00}");
                break;
            case CardType.Schedule: EditSchedule(cfg, card); break;
            case CardType.Weather:
                Text(cfg, "Weather", v => card.Value = v, card.Value, "Clear Skies | Rain | 7");
                Use(cfg, card, here.Weather);
                break;
            case CardType.Mount:
                Text(cfg, "Mount", v => card.Value = v, card.Value, "any, none, flying, or a name");
                if (here.Mount.Length > 0) Use(cfg, card, here.Mount);
                break;
            case CardType.Status:
                statusNames ??= StatusNames();
                Text(cfg, "Status", v => card.Value = v, card.Value, "Busy | Away from Keyboard");
                Pick(cfg, "Names", statusNames, v => card.Value = v);
                ImGui.TextDisabled($"Now: {here.OnlineStatus}");
                break;
            case CardType.Housing: EditHousing(cfg, card, here); break;

            case CardType.Command:
                Text(cfg, "Command", v => card.Value = v, card.Value, "/echo {zone}");
                Int(cfg, "Repeat", card.Repeat, v => card.Repeat = Math.Clamp(v, 1, 30));
                Int(cfg, "Gap ms", card.WaitMs, v => card.WaitMs = Math.Max(0, v));
                TokenHint(card.Value, here);
                break;
            case CardType.Wait: Int(cfg, "Milliseconds", card.WaitMs, v => card.WaitMs = Math.Max(0, v)); break;
            case CardType.Notice:
                Text(cfg, "Text", v => card.Value = v, card.Value, "Entered {zone}");
                TokenHint(card.Value, here);
                break;
            case CardType.SearchComment:
                Text(cfg, "Comment", v => card.Value = v, card.Value, "In {zone} on {world}");
                TokenHint(card.Value, here);
                var filled = Tokens.Fill(card.Value, here);
                if (filled.Length > ChatSender.CommentMax) ImGui.TextColored(Amber, $"Cut to {ChatSender.CommentMax} characters.");
                ImGui.TextDisabled("Edge action. It is not put back unless After sets another comment.");
                break;
            case CardType.Online:
                Combo(cfg, "Status", Catalog.StatusTokens, v => card.Value = v, card.Value);
                ImGui.TextDisabled("Put back on stop. Statuses with no command, such as mentor, cannot be put back.");
                break;
            case CardType.Collection:
                Text(cfg, "Collection", v => card.Value = v, card.Value, "Collection name");
                Flag(cfg, card, "Enable", "Enables the collection.", "Disables the collection.");
                ImGui.TextDisabled("Edge action. Add the opposite card to After to undo it.");
                break;
            case CardType.PluginSet:
                Text(cfg, "Plugin", v => card.Value = v, card.Value, "Plugin name");
                Pick(cfg, "Installed", Bridges.PluginNames(), v => card.Value = v);
                Flag(cfg, card, "Enable##plugin", "Enables the plugin.", "Disables the plugin.");
                var keep = card.Permanent;
                if (ImGui.Checkbox("Remember", ref keep)) { card.Permanent = keep; cfg.Save(); }
                ImGui.TextDisabled(keep ? "Saved in the plugin's collection." : "Temporary. Resets when the game restarts.");
                ImGui.TextWrapped("Dalamud refuses a plugin that sits in more than one collection. Toggle the collection instead.");
                break;
            case CardType.Setting: EditSetting(cfg, card); break;
            case CardType.Glamour:
                Text(cfg, "Design", v => card.Value = v, card.Value, "Design name or id");
                Pick(cfg, "Designs", Bridges.GlamourNames(), v => card.Value = v);
                ImGui.TextDisabled("Applies to you. Revert on stop asks Glamourer to revert your state.");
                break;
            case CardType.GlamourRevert:
                ImGui.TextDisabled("Asks Glamourer to revert your appearance.");
                break;
            case CardType.PenumbraMod: EditMod(cfg, card); break;
            case CardType.PenumbraCollection:
                Text(cfg, "Collection", v => card.Value = v, card.Value, "collection name");
                Pick(cfg, "Collections", Bridges.PenumbraCollections(), v => card.Value = v);
                ImGui.TextDisabled("Edge action. Assigns your individual collection.");
                break;
            case CardType.PenumbraReset:
                Text(cfg, "Directory", v => card.Option = v, card.Option, "blank resets every temp change Flow made");
                Text(cfg, "Name", v => card.Value = v, card.Value, "mod name");
                break;
            case CardType.Logout:
            case CardType.CloseGame:
                Flag(cfg, card, "Ask first", "Flow asks in the Ask window before it sends this.", "Sends straight away.");
                ImGui.TextDisabled("The game may show its own dialog too.");
                break;
        }
    }

    private void EditSchedule(Configuration cfg, Card card)
    {
        Match.TryParseSchedule(card.Value, out var s);
        var mode = Math.Max(0, Array.IndexOf(Catalog.ScheduleModes, s.Mode));
        var changed = ImGui.Combo("Repeat", ref mode, Catalog.ScheduleModes, Catalog.ScheduleModes.Length);
        var kind = Catalog.ScheduleModes[mode];
        var days = s.Days;
        var from = s.From == default ? DateTime.Today.ToString("yyyy-MM-dd") : s.From.ToString("yyyy-MM-dd");
        var to = s.To == default ? from : s.To.ToString("yyyy-MM-dd");
        var range = s.HasTime ? $"{s.Start / 60:00}:{s.Start % 60:00}-{s.End / 60:00}:{s.End % 60:00}" : string.Empty;

        if (kind == "weekly")
        {
            var set = days.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()[..Math.Min(3, d.Trim().Length)]).ToHashSet();
            for (var i = 0; i < DayNames.Length; i++)
            {
                if (i > 0) ImGui.SameLine();
                var on = set.Contains(DayNames[i]);
                if (ImGui.Checkbox(DayNames[i] + "##day", ref on))
                {
                    if (on) set.Add(DayNames[i]); else set.Remove(DayNames[i]);
                    changed = true;
                }
            }
            days = string.Join(',', DayNames.Where(set.Contains));
            if (days.Length == 0) days = "sat,sun";
        }
        if (kind == "once")
        {
            changed |= ImGui.InputText("From date", ref from, 10);
            changed |= ImGui.InputText("To date", ref to, 10);
        }
        if (kind != "always")
        {
            changed |= ImGui.InputTextWithHint("Hours", "blank is all day, or 20:00-23:00", ref range, 16);
            ImGui.TextDisabled("Local time. A window past midnight belongs to the day it starts.");
        }

        if (changed)
        {
            var time = range.Trim().Length > 0 ? " " + range.Trim() : string.Empty;
            card.Value = kind switch
            {
                "daily" => "daily" + time,
                "weekly" => $"weekly {days}{time}",
                "once" => $"once {from.Trim()}{(to.Trim().Length > 0 && to.Trim() != from.Trim() ? ".." + to.Trim() : string.Empty)}{time}",
                _ => "always",
            };
            cfg.Save();
        }
        Text(cfg, "Text", v => card.Value = v, card.Value, "weekly sat,sun 18:00-02:00");
        var ok = Match.TryParseSchedule(card.Value, out _);
        if (!ok) ImGui.TextColored(Red, "Not a schedule Flow can read.");
        else ImGui.TextDisabled(Match.Schedule(DateTime.Now, card.Value) ? "Inside the window now." : "Outside the window now.");
    }

    private void EditHousing(Configuration cfg, Card card, World here)
    {
        AddressSpec.TryParse(card.Value, out var kind, out var district, out var ward, out var plot, out var room, out var sub);
        var changed = false;
        var k = Math.Max(0, Array.IndexOf(AddressSpec.Kinds, kind));
        changed |= ImGui.Combo("Kind", ref k, AddressSpec.Kinds, AddressSpec.Kinds.Length);
        string[] districts = ["(pick one)", .. AddressSpec.Districts];
        var d = Math.Max(0, Array.IndexOf(districts, district));
        changed |= ImGui.Combo("District", ref d, districts, districts.Length);
        changed |= ImGui.InputInt("Ward", ref ward);
        changed |= ImGui.Checkbox("Subdivision", ref sub);
        var kindName = AddressSpec.Kinds[k];
        if (kindName is "House" or "Chamber") changed |= ImGui.InputInt("Plot", ref plot);
        if (kindName is "Apartment" or "Chamber") changed |= ImGui.InputInt(kindName == "Apartment" ? "Apartment" : "Room", ref room);
        ImGui.TextDisabled("0 is any. A subdivision is the ward's second half, not a plot.");
        if (changed)
        {
            card.Value = AddressSpec.Encode(kindName, d == 0 ? string.Empty : districts[d], Math.Clamp(ward, 0, 30), Math.Clamp(plot, 0, 60), Math.Max(0, room), sub);
            cfg.Save();
        }

        ImGui.TextDisabled("Here: " + here.Housing.Line());
        if (here.Housing.InWard && ImGui.Button("Use where I stand"))
        {
            var h = here.Housing;
            var name = h.Kind switch
            {
                Residence.House => "House",
                Residence.Apartment => "Apartment",
                Residence.Chamber => "Chamber",
                _ => "Any",
            };
            card.Value = AddressSpec.Encode(name, h.District, h.Ward, h.Plot, h.Room, h.Subdivision);
            cfg.Save();
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

        ImGui.InputTextWithHint("Find", "type part of a name", ref optionFilter, 40);
        var common = Catalog.Settings.Where(s => s.Section == card.Option || (card.Option.Length == 0 && s.Section == "System")).Select(s => s.Name);
        var all = Actions.OptionNames(card.Option);
        var names = common.Concat(all.Except(common))
            .Where(n => optionFilter.Length == 0 || n.Contains(optionFilter, StringComparison.OrdinalIgnoreCase))
            .Take(400)
            .ToArray();
        var pick = Math.Max(0, Array.IndexOf(names, card.Value));
        if (names.Length > 0 && ImGui.Combo("Option", ref pick, names, names.Length))
        {
            card.Value = names[pick];
            cfg.Save();
        }
        Text(cfg, "Name", v => card.Value = v, card.Value, "Config option");
        Text(cfg, "Value", v => card.Extra = v, card.Extra, "0");

        if (card.Value.Length > 0 && !Actions.RealOption(card.Option, card.Value))
            ImGui.TextColored(Red, $"{card.Value} is not a real option.");
        else if (card.Value.Length > 0)
            ImGui.TextDisabled("Now: " + Actions.ReadOption(card.Option, card.Value));
        var hint = Catalog.Settings.FirstOrDefault(s => s.Name == card.Value);
        if (hint is not null) ImGui.TextWrapped(hint.Hint);
        if (Actions.Resolution(card.Value) && hint is null)
            ImGui.TextWrapped("Config only. Flow does not resize the live window.");
    }

    private void EditMod(Configuration cfg, Card card)
    {
        ImGui.InputTextWithHint("Find mod", "type part of a name", ref modFilter, 60);
        var mods = Bridges.Mods()
            .Where(m => modFilter.Length == 0 || m.Name.Contains(modFilter, StringComparison.OrdinalIgnoreCase) || m.Dir.Contains(modFilter, StringComparison.OrdinalIgnoreCase))
            .Take(300)
            .ToList();
        var labels = mods.Select(m => m.Name == m.Dir ? m.Name : $"{m.Name}  ·  {m.Dir}").ToArray();
        var at = mods.FindIndex(m => m.Dir == card.Option);
        var pick = Math.Max(0, at);
        if (labels.Length > 0 && ImGui.Combo("Mod", ref pick, labels, labels.Length))
        {
            card.Option = mods[pick].Dir;
            card.Value = mods[pick].Name;
            card.Extra = string.Empty;
            cfg.Save();
        }
        if (labels.Length == 0) ImGui.TextDisabled("Penumbra lists no mods, or is not loaded.");
        Text(cfg, "Directory", v => card.Option = v, card.Option, "mod directory");

        var inherit = card.Inherit;
        if (ImGui.Checkbox("Inherit", ref inherit)) { card.Inherit = inherit; cfg.Save(); }
        Tip("Use the parent collection's settings for this mod.");
        if (!card.Inherit)
        {
            ImGui.SameLine();
            var on = card.Flag;
            if (ImGui.Checkbox("Enabled", ref on)) { card.Flag = on; cfg.Save(); }
            Int(cfg, "Priority", card.Number, v => card.Number = v);
        }
        var permanent = card.Permanent;
        if (ImGui.Checkbox("Permanent", ref permanent)) { card.Permanent = permanent; cfg.Save(); }
        ImGui.TextDisabled(permanent ? "Writes your collection. Revert writes the old values back." : "Temporary. Revert removes Flow's temporary setting.");

        if (ImGui.Button("Read current") && card.Option.Length > 0)
        {
            if (Bridges.TryReadMod(card.Option, card.Value, out var now, out var error))
            {
                card.Flag = now.Enabled;
                card.Number = now.Priority;
                card.Inherit = now.Inherit;
                card.Extra = ModOptions.Format(now.Settings);
                pickMessage = "Copied the mod's current settings.";
                cfg.Save();
            }
            else pickMessage = error;
        }
        if (pickMessage.Length > 0) ImGui.TextWrapped(pickMessage);

        if (card.Inherit || card.Option.Length == 0) return;
        var groups = Bridges.ModGroups(card.Option, card.Value);
        if (groups.Count == 0) return;
        ImGui.Separator();
        ImGui.TextDisabled("Options. Unticked groups keep what the mod has.");
        var chosen = ModOptions.Parse(card.Extra);
        var changed = false;
        foreach (var (group, (options, type)) in groups)
        {
            ImGui.PushID(group);
            var use = chosen.ContainsKey(group);
            if (ImGui.Checkbox(group, ref use))
            {
                if (use) chosen[group] = options.Length > 0 && type == 0 ? [options[0]] : [];
                else chosen.Remove(group);
                changed = true;
            }
            if (use && chosen.TryGetValue(group, out var picked))
            {
                ImGui.Indent();
                if (type == 0)
                {
                    var index = Math.Max(0, Array.IndexOf(options, picked.FirstOrDefault()));
                    if (options.Length > 0 && ImGui.Combo("##single", ref index, options, options.Length))
                    {
                        chosen[group] = [options[index]];
                        changed = true;
                    }
                }
                else
                {
                    foreach (var option in options)
                    {
                        var on = picked.Contains(option);
                        if (!ImGui.Checkbox(option, ref on)) continue;
                        if (on) picked.Add(option); else picked.Remove(option);
                        changed = true;
                    }
                }
                ImGui.Unindent();
            }
            ImGui.PopID();
        }
        if (changed)
        {
            card.Extra = ModOptions.Format(chosen);
            cfg.Save();
        }
    }

    private static string[] StatusNames()
    {
        try
        {
            return Plugin.DataManager.GetExcelSheet<OnlineStatus>()
                .Select(r => r.Name.ToString())
                .Where(n => n.Length > 0)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void TokenHint(string text, World here)
    {
        ImGui.TextDisabled("Tokens: {" + string.Join("} {", Tokens.Names) + "}");
        if ((text ?? string.Empty).Contains('{')) ImGui.TextWrapped("Now: " + Tokens.Fill(text ?? string.Empty, here));
    }

    private static void Use(Configuration cfg, Card card, string value, string? shown = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (ImGui.SmallButton($"Use {shown ?? value}"))
        {
            card.Value = value;
            cfg.Save();
        }
    }

    private static void Pick(Configuration cfg, string label, IReadOnlyList<string> items, Action<string> set)
    {
        if (items.Count == 0) return;
        if (ImGui.Button(label + "...")) ImGui.OpenPopup("pick:" + label);
        if (!ImGui.BeginPopup("pick:" + label)) return;
        ImGui.BeginChild("scroll", new Vector2(320, Math.Min(360, items.Count * ImGui.GetTextLineHeightWithSpacing() + 8)), false);
        foreach (var item in items)
        {
            if (!ImGui.Selectable(item)) continue;
            set(item);
            cfg.Save();
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndChild();
        ImGui.EndPopup();
    }

    private static void Flag(Configuration cfg, Card card, string label, string on, string off)
    {
        var value = card.Flag;
        if (ImGui.Checkbox(label, ref value)) { card.Flag = value; cfg.Save(); }
        ImGui.TextDisabled(value ? on : off);
    }

    private static void Text(Configuration cfg, string label, Action<string> set, string current, string hint)
    {
        var value = current ?? string.Empty;
        if (!ImGui.InputTextWithHint(label, hint, ref value, 240)) return;
        set(value);
        cfg.Save();
    }

    private static void Int(Configuration cfg, string label, int current, Action<int> set)
    {
        var value = current;
        if (!ImGui.InputInt(label, ref value)) return;
        set(value);
        cfg.Save();
    }

    private static void Combo(Configuration cfg, string label, string[] items, Action<string> set, string current)
    {
        var index = Math.Max(0, Array.FindIndex(items, i => i.Equals(current, StringComparison.OrdinalIgnoreCase)));
        if (!ImGui.Combo(label, ref index, items, items.Length)) return;
        set(items[index]);
        cfg.Save();
    }
}

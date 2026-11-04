using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Starflow.Windows;

public sealed class HelpWindow : Window
{
    private readonly Plugin plugin;
    private string optionFilter = string.Empty;
    private int optionSection;
    private string? openTab;

    // Opens the window on one tab: "options", "reactions", "tokens".
    public void Show(string tab)
    {
        openTab = tab;
        IsOpen = true;
    }

    private bool Tab(string label, string key)
    {
        var flags = openTab == key ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var open = ImGui.BeginTabItem(label, flags);
        if (open && openTab == key) openTab = null;
        return open;
    }

    public HelpWindow(Plugin plugin) : base("Starflow help###FlowHelp")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 360),
            MaximumSize = new Vector2(1200, 1400),
        };
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("help")) return;
        if (ImGui.BeginTabItem("Basics"))
        {
            Para("A flow is four sections, read top to bottom.");
            Bullet("When", "Conditions. All of them, or any of them. Right-click a card to negate it. The dot fills when that card is true right now.");
            Bullet("Do", "Runs once, in order, when When becomes true.");
            Bullet("Until", "Optional. Ends the flow early while When is still true. After that it waits for When to go false once before it can start again. Leave it empty and the flow simply ends when When goes false.");
            Bullet("After", "Runs once, in order, when the flow ends for any reason.");
            Para("Revert puts back what slotted Do cards replaced (game settings, online status, Glamourer, Penumbra mods), after After runs. Keep leaves them.");
            Para("Modes: Off does not start on its own. Prompt puts the flow in Prompts and waits for a yes. Auto starts on its own.");
            Para("Dry run lists each stage, Do, After, and Revert, with timings. Why says in one line why a flow is on or off, then shows every condition.");
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Matching"))
        {
            Para("Text fields such as zone, region, duty, world, weather, mount, nearby name, target name, status, and job match in two ways.");
            Bullet("Words", "Split on |. Any word that appears in the value matches, without case. Limsa | Gridania matches either city.");
            Bullet("Regex", "Write /pattern/ or re:pattern. Matched without case. The | inside a pattern is the regex 'or'.");
            ImGui.Separator();
            Para("Regex quick guide:");
            Code("^Lim", "starts with Lim");
            Code("Skies$", "ends with Skies");
            Code("^(Mist|The Goblet)$", "exactly Mist or The Goblet");
            Code("Crystarium|Eulmore", "either one, anywhere in the name");
            Code("^The ", "names that start with 'The '");
            Code("\\d+", "contains a number");
            Code("^(?!Limsa)", "anything that does not start with Limsa");
            Para("A bad pattern shows in red under the field and does not match anything.");
            ImGui.Separator();
            Para("Numbers compare with >=4, <8, =1, !=2, or a range 1-4.");
            Para("Clock ranges are local, or Eorzea with an et prefix: et 18:00-06:00. A range past midnight wraps.");
            ImGui.EndTabItem();
        }
        if (Tab("Reactions", "reactions"))
        {
            Para("Some When cards read small things from play: HP, MP, GP, and CP; effects on you, your target, or your party; enemies targeting you; damage you just took; party members who are down; and chat messages.");
            Para("Starflow reacts to them by changing setup around you: settings, status, looks, mods, notices, webhooks, travel. It is meant for reacting, not for playing.");
            Ui.Wrapped(Ui.Amber, "A flow that uses any of these cards holds off on actions and macros (/ac, /action, /pvpaction, /blueaction, /petaction, /gaction, /macro) and says why.");
            ImGui.Separator();
            Bullet("HP, MP, GP, CP", "Compare a number or a percent: <50%, >=8000, =0. For you, your target, any party member, or every party member.");
            Bullet("Effect", "A buff or debuff by name, a | b, or /regex/. On you, your target, or anyone in the party.");
            Bullet("Enmity", "How many enemies are targeting you right now: >=1 means something is on you, =0 means nothing is.");
            Bullet("Took damage", "Your HP dropped within the last few seconds. Other players can only hurt you in PvP, so 'from a player' means PvP and 'from an NPC' means everywhere else.");
            Bullet("Party down", "How many party members are at 0 HP.");
            Bullet("Chat message", "Someone said something in the channels you pick, from a sender you name, matching a word or /regex/. It stays true for a few seconds after the message.");
            ImGui.Separator();
            Ui.Wrapped(Ui.Amber, "A chat card lets other people trigger your flows. Anyone who can type in those channels can make your character emote, speak, change looks, or travel through whatever that flow does. Keep the senders and channels narrow, prefer Prompt mode, and only use it with people you trust. How you use it is up to you.");
            ImGui.EndTabItem();
        }
        if (Tab("Game options", "options"))
        {
            Para("Every System and UI option a person would set, with what it does. Game bookkeeping such as session and login records is left out. Game setting cards can write the ones marked writable. Hover a name to copy it with a click.");
            string[] sections = ["System", "UI"];
            ImGui.SetNextItemWidth(110);
            Ui.Combo("##section", ref optionSection, sections, sections.Length);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##find", "find by name or what it does: shadow, bubble, volume", ref optionFilter, 60);
            var section = optionSection == 1 ? "Ui" : "System";
            if (ImGui.BeginTable("options", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY, new Vector2(0, -1)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthFixed, 220);
                ImGui.TableSetupColumn("What it does", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableSetupColumn("Now", ImGuiTableColumnFlags.WidthFixed, 70);
                ImGui.TableHeadersRow();
                var rows = OptionGuide.All(Actions.OptionNames(section))
                    .Where(r => optionFilter.Length == 0
                        || r.Name.Contains(optionFilter, StringComparison.OrdinalIgnoreCase)
                        || r.Entry.Text.Contains(optionFilter, StringComparison.OrdinalIgnoreCase)
                        || r.Entry.Group.Contains(optionFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.Entry.Group, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase);
                var group = string.Empty;
                foreach (var (name, entry) in rows)
                {
                    if (entry.Group != group)
                    {
                        group = entry.Group;
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TextColored(Ui.Teal, group);
                    }
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name);
                    if (ImGui.IsItemClicked()) ImGui.SetClipboardText(name);
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(entry.Text);
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(plugin.Engine.Last.LoggedIn ? Actions.ReadOption(section, name) : "-");
                }
                ImGui.EndTable();
            }
            ImGui.EndTabItem();
        }
        if (Tab("Tokens", "tokens"))
        {
            Para("Commands, chat notices, search comments, and webhooks fill these when they run. Unknown tokens stay as written.");
            var world = plugin.Engine.Last;
            if (ImGui.BeginTable("tokens", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Token", ImGuiTableColumnFlags.WidthFixed, 110);
                ImGui.TableSetupColumn("What", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableSetupColumn("Now", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableHeadersRow();
                foreach (var token in Tokens.All)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("{" + token.Name + "}");
                    if (ImGui.IsItemClicked()) ImGui.SetClipboardText("{" + token.Name + "}");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(token.Hint);
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(world.LoggedIn ? token.Read(world) : "-");
                }
                ImGui.EndTable();
            }
            ImGui.TextDisabled("Click a token to copy it.");
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Commands"))
        {
            Code("/starflow", "open this window (/flow and /fl work too)");
            Code("/fl Name", "run that flow's Do once (one-shot)");
            Code("/fl stop Name", "run After, then revert");
            Code("/fl why Name", "why it is on or off");
            Code("/fl dry Name", "every stage, without sending");
            Code("/fl now", "which flows are on");
            Code("/fl pause [seconds]", "pause, or pause for a while");
            Code("/fl resume", "resume");
            Code("/fl prompts", "open Prompts");
            Code("/fl config", "settings");
            Code("/fl help", "this window");
            ImGui.Separator();
            Para("Anything the game or a plugin does with a slash command can be a Command card. Browse... on that card lists every game command, emote, and plugin command. A plugin toggle with no command and no IPC is out of reach; Starflow leaves other plugins' config files alone.");
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private static void Para(string text)
    {
        ImGui.TextWrapped(text);
        ImGui.Spacing();
    }

    private static void Bullet(string head, string text)
    {
        ImGui.TextColored(Ui.Teal, head);
        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    private static void Code(string code, string text)
    {
        ImGui.TextColored(Ui.Teal, code);
        ImGui.SameLine(200);
        ImGui.TextWrapped(text);
    }
}

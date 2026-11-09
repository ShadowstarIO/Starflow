using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Starflow.Windows;

namespace Starflow;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IBuddyList BuddyList { get; private set; } = null!;

    public static readonly string AppVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0.0.1.0";
    private const string CommandName = "/starflow";
    private static readonly string[] CommandAliases = ["/flow", "/fl"];

    internal static Plugin Instance { get; private set; } = null!;
    public Configuration Configuration { get; }
    public readonly WindowSystem WindowSystem = new("Starflow");
    internal Engine Engine { get; }

    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private readonly AskWindow askWindow;
    private readonly HelpWindow helpWindow;
    private bool paused;
    private TimeSpan? pauseUntil;
    private int askSeen;

    public Plugin()
    {
        Instance = this;
        Configuration = Configuration.Load();
        if (Configuration.PollSec <= 0) Configuration.PollSec = 0.4f;
        Engine = new Engine(Configuration);
        Eval.PluginLoaded = Bridges.PluginLoaded;

        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);
        askWindow = new AskWindow(this);
        helpWindow = new HelpWindow(this);
        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(askWindow);
        WindowSystem.AddWindow(helpWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = "Starflow. /fl help" });
        foreach (var alias in CommandAliases)
        {
            var help = alias == "/flow" ? "Starflow. /flow help" : "Short for /starflow.";
            try { CommandManager.AddHandler(alias, new CommandInfo(OnCommand) { HelpMessage = help }); }
            catch (Exception ex) { Log.Verbose(ex, "Could not register {Alias}", alias); }
        }

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMain;
        Framework.Update += OnFramework;
        ClientState.TerritoryChanged += OnTerritory;
        ClientState.Logout += OnLogout;
        ChatWatch.Start();

        if (Configuration.OpenUiOnLoad) mainWindow.IsOpen = true;
    }

    public void Dispose()
    {
        Framework.Update -= OnFramework;
        ClientState.TerritoryChanged -= OnTerritory;
        ClientState.Logout -= OnLogout;
        ChatWatch.Stop();
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMain;
        WindowSystem.RemoveAllWindows();
        try { CommandManager.RemoveHandler(CommandName); } catch { /* gone */ }
        foreach (var alias in CommandAliases)
        {
            try { CommandManager.RemoveHandler(alias); } catch { /* gone */ }
        }
    }

    public bool IsPaused => paused;
    public void ToggleConfig() => configWindow.Toggle();
    public void ToggleMain() => mainWindow.Toggle();
    public void ToggleHelp() => helpWindow.Toggle();
    public void OpenHelp(string tab) => helpWindow.Show(tab);
    public void OpenAsk() => askWindow.IsOpen = true;
    public void OpenFlow(string id) { mainWindow.OpenFlow(id); }

    public static void Notify(string text)
    {
        if (Instance is { Configuration.NotifyInChat: false }) return;
        try { Chat.Print("[Starflow] " + text); }
        catch { /* chat not ready */ }
    }

    public FlowItem? Find(string name) =>
        Configuration.Flows.FirstOrDefault(f => f.Token.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    private void OnFramework(IFramework _)
    {
        Engine.Advance(Framework.UpdateDelta);
        if (paused && pauseUntil is TimeSpan until && Engine.Clock >= until)
        {
            paused = false;
            pauseUntil = null;
            Notify("Pause ended.");
        }

        if (!ClientState.IsLoggedIn || ObjectTable.LocalPlayer is null)
        {
            Engine.Reset();
            return;
        }

        if (paused) return;
        Engine.Tick(false);
        // Open only for a new ask. An ask that survives a zone line does not reopen the window.
        if (Engine.AskSerial != askSeen)
        {
            askSeen = Engine.AskSerial;
            if (Engine.Waiting > 0) askWindow.IsOpen = true;
        }
    }

    private void OnTerritory(uint _)
    {
        if (!paused) Engine.Tick(true);
    }

    private void OnLogout(int type, int code) => Engine.Reset();

    private void OnCommand(string command, string args)
    {
        var text = (args ?? string.Empty).Trim();
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var key = parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
        switch (key)
        {
            case "help":
                Notify("/flow — window");
                Notify("/fl Name — run Do once (one-shot, skips slotted cards)");
                Notify("/fl stop Name — After, then revert");
                Notify("/fl why Name — conditions");
                Notify("/fl dry Name — Do list");
                Notify("/fl now | pause | resume | prompts | config");
                helpWindow.IsOpen = true;
                break;
            case "config": ToggleConfig(); break;
            case "ask" or "prompts" or "prompt": OpenAsk(); break;
            case "now":
                Engine.Tick(true);
                var names = Engine.ActiveNames().ToList();
                Notify(names.Count == 0 ? "No flow is on." : "On: " + string.Join(", ", names));
                break;
            case "pause":
                paused = true;
                if (parts.Length > 1 && int.TryParse(parts[1], out var secs) && secs > 0)
                {
                    pauseUntil = Engine.Clock + TimeSpan.FromSeconds(secs);
                    Notify($"Paused {secs}s.");
                }
                else
                {
                    pauseUntil = null;
                    Notify("Paused.");
                }
                break;
            case "resume":
                paused = false;
                pauseUntil = null;
                Notify("Resumed.");
                Engine.Tick(true);
                break;
            case "stop":
            {
                var name = parts.Length > 1 ? parts[1] : string.Empty;
                var flow = string.IsNullOrWhiteSpace(name) ? null : Find(name);
                if (flow is null) Notify("Name a flow: /fl stop Name");
                else Engine.Stop(flow, false);
                break;
            }
            case "why":
            {
                var name = parts.Length > 1 ? parts[1] : string.Empty;
                var flow = string.IsNullOrWhiteSpace(name) ? null : Find(name);
                if (flow is null) Notify("Name a flow: /fl why Name");
                else foreach (var line in Engine.Why(flow)) Notify(line);
                break;
            }
            case "dry":
            {
                var name = parts.Length > 1 ? parts[1] : string.Empty;
                var flow = string.IsNullOrWhiteSpace(name) ? null : Find(name);
                if (flow is null) Notify("Name a flow: /fl dry Name");
                else foreach (var line in Engine.Dry(flow)) Notify(line);
                break;
            }
            case "":
                ToggleMain();
                break;
            default:
            {
                var flow = Find(text);
                if (flow is null)
                {
                    Notify($"No flow named {text}");
                    ToggleMain();
                    break;
                }
                Engine.RunOnce(flow);
                Notify($"Ran {flow.Name} once. One-shot: nothing latches, and slotted cards are skipped unless the flow is on.");
                break;
            }
        }
    }
}

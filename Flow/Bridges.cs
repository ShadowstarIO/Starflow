using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin.Ipc.Exceptions;

namespace Flow;

internal sealed class ModState
{
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public bool Inherit { get; set; }
    public Dictionary<string, List<string>> Settings { get; set; } = [];
}

internal static class Bridges
{
    private const string Source = "Flow";
    private const int Key = 0x464C4F31;

    // Glamourer.Api ApplyFlag: Once | Equipment | Customization, and Equipment | Customization for revert.
    private const ulong DesignFlags = 0x01 | 0x02 | 0x04;
    private const ulong RevertFlags = 0x02 | 0x04;

    private static DateTime modsUntil;
    private static List<(string Dir, string Name)> mods = [];
    private static readonly Dictionary<string, (DateTime Until, IReadOnlyDictionary<string, (string[] Options, int Type)> Groups)> groups = new(StringComparer.Ordinal);

    public static bool PluginLoaded(string name)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        try
        {
            foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
            {
                if (!plugin.IsLoaded) continue;
                if (plugin.InternalName.Equals(text, StringComparison.OrdinalIgnoreCase)
                    || plugin.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Plugin list failed");
        }
        return false;
    }

    public static IReadOnlyList<string> PluginNames()
    {
        try
        {
            return Plugin.PluginInterface.InstalledPlugins
                .Select(p => p.InternalName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static bool SetCollection(string profile, bool enable, out string error)
    {
        var name = (profile ?? string.Empty).Trim();
        error = string.Empty;
        if (name.Length == 0)
        {
            error = "No collection name.";
            return false;
        }
        var primary = enable ? "/xlenablecollection" : "/xldisablecollection";
        var legacy = enable ? "/xlenableprofile" : "/xldisableprofile";
        return Dispatch(primary, legacy, name, out error);
    }

    public static bool SetPlugin(string pluginName, bool enable, bool temporary, out string error)
    {
        var name = (pluginName ?? string.Empty).Trim();
        error = string.Empty;
        if (name.Length == 0)
        {
            error = "No plugin name.";
            return false;
        }
        var primary = temporary
            ? enable ? "/xlenableplugintemp" : "/xldisableplugintemp"
            : enable ? "/xlenableplugin" : "/xldisableplugin";
        return Dispatch(primary, primary, name, out error);
    }

    private static bool Dispatch(string primary, string legacy, string name, out string error)
    {
        error = string.Empty;
        var quoted = name.Contains(' ') ? $"\"{name}\"" : name;
        var head = CommandRegistered(primary) ? primary : legacy;
        if (!CommandRegistered(head))
        {
            error = $"{head} is not registered in this Dalamud.";
            return false;
        }
        if (Plugin.CommandManager.ProcessCommand(head + " " + quoted)) return true;
        error = $"{head} refused {name}. A plugin that sits in more than one collection cannot be toggled alone.";
        return false;
    }

    private static bool CommandRegistered(string command)
    {
        foreach (var pair in Plugin.CommandManager.Commands)
        {
            if (pair.Key.Equals(command, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static Dictionary<Guid, string>? Designs()
    {
        foreach (var label in new[] { "Glamourer.GetDesignList.V2", "Glamourer.GetDesignList" })
        {
            try
            {
                return Plugin.PluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>(label).InvokeFunc();
            }
            catch (IpcNotReadyError) { }
            catch (Exception ex) { Plugin.Log.Verbose(ex, "Glamourer {Label} failed", label); }
        }
        return null;
    }

    public static IReadOnlyList<string> GlamourNames()
    {
        var list = Designs();
        if (list is null) return [];
        return list.Values
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool ApplyGlamour(string design, out string error)
    {
        error = string.Empty;
        var name = (design ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            error = "No design.";
            return false;
        }

        var id = Guid.Empty;
        if (!Guid.TryParse(name, out id))
        {
            var list = Designs();
            if (list is null)
            {
                error = "Glamourer is not answering.";
                return false;
            }
            foreach (var pair in list)
            {
                if (!pair.Value.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                id = pair.Key;
                break;
            }
        }
        if (id == Guid.Empty)
        {
            error = $"No Glamourer design named {name}.";
            return false;
        }

        try
        {
            // Glamourer.Api ApplyDesign: (Guid design, int objectIndex, uint key, ulong flags) -> GlamourerApiEc.
            var code = Plugin.PluginInterface
                .GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign")
                .InvokeFunc(id, 0, 0u, DesignFlags);
            if (code is 0 or 1) return true;
            error = "Glamourer apply returned " + GlamourerCode(code);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Glamourer apply failed for {Design}", name);
            error = "Glamourer apply failed: " + ex.Message;
            return false;
        }
    }

    public static bool RevertGlamour(out string error)
    {
        error = string.Empty;
        try
        {
            // Glamourer.Api RevertState: (int objectIndex, uint key, ulong flags) -> GlamourerApiEc.
            var code = Plugin.PluginInterface
                .GetIpcSubscriber<int, uint, ulong, int>("Glamourer.RevertState")
                .InvokeFunc(0, 0u, RevertFlags);
            if (code is 0 or 1) return true;
            error = "Glamourer revert returned " + GlamourerCode(code);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Glamourer revert failed");
            error = "Glamourer revert failed: " + ex.Message;
            return false;
        }
    }

    private static string GlamourerCode(int code) => code switch
    {
        2 => "actor not found",
        3 => "actor not human",
        4 => "design not found",
        6 => "state is locked by another key",
        7 => "invalid state",
        _ => code.ToString(),
    };

    public static IReadOnlyList<string> PenumbraCollections()
    {
        var list = Collections();
        return list is null ? [] : list.Values.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<Guid, string>? Collections()
    {
        try
        {
            return Plugin.PluginInterface
                .GetIpcSubscriber<Dictionary<Guid, string>>("Penumbra.GetCollections.V5")
                .InvokeFunc();
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Penumbra collections failed");
            return null;
        }
    }

    public static bool SetPenumbraCollection(string name, out string error)
    {
        error = string.Empty;
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "No collection name.";
            return false;
        }
        var list = Collections();
        if (list is null)
        {
            error = "Penumbra is not answering.";
            return false;
        }
        var id = list.FirstOrDefault(p => p.Value.Equals(text, StringComparison.OrdinalIgnoreCase)).Key;
        if (id == Guid.Empty)
        {
            error = $"No Penumbra collection named {text}.";
            return false;
        }
        try
        {
            var result = Plugin.PluginInterface
                .GetIpcSubscriber<int, Guid?, bool, bool, (int, (Guid Id, string Name)?)>("Penumbra.SetCollectionForObject.V5")
                .InvokeFunc(0, id, true, false);
            if (result.Item1 is 0 or 1) return true;
            error = "Penumbra returned " + PenumbraCode(result.Item1);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra collection failed for {Name}", text);
            error = "Penumbra collection failed: " + ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<(string Dir, string Name)> Mods()
    {
        if (DateTime.UtcNow < modsUntil) return mods;
        try
        {
            var raw = Plugin.PluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList").InvokeFunc() ?? [];
            mods = raw
                .Select(kv => (Dir: kv.Key, Name: string.IsNullOrWhiteSpace(kv.Value) ? kv.Key : kv.Value))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Penumbra mod list failed");
            mods = [];
        }
        modsUntil = DateTime.UtcNow.AddSeconds(8);
        return mods;
    }

    public static IReadOnlyDictionary<string, (string[] Options, int Type)> ModGroups(string dir, string name)
    {
        if (string.IsNullOrWhiteSpace(dir)) return new Dictionary<string, (string[], int)>();
        if (groups.TryGetValue(dir, out var hit) && DateTime.UtcNow < hit.Until) return hit.Groups;
        IReadOnlyDictionary<string, (string[], int)> found = new Dictionary<string, (string[], int)>();
        try
        {
            found = Plugin.PluginInterface
                .GetIpcSubscriber<string, string, IReadOnlyDictionary<string, (string[], int)>?>("Penumbra.GetAvailableModSettings.V5")
                .InvokeFunc(dir, name ?? string.Empty) ?? found;
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Penumbra groups failed for {Dir}", dir);
        }
        groups[dir] = (DateTime.UtcNow.AddSeconds(6), found);
        return found;
    }

    public static bool TryReadMod(string dir, string name, out ModState state, out string error)
    {
        state = new ModState();
        error = string.Empty;
        var collection = PlayerCollection();
        if (collection == Guid.Empty)
        {
            error = "Penumbra has no collection for you.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(dir))
        {
            error = "No mod directory.";
            return false;
        }
        try
        {
            // (Guid collection, string dir, string name, bool ignoreInheritance) -> (ec, (enabled, priority, settings, inherited)?)
            var result = Plugin.PluginInterface
                .GetIpcSubscriber<Guid, string, string, bool, (int, (bool, int, Dictionary<string, List<string>>, bool)?)>(
                    "Penumbra.GetCurrentModSettings.V5")
                .InvokeFunc(collection, dir, name ?? string.Empty, false);
            if (result.Item1 is not (0 or 1))
            {
                error = "Penumbra returned " + PenumbraCode(result.Item1);
                return false;
            }
            if (result.Item2 is { } row)
            {
                state.Enabled = row.Item1;
                state.Priority = row.Item2;
                state.Settings = row.Item3 ?? [];
                state.Inherit = row.Item4;
            }
            else
            {
                // No settings in this collection or its parents: the mod is off there.
                state.Enabled = false;
                state.Inherit = true;
            }
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Penumbra read failed for {Dir}", dir);
            error = "Penumbra read failed: " + ex.Message;
            return false;
        }
    }

    public static string Pack(ModState state) => JsonSerializer.Serialize(state);

    public static ModState? Unpack(string text)
    {
        try { return JsonSerializer.Deserialize<ModState>(text); }
        catch { return null; }
    }

    // Temporary settings replace every group, so the current groups are read first and laid under the card's.
    public static bool WriteTemporary(string dir, string name, ModState target, out string error)
    {
        error = string.Empty;
        try
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> settings = target.Settings.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value);
            var code = Plugin.PluginInterface
                .GetIpcSubscriber<int, string, string, (bool, bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>), string, int, int>(
                    "Penumbra.SetTemporaryModSettingsPlayer.V5")
                .InvokeFunc(0, dir, name ?? string.Empty, (target.Inherit, target.Enabled, target.Priority, settings), Source, Key);
            if (code is 0 or 1) return true;
            error = "Penumbra returned " + PenumbraCode(code);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra temp write failed for {Dir}", dir);
            error = "Penumbra temp write failed: " + ex.Message;
            return false;
        }
    }

    public static bool WritePermanent(string dir, string name, ModState target, IReadOnlyDictionary<string, List<string>> changes, out string error)
    {
        error = string.Empty;
        var collection = PlayerCollection();
        if (collection == Guid.Empty)
        {
            error = "Penumbra has no collection for you.";
            return false;
        }
        try
        {
            var mod = name ?? string.Empty;
            var pi = Plugin.PluginInterface;
            var codes = new List<int>
            {
                pi.GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TryInheritMod.V5").InvokeFunc(collection, dir, mod, target.Inherit),
            };
            if (!target.Inherit)
            {
                codes.Add(pi.GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TrySetMod.V5").InvokeFunc(collection, dir, mod, target.Enabled));
                codes.Add(pi.GetIpcSubscriber<Guid, string, string, int, int>("Penumbra.TrySetModPriority.V5").InvokeFunc(collection, dir, mod, target.Priority));
                var set = pi.GetIpcSubscriber<Guid, string, string, string, IReadOnlyList<string>, int>("Penumbra.TrySetModSettings.V5");
                foreach (var (group, options) in changes)
                    codes.Add(set.InvokeFunc(collection, dir, mod, group, options));
            }
            var bad = codes.FirstOrDefault(c => c is not (0 or 1));
            if (bad == 0) return true;
            error = "Penumbra returned " + PenumbraCode(bad);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra write failed for {Dir}", dir);
            error = "Penumbra write failed: " + ex.Message;
            return false;
        }
    }

    public static bool ResetTemp(string dir, string name, out string error)
    {
        error = string.Empty;
        try
        {
            int code;
            if (string.IsNullOrWhiteSpace(dir))
            {
                code = Plugin.PluginInterface
                    .GetIpcSubscriber<int, int, int>("Penumbra.RemoveAllTemporaryModSettingsPlayer.V5")
                    .InvokeFunc(0, Key);
            }
            else
            {
                code = Plugin.PluginInterface
                    .GetIpcSubscriber<int, string, string, int, int>("Penumbra.RemoveTemporaryModSettingsPlayer.V5")
                    .InvokeFunc(0, dir, name ?? string.Empty, Key);
            }
            if (code is 0 or 1) return true;
            error = "Penumbra returned " + PenumbraCode(code);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra reset failed");
            error = "Penumbra reset failed: " + ex.Message;
            return false;
        }
    }

    private static string PenumbraCode(int code) => code switch
    {
        2 => "collection missing",
        3 => "mod missing",
        4 => "option group missing",
        5 => "option missing",
        11 => "invalid argument",
        17 => "Penumbra is shutting down",
        19 => "temporary settings are locked by another plugin",
        _ => code.ToString(),
    };

    private static Guid PlayerCollection()
    {
        try
        {
            var forObject = Plugin.PluginInterface
                .GetIpcSubscriber<int, (bool, bool, (Guid Id, string Name))>("Penumbra.GetCollectionForObject.V5")
                .InvokeFunc(0);
            if (forObject.Item3.Id != Guid.Empty) return forObject.Item3.Id;
        }
        catch (IpcNotReadyError) { }
        catch (Exception ex) { Plugin.Log.Verbose(ex, "Penumbra collection lookup failed"); }

        try
        {
            var current = Plugin.PluginInterface
                .GetIpcSubscriber<byte, (Guid Id, string Name)?>("Penumbra.GetCollection")
                .InvokeFunc(0);
            if (current is { Id: var id } && id != Guid.Empty) return id;
        }
        catch { /* older gate */ }

        return Guid.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc.Exceptions;

namespace Flow;

internal static class Bridges
{
    private const string Source = "Flow";
    private const int Key = 0x464C4F31;

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

    public static bool SetCollection(string profile, bool enable)
    {
        var name = (profile ?? string.Empty).Trim();
        if (name.Length == 0) return false;
        var primary = enable ? "/xlenablecollection" : "/xldisablecollection";
        var legacy = enable ? "/xlenableprofile" : "/xldisableprofile";
        return Dispatch(primary, legacy, name);
    }

    public static bool SetPlugin(string pluginName, bool enable, bool temporary)
    {
        var name = (pluginName ?? string.Empty).Trim();
        if (name.Length == 0) return false;
        var primary = temporary
            ? enable ? "/xlenableplugintemp" : "/xldisableplugintemp"
            : enable ? "/xlenableplugin" : "/xldisableplugin";
        return Dispatch(primary, primary, name);
    }

    private static bool Dispatch(string primary, string legacy, string name)
    {
        var quoted = name.Contains(' ') ? $"\"{name}\"" : name;
        var head = CommandRegistered(primary) ? primary : legacy;
        if (!CommandRegistered(head))
        {
            Plugin.Log.Warning("Command {Command} is not registered", head);
            return false;
        }
        return Plugin.CommandManager.ProcessCommand(head + " " + quoted);
    }

    private static bool CommandRegistered(string command)
    {
        foreach (var pair in Plugin.CommandManager.Commands)
        {
            if (pair.Key.Equals(command, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static IReadOnlyList<string> GlamourNames()
    {
        try
        {
            var list = Plugin.PluginInterface
                .GetIpcSubscriber<Dictionary<Guid, string>>("Glamourer.GetDesignList")
                .InvokeFunc();
            if (list is null) return [];
            return list.Values
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (IpcNotReadyError)
        {
            return [];
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Glamourer list failed");
            return [];
        }
    }

    public static bool ApplyGlamour(string design)
    {
        var name = (design ?? string.Empty).Trim();
        if (name.Length == 0) return false;
        try
        {
            var list = Plugin.PluginInterface
                .GetIpcSubscriber<Dictionary<Guid, string>>("Glamourer.GetDesignList")
                .InvokeFunc();
            var id = Guid.Empty;
            if (list is not null)
            {
                foreach (var pair in list)
                {
                    if (pair.Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        id = pair.Key;
                        break;
                    }
                }
            }
            if (id == Guid.Empty && !Guid.TryParse(name, out id)) return false;

            try
            {
                Plugin.PluginInterface
                    .GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(id, 0, 0u, 0ul);
                return true;
            }
            catch (Exception first)
            {
                Plugin.Log.Verbose(first, "Glamourer ApplyDesign ulong failed, retrying");
                Plugin.PluginInterface
                    .GetIpcSubscriber<Guid, int, uint, uint, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(id, 0, 0u, 0u);
                return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Glamourer apply failed for {Design}", name);
            return false;
        }
    }

    public static bool RevertGlamour()
    {
        try
        {
            Plugin.PluginInterface
                .GetIpcSubscriber<int, uint, int>("Glamourer.RevertState")
                .InvokeFunc(0, 0u);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Glamourer revert failed");
            return false;
        }
    }

    public static IReadOnlyList<string> PenumbraCollections()
    {
        try
        {
            var list = Plugin.PluginInterface
                .GetIpcSubscriber<Dictionary<Guid, string>>("Penumbra.GetCollections")
                .InvokeFunc();
            if (list is null) return [];
            return list.Values.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            return [];
        }
    }

    public static bool SetPenumbraCollection(string name)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        try
        {
            var list = Plugin.PluginInterface
                .GetIpcSubscriber<Dictionary<Guid, string>>("Penumbra.GetCollections")
                .InvokeFunc();
            if (list is null) return false;
            var id = Guid.Empty;
            foreach (var pair in list)
            {
                if (pair.Value.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    id = pair.Key;
                    break;
                }
            }
            if (id == Guid.Empty) return false;
            var code = Plugin.PluginInterface
                .GetIpcSubscriber<int, Guid, bool, int>("Penumbra.SetCollectionForObject.V5")
                .InvokeFunc(0, id, true);
            return code == 0;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra collection failed for {Name}", text);
            return false;
        }
    }

    public static bool TryReadMod(string dir, string name, out bool enabled, out int priority)
    {
        enabled = true;
        priority = 0;
        var collection = PlayerCollection();
        if (collection == Guid.Empty || string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            var result = Plugin.PluginInterface
                .GetIpcSubscriber<Guid, string, string, bool, (int Ec, (bool Enabled, int Priority, Dictionary<string, List<string>> Settings, bool Inherited)?)>(
                    "Penumbra.GetCurrentModSettings.V5")
                .InvokeFunc(collection, dir, name ?? string.Empty, false);
            if (result.Item2 is not { } row) return false;
            enabled = row.Enabled;
            priority = row.Priority;
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Penumbra read failed for {Dir}", dir);
            return false;
        }
    }

    public static bool WriteMod(string dir, string name, bool enabled, int priority, bool permanent)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        return permanent ? WritePermanent(dir, name, enabled, priority) : WriteTemporary(dir, name, enabled, priority);
    }

    public static bool ResetTemp(string dir, string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                Plugin.PluginInterface
                    .GetIpcSubscriber<int, int, int>("Penumbra.RemoveAllTemporaryModSettingsPlayer.V5")
                    .InvokeFunc(0, Key);
                return true;
            }

            Plugin.PluginInterface
                .GetIpcSubscriber<int, string, string, int, int>("Penumbra.RemoveTemporaryModSettingsPlayer.V5")
                .InvokeFunc(0, dir, name ?? string.Empty, Key);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra reset failed");
            return false;
        }
    }

    private static bool WriteTemporary(string dir, string name, bool enabled, int priority)
    {
        try
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> settings = new Dictionary<string, IReadOnlyList<string>>();
            var code = Plugin.PluginInterface
                .GetIpcSubscriber<int, string, string, (bool Inherit, bool Enabled, int Priority, IReadOnlyDictionary<string, IReadOnlyList<string>> Settings), string, int, int>(
                    "Penumbra.SetTemporaryModSettingsPlayer.V5")
                .InvokeFunc(0, dir, name ?? string.Empty, (false, enabled, priority, settings), Source, Key);
            return code is 0 or 1;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra temp write failed for {Dir}", dir);
            return false;
        }
    }

    private static bool WritePermanent(string dir, string name, bool enabled, int priority)
    {
        var collection = PlayerCollection();
        if (collection == Guid.Empty) return false;
        try
        {
            Plugin.PluginInterface
                .GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TrySetMod.V5")
                .InvokeFunc(collection, dir, name ?? string.Empty, enabled);
            Plugin.PluginInterface
                .GetIpcSubscriber<Guid, string, string, int, int>("Penumbra.TrySetModPriority.V5")
                .InvokeFunc(collection, dir, name ?? string.Empty, priority);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra write failed for {Dir}", dir);
            return false;
        }
    }

    private static Guid PlayerCollection()
    {
        try
        {
            var forObject = Plugin.PluginInterface
                .GetIpcSubscriber<int, (bool ObjectValid, bool Individual, (Guid Id, string Name) Collection)>("Penumbra.GetCollectionForObject.V5")
                .InvokeFunc(0);
            if (forObject.Collection.Id != Guid.Empty) return forObject.Collection.Id;
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

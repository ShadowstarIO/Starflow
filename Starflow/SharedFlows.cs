using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Starflow;

// The flows folder in Starflow's GitHub repo. Read only when you open Shared flows or press Refresh.
// index.json lists each file with a name and a short description; a file is fetched when you pick it.
public static class SharedFlows
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly object Gate = new();
    private static readonly Dictionary<string, List<FlowItem>> Files = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> FileErrors = new(StringComparer.OrdinalIgnoreCase);

    public static List<SharedIndex.Entry> Entries { get; private set; } = [];
    public static bool Loading { get; private set; }
    public static string Error { get; private set; } = string.Empty;
    public static DateTime LoadedAt { get; private set; } = DateTime.MinValue;
    public static string FileLoading { get; private set; } = string.Empty;

    // Reads index.json. A load in the last five minutes is reused unless forced.
    public static void Refresh(bool force)
    {
        lock (Gate)
        {
            if (Loading) return;
            if (!force && DateTime.UtcNow - LoadedAt < TimeSpan.FromMinutes(5) && Error.Length == 0) return;
            Loading = true;
            Error = string.Empty;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await Client.GetStringAsync(SharedIndex.Folder + "index.json?t=" + DateTime.UtcNow.Ticks).ConfigureAwait(false);
                var list = SharedIndex.Parse(text);
                lock (Gate)
                {
                    Entries = list;
                    Files.Clear();
                    FileErrors.Clear();
                    LoadedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Shared flows list failed");
                lock (Gate) Error = "Could not load the list: " + ex.Message;
            }
            finally
            {
                lock (Gate) Loading = false;
            }
        });
    }

    // The flows in one file, once fetched. Null while loading or not asked for yet.
    public static List<FlowItem>? FlowsIn(SharedIndex.Entry entry)
    {
        lock (Gate) return Files.TryGetValue(entry.File, out var flows) ? flows : null;
    }

    // Starts a download unless that file is loaded, loading, or failed. Safe to call every frame.
    public static void Fetch(SharedIndex.Entry entry, bool retry = false)
    {
        lock (Gate)
        {
            if (retry) FileErrors.Remove(entry.File);
            if (Files.ContainsKey(entry.File) || FileErrors.ContainsKey(entry.File) || FileLoading.Length > 0) return;
            FileLoading = entry.File;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await Client.GetStringAsync(entry.Url).ConfigureAwait(false);
                if (!Share.TryRead(text, out var flows, out var error)) throw new InvalidOperationException(error);
                lock (Gate) Files[entry.File] = flows;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Shared flow {File} failed", entry.File);
                lock (Gate) FileErrors[entry.File] = $"Could not load {entry.File}: {ex.Message}";
            }
            finally
            {
                lock (Gate) FileLoading = string.Empty;
            }
        });
    }

    public static string ErrorFor(SharedIndex.Entry entry)
    {
        lock (Gate) return FileErrors.TryGetValue(entry.File, out var error) ? error : string.Empty;
    }
}

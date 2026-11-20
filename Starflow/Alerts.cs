using System;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Starflow;

// Local alerts for Prompt mode. Only you see or hear these.
public static class Alerts
{
    public static void Toast(string text)
    {
        try { Plugin.Toast.ShowNormal(text); }
        catch (Exception ex) { Plugin.Log.Verbose(ex, "Toast failed"); }
    }

    // The chat sound effects, <se.1> to <se.16>.
    public static void Sound(int effect)
    {
        if (effect is < 1 or > 16) return;
        try { UIGlobals.PlayChatSoundEffect((uint)effect); }
        catch (Exception ex) { Plugin.Log.Verbose(ex, "Sound failed"); }
    }

    public static void Prompt(Configuration cfg, int waiting)
    {
        if (cfg.PromptToast) Toast(waiting == 1 ? "Starflow: a flow is waiting for a yes." : $"Starflow: {waiting} flows are waiting for a yes.");
        if (cfg.PromptSound > 0) Sound(cfg.PromptSound);
    }
}

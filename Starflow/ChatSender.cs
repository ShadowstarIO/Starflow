using System;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Starflow;

internal static class ChatSender
{
    public const int CommentMax = 60;

    // Dalamud commands go through ICommandManager. Everything else goes through the chat box as the game's own command.
    public static bool TrySend(string command) => TrySend(command, out _);

    public static bool TrySend(string command, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            error = "Empty command.";
            return false;
        }
        if (!Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer is null)
        {
            error = "Not logged in.";
            return false;
        }
        command = command.Trim();
        if (!command.StartsWith('/')) command = "/" + command;
        if (IsSelf(command))
        {
            error = "A card cannot send /starflow, /flow, or /fl.";
            return false;
        }

        var head = command.Split(' ', 2)[0];
        if (Plugin.CommandManager.Commands.ContainsKey(head))
        {
            if (Plugin.CommandManager.ProcessCommand(command)) return true;
            error = head + " did not run.";
            return false;
        }

        try
        {
            unsafe
            {
                var ui = UIModule.Instance();
                if (ui == null)
                {
                    error = "Chat box not ready.";
                    return false;
                }
                var message = Utf8String.FromString(command);
                if (message == null)
                {
                    error = "Could not build the message.";
                    return false;
                }
                ui->ProcessChatBoxEntry(message);
                message->Dtor(true);
                return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Command failed: {Command}", command);
            error = ex.Message;
            return false;
        }
    }

    public static string? StatusCommand(string value) => value.Trim().ToLowerInvariant() switch
    {
        "online" => null,
        "away" or "afk" => "/away on",
        "busy" => "/busy on",
        "roleplaying" or "rp" => "/roleplaying on",
        "lookingtomeld" or "meld" => "/lookingtomeld on",
        "lookingforparty" or "lfp" => "/lookingforparty on",
        _ => null,
    };

    // The command that turns the shown status off. Reads the name the client shows, such as "Away from Keyboard".
    public static string? StatusOff(string current)
    {
        var text = (current ?? string.Empty).ToLowerInvariant();
        if (text.Contains("away")) return "/away off";
        if (text.Contains("busy")) return "/busy off";
        if (text.Contains("role")) return "/roleplaying off";
        if (text.Contains("meld")) return "/lookingtomeld off";
        if (text.Contains("party")) return "/lookingforparty off";
        return null;
    }

    // A status name the client shows, as the token StatusCommand takes. Empty when there is no command for it.
    public static string StatusToken(string shown)
    {
        var text = (shown ?? string.Empty).ToLowerInvariant();
        if (text.Length == 0 || text.Contains("online")) return "online";
        if (text.Contains("away")) return "away";
        if (text.Contains("busy")) return "busy";
        if (text.Contains("role")) return "rp";
        if (text.Contains("meld")) return "meld";
        if (text.Contains("party")) return "lfp";
        return string.Empty;
    }

    public static string Comment(string text)
    {
        var body = (text ?? string.Empty).Replace('\n', ' ').Trim();
        if (body.Length > CommentMax) body = body[..CommentMax];
        return "/searchcomment " + body;
    }

    private static bool IsSelf(string command)
    {
        var head = command.Trim().Split(' ', 2)[0];
        return head.Equals("/fl", StringComparison.OrdinalIgnoreCase)
            || head.Equals("/flow", StringComparison.OrdinalIgnoreCase)
            || head.Equals("/starflow", StringComparison.OrdinalIgnoreCase);
    }
}

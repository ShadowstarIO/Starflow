using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Starflow;

// Keeps the last couple of minutes of chat so a Chat message card can look back a few seconds.
// Only lines from channels a card can name are kept. Nothing is written to disk.
internal static class ChatWatch
{
    private const int MaxLines = 60;
    private static readonly TimeSpan Keep = TimeSpan.FromMinutes(2);
    private static readonly List<(DateTime At, string Channel, string Sender, string Text, bool Mine)> lines = [];
    private static readonly object gate = new();

    public static void Start() => Plugin.Chat.ChatMessage += OnMessage;

    public static void Stop()
    {
        Plugin.Chat.ChatMessage -= OnMessage;
        lock (gate) lines.Clear();
    }

    public static List<ChatLine> Recent()
    {
        var now = DateTime.UtcNow;
        lock (gate)
        {
            lines.RemoveAll(l => now - l.At > Keep);
            return lines.Select(l => new ChatLine(l.Channel, l.Sender, l.Text, (now - l.At).TotalSeconds, l.Mine)).ToList();
        }
    }

    public static string? Channel(XivChatType type) => type switch
    {
        XivChatType.Say => "say",
        XivChatType.Yell => "yell",
        XivChatType.Shout => "shout",
        XivChatType.TellIncoming or XivChatType.TellOutgoing => "tell",
        XivChatType.Party or XivChatType.CrossParty => "party",
        XivChatType.Alliance => "alliance",
        XivChatType.FreeCompany => "fc",
        XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
            or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8 => "ls",
        XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
            or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8 => "cwls",
        XivChatType.NoviceNetwork => "novice",
        XivChatType.CustomEmote or XivChatType.StandardEmote => "emote",
        XivChatType.PvPTeam => "pvpteam",
        XivChatType.Echo => "echo",
        _ => null,
    };

    // The last combat log line that hurt you, and whether a player (or a player's pet) caused it.
    public static DateTime HurtAt { get; private set; } = DateTime.MinValue;
    public static bool HurtByPlayer { get; private set; }

    private static void OnMessage(IHandleableChatMessage msg)
    {
        try
        {
            var type = msg.LogKind;
            if (type == XivChatType.Damage && msg.TargetKind == XivChatRelationKind.LocalPlayer)
            {
                HurtAt = DateTime.UtcNow;
                HurtByPlayer = msg.SourceKind is XivChatRelationKind.PartyMember or XivChatRelationKind.AllianceMember or XivChatRelationKind.OtherPlayer
                    or XivChatRelationKind.PetOrCompanionParty or XivChatRelationKind.PetOrCompanionAlliance or XivChatRelationKind.PetOrCompanionOther;
                return;
            }
            var sender = msg.Sender;
            var message = msg.Message;
            var channel = Channel(type);
            if (channel is null) return;
            var player = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            var name = player?.PlayerName ?? sender.TextValue;
            var me = Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
            var mine = type == XivChatType.TellOutgoing
                || (me.Length > 0 && (name.Equals(me, StringComparison.OrdinalIgnoreCase) || sender.TextValue.EndsWith(me, StringComparison.OrdinalIgnoreCase)));
            if (type == XivChatType.Echo) name = string.Empty;
            var text = message.TextValue;
            lock (gate)
            {
                lines.Add((DateTime.UtcNow, channel, name, text, mine));
                if (lines.Count > MaxLines) lines.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Chat read failed");
        }
    }
}

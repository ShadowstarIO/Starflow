using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace Starflow.Windows;

// Editors for the cards that read play and chat, and the Lifestream travel card.
public sealed partial class MainWindow
{
    private static readonly string[] StatKeys = ["hp", "mp", "gp", "cp"];
    private static readonly string[] StatNames = ["HP", "MP", "GP", "CP"];
    private static readonly string[] VitalWhoKeys = ["me", "target", "party-any", "party-all"];
    private static readonly string[] VitalWhoNames = ["You", "Your target", "Any other party member", "Everyone in the party"];
    private static readonly string[] EffectWhoKeys = ["me", "target", "party", "party-others"];
    private static readonly string[] EffectWhoNames = ["You", "Your target", "Anyone in the party", "Another party member"];
    private static readonly string[] HurtKeys = ["any", "player", "npc"];
    private static readonly string[] HurtNames = ["Anything", "A player", "An NPC"];
    private static readonly string[] ChatChannels = ["tell", "say", "yell", "shout", "party", "alliance", "fc", "ls", "cwls", "novice", "emote", "pvpteam", "echo"];
    private static readonly string[] ChatChannelNames = ["Tell", "Say", "Yell", "Shout", "Party", "Alliance", "Free Company", "Linkshell", "Cross-world LS", "Novice Network", "Emote", "PvP team", "Echo"];
    private static readonly string[] TravelKeys = ["home", "fc", "apartment", "inn", "gc", "auto"];
    private static readonly string[] TravelNames = ["Your house", "Free Company house", "Your apartment", "An inn room", "Grand Company", "Lifestream's choice"];

    private bool EditReaction(Configuration cfg, Card card, World here)
    {
        switch (card.Type)
        {
            case CardType.Vitals:
                Choice(cfg, "Stat", StatKeys, StatNames, v => card.Option = v, card.Option);
                Choice(cfg, "Whose", VitalWhoKeys, VitalWhoNames, v => card.Extra = v, card.Extra);
                Text(cfg, "Compare", v => card.Value = v, card.Value, "<50%  >=8000  =0  20-40%");
                ImGui.TextDisabled("A % compares against the maximum. GP and CP are only known for you.");
                return true;
            case CardType.Effect:
                Choice(cfg, "On", EffectWhoKeys, EffectWhoNames, v => card.Extra = v, card.Extra);
                MatchText(cfg, "Effect", v => card.Value = v, card.Value, "Weakness | Brink of Death | /regex/");
                var seen = here.Me.Effects.Concat(here.TargetUnit.Effects).Concat(here.Party.SelectMany(p => p.Effects))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
                Pick(cfg, "Effects now", seen, v => card.Value = AddWord(card.Value, v));
                ImGui.TextDisabled("Names as the game shows them. Any of the | words matches.");
                return true;
            case CardType.Enmity:
                Text(cfg, "Enemies", v => card.Value = v, card.Value, ">=1  =0  >=3");
                ImGui.TextDisabled("Counts enemies (and hostile players in PvP) whose target is you.");
                return true;
            case CardType.Hurt:
                Choice(cfg, "From", HurtKeys, HurtNames, v => card.Option = v, card.Option);
                Int(cfg, "Within (s)", card.Number <= 0 ? 3 : card.Number, v => card.Number = Math.Clamp(v, 1, 60));
                Text(cfg, "At least", v => card.Value = v, card.Value, "blank, 5000, or 10% of max HP");
                ImGui.TextDisabled("True for a few seconds after a hit. The combat log names the source; without it, only PvP counts as a player.");
                return true;
            case CardType.PartyDown:
                Text(cfg, "Down", v => card.Value = v, card.Value, ">=1  =0  >=2");
                ImGui.TextDisabled("Party members at 0 HP, you included.");
                return true;
            case CardType.Chat:
                EditChat(cfg, card, here);
                return true;
            case CardType.Lifestream:
                EditTravel(cfg, card, here);
                return true;
        }
        return false;
    }

    private void EditChat(Configuration cfg, Card card, World here)
    {
        Ui.Wrapped(Amber, "Other people can trigger this flow by typing. Whatever its Do and After cards do, anyone who can post in these channels can make it happen: emotes, speech, looks, travel. Keep the channels and senders narrow, and use Prompt mode unless you trust everyone who can reach you. How you use it is up to you.");
        ImGui.TextDisabled("Channels");
        var picked = Words(card.Option);
        var changed = false;
        for (var i = 0; i < ChatChannels.Length; i++)
        {
            if (i > 0) Ui.Next(Ui.CheckWidth(ChatChannelNames[i]));
            var on = picked.Contains(ChatChannels[i]);
            if (!ImGui.Checkbox(ChatChannelNames[i] + "##chan", ref on)) continue;
            if (on) picked.Add(ChatChannels[i]); else picked.Remove(ChatChannels[i]);
            changed = true;
        }
        if (changed) { card.Option = string.Join('|', ChatChannels.Where(picked.Contains)); cfg.Save(); }

        MatchText(cfg, "From", v => card.Extra = v, card.Extra, "blank for anyone, Name | Other Name, /regex/");
        if (string.IsNullOrWhiteSpace(card.Extra) && !picked.SetEquals(["echo"]))
            Ui.Wrapped(Red, "No sender set: anyone in those channels can trigger it.");
        var senders = here.Chat.Where(c => !c.Mine && c.Sender.Length > 0).Select(c => c.Sender).Distinct().OrderBy(n => n).ToList();
        Pick(cfg, "Recent senders", senders, v => card.Extra = AddWord(card.Extra, v));
        MatchText(cfg, "Says", v => card.Value = v, card.Value, "blank for any message, a word, or /regex/");
        Int(cfg, "Stays true (s)", card.Number <= 0 ? 5 : card.Number, v => card.Number = Math.Clamp(v, 1, 120));
        var mine = card.Permanent;
        if (ImGui.Checkbox("Count my own messages", ref mine)) { card.Permanent = mine; cfg.Save(); }
        ImGui.TextDisabled("Tokens {chatsender}, {chatmessage}, and {chatchannel} hold the latest line from someone else.");
    }

    private void EditTravel(Configuration cfg, Card card, World here)
    {
        if (!Eval.PluginLoaded("Lifestream")) Ui.Wrapped(Amber, "Lifestream is not loaded. This card does nothing until it is.");
        var at = Array.IndexOf(TravelKeys, (card.Value ?? string.Empty).Trim().ToLowerInvariant());
        for (var i = 0; i < TravelKeys.Length; i++)
        {
            if (i > 0) Ui.Next(Ui.ButtonWidth(TravelNames[i]));
            var chosen = i == at;
            if (chosen) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            if (ImGui.SmallButton(TravelNames[i] + "##travel")) { card.Value = TravelKeys[i]; cfg.Save(); }
            if (chosen) ImGui.PopStyleColor();
        }
        Text(cfg, "Destination", v => card.Value = v, card.Value ?? string.Empty, "home, fc, a world, an aethernet shard, {home}");
        if (here.HomeWorldName.Length > 0 && Ui.Current(here.HomeWorldName, "home world " + here.HomeWorldName, "lshome")) { card.Value = here.HomeWorldName; cfg.Save(); }
        ImGui.TextDisabled("Sent to Lifestream as /li <destination>. Lifestream decides the route and refuses while you are busy.");
        TokenHint(card.Value ?? string.Empty, here);
    }

    private static string NowReaction(Card card, World w) => card.Type switch
    {
        CardType.Vitals => Vital(card, w),
        CardType.Effect => (card.Extra switch
        {
            "target" => w.TargetUnit.Effects,
            "party" or "party-others" => w.Party.SelectMany(p => p.Effects).Concat(card.Extra == "party" ? w.Me.Effects : []).Distinct().ToList(),
            _ => w.Me.Effects,
        }) is { Count: > 0 } list ? string.Join(", ", list) : "no effects",
        CardType.Enmity => w.EnemiesOnMe == 0 ? "no enemy is targeting you" : $"{w.EnemiesOnMe} targeting you",
        CardType.Hurt => w.HurtAgoSec > 600 ? "no damage seen yet"
            : $"last hit {w.HurtAgoSec:0} s ago" + (w.HurtAmount > 0 ? $", {w.HurtAmount} HP" : string.Empty) + (w.HurtByPlayer ? ", by a player" : ", by an NPC"),
        CardType.PartyDown => $"{w.PartyDown} down",
        CardType.Chat => w.LastChat is { } line ? $"last: [{line.Channel}] {line.Sender}: {Short(line.Text)} ({line.AgeSec:0} s ago)" : "no chat yet",
        _ => string.Empty,
    };

    private static string Vital(Card card, World w)
    {
        var stat = card.Option.Length == 0 ? "hp" : card.Option;
        string One(Unit u)
        {
            var (now, max) = u.Stat(stat);
            return max <= 0 ? "-" : $"{now}/{max} ({now * 100 / max}%)";
        }
        return card.Extra switch
        {
            "target" => w.TargetUnit.Present ? $"{w.TargetUnit.Name} {One(w.TargetUnit)}" : "no target",
            "party-any" or "party-all" => w.Party.Count == 0 ? $"solo · you {One(w.Me)}" : $"you {One(w.Me)} · " + string.Join(" · ", w.Party.Select(p => $"{p.Name} {One(p)}")),
            _ => One(w.Me),
        };
    }

    private static string Short(string text) => text.Length <= 60 ? text : text[..57] + "...";

    // A combo of display names that stores a short key.
    private static void Choice(Configuration cfg, string label, string[] keys, string[] names, Action<string> set, string current)
    {
        var index = Math.Max(0, Array.FindIndex(keys, k => k.Equals(current, StringComparison.OrdinalIgnoreCase)));
        if (!Ui.Combo(label, ref index, names, names.Length)) return;
        set(keys[index]);
        cfg.Save();
    }
}

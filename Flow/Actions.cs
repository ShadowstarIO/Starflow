using System;
using System.Globalization;
using Dalamud.Game.Config;

namespace Flow;

internal readonly record struct ActResult(bool Ok, string Detail, string? SlotKey, string? Original);

internal static class Actions
{
    public static ActResult Run(Card card, bool restore, string? original)
    {
        try
        {
            return card.Type switch
            {
                CardType.Command => Cmd(card),
                CardType.Wait => new ActResult(true, "Wait", null, null),
                CardType.Notice => Note(card),
                CardType.Setting => SetOption(card, restore, original),
                CardType.Online => SetStatus(card, restore, original),
                CardType.Collection => Collection(card),
                CardType.PluginSet => PluginSet(card),
                CardType.Glamour => Glamour(card, restore),
                CardType.GlamourRevert => new ActResult(Bridges.RevertGlamour(), "Glamour revert", null, null),
                CardType.PenumbraMod => Mod(card, restore, original),
                CardType.PenumbraCollection => new ActResult(Bridges.SetPenumbraCollection(card.Value), "Penumbra collection", null, null),
                CardType.PenumbraReset => new ActResult(Bridges.ResetTemp(card.Option, card.Value), "Penumbra reset", null, null),
                _ => new ActResult(false, "Unknown card", null, null),
            };
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Action failed: {Type}", card.Type);
            return new ActResult(false, card.Label, null, null);
        }
    }

    public static void Restore(string key, string original)
    {
        if (key.StartsWith("setting:", StringComparison.Ordinal))
        {
            var bits = key.Split(':', 3);
            if (bits.Length < 3) return;
            WriteSetting(bits[1], bits[2], original, out _);
            return;
        }

        if (key == "status")
        {
            var cmd = ChatSender.StatusCommand(original);
            if (cmd is not null) ChatSender.TrySend(cmd);
            return;
        }

        if (key == "glamour")
        {
            Bridges.RevertGlamour();
            return;
        }

        if (key.StartsWith("penumbra:", StringComparison.Ordinal))
        {
            var dir = key["penumbra:".Length..];
            var parts = (original ?? string.Empty).Split('|');
            if (parts.Length < 4) return;
            var permanent = parts[0] == "set";
            var enabled = parts[1] == "1";
            var priority = int.TryParse(parts[2], out var n) ? n : 0;
            var name = parts[3];
            Bridges.WriteMod(dir, name, enabled, priority, permanent);
        }
    }

    private static ActResult Cmd(Card card)
    {
        var ok = ChatSender.TrySend(card.Value);
        return new ActResult(ok, card.Value, null, null);
    }

    private static ActResult Note(Card card)
    {
        if (!string.IsNullOrWhiteSpace(card.Value)) Plugin.Notify(card.Value.Trim());
        return new ActResult(true, card.Value, null, null);
    }

    private static ActResult Collection(Card card)
    {
        var ok = Bridges.SetCollection(card.Value, card.Flag);
        return new ActResult(ok, (card.Flag ? "Enable " : "Disable ") + card.Value, null, null);
    }

    private static ActResult PluginSet(Card card)
    {
        var ok = Bridges.SetPlugin(card.Value, card.Flag, !card.Permanent);
        return new ActResult(ok, card.Label, null, null);
    }

    private static ActResult Glamour(Card card, bool restore)
    {
        if (restore) return new ActResult(Bridges.RevertGlamour(), "Glamour revert", null, null);
        var ok = Bridges.ApplyGlamour(card.Value);
        return new ActResult(ok, "Glamour " + card.Value, ok ? "glamour" : null, "revert");
    }

    private static ActResult SetStatus(Card card, bool restore, string? original)
    {
        if (restore)
        {
            var back = ChatSender.StatusCommand(original ?? "online");
            var ok = back is not null && ChatSender.TrySend(back);
            return new ActResult(ok, "Status restore", null, null);
        }

        var cmd = ChatSender.StatusCommand(card.Value);
        if (cmd is null) return new ActResult(false, "Unknown status", null, null);
        var current = Plugin.Instance.Engine.Last.OnlineStatus;
        var sent = ChatSender.TrySend(cmd);
        return new ActResult(sent, cmd, sent ? "status" : null, string.IsNullOrWhiteSpace(current) ? "online" : current);
    }

    private static ActResult Mod(Card card, bool restore, string? original)
    {
        var dir = (card.Option ?? string.Empty).Trim();
        var name = (card.Value ?? string.Empty).Trim();
        if (restore)
        {
            Actions.Restore("penumbra:" + dir, original ?? string.Empty);
            return new ActResult(true, "Penumbra restore", null, null);
        }

        var had = Bridges.TryReadMod(dir, name, out var enabled, out var priority);
        var ok = Bridges.WriteMod(dir, name, card.Flag, card.Number, card.Permanent);
        var snap = $"{(card.Permanent ? "set" : "temp")}|{(enabled ? "1" : "0")}|{priority}|{name}";
        return new ActResult(ok, card.Label, ok && had ? "penumbra:" + dir : null, snap);
    }

    private static ActResult SetOption(Card card, bool restore, string? original)
    {
        var section = string.IsNullOrWhiteSpace(card.Option) ? "System" : card.Option.Trim();
        var name = (card.Value ?? string.Empty).Trim();
        var written = restore ? original ?? string.Empty : (card.Extra ?? string.Empty).Trim();
        if (!WriteSetting(section, name, written, out var previous))
            return new ActResult(false, $"{name}={written}", null, null);
        return new ActResult(true, $"{name}={written}", restore ? null : $"setting:{section}:{name}", previous);
    }

    private static bool WriteSetting(string section, string name, string raw, out string previous)
    {
        previous = string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var isFloat = raw.Contains('.') || raw.Contains(',');
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)
            && !uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return false;
        var number = uint.TryParse(raw.Split('.')[0], out var n) ? n : (uint)floating;

        if (section.Equals("Ui", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<UiConfigOption>(name, true, out var option)) return false;
            previous = ReadUi(option, isFloat);
            if (isFloat) Plugin.GameConfig.Set(option, floating);
            else Plugin.GameConfig.Set(option, number);
            return true;
        }

        if (!Enum.TryParse<SystemConfigOption>(name, true, out var sys)) return false;
        previous = ReadSystem(sys, isFloat);
        if (isFloat) Plugin.GameConfig.Set(sys, floating);
        else Plugin.GameConfig.Set(sys, number);
        WriteTwin(sys, number, floating, isFloat);
        return true;
    }

    private static string ReadSystem(SystemConfigOption option, bool isFloat)
    {
        if (isFloat && Plugin.GameConfig.TryGet(option, out float f))
            return f.ToString(CultureInfo.InvariantCulture);
        if (Plugin.GameConfig.TryGet(option, out uint u))
            return u.ToString(CultureInfo.InvariantCulture);
        return string.Empty;
    }

    private static string ReadUi(UiConfigOption option, bool isFloat)
    {
        if (isFloat && Plugin.GameConfig.TryGet(option, out float f))
            return f.ToString(CultureInfo.InvariantCulture);
        if (Plugin.GameConfig.TryGet(option, out uint u))
            return u.ToString(CultureInfo.InvariantCulture);
        return string.Empty;
    }

    private static void WriteTwin(SystemConfigOption option, uint number, float floating, bool isFloat)
    {
        var otherName = option.ToString().EndsWith("_DX11", StringComparison.Ordinal)
            ? null
            : option.ToString() + "_DX11";
        if (otherName is null) return;
        if (!Enum.TryParse<SystemConfigOption>(otherName, out var other) || other.Equals(option)) return;
        try
        {
            if (isFloat) Plugin.GameConfig.Set(other, floating);
            else Plugin.GameConfig.Set(other, number);
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Config twin failed: {Option}", otherName);
        }
    }
}

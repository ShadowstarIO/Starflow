using System;
using System.Globalization;
using Dalamud.Game.Config;

namespace Flow;

internal readonly record struct ActResult(bool Ok, string Detail, string? SlotKey = null, string? Original = null, bool NeedsConfirm = false);

internal static class Actions
{
    public static ActResult Run(Card card, World world, bool confirmed = false)
    {
        try
        {
            return card.Type switch
            {
                CardType.Command => Cmd(Tokens.Fill(card.Value, world)),
                CardType.Wait => new ActResult(true, "Wait"),
                CardType.Notice => Note(Tokens.Fill(card.Value, world)),
                CardType.SearchComment => Cmd(ChatSender.Comment(Tokens.Fill(card.Value, world))),
                CardType.Setting => SetOption(card),
                CardType.Online => SetStatus(card, world),
                CardType.Collection => Collection(card),
                CardType.PluginSet => PluginSet(card),
                CardType.Glamour => Glamour(card),
                CardType.GlamourRevert => GlamourRevert(),
                CardType.PenumbraMod => Mod(card),
                CardType.PenumbraCollection => Fail(Bridges.SetPenumbraCollection(card.Value, out var e1), card.Label, e1),
                CardType.PenumbraReset => Fail(Bridges.ResetTemp(card.Option, card.Value, out var e2), card.Label, e2),
                CardType.Logout => Leave(card, "/logout", confirmed),
                CardType.CloseGame => Leave(card, "/shutdown", confirmed),
                _ => new ActResult(false, "Unknown card " + card.Type),
            };
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Action failed: {Type}", card.Type);
            return new ActResult(false, $"{card.Label}: {ex.Message}");
        }
    }

    public static bool Restore(string key, string original, out string error)
    {
        error = string.Empty;
        if (key.StartsWith("setting:", StringComparison.Ordinal))
        {
            var bits = key.Split(':', 3);
            if (bits.Length < 3) return false;
            if (original.Length == 0)
            {
                error = $"No saved value for {bits[2]}.";
                return false;
            }
            return WriteSetting(bits[1], bits[2], original, out _, out error);
        }

        if (key == "status")
        {
            var current = Plugin.Instance.Engine.Last.OnlineStatus;
            var cmd = original == "online" ? ChatSender.StatusOff(current) : ChatSender.StatusCommand(original);
            if (cmd is null) return original == "online";
            return ChatSender.TrySend(cmd, out error);
        }

        if (key == "glamour") return Bridges.RevertGlamour(out error);

        if (key.StartsWith("penumbra:", StringComparison.Ordinal))
        {
            var dir = key["penumbra:".Length..];
            var parts = (original ?? string.Empty).Split('|', 3);
            var name = parts.Length > 1 ? parts[1] : string.Empty;
            if (parts[0] == "temp") return Bridges.ResetTemp(dir, name, out error);
            if (parts[0] == "perm" && parts.Length == 3 && Bridges.Unpack(parts[2]) is { } state)
                return Bridges.WritePermanent(dir, name, state, state.Settings, out error);
            error = "No saved Penumbra state.";
            return false;
        }

        error = "Nothing to restore for " + key;
        return false;
    }

    private static ActResult Fail(bool ok, string detail, string error) => new(ok, ok ? detail : error);

    private static ActResult Cmd(string command)
    {
        var ok = ChatSender.TrySend(command, out var error);
        return new ActResult(ok, ok ? command : error);
    }

    private static ActResult Note(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new ActResult(false, "Empty notice.");
        Plugin.Notify(text.Trim());
        return new ActResult(true, text);
    }

    private static ActResult Leave(Card card, string command, bool confirmed)
    {
        if (card.Flag && !confirmed) return new ActResult(true, "Waiting for a yes", NeedsConfirm: true);
        return Cmd(command);
    }

    private static ActResult Collection(Card card)
    {
        var ok = Bridges.SetCollection(card.Value, card.Flag, out var error);
        return new ActResult(ok, ok ? card.Label : error);
    }

    private static ActResult PluginSet(Card card)
    {
        var ok = Bridges.SetPlugin(card.Value, card.Flag, !card.Permanent, out var error);
        return new ActResult(ok, ok ? card.Label : error);
    }

    private static ActResult Glamour(Card card)
    {
        var ok = Bridges.ApplyGlamour(card.Value, out var error);
        return new ActResult(ok, ok ? card.Label : error, ok ? "glamour" : null, "revert");
    }

    private static ActResult GlamourRevert()
    {
        var ok = Bridges.RevertGlamour(out var error);
        return new ActResult(ok, ok ? "Glamour revert" : error, ok ? "glamour" : null, "revert");
    }

    private static ActResult SetStatus(Card card, World world)
    {
        var want = (card.Value ?? string.Empty).Trim().ToLowerInvariant();
        var current = world.OnlineStatus;
        var before = ChatSender.StatusToken(current);
        string? cmd = want == "online" ? ChatSender.StatusOff(current) : ChatSender.StatusCommand(want);
        if (cmd is null && want != "online") return new ActResult(false, $"No command for status {card.Value}.");

        if (cmd is not null && !ChatSender.TrySend(cmd, out var error)) return new ActResult(false, error);
        if (before.Length == 0)
            return new ActResult(true, $"{cmd ?? "online"} (the old status {current} has no command, so it will not be put back)");
        return new ActResult(true, cmd ?? "Already online", "status", before);
    }

    private static ActResult Mod(Card card)
    {
        var dir = (card.Option ?? string.Empty).Trim();
        var name = (card.Value ?? string.Empty).Trim();
        if (!Bridges.TryReadMod(dir, name, out var current, out var error))
            return new ActResult(false, error);

        var changes = ModOptions.Parse(card.Extra);
        var target = new ModState
        {
            Enabled = card.Flag,
            Priority = card.Number,
            Inherit = card.Inherit,
            Settings = ModOptions.Merge(current.Settings, changes),
        };

        if (card.Permanent)
        {
            var ok = Bridges.WritePermanent(dir, name, target, changes, out error);
            return new ActResult(ok, ok ? card.Label : error, ok ? "penumbra:" + dir : null, $"perm|{name}|{Bridges.Pack(current)}");
        }

        var done = Bridges.WriteTemporary(dir, name, target, out error);
        return new ActResult(done, done ? card.Label : error, done ? "penumbra:" + dir : null, $"temp|{name}");
    }

    private static ActResult SetOption(Card card)
    {
        var section = string.IsNullOrWhiteSpace(card.Option) ? "System" : card.Option.Trim();
        var name = (card.Value ?? string.Empty).Trim();
        var written = (card.Extra ?? string.Empty).Trim();
        if (!WriteSetting(section, name, written, out var previous, out var error))
            return new ActResult(false, error);
        var detail = $"{name}={written}";
        if (Resolution(name)) detail += " (config only; the live window was not resized by Flow)";
        return new ActResult(true, detail, $"setting:{section}:{name}", previous);
    }

    public static bool Resolution(string name) => name is "ScreenWidth" or "ScreenHeight" or "FullScreenWidth" or "FullScreenHeight" or "ScreenMode";

    public static bool RealOption(string section, string name) =>
        section.Equals("Ui", StringComparison.OrdinalIgnoreCase)
            ? Enum.TryParse<UiConfigOption>(name, true, out _)
            : Enum.TryParse<SystemConfigOption>(name, true, out _);

    public static string[] OptionNames(string section) =>
        section.Equals("Ui", StringComparison.OrdinalIgnoreCase) ? Enum.GetNames<UiConfigOption>() : Enum.GetNames<SystemConfigOption>();

    public static string ReadOption(string section, string name)
    {
        if (section.Equals("Ui", StringComparison.OrdinalIgnoreCase))
            return Enum.TryParse<UiConfigOption>(name, true, out var ui) ? ReadUi(ui) : string.Empty;
        return Enum.TryParse<SystemConfigOption>(name, true, out var sys) ? ReadSystem(sys) : string.Empty;
    }

    private static bool WriteSetting(string section, string name, string raw, out string previous, out string error)
    {
        previous = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "No option name.";
            return false;
        }
        var isFloat = raw.Contains('.') || raw.Contains(',');
        if (!float.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var floating))
        {
            error = $"{raw} is not a number.";
            return false;
        }
        var number = (uint)Math.Max(0, Math.Round(floating));

        if (section.Equals("Ui", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<UiConfigOption>(name, true, out var option))
            {
                error = $"{name} is not a real UI option.";
                return false;
            }
            previous = ReadUi(option);
            if (isFloat || previous.Contains('.')) Plugin.GameConfig.Set(option, floating);
            else Plugin.GameConfig.Set(option, number);
            return true;
        }

        if (!Enum.TryParse<SystemConfigOption>(name, true, out var sys))
        {
            error = $"{name} is not a real system option.";
            return false;
        }
        previous = ReadSystem(sys);
        isFloat |= previous.Contains('.');
        if (isFloat) Plugin.GameConfig.Set(sys, floating);
        else Plugin.GameConfig.Set(sys, number);
        WriteTwin(sys, number, floating, isFloat);
        return true;
    }

    private static string ReadSystem(SystemConfigOption option)
    {
        if (Plugin.GameConfig.TryGet(option, out uint u)) return u.ToString(CultureInfo.InvariantCulture);
        if (Plugin.GameConfig.TryGet(option, out float f)) return f.ToString(CultureInfo.InvariantCulture);
        return string.Empty;
    }

    private static string ReadUi(UiConfigOption option)
    {
        if (Plugin.GameConfig.TryGet(option, out uint u)) return u.ToString(CultureInfo.InvariantCulture);
        if (Plugin.GameConfig.TryGet(option, out float f)) return f.ToString(CultureInfo.InvariantCulture);
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

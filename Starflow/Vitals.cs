using System;
using System.Collections.Generic;

namespace Starflow;

// HP, MP, GP, CP and effects for one character. No Dalamud types, so Eval can be tested.
public sealed record Unit
{
    public string Name { get; init; } = string.Empty;
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Mp { get; init; }
    public int MaxMp { get; init; }
    public int Gp { get; init; }
    public int MaxGp { get; init; }
    public int Cp { get; init; }
    public int MaxCp { get; init; }
    public List<string> Effects { get; init; } = [];

    public bool Present => MaxHp > 0;
    public bool Down => Present && Hp <= 0;

    public static readonly Unit None = new();

    public (int Now, int Max) Stat(string stat) => (stat ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "mp" => (Mp, MaxMp),
        "gp" => (Gp, MaxGp),
        "cp" => (Cp, MaxCp),
        _ => (Hp, MaxHp),
    };
}

// One chat line Starflow has seen, newest last.
public sealed record ChatLine(string Channel, string Sender, string Text, double AgeSec, bool Mine);

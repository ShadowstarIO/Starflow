using System;

namespace Starflow;

// The client's weather roll. One weather window is 8 Eorzea hours, 1400 real seconds.
// No Dalamud types, so the roll can be tested.
public static class Forecast
{
    public const long WindowSeconds = 1400;

    public static long WindowStart(long unixSeconds) => unixSeconds - unixSeconds % WindowSeconds;

    // 0 to 99. The territory's weather rate table turns this into a weather.
    public static int Chance(long unixSeconds)
    {
        var bell = unixSeconds / 175;
        var increment = (uint)((bell + 8 - bell % 8) % 24);
        var days = (uint)(unixSeconds / 4200);
        var seed = days * 100 + increment;
        var step1 = (seed << 11) ^ seed;
        var step2 = (step1 >> 8) ^ step1;
        return (int)(step2 % 100);
    }

    // Rates are the cumulative cut-offs, as the WeatherRate sheet stores them. Returns the slot index, or -1.
    public static int Pick(int chance, ReadOnlySpan<byte> rates)
    {
        for (var i = 0; i < rates.Length; i++)
        {
            if (chance < rates[i]) return i;
        }
        return -1;
    }
}

using System;
using System.Globalization;

namespace Starflow;

public enum Residence
{
    None = 0,
    House = 1,
    Apartment = 2,
    Chamber = 3,
}

// Where the client says the player is. Plot is 1-based and only set on a house or a chamber's house.
// Room is the apartment number or the chamber number. Subdivision is the ward's second half, not a plot.
public readonly record struct Address(Residence Kind, string District, int Ward, int Plot, int Room, bool Subdivision)
{
    public static readonly Address None = new(Residence.None, string.Empty, 0, 0, 0, false);

    public bool InWard => Ward is >= 1 and <= 30;

    public string Line()
    {
        if (!InWard) return "not in a housing ward";
        var place = District.Length == 0 ? "ward" : District;
        var sub = Subdivision ? " sub" : string.Empty;
        return Kind switch
        {
            Residence.House => $"{place}{sub} W{Ward} P{Plot}",
            Residence.Apartment => $"{place}{sub} W{Ward} apt" + (Room > 0 ? $" {Room}" : string.Empty),
            Residence.Chamber => $"{place}{sub} W{Ward} P{Plot} room" + (Room > 0 ? $" {Room}" : string.Empty),
            _ => $"{place}{sub} W{Ward} outside",
        };
    }
}

// Spec text: Kind|District|Ward|Plot|Room|sub. Zero or blank means any.
// Kind is House, Apartment, Chamber, or Any. "Apartment|Mist|5|12" reads 12 as the room, as AndThen and StatusShift wrote it.
public static class AddressSpec
{
    public static readonly string[] Districts = ["Mist", "The Lavender Beds", "The Goblet", "Shirogane", "Empyreum"];
    public static readonly string[] Kinds = ["Any", "House", "Apartment", "Chamber"];

    public static string Encode(string kind, string district, int ward, int plot, int room, bool sub)
    {
        var k = Kind(kind);
        if (k is "House" or "Chamber" && plot > 0) sub = plot > 30;
        var tail = sub ? "|sub" : string.Empty;
        return k switch
        {
            "Apartment" => $"Apartment|{district}|{ward}|{room}{tail}",
            "Chamber" => $"Chamber|{district}|{ward}|{plot}|{room}{tail}",
            "Any" => $"Any|{district}|{ward}{tail}",
            _ => $"House|{district}|{ward}|{plot}{tail}",
        };
    }

    public static bool TryParse(string spec, out string kind, out string district, out int ward, out int plot, out int room, out bool sub)
    {
        kind = "Any";
        district = string.Empty;
        ward = plot = room = 0;
        sub = false;
        var text = (spec ?? string.Empty).Trim();
        if (text.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;

        var parts = text.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;
        kind = Kind(parts[0]);
        if (kind.Length == 0) return false;
        district = Canon(parts[1]);
        sub = parts[^1].Equals("sub", StringComparison.OrdinalIgnoreCase);
        var count = sub ? parts.Length - 1 : parts.Length;
        int Part(int i) => i < count && int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        ward = Part(2);
        switch (kind)
        {
            case "Apartment": room = Part(3); break;
            case "Chamber": plot = Part(3); room = Part(4); break;
            case "House": plot = Part(3); break;
        }
        return true;
    }

    public static bool Matches(string spec, Address here)
    {
        if (!here.InWard) return false;
        if (!TryParse(spec, out var kind, out var district, out var ward, out var plot, out var room, out var sub)) return false;
        if ((spec ?? string.Empty).Trim().Equals("any", StringComparison.OrdinalIgnoreCase)) return true;
        switch (kind)
        {
            case "House" when here.Kind != Residence.House: return false;
            case "Apartment" when here.Kind != Residence.Apartment: return false;
            case "Chamber" when here.Kind != Residence.Chamber: return false;
        }
        if (district.Length > 0 && !Canon(here.District).Equals(district, StringComparison.OrdinalIgnoreCase)) return false;
        if (ward > 0 && here.Ward != ward) return false;
        // Plots 31 to 60 are the subdivision, so a plot already says which half it is.
        if (ward > 0 && plot == 0 && here.Subdivision != sub) return false;
        if (plot > 0 && here.Plot != plot) return false;
        if (room > 0 && here.Room != room) return false;
        return true;
    }

    public static string Label(string spec)
    {
        if (!TryParse(spec, out var kind, out var district, out var ward, out var plot, out var room, out var sub))
            return "housing " + spec;
        if ((spec ?? string.Empty).Trim().Equals("any", StringComparison.OrdinalIgnoreCase)) return "housing any";
        var place = district.Length == 0 ? "any district" : district;
        var w = ward > 0 ? $" W{ward}" + (sub ? " sub" : string.Empty) : string.Empty;
        var p = plot > 0 ? $" P{plot}" : string.Empty;
        var r = room > 0 ? $" #{room}" : string.Empty;
        return kind switch
        {
            "Apartment" => $"apartment {place}{w}{r}",
            "Chamber" => $"chamber {place}{w}{p}{r}",
            "House" => $"house {place}{w}{p}",
            _ => $"housing {place}{w}",
        };
    }

    public static string Canon(string value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) return string.Empty;
        if (Has(v, "Mist") || Has(v, "Topmast")) return "Mist";
        if (Has(v, "Lavender") || Has(v, "Lily Hills")) return "The Lavender Beds";
        if (Has(v, "Goblet") || Has(v, "Sultana")) return "The Goblet";
        if (Has(v, "Shirogane") || Has(v, "Kobai")) return "Shirogane";
        if (Has(v, "Empyreum") || Has(v, "Ingleside")) return "Empyreum";
        return v;
    }

    private static string Kind(string text) => (text ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "house" => "House",
        "apartment" or "apt" => "Apartment",
        "chamber" or "fcapartment" or "fc apartment" or "room" => "Chamber",
        "any" or "" => "Any",
        _ => string.Empty,
    };

    private static bool Has(string hay, string needle) => hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Starflow;

// Reads the address the client reports. Ward and plot come from HousingManager; the territory
// says which kind of room it is. A wing is an apartment building, never a plot.
internal static class HousingReader
{
    public static Address Read(uint territory)
    {
        try
        {
            unsafe
            {
                var manager = HousingManager.Instance();
                return manager == null ? Address.None : Read(manager, territory);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Housing read failed");
            return Address.None;
        }
    }

    private static unsafe Address Read(HousingManager* manager, uint territory)
    {
        var house = manager->GetCurrentHouseId();
        int wardIndex = manager->GetCurrentWard();
        var ward = wardIndex >= 0 ? wardIndex + 1 : house.Id != 0 ? house.WardIndex + 1 : 0;
        if (ward is < 1 or > 30) return Address.None;

        var district = District(territory);
        if (district.Length == 0) district = District(HousingManager.GetOriginalHouseTerritoryTypeId());

        int plotIndex = manager->GetCurrentPlot();
        int roomIndex = manager->GetCurrentRoom();
        var inside = manager->IsInside();
        var division = manager->GetCurrentDivision();
        var sub = division == 2 || plotIndex == -127;
        if (inside && house.Id != 0 && house.IsApartment) sub = house.ApartmentDivision == 1;
        var room = roomIndex is > 0 and < 0x3FF ? roomIndex : 0;

        if (Wing(territory) || (inside && house.Id != 0 && house.IsApartment))
            return new Address(Residence.Apartment, district, ward, 0, room, sub);

        if (plotIndex is < 0 or > 59)
            return new Address(Residence.None, district, ward, 0, 0, sub);

        var plot = plotIndex + 1;
        var workshop = Workshop(territory) || manager->IsInWorkshop() || roomIndex == 0x3FF;
        if (Chamber(territory) && !workshop)
            return new Address(Residence.Chamber, district, ward, plot, room, sub);

        return new Address(Residence.House, district, ward, plot, 0, sub);
    }

    private static string District(uint id) => id switch
    {
        339 or 282 or 283 or 284 or 384 or 423 or 573 or 608 => "Mist",
        340 or 342 or 343 or 344 or 385 or 425 or 574 or 609 => "The Lavender Beds",
        341 or 345 or 346 or 347 or 386 or 424 or 575 or 610 => "The Goblet",
        641 or 649 or 650 or 651 or 652 or 653 or 654 or 655 => "Shirogane",
        979 or 980 or 981 or 982 or 983 or 984 or 985 or 999 => "Empyreum",
        _ => string.Empty,
    };

    private static bool Wing(uint id) => id is 573 or 574 or 575 or 608 or 609 or 610 or 654 or 655 or 985 or 999;

    private static bool Chamber(uint id) => id is 384 or 385 or 386 or 652 or 983;

    private static bool Workshop(uint id) => id is 423 or 424 or 425 or 653 or 984;
}

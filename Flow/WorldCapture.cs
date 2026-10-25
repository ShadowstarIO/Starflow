using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using GameCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Flow;

public sealed partial class World
{
    public static World Capture()
    {
        try
        {
            return CaptureCore();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Snapshot failed");
            return new World();
        }
    }

    private static World CaptureCore()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        var logged = Plugin.ClientState.IsLoggedIn && player is not null;
        var job = player?.ClassJob.ValueNullable;
        var world = player?.CurrentWorld.ValueNullable;
        var home = player?.HomeWorld.ValueNullable;
        uint territoryId = Plugin.ClientState.TerritoryType;
        var territory = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        var intended = territory?.TerritoryIntendedUse.RowId ?? 0;
        var party = Plugin.PartyList.Length;
        var now = DateTime.Now;
        var eorzeaSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 144 / 7 / 1000;
        var eorzeaHour = (int)(eorzeaSeconds / 3600 % 24);
        var eorzeaMinute = (int)(eorzeaSeconds / 60 % 60);

        var nearby = 0;
        var names = new List<string>();
        if (player is not null)
        {
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj.ObjectKind != ObjectKind.Pc || obj.EntityId == player.EntityId) continue;
                var d = obj.Position - player.Position;
                if (d.LengthSquared() > 30f * 30f) continue;
                nearby++;
                var name = obj.Name.TextValue;
                if (name.Length > 0) names.Add(name);
            }
        }

        var places = PlacesOf(intended);
        var states = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, bool on)
        {
            if (on) states.Add(name);
        }

        Add("InCombat", Flag(ConditionFlag.InCombat));
        Add("InDuty", Duty());
        Add("DutyReady", Flag(ConditionFlag.WaitingForDuty) || Flag(ConditionFlag.WaitingForDutyFinder));
        Add("InQueue", Flag(ConditionFlag.InDutyQueue));
        Add("Cutscene", Flag(ConditionFlag.WatchingCutscene) || Flag(ConditionFlag.WatchingCutscene78) || Flag(ConditionFlag.OccupiedInCutSceneEvent));
        Add("Mounted", Flag(ConditionFlag.Mounted) || Flag(ConditionFlag.RidingPillion));
        Add("Flying", Flag(ConditionFlag.InFlight));
        Add("Swimming", Flag(ConditionFlag.Swimming));
        Add("Diving", Flag(ConditionFlag.Diving));
        Add("Crafting", Flag(ConditionFlag.Crafting) || Flag(ConditionFlag.ExecutingCraftingAction) || Flag(ConditionFlag.PreparingToCraft));
        Add("Gathering", Flag(ConditionFlag.Gathering) || Flag(ConditionFlag.ExecutingGatheringAction));
        Add("Dead", Flag(ConditionFlag.Unconscious));
        Add("Occupied", Flag(ConditionFlag.Occupied) || Flag(ConditionFlag.Occupied30) || Flag(ConditionFlag.Occupied33) || Flag(ConditionFlag.Occupied38) || Flag(ConditionFlag.Occupied39));
        Add("BetweenAreas", Flag(ConditionFlag.BetweenAreas) || Flag(ConditionFlag.BetweenAreas51));
        Add("Casting", Flag(ConditionFlag.Casting) || Flag(ConditionFlag.Casting87));
        Add("Fishing", Flag(ConditionFlag.Fishing));
        Add("PvP", Flag(ConditionFlag.PvPDisplayActive) || places.Contains("PvP"));
        Add("Housing", Flag(ConditionFlag.UsingHousingFunctions) || places.Contains("Housing"));
        Add("WeaponDrawn", player?.StatusFlags.HasFlag(StatusFlags.WeaponOut) == true);
        Add("InParty", party > 1);
        Add("HasTarget", Plugin.TargetManager.Target is not null);
        Add("Emoting", Flag(ConditionFlag.Emoting));
        Add("Performing", Flag(ConditionFlag.Performing));
        Add("Trade", Flag(ConditionFlag.TradeOpen));
        Add("RolePlaying", Flag(ConditionFlag.RolePlaying));
        Add("Sitting", Flag(ConditionFlag.InThatPosition));
        Add("Talking", Flag(ConditionFlag.OccupiedInEvent) || Flag(ConditionFlag.OccupiedInQuestEvent));
        Add("DeepDungeon", Flag(ConditionFlag.InDeepDungeon) || places.Contains("DeepDungeon"));
        foreach (var place in places) states.Add(place);

        var (weatherId, weather) = ReadWeather();
        var target = Plugin.TargetManager.Target;

        return new World
        {
            Now = now,
            LoggedIn = logged,
            TerritoryId = territoryId,
            TerritoryName = territory?.PlaceName.ValueNullable?.Name.ToString() ?? territoryId.ToString(),
            RegionName = territory?.PlaceNameRegion.ValueNullable?.Name.ToString() ?? string.Empty,
            WorldName = world?.Name.ToString() ?? string.Empty,
            DataCenterName = world?.DataCenter.ValueNullable?.Name.ToString() ?? string.Empty,
            HomeWorldName = home?.Name.ToString() ?? string.Empty,
            JobAbbr = job?.Abbreviation.ToString() ?? string.Empty,
            Role = RoleOf(job?.Abbreviation.ToString() ?? string.Empty, job?.Role ?? 0),
            Level = player?.Level ?? 0,
            PartySize = party <= 0 ? (logged ? 1 : 0) : party,
            Alliance = Plugin.PartyList.IsAlliance,
            Nearby = nearby,
            NearbyNames = names,
            LocalHour = now.Hour,
            LocalMinute = now.Minute,
            EorzeaHour = eorzeaHour,
            EorzeaMinute = eorzeaMinute,
            WeatherId = weatherId,
            Weather = weather,
            Mount = ReadMount(player?.Address ?? IntPtr.Zero),
            Housing = HousingReader.Read(territoryId),
            OnlineStatus = player?.OnlineStatus.ValueNullable?.Name.ToString() ?? string.Empty,
            Target = TargetOf(),
            TargetName = target?.Name.TextValue ?? string.Empty,
            States = states,
            Places = places,
        };
    }

    private static bool Flag(ConditionFlag flag) => Plugin.Condition[flag];

    private static bool Duty() =>
        (Flag(ConditionFlag.BoundByDuty) || Flag(ConditionFlag.BoundByDuty56) || Flag(ConditionFlag.BoundByDuty95))
        && !Flag(ConditionFlag.BetweenAreas)
        && !Flag(ConditionFlag.BetweenAreas51)
        && !Flag(ConditionFlag.OccupiedInCutSceneEvent);

    private static (uint Id, string Name) ReadWeather()
    {
        try
        {
            unsafe
            {
                var manager = WeatherManager.Instance();
                if (manager == null) return (0, string.Empty);
                uint id = manager->GetCurrentWeather();
                var row = Plugin.DataManager.GetExcelSheet<Weather>().GetRowOrDefault(id);
                var name = row?.Name.ToString() ?? string.Empty;
                return (id, name.Length > 0 ? name : id == 0 ? string.Empty : id.ToString());
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Weather read failed");
            return (0, string.Empty);
        }
    }

    private static string ReadMount(IntPtr address)
    {
        if (address == IntPtr.Zero || !Flag(ConditionFlag.Mounted)) return string.Empty;
        try
        {
            unsafe
            {
                var character = (GameCharacter*)address;
                uint id = character->Mount.MountId;
                if (id == 0) return string.Empty;
                var row = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Mount>().GetRowOrDefault(id);
                var name = row?.Singular.ToString() ?? string.Empty;
                return name.Length > 0 ? name : id.ToString();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Mount read failed");
            return string.Empty;
        }
    }

    private static string TargetOf()
    {
        var t = Plugin.TargetManager.Target;
        if (t is null) return "none";
        return t.ObjectKind switch
        {
            ObjectKind.Pc => "player",
            ObjectKind.BattleNpc or ObjectKind.EventNpc or ObjectKind.Companion or ObjectKind.Retainer => "npc",
            _ => "any",
        };
    }

    private static string RoleOf(string abbr, int role)
    {
        if (abbr is "CRP" or "BSM" or "ARM" or "GSM" or "LTW" or "WVR" or "ALC" or "CUL") return "Crafter";
        if (abbr is "MIN" or "BTN" or "FSH") return "Gatherer";
        return role switch
        {
            1 => "Tank",
            4 => "Healer",
            2 or 3 => "DPS",
            _ => string.Empty,
        };
    }

    private static HashSet<string> PlacesOf(uint use)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        switch (use)
        {
            case 0: set.Add("Town"); set.Add("Sanctuary"); break;
            case 1: set.Add("Overworld"); break;
            case 2: set.Add("Inn"); set.Add("Indoor"); set.Add("Sanctuary"); break;
            case 3 or 4 or 7 or 57 or 58: set.Add("Dungeon"); break;
            case 8: set.Add("Alliance"); break;
            case 10: set.Add("Trial"); break;
            case 13: set.Add("Housing"); set.Add("Sanctuary"); break;
            case 14: set.Add("Housing"); set.Add("Indoor"); set.Add("Sanctuary"); break;
            case 16 or 17 or 36: set.Add("Raid"); break;
            case 18 or 28 or 37: set.Add("PvP"); break;
            case 23: set.Add("GoldSaucer"); break;
            case 31: set.Add("DeepDungeon"); break;
            case 41 or 47 or 48 or 60 or 61: set.Add("Field"); break;
            case 49: set.Add("Sanctuary"); break;
        }
        return set;
    }
}

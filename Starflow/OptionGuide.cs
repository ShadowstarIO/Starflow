using System;
using System.Collections.Generic;
using System.Text;

namespace Starflow;

// Plain descriptions for the game's System and UI options. Hand written for the common ones,
// built from the name for the rest. No Dalamud types here, so it can be tested.
public static class OptionGuide
{
    public sealed record Entry(string Group, string Text, bool Internal = false);

    private static readonly Dictionary<string, Entry> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Display
        ["ScreenMode"] = new("Display", "0 windowed, 1 full screen, 2 borderless window. Saved to the config only; Starflow does not move the live window."),
        ["ScreenWidth"] = new("Display", "Windowed width in pixels. Config only; the live window is not resized."),
        ["ScreenHeight"] = new("Display", "Windowed height in pixels. Config only; the live window is not resized."),
        ["FullScreenWidth"] = new("Display", "Full screen width in pixels. Config only."),
        ["FullScreenHeight"] = new("Display", "Full screen height in pixels. Config only."),
        ["ScreenLeft"] = new("Display", "Window position, left edge. Config only."),
        ["ScreenTop"] = new("Display", "Window position, top edge. Config only."),
        ["Refreshrate"] = new("Display", "Full screen refresh rate."),
        ["Fps"] = new("Display", "Frame rate cap. 0 none, 1 refresh rate, 2 half the refresh rate, 3 a quarter."),
        ["FPSInActive"] = new("Display", "1 limits the frame rate while the game window is in the background."),
        ["FPSDownAFK"] = new("Display", "1 lowers the frame rate while you are away from keyboard."),
        ["Gamma"] = new("Display", "Brightness (gamma), 0 to 100."),
        ["UiBaseScale"] = new("Display", "Size of the whole HUD and menus."),
        ["UiHighScale"] = new("Display", "UI resolution: standard or high (for 4K and similar)."),
        ["UiSystemEnlarge"] = new("Display", "Enlarges system text and menus."),
        ["WindowDispNum"] = new("Display", "Which monitor the game opens on."),
        ["MainAdapter"] = new("Display", "Graphics adapter in use.", true),
        ["ColorThemeType"] = new("Display", "HUD colour theme: dark, light, classic FF, clear blue."),
        ["UiAssetType"] = new("Display", "HUD style: standard or legacy."),
        ["HardwareCursorSize"] = new("Display", "Mouse cursor size."),
        ["AccessibilityColorBlindFilterEnable"] = new("Display", "1 turns on the colour vision filter."),
        ["AccessibilityColorBlindFilterType"] = new("Display", "Colour vision filter type."),
        ["AccessibilityColorBlindFilterStrength"] = new("Display", "Colour vision filter strength."),
        ["AccessibilitySoundVisualEnable"] = new("Display", "1 shows on-screen markers for important sounds."),

        // Graphics
        ["GraphicsRezoScale"] = new("Graphics", "3D resolution scale, 50 to 100 percent."),
        ["GraphicsRezoUpscaleType"] = new("Graphics", "Upscaler: 0 AMD FSR, 1 NVIDIA DLSS."),
        ["DynamicRezoType"] = new("Graphics", "Dynamic resolution. 0 off, 1 when the frame rate drops below the threshold, 2 always."),
        ["DynamicRezoThreshold"] = new("Graphics", "Frame rate below which dynamic resolution starts."),
        ["DynamicRezoEnableCutScene"] = new("Graphics", "1 allows dynamic resolution in cutscenes."),
        ["GeneralQuality"] = new("Graphics", "The graphics preset. Changing it alone does not change each option."),
        ["DisplayObjectLimitType"] = new("Graphics", "How many characters are drawn around you. 0 maximum, 4 minimum. Big effect on busy towns."),
        ["DisplayObjectLimitType2"] = new("Graphics", "Second character-limit setting, used alongside the first."),
        ["DisplayHouseIndoorPcLimitType"] = new("Graphics", "Character limit inside houses and rooms."),
        ["LodType"] = new("Graphics", "Level of detail on distant objects. 1 on lowers detail far away. Writes the _DX11 twin too."),
        ["GrassQuality"] = new("Graphics", "Grass quality. 0 off up to 3 high. Writes the _DX11 twin too."),
        ["GrassEnableDynamicInterference"] = new("Graphics", "1 lets grass bend around characters."),
        ["SSAO"] = new("Graphics", "Ambient occlusion (soft contact shadows). 0 off. Writes the _DX11 twin too."),
        ["Glare"] = new("Graphics", "Glare and bloom. 0 off. Writes the _DX11 twin too."),
        ["DepthOfField"] = new("Graphics", "Depth of field blur. 1 on. Writes the _DX11 twin too."),
        ["RadialBlur"] = new("Graphics", "Radial blur on fast motion and some effects. Writes the _DX11 twin too."),
        ["Vignetting"] = new("Graphics", "Darkened screen edges. Writes the _DX11 twin too."),
        ["DistortionWater"] = new("Graphics", "Water refraction. Writes the _DX11 twin too."),
        ["WaterWet"] = new("Graphics", "Wet surfaces after rain and swimming. Writes the _DX11 twin too."),
        ["TranslucentQuality"] = new("Graphics", "Quality of transparent things like hair and glass. Writes the _DX11 twin too."),
        ["AntiAliasing"] = new("Graphics", "Edge smoothing. 0 off, 1 FXAA, 2 TSCMAA. Writes the _DX11 twin too."),
        ["TextureFilterQuality"] = new("Graphics", "Texture filtering. Writes the _DX11 twin too."),
        ["TextureAnisotropicQuality"] = new("Graphics", "Anisotropic filtering, 0 to 3 (x4 to x16). Writes the _DX11 twin too."),
        ["TextureRezoType"] = new("Graphics", "Texture resolution."),
        ["ParallaxOcclusion"] = new("Graphics", "Parallax occlusion (depth on flat surfaces)."),
        ["Tessellation"] = new("Graphics", "Tessellation (rounder shapes up close)."),
        ["GlareRepresentation"] = new("Graphics", "Glare style."),
        ["ReflectionType"] = new("Graphics", "Real-time reflections. 0 off. Writes the _DX11 twin too."),
        ["OcclusionCulling"] = new("Graphics", "Skips drawing objects hidden behind others. Writes the _DX11 twin too."),
        ["StreamingType"] = new("Graphics", "Streams distant terrain at lower detail."),
        ["MapResolution"] = new("Graphics", "Resolution of the map textures. Writes the _DX11 twin too."),
        ["CharaLight"] = new("Graphics", "Character lighting. Brightens characters in dark areas."),
        ["BattleEffect"] = new("Graphics", "General battle effect detail."),
        ["BGEffect"] = new("Graphics", "Background effects."),
        ["FootEffect"] = new("Graphics", "Footprints and footstep effects."),
        ["DynamicAroundRangeMode"] = new("Graphics", "Draw distance mode for surroundings."),
        ["ShadowVisibilityType"] = new("Graphics", "Shadow display (general)."),
        ["ShadowVisibilityTypeSelf"] = new("Graphics", "Your own shadow. 1 shown. Writes the _DX11 twin too."),
        ["ShadowVisibilityTypeParty"] = new("Graphics", "Party members' shadows. 1 shown. Writes the _DX11 twin too."),
        ["ShadowVisibilityTypeOther"] = new("Graphics", "Other players' shadows. 1 shown. Writes the _DX11 twin too."),
        ["ShadowVisibilityTypeEnemy"] = new("Graphics", "Enemy shadows. 1 shown. Writes the _DX11 twin too."),
        ["ShadowLOD"] = new("Graphics", "Lower shadow detail far away. Writes the _DX11 twin too."),
        ["ShadowBgLOD"] = new("Graphics", "Lower background shadow detail far away."),
        ["ShadowTextureSizeType"] = new("Graphics", "Shadow resolution. Writes the _DX11 twin too."),
        ["ShadowCascadeCountType"] = new("Graphics", "Shadow cascading (sharpness with distance). Writes the _DX11 twin too."),
        ["ShadowSoftShadowType"] = new("Graphics", "Soft shadow edges. Writes the _DX11 twin too."),
        ["ShadowLightValidType"] = new("Graphics", "Shadows cast by light sources other than the sun."),
        ["PhysicsType"] = new("Graphics", "Cloth and hair physics (general)."),
        ["PhysicsTypeSelf"] = new("Graphics", "Your own cloth and hair physics. Writes the _DX11 twin too."),
        ["PhysicsTypeParty"] = new("Graphics", "Party members' physics. Writes the _DX11 twin too."),
        ["PhysicsTypeOther"] = new("Graphics", "Other players' physics. Writes the _DX11 twin too."),
        ["PhysicsTypeEnemy"] = new("Graphics", "Enemy physics. Writes the _DX11 twin too."),
        ["ScreenShotImageType"] = new("Graphics", "Screenshot file type: JPG or PNG."),
        ["ScreenShotDir"] = new("Graphics", "Screenshot folder.", true),

        // Sound
        ["SoundMaster"] = new("Sound", "Master volume, 0 to 100."),
        ["SoundBgm"] = new("Sound", "Music volume, 0 to 100."),
        ["SoundSe"] = new("Sound", "Sound effects volume, 0 to 100."),
        ["SoundVoice"] = new("Sound", "Voice volume, 0 to 100."),
        ["SoundEnv"] = new("Sound", "Ambient sound volume, 0 to 100."),
        ["SoundSystem"] = new("Sound", "System sounds volume, 0 to 100."),
        ["SoundPerform"] = new("Sound", "Performance (instrument) volume, 0 to 100."),
        ["SoundPlayer"] = new("Sound", "Your own effects volume, 0 to 100."),
        ["SoundParty"] = new("Sound", "Party members' effects volume, 0 to 100."),
        ["SoundOther"] = new("Sound", "Other players' effects volume, 0 to 100."),
        ["SoundPad"] = new("Sound", "Gamepad speaker volume."),
        ["IsSoundDisable"] = new("Sound", "1 turns all sound off."),
        ["IsSoundAlways"] = new("Sound", "1 keeps sound playing while the game is in the background."),
        ["SoundChocobo"] = new("Sound", "1 plays the chocobo music while mounted on a chocobo."),
        ["SoundFieldBattle"] = new("Sound", "1 plays battle music in the open world."),
        ["SoundCfTimeCount"] = new("Sound", "1 plays the duty-ready sound."),
        ["SoundHousing"] = new("Sound", "1 plays housing music."),
        ["SoundDolby"] = new("Sound", "Dolby Atmos output."),
        ["SoundMicpos"] = new("Sound", "Where sound is heard from: the camera or your character."),
        ["SoundEqualizerType"] = new("Sound", "Equalizer preset."),
        ["Is3DAudio"] = new("Sound", "1 turns on 3D audio."),
        ["IsEmoteSe"] = new("Sound", "1 plays emote sounds."),
        ["EmoteSeType"] = new("Sound", "Whose emote sounds play: everyone's, or only yours and your party's."),

        // Controls and camera
        ["MouseSpeed"] = new("Controls", "Camera speed with the mouse (decimal)."),
        ["CameraZoom"] = new("Controls", "Camera zoom speed."),
        ["TurnSpeed"] = new("Controls", "Character turn speed with the keyboard."),
        ["MouseAutoFocus"] = new("Controls", "Focuses the window when the mouse is over it."),
        ["MouseOpeLimit"] = new("Controls", "1 keeps the cursor inside the game window."),
        ["AutoChangeCameraMode"] = new("Controls", "Changes camera mode automatically."),
        ["AutoAfkSwitchingTime"] = new("Controls", "Minutes before you are marked away from keyboard."),
        ["IdlingCameraAFK"] = new("Controls", "1 starts the idle camera while away."),
        ["IdlingCameraSwitchType"] = new("Controls", "Idle camera style."),
        ["PadMode"] = new("Controls", "Gamepad mode."),
        ["PadAvailable"] = new("Controls", "1 enables the gamepad."),
        ["PadReverseConfirmCancel"] = new("Controls", "Swaps the gamepad confirm and cancel buttons."),
        ["PadSelectButtonIcon"] = new("Controls", "Button icon style: Xbox or PlayStation."),
        ["WeaponAutoPutAway"] = new("Controls", "1 sheathes your weapon automatically after combat."),
        ["WeaponAutoPutAwayTime"] = new("Controls", "Seconds before the weapon is put away."),
        ["LipMotionType"] = new("Controls", "Lip sync on voiced lines."),
        ["CameraProductionOfAction"] = new("Controls", "Camera movement during some actions."),
        ["LegacyCameraType"] = new("Controls", "1 uses the legacy movement camera."),
        ["EventCameraAutoControl"] = new("Controls", "Automatic camera during events."),
        ["IdleEmoteTime"] = new("Controls", "Seconds before idle emotes play."),
        ["IdleEmoteRandomType"] = new("Controls", "Random idle emotes."),
        ["CutsceneSkipIsShip"] = new("Controls", "Skip the airship and ferry cutscenes."),
        ["CutsceneSkipIsContents"] = new("Controls", "Skip cutscenes you have seen in duties."),
        ["CutsceneSkipIsHousing"] = new("Controls", "Skip the housing entrance cutscene."),
        ["CutsceneMovieVoice"] = new("Controls", "Cutscene voice language."),
        ["CutsceneMovieCaption"] = new("Controls", "Cutscene subtitles."),
        ["CutsceneMovieOpening"] = new("Controls", "Plays the opening movie at the title screen."),
        ["EnableMoveTiltCharacter"] = new("Controls", "Your character leans when turning."),
        ["EnableMoveTiltMountGround"] = new("Controls", "Ground mounts lean when turning."),
        ["EnableMoveTiltMountFly"] = new("Controls", "Flying mounts lean when turning."),
        ["AutoNearestTarget"] = new("Controls", "Targets the nearest enemy when you use an action with no target."),
        ["AutoNearestTargetType"] = new("Controls", "How the nearest target is chosen."),
        ["PetTargetOffInCombat"] = new("Controls", "1 stops pets and minions being targeted in combat."),
        ["TargetEnableMouseOverSelect"] = new("Controls", "Mouse-over targeting for actions."),
        ["RightClickExclusionPC"] = new("Controls", "Right click ignores other players."),
        ["RightClickExclusionBNPC"] = new("Controls", "Right click ignores battle NPCs."),
        ["RightClickExclusionMinion"] = new("Controls", "Right click ignores minions."),
        ["TextPasteEnable"] = new("Controls", "1 allows pasting text into the chat box."),

        // HUD and display
        ["ActiveInfo"] = new("HUD", "Shows the active help window."),
        ["PartyList"] = new("HUD", "Shows the party list."),
        ["PartyListStatus"] = new("HUD", "Shows status effects on the party list."),
        ["PartyListStatusTimer"] = new("HUD", "Shows status timers on the party list."),
        ["EnemyList"] = new("HUD", "Shows the enemy list."),
        ["EnemyListCastbarEnable"] = new("HUD", "Shows cast bars on the enemy list."),
        ["TargetInfo"] = new("HUD", "Shows the target information bar."),
        ["Gil"] = new("HUD", "Shows your gil on the HUD."),
        ["DTR"] = new("HUD", "Shows the server info bar (top right)."),
        ["PlayerInfo"] = new("HUD", "Shows the parameter bar."),
        ["NaviMap"] = new("HUD", "Shows the minimap."),
        ["Help"] = new("HUD", "Shows help tips."),
        ["BuffDispType"] = new("HUD", "How status effects are drawn."),
        ["RatioHpDisp"] = new("HUD", "Shows HP as a percentage."),
        ["FlyTextDispSize"] = new("HUD", "Size of the damage numbers."),
        ["PopUpTextDispSize"] = new("HUD", "Size of pop-up text."),
        ["ToolTipDispSize"] = new("HUD", "Tooltip size."),
        ["MapDispSize"] = new("HUD", "Map window size."),
        ["BattleTalkShowFace"] = new("HUD", "Shows portraits in battle dialogue."),
        ["TimeMode"] = new("HUD", "Which clocks the server info bar shows."),
        ["Time12"] = new("HUD", "1 uses a 12-hour clock."),
        ["TimeEorzea"] = new("HUD", "Shows Eorzea time."),
        ["TimeLocal"] = new("HUD", "Shows local time."),
        ["TimeServer"] = new("HUD", "Shows server time."),
        ["PlateType"] = new("Nameplates", "Nameplate style."),
        ["PlateDispHPBar"] = new("Nameplates", "Shows HP bars on nameplates."),
        ["PlateDisableMaxHPBar"] = new("Nameplates", "Hides the HP bar when HP is full."),
        ["NamePlateDispSize"] = new("Nameplates", "Nameplate size."),
        ["NamePlateDispJobIcon"] = new("Nameplates", "Shows job icons on nameplates."),
        ["NamePlateDispJobIconType"] = new("Nameplates", "Job icon style on nameplates."),
        ["NamePlateSetRoleColor"] = new("Nameplates", "Colours nameplates by role."),
        ["NamePlateDispWorldTravel"] = new("Nameplates", "Shows the home world of visitors."),
        ["NamePlateHpSizeType"] = new("Nameplates", "HP bar size on nameplates."),
        ["TargetNamePlateNameType"] = new("Nameplates", "How your target's name is shown."),
        ["FocusTargetNamePlateNameType"] = new("Nameplates", "How your focus target's name is shown."),
        ["LogNamePlateDispEnemyCast"] = new("Nameplates", "Shows enemy casts on nameplates."),
        ["InfoSettingDispWorldNameType"] = new("Nameplates", "How world names are shown."),

        // Chat
        ["LogChatBubbleEnableChatBubble"] = new("Chat", "Speech bubbles over characters who talk. 1 on."),
        ["LogChatBubbleShowMax"] = new("Chat", "Most speech bubbles shown at once."),
        ["LogChatBubbleShowOwnMessage"] = new("Chat", "Shows a bubble for your own messages."),
        ["LogChatBubbleShowDuringBattle"] = new("Chat", "Shows bubbles during combat."),
        ["LogChatBubbleSizeType"] = new("Chat", "Speech bubble size."),
        ["LogChatBubbleShowQuickChat"] = new("Chat", "Shows quick chat in bubbles."),
        ["LogTimeDisp"] = new("Chat", "Shows time stamps in the chat log."),
        ["LogTimeSettingType"] = new("Chat", "Which clock chat time stamps use."),
        ["LogFontSize"] = new("Chat", "Chat log font size."),
        ["LogPermeationRate"] = new("Chat", "Chat log background transparency."),
        ["LogNameType"] = new("Chat", "How player names appear in chat: full, surname shortened, forename shortened, initials."),
        ["LogCrossWorldName"] = new("Chat", "Shows the home world of visitors in chat."),
        ["LogDispClassJobName"] = new("Chat", "Shows the job next to names in chat."),
        ["LogSetRoleColor"] = new("Chat", "Colours names in chat by role."),
        ["LogItemLinkEnableType"] = new("Chat", "Item links in chat."),
        ["LogEnableErrMsgLv1"] = new("Chat", "Shows error messages in the chat log."),
        ["LogRecastActionErrDisp"] = new("Chat", "Shows the 'not ready yet' message."),
        ["LogFlyingHeightMaxErrDisp"] = new("Chat", "Shows the 'cannot fly higher' message."),
        ["LogTell"] = new("Chat", "Where incoming tells go."),
        ["ChatType"] = new("Chat", "Chat input mode."),
        ["EmoteTextType"] = new("Chat", "Emote messages in the log: shown, or only your own."),

        // Items and shops
        ["ShopConfirm"] = new("Items", "Asks before buying."),
        ["ShopConfirmMateria"] = new("Items", "Asks before selling melded gear."),
        ["ShopConfirmExRare"] = new("Items", "Asks before selling unique items."),
        ["ShopConfirmSpiritBondMax"] = new("Items", "Asks before selling fully spiritbonded gear."),
        ["ShopSell"] = new("Items", "Sell window behaviour."),
        ["ItemInventryStoreEnd"] = new("Items", "Where new items go in the inventory."),
        ["ItemNoArmoryMaskOff"] = new("Items", "Keeps gear in the inventory instead of the armoury chest."),
        ["ItemInventryWindowSizeType"] = new("Items", "Inventory window size."),
        ["ItemInventryRetainerWindowSizeType"] = new("Items", "Retainer inventory window size."),
        ["TelepoTicketUseType"] = new("Items", "Uses Aetheryte Tickets when teleporting."),
        ["TelepoTicketGilSetting"] = new("Items", "Gil cost above which tickets are used."),

        // Duty Finder
        ["ContentsFinderListSortType"] = new("Duty Finder", "Duty list sort order."),
        ["ContentsFinderSupplyEnable"] = new("Duty Finder", "Shows the reward bonus."),
        ["ContentsReplayEnable"] = new("Duty Finder", "Allows duty recordings."),
        ["PartyFinderNewArrivalDisp"] = new("Duty Finder", "Notifies new Party Finder listings."),
        ["DispCCCountDown"] = new("Duty Finder", "Crystalline Conflict countdown."),
        ["PvPFrontlinesGCFree"] = new("Duty Finder", "Frontline team colours not tied to your Grand Company."),

        // Login and guides
        ["RecommendLoginDisp"] = new("Guides", "Shows recommendations at login."),
        ["RecommendAreaChangeDisp"] = new("Guides", "Shows recommendations when you change area."),
        ["PlayGuideLoginDisp"] = new("Guides", "Shows the play guide at login."),
        ["PlayGuideAreaChangeDisp"] = new("Guides", "Shows the play guide when you change area."),
        ["AchievementAppealLoginDisp"] = new("Guides", "Shows achievement notices at login."),
        ["FellowshipShowNewNotice"] = new("Guides", "Shows new fellowship notices."),
        ["HidePcAroundQuestProgressNpc"] = new("Guides", "Hides other players near quest NPCs."),
        ["HidePcAroundQuestProgressNpcIncludeParty"] = new("Guides", "Also hides party members near quest NPCs."),
        ["HidePcAroundNpcAccessingQuest"] = new("Guides", "Hides other players near an NPC you are talking to."),
        ["HidePcAroundNpcAccessingQuestIncludeParty"] = new("Guides", "Also hides party members near that NPC."),

        // Internal
        ["GuidVersion"] = new("Internal", "Game bookkeeping. Do not change.", true),
        ["ConfigVersion"] = new("Internal", "Game bookkeeping. Do not change.", true),
        ["Language"] = new("Internal", "Client language. Do not change here.", true),
        ["Region"] = new("Internal", "Client region. Do not change.", true),
        ["UPnP"] = new("Internal", "Network port mapping. Do not change.", true),
        ["Port"] = new("Internal", "Network port. Do not change.", true),
        ["LastLogin0"] = new("Internal", "Last login record. Do not change.", true),
        ["LastLogin1"] = new("Internal", "Last login record. Do not change.", true),
        ["WorldId"] = new("Internal", "Last world. Do not change.", true),
        ["ServiceIndex"] = new("Internal", "Service account. Do not change.", true),
        ["DktSessionId"] = new("Internal", "Session id. Do not change.", true),
        ["PadGuid"] = new("Internal", "Gamepad id. Do not change.", true),
        ["InstanceGuid"] = new("Internal", "Gamepad id. Do not change.", true),
        ["ProductGuid"] = new("Internal", "Gamepad id. Do not change.", true),
        ["ActiveInstanceGuid"] = new("Internal", "Gamepad id. Do not change.", true),
        ["ActiveProductGuid"] = new("Internal", "Gamepad id. Do not change.", true),
        ["MsqProgress"] = new("Internal", "Story progress record. Do not change.", true),
        ["PromptConfigUpdate"] = new("Internal", "Game bookkeeping. Do not change.", true),
        ["FirstConfigBackup"] = new("Internal", "Game bookkeeping. Do not change.", true),
        ["TitleScreenType"] = new("Internal", "Which title screen is shown.", true),
        ["ActiveLS_H"] = new("Internal", "Active linkshell record. Do not change.", true),
        ["ActiveLS_L"] = new("Internal", "Active linkshell record. Do not change.", true),
        ["Alias"] = new("Internal", "Gamepad alias record. Do not change.", true),
        ["EnablePsFunction"] = new("Internal", "Console-only switch. Do not change.", true),
    };

    // Who a nameplate or bubble option is about, from the end of its name.
    private static readonly (string Suffix, string Who)[] Subjects =
    [
        ("SelfBuddy", "your chocobo"), ("SelfPet", "your pet"), ("Self", "you"),
        ("PartyPet", "party members' pets"), ("PartyBuddy", "party members' chocobos"), ("Party", "party members"),
        ("AlliancePet", "alliance members' pets"), ("Alliance", "alliance members"),
        ("OtherPet", "other players' pets"), ("OtherBuddy", "other players' chocobos"), ("OtherClass", "players with no role"), ("Other", "other players"),
        ("FriendPet", "friends' pets"), ("FriendBuddy", "friends' chocobos"), ("FriendEdge", "friends (outline)"), ("Friend", "friends"),
        ("FeastPet", "Crystalline Conflict pets"), ("Feast", "Crystalline Conflict players"), ("PvPEnemy", "PvP enemies"),
        ("UnengagedEnemy", "enemies not in combat"), ("UnengagedEmemy", "enemies not in combat"),
        ("EngagedEnemy", "enemies fighting you"), ("EngagedEmemy", "enemies fighting you"),
        ("ClaimedEnemy", "enemies claimed by others"), ("ClaimedEmemy", "enemies claimed by others"),
        ("UnclaimedEnemy", "unclaimed enemies"), ("UnclaimedEmemy", "unclaimed enemies"),
        ("HousingFurniture", "housing furniture"), ("HousingField", "housing yard items"),
        ("Npc", "NPCs"), ("Object", "objects"), ("Minion", "minions"),
        ("LimEdge", "Maelstrom members (outline)"), ("Lim", "Maelstrom members"),
        ("GriEdge", "Twin Adder members (outline)"), ("Gri", "Twin Adder members"),
        ("UldEdge", "Immortal Flames members (outline)"), ("Uld", "Immortal Flames members"),
        ("Tank", "tanks"), ("Healer", "healers"), ("Dps", "DPS"), ("DPS", "DPS"),
    ];

    private static readonly (string Key, string Name)[] Channels =
    [
        ("Say", "Say"), ("Yell", "Yell"), ("Shout", "Shout"), ("Tell", "Tell"), ("Party", "Party"), ("Alliance", "Alliance"),
        ("Fc", "Free Company"), ("FCompany", "Free Company"), ("FCAnnounce", "Free Company announcements"),
        ("Pvpteam", "PvP team"), ("PvpTeam", "PvP team"), ("PvPGroupAnnounce", "PvP team announcements"), ("PvPGroup", "PvP team"),
        ("BeginnerAnnounce", "Novice Network announcements"), ("Beginner", "Novice Network"),
        ("Cwls", "cross-world linkshell"), ("CWLS", "cross-world linkshell"), ("Ls", "linkshell"), ("LS", "linkshell"),
        ("EmoteUser", "custom emotes"), ("Emote", "standard emotes"), ("Echo", "Echo"), ("NpcSay", "NPC dialogue"),
        ("SysMsg", "system messages"), ("SysBattle", "battle system messages"), ("SysGathering", "gathering system messages"),
        ("SysErr", "error messages"), ("AttackSuccess", "hits"), ("AttackFailure", "misses"), ("Action", "actions used"),
        ("Item", "items used"), ("CureGive", "healing"), ("BuffGive", "buffs"), ("DebuffGive", "debuffs"),
        ("ItemNotice", "item notices"), ("Growup", "level and progress"), ("Loot", "loot"), ("Craft", "crafting"), ("Gathering", "gathering"),
    ];

    // Game bookkeeping: session and login records, device ids, version stamps. Not settings a person uses,
    // and changing them could upset the client, so Starflow hides them and never writes them.
    public static bool Blocked(string name) =>
        Known.TryGetValue((name ?? string.Empty).Trim(), out var entry) && entry.Internal;

    public static Entry Describe(string name)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0) return new Entry("Other", string.Empty);
        if (Known.TryGetValue(text, out var known)) return known;

        if (text.EndsWith("_DX11", StringComparison.OrdinalIgnoreCase))
        {
            var base_ = text[..^5];
            var inner = Describe(base_);
            return new Entry(inner.Group, $"DirectX 11 copy of {base_}. The game reads this one; Starflow writes both together. {inner.Text}".TrimEnd());
        }

        if (Starts(text, "IsSnd", out var snd)) return new Entry("Sound", $"1 mutes {SoundName(snd)}.");
        if (Starts(text, "IsSound", out var always) && always.EndsWith("Always", StringComparison.Ordinal))
            return new Entry("Sound", $"1 keeps {SoundName(always[..^6])} playing while the game is in the background.");
        if (Starts(text, "IsSoundPad", out _)) return new Entry("Sound", "1 plays sound through the gamepad speaker.");

        if (Starts(text, "NamePlate", out var plate)) return Plate(plate);

        if (Starts(text, "LogChatBubble", out var bubble))
        {
            var (part, channel) = Trailing(bubble, ["ShowType", "FontColor", "WindowColor"]);
            var what = part switch
            {
                "ShowType" => "when speech bubbles show",
                "FontColor" => "speech bubble text colour",
                "WindowColor" => "speech bubble background colour",
                _ => "speech bubble setting",
            };
            return new Entry("Chat", $"{Cap(what)} for {ChannelName(channel)}.");
        }

        if (Starts(text, "IsLog", out var isLog)) return new Entry("Chat", $"Chat log: {ChannelName(isLog)} channel on or off.");
        if (Starts(text, "LogTabName", out var tab)) return new Entry("Chat", $"Name of chat tab {tab}.");
        if (Starts(text, "LogTabFilter", out var filter)) return new Entry("Chat", $"Which messages chat tab {filter} shows.");
        if (Starts(text, "LogFontSize", out var font)) return new Entry("Chat", $"Font size for chat window {Trim(font, "Log")}.");
        if (Starts(text, "LogTimeDisp", out var stamp)) return new Entry("Chat", $"Time stamps for chat window {Trim(stamp, "Log")}.");
        if (Starts(text, "LogPermeationRate", out var see)) return new Entry("Chat", $"Background transparency for chat window {Trim(see, "Log")}.");
        if (Starts(text, "LogColorRole", out var roleColor)) return new Entry("Chat", $"Chat name colour for {roleColor.ToLowerInvariant()} players.");
        if (Starts(text, "LogColorOtherClass", out _)) return new Entry("Chat", "Chat name colour for players with no role.");
        if (Starts(text, "Color", out var color)) return new Entry("Chat", $"Chat text colour for {ChannelName(color)}. A packed colour number.");
        if (Starts(text, "Log", out var log) && Channel(log, out var logChannel)) return new Entry("Chat", $"Chat log: which window {logChannel} goes to.");

        if (Starts(text, "PadButton_", out var button)) return new Entry("Controls", $"What the gamepad {button} button does.");
        if (Starts(text, "TouchPad", out var touch)) return new Entry("Controls", $"Touch pad: {Words(touch)}.");
        if (Starts(text, "MouseWheelOperation", out var wheel)) return new Entry("Controls", $"What the mouse wheel does: {Words(wheel)}.");
        if (Starts(text, "GroundTarget", out var ground)) return new Entry("Controls", $"Ground targeting: {Words(ground)}.");
        if (Starts(text, "FirstPerson", out var fp)) return new Entry("Controls", $"First person camera: {Words(fp)}.");
        if (Starts(text, "ThirdPerson", out var tp)) return new Entry("Controls", $"Third person camera: {Words(tp)}.");
        if (Starts(text, "Lockon", out var lockon)) return new Entry("Controls", $"Lock-on camera: {Words(lockon)}.");
        if (Starts(text, "FPSCamera", out var fpsCam)) return new Entry("Controls", $"First person camera: {Words(fpsCam)}.");
        if (Starts(text, "SystemMouseOperation", out var mouse)) return new Entry("Controls", $"Mouse: {Words(mouse)}.");

        if (Starts(text, "HotbarCommon", out var common)) return new Entry("Hotbars", $"1 shares hotbar {common.TrimStart('0')} across all jobs.");
        if (Starts(text, "HotbarCrossCommon", out var crossCommon)) return new Entry("Hotbars", $"1 shares cross hotbar {crossCommon.TrimStart('0')} across all jobs.");
        if (Starts(text, "HotbarWXHB", out var wxhb)) return new Entry("Hotbars", $"Expanded cross hotbar: {Words(wxhb)}.");
        if (Starts(text, "HotbarXHB", out var xhb)) return new Entry("Hotbars", $"Cross hotbar: {Words(xhb)}.");
        if (Starts(text, "HotbarCross", out var cross)) return new Entry("Hotbars", $"Cross hotbar: {Words(cross)}.");
        if (Starts(text, "Hotbar", out var hotbar)) return new Entry("Hotbars", $"Hotbar: {Words(hotbar)}.");
        if (Starts(text, "ExHotbar", out var ex)) return new Entry("Hotbars", $"Hotbar: {Words(ex)}.");

        if (Starts(text, "PartyListSortType", out var sortRole)) return new Entry("HUD", $"Where {sortRole.ToLowerInvariant()} players sort in the party list.");
        if (Starts(text, "ContentsFinderUseLangType", out var lang)) return new Entry("Duty Finder", $"Duty Finder matches with {LangName(lang)} players.");
        if (Starts(text, "ItemSort", out var sort)) return new Entry("Items", $"Inventory sort: {Words(sort)}.");
        if (Starts(text, "ItemDetail", out var detail)) return new Entry("Items", $"Item tooltip: {Words(detail)}.");
        if (Starts(text, "GPose", out var gpose) || Starts(text, "Gpose", out gpose)) return new Entry("Group pose", $"Group pose: {Words(gpose)}.");
        if (Starts(text, "GroupPose", out var pose)) return new Entry("Group pose", $"Group pose: {Words(pose)}.");
        if (Starts(text, "Accessibility", out var access)) return new Entry("Display", $"Accessibility: {Words(access)}.");
        if (Starts(text, "BattleEffect", out var effect)) return new Entry("Graphics", $"Battle effects for {SubjectName(effect)}. 0 all, 1 limited, 2 none.");
        if (Starts(text, "EgiMirageType", out var egi)) return new Entry("Pets", $"Look of the {egi} egi.");
        if (Starts(text, "PetMirageType", out var pet)) return new Entry("Pets", $"Look of the {Words(pet)}.");
        if (text.EndsWith("Size", StringComparison.Ordinal) && text.Length > 4 && char.IsUpper(text[0]) && !text.Contains("Disp", StringComparison.Ordinal))
            return new Entry("Pets", $"Size of the summoned {Words(text[..^4])}.");
        if (Starts(text, "Banner", out var banner)) return new Entry("HUD", $"Banners: {Words(banner)}.");
        if (Starts(text, "CC", out var cc)) return new Entry("Duty Finder", $"Crystalline Conflict: {Words(cc)}.");
        if (Starts(text, "FriendList", out var friends)) return new Entry("HUD", $"Friend list: {Words(friends)}.");
        if (Starts(text, "LetterList", out var letters)) return new Entry("HUD", $"Letters: {Words(letters)}.");
        if (Starts(text, "LsList", out var ls)) return new Entry("HUD", $"Linkshell list: {Words(ls)}.");
        if (Starts(text, "Telepo", out var tele)) return new Entry("Items", $"Teleport: {Words(tele)}.");
        if (Starts(text, "Pad", out var pad)) return new Entry("Controls", $"Gamepad: {Words(pad)}.");
        if (Starts(text, "Log", out var rest)) return new Entry("Chat", $"Chat log: {Words(rest)}.");
        if (Starts(text, "Sound", out var sound)) return new Entry("Sound", $"{Cap(SoundName(sound))} volume or setting.");

        return new Entry("Other", Cap(Words(text)) + ".");
    }

    // All option names with a description, for the help page.
    public static IEnumerable<(string Name, Entry Entry)> All(IEnumerable<string> names)
    {
        foreach (var name in names) yield return (name, Describe(name));
    }

    private static Entry Plate(string rest)
    {
        foreach (var (prefix, what) in new[]
        {
            ("NameTitleType", "how titles show"), ("DispType", "when nameplates show"), ("NameType", "how names show"),
            ("HpType", "HP bar"), ("Color", "nameplate text colour"), ("Edge", "nameplate outline colour"),
            ("DispJobIconIn", "job icons"), ("Disp", "display"),
        })
        {
            if (!rest.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var subject = rest[prefix.Length..];
            if (prefix == "DispJobIconIn")
            {
                var inside = subject.StartsWith("Instance", StringComparison.Ordinal) ? "in duties" : "in the open world";
                var who = subject.EndsWith("Party", StringComparison.Ordinal) ? "party members" : "others";
                return new Entry("Nameplates", $"Job icons on nameplates {inside}, for {who}.");
            }
            return new Entry("Nameplates", $"{Cap(what)} for {SubjectName(subject)}.");
        }
        return new Entry("Nameplates", $"Nameplates: {Words(rest)}.");
    }

    private static string SubjectName(string subject)
    {
        foreach (var (suffix, who) in Subjects)
        {
            if (subject.Equals(suffix, StringComparison.Ordinal)) return who;
        }
        return subject.Length == 0 ? "everyone" : Words(subject);
    }

    private static bool Channel(string key, out string name)
    {
        foreach (var (k, n) in Channels)
        {
            if (!key.StartsWith(k, StringComparison.Ordinal)) continue;
            var number = key[k.Length..];
            if (number.Length > 0 && !int.TryParse(number, out _)) continue;
            name = number.Length > 0 ? $"{n} {number}" : n;
            return true;
        }
        name = string.Empty;
        return false;
    }

    private static string ChannelName(string key) => Channel(key, out var name) ? name : Words(key);

    private static string SoundName(string key) => key switch
    {
        "Master" => "all sound",
        "Bgm" => "music",
        "Se" => "sound effects",
        "Voice" => "voices",
        "Env" => "ambient sound",
        "System" => "system sounds",
        "Perform" => "performance",
        "" => "sound",
        _ => Words(key),
    };

    private static string LangName(string code) => code switch
    {
        "JA" => "Japanese",
        "EN" => "English",
        "DE" => "German",
        "FR" => "French",
        _ => code,
    };

    private static (string Part, string Remainder) Trailing(string text, string[] parts)
    {
        foreach (var part in parts)
        {
            if (text.EndsWith(part, StringComparison.Ordinal)) return (part, text[..^part.Length]);
        }
        return (string.Empty, text);
    }

    private static bool Starts(string text, string prefix, out string rest)
    {
        if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length >= prefix.Length)
        {
            rest = text[prefix.Length..];
            return true;
        }
        rest = string.Empty;
        return false;
    }

    private static string Trim(string text, string prefix)
    {
        var t = text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : text;
        return t.Length == 0 ? "1" : t;
    }

    private static readonly Dictionary<string, string> Abbrev = new(StringComparer.OrdinalIgnoreCase)
    {
        ["disp"] = "display", ["pc"] = "players", ["bnpc"] = "battle NPCs", ["npc"] = "NPCs", ["rezo"] = "resolution",
        ["lod"] = "level of detail", ["pad"] = "gamepad", ["xhb"] = "cross hotbar", ["wxhb"] = "expanded cross hotbar",
        ["fc"] = "Free Company", ["ls"] = "linkshell", ["cwls"] = "cross-world linkshell", ["pvp"] = "PvP", ["afk"] = "away",
        ["fps"] = "frame rate", ["ssao"] = "ambient occlusion", ["hp"] = "HP", ["ui"] = "UI", ["bg"] = "background",
        ["se"] = "sound effects", ["bgm"] = "music", ["ope"] = "operation", ["excute"] = "execute", ["ememy"] = "enemy",
        ["recast"] = "cooldown", ["telepo"] = "teleport", ["chara"] = "character", ["dps"] = "DPS", ["dtr"] = "server info bar",
        ["is"] = string.Empty, ["type"] = string.Empty,
    };

    // "HotbarDispRecastTime" -> "display cooldown time".
    public static string Words(string text)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '_')
            {
                Flush();
                continue;
            }
            var split = i > 0 && sb.Length > 0 && (
                (char.IsUpper(c) && (char.IsLower(text[i - 1]) || (i + 1 < text.Length && char.IsLower(text[i + 1]) && char.IsUpper(text[i - 1]))))
                || (char.IsDigit(c) && !char.IsDigit(text[i - 1]))
                || (!char.IsDigit(c) && char.IsDigit(text[i - 1])));
            if (split) Flush();
            sb.Append(c);
        }
        Flush();
        return parts.Count == 0 ? text : string.Join(' ', parts);

        void Flush()
        {
            if (sb.Length == 0) return;
            var word = sb.ToString();
            sb.Clear();
            if (Abbrev.TryGetValue(word, out var full))
            {
                if (full.Length > 0) parts.Add(full);
                return;
            }
            parts.Add(word.Length > 1 && word.ToUpperInvariant() == word ? word : word.ToLowerInvariant());
        }
    }

    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

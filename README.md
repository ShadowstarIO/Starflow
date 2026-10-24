# Flow

Set up a flow. When the conditions match, it does the rest. When they stop, it can put things back.

Open `/flow` or `/fl`.

A flow is a graph of four sections. Cards are the nodes.

| Section | What it is |
|---|---|
| When | Conditions. All or any. A card can be negated. |
| Do | What runs when the flow turns on. |
| Until | Optional. Ends the flow while When is still true. |
| After | What runs when the flow turns off. |

Conditions read the game, the clock, or whether a plugin is loaded. Actions write a game setting, run a command, repeat a macro, switch a plugin collection, set an online status, apply a Glamourer design, or change a Penumbra mod's enabled state and priority.

Slotted actions remember what they replaced: game settings, online status, a Glamourer design, and Penumbra mod state. **Revert** puts that back when the flow stops. **Keep** leaves it. After still runs either way. A higher-priority flow keeps a slot if two flows want it.

Plugin collections use `/xlenablecollection` and `/xldisablecollection`. A plugin card uses the matching enable and disable commands, saved or temporary. Flow does not unload a plugin that lives in more than one collection. Dalamud refuses that.

Nothing runs on the title screen. Quiet options can hold a flow during combat, a cutscene, or a zone line.

## Commands

- `/flow` — window
- `/fl Name` — run that flow's Do stack once
- `/fl stop Name` — run After and revert, if it was on
- `/fl why Name` — print each condition
- `/fl dry Name` — print the Do stack without running it
- `/fl now` — print which flows are on
- `/fl pause [seconds]`
- `/fl resume`
- `/fl ask` — flows waiting for a yes
- `/fl config`
- `/fl help`

Copy and paste flows as JSON from the window.

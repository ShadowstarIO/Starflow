# Flow

Set up a flow. When the conditions match, it does the rest. When they stop, it puts things back.

Open `/flow` or `/fl`.

A flow is four sections, read top to bottom. Cards are the nodes.

| Section | What it is |
|---|---|
| When | Conditions. All or any. A card can be negated. Empty means the flow stays off. |
| Do | What runs on the way in, in order. A command repeats up to 30 times. Wait is a delay. |
| Until | Optional. Ends the flow while When is still true. It stays off until When goes false. |
| After | What runs on the way out, every time. |

**Conditions:** state, duty, place, zone, region, housing address, group size, party count, job, role, level, world, home or visiting, data center, schedule, clock range, weekday, weather, mount, nearby count or name, online status, target, plugin loaded.

**Actions:** commands and macros, chat notices, game settings, online status, search comment, plugin collections, one plugin on or off, Glamourer designs, Penumbra mods with their options, Penumbra collection, log out, close game. Commands, notices, and comments fill `{zone} {region} {job} {world} {home} {dc} {ward} {plot} {time} {target}`.

**Revert** runs after After and puts back what slotted cards replaced: game settings, online status, Glamourer, and Penumbra mods. An After card that writes the same thing wins. **Keep** leaves it, for the flow or one card. Higher priority wins a slot; the first owner's original is what comes back.

Collections use `/xlenablecollection` and `/xldisablecollection`. A plugin that sits in more than one collection cannot be toggled alone; Dalamud refuses it.

Nothing runs on the title screen. Logging out drops the queue; a half-sent macro does not resume. Quiet options hold flows in combat, cutscenes, duties, or zone lines; a flow can ignore them.

Templates and imports open off. Import AndThen and StatusShift rules from the Import button.

## Commands

- `/flow` — window
- `/fl Name` — run Do once (one-shot; skips slotted cards unless the flow is on)
- `/fl stop Name` — run After and revert
- `/fl why Name` — print each condition
- `/fl dry Name` — print the Do stack without sending it
- `/fl now` — print which flows are on
- `/fl pause [seconds]` / `/fl resume`
- `/fl ask` — flows and cards waiting for a yes
- `/fl config` · `/fl help`

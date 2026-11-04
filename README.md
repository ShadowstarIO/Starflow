<p align="center">
  <img src="images/icon.png" width="160" alt="Starflow icon: a gold star joined to two nodes like a constellation, on a night-blue tile" />
</p>

<h1 align="center">Starflow</h1>

<p align="center">
  <img alt="Version 0.0.1.0" src="https://img.shields.io/badge/version-0.0.1.0-4a6fd8?style=flat-square" />
  <img alt="Dalamud API 15" src="https://img.shields.io/badge/Dalamud-API%2015-5ac8b4?style=flat-square" />
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512bd4?style=flat-square&logo=dotnet&logoColor=white" />
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-3a3a3a?style=flat-square" /></a>
  <img alt="Status: pre-release" src="https://img.shields.io/badge/status-pre--release-f0be5a?style=flat-square" />
</p>

<p align="center">
  <b>Read the stars, set your course. When the realm changes, your setup follows.</b><br>
  Conditional setups for FFXIV: settings, commands, status, Glamourer, Penumbra, plugin collections, and Lifestream.<br>
  <em>Built on Dalamud.</em>
</p>

<p align="center">
  <a href="#install"><b>Install</b></a> ·
  <a href="#how-a-flow-works">How a flow works</a> ·
  <a href="#use-with-care">Use with care</a> ·
  <a href="#commands">Commands</a> ·
  <a href="https://github.com/ShadowstarIO/xiv">All Shadowstar plugins</a>
</p>

---

## Install

1. In game, open `/xlsettings` → **Experimental**.
2. Under **Custom Plugin Repositories**, add `https://raw.githubusercontent.com/ShadowstarIO/XIV/main/repo.json`, tick it, and save.
3. Open `/xlplugins`, search for **Starflow**, and install.

Open it with `/starflow` or `/flow`. `/fl` is the short form.

## How a flow works

A flow is four sections, read top to bottom. Cards are the nodes.

| Section | What it is |
|---|---|
| When | Conditions. All or any. A card can be negated. Empty means the flow stays off. |
| Do | What runs on the way in, in order. A command repeats up to 30 times. Wait is a delay. |
| Until | Optional. Ends the flow while When is still true. It stays off until When goes false. |
| After | What runs on the way out, every time. |

**Conditions:** state, duty, place, zone, region, housing address, party size (with or without chocobo and pet), job, role, level, world, home or visiting, data center, schedule, clock range, weekday, weather, mount, nearby count or name, online status, target, plugin loaded.

**Reactions:** HP, MP, GP, or CP (yours, your target's, or the party's, as a number or percent), effects on you, your target, or the party, enemies targeting you, damage you just took (from a player or an NPC), party members down, and chat messages by channel, sender, and text.

**Actions:** commands and macros, chat notices, game settings, online status, search comment, plugin collections, one plugin on or off, Glamourer designs, Penumbra mods with their options, Penumbra collection, webhooks (Discord or any URL), Lifestream travel (home, FC, apartment, inn, a world, or an aetheryte), log out, close game. Commands, notices, comments, and webhooks fill tokens such as `{zone} {job} {world} {weather}`; Help lists all of them. Text conditions take words split on `|`, or a `/regex/`. Help lists every System and UI game option with what it does.

**Revert** runs after After and puts back what slotted cards replaced: game settings, online status, Glamourer, and Penumbra mods. An After card that writes the same thing wins. **Keep** leaves it, for the flow or one card. Higher priority wins a slot; the first owner's original is what comes back.

Collections use `/xlenablecollection` and `/xldisablecollection`. Dalamud may refuse to toggle a plugin that sits in more than one collection.

Nothing runs on the title screen. Logging out drops the queue; a half-sent macro does not resume. Quiet options hold flows in combat, cutscenes, duties, or zone lines; a flow can ignore them.

## Use with care

Starflow is meant for reacting to what goes on around you, not for playing. Flows that react to HP, effects, enmity, damage, or chat hold off on actions and macros (`/ac`, `/action`, `/macro` and the like). Tokens fill a command's arguments, not the command word.

**Chat cards let other people trigger your flows.** Anyone who can post in the channels you pick can set off whatever that flow does: emotes, speech, looks, travel. Name the senders, keep the channels narrow, and prefer Prompt mode. How you use it is up to you.

The game settings list sticks to options a person would set. Game bookkeeping, such as session and login records, is left out.

## Templates and imports

Templates and imports open off. Import AndThen and StatusShift rules from the Import button.

## Commands

- `/starflow` or `/flow` (short: `/fl`) — window
- `/fl Name` — run Do once (one-shot; skips slotted cards unless the flow is on)
- `/fl stop Name` — run After and revert
- `/fl why Name` — print each condition
- `/fl dry Name` — print the Do stack without sending it
- `/fl now` — print which flows are on
- `/fl pause [seconds]` / `/fl resume`
- `/fl prompts` — flows and cards waiting for a yes
- `/fl config` · `/fl help` (opens Help)

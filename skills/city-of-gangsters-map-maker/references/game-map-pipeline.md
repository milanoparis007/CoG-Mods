# Game Map Pipeline

## Purpose

This reference explains the code-backed difference between built-in maps, proc/custom maps, and exported custom map mods.

## Built-In Maps

Primary anchor:

- `decompiled/Game.UI/NewGameCityPopup.cs`

Key behavior:

1. `GetPurchasedBaseMapsSorted()` reads built-in maps from `Game.serv.globals.mapgen.maps`.
2. The new-game city picker includes those built-ins first.
3. Enabled mod maps are appended separately.
4. The `Custom City` button is a dedicated entry, not a built-in map.

Practical meaning:

- built-in maps are predefined `MapConfig` definitions
- they are not generated from scratch at selection time
- they act as the source templates for the proc/custom path

## Proc/Custom Maps

Primary anchor:

- `decompiled/Game.UI/NewGameCustomizePopup.cs`

`ShowNext()` calls `MakeCustomMap(GetOriginatingMap())`, which means the custom-city flow starts from a purchased base city, not from an empty map definition.

Inside `MakeCustomMap` the game:

1. clones the original `MapConfig`
2. assigns a fresh `id`
3. sets `proc = true`
4. rewrites city and state names
5. updates group counts
6. updates `map.mapSize`
7. clears and rebuilds roads (`map.nodes`)
8. clears and rebuilds rails (`map.rails`)
9. clears and rebuilds water (`terrain.waternodes`)
10. clears and rebuilds hills (`terrain.mountainnodes`)
11. clears and rebuilds districts (`map.districts`)

This is the strongest code-backed explanation for why a proc/custom map behaves differently from a normal built-in map: it is inherited from a base `MapConfig`, then heavily rewritten before the session generates the city.

## How Roads Actually Appear

Primary anchor:

- `decompiled/Game.Session.Setup/CreateMapNodes.cs`

`CreateMapNodes` reads `Game.ctx.session.mapconfig.map` and converts each `MapNodesConfig` entry in `map.nodes` into actual board nodes during setup.

Important implications:

- `map.nodes` is generation input, not the final rendered road list
- `forceStart`, `nodeSpacing`, `forceAngle`, `forceMaxSize`, and `forceMaxSpread` influence how the network grows
- changing the proc/custom road settings changes the generated city because the node inputs change before session setup runs

## How Custom Maps Enter and Stay In Session State

Primary anchors:

- `decompiled/Game.UI/GSPrepareForSession.cs`
- `decompiled/Game.Session/SessionContext.cs`

Key behavior:

1. Starting a new custom city passes the generated `MapConfig` into session prep as `custommap`.
2. Saves serialize `session.mapconfig` under the `mapconfig` key.
3. Loading a save restores that serialized `MapConfig` with `ParseMapConfigOrNull`.

Practical meaning:

- a generated custom map becomes part of the save
- the session is not just remembering the source city id
- the rewritten map definition persists with the game state

## How A Generated Map Becomes A Reusable Mod

Primary anchors:

- `decompiled/Game.UI.Session.Popups/ExportPopup.cs`
- `decompiled/Game.UI.Session.Popups/SaveQuitPopup.cs`
- `decompiled/Game.UI/NewGameCityPopup.cs`

Key behavior:

1. The export button is only available for proc-generated custom maps.
2. `ExportPopup.OnConfirm()` calls `Game.serv.mods.CreateNewMapMod(Game.ctx.session.custommap, ...)`.
3. That packaged mod can then be enabled in `Settings > Mods`.
4. On the next new-game flow, enabled mod maps are appended into the city list and loaded through `moddata.map`.

Practical meaning:

- exported maps start life as proc-generated session maps
- export turns that runtime `MapConfig` into a reusable mod package
- after export, the map comes back through the mod-map path rather than the proc/custom popup path

## Default Explanation To Reuse

When the user asks for the difference, use this wording:

1. Built-in maps are shipped `MapConfig` definitions from the game's map list.
2. Proc/custom maps clone one of those built-ins, set `proc = true`, then regenerate groups, size, roads, rails, water, hills, and districts before city generation.
3. Exported custom maps package the generated `custommap` into a mod so it can show up later as a normal selectable map entry.

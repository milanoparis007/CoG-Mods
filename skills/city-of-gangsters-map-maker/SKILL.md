---
name: city-of-gangsters-map-maker
description: Build, export, customize, and explain City of Gangsters custom maps from in-game proc generation, exported map mod files, workbook coordinate examples, and decompiled game anchors. Use when creating map mods, editing Data.txt map definitions, placing rivers or hills, naming districts, updating map previews, or comparing built-in maps with custom and exported map generation.
---

# City of Gangsters Map Maker

## Overview

Use this skill for City of Gangsters map authoring first, with reverse-engineering support when the user needs to understand how built-in maps, proc/custom maps, and exported map mods differ.

Keep the skill lean in context:

1. Start with the compact references in `references/`.
2. Open the decompiled anchors only when the task needs code-backed behavior.
3. Treat the original PDFs and workbook as source material, not default context.

## Quick Start

### 1. Pick the right reference first

- Exporting, editing, and sharing custom maps:
  `references/map-modding-source-summary.md`
- Reusing the New Orleans coordinate workbook:
  `references/new-orleans-workbook-example.md`
- Explaining built-in vs proc/custom vs exported map behavior:
  `references/game-map-pipeline.md`

### 2. Use the map-authoring workflow

1. Generate a custom city in-game and export it as a map mod.
2. Edit the exported `Data/Data.txt` for map content and `Preview.png` for presentation.
3. Enable the local mod in `Settings > Mods`.
4. Start a new game with the modded map and verify the geography.
5. Share or update the Workshop version only after the local version works.

### 3. Respect the data-format rules

- The exported map data format is not JSON.
- Do not add commas or colons unless the file already uses them in that exact place.
- Keep `{}` and `[]` balanced.
- Use the map's height to flip image-editor Y coordinates into in-game Y coordinates.

## Code-Grounded Routing

Open these anchors when the question is about true game behavior rather than guide-level authoring:

1. `decompiled/Game.UI/NewGameCityPopup.cs`
   Built-in map list, custom-city entry, and enabled mod map entries.
2. `decompiled/Game.UI/NewGameCustomizePopup.cs`
   The proc/custom map path that clones a base `MapConfig`, sets `proc = true`, and rewrites map content.
3. `decompiled/Game.Session.Setup/CreateMapNodes.cs`
   How `map.nodes` become actual road-network nodes during session setup.
4. `decompiled/Game.UI.Session.Popups/ExportPopup.cs`
   How a generated custom map becomes an exportable mod.
5. `decompiled/Game.Session/SessionContext.cs`
   How custom map configs are carried into session state and serialized into saves.

## Built-In vs Custom vs Exported

Use these defaults when explaining the difference:

1. Built-in maps are predefined `MapConfig` entries loaded from `Game.serv.globals.mapgen.maps`.
2. Proc/custom maps start from a cloned built-in `MapConfig`, then `NewGameCustomizePopup.MakeCustomMap` changes groups, size, roads, rails, water, hills, and districts before generation.
3. Exported custom maps come from `Game.ctx.session.custommap`, are packaged by `CreateNewMapMod`, and later show up as enabled mod maps in the new-game map list.

## Source Material

- Read `assets/source-material/README.md` for the original PDF and workbook paths.
- Prefer the derived markdown references first.
- Re-open the PDFs only when you need a verbatim field example, workflow wording, or screenshots the summary does not cover.

# Map Modding Source Summary

## Source Files

- `C:\Users\User\Documents\COG Modding Stuff\cog-map-modding-part-1.pdf`
- `C:\Users\User\Documents\COG Modding Stuff\cog-map-modding-part-2.pdf`

This summary is the first thing to read. Only reopen the PDFs when you need screenshots or the exact original walkthrough wording.

## Part 1: Export, Enable, Share

The first guide is the practical export and Workshop flow:

1. Start a new game with the `Custom City` option.
2. Pick a source city and geography, then generate until the map is worth keeping.
3. In `Save and Quit`, use `Export this map...`.
4. Fill in the mod name and description. The guide recommends leaving both checkboxes enabled so the game opens the mod folder and the documentation.
5. Return to the main menu, go to `Settings > Mods`, enable the local map mod, and verify it appears in the new-game city list.
6. Share the local map to Steam Workshop, publish it as `Public`, subscribe to it yourself, restart the game, and test the downloaded copy.

The guide also distinguishes mod states:

- `Local only`: exported on disk, not uploaded
- `Local shared`: local copy is linked to a Workshop item
- `Downloaded`: Workshop-managed copy that updates on game start

Important behavior:

- Local and downloaded copies are separate mod entries.
- Updating the local version and pressing `Update` syncs the Workshop item.

## Part 2: Edit the Exported Map Files

The second guide explains the file layout and the safest authoring loop:

1. Generate a blank custom city first.
2. Export it as a mod.
3. Edit the exported files directly.
4. Re-enable and launch the modded map in-game to test.

Expected mod folder contents:

- `Preview.png`
- `Description.txt`
- `Data/Data.txt`

The guide says the map content you usually care about is in these `Data.txt` sections:

- `mapSize`
- `waternodes`
- `mountainnodes`
- `districts`

## Coordinate and Authoring Rules

The guide's core authoring rule is that image editors usually use top-left origin, while the game uses lower-left origin. To convert image-picked points into game coordinates:

1. Resize the reference image to the map size.
2. Record `x` and `y` points from the image.
3. Flip Y using `gameY = mapHeight - imageY`.

Starter-city map sizes called out in the guide:

- Chicago: `300 x 400`
- Cincinnati: `400 x 400`
- Detroit: `400 x 400`
- Pittsburgh: `400 x 300`

The guide also warns that the data format is not JSON:

- no commas between entries unless the format already has them
- no JSON-style colons
- braces and brackets must stay balanced

## Data Sections the Guide Teaches

### waternodes

The guide uses `waternodes` to build rivers and lakes from linked points.

Fields called out explicitly:

- `id`
- `width`
- `forceStart`
- `connectingNodes`

### mountainnodes

The guide uses `mountainnodes` for hill blobs placed at explicit locations.

Fields called out explicitly:

- `id`
- `size`
- `forceStart`

### districts

The guide treats `districts` as named neighborhoods shown on the map.

Fields called out explicitly:

- `customname`
- `start`
- `radius`
- `tags`

Tags called out explicitly:

- `downtown`
- `bizcenter`
- `indcenter`

The guide also notes that `\n` can be used in `customname` to force a line break.

## Practical Defaults

Use these defaults unless the user asks for a different workflow:

1. Start from a custom map with rivers, hills, and coasts set to `none` when you want maximum manual control.
2. Edit geography in `Data/Data.txt` first, then refresh `Preview.png`.
3. Test the local mod before sharing or updating the Workshop copy.

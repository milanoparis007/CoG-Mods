# New Orleans Workbook Example

## Source File

- `C:\Users\User\Downloads\New Orleans.xlsx`

This workbook is a worked example for map placement, not a formal game schema. Use it as a coordinate-planning aid before retyping data into the map mod files.

## Observed Structure

The workbook has one sheet with about 178 populated rows and six used columns. The most important pattern is a reusable Y-flip formula:

- `B2 = 300`
- column `C` uses formulas like `B2-B5`

That means the workbook is using `300` as the map-height constant and computing:

`gameY = mapHeight - imageY`

This matches the PDF guide's advice about converting top-left image coordinates into the game's lower-left coordinate system.

## Section Breakdown

### Main river

Rows `5-28` look like the main river path:

- `A`: image-space X
- `B`: image-space Y
- `C`: flipped in-game Y via `B2 - current row B`
- `D`: node id such as `a` through `w`
- `E`: width values appear on at least the first node and likely serve as manual river-width notes

There is a duplicate `r` marker around rows `22-23`, with row `22` blank except for the label. Treat that as a workbook cleanup spot before direct reuse.

### Small rivers

Rows `31-34`, `37-41`, and `44-49` are labeled `Small River 1`, `Small River 2`, and `Small River 3`.

These sections keep the same coordinate pattern:

- `A/B`: source image point
- `C`: flipped game-space Y
- `D`: node id
- `E`: optional note such as `connects to t`

### Unlabeled secondary coordinate batch

Rows `53-70` contain ids `s1` through `s18` plus optional values in column `E`.

Inference:

- this is another coordinate-driven feature set
- column `E` likely stores a size, width, or radius note

Because the workbook section label is generic, verify the intended target before turning this block into `waternodes`, `districts`, or another map-data section.

### Mountain nodes

Rows `72-91` are labeled `Mountain Nodes`.

These appear to track:

- `A`: X
- `B`: image Y
- `C`: flipped game Y
- `D`: human-readable hill label

Most of these names are location notes like `East Park 1` or `Southwest Industry 4`, which makes them good planning labels before you convert them into final `mountainnodes` ids and `forceStart` values.

Two rows show `System.Xml.XmlElement` as the label, which looks like workbook or export noise. Double-check those before reusing them.

### District placement

Rows `109-123` appear to be neighborhood centers:

- `A/B/C`: coordinate triplet using the same Y-flip pattern
- `D`: district name such as `French Quarter`, `Bywater`, `Lower Ninth Ward`, `Lakeview`, or `Algiers`

This block maps well to the `districts` entries described in the PDF guide:

- `customname` from column `D`
- `start.x` from column `A`
- `start.y` from column `C`

You still need to choose the district `radius` and `tags` when converting these into `Data.txt`.

### Train placement notes

Rows `125-137` are labeled `Trains` and contain coordinate pairs, but they have little annotation.

Treat this block as exploratory reference only unless you have a matching in-game data target or decompiled anchor for rails or train paths.

## How To Reuse The Workbook Safely

1. Read the source image or map screenshot points from columns `A` and `B`.
2. Use column `C` as the in-game Y value.
3. Rebuild the final map data manually in `Data/Data.txt` using the correct section shape for `waternodes`, `mountainnodes`, or `districts`.
4. Keep the workbook labels as planning notes, then replace them with valid map ids and data fields in the exported map file.

## Practical Translation Defaults

- Water features:
  use `A` as `forceStart.x`, `C` as `forceStart.y`, `D` as the planning id, and bring over any width note from `E`.
- Hills:
  use `A` and `C` as `forceStart`, then choose a `size` experimentally.
- Districts:
  use `D` as `customname`, `A/C` as `start`, and set `radius` plus `tags` manually.

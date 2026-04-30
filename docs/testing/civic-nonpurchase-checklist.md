# Civic Nonpurchase Test Checklist

## Setup

1. Install the current `GameplayTweaks.dll`.
2. Use a save or map where these civic buildings exist:
   - bank
   - church regular
   - church large
   - school medium
   - school large
3. If testing with the PIA gambler purchase flow, note that in the report.

## Core UI Check

1. Go to a business that can offer building purchase or takeover options.
2. Also test any gambler-based building purchase path from the PIA mod.
3. Try to surface a purchase target for each civic building type above.
4. Expected result:
   - no buy button
   - no takeover prompt
   - no purchase action entry
   - no "available to buy" style highlight for those civic buildings

## Per-Building Checks

1. Bank:
   - visible on map
   - normal bank interaction still works
   - no purchase or takeover option
2. Church regular:
   - normal civic interaction still works
   - no purchase or takeover option
3. Church large:
   - normal civic interaction still works
   - no purchase or takeover option
4. School medium:
   - normal civic interaction still works
   - no purchase or takeover option
5. School large:
   - normal civic interaction still works
   - no purchase or takeover option

## PIA / Gambler Fallback Check

1. Use the PIA gambler path to try to acquire one of the civic buildings.
2. If the option appears anyway, click it once.
3. Expected result:
   - takeover should still fail
   - building should remain unowned or unpurchased
   - no crash or broken convo

## Stability Check

1. Select civic nodes directly.
2. Right-click civic nodes.
3. Open and close any civic-related convo or popup.
4. Expected result:
   - no null errors
   - no stuck popup
   - no broken selection behavior

## Map / Generation Check

1. Start or load a map that includes these civic buildings.
2. Expected result:
   - map generation succeeds
   - civic buildings still spawn normally
   - no setup errors

## Log Check

Log file:

`C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`

Look for:

- `CivicNonpurchase blocked runtime takeover`
- `CivicNonpurchase blocked perform-takeover`

Meaning:

- if no buy option appeared, that is already a pass
- if a mod leaked a purchase path, one of those log lines should appear when the block catches it

## Tester Report Format

1. Build date or version tested
2. Mods enabled
3. Which civic building was tested
4. Which path was used:
   - normal business purchase path
   - PIA gambler path
5. Result:
   - no button shown
   - button shown but blocked
   - failed or bug
6. If bug:
   - screenshot
   - last 30-50 lines of `Player.log`

## Release Gate

Ship-ready if:

1. none of the civic buildings show purchase or takeover options
2. the PIA gambler path cannot acquire them
3. civic interactions still work
4. no new `Player.log` errors
5. no map-generation regressions

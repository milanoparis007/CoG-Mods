# Current After Prohibition Mod Guide

## Install

1. Open the City of Gangsters install folder.
2. Copy this release folder's `BepInEx` contents into the game's `BepInEx` folder.
3. Keep the included `CoG_Data` and `Custom Maps` contents aligned with this release package when those folders are part of the release.

## Current DLL Staging

- `BepInEx\plugins\GameplayTweaks.dll` is staged from `GameplayTweaks\bin\Release`.
- Until symlinks are restored, manually copy staged DLLs from this release folder to the live game install.
- Public release DLLs should stay in this repo release folder first so the package remains ready for users.

## GameplayTweaks Inventory Behavior

- Storage transfer works when the crew vehicle is physically at the owned building or safehouse.
- Storage transfer also works while the selected vehicle is actively traveling to that exact storage building as its final goal.
- If the vehicle leaves or targets another corner, building and selected-vehicle storage can still be viewed, but loading and unloading are disabled for that vehicle.

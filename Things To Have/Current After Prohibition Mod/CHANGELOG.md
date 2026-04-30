# Current After Prohibition Mod Changelog

## 2026-04-30

- Staged an updated GameplayTweaks public release DLL.
- Fixed owned-building and safehouse storage view-only mode after a vehicle leaves the corner:
  - the selected vehicle context is preserved so the storage UI layout stays stable;
  - transfer buttons are disabled unless the vehicle is physically present or actively traveling to that exact storage target.
- Suppressed destination corner/summary glow during travel-preview refreshes while still allowing destination building picks to refresh for scopeout.
- Removed stale departure-corner building picks during vehicle travel preview so businesses at the corner the player drove from do not keep glowing.
- Updated owned-building and safehouse inventory behavior around vehicle final-goal travel:
  - transfer remains enabled while the selected vehicle is actively traveling to the matching storage building;
  - storage remains visible but view-only after the vehicle leaves or targets another corner;
  - physical arrival still enables normal loading and unloading.
- Hardened early startup logging so new-game initialization cannot crash GameplayTweaks when the game clock is not ready yet.
- Blocked gang and troublemaker affiliated candidates from player crew hire and business-owner assignment paths.

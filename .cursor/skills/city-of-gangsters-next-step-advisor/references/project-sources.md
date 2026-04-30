# Project Sources

Use this file to route next-step analysis to the right project, repo docs, and decompiled anchors.

## Repo root

- The workspace root for this solution: the directory that contains `ClassLibrary1.sln`.

## Project ownership

- `GameplayTweaks`
  - Main owner for gameplay state, save data, pacts, gang ops, dirty cash, jail/heat, major UI behavior, and most vehicle authority work.
- `CopKilling`
  - Owner for cop-war behavior, witness escalation, precinct retaliation, and cop-combat naming.
- `GameOptimizer`
  - Owner for performance-only changes, manual/reverse patches, batching, debouncing, and coroutine substitutions.
- `BossBuildings`
  - Focused boss building behavior only.
- `BossDeath`
  - Focused boss death continuation and related follow-up behavior.
- `OrgChartMod`
  - Org chart display behavior only.
- `AutoLevelup`
  - Automatic level-up behavior only.
- `ClassLibrary1/ModLauncher`
  - Bridge/launcher compatibility only.

## Recent addition anchors

Check these first when the user asks what should happen next after the latest expansion:

1. `docs/progress.md`
2. `session_summary.md`
3. `docs/compatibility-matrix.md`
4. `git show --stat --name-only HEAD`

Latest large-change ownership:

1. `CopKilling/CopKillingPlugin.cs`
2. `GameplayTweaks/GameplayTweaksPlugin.cs`
3. `GameOptimizer/GameOptimizerPlugin.cs`
4. `GameplayTweaks/GameplayTweaks.csproj`
5. `GameOptimizer/GameOptimizer.csproj`
6. `CopKilling/CopKilling.csproj`

## Method-heavy live files

These are current signal hotspots, not automatic rewrite targets:

1. `GameplayTweaks/GameplayTweaksPlugin.cs`
2. `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.cs`
3. `GameplayTweaks/GameplayTweaksPlugin.Combat.cs`
4. `GameOptimizer/GameOptimizerPlugin.cs`
5. `CopKilling/CopKillingPlugin.cs`
6. `GameplayTweaks/GameplayTweaksPlugin.Pacts.cs`
7. `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.TerritoryAndGangWarsCompatibility.cs`
8. `GameplayTweaks/GameplayTweaksPlugin.UI.cs`

## Docs and logs

Use these to infer whether the next step should be stabilization or new work:

1. `Player.log`
2. `Player-prev.log`
3. `docs/multi-crew-vehicle-plan.md`
4. `docs/compatibility-matrix.md`
5. `CLAUDE.md`

## Decompiled game anchors

Inspect these when the recommendation depends on true game behavior rather than mod assumptions:

1. `decompiled/Game.UI.Session.Crew/CrewManagementPopup.cs`
   Why: crew assignment UI, add-to-building or add-to-vehicle affordances, crew count display.
2. `decompiled/Game.UI.Session.Crew/CrewInfoGen.cs`
   Why: crew-card target/location display.
3. `decompiled/Game.UI.Session.Crew/CrewInfoGenJustVehicle.cs`
   Why: vehicle-specific crew card behavior.
4. `decompiled/Game.Session.Input/BaseInputMode.cs`
   Why: Harmony warnings currently point at base virtual input hooks.
5. `decompiled/Game.Session.Input/CarInputMode.cs`
   Why: concrete vehicle-input behavior if base-hook warnings imply wrong target choice.
6. `decompiled/Game.Session.Input/DefaultInputMode.cs`
   Why: alternate concrete input hooks.
7. `decompiled/Game.UI.Session.Picks/`
   Why: pick refresh, target selection, and type drift such as `MobilePickUtil`.
8. `decompiled/Game.Session.Player.AI/FedsAdvisor.cs`
   Why: fed investigation cadence when evaluating CopKilling witness escalation.
9. `decompiled/Game.Session.Data/Relationship.cs`
   Why: GameOptimizer reverse-patch constraints around `Relationship.Evaluate`.
10. `decompiled/Game.Session.Data/VisitState.cs`
    Why: vehicle node resolution and arrival authority.
11. `decompiled/Game.Session.Entities/BuildingUtil.cs`
    Why: known scope or building-selection phrasing when vehicle/building interactions feel off.

## Current likely investigation threads

Start here when the user asks for the most valuable next step today:

1. `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiDebugPatches.cs`
   Why: current `HostileMobileSelectionFixPatch` assembly-load failures.
2. `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.cs`
   Why: documented follow-up work plus the largest active feature branch.
3. `GameplayTweaks/GameplayTweaksPlugin.cs`
   Why: gang ops logs show repeated pact-side `skipped=no-eligible-gangs`.
4. `GameOptimizer/GameOptimizerPlugin.cs`
   Why: disabled patches and manual patch debt remain visible.
5. `BossDeath/BossDeathPlugin.cs`
   Why: repeated `BossRoleSetter` null warnings in `Player.log`.
6. `CopKilling/CopKillingPlugin.cs`
   Why: newest standalone mod with witness and precinct escalation flows.

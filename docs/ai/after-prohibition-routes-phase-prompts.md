# AfterProhibitionRoutes Phase Prompts

Use this plan to split travel, route continuation, vehicle node authority, route simulation, and delivery-route movement safety out of `GameplayTweaks` into a standalone plugin named `AfterProhibitionRoutes`.

## Goal

Create a routes-only owner for:

- vehicle physical node authority;
- long-distance turn continuation and queued route resume;
- route-mode labels and route state diagnostics;
- route-simulated destination access guards;
- delivery-route automation pump safety;
- route arrival finalization and observed-arrival diagnostics;
- strict prevention of false corner presence.

This plugin must not own:

- shop buy/sell stock, bank/warehouse purchase access, dirty-cash routing, or front resource repair, which belong in `AfterProhibitionEconomy`;
- popup docking, stale portraits, crew pick visuals, retheme work, or menu/sidebar layout, which belong in `AfterProhibitionUI`;
- spouse, children, dead relationship cleanup, or hiring/family safety, which belong in `AfterProhibitionFamily`;
- political starter quest, bribes, judge/law-office, campaigns, or elections, which belong in `AfterProhibitionPolitics`;
- external DLL detection, 10k input compatibility, Dirty Cash DLL detection, or broad-unpatch safety, which belong in `AfterProhibitionCompatibility`;
- combat, robberies, cop war, gang pacts, or territory simulation.

## Current Live Signals

Recent logs show route behavior is still owned by `GameplayTweaks`:

```txt
[GameplayTweaks] Gameplay Tweaks Extended loaded ... routeSimConvenienceEnabled=True
[VERIFY-HOTFIX] [VehicleNodeAuthority] route-mode-label vehicle=... label="At corner" node=...
[VERIFY-HOTFIX] [VehicleNodeAuthority] arrival-node vehicle=... node=... source=mobile
[VERIFY-HOTFIX] [VehicleNodeAuthority] vehicle-node-reached vehicle=... node=... source=mobile
[VERIFY-HOTFIX] [ScopeOut] building-presence-scope-preview-blocked ... reason=vehicle-not-physical
```

The existing stabilization plan is:

```txt
docs\ai\route-simulation-authority-phase-plan.md
```

That plan should be treated as the behavior source of truth. `AfterProhibitionRoutes` should migrate ownership in small proven slices.

## Phase 1 - Standalone Plugin Scaffold

Status: completed 2026-05-13.

Implementation notes:

- Added `AfterProhibitionRoutes\AfterProhibitionRoutes.csproj`.
- Added `AfterProhibitionRoutes\AfterProhibitionRoutesPlugin.cs`.
- Added `AfterProhibitionRoutes\README.md`.
- Added the project to `ClassLibrary1.sln`.
- Added read-only bridge methods: `IsRoutesBridgeAvailable()`, `GetRoutesBridgeVersion()`, and `GetRoutesBridgeSummary()`.
- No route continuation, vehicle node authority, route-simulated access, delivery automation, shop staging, UI labels, storage, scope-out, economy, family, politics, compatibility, combat, robbery, territory, save, or `GameplayTweaks` behavior was moved in this phase.

Prompt:

```txt
Create a new standalone BepInEx plugin project named AfterProhibitionRoutes. Keep it separate from GameplayTweaks, AfterProhibitionCompatibility, AfterProhibitionEconomy, AfterProhibitionFamily, AfterProhibitionPolitics, AfterProhibitionUI, and AfterProhibitionAssets. Add a minimal plugin class, Harmony ID, logger, config section, delayed routes baseline log, and bridge version methods. Do not move route continuation, vehicle node authority, route simulated access, delivery automation, shop staging, UI labels, storage, or scope-out behavior yet.

Validation:
- dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release
- Confirm no dependency on GameplayTweaks.
- Confirm no economy, UI, family, politics, compatibility, combat, robbery, territory, or save behavior is included.
```

Expected files:

```txt
AfterProhibitionRoutes/
  AfterProhibitionRoutes.csproj
  AfterProhibitionRoutesPlugin.cs
  README.md
```

Expected logs:

```txt
[After Prohibition Routes] routes baseline scheduled ownsEconomy=False ownsUi=False ownsFamily=False ownsPolitics=False ownsCompatibility=False
[After Prohibition Routes] After Prohibition Routes loaded phase=scaffold
```

## Phase 2 - Route Migration Inventory

Status: completed 2026-05-13.

Implementation notes:

- Added `docs\ai\after-prohibition-routes-migration-inventory.md`.
- Confirmed live logs show the map loaded and route behavior is still owned by `GameplayTweaks`.
- Inventoried vehicle physical node authority, active/pending route state, queued route resume, human travel command guards, StartDriving/path guards, travel-end finalization, turn-start finalization, delivery route automation pump, route-simulated conversations, route-in shop staging coordination, route-mode labels, scope-out presence guards, safehouse/owned-storage access guards, ambient traffic, and fake traffic.
- Marked read-only diagnostics, UI labels, economy staging, storage mutation, and behavior-changing patches separately.
- No code migration was done in this phase.

Prompt:

```txt
Inventory current route and travel code in GameplayTweaks and decompiled game anchors. Create a migration table listing the current source file, method or patch, current log tag, behavior risk, and whether the item should move now, later, or stay.

Validation:
- No code migration yet.
- Include vehicle physical node authority, active route state dictionaries, queued route resume, human travel command guards, StartDriving guards, travel-end finalization, turn-start finalization, delivery route automation pump, route-simulated conversations, route-in shop staging coordination, route-mode labels, scope-out presence guards, safehouse/owned-storage access guards, ambient traffic, and fake traffic.
- Mark read-only diagnostics, UI labels, economy staging, storage mutation, and behavior-changing patches separately.
```

Suggested doc:

```txt
docs\ai\after-prohibition-routes-migration-inventory.md
```

Likely source anchors:

```txt
GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs
GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.TravelAndAssignment.cs
GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.Interaction.cs
GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.CrewUi.cs
GameplayTweaks\GameplayTweaksPlugin.DeliveryFrontExpansionPatch.cs
docs\ai\route-simulation-authority-phase-plan.md
docs\ai\route-in-out-shop-staging-design.md
decompiled\Game.Session.Commands\CommandGoto.cs
decompiled\Game.Session.Commands\CommandAutomationStep.cs
decompiled\Game.Session.Entities\MobileComponent.cs
decompiled\Game.Session.Entities\BuildingComponent.cs
decompiled\Game.UI.Session.Picks\BuildingPick.cs
```

## Phase 3 - Read-Only Route State Audit

Status: completed 2026-05-13.

Implementation notes:

- Updated `AfterProhibitionRoutes` to `0.2.0`.
- Added read-only startup and human-turn route state audits.
- Audits reflect `GameplayTweaks.MultiCrewVehicleHelper`, `GameplayTweaks.HumanVehicleTravelEndSyncPatch`, and `GameplayTweaks.RouteShopStagingState` without calling route behavior methods.
- Counts include pending vehicles, active vehicles, queued resumes, recent requeues, turn-start finalized/deferred sets, delivery pump guards, manual delivery stops, staged shop orders, remembered route-in conversations, open route shop pickers, and route-mode label candidates.
- Added config gates `EnableStartupRouteStateAudit` and `EnableHumanTurnRouteStateAudit`.
- Patched `CommandExecutor.OnPlayerTurnStarted` with a read-only postfix only; no route state is started, stopped, resumed, cleared, or delegated.
- Built successfully with `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`.

Prompt:

```txt
Move read-only route diagnostics into AfterProhibitionRoutes. Add a startup and human-turn audit that reports whether GameplayTweaks route state is readable, how many human vehicles have pending travel state, active route state, queued resume state, delivery pump guards, staged route shop orders, and route-mode labels. Read GameplayTweaks state through reflection only. Do not mutate route state.

Validation:
- Do not start, stop, resume, or clear any route.
- Do not change vehicle nodes, agent nodes, mobile nodes, building picks, delivery automation, shop staging, storage, or UI.
- Logs must include a clear reason when GameplayTweaks route state is missing or unreadable.
```

Expected logs:

```txt
[After Prohibition Routes] route-state-audit source=start-1s gameplayTweaks=True pendingVehicles=... activeVehicles=... queuedResume=... deliveryPumpGuards=... stagedShopOrders=... reason=readable
```

## Phase 4 - Route Authority Classification Bridge

Status: completed 2026-05-13.

Implementation notes:

- Updated `AfterProhibitionRoutes` to `0.3.0`.
- Added `GetRouteStateSummary()`.
- Added `ClassifyVehicleRouteAuthority(EntityID vehicleId)`.
- Added `ClassifyVehicleRouteAuthority(EntityID vehicleId, NodeID queryNodeId)`.
- Added `ClassifyVehicleRouteAuthority(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- Added `ClassifyVehicleRouteAuthoritySummary(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- The classifier reports live physical node, physical node source, pending start node, active expected node, queued final goal, route mode label, query-node matches, and route-sim convenience classification.
- Route labels match current `GameplayTweaks` semantics: `At corner`, `Arriving`, `In route`, or `Unknown`.
- Route-sim convenience classification stays diagnostic only and blocks storage, combat, hostile, module, and commit-style physical-only actions.
- The bridge reads `GameplayTweaks` route fields and live vehicle node data without starting, stopping, resuming, clearing, finalizing, or delegating any route.
- Built successfully with `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`.

Prompt:

```txt
Add read-only route authority bridge methods to AfterProhibitionRoutes. The bridge should classify a selected vehicle's physical node, expected active-segment node, queued final goal, route mode label, and whether route-simulated convenience access would be allowed. GameplayTweaks remains the behavior owner in this phase.

Validation:
- GameplayTweaks can call bridge methods for diagnostics without delegating behavior.
- No false physical presence is introduced.
- Route labels still match At corner, Arriving, and In route.
```

Expected bridge examples:

```txt
AfterProhibitionRoutesPlugin.IsRoutesBridgeAvailable()
AfterProhibitionRoutesPlugin.GetRoutesBridgeVersion()
AfterProhibitionRoutesPlugin.GetRouteStateSummary()
AfterProhibitionRoutesPlugin.ClassifyVehicleRouteAuthority(...)
```

## Phase 5 - Travel Turn Continuation Bridge

Status: completed 2026-05-13.

Implementation notes:

- Updated `AfterProhibitionRoutes` to `0.4.0`.
- Added `EnableTravelContinuationDecisionBridge`, default `true`.
- Added `OwnsTravelContinuationDecision()`.
- Added `ClassifyTravelContinuation(EntityID vehicleId)`.
- Added `ClassifyTravelContinuationSummary(EntityID vehicleId)`.
- Added `LogTravelContinuationDecision(EntityID vehicleId, string source)`.
- `AfterProhibitionRoutes` now owns the read-only travel-continuation decision/classification only. It does not start, stop, resume, clear, finalize, or delegate route mutation.
- `GameplayTweaks` now calls the Routes decision bridge during turn-start finalization and queued route resume, logs `RouteContinuation delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True`, and keeps the existing GameplayTweaks route-resume mutator as fallback behavior.
- If the bridge is missing, disabled, or missing expected methods, `GameplayTweaks` logs `RouteContinuation fallback active reason=...`.
- Built successfully with:
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`

Prompt:

```txt
Move only the proven long-distance travel continuation decision into AfterProhibitionRoutes behind a config gate. GameplayTweaks keeps fallback behavior until live logs prove AfterProhibitionRoutes is loaded and owns travel continuation. Focus on the player complaint that ending a turn while the vehicle is between nodes should continue the committed route instead of requiring manual waits at each intermediate node.

Validation:
- Long route continuation resumes across turns.
- Delivery routes do not advance automation ahead of physical/observed arrival.
- Active segment expected-node finalization still requires observed mobile or strict physical arrival.
- GameplayTweaks alone still works when AfterProhibitionRoutes is missing.
```

Expected logs:

```txt
[After Prohibition Routes] travel-continuation route-resume ...
[After Prohibition Routes] travel-continuation deferred reason=await-observed-arrival ...
[GameplayTweaks] RouteContinuation delegated owner=AfterProhibitionRoutes
[GameplayTweaks] RouteContinuation fallback active reason=bridge-missing
```

## Phase 6 - Vehicle Node Authority Bridge

Status: completed 2026-05-13.

Implementation notes:

- Updated `AfterProhibitionRoutes` to `0.5.0`.
- Added `EnableVehicleNodeAuthorityDecisionBridge`, default `true`.
- Added `OwnsVehicleNodeAuthorityDecision()`.
- Added `ClassifyVehicleNodeAuthority(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- Added `ClassifyVehicleNodeAuthoritySummary(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- Added `LogVehicleNodeAuthorityDecision(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- The bridge classifies live authority node, strict physical node, observed arrival marker, pending expected node, final goal node, physical-only action rejection, stale logical-node rejection, and expected-arrival finalization/defer decisions.
- `AfterProhibitionRoutes` now owns read-only vehicle-node authority decisions only. It does not record observed arrivals, set recent finalized nodes, sync occupants, move vehicles, clear route state, or mutate UI/picks.
- `GameplayTweaks` now calls the bridge from physical, strict-physical, and observed-arrival checks, logs `VehicleNodeAuthority delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True`, and keeps all existing behavior/fallback logic.
- Built successfully with:
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`

Prompt:

```txt
Move vehicle physical node classification, observed-arrival markers, expected-node finalization checks, and stale logical-node rejection into AfterProhibitionRoutes. Keep UI refresh and building-pick visual work in GameplayTweaks or AfterProhibitionUI until the authority bridge has live validation.

Validation:
- No vehicle is treated as physically present at a wrong corner.
- Logs still show arrival-node and vehicle-node-reached only after mobile or strict physical arrival.
- Scope-out, storage transfer, combat, and shop commit paths still reject false presence.
```

Expected logs:

```txt
[After Prohibition Routes] vehicle-node-authority arrival-node vehicle=... node=... source=mobile
[After Prohibition Routes] vehicle-node-authority vehicle-node-reached vehicle=... node=... source=observed-mobile
[After Prohibition Routes] vehicle-node-authority finalize-deferred reason=not-physical ...
```

## Phase 7 - Delivery Route Pump Bridge

Status: completed 2026-05-13.

Implementation notes:

- Updated `AfterProhibitionRoutes` to `0.6.0`.
- Added `EnableDeliveryPumpDecisionBridge`, default `true`.
- Added `OwnsDeliveryPumpDecision()`.
- Added `ClassifyDeliveryPump(EntityID vehicleId, NodeID finalNodeId, string source, int queuePumps, int automationPumps)`.
- Added `ClassifyDeliveryPumpSummary(EntityID vehicleId, NodeID finalNodeId, string source, int queuePumps, int automationPumps)`.
- Added `LogDeliveryPumpDecision(EntityID vehicleId, NodeID finalNodeId, string source, int queuePumps, int automationPumps)`.
- The bridge classifies active-route defers, queued-resume defers, delivery pump guard targets, manual-stop suppression, one-automation-step budget, and safe pump continuation.
- `AfterProhibitionRoutes` now owns read-only delivery-pump decisions only. It does not process command queues, run automation steps, clear pump guards, mark manual stops, start routes, or mutate delivery/economy state.
- `GameplayTweaks` now calls the Routes delivery-pump decision bridge around delivery queue and automation pump checkpoints, logs `DeliveryRoutePump delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True`, and keeps the existing GameplayTweaks delivery pump mutator as fallback behavior.
- Built successfully with:
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`

Prompt:

```txt
Move delivery route queue pump and automation pump safety into AfterProhibitionRoutes after vehicle node authority is proven. Keep economy buy/sell, resource transfer, and shop stock mutation outside this plugin.

Validation:
- Delivery routes can continue multiple stops without manual babysitting.
- A delivery pump cannot advance unrelated route steps while a queued resume owns the next move.
- A pump runs at most one non-route automation step per arrival unless it starts or queues a route segment.
- Existing diagnostics for delivery-route-arrival-confirmed, delivery-route-pump-wait, and delivery-route-pump-summary remain available.
```

Expected logs:

```txt
[After Prohibition Routes] delivery-route-queue-pump ...
[After Prohibition Routes] delivery-route-pump-wait reason=active-route-* ...
[After Prohibition Routes] delivery-route-pump-summary automationPumps=1 ...
```

## Phase 8 - Route Simulated Destination Access Bridge

Status: completed 2026-05-13.

Implementation notes:

- Updated `AfterProhibitionRoutes` to `0.7.0`.
- Added `EnableRouteSimAccessDecisionBridge`, default `true`.
- Added `OwnsRouteSimAccessDecision()`.
- Added `ClassifyRouteSimAccess(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- Added `ClassifyRouteSimAccessSummary(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- Added `LogRouteSimAccessDecision(EntityID vehicleId, NodeID queryNodeId, string actionType)`.
- The bridge classifies route-sim expected-node access, route-sim goal-node access, wrong-destination blocks, physical-only action blocks, and route shop staging context state.
- `AfterProhibitionRoutes` now owns read-only route-sim access decisions only. It does not open conversations, remember route-in conversations, stage shop orders, commit buy/sell orders, move goods, move cash, clear route state, or mutate UI/picks.
- `GameplayTweaks` now calls the Routes route-sim decision bridge from `IsHumanVehicleRouteSimAccessNode`, logs `RouteSimAccess delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True`, and keeps the existing GameplayTweaks route-sim access/staging mutator as fallback behavior.
- Built successfully with:
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`

Prompt:

```txt
Move route-simulated destination access classification into AfterProhibitionRoutes. Keep actual shop buy/sell order staging coordinated with AfterProhibitionEconomy and keep UI presentation with AfterProhibitionUI or GameplayTweaks until those systems have their own owners.

Validation:
- Route-in destination conversations can open only for the committed destination.
- Route-out or wrong-shop attempts stay blocked.
- Shop staging cannot move goods or cash before physical arrival.
- Hostile, combat, storage, and module mutation actions stay physical-only.
```

Expected logs:

```txt
[After Prohibition Routes] route-sim-access vehicle=... node=... action=...
[After Prohibition Routes] route-sim-convo vehicle=... node=... context=...
[After Prohibition Routes] route-sim-blocked reason=physical-only ...
```

## Phase 9 - GameplayTweaks Delegation

Status: completed 2026-05-13.

Implementation notes:

- Updated `AfterProhibitionRoutes` to `0.8.0`.
- Added full-behavior ownership methods:
  - `OwnsTravelContinuation()`
  - `OwnsVehicleNodeAuthority()`
  - `OwnsDeliveryPump()`
  - `OwnsRouteSimAccess()`
  - `OwnsRouteBehaviorSlice(string slice)`
- All full-behavior ownership methods intentionally return `false` in this phase because `AfterProhibitionRoutes` still owns decision/classification only, not mutation.
- `GameplayTweaks` now checks `OwnsRouteBehaviorSlice(string slice)` before route continuation finalization/resume, vehicle node authority checks, delivery pump guards, delivery pump continuation, and route-sim access.
- Because `AfterProhibitionRoutes` reports full behavior ownership as `false`, `GameplayTweaks` keeps all existing behavior and logs `Routes behavior fallback active slice=... reason=behavior-owner-false`.
- This creates safe future skip points without double-finalizing arrivals, double-pumping delivery routes, or double-clearing pending route state.
- Built successfully with:
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`

Prompt:

```txt
Add delegation checks in GameplayTweaks so route-specific behavior is skipped when AfterProhibitionRoutes is loaded and owns that slice. Keep fallback enabled for users who do not install AfterProhibitionRoutes.

Validation:
- GameplayTweaks still works alone.
- GameplayTweaks plus AfterProhibitionRoutes does not double-finalize arrivals, double-pump delivery routes, or double-clear pending route state.
- Logs clearly show delegated, fallback, and disabled states.
```

Expected logs:

```txt
[GameplayTweaks] RouteContinuation delegated owner=AfterProhibitionRoutes
[GameplayTweaks] VehicleNodeAuthority delegated owner=AfterProhibitionRoutes
[GameplayTweaks] DeliveryRoutePump delegated owner=AfterProhibitionRoutes
[GameplayTweaks] Routes fallback active reason=bridge-missing
```

## Phase 10 - Packaging And Guide

Status: completed 2026-05-13.

Implementation notes:

- Verified `AfterProhibitionRoutes.dll` is staged in:
  - `Things To Have\Current After Prohibition Mod\Personal\BepInEx\plugins`
  - `Things To Have\Current After Prohibition Mod\Public\Days Of Prohibition v1.3.98\After Prohibition Mod\BepInEx\plugins`
- Verified matching staged `GameplayTweaks.dll` is present in both release plugin folders because GameplayTweaks remains required.
- Updated release guide text to state that `AfterProhibitionRoutes` is installed separately, `GameplayTweaks` remains required, and the current route split owns read-only diagnostics/decision bridges only.
- Updated release changelog text to identify current route ownership:
  - fully owned by `AfterProhibitionRoutes`: read-only route audits and decision/classification bridges
  - delegated to `AfterProhibitionRoutes`: decision logging/classification consumed by `GameplayTweaks`
  - still fully owned by `GameplayTweaks`: mutating route behavior for travel continuation, vehicle node authority, delivery-route pump, route-simulated access, shop staging, goods/cash movement, UI mutation, and save state
- Live log check showed `AfterProhibitionRoutes` version `0.8.0` active with route-state audit, travel-continuation, vehicle-node-authority, delivery-route-pump, and route-sim-access decision logs. No matching route/map exceptions were found in the filtered live log.

Prompt:

```txt
Stage AfterProhibitionRoutes in the Personal and Public release plugin folders. Update the release guide and changelog with install order, ownership boundaries, and fallback behavior.

Validation:
- The DLL is staged in both release plugin folders.
- The guide says GameplayTweaks remains required unless a later phase removes that dependency.
- The changelog states which route behavior is read-only, delegated, or fully owned.
```

Expected staging paths:

```txt
Things To Have\Current After Prohibition Mod\Personal\BepInEx\plugins\AfterProhibitionRoutes.dll
Things To Have\Current After Prohibition Mod\Public\Days Of Prohibition v1.3.98\After Prohibition Mod\BepInEx\plugins\AfterProhibitionRoutes.dll
```

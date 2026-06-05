# After Prohibition Suite Ownership Audit

This audit supports Phase 2.7 in `docs/turn-end-pause-phased-prompt.md`.

File and DLL separation is not the runtime cost. The cost to watch is duplicate hot-path ownership: more than one DLL mutating the same turn, UI, route, or economy state during the same hook.

## Live Suite Snapshot

Observed live DLL timestamps:

| DLL | Timestamp | Current role |
| --- | --- | --- |
| `AfterProhibitionAssets.dll` | `2026-05-14 11:07:30` | Main-menu and asset replacement |
| `AfterProhibitionCompatibility.dll` | `2026-05-14 11:07:32` | External DLL detection and compatibility matrix |
| `AfterProhibitionEconomy.dll` | `2026-05-15 10:34:07` | Economy classification plus stock refresh and empty-business repair ownership |
| `AfterProhibitionFamily.dll` | `2026-05-14 19:12:17` | Family, pregnancy, spouse, and future-child validation |
| `AfterProhibitionPolitics.dll` | `2026-05-14 11:07:40` | Politics starter and politics bridge behavior |
| `AfterProhibitionRoutes.dll` | `2026-05-14 11:07:42` | Route decisions and vehicle-node authority bridge |
| `AfterProhibitionUI.dll` | `2026-05-14 11:07:45` | Retheme, Crew HUD, aggro UI, portraits, and visual guards |

## Current Hot-Path Ownership Matrix

| Hot path | Current patching DLLs | Mutation owner today | Classification/decision owner | Current risk |
| --- | --- | --- | --- | --- |
| `BusinessUpdate.UpdateBusinessModules` | `GameplayTweaks`, `AfterProhibitionEconomy` | Base game plus `AfterProhibitionEconomy` for stock/repair; `GameplayTweaks` for Dirty Cash safety fallback | `AfterProhibitionEconomy` | Medium: multiple postfixes, but economy stock/repair is now batched |
| `BusinessUpdate.Tick` | `GameplayTweaks` | Base game; `GameplayTweaks` safety fallback and perf cache | `GameplayTweaks` | Medium: hot path still has Dirty Cash safety hooks and performance diagnostics |
| `PlayerInfo.OnPlayerTurnStarted` | `GameplayTweaks`, `AfterProhibitionEconomy`, other suite DLLs by feature | Feature-specific owners | Mixed | Medium: human turn-start can collect several feature passes |
| `PeopleTracker` birth/marriage/death paths | `GameplayTweaks`, `AfterProhibitionFamily` | `AfterProhibitionFamily` for family validation; `GameplayTweaks` for perf guards | Mixed | Medium: keep perf guards until duplicate mutation is proven |
| `PoliticsManager` / politics startup | `GameplayTweaks`, `AfterProhibitionPolitics` | `AfterProhibitionPolitics` | `AfterProhibitionPolitics` | Low: logs show starter delegation |
| `CommandGoto` / route simulation / vehicle node authority | `GameplayTweaks`, `AfterProhibitionRoutes` | `GameplayTweaks` fallback today | `AfterProhibitionRoutes` decision bridge | Medium: `behaviorFallback=True` remains by design |
| HUD, Crew HUD, aggro UI, crew picks | `GameplayTweaks`, `AfterProhibitionUI` | `AfterProhibitionUI` for UI ownership; `GameplayTweaks` for stability guards | `AfterProhibitionUI` | Medium: make sure GameplayTweaks is guard-only for delegated UI |
| Main-menu assets | `AfterProhibitionAssets`, external asset DLLs | `AfterProhibitionAssets` | `AfterProhibitionAssets` | Low: startup/menu only |
| External compatibility detection | `AfterProhibitionCompatibility`, `GameplayTweaks` bridge reads | `AfterProhibitionCompatibility` | `AfterProhibitionCompatibility` | Low: should remain read-only after startup |
| Camera panning / map bounds | `GameplayTweaks` | `GameplayTweaks` guard-only clamp | Base map config | Low: UI/input guard, no feature mutation |

## Phase 2.7A Code And Log Audit

Status: completed as an evidence pass on `2026-05-15`.

The audit confirms that the current pause risk is not caused by splitting source files or DLLs. The risk comes from shared hot hooks where one DLL classifies or decides and another DLL still mutates state in the same turn.

### Economy

| Method / feature | Current patching DLLs | Mutation owner | Classification owner | Evidence |
| --- | --- | --- | --- | --- |
| `BusinessUpdate.UpdateBusinessModules` | `GameplayTweaks`, `AfterProhibitionEconomy` | `AfterProhibitionEconomy` for purchase-stock and empty-business repair; `GameplayTweaks` for Dirty Cash runtime safety | `AfterProhibitionEconomy` | `AfterProhibitionEconomyPlugin` exposes purchase/repair ownership; `GameplayTweaksPlugin.DirtyCashEconomyCompatibility` still patches `UpdateBusinessModules` |
| `BusinessUpdate.Tick` | `GameplayTweaks` | `GameplayTweaks` | `GameplayTweaks` / `AfterProhibitionEconomy` bridge summaries | `BusinessUpdateTickPostfix` still runs safety/audit paths |
| Dirty Cash runtime sweep | `GameplayTweaks`, `AfterProhibitionEconomy` bridge | `GameplayTweaks` | `AfterProhibitionEconomy` | Logs show `dirty-cash-runtime-sweep ... classificationOwner=AfterProhibitionEconomy executionOwner=GameplayTweaks mutationMovedToEconomy=False` |
| Player legal business consumer | `GameplayTweaks`, `AfterProhibitionEconomy` bridge | `GameplayTweaks` | `AfterProhibitionEconomy` | Logs show `player-legal-business-consumer ... classificationOwner=AfterProhibitionEconomy executionOwner=GameplayTweaks mutationMovedToEconomy=False` |

Economy conclusion:

- `AfterProhibitionEconomy` is already the correct home for classification and for purchase-stock / empty-business repair mutation.
- Dirty Cash runtime sweep mutation is broader and touches module update behavior, heat/respect safety, and repeated sweep sources.
- Player legal business consumer mutation is the smaller first migration candidate because `AfterProhibitionEconomy` already exposes `ShouldObservePlayerLegalBusinessConsumer`, runtime summaries, and module/building classifiers.

### Routes

| Method / feature | Current patching DLLs | Mutation owner | Decision owner | Evidence |
| --- | --- | --- | --- | --- |
| `CommandExecutor.OnPlayerTurnStarted` route audit | `AfterProhibitionRoutes`, `GameplayTweaks` diagnostics | `GameplayTweaks` for route state mutation | `AfterProhibitionRoutes` audit/decision bridge | `AfterProhibitionRoutes` patches `CommandExecutor.OnPlayerTurnStarted` for audit only |
| `CommandGoto` / human vehicle route state | `GameplayTweaks` | `GameplayTweaks` | `AfterProhibitionRoutes` decision bridge | Routes reports `ownsVehicleNodeAuthority=False ownsVehicleNodeAuthorityDecision=True` and logs `behaviorFallback=True` |
| Route-simulated access | `GameplayTweaks`, `AfterProhibitionRoutes` bridge | `GameplayTweaks` | `AfterProhibitionRoutes` | Routes reports `ownsRouteSimAccess=False ownsRouteSimAccessDecision=True` |

Routes conclusion:

- Route behavior is intentionally decision-only in `AfterProhibitionRoutes` right now.
- Do not remove GameplayTweaks route fallback during Phase 2.7B. That would be a separate routes migration after economy behavior is quieter.

### UI

| Method / feature | Current patching DLLs | Mutation owner | Decision/classification owner | Evidence |
| --- | --- | --- | --- | --- |
| UI retheme / text sanitizer / crew HUD refresh | `AfterProhibitionUI`, `GameplayTweaks` bridge checks | `AfterProhibitionUI` | `AfterProhibitionUI` | Logs show `uiRetheme=delegated`, `crewHudRefresh=delegated`, `aggroUiRefresh=delegated` |
| Crew pick stale portraits | `AfterProhibitionUI`, `GameplayTweaks` stability guards | Shared guard surface; no gameplay mutation | `AfterProhibitionUI` for visual ownership | `AfterProhibitionUI` owns stale portrait guards; GameplayTweaks still has guard-only pick cleanup for vehicle/custody reconciliation |
| Aggro crew-pick refresh | `AfterProhibitionUI`, `GameplayTweaks` executor bridge | `AfterProhibitionUI` schedules refresh; GameplayTweaks executes compatibility fallback | `AfterProhibitionUI` | `CrewPickAggroRefreshBridge` logs owner `AfterProhibitionUI` |

UI conclusion:

- UI ownership is mostly correct: UI visual work belongs in `AfterProhibitionUI`.
- GameplayTweaks should keep guard-only reconciliation where it protects vehicle/custody state, but should avoid broad visual refresh ownership when `AfterProhibitionUI` is active.

### Family And Politics

| Method / feature | Current patching DLLs | Mutation owner | Classification/decision owner | Evidence |
| --- | --- | --- | --- | --- |
| `PeopleTracker.ProcessBirths` / pregnancy validation | `AfterProhibitionFamily`, `GameplayTweaks` performance guard | `AfterProhibitionFamily` for validation; GameplayTweaks for city-gen performance guard | `AfterProhibitionFamily` | Family logs `ownsPregnancy=True ownsFutureKidValidation=True`; GameplayTweaks guard is city-gen scoped |
| `PeopleTracker.ProcessMarriages` / spouse search | `AfterProhibitionFamily`, `GameplayTweaks` performance guard | `AfterProhibitionFamily` for spouse/family validation; GameplayTweaks for city-gen performance guard | `AfterProhibitionFamily` | Family logs `spouseSearch=delegated fallback=False` |
| `PoliticsManager.OnSystemTurn` and starter quest | `AfterProhibitionPolitics`, `GameplayTweaks` diagnostics | `AfterProhibitionPolitics` | `AfterProhibitionPolitics` | Logs show `PoliticsStarter delegated owner=AfterProhibitionPolitics` |

Family/politics conclusion:

- Do not move GameplayTweaks city-gen birth/marriage performance guards yet. They are performance-specific and scoped to city generation.
- Politics ownership is already clean enough for Phase 2.7; no immediate code move needed there.

## Economy-First Findings

The current live logs show:

- `businessRepair=delegated purchaseStock=delegated`
- `dirtyCashRuntimeSweeps=bridge-available`
- `playerLegalBusinessConsumer=bridge-available`
- `dirty-cash-runtime-sweep ... classificationOwner=AfterProhibitionEconomy executionOwner=GameplayTweaks mutationMovedToEconomy=False`
- `player-legal-business-consumer ... classificationOwner=AfterProhibitionEconomy executionOwner=GameplayTweaks mutationMovedToEconomy=False`

That means stock refresh and empty-business repair are properly owned by `AfterProhibitionEconomy`, while Dirty Cash runtime sweeps and player legal consumer mutation are still intentionally owned by `GameplayTweaks`.

## First Cleanup Applied

`GameplayTweaks` now suppresses repeated zero-work Dirty Cash runtime sweep summaries after the first line per source. This does not move mutation ownership; it only reduces repeated log/bridge noise when there are no candidates and no work.

Expected log change:

- First zero-work sweep per source still logs with `repeatZeroWorkSuppressed=True`.
- Repeated zero-work lines for the same source no longer repeat every turn.
- Non-zero sweep work still logs per day with the existing counts.

## Phase 2.7B Economy Consumer Ownership Move

Status: implemented in `AfterProhibitionEconomy` and `GameplayTweaks`, built in Release.

Target moved:

- Player legal business consumer runtime observation and summary logging.

Files changed:

- `AfterProhibitionEconomy\AfterProhibitionEconomyPlugin.cs`
- `AfterProhibitionEconomy\PlayerLegalBusinessConsumerClassifier.cs`
- `AfterProhibitionEconomy\PlayerLegalBusinessConsumerRuntimePatch.cs`
- `GameplayTweaks\Features\Compatibility\GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs`

Change:

- `AfterProhibitionEconomy` now patches `ConsumerModule.DoUpdate` for player legal business consumer runtime observation.
- `AfterProhibitionEconomy` exposes `OwnsPlayerLegalBusinessConsumerMutation()`.
- `PlayerLegalBusinessConsumerClassifier` now reports:
  - `executionOwner=AfterProhibitionEconomy`
  - `mutationMovedToEconomy=True`
  when the runtime owner config is active.
- `GameplayTweaks` checks `OwnsPlayerLegalBusinessConsumerMutation()` and skips its player-legal consumer branch when the economy DLL owns it.
- Dirty Cash illegal backroom consumer behavior remains in `GameplayTweaks`.

Expected new log markers:

- `player-legal-business-consumer runtime patch applied owner=AfterProhibitionEconomy`
- `player-legal-business-consumer-runtime source=consumer-update ... executionOwner=AfterProhibitionEconomy mutationMovedToEconomy=True`
- `playerLegalBusinessConsumerMutation=True` in the economy ownership summary.

Expected removed/reduced GameplayTweaks marker:

- `EconomyConsumer ... player-legal-business-consumer ... executionOwner=GameplayTweaks mutationMovedToEconomy=False`

Validation:

- `dotnet build AfterProhibitionEconomy\AfterProhibitionEconomy.csproj -c Release`
- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`
- Both builds passed with `0` warnings and `0` errors.

Staged DLLs:

- `Things To Have\Current After Prohibition Mod\BepInEx\plugins\AfterProhibitionEconomy.dll`
- `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`

## Phase 2.7B Dirty Cash Runtime Sweep Ownership Move

Status: implemented in `AfterProhibitionEconomy` and `GameplayTweaks`, built in Release.

Target moved:

- Dirty Cash runtime safety sweep mutation.

Files changed:

- `AfterProhibitionEconomy\AfterProhibitionEconomyPlugin.cs`
- `AfterProhibitionEconomy\DirtyCashRuntimeSweepClassifier.cs`
- `AfterProhibitionEconomy\DirtyCashRuntimeSweepPatch.cs`
- `GameplayTweaks\Features\Compatibility\GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs`
- `GameplayTweaks\Features\Compatibility\GameplayTweaksPlugin.TurnPerformanceDiagnostics.cs`

Change:

- `AfterProhibitionEconomy` now owns the Dirty Cash runtime safety sweep hooks for:
  - `ModulesComponent.DoUpdate`
  - `BusinessUpdate.UpdateBusinessModules`
  - `BusinessUpdate.Tick`
  - `BusinessUpdate.UpdateGamblingModules`
  - `BusinessUpdate.RecalculateHeatAndRespectForNodes`
  - `PlayerFinances.OnGlobalTurnSetAdvanced`
- `AfterProhibitionEconomy` exposes `OwnsDirtyCashRuntimeSweepMutation()`.
- `DirtyCashRuntimeSweepClassifier` now reports:
  - `executionOwner=AfterProhibitionEconomy`
  - `mutationMovedToEconomy=True`
  when the runtime owner config is active.
- `GameplayTweaks` checks `OwnsDirtyCashRuntimeSweepMutation()` and skips its Dirty Cash runtime sweep mutation path when the economy DLL owns it.
- The sliced `BusinessUpdate.UpdateBusinessModules` path now calls Economy begin/complete/end bridge methods directly, because that slicer bypasses the normal Harmony postfix for the stock method.
- `GameplayTweaks` still keeps non-economy compatibility work, turn pacing, and territory reconciliation guards.

Expected new log markers:

- `dirty-cash-runtime-sweep runtime patch applied owner=AfterProhibitionEconomy`
- `dirty-cash-runtime-sweep source=... executionOwner=AfterProhibitionEconomy mutationMovedToEconomy=True`
- `dirtyCashRuntimeSweepMutation=True` in the economy ownership summary.

Expected removed/reduced GameplayTweaks markers:

- `[GameplayTweaks] DirtyCash runtime module safety sweep source=...`
- `dirty-cash-runtime-sweep ... executionOwner=GameplayTweaks mutationMovedToEconomy=False`

Validation:

- `dotnet build AfterProhibitionEconomy\AfterProhibitionEconomy.csproj -c Release`
- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`
- Both builds passed with `0` warnings and `0` errors.

## Performance Split Applied After Audit

`GameplayTweaks` still owns turn pacing. After the audit confirmed `UpdateBusinessModules` itself remained the large frame, the `BusinessTracker` slicer was extended to split that specific phase across owned businesses.

This does not move economy ownership. It preserves:

- Dirty Cash `UpdateBusinessModules` prefix/postfix/finalizer behavior.
- `AfterProhibitionEconomy` purchase-stock refresh completion.
- `AfterProhibitionEconomy` empty-business repair completion.

Expected marker:

- `[PERF][BusinessUpdateModulesSlice]`

## Camera Bounds Guard Applied

`GameplayTweaks` now applies a guard-only camera clamp at `CameraService.SetPosition(CameraPos, CameraTween?)`.

This is not an After Prohibition feature ownership move. It uses the loaded base map config as authority and prevents panning/tweens from pushing the visible screen-corner ground-plane viewport beyond `mapSize`.

Expected markers:

- `CameraBounds camera map-bounds clamp patch applied`
- `CameraBounds camera-position-clamped ...`

## Next Safe Move Candidates

1. Confirm Phase 2.7B in live logs.
   The next game run should show Economy-owned player legal consumer and Dirty Cash runtime sweep markers, with the old GameplayTweaks `executionOwner=GameplayTweaks mutationMovedToEconomy=False` economy sweep markers gone.

2. For routes, change `AfterProhibitionRoutes` from decision-only ownership to full behavior ownership.
   Do this only after turn-end simulation is stable, because route mutation affects input and vehicle state.

3. For UI, keep ownership in `AfterProhibitionUI` and reduce `GameplayTweaks` to guard-only hooks where possible.

## Pass Criteria

- Startup ownership lines identify one mutation owner per feature.
- `behaviorFallback=True` only remains where a feature DLL is explicitly decision-only.
- `executionOwner=GameplayTweaks mutationMovedToEconomy=False` disappears only after the mutation actually moves.
- No behavior is disabled just because a read-only bridge exists.

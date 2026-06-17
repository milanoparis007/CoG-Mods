# Large Save FPS And Responsiveness Phased Plan

## Goal

Improve FPS and responsiveness on large saves without destabilizing turn simulation, route behavior, crew UI, business updates, or ongoing v0.3 combat work.

This plan targets the large save currently showing about `74` players, nearly `10k` human crew candidates, many backrooms/modules, and repeated UI/log churn.

## Current Evidence

From the live large-save log:

- `SimulationManagerSlice phase=total` usually lands around `867-1371 ms` wall time per system turn.
- `BusinessTracker` is the main simulation contributor, commonly around `200-270 ms` CPU.
- `BusinessUpdate.UpdateGamblingModules` often reports `120-190 ms`.
- `human-crew-candidates-lazy-recompute` hit `417 ms` with `9761` candidates.
- `BuildingPickRefresh` route-preview work hit about `166-172 ms`.
- `deferred-alliances-pacts-gangops-wall-total` sometimes reached `941 ms`, `1482 ms`, and once `3244 ms`, while CPU was much lower.
- Repeated Dirty Cash UI/log churn appears around legal-module upgrade refreshes:
  - `Found 40 player-legal modules`
  - `Returning 34/35 modules`
  - `Missing loc key en/module.icons.storage-general.icon`
  - `Missing loc key en/module.icons.loanshark.icon`

## Safety Rules

- Do not change gameplay outcomes just to gain FPS.
- Do not remove business, crew, relationship, or route updates.
- Prefer caching, coalescing, stale-safe UI reads, and frame spreading.
- Keep performance telemetry targeted; do not add new noisy per-frame logs.
- Validate each pass on the same large save before broadening.
- Build only `GameplayTweaks` unless shared contracts change.

## Phase 1 - Baseline And Noise Separation

Purpose: separate true FPS/UI hitches from simulation-turn cost and log spam.

Tasks:

- Confirm live DLL timestamp before every run.
- Capture a short baseline after loading the large save:
  - idle map for 30 seconds
  - open/close crew relations
  - route preview with selected vehicle
  - next-turn sequence for 5 turns
- Track:
  - `BuildingPickRefresh`
  - `HumanTurnPhase`
  - `SimulationManagerSlice`
  - `BusinessTrackerSlice`
  - `UpdateGamblingModules`
  - `human-crew-candidates-*`
  - Dirty Cash legal-module logs
  - missing icon loc spam

Pass criteria:

- We know whether the main player complaint is idle FPS, route/selection hitching, popup/UI hitching, or next-turn slowdown.
- The next implementation pass has one primary target.

## Phase 2 - Low-Risk UI And Log Churn Cleanup

Purpose: reduce repeated work that can hurt frame pacing while UI is open.

Tasks:

- Add/fix localization entries or fallbacks for:
  - `module.icons.storage-general.icon`
  - `module.icons.loanshark.icon`
- Find the Dirty Cash legal-module upgrade refresh path that logs repeated `Found 40 player-legal modules`.
- Cache or suppress repeated upgrade-list scans during the same UI refresh/frame when results are unchanged.
- Keep any user-visible upgrade list behavior unchanged.

Expected impact:

- Lower log spam.
- Less UI refresh churn when inspecting legal businesses/modules.
- Small to medium FPS improvement if the player is interacting with business UI.

Pass criteria:

- Missing icon spam disappears.
- Legal-module scan logs do not repeat dozens of times during one UI refresh.
- Upgrade lists still show correct modules, including loan shark slot caps and gym naming additions.

## Phase 3 - Crew Candidate Hitch Reduction

Purpose: prevent large saves from hitching when the family/hire/crew candidate UI asks for candidate data.

Tasks:

- Inspect existing `PlayerCrewGrowth` candidate deferral in `GameplayTweaksPlugin.TurnPerformanceDiagnostics.cs`.
- Tune candidate-access behavior so existing candidate lists are reused more often.
- Move recompute toward idle/update frames when possible.
- Add a short diagnostic marker only when recompute exceeds a meaningful threshold.

Expected impact:

- Better UI/input responsiveness around crew and hire-family surfaces.
- Reduced large single-frame spikes like `417 ms`.

Pass criteria:

- Candidate access usually reports `human-crew-candidates-stale-served`.
- Any `human-crew-candidates-lazy-recompute` is lower or occurs off the immediate input frame.
- Family counts/hire pools remain accurate.

## Phase 4 - Building Pick And Route Preview Refresh

Purpose: reduce route-preview and selected-vehicle hitches.

Tasks:

- Inspect `BuildingPickRefresh` paths in:
  - `GameplayTweaksPlugin.cs`
  - `GameplayTweaksPlugin.MultiCrewVehicle.cs`
  - `GameplayTweaksPlugin.MultiCrewVehicle.Interaction.cs`
- Coalesce duplicate refreshes for the same vehicle/node/frame.
- Avoid full visible-pick refresh when only the preview target changes and the node set is unchanged.
- Keep travel-finalize and route-queued behavior intact.

Expected impact:

- Better FPS while plotting routes or changing selected vehicles.
- Fewer `BuildingPickRefresh ms=100+` spikes.

Pass criteria:

- Route preview still updates correctly.
- Recent `BuildingPickRefresh` spikes drop from `166-172 ms` or occur less often.
- No regression to owned garage/repair bay visibility.

## Phase 5 - Business And Gambling Update Smoothing

Purpose: reduce repeated turn hitches from large business/module counts.

Tasks:

- Inspect the current slicing around:
  - `BusinessUpdate.UpdateBusinessModules`
  - `BusinessUpdate.UpdateGamblingModules`
  - `RecalculateHeatAndRespectForNodes`
- Determine whether `UpdateGamblingModules` can be sliced or guarded like module updates.
- Check whether backroom-heavy saves repeatedly recompute unchanged gambling/loan shark state.
- Prefer no-op skips and cohorting before changing gameplay timing.

Expected impact:

- Better next-turn frame pacing.
- Lower `BusinessTracker` CPU spikes.

Pass criteria:

- `UpdateGamblingModules` average drops below the current `120-190 ms` range, or is spread across frames.
- Business production, gambling debt, loan shark slot caps, and debt warning behavior still work.

## Phase 6 - GangOps Deferred Wall-Time Audit

Purpose: understand and reduce long deferred wall-time phases without breaking AI pressure.

Tasks:

- Inspect `deferred-alliances-pacts-gangops-wall-total` when it exceeds `900 ms`.
- Compare wall time versus CPU time to identify waiting/deferral versus hot CPU.
- Add more detailed timing only if needed around:
  - AI alliances
  - pact votes/economy peace
  - independent GangOps
  - robbery meeting/contact routing
- Tune budgets or batch sizes only after a clear culprit appears.

Expected impact:

- Smoother human-turn return after AI work.
- Better perceived next-turn responsiveness.

Pass criteria:

- Long wall-total spikes are explained by a named subphase.
- No increase in stuck robbery meetings, delayed gang actions, or war stance/GangOps overlap.

## Phase 7 - Dirty-State Cascade Design

Purpose: improve FPS and turn speed by letting changed systems invalidate only the downstream work that truly depends on them.

This phase should come after the low-risk cleanup passes. A dirty-state cascade can help a lot, but a broad dirty flag such as `all territory changed` or `all businesses changed` can make large saves slower.

Core idea:

1. Mark the smallest changed object dirty:
   - one business
   - one module
   - one node
   - one gang pair
   - one crew list
   - one route/vehicle
   - one UI panel
2. Process only dirty buckets during turn/update work.
3. Trigger downstream refreshes only when the upstream result actually changed.

Candidate buckets:

- `DirtyBusinessModules`
- `DirtyGamblingModules`
- `DirtyRespectNodes`
- `DirtyHeatNodes`
- `DirtyTerritoryColorNodes`
- `DirtyGangPairs`
- `DirtyCrewCandidatePools`
- `DirtyRoutePreviewVehicles`
- `DirtyCrewRelationsPanels`

Example cascades:

- Business module changed production -> dirty business module -> dirty node heat/respect only if output changed -> dirty territory color only if ownership/respect display changed.
- Crew member born/died/hired/fired -> dirty crew candidate pool -> refresh hire-family UI only if currently open.
- Gang war heat changed -> dirty gang pair -> refresh war stance/sit-down UI only for that pair.
- Vehicle route preview changed -> dirty route preview vehicle -> refresh building picks only for affected nodes.

Tasks:

- Map current broad refreshes to possible dirty buckets.
- Start with diagnostics-only dirty logging for the highest-cost paths:
  - business/gambling modules
  - crew candidates
  - building picks/routes
  - territory colors/respect nodes
- Add one bucket at a time; do not introduce a central invalidation framework until at least two buckets prove useful.
- Prefer local dirty sets owned by the system being optimized.
- Add guard logs for accidental broad invalidation.

Expected impact:

- Higher FPS when UI or map overlays would otherwise rebuild unchanged state.
- Faster large-save turns by skipping unchanged business, relationship, and map work.
- Cleaner performance reasoning because each expensive refresh has an upstream cause.

Pass criteria:

- At least one expensive repeated path is converted from broad refresh to narrow dirty refresh.
- Logs show changed object counts, not full-world refreshes.
- No stale UI state after business, crew, route, or gang-war changes.
- No gameplay regression from skipped updates.

## Phase 8 - Large Save Verification Pass

Purpose: verify that FPS work is actually helping the target save.

Test matrix:

- Load the large save cold.
- Idle on the map for 30 seconds.
- Open legal business UI with storage/general and loan shark modules visible.
- Open crew relations and hire-family surfaces.
- Preview and queue several routes.
- Advance 5-10 turns.
- Trigger one AI/player hostile situation if safe.

Success markers:

- Fewer repeated missing loc and legal-module scan logs.
- No `human-crew-candidates-lazy-recompute` input-frame spike near `400 ms`.
- `BuildingPickRefresh` spikes are lower or less frequent.
- `BusinessTracker` and `UpdateGamblingModules` trend lower or are smoother.
- No gameplay regressions in business UI, family UI, route selection, or weapon stance.

## Current Priority

Start later with Phase 1 and Phase 2.

The best first implementation pass is the low-risk UI/log churn cleanup because it has small blast radius and helps clarify whether the remaining FPS issue is true CPU work or repeated UI refresh noise.

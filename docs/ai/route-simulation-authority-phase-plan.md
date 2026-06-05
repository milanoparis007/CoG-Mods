# Route Simulation Authority Phase Plan

## Goal

Reduce babysitting from long-distance multi-vehicle travel without returning to false corner presence. The route system should know the difference between:

- physical presence: the vehicle is actually at the node;
- active segment target: the node the vehicle is currently driving toward;
- final route goal: the planned destination after chained movement;
- simulated convenience access: low-risk player actions allowed because the player has committed the vehicle to that route.

## Current Problems

- Long routes can feel like the vehicle is moving back and forth because UI preview state and physical node state compete.
- Business glow and corner picks can imply arrival before the vehicle is physically there.
- Some actions are too strict for the new multi-vehicle flow, forcing the player to wait and babysit every segment.
- Other actions must remain strict because remote access would create exploits or misleading combat state.

## Phase 1: Stabilize Route Authority

Status: started.

- Treat the active segment node as the display authority while a vehicle is moving.
- Stop refreshing business/corner glow previews against remote final goals during active travel.
- Keep final-goal previews out of physical-presence checks.
- Add logs for route-node authority decisions so stream runs show which source authorized an action.

Validation signals:

- `travel-start-business-preview-skipped`
- `route-resume-business-preview-skipped`
- `active-segment-target`
- no false business glow while vehicle is still in transit.

## Phase 2: Route-Simulated Convenience Actions

Status: started; Phase 2A healing implemented.

Allow safe, player-only actions when the selected/assigned vehicle is actively routed to the correct destination.

Initial whitelist:

- heal wounded vehicle occupants when the route is committed to a safehouse or controlled building;
- open low-risk player/crew management style interactions that do not transfer goods or start combat;
- allow shop purchase conversations only when the route is committed to that shop/building and the action does not require vehicle storage transfer.

Keep strict physical-only:

- attacks, ambushes, hostile crew interactions, and cop/federal combat;
- owned-building inventory transfers, loading, unloading, and storage mutation;
- shop purchases that immediately pull from or push into vehicle cargo;
- business module install/remove flows that depend on physical storage or vehicle cargo.

Validation signals:

- `route-sim-access`
- `route-sim-heal`
- blocked logs for strict actions still saying `vehicle-not-physical`.

## Phase 3: Route-Simulated Conversations

Status: started; Phase 3A business/civic conversation authority implemented.

Open conversation UI from committed route destinations without letting enemy or combat options leak.

Rules:

- friendly or neutral shop/business conversations can open from route commitment;
- enemy crew and hostile on-map actions require physical arrival;
- conversation button callbacks get a second backend guard for transfer/combat actions;
- buy/sell callbacks get a physical vehicle guard so route-sim conversations cannot move shop goods while the car is still driving;
- stale portraits and wrong-corner picks are cleared if the selected route changes.

Validation signals:

- `route-sim-convo`
- `buy-sell-vehicle-physical-blocked`
- `route-shop-stage-blocked` while staging UI is not implemented yet;
- route-simulated convo opens only for the route destination;
- enemy crew convo remains blocked until physical arrival;
- buy/sell picker and backend shop transaction stay blocked until actual vehicle arrival;
- no lingering portrait after route cancel, route change, or popup close.

## Phase 4: Shop Purchase Bypass

Status: implemented for route-in staged buy/sell and arrival commit; current work is validation/UX polish, not first implementation.

Allow route-committed purchases where the game normally requires the crew to be on the shop corner, without instantly mutating inventory before arrival.

Detailed design: [route-in-out-shop-staging-design.md](route-in-out-shop-staging-design.md)

Rules:

- route-in can open the shop and stage buy/sell additions for the committed destination;
- staged buy/sell entries do not move cash, goods, or vehicle cargo until the vehicle actually reaches the shop node;
- route-out, route cancel, destination change, vehicle switch, or failed arrival clears staged buy/sell entries before they execute;
- clean-cash-only purchases can be considered for instant execution later, but vehicle-cargo purchases need either physical arrival or an explicit delayed-delivery queue;
- if delayed, the purchased items should arrive when the route reaches the shop node, not instantly;
- failed/changed routes must refund or cancel cleanly.

Validation signals:

- purchase logs include `route-sim-shop`;
- staged order logs include `route-shop-staged`, `route-shop-committed`, and `route-shop-cleared`;
- no goods appear in vehicle cargo before the route reaches the shop unless the design explicitly allows instant purchase.

## Phase 5: AI and Player Route Simulation

Status: validation pass active; Phase 5A duplicate same-route command discard implemented; Phase 5B log dedupe and route-out diagnostics implemented; Phase 5C mid-drive destination queue implemented; Phase 5D immediate-reverse resume bridge implemented and live-validated; Phase 5E route-owned automation nullref guard implemented with no latest nullref reproduction; Phase 5F queued-resume watchdog diagnostics implemented; Phase 5G start-driving duplicate/defer guard implemented; Phase 5H resume-deferred diagnostics clarified; Phase 5I delivery automation queue pump implemented; Phase 5J delivery requeue/pump guard live-validated; Phase 5K active-route delivery pump guard implemented; Phase 5L expected-arrival confirmation guard implemented; Phase 5M same-frame delivery pump reentry guard implemented; Phase 5N pump-started target physical guard implemented; Phase 5O strict finalizer and pump-guard cleanup implemented; Phase 5P mobile-arrival pump-guard cleanup implemented; Phase 5Q observed-arrival pump-guard cleanup implemented; Phase 5R unresolved pump-target command-start gate implemented; Phase 5S post-arrival automation-step budget implemented; Phase 5T settled command-start pump-guard release implemented; Phase 5U expected-arrival observed marker implemented; Phase 5V observed-source finalizer logging implemented; Phase 5W finalize-override reason logging implemented; Phase 5X travel-end UI refresh isolation and contextual failure logging implemented; Phase 5Y stale building-pick click guard implemented.

Improve simulation so player and AI vehicles do not need per-node babysitting.

- Continue queued routes across turns without command churn.
- Rate-limit repeated command deferrals for the same route.
- Keep AI deliveries using the same route authority model where possible.
- Add stuck-route detection and recovery for vehicles repeatedly targeting the same two nodes.

Validation signals:

- fewer repeated `route-command-deferred` lines for identical routes;
- duplicate active-route commands log `route-duplicate-command-discarded` and `command-path-duplicate-discarded`;
- route-out shop attempts log `route-out-wrong-shop` with the current authoritative vehicle node;
- repeated physical-only owned-business and peep/live suppression diagnostics are deduped;
- mid-drive replacement destinations log `route-command-queued-after-arrival` and continue after the active segment reaches its expected node;
- queued resumes that would immediately drive back to the previous segment start log `route-resume-freebridge` when bridged to the final queued node;
- stale delivery automation steps owned by an active mod route log `automation-step-nullref-swallowed` instead of surfacing a vanilla `CommandAutomationStep.OnStarted` nullref;
- no alternating stale/finalize source loops;
- queued resume attempts log `route-resume-watchdog` before path rebuild, with committed node/source and final goal;
- vanilla same-route `StartDriving` callbacks during an active queued resume log `start-driving-duplicate-discarded` instead of interrupting the mod-owned segment;
- committed intermediate-hop waits log `route-resume-deferred reason=segment-committed-awaiting-resume` instead of looking like a same-node final-goal loop;
- delivery route arrivals can pump the queued delivery command/next automation step after the vehicle physically reaches the stop, preserving vanilla-style multiple front/resource actions within one turn when AP and movement remain;
- delivery routes can repeat legitimate same-day legs without `requeue-suppressed`, while the arrival pump waits instead of advancing unrelated route steps when a queued resume is still pending;
- delivery arrival pumps log `delivery-route-pump-wait reason=active-route-*` and stop once a new route segment is active, so fast turn-ending cannot advance delivery automation ahead of the vehicle's physical segment;
- travel-end and turn-start route finalizers only commit an active segment's expected node after live node or vehicle/driver world-position snap confirms physical arrival, otherwise they log `travel-finalize-deferred` or `turnstart-arrival-deferred`;
- if a delivery arrival pump starts a new travel segment, same-frame reentry logs `delivery-route-pump-wait reason=recent-active-route-same-frame` and waits for a later physical callback before pumping the next delivery step;
- after a delivery pump starts a new travel segment, subsequent delivery pumps for that vehicle wait until the pump-started target is strictly physically reached; premature reentry logs `delivery-route-pump-wait reason=recent-active-route-await-physical`;
- expected-node finalization only accepts the mobile-reported target, or strict physical position when mobile has no valid node, so board/agent logical node changes cannot finalize delivery route stops ahead of the visible vehicle;
- stale delivery pump guards clear when a queued route resumes or when a later strictly physical final node supersedes the old stored pump target; cleanup logs `delivery-route-pump-guard-cleared`;
- delivery pump guards also clear after the existing `vehicle-node-reached` path has observed mobile arrival for the guard target, so cleanup no longer trusts a fresh mobile query that can run ahead in the same callback; cleanup logs `delivery-route-pump-guard-cleared reason=target-reached ... source=observed-mobile`;
- while a delivery pump guard is still waiting for its target, new human vehicle travel commands for that vehicle defer at path validation or start-driving instead of starting from an unobserved logical node; diagnostics log `delivery-route-command-deferred` or `delivery-route-start-deferred`;
- a delivery arrival pump runs at most one non-route automation step after queue processing unless that step starts or queues a route segment; additional same-stop delivery work waits for a later callback/turn and logs `delivery-route-pump-wait reason=automation-step-budget`;
- if a guarded target later becomes the stable command start node with no active route and no queued resume, the command gate clears the stale pump guard instead of deferring every future turn; cleanup logs `delivery-route-pump-guard-cleared reason=settled-command-start`;
- expected-arrival confirmation records an observed mobile arrival before finalization and delivery pumping continue; diagnostics log `delivery-route-arrival-confirmed`;
- travel-end finalizer diagnostics carry the same-frame observed mobile arrival source forward instead of reporting a synthetic `source=expected` after a confirmed mobile arrival;
- travel-end override diagnostics include `source=` and `reason=` so observed-arrival corrections can be separated from unobserved expected-authority corrections;
- travel-end route finalization and delivery automation continue even if HUD/building-pick refresh fails; diagnostics log `travel-finalize-ui-refresh-failed`, and any remaining travel-end exception logs vehicle, final node, pending start/expected/goal, active state, and queued state;
- stale building-pick clicks during route/selection churn do not fall through to vanilla `BuildingPick.OnClick` with missing pick data; diagnostics log `building-pick-click-guard` with building, node, selected vehicle, route state, and exception type;
- vehicle route resumes continue after load.

Current pass notes:

- The latest live log showed the `buy-sell-vehicle-physical-blocked` line after the vehicle had finalized at `NID_491` while the attempted shop access nodes were `NID_1473,NID_1330`; this is expected route-out blocking, not a route-in failure.
- `route-shop-stage-miss` now reports `currentNode` and `currentSource`, and uses `route-out-wrong-shop` when the vehicle is physically/authoritatively elsewhere.
- Deferred territory refresh warnings now include the operation name and exception type so the next index error can be tied to the specific refresh step.
- A different destination command issued while the vehicle is still driving is now stored as the route's queued final goal instead of being deferred and lost when the current segment completes.
- Latest validation showed Phase 3A behaving as intended: `route-sim-convo` opened only for the committed business node, hostile preview crew remained blocked, and buy/sell stayed physical-only on route-out/wrong-shop attempts.
- Latest validation also showed the queued route still stepped backward from `NID_541` to `NID_426` before continuing to `NID_1473`; Phase 5D now bridges that immediate reverse case to the final queued node when a valid full route can be built.
- Latest validation confirmed `route-resume-freebridge` fired and routed from `NID_541` straight to `NID_1473`. The remaining route-adjacent error was a vanilla `CommandAutomationStep.OnStarted` nullref during human turn-start while mod-owned routes were active, so Phase 5E suppresses that specific route-owned stale automation nullref and logs its peep/vehicle/building context.
- Latest live pass with the staged/live `GameplayTweaks.dll` showed `route-command-queued-after-arrival`, `route-resume-freebridge`, `resume-segment-rebased`, `route-duplicate-command-discarded`, and `command-path-duplicate-discarded` in the expected order.
- The same pass did not reproduce `CommandAutomationStep.OnStarted` nullrefs or `automation-step-nullref-swallowed`, which means Phase 5E either prevented the stale automation step or the failing turn path did not fire.
- `presence-preview-blocked` and `building-node-preview-blocked` still appeared for selected vehicles that had not physically arrived. Treat these as expected strict-arrival behavior unless a route-simulated convenience action should have been allowed there.
- 2026-05-06 22:39 long-run log pass confirmed a queued delivery-style route resumed from `NID_426` to `NID_1473`, discarded duplicate same-route commands, and reached `[At corner]` without a back-and-forth loop. A later command queued `NID_1347` while the vehicle was still traveling toward `NID_1176`; the log ended with that resume still pending, so it should be checked in the next run before adding new recovery logic.
- 2026-05-07 morning log did not continue that exact pending delivery route; it appeared to start from a different save/session state. The next pass adds `route-resume-watchdog` so future logs show whether a queued final goal was still present at turn-start, what committed node it used, and whether resume then built, deferred, or blocked.
- 2026-05-07 follow-up log showed the watchdog working and exposed a concrete handoff fault: after `route-resume vehicle=64424634273 startNode=NID_1020 nextGoal=NID_1330 finalGoal=NID_541`, a vanilla same-final-goal `StartDriving` callback interrupted the queued-resume segment, committed the vehicle back to `NID_1020`, and created a degenerate `expectedNode=NID_1020` segment. Phase 5G mirrors the command-path duplicate/defer guard in the start-driving path to discard same-route replacements before they can interrupt queued resume.
- 2026-05-07 next-pass log confirmed Phase 5G is active: the same `NID_1020 -> NID_1330 -> NID_491 -> NID_541` route discarded duplicate vanilla same-route callbacks with `start-driving-duplicate-discarded`/`command-path-duplicate-discarded`, and no new `start-driving-interrupt-active` was present in the route tail. The remaining `same-node-final-goal` line was a misleading intermediate-hop wait, so Phase 5H renames that reason to `segment-committed-awaiting-resume` when the final goal is still ahead.
- 2026-05-07 delivery validation pass found why routes felt limited to one front per turn: vanilla automation can logically advance to the next delivery step in the same turn after `CommandGoto` moves the agent node, but the route-authority fix waits for physical vehicle arrival. Phase 5I adds an arrival-time delivery automation pump that processes the queued route command and, when no command remains and AP is still available, runs the next active delivery step for that same vehicle. Validation logs: `delivery-route-queue-pump`, `delivery-route-automation-pump`, and `delivery-route-pump-summary`.
- 2026-05-07 follow-up log validated Phase 5I but exposed two delivery-specific edge cases: repeated same-day delivery legs could be cleared as `requeue-suppressed`, and the arrival pump could advance several automation steps while a queued resume was still pending. Phase 5J allows repeated same-day requeues for active delivery automation and adds `delivery-route-pump-wait` so the pump pauses when a queued resume owns the next move.
- 2026-05-07 latest live validation confirms Phase 5J is loaded and behaving: active delivery automation now logs `route-requeue-allowed reason=delivery-automation`, queued-resume boundaries log `delivery-route-pump-wait`, and the route resumes afterward with `route-resume` instead of being cleared by `requeue-suppressed`.
- 2026-05-07 fast turn-ending validation then exposed a Phase 5K fault: stale/rapid travel-end callbacks could re-enter the delivery arrival pump while a new route segment was already active, causing multiple `delivery-route-automation-pump` entries and route steps to advance ahead of the vehicle's physical node. Phase 5K adds entry/loop/after-queue/after-automation active-route guards and explicit `delivery-route-pump-wait reason=active-route-*` diagnostics.
- 2026-05-07 evening validation loaded Phase 5K and confirmed the pump guard fired, but delivery routes still skipped because the upstream finalizer promoted `ExpectedNodeID` before physical/mobile arrival caught up. Example pattern: `delivery-route-queue-pump node=NID_1347` followed immediately by `vehicle-node-reached node=NID_1176`. Phase 5L adds expected-arrival confirmation to both travel-end and turn-start finalization before the expected node can become the committed route node.
- 2026-05-07 follow-up validation loaded Phase 5L and showed the finalization path improved, but the delivery pump could still reenter within the same callback/frame after starting a new segment, walking multiple stops through `delivery-route-automation-pump` before the visible `vehicle-node-reached` logs caught up. Phase 5M records the frame when a delivery pump starts a segment and suppresses same-frame pump reentry for that vehicle.
- 2026-05-08 validation loaded Phase 5M but showed the remaining reentry was not same-frame; delivery automation pumped the next stop a few lines before `vehicle-node-reached` confirmed that stop. Phase 5N stores the pump-started target node and blocks future pump attempts for that vehicle until `IsHumanVehicleStrictlyPhysicalAtNode` confirms the target.
- 2026-05-08 validation loaded Phase 5N and confirmed `delivery-route-pump-wait reason=recent-active-route-await-physical`, but the old pump target could stay as `NID_1176` after the route resumed and the expected-node finalizer still accepted logical board/agent authority. Phase 5O removes board/agent/live-authority finalizer confirmation when mobile is reporting another node, and clears the delivery pump guard on queued route resume or strict physical supersession.
- 2026-05-08 validation loaded Phase 5O and confirmed `delivery-route-pump-guard-cleared reason=queued-route-resume`. Remaining waits happened after `vehicle-node-reached` had already reported the same target node, meaning strict world-position snapping could lag behind mobile arrival. Phase 5P lets mobile authority clear the pump guard with `delivery-route-pump-guard-cleared reason=target-reached ... source=mobile` while keeping expected-node finalization strict.
- 2026-05-08 validation loaded Phase 5P and confirmed `delivery-route-pump-guard-cleared reason=target-reached ... source=mobile`, but it also showed the raw mobile query could clear a pump guard before the corresponding `vehicle-node-reached` line. Phase 5Q records mobile arrivals only inside `IsHumanVehiclePhysicallyAtNode` when it emits `vehicle-node-reached`, then allows pump-guard cleanup from that observed marker if it is at or after the guarded segment's start frame.
- 2026-05-08 validation loaded Phase 5Q and confirmed `delivery-route-pump-guard-cleared reason=target-reached ... source=observed-mobile` ordering, but later route commands could still start from a guarded target before that target's observed arrival. Phase 5R defers command path/start-driving while a pump guard remains unresolved, so the route cannot start from the unobserved logical target.
- 2026-05-08 validation loaded Phase 5R and confirmed repeated `delivery-route-command-deferred reason=await-pump-target` before `vehicle-node-reached` and observed guard cleanup, with no early `human-start-preserved` for the guarded node. The remaining route-adjacent churn was a final-stop delivery pump that ran five non-route automation steps in one callback (`delivery-route-pump-summary ... automationPumps=5 routeActive=False routeQueued=False`). Phase 5S caps that path to one automation step per arrival pump.
- 2026-05-08 validation loaded Phase 5S and confirmed the final-stop batch became `delivery-route-pump-wait reason=automation-step-budget automationPumps=1` with `delivery-route-pump-summary ... automationPumps=1 routeActive=False routeQueued=False`. A later route then got stuck because the strict pump guard kept waiting for an observed `vehicle-node-reached` at `NID_1176` even though the route label had settled to `At corner node=NID_1176` and future commands started from that same node. Phase 5T keeps same-callback pump release strict, but lets a later command attempt clear the guard when the guarded target is already the stable command start node and no route/resume owns the vehicle.
- 2026-05-08 validation loaded Phase 5T and confirmed `delivery-route-pump-guard-cleared reason=settled-command-start targetNode=NID_1176 source=mobile` followed by route progress. Remaining log ambiguity showed delivery queue pumps at an expected node before the later `vehicle-node-reached` diagnostic for that node, even though expected-arrival confirmation had already accepted the mobile target. Phase 5U records the observed mobile marker and logs `delivery-route-arrival-confirmed` inside expected-arrival confirmation before finalization/pump processing continues.
- 2026-05-08 validation loaded Phase 5U and confirmed `delivery-route-arrival-confirmed` appears before `delivery-route-queue-pump`, with no repeated stale-guard deferrals or multi-step final-stop automation batches. Remaining ambiguity is diagnostic: `travel-finalize-source` can still say `source=expected` immediately after a same-frame mobile confirmation. Phase 5V carries the observed mobile source into the finalizer source log.
- 2026-05-08 validation loaded Phase 5V and confirmed `travel-finalize-source ... source=observed-mobile`. Remaining override lines were still ambiguous because `travel-finalize-override` did not include the source/reason that justified the correction. Phase 5W adds `source=... reason=observed-arrival|expected-authority` to that diagnostic.
- 2026-05-08 validation loaded Phase 5W and confirmed `travel-finalize-override ... source=observed-mobile reason=observed-arrival`, with delivery pump summaries staying at `automationPumps=1`. The new route-adjacent signal is a bare `HumanVehicleTravelEndSyncPatch: Object reference not set to an instance of an object` immediately before a PickManager `ArgumentOutOfRangeException` near shutdown. Phase 5X isolates the travel-end HUD/building-pick refresh from route finalization and adds route context to any remaining travel-end exception.
- 2026-05-08 14:04 validation loaded Phase 5X in bin, staged, and live DLLs. The log no longer showed the bare `HumanVehicleTravelEndSyncPatch` warning or PickManager crash. The delivery route repeatedly advanced with observed arrivals and `automationPumps=1`, ending at `[At corner]`. The attached clip matched the log labels (`[In route]`, `[Arriving]`, `[At corner]`). The remaining route-adjacent UI warning was `BuildingPick.OnClick guard refresh failed: Object reference not set to an instance of an object`; Phase 5Y makes stale pick-data refresh failures swallow the stale click and logs `building-pick-click-guard` with context.
- Non-route warnings still present in the log: `refresh-outpost-target-respect` `ArgumentOutOfRangeException` during deferred territory refresh, plus CopKilling advisor nullrefs already swallowed by its stabilization guard.

## Phase 6: Optional UX Controls

Status: complete for current scope; Phase 6A crew-sidebar route-mode labels implemented for muscle, delivery, and unassigned-vehicle cards. Phase 6A validation diagnostics added with deduped `route-mode-label` logs and confirmed for `[At corner]`, `[Arriving]`, and `[In route]`. Phase 6B config toggle implemented for route-simulated convenience actions, with both the default-on and config-off paths live-validated.

Expose the system clearly once stable.

- Add a small route-mode indicator on vehicle cards. Implemented as bracketed text appended to the existing card description: `[In route]`, `[Arriving]`, or `[At corner]`.
- Add a toggle for route-simulated convenience actions. Implemented as `VehicleRouteSimulation.EnableRouteSimulatedConvenienceActions`; when disabled, route-simulated business/civic destination conversations and shop staging are blocked before physical arrival, while normal physical-arrival actions still work.
- Consider player-facing tooltips for “In route” versus “At corner.”
- Add future search/filter inputs for route resources and shop resources after the action authority is stable.

Validation signals:

- muscle crew cards in a vehicle show route status beside the existing action/movement line;
- delivery cards show route status beside the delivery route name;
- unassigned vehicle cards show route status beside their idle line;
- active long-route cards should show `[In route]` when there is still a queued final goal and `[Arriving]` when the active segment is the final goal;
- idle vehicles with an authoritative node show `[At corner]`.
- validation logs include deduped `route-mode-label` lines with vehicle, label, start node, expected node, final goal, and resume state.
- Latest live validation confirmed `route-mode-label` for `At corner`, `Arriving`, and `In route`, including queued resume state after `route-command-queued-after-arrival`.
- Latest live validation with the Phase 6B build active confirmed default-on route-sim convenience still opens `route-sim-convo`, opens the route-shop staging picker, commits with `route-shop-committed`, and transitions route labels through queued resume and final arrival.
- If route-sim convenience is disabled in config, logs include deduped `route-sim-disabled` and route-in destination conversations/staging remain blocked until physical arrival.
- 2026-05-06 21:49 video/log validation confirmed the route-mode labels and authority split in a visible run: old/departure shop buy/sell stayed blocked as `route-out-wrong-shop`, while the later committed destination opened route-in staging and committed on arrival.
- 2026-05-06 22:28 log validation confirmed the config-off path: `route-sim-disabled` blocked route-in business conversation and shop staging before arrival, `route-target-mismatch` and `route-out-wrong-shop` stayed strict, and route-mode labels continued to update through `[Arriving]`, `[In route]`, and `[At corner]`.

## Immediate Next Step

Phase 5Y is live and the latest log shows no old `BuildingPick.OnClick guard refresh failed`, no `HumanVehicleTravelEndSyncPatch` warning, and no PickManager crash. Route progression is stable enough to move the next validation pass back to route-in shop buy/sell. Route-in buy/sell is already implemented as staged orders that commit on arrival, but the latest video/log showed the Shift-click destination path only selected the target building and did not start the shop conversation. The next build should validate the Shift-click conversation bridge: route to a shop destination, Shift-click/open that committed destination while en route, confirm `route-in-shift-convo-opened`, then use the buy/sell option to stage one or more entries. Expected shop logs: `route-in-shift-destination-open`, `route-sim-convo`, `route-in-shift-convo-opened`, `route-shop-stage-opened`, `route-shop-staged`, then on physical/observed arrival `route-shop-commit-start`, `route-shop-commit-gate-bypassed`, `route-shop-committed`, and `route-shop-cleared reason=committed`.

Keep watching the route safety markers during the shop test: `delivery-route-arrival-confirmed` before queue pumps, `delivery-route-pump-summary ... automationPumps=1`, `travel-finalize-source source=observed-mobile`, and `travel-finalize-override source=observed-mobile reason=observed-arrival`. Treat any continuing PickManager `ArgumentOutOfRangeException` as the next UI-specific cleanup item unless route or shop diagnostics regress at the same time.

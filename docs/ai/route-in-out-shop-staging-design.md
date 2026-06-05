# Route-In / Route-Out Shop Staging Design

## Goal

Let route-simulated shop conversations reduce travel babysitting without allowing instant remote inventory mutation.

The intended behavior is:

- route-in opens the shop UI for the committed route destination;
- buy/sell choices can be staged while the vehicle is still driving in;
- no cash, goods, vehicle cargo, or shop stock changes until arrival;
- destination business glow, automatic pop-ups, and map highlight state are not arrival authority while the vehicle is en route;
- route-out, route cancel, route destination change, vehicle switch, or failed arrival clears staged choices.

## Current Guardrail

Current implementation blocks immediate shop buy/sell if the visit vehicle is not physically at the shop:

- `ConvoCallbacks.ShowBuySellPopup` is guarded before the picker opens.
- `BuySellUtils.ExecuteHumanBuySell(VisitState...)` is guarded as a backend safety net.
- `BuySellUtils.ExecuteHumanBuySell(CrewAssignment...)` is guarded as a backend safety net.
- validation log: `buy-sell-vehicle-physical-blocked`.

Keep this guard until staged buy/sell has its own UI and arrival commit path.

## Route State Model

Use vehicle-scoped staged orders:

- `VehicleID`: vehicle that owns the staged order.
- `DriverPeepID`: driver when the order was staged.
- `BuildingID`: shop/building being visited.
- `BizID`: business entity if available.
- `DestinationNodeID`: route-in destination node required for commit.
- `ResourceID`: item being bought or sold.
- `QtyAndDir`: intended quantity and direction.
- `PriceSnapshot`: negotiated unit price and total money delta at staging time, used for diagnostics while the commit still re-prices through vanilla buy/sell.
- `CreatedTurn`: game turn/date for diagnostics and stale cleanup.

The staged order is valid only while all are true:

- the same vehicle is still selected/assigned to the same driver;
- the active route expected or final node still matches the staged destination;
- the building/business still exists and is still interactable;
- the vehicle has not route-outed, cancelled, switched destination, switched vehicle, or arrived somewhere else.

## Route-In

Route-in means the vehicle is actively traveling to the shop node.

Allowed:

- open friendly/neutral business conversation;
- open a custom staging picker for buy/sell;
- add, replace, or clear staged buy/sell entries for that vehicle/shop pair.
- let the player intentionally pick the committed next-destination business, including via modified click flows such as Shift-click if that is the cleanest UI path.

Blocked:

- direct calls to vanilla `BuySellUtils.ExecuteHumanBuySell`;
- vehicle cargo mutation;
- shop stock mutation;
- cash mutation;
- enemy, attack, raid, or combat callbacks.
- automatic destination business pop-ups or glow/highlight behavior that could make the vehicle look physically present before arrival.

Validation logs:

- `route-shop-stage-opened`
- `route-shop-staged`
- `route-shop-replaced`
- `route-shop-stage-blocked`

Important distinction: a vehicle leaving one node and heading to another is route-out for the old/departure node, but route-in for the committed next destination. The next destination may open the friendly/neutral business conversation and stage buy/sell choices if it is the active expected/final route target.

Policy answer: when a vehicle is en route, the old/departure business should not remain usable. The committed next-destination business may be intentionally opened for business conversation and staged buy/sell, but the purchase/sale is held until strict physical arrival at that same destination or a validated frontage/access node. This keeps the node ID stable for commit, avoids remote inventory mutation, and removes the need to use business glow or automatic pop-ups as proof of arrival.

This route-in convenience can be disabled with `VehicleRouteSimulation.EnableRouteSimulatedConvenienceActions=false`. When disabled, next-destination business/civic conversations and shop staging wait for physical arrival.

## Arrival Commit

When the vehicle physically reaches the destination node:

1. Re-resolve the vehicle, driver, building, business, and resource.
2. Re-check physical presence with `IsHumanVehicleStrictlyPhysicalAtNode`.
3. Re-check current inventory, stock, and cash constraints.
4. Execute the vanilla buy/sell once if still valid.
5. Clear the staged order.

Validation logs:

- `route-shop-commit-start`
- `route-shop-committed`
- `route-shop-commit-blocked`
- `route-shop-cleared reason=committed`

## Route-Out

Route-out means the vehicle leaves the staged shop target before commit or receives a new route that no longer points at the staged shop.

Route-out does not allow continued purchase, sale, storage transfer, combat, enemy crew conversation, or other physical-presence actions at the old/departure business. Those old-node actions stay blocked once the vehicle has left. If the player picks the next destination business while the vehicle is driving there, that is handled by the route-in rules above.

Clear staged orders on:

- `ClearPendingHumanVehicleTravel`;
- new `MarkHumanVehicleTravelActive` for the same vehicle where final/expected node no longer matches;
- driver switch;
- vehicle switch;
- vehicle removed/destroyed;
- route resume invalidation;
- arrival at a different node.

Validation logs:

- `route-shop-cleared reason=route-out`
- `route-shop-cleared reason=route-cancel`
- `route-shop-cleared reason=destination-changed`
- `route-shop-cleared reason=vehicle-switch`
- `route-shop-cleared reason=arrival-mismatch`

## Implementation Phases

### Phase 1: Physical Guard Baseline

Status: implemented.

Keep direct buy/sell, storage transfer, combat, hostile crew conversation, and old-node actions tied to strict physical vehicle presence. This phase prevents route preview/final-goal state from mutating cash, cargo, stock, or enemy action state.

Pass criteria:

- old/departure shop access logs `route-out-wrong-shop` or `route-target-mismatch`;
- backend `BuySellUtils.ExecuteHumanBuySell` calls cannot bypass the physical gate;
- no vehicle cargo, shop stock, or cash changes before physical arrival.

### Phase 2: Route-In Conversation Authority

Status: implemented; needs live validation with `VehicleRouteSimulation.EnableRouteSimulatedConvenienceActions=true`.

Allow the committed next-destination friendly/neutral business or civic conversation to open while the vehicle is en route. Do not restore business glow, automatic pop-ups, or map highlight authority. Intentional player access may come from normal destination selection or a future modified click path, but the authority check must remain the active route expected/final node.

Pass criteria:

- next-destination business conversation logs `route-sim-convo`;
- old/departure business conversation remains blocked;
- hostile, combat, raid, storage, and cargo actions still require physical presence;
- startup log shows `routeSimConvenienceEnabled=True`, otherwise this phase is intentionally disabled by config.

### Phase 3: Route-In Staging Picker

Status: implemented for one-shot human buy/sell orders.

Replace immediate route-in buy/sell execution with a custom staging picker. The picker stores the intended transaction for the selected vehicle and destination, then returns to the conversation without changing inventory or money.

Pass criteria:

- route-in buy/sell opens `route-shop-stage-opened`;
- valid selections log `route-shop-staged`;
- replacement selections log `route-shop-replaced`;
- empty/cancelled selections log `route-shop-stage-rejected` or `route-shop-stage-cancelled`;
- no cash, goods, or stock changes at staging time.

### Phase 4: Arrival Commit

Status: implemented for one-shot human staged buy/sell orders.

Commit the staged transaction only after strict physical arrival at the staged destination or a validated shop frontage/access node. Re-resolve driver, building, business, resource, current stock, current vehicle inventory, and current cash before executing vanilla buy/sell once.

Pass criteria:

- arrival logs `route-shop-commit-start`;
- valid orders log `route-shop-committed` and clear as `reason=committed`;
- stale/cannot-afford/out-of-stock orders log `route-shop-commit-blocked` and clear without partial mutation;
- `route-shop-commit-gate-bypassed` appears only inside prevalidated staged commit execution.

### Phase 5: Route Change, Arrival Gap, And Save/Load Cleanup

Status: implemented and validated for the current regression set.

Preserve staged orders across internal route node transitions that still target the same shop frontage/access set. Clear staged state on route-out, destination change, vehicle switch, driver mismatch, cancellation, load, and new-game reset. Treat staged orders as transient runtime state, not saved gameplay state.

Pass criteria:

- internal frontage transitions log `route-shop-order-preserved`;
- logical-arrival/physical-authority gaps log `route-shop-commit-deferred` then retry through `route-shop-commit-retry`;
- load/new game cleanup logs `route-shop-state-cleared` if there was leftover state;
- stale route-in memories expire or clear with `route-shop-routein-cleared`.

### Phase 6: UX And Config Validation

Status: next validation phase; startup config logging implemented in this pass.

Make the route-in policy obvious in logs and future UI decisions. The build should clearly show whether route-in convenience is enabled, and future UI work should prefer an intentional next-destination action path over automatic glow/pop-up behavior.

Pass criteria:

- startup verification log includes `routeSimConvenienceEnabled=True/False`;
- when disabled, route-in attempts log `route-sim-disabled` and are treated as config-disabled, not failed route authority;
- when enabled, destination access validates through `route-sim-convo`, `route-shop-stage-opened`, `route-shop-staged`, and eventual `route-shop-committed`;
- no destination glow or automatic business pop-up is required for route-in access.

### Phase 7: Optional Follow-Up Enhancements

Status: Phase 7A implemented for multiple staged orders per vehicle; Phase 7B implemented for Shift-click route-in destination opening; remaining items planned.

These are deliberately outside the current build-complete bar, but can be pulled in as focused passes:

- Phase 7A: allow multiple staged orders per vehicle/shop instead of one replacement order. Status: implemented; new staged orders now replace only the same shop/resource/direction slot, while different resources can remain queued together for arrival commit.
- Phase 7B: add an explicit Shift-click or route destination action if normal picking is too hidden. Status: implemented as a narrow Shift-click path on business building picks; it only opens when the selected human vehicle is actively routed to that building's route-in access node, then normal route-sim conversation and staged buy/sell guards still apply.
- Phase 7C: add resource search/filtering to route/delivery/shop pickers. Status: first delivery-editor pass implemented and UI cleanup applied; route setup now adds a compact item filter inside the existing item-label area instead of overlaying the action/resource dropdowns, filters by display name/list name/resource debug text, preserves the selected item when it remains visible, and logs `DeliveryResourceFilter filter="..." before=... after=...`. Shop route-in staging still uses the vanilla one-resource quantity panel, so there is no multi-resource shop picker to filter yet.
- Phase 7D: decide whether staged prices are locked or re-priced on arrival. Status: implemented as explicit re-price-on-arrival policy with diagnostics; staged orders now log `unitPriceSnapshot`, `moneyDeltaSnapshot`, and `pricePolicy=repriced-on-arrival`, and arrival commit logs `route-shop-price-drift` if the current vanilla price differs from the staged quote before executing at the current price.

## Current Validation

Status: passed for the current route-in/route-out access regression set and validated for staged-order route-in buy/sell commit. Phase 7A is implemented and needs live validation for multiple different resources staged before arrival.

- 2026-05-07 live validation with `VehicleRouteSimulation.EnableRouteSimulatedConvenienceActions=true` showed route-in shop staging working end to end: `route-sim-convo`, `route-shop-stage-opened`, `route-shop-staged`, `route-shop-commit-immediate`, `route-shop-commit-gate-bypassed`, `route-shop-committed`, and `route-shop-cleared reason=committed`.
- 2026-05-07 follow-up live validation confirmed the Phase 7A build was live and single-order staging still commits cleanly with the new multi-order storage: `route-shop-staged pendingOrders=1`, `route-shop-commit-start`, `route-shop-committed`, and `route-shop-cleared remainingOrders=0`. Multi-order validation is still pending; stage two different resources or buy/sell directions for the same destination before arrival and expect `pendingOrders=2`, followed by separate `route-shop-commit-start` and `route-shop-committed` entries for each resource on arrival.
- 2026-05-07 11:11 video/log validation confirmed Phase 7B is live: Shift-click route-in destination access logged `route-in-shift-destination-open`, opened `route-sim-convo`, reached `route-shop-stage-opened`, staged `furniture` and `half-barrels`, and committed both orders on arrival. The later `route-shop-stage-miss reason=route-out-wrong-shop` / `buy-sell-vehicle-physical-blocked` entries are expected old-shop physical guard rejections after the vehicle was no longer at the prior shop access nodes.
- Phase 7C first delivery-editor pass is build-complete and needs live validation by opening delivery route setup, typing into the item filter, confirming the dropdown narrows without losing the selected resource when it still matches, and checking for `DeliveryResourceFilter filter="..." before=... after=...`.
- 2026-05-07 12:38 video/log validation confirmed the delivery filter works (`filter="b"` narrowed 92 to 91, `filter="br"` narrowed 92 to 4) but showed the original filter field overlaying the item dropdown area. Follow-up UI cleanup moved the filter into `Edit Panel/Item/Text`, the unused item-label slot, so it no longer competes with the action/resource dropdowns, amount selector, route step buttons, or OK/delete controls. Needs live visual validation with the 12:44 build.
- Phase 7D price policy diagnostics are build-complete and need live validation by staging a route-in shop order and confirming `route-shop-staged` includes `unitPriceSnapshot`, `moneyDeltaSnapshot`, and `pricePolicy=repriced-on-arrival`. If relationship/discount/price changes before arrival, expect `route-shop-price-drift`; otherwise no drift log is expected.
- Latest validation logs show no `route-shop-stage-miss` and no `buy-sell-vehicle-physical-blocked`.
- Latest route-out policy is confirmed: old/departure node actions remain physical-only, while the committed next destination can open route-sim business conversation and stage buy/sell as route-in.
- Settled-node access is active through `vehicle-node-settled-action`.
- Owned-business inventory and shop access no longer report false route-out failures in the latest checked run.
- Latest route-shop validation logs show `route-shop-commit-gate-bypassed` followed by two `route-shop-committed` entries, with committed orders clearing as `reason=committed`.
- 2026-05-08 latest route stability pass confirmed the Phase 5Y live DLL and showed `route-in-shift-destination-open` followed by observed arrival at that destination. No `route-shop-stage-opened` or `route-shop-staged` lines appeared because Shift-click selected the destination building but did not start the shop conversation. The next pass wires route-in Shift-click to open the business conversation with the selected vehicle driver; validation should show `route-in-shift-convo-opened`, then the buy/sell option should be available and selecting it should log `route-shop-stage-opened`.
- 2026-05-06 log pass found `route-shop-stage-miss reason=route-out-wrong-shop` immediately after `route-shop-routein-preserved`: the remembered route-in conversation node was not one of the shop frontage/access nodes. The next pass now bridges preserved conversation nodes to shop-frontage physical validation and attempts immediate commit when the staged order is created after the segment-arrival callback already fired.
- Follow-up 2026-05-06 log pass showed the bridge working through `route-shop-routein-bridged`, `route-shop-stage-opened`, and `route-shop-staged`, but the order was cleared as `destination-changed` when the internal route advanced from the remembered conversation node to a shop frontage node. The next pass preserves staged orders when the new route node is still one of the staged shop's access/frontage nodes.
- Follow-up 2026-05-06 evening log pass showed `route-shop-order-preserved` working and `route-shop-commit-start` firing, but strict physical lookup returned `none` during the `segment-arrived` callback. The next pass treats that as a transient arrival-authority gap: keep the staged order alive, then retry commit during pending-travel clear with `recent-finalize-arrival` authority.
- Follow-up retry log showed commit reached vanilla execution with `recent-finalize-arrival`, but the buy/sell physical gate intercepted the internal `BuySellUtils.ExecuteHumanBuySell` call and forced `vanilla-execute-failed`. The next pass adds a scoped gate bypass only while a prevalidated staged order is executing.
- Final 2026-05-06 validation log showed two staged route-in orders committing successfully after the scoped gate bypass.
- Final cleanup pass makes route-shop staging transient-only: staged orders are not written to mod save data and any leftover staged orders, remembered route-in nodes, or open route-shop pickers clear on load/new-game reset with `route-shop-state-cleared` if there was state to remove.
- 2026-05-06 21:49 video/log validation confirmed the strict route-out policy: a buy/sell attempt against shop access nodes `NID_1178,NID_1020` was correctly blocked after the vehicle had finalized at `NID_119` with `reason=route-out-wrong-shop`. The same run later confirmed route-in staging at the committed destination `NID_1176` with `route-shop-staged` and `route-shop-committed`.
- 2026-05-06 22:28 config-off validation confirmed `VehicleRouteSimulation.EnableRouteSimulatedConvenienceActions=false` blocks route-in shop staging before physical arrival with `route-sim-disabled`, while old/departure and wrong-target shops remain blocked by `route-target-mismatch` or `route-out-wrong-shop`.

## Implemented Hooks

- `route-shop-stage-blocked`: buy/sell was attempted from a route-in shop conversation, but the staging picker could not be opened for the current button/context.
- `route-shop-stage-opened`: route-in buy/sell opened the staged picker.
- `route-shop-stage-rejected`: picker OK did not create an order, usually because quantity was empty or the context became invalid.
- `route-shop-stage-cancelled`: player cancelled the staged picker.
- `route-shop-stage-abandoned`: route-out or destination change happened while the staged picker was open.
- `route-shop-routein-remembered`: route-sim business conversation remembered a route-in destination for later shop staging.
- `route-shop-routein-preserved`: remembered route-in destination survived segment completion so the picker can stage during the logical-arrival/visual-arrival gap.
- `route-shop-routein-bridged`: remembered route-in conversation node did not match the shop frontage/access nodes, so staging used the remembered conversation node while commit still validates physical presence at the route node or shop frontage.
- `route-shop-stage-miss`: buy/sell was blocked and staging did not open; includes route expected/final nodes and whether the miss was no route-in state, route target mismatch, or unresolved route-in state.
- `route-shop-staged`: staged order manager accepted a future order from the route-in picker.
- `route-shop-replaced`: a new staged order for the same vehicle replaced an older staged order before arrival.
- `route-shop-order-preserved`: segment-start changed the active route node, but the new node still targets the staged shop's frontage/access node, so the staged order remains live.
- `route-shop-cleared`: staged order was cleared by route-out, route cancel, destination change, arrival mismatch, or disabled commit.
- `route-shop-commit-start`: vehicle reached the staged node and the staged order started revalidation.
- `route-shop-commit-immediate`: order was staged after the arrival callback had already run, and the vehicle was already physically at the route node or a matching shop frontage node, so commit was attempted immediately.
- `route-shop-commit-deferred`: order was staged after route-in memory, but the vehicle was still not physically at the route node or shop frontage node.
- `route-shop-commit-retry`: segment-arrived could not validate physical presence yet, so pending-travel clear retried the staged order after route authority finalized.
- `route-shop-commit-gate-bypassed`: the staged-order commit is already validated by route-shop commit checks, so the generic human buy/sell physical gate allowed the internal vanilla execute call.
- `route-shop-commit-node-bridged`: commit used strict physical presence at a shop frontage node while the logical route destination was the remembered route conversation node.
- `route-shop-committed`: staged order passed strict physical arrival, driver/building/business/resource checks, and executed the vanilla one-shot buy/sell path.
- `route-shop-commit-blocked`: vehicle reached the staged node but commit revalidation failed; the log reason now reports the failed check.
- `route-shop-state-cleared`: load/new-game cleanup removed transient staged route-shop state instead of carrying it across saves.

## Open Risks

- Vanilla `ShowBuySellPopup` uses a local callback that immediately executes `BuySellUtils`, so route-in staging should not reuse that callback directly.
- Price changes between staging and arrival need a decision: lock the staged price or re-price on arrival.
- If stock/cash changes before arrival, the commit must clamp, fail, or ask again. Initial recommendation: fail and clear, with a log.
- Scheduled/automation buy/sell should remain separate from human route-in staging.
- Arrival commit now uses current price, stock, cargo, and cash state through vanilla `BuySellUtils.ExecuteHumanBuySell`; if the vanilla call refuses the order, the staged order is cleared with `reason=vanilla-execute-failed`.

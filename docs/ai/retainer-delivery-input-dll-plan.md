# Retainer, Delivery Input, and Resource Search DLL Plan

## Current Pass

- Harden lawyer retainers so the player must have enough safehouse cash before the retainer is increased.
- Done now: political bribes and judge bribes also require enough clean safehouse cash before they activate.
- Stop hostile crew conversation/action UI from opening when the player vehicle is only routed to the enemy corner but has not arrived.
- Clear stale on-map crew-pick text during reset/hide paths to reduce lingering portrait artifacts after invalid enemy vehicle selections.
- Keep residential event work tied to the staged `ResEvents.sim` fix; the live game file still needs the staged copy before the runtime test is meaningful.
- Added large dirty-cash source logs because `$10,000` can be legitimate output from authored dirty-cash production recipes, and future logs need to show the exact source.

## Phase 1: Retainer Payment Guards

- Disable the Add Retainer button when the player safehouse cannot pay the next retainer amount.
- Before any cash leaves the safehouse, verify `CanChangeMoneyOnSafehouse(-amount)`.
- Only increase the retainer after the cash spend is allowed.
- Log blocked attempts with `Retainer payment-blocked` so stream logs show whether the UI guard or backend guard fired.

## Phase 2: Retainer 10k and Custom Input

- Add fixed retainer increment buttons: `$1k`, `$10k`, and max affordable.
- Done now: `$1k` and `$10k` buttons share the safehouse-cash guard and backend payment validation.
- Add a custom amount input that clamps to positive whole dollars and the player safehouse balance.
- Prevent repeated empty payments, negative payments, and double-click overpayment.
- Keep confirm-retainer separate from add-retainer so players cannot accidentally spend while toggling confirmation.

## Phase 3: Delivery Route Custom Input DLL

- Replace fragile delivery quantity prompts with a mod-owned numeric input panel.
- Support route item steps such as `+1`, `+10`, `+100`, and `max`, with `10k` support for money-like route amounts where capacity allows it.
- Clamp requested route amounts to available source inventory, destination capacity, vehicle capacity, and any route-specific game constraints.
- Log every rejected route amount with the route id, item id, requested amount, clamp amount, and reason.
- Preserve keyboard/mouse focus so typing into the input box does not also trigger map hotkeys.

## Phase 4: Resource Search

- Add a small text filter to resource pickers used by delivery route setup.
- Search by localized display name first, then fallback resource id/debug id.
- Preserve selected resource when the filter changes if it is still visible.
- Debounce filtering so large modded resource lists do not hitch the UI.

## Phase 5: Residential Events Verification

- Copy the staged fixed `ResEvents.sim` into the live Steam StreamingAssets folder with admin permission.
- Re-run the residential favor test and check for `ResEvents picker-hidden` or event availability logs.
- If the data fix is active but events still do not show, add diagnostics for target selection, residence ownership, off-map targets, and event gate methods.

### 2026-05-08 Phase 5 Preflight

- Live `GameplayTweaks.dll` matches the staged release DLL, so the route-in shop/conversation hotfix is active in the current runtime.
- At preflight time, live `ResEvents.sim` was still stale and did not match the staged fixed file:
  - staged: `89973` bytes, SHA256 `ECDF86A37BB2122216E9D4132C659600E15C39D7A1FE1A70C629D80D54E89C70`
  - live Steam: `90122` bytes, SHA256 `2C08699CCC8EB4B89ABC2F46F1B40A662D3C91FCB936C26ABBB470AEED518A5B`
- Normal copy to the live Steam `StreamingAssets\Settings\ResEvents.sim` failed with Windows access denied; the file was later added manually and verified in the diagnostics pass below.
- Current log confirms the residential diagnostics patch loaded with `[VERIFY-HOTFIX] [ResEvents] stability diagnostics applied patched=5`, but there are no `ResEvents picker-hidden` or residential favor availability logs yet.

### 2026-05-08 Phase 5 Diagnostics Pass

- Live `ResEvents.sim` now matches the staged fixed file: `89973` bytes, SHA256 `ECDF86A37BB2122216E9D4132C659600E15C39D7A1FE1A70C629D80D54E89C70`.
- Current logs confirm the residential candidate diagnostic patch loaded with `patched=6`.
- The first residential favor test now produces `ResEvents candidate-available ... event=dinner-party`, so the fixed live `ResEvents.sim` is producing a valid event candidate.
- Added `ResEventManager.FindPossibleResEventToCreate` diagnostics so the next run logs either:
  - `ResEvents candidate-available ... host=... building=... event=...`
  - `ResEvents candidate-missing ... reason=...`
- Missing-candidate reasons now include host availability, owner business building, nearby event-space search count, global reserved/active/off-node event-space counts, and event visreq pass counts.
- Built and staged `GameplayTweaks.dll` with this diagnostics pass. Copy the staged DLL into the live BepInEx plugin folder before the next residential favor test.

### 2026-05-08 Phase 5 Lifecycle Diagnostics

- Added follow-up lifecycle markers for the post-candidate path:
  - `ResEvents host-assigned ...`
  - `ResEvents attendance-registered ...`
  - `ResEvents attendance-done ...`
  - `ResEvents result-processed ...`
- Built and staged `GameplayTweaks.dll` with lifecycle diagnostics. Live still has the prior `candidate-available` diagnostics build until the staged DLL is copied into the live BepInEx plugin folder.
- Follow-up log check confirmed the lifecycle diagnostics build is now live: `[VERIFY-HOTFIX] [ResEvents] stability diagnostics applied patched=10`.
- The current log has no `candidate-available`, `candidate-missing`, `host-assigned`, attendance, or result markers after the `patched=10` startup line, so the residential favor path has not been exercised in the current run yet.
- Next log check still shows the same state: live DLL and live `ResEvents.sim` match staged, `patched=10` is loaded, and there are no current-run residential candidate/lifecycle markers. Continue by opening the residential favor conversation/options path in game; do not add more residential code until that path emits either candidate or lifecycle markers.

### 2026-05-08 Phase 5 Residential Ticker Fix

- User-facing issue: residential events could exist without an obvious top-screen ticker, leaving no clear way to know an event had been created or was happening.
- Root cause from decompiled `ResEventManager`: vanilla only posts `tickerinfo` from `RestartEvent`, and only posts `tickerstart` when `IsHappeningThisTurn && IsAttendanceRegistered`. A newly assigned residential host is not guaranteed to surface a ticker immediately.
- Added a host-assignment fallback that starts the event schedule immediately when the new `ResEventData` is still in its reset/expired state, then posts the localized event `tickerinfo`.
- Added a system-turn fallback that posts localized `tickerstart` for registered residential events happening this turn, throttled by building/event/state/turn.
- New validation markers:
  - `ResEvents schedule-started source=host-assigned ...`
  - `ResEvents ticker-posted source=host-assigned-started state=info ...`
  - `ResEvents ticker-posted source=attendance-registered ...`
  - `ResEvents ticker-posted source=system-turn-this-turn state=start ...`
- Built and staged `GameplayTweaks.dll` with this ticker fix. Live still has the prior lifecycle diagnostics build until the staged DLL is copied into the live BepInEx plugin folder.

### 2026-05-08 Phase 5 Residential Ticker Widening

- Follow-up log showed the ticker build was live with `patched=11`, but no `schedule-started` or `ticker-posted` marker fired. That means no residential event state matched the narrow assignment/registered-this-turn conditions.
- Widened the system-turn scan to post one top ticker for existing active residential events that are:
  - `IsComingSoon`, using `tickerinfo`
  - `IsThisTurn`, using `tickerstart`, even if attendance is not registered
- Changed ticker suppression from once-per-turn to once-per residential event stage using building, event id, start/end turns, state, and attendance. This avoids spam while still surfacing already-existing scheduled events after loading.
- Additional validation markers:
  - `ResEvents ticker-posted source=system-turn-coming-soon state=info ...`
  - `ResEvents ticker-posted source=system-turn-this-turn-unregistered state=start ...`
- Built and staged `GameplayTweaks.dll` with the widened ticker scan. Live still has the prior ticker build until the staged DLL is copied into the live BepInEx plugin folder.

### 2026-05-08 Phase 5 Ticker Visibility Diagnostics

- Follow-up logs still only showed `[VERIFY-HOTFIX] [ResEvents] stability diagnostics applied patched=11`; no `candidate-available`, `candidate-missing`, `host-assigned`, `schedule-started`, `ticker-posted`, or lifecycle markers fired in the current/previous runtime logs.
- Added a compact system-turn scan marker so the next run can distinguish "no active residential event state exists" from "active event exists but no ticker was posted":
  - `ResEvents ticker-scan source=system-turn turn=... active=... comingSoon=... thisTurn=... registeredThisTurn=... posted=... globalSpaces=... globalActive=...`
- Added a candidate fallback top ticker when `FindPossibleResEventToCreate` returns a valid residential event candidate:
  - `ResEvents candidate-ticker-posted source=candidate-available ...`
  - `ResEvents candidate-available ... tickerPosted=True`
- Built and staged `GameplayTweaks.dll` with hash `B3BEE26FC65CF333713B61EA752741A8F87856566BD090ACD6C875A751799B94`. Live Steam plugin still has the previous hash `A179EC297C01FD14F9EBAD8BBBBB6216579754AC0552B37C7622F1E2DDEDD5B3`, so copy the staged DLL into the live BepInEx plugin folder before retesting.

### 2026-05-08 Phase 5 Human-Turn Ticker Scan Hook

- Follow-up logs showed staged and live Steam `GameplayTweaks.dll` both matched `B3BEE26FC65CF333713B61EA752741A8F87856566BD090ACD6C875A751799B94`, but the current log still had no `ticker-scan`, `candidate-*`, `host-assigned`, or `ticker-posted` markers.
- The log did show human turn-start hooks firing from the dirty-cash/outpost patch path, while `ResEventManager.OnSystemTurn` produced no scan markers. Added a residential ticker scan postfix on `PlayerInfo.OnPlayerTurnStarted` for the human player.
- Next validation marker should now appear on every human turn start, even when there are no active events:
  - `ResEvents ticker-scan source=human-turn-start turn=... active=0 ... globalSpaces=... globalActive=...`
- New build/staged DLL hash: `DE21F2D7CCE331A7CE860B0E630E59F043BF0CFDC58B47BA733AC227C15173B9`. Live Steam plugin still has `B3BEE26FC65CF333713B61EA752741A8F87856566BD090ACD6C875A751799B94`, so copy the staged DLL into the live BepInEx plugin folder before the next ticker/res-event test.

### 2026-05-08 Phase 5 Ticker Log Pass After Human-Turn Hook

- Latest log check still reports `[VERIFY-HOTFIX] [ResEvents] stability diagnostics applied patched=11`; the expected `patched=12` startup marker is not live yet.
- Build output and staged release DLL remain `DE21F2D7CCE331A7CE860B0E630E59F043BF0CFDC58B47BA733AC227C15173B9`, but the live Steam BepInEx plugin is still `B3BEE26FC65CF333713B61EA752741A8F87856566BD090ACD6C875A751799B94`.
- No `ticker-scan`, `candidate-*`, `host-assigned`, `schedule-started`, `ticker-posted`, attendance, or result markers are present because the game has not loaded the new human-turn scan hook.
- Continue by copying the staged DLL to the live Steam BepInEx plugin folder, restarting the game, then advancing one human turn. The first useful validation line is `ResEvents ticker-scan source=human-turn-start ...`.

### 2026-05-08 Phase 5 Expired Event Restart Pass

- Latest log check confirms the human-turn scan hook is live with `[VERIFY-HOTFIX] [ResEvents] stability diagnostics applied patched=12`.
- Human-turn scan now shows real residential event state:
  - `ticker-scan source=human-turn-start turn=149 active=8 ... expired=8 ... posted=0`
  - repeated through later turns, so eight active residential event hosts are stuck expired and never become `comingSoon` or `thisTurn`.
- A residential favor path did fire:
  - `candidate-ticker-posted source=candidate-available event=dinner-party ... tickerPosted=True`
  - `schedule-started source=host-assigned ... skipped=True`
  - `host-assigned ... scheduleStarted=True tickerPosted=False`
- Interpretation: the top ticker path works for the candidate notice. The assigned dinner party rolled the event's configured skip chance (`probskip { value 0.75 }`), so no event ticker was posted because the event was skipped. Separately, expired active hosts need a restart fallback because the vanilla system-turn restart path is not clearing them in this runtime.
- Added a human-turn restart fallback for expired active residential events. When the scan finds `data.IsExpired`, it now restarts the event schedule with the same `ResEventData.Start(waitTurns, skipped)` path, then posts the normal localized info ticker when the restarted event is not skipped.
- New validation markers:
  - `schedule-started source=human-turn-start-expired-restart ... skipped=False`
  - `ticker-posted source=human-turn-start-coming-soon state=info ...`
  - `ticker-scan source=human-turn-start ... expired=... restartedExpired=... comingSoon=... posted=...`
- Built and staged `GameplayTweaks.dll` with hash `DC4AE9ACC4DD16B93EAD1621DE0D9D4A0D6EFDA2FABFF23B96DBB13B55DB4F9E`. Live Steam plugin still has `DE21F2D7CCE331A7CE860B0E630E59F043BF0CFDC58B47BA733AC227C15173B9`, so copy the staged DLL live before the next expired-event ticker test.

## Phase 6: Scheme Vehicle Suspension And Duplicate Guard

### 2026-05-08 Phase 6 Setup

- New user-facing issue: when a scheme removes the acting crew member from the map, a duplicate vehicle can appear if that crew member was part of a multi-crew/passenger vehicle state.
- Current residential status before switching focus: build, staged release, and live Steam `GameplayTweaks.dll` all match `DC4AE9ACC4DD16B93EAD1621DE0D9D4A0D6EFDA2FABFF23B96DBB13B55DB4F9E`, so the expired-event restart fix is now live. The next residential check should look for `restartedExpired=...` and `ticker-posted source=human-turn-start-coming-soon ...`.
- `Schemes.sim` startup definitions commonly require the scheme actor to be assigned to a vehicle with `check-crew-peep-assigned to vehicle target npc`. That means scheme starts intentionally begin from an in-vehicle crew state.
- Decompiled `PlayerScheme.StartSchemeForCrew` starts the scheme and immediately runs the first chapter script for `crew.Id`; later `ProcessDecision` runs grants such as `arrest-crew` and `kill-crew`. Those paths can remove only the scheme actor while the multi-crew vehicle authority may still believe the actor belongs to the vehicle.
- Revised policy after reviewing the scheme vehicle requirement: keep the scheme actor attached to the vehicle assignment at scheme start, but suspend route/vehicle visual sync for that actor while scheme state places them off-map. Reattach/reconcile the existing vehicle link when the scheme state returns the actor to normal map play. Do not make passengers follow as helpers yet, because vanilla scheme data tracks one `crewAssigned` and chapter requirements/grants are written for one actor.

### Phase 6 Scheme Script Model

- `AI.sim` defines two scheme movement families:
  - `move-to-target-and-wait` runs `goto target-building` and then `wait-turns target-number`.
  - `teleport-to-target` runs `teleport-to target-node`.
  - `leave-board` runs `remove-from-board`.
  - `return-board` runs `return-to-board`.
- `PlayerScheme.StartCurrentChapter` runs the chapter `script` through `ScriptDispatcher.RunScript`. If the chapter has a selected target, it passes `targetBuilding` and `number=1`.
- `PlayerScheme.OnDecisionPop` flushes the actor command queue and runs the chapter `recoveryScript`. If the chapter had a target, it passes `targetBuilding`, `targetNode`, and `number=1`.
- `ScriptDispatcher` converts these script steps into queued player commands:
  - `goto` becomes `CommandGoto`, which moves the actor's assigned vehicle along a driving path.
  - `teleport-to` becomes `CommandTeleportTo`, which sets the actor at the target node and calls `TeleportCarToNode` for the actor's assigned vehicle.
  - `remove-from-board` and `return-to-board` become `CommandBoardCallback`.
- The duplicate-vehicle path is specifically the board-removal family. `CommandBoardCallback` calls `PlayerCrew.RemoveCrewFromBoard(targetPeep, OffBoardReason.Scheme, removeCar: true)`. Vanilla then stores the vehicle template and destroys or unassigns the current vehicle. `PlayerCrew.ReturnCrewToBoard` sees the stored template and calls `CreateVehicleAndAssignCrew` at headquarters.
- Target-travel chapters are different. `move-to-target-and-wait` and `teleport-to-target` keep using the actor's assigned vehicle; they need vehicle/passenger reconciliation around teleports, but they should not be treated like off-board removal unless logs show the actor is also placed in `CrewOffBoard`.

### Phase 6 Implementation Plan

- Add scheme-start diagnostics around `PlayerScheme.StartSchemeForCrew`:
  - `SchemeVehicle scheme-start actor=... vehicle=... occupantsBefore=...`
  - Include scheme id, current node, vehicle node, and whether the actor was driver or passenger.
- Add command-level diagnostics for the two script families:
  - `scheme-board-remove-start` and `scheme-board-return-start` around `CommandBoardCallback` / `RemoveCrewFromBoard` / `ReturnCrewToBoard`.
  - `scheme-target-move-start` and `scheme-target-teleport` around `CommandGoto` / `CommandTeleportTo` when the actor is in a multi-crew vehicle.
- For `leave-board` / `return-board` chapters:
  - capture the actor's current vehicle id, vehicle template, vehicle node, route ownership, representative/driver status, and occupant list before vanilla removal;
  - mark the actor as `scheme-vehicle-suspended` when `OffBoardReason.Scheme` removal begins;
  - block multi-crew route/vehicle repair code from creating or syncing a replacement vehicle for that suspended actor;
  - on return, prefer reattaching the actor to the original still-valid vehicle instead of allowing an extra HQ-created vehicle to persist;
  - if vanilla already created an HQ vehicle for the returning actor, reconcile to one authority by keeping the original multi-crew vehicle when valid and removing only the duplicate assignment path from our state.
- For `move-to-target-and-wait` / `teleport-to-target` chapters:
  - allow the assigned vehicle to travel with the actor because vanilla `CommandGoto` and `CommandTeleportTo` are vehicle-aware;
  - after teleport recovery, verify the actor, vehicle, and other occupants agree on the same node;
  - add a repair pass only if logs show passengers split from the vehicle or route ownership still points to the pre-teleport node.
- Add scheme outcome reconciliation after `PlayerScheme.ProcessDecision` and `EndSchemeForCrew`:
  - if the actor is still in scheme/off-map state, keep route/repair sync suspended and do not spawn a duplicate vehicle;
  - if the actor returns to board play, reconcile them back to the original vehicle link or clear the suspended link if the original vehicle no longer exists;
  - if the actor is dead, arrested, in Canada, or permanently off-board, clear stale route ownership and clear only the actor's suspended vehicle link;
  - log `scheme-vehicle-suspended`, `scheme-vehicle-reattached`, `scheme-vehicle-suspension-cleared`, or `scheme-duplicate-spawn-blocked`.
- Keep `Schemes.sim` content unchanged for now. The data file's vehicle requirement is useful because schemes are launched from field crews; the bug is the transition between in-vehicle state and off-map scheme state, not the requirement itself.

### 2026-05-08 Diagnostics Pass

- Added `SchemeVehicleDiagnosticsPatch` in `GameplayTweaks` and registered it with the vehicle/politics feature gate.
- Initial diagnostics were behavior-only observers. Follow-up prevention now changes only the `return-board` path for human multi-occupant scheme vehicles where the original vehicle still has another live occupant.
- New log channel: `[VERIFY-HOTFIX] [SchemeVehicle]`.
- Startup/chapter/recovery markers:
  - `diagnostics applied patched=...`
  - `scheme-start ...`
  - `scheme-chapter-start ...`
  - `scheme-decision-recovery ...`
- Board-removal markers for `leave-board` / `return-board`:
  - `scheme-board-remove-start`
  - `scheme-board-remove-finish`
  - `scheme-remove-from-board-start`
  - `scheme-remove-from-board-finish`
  - `scheme-board-return-start`
  - `scheme-board-return-finish`
  - `scheme-return-to-board-start`
  - `scheme-return-to-board-finish`
- Target-travel markers for `move-to-target-and-wait` / `teleport-to-target`:
  - `scheme-target-move-start`
  - `scheme-target-move-finish`
  - `scheme-target-teleport-start`
  - `scheme-target-teleport-finish`
- Each marker includes scheme id, chapter id, script/recovery script, peep id, vehicle id, vehicle template, peep node, vehicle node/source, goal node, occupant count/list, passenger count, slot count, driver id, driver status, on-board status, pending/active/queued route flags, pending route nodes, and off-board reason/template when available.
- Log evidence from the first live diagnostics run:
  - `burglar-man` / `burglar-talk` started with actor `4295089775` as a passenger in vehicle `4295092012`; occupants were `4295092011,4295089775`.
  - `scheme-remove-from-board-start ... removeCar=True` stored `vehicle-car` and removed the actor from board play.
  - `scheme-return-to-board-finish` then assigned the actor to a new vehicle such as `47244765136`, later `90194438095` and `2774548998063`, proving vanilla `return-board` was repeatedly creating replacement cars at return.
- Added duplicate prevention for that exact path:
  - record `scheme-vehicle-suspended` before scheme board removal when a human multi-occupant vehicle is involved;
  - on return, clear the off-board `vehTemplate` before vanilla `ReturnCrewToBoard` can call `CreateVehicleAndAssignCrew`;
  - after vanilla removes the off-board record, reattach the actor to the original still-valid vehicle by metadata and sync the actor to the vehicle node;
  - leave solo schemes and schemes whose original vehicle no longer exists on the vanilla return path.
- New prevention validation markers:
  - `scheme-vehicle-suspended`
  - `scheme-duplicate-spawn-blocked`
  - `scheme-vehicle-reattached`
  - `scheme-duplicate-spawn-block-skipped`
  - `scheme-vehicle-reattach-skipped`
- Build/stage result: `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed, and staged `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll` matches release build hash `8CB15B3B08BF82F5CF791078479060CBE994CDAD9284A70B9D84F5FBBD7B55DC`.

### 2026-05-08 Scheme Prevention Validation

- Live Steam `GameplayTweaks.dll`, staged DLL, and release build all match hash `8CB15B3B08BF82F5CF791078479060CBE994CDAD9284A70B9D84F5FBBD7B55DC`.
- Current log confirms the prevention path worked for `top-man` chapters:
  - `scheme-vehicle-suspended peep=4295089775 vehicle=4295092012 occupants=2`
  - `scheme-duplicate-spawn-blocked peep=4295089775 vehicle=4295092012 reason=cleared-template-vehicle-car`
  - `scheme-vehicle-reattached peep=4295089775 vehicle=4295092012 driver=4295092011`
  - final return snapshots keep `vehicle=4295092012`, `occupants=2`, `passengers=1`, `driver=4295092011`, and `isDriver=False`.
- This validates the intended behavior for passenger scheme actors: no new HQ car is created, the original driver stays driver, and the actor returns as passenger in the original car.
- Follow-up to watch: target-travel recovery can briefly log `scheme-target-teleport-finish` with actor node at the target while `vehicleNodeSource=recent-finalize` still reports the old node. Later chapter/return lines show the vehicle and occupants reconciled, so this is not yet a blocker, but keep it in the next log review.

### Phase 6 Validation Checks

- Start a scheme with a solo driver in a vehicle: actor remains linked to the original vehicle assignment, vehicle sync is suspended while off-map, and no duplicate vehicle appears.
- Start a scheme with actor plus one or more passengers:
  - actor remains the scheme actor and does not create a helper party;
  - passengers remain in the original vehicle;
  - vehicle representative/driver remains valid or is reconciled only if the actor is permanently removed.
- Resolve scheme branches that remove the actor from the map, especially `arrest-crew` and `kill-crew` grants in `Schemes.sim`.
- Confirm no route or delivery-route ownership creates a duplicate for the suspended actor:
  - no duplicate vehicle spawn;
  - no `repair trigger=travel-end ... mismatches=...` caused by the scheme actor;
  - no stale `route-owned` or `representative-swap` loop for the removed actor.
- Re-check residential ticker logs in the same pass:
  - `ticker-scan source=human-turn-start ... restartedExpired=...`
  - `ticker-posted source=human-turn-start-coming-soon state=info ...` when a restarted event does not skip.

### 2026-05-08 One Active Scheme Per Vehicle Guard

- Added a conservative start guard on `PlayerScheme.StartSchemeForCrew` for human multi-crew vehicle state.
- If the selected crew member's vehicle already has another occupant in an active scheme, the new scheme start is blocked before vanilla starts the scheme.
- The guard also checks suspended off-map scheme actors recorded by the duplicate-vehicle prevention path, so a second crew member cannot start a scheme from the same vehicle while the first actor is temporarily removed from the board.
- Stale suspended records are cleared when the actor is no longer in a scheme.
- New validation marker:
  - `scheme-start-blocked peep=... vehicle=... scheme=... reason=vehicle-active-scheme activePeep=... source=occupant|suspended`
- Player-facing fallback message:
  - `That vehicle already has a crew member on a scheme.`
- Build/stage result: `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed, and staged `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll` matches release build hash `B7DA1D9256A9046492C392F6A57E22A3A9F0521B3FB0663E6079906B786D0CD4`.

## Phase 7: Boss Crew Button, Crew Relations, And Crew Quest Cadence

### Goals

- Add a boss-only `Crew` button in the muscle tab next to `Drive` and the existing vehicle action buttons.
- Use the `Crew` button as the entry point for crew relations, with quest state as the button color:
  - green `Crew` text when no crew/boss quest is currently available;
  - red `Crew` text when at least one crew relation quest is available.
- Keep the button layout compact so it does not displace or overlap `Drive`, passenger/driver actions, route buttons, or resource/shop controls.
- Keep scheme stability tied to vehicles by allowing only one active scheme per vehicle. A second crew member in the same car should be blocked from starting a scheme until the first scheme completes or the suspended vehicle state clears.

### Quest Cadence Policy

- Track quest availability per crew member, not just globally.
- Crew-member quests should be less frequent than boss-facing events.
- For each crew member, roll a 4-12 week eligibility window.
- Most eligible crew should offer only one quest in that window; a smaller subset may offer two.
- Some crew should roll no quest at all during a given 4-12 week window so the button can show relations without always becoming a quest dispenser.
- Do not launch quests automatically from the button. The button opens crew relations first; available quests are selected from that screen.

### Implementation Plan

- Locate the muscle tab/crew card action row that owns `Drive`, then clone the established button style and spacing for `Crew`.
- Gate button visibility to the boss card only. If the selected card is not the boss, leave the current muscle tab layout unchanged.
- Add a crew-relations panel opened by `Crew`:
  - show each living crew member;
  - show relation/standing details that can be resolved from the current save;
  - show available quest indicators only where the cadence state says a quest is ready.
- Add save-backed cadence state for each crew member:
  - next eligibility turn/week;
  - offers used in the current window;
  - window max offers (`0`, `1`, or `2`);
  - currently available quest id, if any.
- Add validation markers:
  - `CrewRelations crew-button-added boss=... availableQuests=...`
  - `CrewRelations crew-button-clicked boss=... availableQuests=...`
  - `CrewRelations quest-window-rolled peep=... weeks=... maxOffers=...`
  - `CrewRelations quest-available peep=... quest=...`
  - `CrewRelations quest-window-skipped peep=... weeks=...`
- Validation pass:
  - boss card shows the button; non-boss crew cards do not;
  - green button when no quest is available;
  - red button when at least one quest is available;
  - relations panel opens from the button and does not conflict with vehicle actions;
  - starting schemes from a multi-crew vehicle never allows more than one active scheme for that vehicle.

### 2026-05-08 Crew Button First Pass

- Added the first boss-only `Crew` top-row button in the muscle card UI.
- The button opens the existing crew relations popup through `OpenCrewRelationsFromExternalUi`.
- The button text is green when there are no saved pending crew side quests and red when `SaveData.PendingCrewSideQuests` has at least one pending entry.
- The button does not call the existing quest count seeder, so merely refreshing the boss card will not force a new quest into existence.
- Insert order is now `Drive`, `Crew`, `Scout`, `Set driver` for vehicle cards, with non-boss cards hiding the `Crew` button.
- New validation markers:
  - `CrewRelations crew-button-added boss=... availableQuests=...`
  - `CrewRelations crew-button-clicked boss=... availableQuests=...`

### 2026-05-08 Crew Quest Cadence Fix

- Live logs showed the weekly quest problem clearly:
  - opening the boss `Crew` button with `availableQuests=0` opened `CrewRelationsPopup`;
  - the existing crew side-quest count path immediately ran `EnsureCrewSideQuestsSeededForHumanBoss`;
  - that seeder called `TryQueueCrewPepTalkPrompt(... requireChance: false)`, so it forced a pending quest every time the inbox refreshed with no pending quest;
  - after a quest was accepted/declined, the same path queued the next crew member in the same week.
- Added per-crew saved cadence fields on `CrewModState`:
  - `NextCrewQuestEligibleDay`;
  - `CrewQuestWindowEndDay`;
  - `CrewQuestOffersRemainingInWindow`.
- New cadence policy:
  - each crew member rolls a 4-12 week window;
  - regular crew roll no quest more often than the boss;
  - eligible windows offer 0, 1, or 2 quests;
  - second offers in the same window are separated by at least 14 days;
  - consuming a quest no longer immediately chains to the next crew member unless that other crew member is independently due.
- New validation markers:
  - `CrewRelations quest-window-rolled peep=... boss=... weeks=... maxOffers=... nextDay=... endDay=...`
  - `CrewRelations quest-window-skipped peep=... boss=... weeks=... nextDay=...`
  - `CrewRelations quest-available peep=... boss=... remainingInWindow=... nextDay=... endDay=...`
  - `CrewRelations quest-cadence-skip peep=... reason=not-due|window-empty|manual-pep-talk-used-today`
- Build/stage result: `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed, and staged `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll` matches release build hash `3C0CD2A4D1476DE1B2B7E2088AC4396FCE747F5FE93DEEEDC750FD544782FD99`.

### 2026-05-08 Crew Quest Cadence Validation And Global Inbox Cooldown

- Live/staged/release hashes matched `3C0CD2A4D1476DE1B2B7E2088AC4396FCE747F5FE93DEEEDC750FD544782FD99`, confirming the cadence build was live during the log pass.
- Validation signals looked mostly correct:
  - `quest-window-rolled` created per-crew windows;
  - `quest-window-skipped` confirmed some crew rolled no quest in the current window;
  - later `Crew` opens showed `quest-cadence-skip ... reason=not-due` or `reason=window-empty` instead of weekly prompt creation.
- Residual issue from the first initialized day: when several crew members had no cadence state yet, accepting one prompt could immediately seed another due crew member on the same day because the global inbox had no cooldown.
- Added `NextCrewSideQuestGlobalEligibleDay` to `ModSaveData`.
- New global guard:
  - after any new crew side quest is queued, the inbox cannot seed another new crew side quest for 7 days;
  - existing pending quests still display normally;
  - per-crew 4-12 week windows remain unchanged.
- New validation markers:
  - `CrewRelations quest-global-cooldown-set peep=... nextDay=...`
  - `CrewRelations quest-cadence-skip peep=0 reason=global-not-due day=... nextDay=...`
- Build/stage result: `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed, and staged `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll` matches release build hash `70FCD0C0115365D828E0BE9B06F68BD3835E86E5C4C49C2AA3F1931E4F330D26`.

### 2026-05-09 Save Load Victory Recompute Guard

- User reported the last save would not load.
- Live logs showed the failed load used the global-cooldown build and crashed after entity load, not while parsing `GameplayTweaks_v2.sim`:
  - `NullReferenceException` in `SomaSim.Util.ListExtensions.StableSort`
  - called from `Game.Session.Sim.VictoryAbstractWorthSubgoal.RecomputeState`
  - called from `VictoryTracker.OnAfterEntityLoad`
- Root interpretation:
  - existing stability finalizers covered `LedgerReportGenerator.GetNetWorthOfPlayer` and `VictoryNetWorthSubgoal.ProduceWorthPerPlayer`;
  - the `ProduceWorthPerPlayer` finalizer could convert an underlying nullref into a `null` `List<Money>`;
  - vanilla `VictoryAbstractWorthSubgoal.RecomputeState` then attempted to sort that null list during load and aborted the save load.
- Fix:
  - removed the generic null finalizer from `VictoryNetWorthSubgoal.ProduceWorthPerPlayer`;
  - added nullref finalizers to `VictoryTracker.OnAfterEntityLoad`, `VictoryTracker.RecomputeAllGoals`, and `VictoryAbstractWorthSubgoal.RecomputeState`;
  - added `LoadStability nullref-swallowed method=...` diagnostics for future verification.
- Build/stage result: `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed, and staged `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll` matches release build hash `82BCC660743693DD35B893587C6D0880BFA466905A29C11D2684E465CFD2D362`.

### Last Log Follow-Up Before Phase 5

- Route-in shop buy/sell now shows working markers: `route-in-shift-convo-opened`, `route-shop-stage-opened`, and `route-shop-committed`.
- Delivery routes still show occasional mismatched arrival diagnostics such as `delivery-route-arrival-confirmed ... node=NID_541 finalGoal=NID_1473`, so skipped-node behavior under fast turn skipping should remain a route-simulation follow-up.
- Recurring non-route cleanup remains: CopKilling `AttackAdvisor.OnTurnUpdate` nullrefs are swallowed by its guard, and outpost/territory logs still show `Cannot double-start outpost` plus `outpost-target-respect-target-skipped ... ArgumentOutOfRangeException`.

## Acceptance Checks

- Retainer cannot increase when the player lacks safehouse cash.
- Enemy crew actions cannot open until the player vehicle is physically on the enemy corner.
- Delivery route inputs accept large values only when inventory and capacity allow them.
- Resource search does not break existing route setup.
- Residential events are tested against the corrected live `ResEvents.sim`, not just the staged release file.

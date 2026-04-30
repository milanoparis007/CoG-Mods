# Scope Out / Final Goal Debug Prompt

Use this prompt when scope-out, final destination, safehouse access, or vehicle route authority bugs come back. Paste it into a new agent/session so the exact bug cluster does not need to be re-explained.

```
We are in C:\Users\User\source\repos\ClassLibrary1 working on the City of Gangsters GameplayTweaks multi-crew vehicle patches.

Investigate the recurring scope-out/final-goal bug cluster:
- Buildings at a selected route final goal sometimes do not glow for scope-out before the vehicle arrives.
- Scope-out may compare the clicked building to the current segment/arrival node instead of the queued final goal.
- Unknown final-goal corners must not become generally known just because the route was queued.
- Safehouse and owned-building resource access must not glow/open unless the selected vehicle is physically at that corner. A manager assigned to a building must not count as physical safehouse presence.
- Healing at a safehouse or owned building must not enable or execute before the selected vehicle physically/stably arrives. A pending route final goal does not count as being at a heal location.
- After a pre-arrival scope-out, the scoped business can stop glowing while still being accessible if transient scope feedback clears same-node building picks. Treat scope-feedback-transient as preview refresh state, not as a destructive same-node pick reset.
- A building can log `scope-selector-final-goal-preview` and then still fail with `scope-map-disabled reason=wrong-corner` if the follow-up crew HUD scope check resolves a stale selected/segment node instead of the route final goal.
- While the vehicle is actively traveling, the corner it just left can keep glowing because live/mobile selected-vehicle matches survive after `building-presence-preview-ignored`. Active-route selected vehicle matches should only count for the preview destination or the node the vehicle is physically at.
- On a long movement with a queued final goal, if the vehicle has physically reached an intermediate corner with scopeable buildings, those buildings should remain scopeable before the final goal is reached.
- During delivery routes or route resumes, vehicles sometimes skip/teleport one node. Check for active-route interruption or pre-drive logical commits that move crew/vehicle authority ahead of the visual DriveOnPath.
- Attack planning opened from a final-goal conversation while the vehicle is still traveling should populate the target/enemy name from the selected final goal instead of the stale current segment.
- If logs show `VehicleArrivalPresence ... source=selected-final-goal`, `selected-current-segment`, `pending-*`, or `queued-*`, treat that as an access leak. `FindFirstCrewAtLocation` / `FindAllDriversAtNode` are general building-presence APIs and must not merge preview route nodes into live presence. Scope-out has its own preview path.
- If logs show `building-node-source ... source=selected-final-goal` during active travel before the vehicle arrives, treat that as a building-interaction leak. Scope/attack planning should fall through to preview-specific handling; owned-building resources, safehouse, heal, and actual actions should fail closed.
- If owned buildings log `scope-building-node-match ... source=route-final-goal` or `owned-building-preview`, that is an access leak. Owned buildings should log `owned-building-live` only when the selected vehicle is physically/stably at that owned building's access node; otherwise expect `reason=owned-building-live-only`.
- If hostile click logs `HostileMobileSelect skip reason=no-matching-human-crew ... source=building-committed` or `reason=no-human-crew` while the clicked hostile is at the selected route final goal, the actual-access block is working but attack planning needs its final-goal preview fallback. Expected good tag is `ai-vehicle-target-preview`.

Start with live runtime logs only:
C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log
C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log

Useful log tags:
ScopeOut
VehicleNodeAuthority
scope-selector-empty
scope-selector-final-goal-preview
scope-building-node-match
scope-preview-source
scope-preview-final-goal-accepted
display-final-goal-preview
route-owned
route-queued
route-resume
route-resume-source
turnstart-arrival-already-committed
travel-interrupt-commit
travel-interrupt-failed
command-path-interrupt-active
command-path-discarded-active
user-stop-route-preserved-old-goal
command-path-start movesBefore
command-path-discarded-active moves
start-driving-interrupt-active
pre-drive-agent-skip
travel-logical-commit-deferred
safehouse-physical-crew-filtered
safehouse-access-crew-filtered
safehouse-live-only
building-presence-preview-ignored
actual-convo-preview-blocked
attack-popup-preview-node
ai-vehicle-target-preview
heal-validate-disabled
heal-validate-enabled
heal-skip
scope-feedback-selection-skipped
scope-feedback-refresh
same-node-ui-picks-cleared
scope-map-disabled
scope-transient-selected-filtered
scope-status-preview-authorized
scope-status-preview-rejected
scope-status-preview-match-fallback
building-pick-refresh-preview-start-cleanup

Primary files/methods:
- GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.cs
  - TryGetHumanVehicleScopePreviewNodeId
  - TryGetHumanCrewUsableForScopePreviewAtBuilding / AtNodes
  - TryGetHumanVehicleTravelDisplayFinalNodeId
  - TryInterruptActiveHumanVehicleTravelForImmediateMove
  - TrySyncVehicleOccupantsToNode
  - MarkHumanVehicleTravelActive
  - TryResumeQueuedHumanVehicleRoutes
- GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.Interaction.cs
  - HumanBuildingCrewPresencePatch.Prefix
  - HumanCrewSelectorNodePatch.Prefix
  - OwnedBuildingInteractionSelectionPatch.TryResolveOwnedBuildingVehicleMatches
  - OwnedBuildingInteractionSelectionPatch.TryResolveBuildingInteractionMatches
  - CommandButtonScopeOutPatch.TryGetCommittedScopeMatchesForBuilding
  - BuildingPickScopeOutPatch.Prefix
- GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.TravelAndAssignment.cs
  - HumanVehicleCommandGotoPathPatch.Prefix
  - StartDrivingCarPrefix

Acceptance criteria:
- Clicking an unscoped building at the selected route final destination allows/glows scope-out before arrival, including unknown final-goal route previews when the vehicle has a valid active/queued route.
- Scope selector logs use final-goal preview when the clicked building matches the queued destination, not the current segment node.
- Safehouse/resource/owned-building access requires a selected live vehicle physically at that building's comparison node; assigned managers and idle off-node crew do not count.
- Heal access requires the selected live vehicle to be physically/stably at the safehouse or owned-building node. Validate, CanStart, and CombatManager.PerformHealing stale execution all fail closed during active or queued travel.
- Transient scope feedback after pre-arrival scope-out preserves/rebuilds final-goal building glow and does not log `same-node-ui-picks-cleared` for `scope-feedback-transient`.
- A final-goal scope-out accepted by the selector remains accepted by the map command status; it must not immediately fall back to `wrong-corner` because `crewhud-scope` or command-start preview resolved stale UI authority.
- Old/start-corner building picks stop glowing during active travel unless the selected vehicle is physically still at that node; physically reached intermediate route nodes can still scope while the final goal remains queued.
- Preview-only vehicle UI refreshes should rebuild the prior segment/start node as well as the preview destination on travel start/finalize, so stale business glows do not survive after the authority source moves.
- No pre-drive crew/vehicle authority commit makes the safehouse or other owned buildings open before the vehicle arrives.
- Active route interruption must not commit to the expected node unless the vehicle is physically there; otherwise it should block/defer instead of teleporting or skipping a node.
- General building presence must be physical/recent-finalize only. It can accept the active route expected node only after the vehicle is physically snapped to that node, and must not report a queued/final-goal preview as live crew presence.
- Building interaction node resolution must not return `selected-final-goal`, `selected-current-segment`, `current-segment`, `queued-resume-expected`, or other pending/final preview sources while the active route has not physically arrived. Expected log for the guarded case is `building-node-preview-blocked`.
- Owned-building committed matches must be live-only, including non-safehouse owned businesses. Expected guarded log is `scope-building-node-match ... accepted=false reason=owned-building-live-only`.
- Hostile crew-pick selection can use final-goal preview only for attack planning. It should log `ai-vehicle-target-preview` followed by the normal hostile normalization; this must not reopen generic building/resource presence.
- If a replacement move is clicked while an active route has not reached a physical committed node, the replacement command should be discarded/failed, not left as `SkipThisTurn`, so it cannot linger in the queue or drain the next action cycle.
- Explicit user-stop route clears must clear selected final-goal UI memory and refresh the preserved expected node so old final-goal icons do not stay on the wrong corner.
- Explicit user-stop route clears must also refresh the abandoned queued final goal and the route start node. `user-stop-route-preserved` is preview cleanup, not stable physical access.
- If action/movement points look drained after a stop-route replacement click, compare `command-path-start ... cost/movesBefore` with `command-path-discarded-active ... moves/actions`; the discarded replacement must not consume points.
- Final-goal attack planning can show the enemy target name from the selected route goal before arrival, while actual building/resource access still fails closed during active travel.
- Build GameplayTweaks and compare/deploy the produced DLL if the game is not locking it.
```

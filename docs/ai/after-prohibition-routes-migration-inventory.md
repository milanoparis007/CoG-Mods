# AfterProhibitionRoutes Migration Inventory

Date: 2026-05-13

Purpose: inventory route, travel, vehicle-node authority, route simulation, and delivery-route behavior before moving any behavior out of `GameplayTweaks`.

Current runtime owner: `GameplayTweaks`.

Current standalone owner status: `AfterProhibitionRoutes` exists as a scaffold only. It exposes bridge/version methods and baseline logs, but owns no behavior.

## Live Log Signals Checked

Latest live log showed the map loaded and route behavior still came from `GameplayTweaks`:

```txt
[VERIFY-HOTFIX] [VehicleNodeAuthority] route-mode-label ...
[VERIFY-HOTFIX] [VehicleNodeAuthority] vehicle-node-reached ...
[VERIFY-HOTFIX] [ScopeOut] building-presence-scope-preview-blocked ... reason=vehicle-not-physical
[VERIFY-HOTFIX] [VehicleNodeAuthority] ambient-spawn ...
[VERIFY-HOTFIX] [FakeTraffic] road graph rebuilt ...
```

Startup diagnostic gates for Economy, Family, and Politics were skipped as intended, so the route split can continue without reintroducing startup map-load risk.

## Inventory Table

| Area | Current source | Main anchors | Current log tags | Risk | Migration decision |
| --- | --- | --- | --- | --- | --- |
| Routes plugin scaffold | `AfterProhibitionRoutes\AfterProhibitionRoutesPlugin.cs` | `IsRoutesBridgeAvailable`, `GetRoutesBridgeVersion`, `GetRoutesBridgeSummary`, startup baseline log | `routes baseline scheduled`, `routes-baseline` | Low | Keep. This is already isolated and read-only. |
| Pending route state | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `PendingVehicleTravelState`, `_pendingVehicleTravelByVehicleId`, `TryGetPendingHumanVehicleTravelState`, `ClearPendingHumanVehicleTravelState` | `pending-travel-clear`, `driver-switch-route-preserved` | High | Move later. First expose read-only counts in Phase 3. |
| Active route state | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `_activeHumanVehicleTravel`, `TryGetActiveHumanVehicleTravelState`, `IsHumanVehicleDeliveryAutomationActive` | `route-resume-*`, `travel-finalize-*` | High | Move later with travel continuation, not during diagnostics. |
| Recent requeue state | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `HumanVehicleRequeueState`, `_recentHumanVehicleRequeueByVehicleId`, `TryGetHumanImmediateReverseState` | `route-resume-replacement-*` | Medium | Move later after route continuation is stable. |
| Vehicle physical node authority | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `IsHumanVehiclePhysicallyAtNode`, `IsHumanVehicleStrictlyPhysicalAtNode`, `TryGetLiveArrivalBridgeNodeId` | `vehicle-node-reached`, `arrival-node` | Critical | Move in Phase 6 only after read-only bridge validation. |
| Observed arrival markers | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `RecordObservedHumanVehicleReachedNode`, `TryGetObservedHumanVehicleReachedNode`, expected-arrival confirmation | `delivery-route-arrival-confirmed`, `travel-finalize-deferred` | Critical | Move with vehicle node authority in Phase 6. |
| Settled-for-action checks | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `IsHumanVehicleSettledAtNodeForAction`, strict physical checks, observed expected arrival checks | `building-presence-*`, `travel-finalize-*` | Critical | Move with node authority. Storage, combat, and shop commits must remain strict. |
| Owned building and safehouse access | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `IsHumanVehicleAtOwnedBuildingAccessNode`, owned-building access guards | storage/building presence block logs | Critical | Move later. This must not be loosened by route-sim convenience. |
| Route-sim access classification | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `IsHumanVehicleRouteSimAccessNode`, route context checks, selected vehicle route state | `route-sim-convo`, `building-presence-scope-preview-blocked` | High | Move in Phase 8 after authority bridge is proven. |
| Route-sim conversations | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | conversation context detection, route-in destination validation, `RouteShopStagingState.RememberRouteInConversation` | `route-sim-convo` | High | Move classification later. Actual shop staging remains coordinated with Economy. |
| Route-in shop staging coordination | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs`; `docs\ai\route-in-out-shop-staging-design.md` | `RouteShopStagingState`, staged conversation memory, staged buy/sell commits | route shop staging logs | Critical | Do not move now. Split route authority from economy mutation first. |
| Route-mode labels | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs`; `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.CrewUi.cs` | route label helpers and crew-card label patches | `route-mode-label` | Medium | Read-only classification can move in Phase 4. Final UI label drawing should stay with UI owner until stable. |
| Queued route resume | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | queued resume scans over pending state, resume watchdog, route rebuild and rebase logic | `route-resume`, `route-resume-source`, `route-resume-deferred`, `route-resume-skip`, `route-resume-watchdog` | Critical | Move in Phase 5 behind config and GameplayTweaks fallback. |
| Human travel command guards | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `CommandGoto.OnTurnStarted`, replacement/dequeue checks, stop-route preservation | `route-resume-replacement-blocked`, `route-resume-replacement-deferred`, `user-stop-route-preserved` | Critical | Move with travel continuation after read-only state audit. |
| StartDriving and path guards | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs`; game anchors in `CommandGoto` and transit/path code | segment-start state, path rebuild and invalid-path handling | `route-resume-invalid-path`, `route-resume-freebridge` | Critical | Move only after dnSpy/decompiled signature check. |
| Travel-end finalization | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `ActionNavigate.Stop` patch, final node sync, expected node override | `travel-finalize-source`, `travel-finalize`, `travel-end-sync`, `travel-finalize-override` | Critical | Move with or after Phase 5. Must avoid double-finalization. |
| Turn-start finalization | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | `CommandExecutor.OnPlayerTurnStarted`, `PlayerInfo.OnPlayerTurnStarted`, queued route finalizers | `route-resume-*`, `travel-finalize-*` | Critical | Move with travel continuation and fallback. |
| Delivery-route automation pump | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | route queue pump, automation step pump, delivery pump segment guard | `delivery-route-queue-pump`, `delivery-route-pump-wait`, `delivery-route-pump-summary`, `delivery-route-arrival-confirmed` | Critical | Move in Phase 7 after node authority is owned by Routes. |
| Delivery automation game anchor | `decompiled\Game.Session.Commands\CommandAutomationStep.cs` | vanilla automation step sequencing | vanilla/no direct hotfix tag | High | Keep as decompiled validation anchor. Do not patch from Routes until Phase 7. |
| Scope-out presence guards | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs`; `BuildingPick` anchors | selected vehicle checks, preview node checks, known/reached node gating | `building-presence-scope-preview-blocked` | High | Move authority decision later. UI preview remains separate. |
| Building pick visuals and interaction previews | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs`; `decompiled\Game.UI.Session.Picks\BuildingPick.cs` | building pick refresh, scope/conversation preview state | scope/presence logs | Medium | Keep out of Routes except authority classification. UI work belongs in AfterProhibitionUI. |
| Crew UI route cards | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.CrewUi.cs`; `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | crew card refresh, route labels, driver portrait/vehicle mapping | `CrewHUD`, `AggroUI`, `route-mode-label` | Medium | Keep with UI until route classifier exists. |
| Travel assignment helpers | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.TravelAndAssignment.cs` | driver assignment, vehicle and crew travel helper behavior | mixed route/driver logs | Medium | Review during Phase 3. Move only route authority pieces, not crew UI ownership. |
| Interaction helpers | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.Interaction.cs` | selected crew/vehicle interaction helpers | interaction/presence logs | Medium | Keep mostly in GameplayTweaks/UI. Routes may expose read-only classification only. |
| Ambient traffic | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | ambient vehicle spawn policy and route selection | `ambient-spawn-policy`, `ambient-spawn`, `AmbientTraffic` | Medium | Stay for now. Not part of player route continuation complaint. |
| Fake traffic road graph | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | fake traffic graph rebuild and movement helpers | `FakeTraffic road graph rebuilt` | Medium | Stay. This is closer to visual/performance simulation than route authority. |
| AI vehicle policy | `GameplayTweaks\GameplayTweaksPlugin.MultiCrewVehicle.cs` | AI driver stabilization and vehicle policy helpers | `ai-driver-stabilized`, AI vehicle policy logs | Medium | Stay for now. Human travel continuation should be isolated first. |
| Economy-facing delivery patches | `GameplayTweaks\GameplayTweaksPlugin.DeliveryFrontExpansionPatch.cs`; `GameplayTweaks\GameplayTweaksPlugin.DeliveryStreetCreditPatch.cs` | front expansion delivery effects and street credit delivery effects | delivery/front/street-credit logs | High | Stay with Economy/GameplayTweaks until route pump no longer mutates resources. |
| Game command anchor | `decompiled\Game.Session.Commands\CommandGoto.cs` | vanilla goto command lifecycle and turn-start continuation | vanilla/no direct hotfix tag | Critical | Validation anchor for Phases 5 and 6. Verify with dnSpy before changing signatures. |
| Game mobile anchor | `decompiled\Game.Session.Entities\MobileComponent.cs` | live/mobile node position authority | vanilla/no direct hotfix tag | Critical | Validation anchor for physical-node authority. |
| Game building anchor | `decompiled\Game.Session.Entities\BuildingComponent.cs` | building access and node relationships | vanilla/no direct hotfix tag | High | Validation anchor for scope-out, storage, and route-sim access. |
| Combat, robbery, cops, territory | many `GameplayTweaks` systems outside route files | gang ops, robbery, cop war, territory color/sim | `GangOps`, `Compat territory-*`, robbery/cop logs | High | Stay out of AfterProhibitionRoutes. |

## Move Order Recommendation

1. Phase 3: read-only route state audit from `AfterProhibitionRoutes` through reflection.
2. Phase 4: route authority classification bridge, still read-only.
3. Phase 5: travel turn continuation bridge with `GameplayTweaks` fallback.
4. Phase 6: vehicle node authority bridge.
5. Phase 7: delivery route pump.
6. Phase 8: route-sim destination access classification.
7. Phase 9: `GameplayTweaks` delegation once each slice is proven.

## Guardrails

- Do not move any mutation in the same phase as a first read-only bridge.
- Do not let route-sim access count as physical access for storage, combat, hostile actions, module mutation, or shop commit.
- Do not double-finalize arrivals between `GameplayTweaks` and `AfterProhibitionRoutes`.
- Keep `GameplayTweaks` fallback working until logs prove the standalone plugin owns a slice.
- Use live logs plus decompiled/dnSpy command anchors before moving Harmony patches around `CommandGoto`, `CommandAutomationStep`, `ActionNavigate.Stop`, or mobile-node authority.

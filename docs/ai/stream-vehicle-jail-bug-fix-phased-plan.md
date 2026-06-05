# Stream Vehicle, Portrait, And Jail Bug Fix Plan

## Log Findings

- `SaveModDataV2` failed 88 times with `TargetParameterCountException`, so GameplayTweaks state was not reliably persisting after saves. This can make jail/trial/sentence state and vehicle authority state appear to stall or revert.
- `Jail blocked-away-custody-confirmed` logged 41,032 times across the live logs. Most entries were no-op query reconciliation, creating heavy log churn and making real jail transitions hard to see.
- `VehicleNodeAuthority` repeatedly used active-route final-goal preview nodes for map/UI refreshes before physical arrival.
- `ScopeOut safehouse-access-crew-filtered` appeared 1,362 times, including `after=0` cases while vehicles were traveling.
- `building-presence-scope-preview`, `building-presence-preview-ignored`, `VehicleArrivalPresence`, `route-command-deferred`, and `command-path-deferred` all appeared during the stream window. These point to preview route authority leaking into live presence/access and route commands being deferred during active travel.
- Portrait assets loaded successfully through `CoGCustomAssets`; no direct portrait asset load failure appeared. Missing portraits are more likely stale/preview/pooling state than missing files.

## Fixed Now

- Added a V2 save failure fallback in `GameplayTweaksPlugin.SaveLoad.cs`.
  - If SION V2 serialization fails, GameplayTweaks now writes the legacy JSON save instead of losing critical mod state for that save.
  - This preserves core crew jail fields, trial days, sentence days, witnesses, heat, pacts, and other legacy-covered state until the V2 serializer is repaired.
- Reduced jail reconciliation log spam in `GameplayTweaksPlugin.cs`.
  - `blocked-away-custody-confirmed` now logs only when vacation/hideout/away state is actually cleared.
- Fixed local important-witness custody reconciliation in `GameplayTweaksPlugin.cs`.
  - Once the base game reports the crew member is actually in jail, pending local witness custody no longer keeps the old trial countdown alive or forces prison days to zero.
  - This targets the symptom where the trial date stayed visible and no prison sentence showed.
- Patched sentence-aware jail text into the base crew UI formatters.
  - `CrewInfoGen.DescribeCrewPeep` and `CrewManagementPopup.GetArrestedDesc` now use `JailSystem.GetJailStatusString` for GameplayTweaks-jailed crew.
  - This targets the screenshot mismatch where the details panel showed a sentence but the crew list still said "awaiting trial".
- Hardened final-goal preview presence in `GameplayTweaksPlugin.MultiCrewVehicle.cs` and `.Interaction.cs`.
  - A route final-goal preview no longer counts as physical building/corner presence for building scope/access paths before the selected vehicle actually reaches that node.
  - This targets phantom corner presence such as "cop is present on corner" with no visible vehicle and owned/safehouse business access leaking or blocking incorrectly during travel.
- Fixed the first proven Phase 2 route-resume bug in `GameplayTweaksPlugin.MultiCrewVehicle.cs`.
  - Live logs showed queued delivery route resumes being marked `route-resume-interrupted`, then cleared by replacement commands while the vehicle was still between corners.
  - Queued route resume segments now defer/block replacement route commands until the vehicle physically reaches the expected segment node.
  - New verification logs are `route-resume-replacement-deferred` and `route-resume-replacement-blocked`; old `route-resume-interrupted` should no longer appear for this path.
- Fixed the second proven Phase 2 delivery bounce bug in `GameplayTweaksPlugin.MultiCrewVehicle.cs`.
  - Live logs for vehicle `4295092012` showed `path-redirect-resolved ... priorSource=mobile resolvedSource=peep-live`, then route commands alternated between old and new nodes.
  - Route command path starts now keep the valid vehicle mobile authority node when the crew member's live node disagrees, instead of rebasing the vehicle to stale peep position.
  - New verification log is `path-peep-live-suppressed`; the old mobile-to-peep redirect should disappear for this bounce path.
- Fixed the third proven Phase 2 delivery bounce bug in `GameplayTweaksPlugin.MultiCrewVehicle.cs`.
  - Follow-up logs showed `route-same-command-allowed` repeatedly while the vehicle had not physically reached the active segment's expected node.
  - Same-route command replays now defer until physical arrival instead of rebuilding path state and re-running `StartDrivingCar` during an active delivery segment.
  - New verification logs are `travel-conflict-same-route-active` and `route-same-command-deferred`; old repeated `route-same-command-allowed` should not appear before arrival for this path.
- Fixed owned-business inventory resource leakage in `GameplayTweaksPlugin.MultiCrewVehicle.Interaction.cs`.
  - The owned business inventory subview no longer treats selected/final-goal/in-route vehicles as loadable or visible vehicle inventory.
  - If no vehicle is physically at the business, the panel clears the visit vehicle and stale loading state before `ViewInventory.RefreshAllPanels`.
  - New verification logs are `ownedbiz-inventory-finalgoal-hidden`, `ownedbiz-inventory-viewonly-vehicle-hidden`, and `ownedbiz-inventory-stale-vehicle-cleared`.
- Tightened owned-business inventory again after screenshot/log validation.
  - Inventory loading now requires the vehicle at the business's strict visit/building node, not a frontage or owned-building preview match.
  - Inventory-specific normalization and footer refresh can no longer reattach a blocked vehicle after `TryEnsureOwnedBizPhysicalVehicleAccess` clears it.
  - New verification logs are `ownedbiz-inventory-physical-frontage-blocked` and `ownedbiz-inventory-driver-hidden`.
- Fixed the first proven Phase 3 false-presence paths.
  - `CrewPickAggroRefreshStabilityPatch` no longer clears all cop/federal crew-pick targets when valid live vehicle targets exist. This targets the case where the game still reports a cop/fed on a corner but the visible vehicle pick has been removed.
  - Human-vehicle attack popups no longer use final-goal preview nodes before the player vehicle is physically at the attack node. This targets attacks being offered or resolved from an invisible/in-route vehicle.
  - Building/corner presence no longer accepts scope preview matches unless the vehicle is physically at the relevant node. This targets "present on corner" text while no vehicle is visible there.
  - New verification logs are `attack-popup-preview-blocked`, `ai-vehicle-target-preview-blocked`, and `building-presence-scope-preview-blocked`.

## Phase 1: Verify Current Hotfixes

- Build `GameplayTweaks` in Release.
- Start a stream repro with one selected multi-crew vehicle, one delivery route vehicle, and one jailed crew member.
- Confirm logs no longer spam `blocked-away-custody-confirmed`.
- Confirm save logs show either successful V2 save or legacy fallback, not silent state loss.
- Confirm a trial that completes switches to visible prison time within the same UI refresh cycle.
- Confirm destination businesses/corners do not report live crew/cop/vehicle presence until the vehicle physically arrives.
- Confirm owned-business inventory shows only business storage unless the selected vehicle is physically at that business.
- Confirm owned-business inventory no longer shows the vehicle pane when the vehicle is on a different corner/frontage node. Expected logs are `ownedbiz-inventory-physical-frontage-blocked` followed by `ownedbiz-inventory-driver-hidden`; `ownedbiz-inventory-physical-allowed` should only appear when the vehicle is at the strict business node.

## Phase 2: Route And Delivery Movement Repro

- Status: three code fixes applied for logged delivery-route authority bugs. Still needs long-run validation against delivery routes.
- Instrument route command ownership around:
  - `route-command-deferred`
  - `command-path-deferred`
  - `pre-drive-agent-skip`
  - `travel-finalize-override`
  - `turnstart-sync-skip`
  - `path-peep-live-suppressed`
  - `route-same-command-deferred`
- Reproduce delivery route movement with no manual player input.
- Classify each unexpected move as one of:
  - delivery route resume
  - queued command replay
  - active-route interruption
  - stale command from previous segment
  - UI preview refresh only
- Fix only the proven source. Remaining likely candidates are stale queued route commands resuming after arrival or command deferral returning `SkipThisTurn` while the original route command remains live.

## Phase 3: Portrait And Attack Presentation

- Status: first code fix applied for false presence where the vehicle itself is missing, not just the portrait. Needs stream validation against cops, feds, and hostile attack picks.
- Reproduce missing portrait on:
  - selected crew pick
  - hostile/cop vehicle pick
  - attack popup
  - person info popup
- Confirm cop/fed refresh logs keep live targets instead of switching to clear-only refresh when valid vehicles are present.
- Confirm attacks cannot open from a player vehicle final-goal preview before physical arrival.
- Confirm `building-presence-scope-preview` no longer appears for in-route vehicles. The expected replacement is `building-presence-scope-preview-blocked` when a preview-only vehicle would have been counted as present.
- Check whether the missing portrait is stale pick pooling or target mismatch.
- If it is pooling, clear `CrewPick` portrait state on reset/deactivate and prune stale pick targets before refresh.
- If missing vehicles still appear after this fix, inspect AI hostile mobile spawn/despawn and crew-pick target lifecycle next. At that point the likely source is not portrait loading or player final-goal preview, but a stale AI vehicle/pick authority entry.

## Phase 4: V2 Save Repair

- Status: first code fix applied. `SaveModDataV2` now writes V2 with a fields-only SION writer instead of direct `FileUtil.SerializeToString`.
- This avoids SION reading computed properties/indexers from live domain classes during save, which was producing `TargetParameterCountException`.
- The existing V2 load path remains in place, so already-written V2 files should still load through `FileUtil.DeserializeFromString<TweaksSaveEnvelopeV2>`.
- Confirm the next in-game save logs `[PERF][TweaksSave] ... writer=fields-only-sion` and no longer logs `V2 save failed; writing legacy JSON fallback`.
- Confirm preserved fields not covered by legacy JSON, especially:
  - vehicle driver mapping
  - front route expansion keys
  - inter-pact alliances and votes
  - newer gang ops stores
- Keep the legacy fallback permanently as a recovery path.

## Open Risks

- The legacy fallback remains as recovery, but after the fields-only V2 writer it should only be used for unexpected write failures. Live save/load validation is still needed.
- Delivery movement now has fixes for the two proven log paths, but still needs a controlled long run to confirm no other command replay path remains.
- Phase 3 now blocks the proven player final-goal attack preview path, but hostile AI/cop vehicle visibility still needs live validation. If the UI still says a cop or attacker is present with no vehicle on the map, the next likely source is AI mobile vehicle pick lifecycle rather than player route preview state.

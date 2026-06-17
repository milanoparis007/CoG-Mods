# AI War Stance Breach Phased Plan

## Goal

Let AI outfits sometimes escalate one weapon tier above the current heat stance during AI-vs-AI wars, without changing weapon supply, shops, unlocks, player combat choices, or inventory contents.

This is a controlled breach system, not a black market or procurement system. It should make aggressive wars escalate faster while preserving early-war fist and street-weapon fights.

## Design Rules

- Keep universal pair war heat as the stance source of truth.
- Resolve the normal stance immediately before combat.
- Allow at most one tier above the normal stance.
- Only AI-vs-AI fights can use this first.
- Only use weapons already available in the active combat vehicle.
- Apply extra heat only after a higher-tier weapon is actually used.
- Keep the feature config-gated before any behavior change.
- Do not alter monthly drops, gun shops, weapon unlocks, or outfit inventories.

## Tier Breach Model

| Current stance | Maximum breach stance |
| --- | --- |
| HandsOnly | StreetWeapons |
| StreetWeapons | Sidearms |
| Sidearms | OpenArsenal |
| OpenArsenal | OpenArsenal |

Personality weighting should start conservative:

- aggressive, bold, cruel, vindictive, confident: higher breach chance
- expansionist: moderate breach chance
- peaceful, cautious, isolationist, nervous: lower breach chance
- direct aggro, recent retaliation, or heat near the next threshold: higher breach chance

## Phase 1 - Diagnostics Only

Add `[WarStance][AIBreachDiag]` logs for AI-vs-AI combat.

Log:

- attacker and defender outfit IDs
- current stance and one-tier breach stance
- effective heat and direct aggro
- personality flags and boss trait flags
- active vehicle IDs
- current compliant weapon and breach-tier weapon
- breach chance, deterministic roll, and whether a breach would happen

No combat behavior changes in this phase.

Status: complete. Live logs showed `PerformAICombat` diagnostics, real one-tier melee candidates, and deterministic `wouldBreach` cases.

## Phase 2 - Shadow Tuning

Keep weapons unchanged, but tune the chance model using live logs.

Check:

- aggressive outfits would breach more than peaceful outfits
- low-heat peaceful wars still mostly stay fists
- StreetWeapons reaches sidearms when heat/personality supports it
- unavailable vehicle weapons never create a fake breach

## Phase 3 - Config-Gated AI Breach

Add config entries and enable behavior only when the gate is on.

Behavior:

- choose the one-tier breach weapon when `wouldBreach=true`
- preserve normal compliant weapon when no breach is allowed
- log `[WarStance][AIBreachUsed]`

Status: first pass implemented. `WarWeaponStance.EnableAiBreaches` gates AI-vs-AI breach selection and defaults on for current testing. The pass changes only the weapon selected for the active combat commit; it does not change inventories, shops, unlocks, monthly drops, or war heat.

## Phase 4 - Heat Consequences

When an AI outfit actually uses a breach weapon, add war heat to the offended direction.

Initial heat:

- melee breach: `+5`
- sidearm breach: `+10`
- long gun or automatic breach: `+15`

Log `[WarStance][AIBreachHeat]` with before/after heat.

Status: first pass implemented. Heat is applied from the final `CombatResults` only, after the breach weapon is confirmed as the actual combat weapon. Attacker breaches add heat from defender to attacker; defender breaches add heat from attacker to defender. The pass logs `[WarStance][AIBreachHeat]` and dedupes by combat transaction and role.

## Phase 5 - Balance Pass

Use multiple saves and AI-vs-AI wars to tune:

- breach chance by personality
- heat gain by weapon tier
- whether defenders can breach as often as attackers
- whether OpenArsenal appears too early
- whether boss vehicles should receive a controlled firearm escalation exception

Stop if low-heat wars become immediate gunfights again.

Status: first boss-vehicle pass implemented, then tightened after live logs showed boss vehicles jumping from `HandsOnly` straight to sidearms at heat 0. Boss vehicles now stay one tier above the current stance: no firearm exception from `HandsOnly`, sidearms only from `StreetWeapons`, and long guns only from `Sidearms`. The exception is still AI-vs-AI only, config-gated by `WarWeaponStance.EnableAiBreaches`, deterministic-roll based, and uses the existing `[WarStance][AIBreachHeat]` consequence path after the weapon reaches final combat. Automatics are not selected by this boss-vehicle exception.

Follow-up tuning: live logs from the one-tier build showed no remaining `HandsOnly` firearm jumps, but AI melee breaches could still roll at heat 0 and sidearm breaches could roll at the floor of `StreetWeapons`. AI breach chance now has a heat-pressure floor at the midpoint of the current stance band. With default thresholds this means no breach rolls below heat `13` in `HandsOnly`, below `38` in `StreetWeapons`, or below `63` in `Sidearms`. `[WarStance][AIBreachDiag]` logs include `heatPressureFloor=` for verification.

Second follow-up tuning: live logs from the heat-floor build showed the floor working and no cold firearm jumps, but valid `HandsOnly` melee breaches near heat `17-22` still rolled at roughly `40-55%`. Breach chance now scales from `35%` to `100%` of the personality/base score between the heat-pressure floor and the next stance threshold, then caps by current stance: `HandsOnly` `35%`, `StreetWeapons` `55%`, and `Sidearms` `65%`. Diagnostics now include `heatPressureScale=` so the next test can confirm lower chances near the floor and full pressure only near the next tier.

Third follow-up tuning: live logs from the scaled build confirmed `heatPressureScale=` was active, `HandsOnly` firearm jumps stayed blocked, and `StreetWeapons` could still breach to sidearms near heat `49`. The remaining issue was early `HandsOnly` melee breaches at heat `14` and `16`. `HandsOnly` breach pressure now starts at `70%` of the StreetWeapons threshold instead of `50%`, which is heat `18` with default thresholds, and the `HandsOnly` chance cap is reduced from `35%` to `30%`. StreetWeapons and Sidearms breach floors and caps are unchanged.

Fourth follow-up pass: live logs from the heat `18` floor build showed the early HandsOnly tuning working, but grouped AI combat could report a different effective heat between rows in the same transaction. Grouped combat now records a `[WarStance][GroupedCommitSnapshot]` at transaction start and reuses that snapshot for grouped diagnostics, raw `PerformCombat` AI enforcement, and breach checks until the grouped transaction ends. This prevents row one combat heat from changing the stance used by later rows in the same grouped attack.

Fifth follow-up pass: live logs from the grouped snapshot build confirmed grouped AI rows now keep the same frozen stance and effective heat across the transaction. Added a diagnostic-only `[WarStance][GroupedSnapshotLiveDelta]` marker that logs once per grouped transaction only if live pair heat or live stance has drifted away from the frozen snapshot. This gives the next test a clean way to confirm that mid-transaction heat changes are being held back intentionally, without changing weapon selection, war heat, inventories, shops, or unlocks.

Sixth follow-up pass: live logs confirmed the grouped freeze, but also showed GangOps front/revenge pacing using relationship-adjusted pressure while weapon stance still saw stored WarHeat as `0`. Added diagnostic-only `[WarStance][GangOpsHeatCompare]`, logged once per combat transaction when relationship-adjusted GangOps heat differs from stance heat or would raise the tier. This pass does not let relationship hostility upgrade weapons; it only exposes whether stance and GangOps escalation are looking at different heat models so the next balance decision can be made from logs.

Seventh follow-up pass: live logs showed repeated cases where stored stance heat stayed in `HandsOnly` or `StreetWeapons` while relationship-adjusted GangOps pressure was already one tier higher. AI breach chance now uses the higher relationship-adjusted pressure value for its heat floor and scale, while the base stance still comes from stored directional WarHeat. This can make a hostile outfit more willing to break one tier above the stance, but it cannot jump more than one tier, cannot use weapons absent from the vehicle, and still applies breach heat only if the stronger weapon is actually used.

Eighth follow-up pass: live logs from the pressure build showed `heatPressureAdjusted=True` working, but the tested HandsOnly fights still had `breachAvailable=False` because the one-tier breach was `StreetWeapons` and the active vehicles had sidearms/long guns without melee weapons. Added `breachBlockReason=` and `actorVehicleWeapons=` to `[WarStance][AIBreachDiag]` so the next run can distinguish a failed roll from a missing breach-tier weapon, especially `no-weapon-above-current-stance` when the selector falls back to fists.

Ninth follow-up pass: live logs confirmed the current bottleneck: active vehicles commonly had `melee:0,sidearm:1,longGun:1`, so the one-tier HandsOnly-to-StreetWeapons breach had no usable weapon above fists. Added shadow-only `nextAvailableProhibitedWeapon=`, `nextAvailableProhibitedCategory=`, and `nextAvailableProhibitedStance=` to `[WarStance][AIBreachDiag]`. This does not let HandsOnly skip directly to sidearms; it only shows the closest prohibited weapon actually present in the vehicle so the next design choice can be made from evidence.

Tenth follow-up pass: live logs showed the closest prohibited weapon was usually `weapon-pistol` with `nextAvailableProhibitedStance=Sidearms`, while current relationship-adjusted pressure was only `StreetWeapons`. Added a narrow pressure-tier exception: if the normal one-tier breach has no usable weapon, AI may use the closest prohibited weapon only when `heatPressureStance` has already reached that weapon's required stance. This means the observed `StreetWeapons` pressure cases still stay fists, but true Sidearms pressure can now select a present sidearm without changing inventories or allowing Open Arsenal skips.

Eleventh follow-up pass: live logs confirmed AI breaches are now occurring (`Melee`, `Sidearm`, and one `LongGun`), and also exposed three `pressureTierEscalation=True` sidearm opportunities. Those opportunities were still capped by the current `HandsOnly` chance cap, and the heat application path still assumed one-tier breaches. Updated AI breach heat to use the actual weapon's required stance, and gave pressure-tier opportunities a pressure-aware chance cap while still blocking Open Arsenal skips. `[WarStance][AIBreachDiag]` and `[WarStance][AIBreachUsed]` now include `chanceCap=` for the next balance run.

Twelfth follow-up validation: live logs from the `2026-06-16 18:41:29` DLL confirmed the new build was loaded and `chanceCap=` appeared on every AI breach diagnostic. The run produced three actual AI breaches: one sidearm breach from `StreetWeapons` to `Sidearms`, and two long-gun breaches from `Sidearms` to `OpenArsenal`. Each `[WarStance][AIBreachUsed]` had a matching `[WarStance][AIBreachHeat]`, and there were no compliance failures. This run did not include a `HandsOnly` pair with `heatPressureStance=Sidearms`, so the pressure-tier sidearm exception still needs one targeted test before further tuning.

Thirteenth follow-up pass: live logs added more evidence that the current bottleneck is not selection failure. There were many `HandsOnly` rows with no melee and available pistols, but none reached `heatPressureStance=Sidearms`, so `pressureTierEscalation=True` still did not execute on the current DLL. Added diagnostic-only `nextAvailablePressureGap=` to `[WarStance][AIBreachDiag]` so future logs show exactly how much pressure is still needed before the closest available prohibited weapon can be used.

Fourteenth follow-up validation: live logs from the `2026-06-16 20:49:34` DLL confirmed `nextAvailablePressureGap=` is working. The run produced 16 AI breach diagnostics, no actual breaches, and no compliance failures. All tested `HandsOnly` rows with pistols available were still below Sidearms pressure; the sidearm pressure gap ranged from `18.0` to `46.6` heat, with an average gap of `26.1`. No source behavior change was made because the pressure-tier exception still has not been exercised by a qualifying combat row.

Fifteenth follow-up validation: the same `20:49:34` DLL produced 40 AI breach diagnostics, four actual sidearm breaches, four matching breach-heat rows, and no compliance failures. The sidearm breaches all occurred from `StreetWeapons` into `Sidearms`, including one combat where both attacker and defender breached and each received one heat application. `HandsOnly` pistol rows still did not reach Sidearms pressure; the closest observed gap was `8.7` heat, so the pressure-tier sidearm exception remains untested rather than broken.

Sixteenth follow-up validation: the live `20:49:34` DLL produced 74 AI breach diagnostics, eight actual sidearm breaches, eight matching breach-heat rows, and no compliance failures. All actual breaches were still normal `StreetWeapons` to `Sidearms` breaches; none used the `pressureTierEscalation=True` path. The closest `HandsOnly` pistol row remained `8.7` heat short of Sidearms pressure, so the pressure-tier exception still needs a hotter Hands Only row before tuning.

Seventeenth follow-up pass: live logs showed many `HandsOnly` AI rows where the active vehicles had fists plus firearms, no melee, and relationship-adjusted pressure already in high `StreetWeapons`. Added a narrow missing-melee pressure bridge: when the current stance is `HandsOnly`, the one-tier `StreetWeapons` breach has no usable melee weapon, the closest available prohibited weapon is a sidearm, relationship-adjusted pressure is within `10` heat of the Sidearms threshold, and the AI breach gate is enabled, the sidearm can be considered as a `pressureTierEscalation=True` breach. This still blocks cold heat-0 pistol jumps, blocks Open Arsenal skips, uses only weapons already in the active vehicle, and applies normal AI breach heat only if the weapon is actually used.

Eighteenth follow-up pass: completed retaliation business/front closures now add `+10` WarHeat from the offended outfit toward the outfit that completed the closure. Heat is applied only when the pending closure cleanup confirms the business is actually forced closed, not when the action is queued, requeued, abandoned, or unavailable. The new `[WarStance][FrontClosureHeat]` log reports before/after directional heat, effective pair heat, and stance so the next combat can be verified to escalate naturally from the closure. Next phase: audit and tune existing AI-vs-AI robbery event heat rather than adding a duplicate writer; current robbery paths already include `ai-robbery-success`, `ai-robbery-failed`, and front-closure-before-robbery heat reasons.

Nineteenth follow-up pass: live logs confirmed the `21:45:42` build loaded, front-closure heat fired on six completed closures, and AI breach heat matched all six actual breaches with no compliance failures. The missing-melee pressure bridge finally produced two `pressureTierEscalation=True` sidearm uses from `HandsOnly`, but both happened at stored effective heat `16` with relationship-adjusted pressure `48`. Tightened the bridge so stored effective heat must also reach the normal `HandsOnly` breach floor before a sidearm bridge can fire. With default thresholds this means relationship pressure can still bridge missing melee near escalation, but not before the pair has at least `18` stored WarHeat.

Twentieth follow-up pass: live logs from the `07:40:46` DLL showed the tightened bridge working as intended: no `HandsOnly` to `Sidearms` bridge fired, AI breach heat matched all actual breaches, front-closure heat continued to apply only on completed closures, and there were no WarStance compliance failures. The remaining repeated log issue was `AttackAdvisor.TryPickCoord` throwing a null reference for one AI player after the prefix had already detected no valid coordinated target. The coord guard now clears the stale target and skips vanilla `TryPickCoord` when no valid target remains, including GangOps violence-gate filtered target sets.

Twenty-first follow-up pass: added diagnostic wrapping around the existing AI robbery WarHeat writers without changing heat amounts, cash behavior, retaliation, inventories, shops, unlocks, or procurement. The new `[WarStance][AiRobberyHeat]` marker logs robbery phase, reason, direction, before/after directional heat, before/after effective pair heat, and stance before/after. This covers AI-vs-AI robbery success/failure, pre-robbery front pressure, and AI-vs-player contact robbery choices so the next live run can show whether robbery heat is too weak, whether it naturally changes stance, or whether front-closure plus robbery heat stacks too quickly.

Twenty-second follow-up pass: live logs from the `08:25:26` build confirmed the new robbery heat marker loaded and produced three events. The values were too light for AI-vs-AI stance progression: pre-front robbery pressure added `+2`, player-contact refusal added `+4`, and a high-value AI-vs-AI robbery success added `+5`, all without changing stance. Added separate AI gang robbery heat values while leaving player-contact robbery heat unchanged: pre-front pressure `+8`, low/high robbery success `+8/+12`, and low/high failed robbery `+16/+22`. This should let robbery build stored stance heat over repeated shakedowns and make failed robberies meaningfully hotter, while avoiding an automatic one-event jump to Sidearms.

Twenty-third follow-up pass: live logs from the `08:56:44` build confirmed the tuned AI robbery heat values fired: two pre-front robbery events used `+8`, and two high-value AI-vs-AI robbery successes used `+12`. No failed AI robbery happened in that run, so `+16/+22` still need live evidence. The remaining issue was breach pressure: one AI used a melee breach from `HandsOnly` at stored effective heat `0` because relationship-adjusted pressure was `30.3`. Tightened AI breach availability so `HandsOnly` breaches require stored pair heat to reach the existing Hands Only breach floor before relationship pressure can roll a breach. `StreetWeapons` and `Sidearms` breach behavior is unchanged. `[WarStance][AIBreachDiag]` now includes `storedHeatFloorSatisfied=` and should report `breachBlockReason=stored-heat-below-breach-floor` when high relationship pressure is blocked by cold stored heat.

Twenty-fourth follow-up pass: live logs from the `09:17:20` build confirmed the stored heat floor is active. The run produced two `stored-heat-below-breach-floor` blocks for `HandsOnly` melee candidates at stored heat `0`, while later `StreetWeapons` sidearm breaches still applied heat normally. The robbery tuning also gained failed-robbery evidence: a high-value failed AI-vs-AI robbery used `+22` and moved one pair from `StreetWeapons` to `Sidearms`, while a cold failed robbery moved another pair only to `22` heat and stayed `HandsOnly`. No exceptions or compliance failures appeared. Updated the `[WarStance][AIBreachUsed]` marker from `heatApplied=false` to `heatPending=true` so selection-time breach logs are not confused with the final `[WarStance][AIBreachHeat]` consequence logs.

Twenty-fifth follow-up pass: live logs from the `10:09:36` build confirmed the marker cleanup worked: all 10 `[WarStance][AIBreachUsed]` rows reported `heatPending=true`, all 10 had matching `[WarStance][AIBreachHeat]` rows, and `heatApplied=false` disappeared. AI robbery heat continued to fire with tuned values, including high-value successes at `+12` and pre-front pressure at `+8`. No new WarStance compliance issue was supported by the run. The only live exception was a vanilla conversation UI null reference in `CheckIsConvoInsideTerritory.DoesPass` while generating conversation buttons for a visit without a valid building node, so the code pass added a null-safe prefix for that requirement inside `ConvoNullFixPatch`. The prefix mirrors the vanilla owner/type check when a node exists and treats missing nodes as unowned territory instead of crashing.

Twenty-sixth follow-up validation: live logs from the `10:57:47` release-candidate build confirmed the expected DLL was active in the live install, the `CheckIsConvoInsideTerritory` prefix loaded, and no `Exception` or `NullReferenceException` occurred. The conversation guard did not need to fire during this run. WarStance evidence was release-stable: one AI breach selection used `heatPending=true` and had one matching `[WarStance][AIBreachHeat]`; `heatApplied=false` stayed absent; robbery heat fired 13 times with tuned values; and 105 cold stored-heat floor blocks prevented `HandsOnly` escalation when stored heat was below the floor. A player-selected Colt violation at `HandsOnly` showed the warning, confirmation, `[ProposedViolation]`, and `[StanceViolated]` path, applying `+20` heat and the relationship penalty. No source code change was made for this pass because the log did not support another weapon-stance fix before release.

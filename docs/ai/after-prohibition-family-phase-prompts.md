# AfterProhibitionFamily Phase Prompts

Use this plan to split spouse search, pregnancy and children, family generation, dead relationship cleanup, and relationship safety checks out of `GameplayTweaks` into a separate BepInEx plugin named `AfterProhibitionFamily`.

## Goal

Create a family-only owner for:

- spouse search and spouse candidate safety;
- pregnancy scheduling, birth checks, and child lifecycle safety;
- startup family generation and parent fallback;
- dead relationship cleanup;
- relationship safety checks that prevent invalid parent, child, sibling, spouse, and dead-person links.

This plugin must not own:

- UI rendering, crew tab layout, blank portrait rendering, popup docking, retheme work, or crew info buttons, which belong in `AfterProhibitionUI`;
- shop buy/sell fixes, bank/warehouse purchases, dirty-cash routing, or business resource logic, which belongs in `AfterProhibitionEconomy`;
- Dirty Cash DLL detection, 10k input compatibility, external mod detection, or broad-unpatch prevention, which belongs in `AfterProhibitionCompatibility`;
- political starter quests, bribes, judge/law office systems, campaign systems, or elections, which belongs in `AfterProhibitionPolitics`;
- vehicle route continuation, delivery movement authority, AI combat, robbery, territory simulation, or map generation.

## Current Live Signals

Recent live logs show family and relationship behavior is active through `GameplayTweaks`, with no fresh family crash in the checked log:

```txt
[VERIFY-HOTFIX] [RelationshipCleanup] patch applied crewDeath=True peopleDeath=True
[VERIFY-HOTFIX] [RelationshipCleanup] dead-peep scrub peep=... source=crew-death sourceListRemoved=True outgoing=2 incoming=2 emptiedIncomingLists=0
[VERIFY-HOTFIX] [CrewRelations] crew-button-added boss=... availableQuests=0
[VERIFY-HOTFIX] [NewGameFamily] startup-safehouse-reconciled building=... business=... owner=... front=...
```

The main risk is that relationship safety, startup family fallback, safehouse owner guards, hiring eligibility, and UI-facing relationship displays are currently close together. This migration should move family state and validation first, then delegate behavior only after the standalone plugin logs stable read-only audits.

## Current Source Inventory

Primary current owners in `GameplayTweaks`:

- `GameplayTweaks\Features\Relationships\GameplayTweaksPlugin.MarriageAndHiringPatches.cs`
  - `SpouseEthnicityLinkPatch`
  - `SpouseEthnicityCandidatePatch`
  - `IsForbiddenSpouseCandidate`
  - `FindRandoToMarryPatch`
  - `HumanStartupParentFallbackPatch`
  - `HumanStartupSafehouseReplacementPatch`
  - `StartupGeneratedPopulationPatch`
  - `StartupStarterPackNullGuardPatch`
  - `StartupNpcSafehouseOwnerGuardPatch`
  - `HireableAgePatch`
  - `PotentialBizOwnerEligibilityPatch`
- `GameplayTweaks\Features\Relationships\GameplayTweaksPlugin.DeadRelationshipCleanupPatch.cs`
  - `ResetDeadRelationshipCleanupRuntime`
  - `TryScrubDeadRelationshipState`
  - `FlushPendingDeadRelationshipStartupScrubs`
  - `DeadRelationshipCleanupPatch`
- `GameplayTweaks\GameplayTweaksPlugin.cs`
  - spouse search UI flow and `TryStartSpouseSearch`
  - pregnancy scheduling around `OnMarryChild`
  - crew relationship button state
  - runtime state reset calls

Game anchors to verify before patch migration:

- `PeopleTracker.FindMatchAndLinkCouple`
- `PeopleTracker.ScoreCandidate`
- `PeopleTracker.FindRandoToMarry`
- `PeopleTracker.MarkAsDead`
- `CreatePlayersHumanHelper.PickAndSetValidParents`
- `PlayerCrew.ProcessCrewMemberDeath`
- `RelationshipTracker`, `RelationshipList`, `RelationshipType`
- `PersonData.futurekids`

## Phase 1 - Standalone Plugin Scaffold

Status: completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionFamily\AfterProhibitionFamily.csproj`.
- Added `AfterProhibitionFamily\AfterProhibitionFamilyPlugin.cs`.
- Added `AfterProhibitionFamily\README.md`.
- Added `AfterProhibitionFamily` to `ClassLibrary1.sln`.
- The plugin logs a delayed read-only family baseline and owns no gameplay behavior yet.

Prompt:

```txt
Create a new standalone BepInEx plugin project named AfterProhibitionFamily. Keep it separate from GameplayTweaks, AfterProhibitionUI, AfterProhibitionEconomy, AfterProhibitionCompatibility, AfterProhibitionPolitics, and AfterProhibitionAssets. Add a minimal plugin class, Harmony ID, logger, config section, and delayed family baseline log. Do not move family behavior yet.

Validation:
- dotnet build AfterProhibitionFamily\AfterProhibitionFamily.csproj -c Release
- Confirm no dependency on GameplayTweaks.
- Confirm no UI, economy, compatibility, politics, route travel, robbery, combat, or territory patches are included.
```

Expected files:

```txt
AfterProhibitionFamily/
  AfterProhibitionFamily.csproj
  AfterProhibitionFamilyPlugin.cs
  README.md
```

Expected logs:

```txt
[After Prohibition Family] family baseline scheduled ownsUi=False ownsEconomy=False ownsPolitics=False ownsRoutes=False
[After Prohibition Family] After Prohibition Family loaded phase=scaffold
```

## Phase 2 - Family Migration Inventory

Status: completed 2026-05-12.

Implementation notes:

- Added `docs\ai\after-prohibition-family-migration-inventory.md`.
- Checked live `Player.log` and `Player-prev.log`; `AfterProhibitionFamily` was not present in the live run yet, while `GameplayTweaks` still logged relationship cleanup and startup safehouse/family reconciliation.
- Inventoried spouse search, spouse candidate scoring, relationship safety, pregnancy scheduling, future child state, family hire relative discovery, startup parent fallback, generated population helpers, safehouse/startup guards, hiring gates, business owner eligibility, and dead relationship cleanup.
- No behavior was moved in this phase.
- Next recommended phase is a read-only relationship and family audit in `AfterProhibitionFamily`.

Prompt:

```txt
Inventory current family and relationship code in GameplayTweaks. Create a migration table listing the current source file, method or patch, current log tag, behavior risk, and whether the item should move now, later, or stay.

Validation:
- No code migration yet.
- Include spouse search, spouse candidate scoring, parent fallback, startup generated population, safehouse owner guards, pregnancy scheduling, birth/child checks, dead relationship cleanup, hiring age gates, business owner eligibility, and crew relationship UI hooks.
- Mark audit-only, classifier-only, repair, behavior-changing, and UI-facing items separately.
```

Suggested doc:

```txt
docs/ai/after-prohibition-family-migration-inventory.md
```

Boundary notes:

- `CrewRelations` button creation and blank/stale portrait fixes should move to or stay with `AfterProhibitionUI`.
- `Hiring` business owner eligibility can stay in `GameplayTweaks` until there is a hiring-specific owner. `AfterProhibitionFamily` may expose relationship safety helpers for it.
- Safehouse owner startup guards should be split carefully. Family can own valid people and parent generation; economy/startup ownership repair should not be moved in the same phase.

## Phase 3 - Read-Only Relationship And Family Audit

Status: completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionFamily\FamilyStartupAudit.cs`.
- Updated `AfterProhibitionFamily` to version `0.2.0`.
- Added `Features.EnableStartupFamilyAudit`, default `true`.
- Added `Features.FamilyAuditSampleLimit`, default `12`.
- Startup diagnostics now wait for the people cache and relationship tracker before logging.
- The audit counts tracked people, living/dead people, missing person data, relationship sources, spouse links, parent links, child links, family links, dead refs, missing target refs, invalid spouse links, future child entries, invalid future child entries, and scan errors.
- The audit logs limited `relationship-audit-sample` lines for missing sources, dead sources, missing targets, dead targets, invalid spouses, and invalid future child entries.
- No relationship, person, pregnancy, child, family generation, UI, economy, route, politics, or compatibility behavior was moved or mutated in this phase.
- Built `AfterProhibitionFamily` in Release and staged `AfterProhibitionFamily.dll` to the current Personal and Public release plugin folders.

Prompt:

```txt
Add a read-only startup audit to AfterProhibitionFamily. Count people, living people, dead people, spouse links, parent-child links, sibling links, same-family spouse candidates, dead-person relationship references, missing reciprocal links, future child entries, and invalid future child mothers. Do not repair or mutate anything.

Validation:
- No relationships, people, families, futurekids, businesses, UI, or save data are changed.
- Audit waits until game entity manager and relationship tracker are ready.
- Logs sample only a small number of invalid entries to avoid log spam.
```

Expected logs:

```txt
[After Prohibition Family] relationship-audit source=start-1s people=... living=... dead=... spouseLinks=... parentLinks=... childLinks=... deadRefs=... invalidSpouses=... invalidFutureKids=...
[After Prohibition Family] relationship-audit-sample type=invalid-spouse current=... other=... reason=same-family-tree
```

## Phase 4 - Relationship Safety Classifier Bridge

Status: completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionFamily\RelationshipSafetyClassifier.cs`.
- Updated `AfterProhibitionFamily` to version `0.3.0`.
- Added public bridge methods on `AfterProhibitionFamilyPlugin`:
  - `IsFamilyBridgeAvailable()`
  - `GetFamilyBridgeVersion()`
  - `OwnsRelationshipSafetyClassification()`
  - `IsValidSpouseCandidate(Entity current, Entity other, out string reason)`
  - `IsValidParentChildLink(Entity parent, Entity child, out string reason)`
  - `IsRelationshipTargetAlive(EntityID peepId, out string reason)`
  - `GetRelationshipSafetySummary(EntityID peepId)`
- Added `Features.EnableRelationshipSafetyBridge`, default `true`.
- Added `Features.EnableRelationshipSafetySampleLog`, default `true`.
- Startup diagnostics now include limited `relationship-safety` and `spouse-safety` sample logs after family state is ready.
- Live logs from version `0.2.0` showed the read-only audit incorrectly counted existing spouse links as invalid when spouses shared the same `famId`. Version `0.3.0` keeps same-family blocking for new spouse candidates, but no longer treats an existing spouse link as invalid solely because it is in the same family tree.
- No `GameplayTweaks` delegation was added in this phase.
- Built `AfterProhibitionFamily` in Release and staged `AfterProhibitionFamily.dll` to the current Personal and Public release plugin folders.

Prompt:

```txt
Move relationship safety classification into AfterProhibitionFamily as public bridge methods. Keep GameplayTweaks behavior as fallback. The family plugin should answer whether two people may be spouse candidates, whether a parent-child link is valid, whether a relationship points at a dead or missing person, and whether a person is safe for family generation.

Validation:
- Classifier does not mutate state.
- Same person, dead person, missing person data, same family ID, existing family relationship, and direct parent-child relationships are rejected.
- Logs explain why a sampled candidate is rejected.
```

Expected bridge examples:

```txt
IsValidSpouseCandidate(Entity current, Entity other, out string reason)
IsValidParentChildLink(Entity parent, Entity child, out string reason)
IsRelationshipTargetAlive(EntityID peepId, out string reason)
GetRelationshipSafetySummary(EntityID peepId)
```

Expected logs:

```txt
[After Prohibition Family] spouse-safety current=... other=... allowed=False reason=same-family-tree
[After Prohibition Family] relationship-safety peep=... status=warning reason=dead-incoming-refs
```

## Phase 5 - Spouse Search Migration

Status: completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionFamily\SpouseSearchPatch.cs`.
- Updated `AfterProhibitionFamily` to version `0.4.0`.
- Added `Features.EnableSpouseSearchMigration`, default `true`.
- Added `Features.EnableSpouseSearchSampleLog`, default `true`.
- Added `Features.SpouseSearchSampleLimit`, default `12`.
- Added spouse-search config values:
  - `SpouseSearch.EnableSpouseEthnicityPreference`
  - `SpouseSearch.SpouseEthnicityPreferenceChance`
  - `SpouseSearch.MarriageMinAge`
  - `SpouseSearch.MarriageMaxAgeDifference`
- `AfterProhibitionFamily` now patches `PeopleTracker.FindRandoToMarry`, `PeopleTracker.ScoreCandidate`, and `PeopleTracker.FindMatchAndLinkCouple`.
- Added `OwnsSpouseSearchMigration()` to the public Family bridge.
- Added a soft BepInEx dependency from `GameplayTweaks` to `afterprohibition.family` so `GameplayTweaks` can see the Family owner when both DLLs are installed.
- Updated `GameplayTweaks` spouse patch registration to skip `SpouseEthnicityLinkPatch`, `SpouseEthnicityCandidatePatch`, and `FindRandoToMarryPatch` when `AfterProhibitionFamily` owns spouse search.
- `GameplayTweaks` keeps fallback spouse patches when `AfterProhibitionFamily` is missing or disabled.
- Built `AfterProhibitionFamily` and `GameplayTweaks` in Release and staged both DLLs to the current Personal and Public release plugin folders.

Prompt:

```txt
Migrate spouse search and spouse candidate scoring into AfterProhibitionFamily. Patch PeopleTracker.FindRandoToMarry, PeopleTracker.ScoreCandidate, and PeopleTracker.FindMatchAndLinkCouple only after the read-only classifier bridge is stable. GameplayTweaks should delegate to AfterProhibitionFamily when available and keep its existing fallback until live validation passes.

Validation:
- Bosses cannot marry parents, children, siblings, same-family relatives, dead people, or themselves.
- Candidate age, gender, business owner, family ID, and existing relationship filters still match or improve the current GameplayTweaks behavior.
- If AfterProhibitionFamily is missing, GameplayTweaks fallback still prevents the known father/spouse issue.
```

Expected logs:

```txt
[After Prohibition Family] spouse-search source=find-rando peep=... candidates=... selected=... rejectedFamily=... rejectedDead=... rejectedBusinessOwner=...
[VERIFY-HOTFIX] [Family] source=afterprohibition-family spouseSearch=delegated fallback=False
```

## Phase 6 - Pregnancy And Children Lifecycle

Status: completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionFamily\PregnancyLifecycleBridge.cs`.
- Updated `AfterProhibitionFamily` to version `0.5.0`.
- Added public bridge methods on `AfterProhibitionFamilyPlugin`:
  - `OwnsPregnancyLifecycle()`
  - `TrySchedulePregnancyForCrewPeep(Entity selectedPeep, out ulong motherId, out int dueDay, out int durationDays, out int futureKidsCount, out string reason)`
- Added config values:
  - `Features.EnablePregnancyLifecycleBridge`, default `true`
  - `Features.EnablePregnancyTurnAudit`, default `true`
  - `Features.EnablePregnancyAuditEveryTurn`, default `false`
  - `Features.EnablePregnancyStartupAudit`, default `false`
  - `Features.PregnancyAuditSampleLimit`, default `8`
  - `Pregnancy.PregnancyMinDays`, default `210`
  - `Pregnancy.PregnancyMaxDays`, default `284`
- `AfterProhibitionFamily` now patches `PeopleTracker.OnSystemTurn` with a read-only prefix audit that counts pending future child entries, due births, invalid mothers/fathers, and duplicate due dates before vanilla birth processing.
- `GameplayTweaks` now calls the Family pregnancy bridge from the `Have Child` button when available, and keeps the old direct `futurekids` scheduling fallback when the Family bridge is missing or disabled.
- `GameplayTweaks` blocks scheduling instead of falling back if the Family bridge is active and rejects the pregnancy for invalid parent/spouse state.
- Reduced spouse-search log spam from version `0.4.0` by removing per-`ScoreCandidate` sample lines; spouse-search samples now remain on the random-spouse path.
- Built `AfterProhibitionFamily` and `GameplayTweaks` in Release and staged both DLLs to the current Personal and Public release plugin folders.
- Follow-up hardening updated `AfterProhibitionFamily` to version `0.5.1` after live logs showed `0.5.0` pregnancy audits running through startup family catch-up. The audit now skips non-interactive startup simulation by default unless `Features.EnablePregnancyStartupAudit=true`.
- Built `AfterProhibitionFamily` `0.5.1` in Release and staged the DLL to the current Personal and Public release plugin folders.

Prompt:

```txt
Move pregnancy scheduling and child lifecycle validation into AfterProhibitionFamily. Keep UI prompts and popup text outside this plugin. The family plugin should validate future child entries, due dates, mother/father links, child creation, duplicate birth prevention, and dead-parent edge cases.

Validation:
- Existing saves with futurekids continue loading.
- Pregnancies with invalid or dead parents log a warning and are not allowed to crash turn progression.
- Birth checks do not duplicate children on repeated turn processing.
- UI code only calls family bridge methods and does not directly mutate futurekids unless fallback is active.
```

Expected logs:

```txt
[After Prohibition Family] pregnancy-audit source=turn-start pregnancies=... due=... invalidMothers=... invalidFathers=...
[After Prohibition Family] child-birth source=turn-start mother=... father=... child=... due=... result=created
```

## Phase 7 - Startup Family Generation Fallback

Status: first implementation completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionFamily\StartupFamilyGenerationPatch.cs`.
- Updated `AfterProhibitionFamily` to version `0.6.0`.
- Added `Features.EnableStartupFamilyGenerationFallback`, default `true`.
- Added public bridge method `OwnsStartupFamilyGenerationFallback()` so `GameplayTweaks` can avoid double-patching startup family generation.
- Moved the family-only startup parent fallback into `AfterProhibitionFamily`:
  - patches `CreatePlayersHumanHelper.PickAndSetValidParents`;
  - creates or finds an adult married male parent when vanilla has no eligible parent candidates;
  - uses `RelationshipSafetyClassifier` before forcing new spouse links.
- Moved generated startup person fallback into `AfterProhibitionFamily`:
  - patches `PlayerSetup.GetAndRemoveCandidatePeep`;
  - patches `CreatePlayersCops.AssignEntitiesAsOfficers`;
  - patches `CreatePlayersFeds.SetUpFedPlayer`;
  - can generate root families and unassigned adult candidates when startup candidate pools are empty.
- `GameplayTweaks` now delegates parent fallback and generated startup person fallback when `AfterProhibitionFamily` owns startup generation. It still owns safehouse replacement/reconcile, starter-pack guards, NPC safehouse owner guards, hiring gates, and business owner eligibility.
- Built `AfterProhibitionFamily` and `GameplayTweaks` in Release and staged both DLLs to the current Personal and Public release plugin folders.
- Live log validation is still needed after copying the staged DLLs into the test install. Expected new signals are `After Prohibition Family 0.6.0`, `startup family-generation patches applied ...`, and `[VERIFY-HOTFIX] [Family] source=afterprohibition-family startupGeneration=delegated fallback=False`.

Prompt:

```txt
Migrate startup family generation fallback into AfterProhibitionFamily. Focus on parent fallback, root-family candidate generation, generated population safety, and startup person validity. Do not move safehouse ownership repair or business setup in this phase unless the code is only validating person/family relationships.

Validation:
- New games do not start with a boss whose generated parent, spouse, or child relationship is invalid.
- Generated parents are alive adults, married to each other when needed, and do not share forbidden descendant links.
- Startup person generation failure logs once with a specific reflection or data reason.
```

Expected logs:

```txt
[After Prohibition Family] startup-parent-fallback parent=... spouse=... child=... reason=generated-parent-couple
[After Prohibition Family] startup-generated-population source=parent-fallback peep=... fam=...
[After Prohibition Family] startup-family-audit result=ok boss=... invalidLinks=0
```

## Phase 8 - Dead Relationship Cleanup Migration

Status: first implementation completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionFamily\DeadRelationshipCleanupPatch.cs`.
- Updated `AfterProhibitionFamily` to version `0.7.0`.
- Added `Features.EnableDeadRelationshipCleanupMigration`, default `true`.
- Added public bridge method `OwnsDeadRelationshipCleanupMigration()` so `GameplayTweaks` can skip duplicate death cleanup hooks.
- Moved dead relationship cleanup hooks into `AfterProhibitionFamily`:
  - patches `PeopleTracker.MarkAsDead`;
  - patches `PlayerCrew.ProcessCrewMemberDeath`;
  - removes the dead peep relationship source list;
  - removes incoming relationship refs to the dead peep;
  - removes emptied incoming source lists;
  - enqueues `SessionEventType.SomeEntityRelationshipChanged` after mutations.
- Preserved startup batching behavior inside `AfterProhibitionFamily`. Early death cleanup is deferred until frame `300` and until `Game.ctx.IsInteractive`.
- `GameplayTweaks` now delegates dead relationship cleanup when `AfterProhibitionFamily` owns it, but keeps the old cleanup hooks as fallback when the Family plugin is missing or disabled.
- Built `AfterProhibitionFamily` and `GameplayTweaks` in Release and staged both DLLs to the current Personal and Public release plugin folders.
- Live log validation is still needed after copying the staged DLLs into the test install. Expected new signals are `After Prohibition Family 0.7.0`, `dead relationship cleanup patches applied crewDeath=True peopleDeath=True`, and `[VERIFY-HOTFIX] [Family] source=afterprohibition-family deadCleanup=delegated fallback=False`.
- Live validation of `0.7.0` showed the owner and delegation were correct, and a large startup cleanup batch removed thousands of dead-person relationship refs. The first startup relationship audit still ran before that deferred cleanup flush, so it reported pre-cleanup `deadRefs`.
- Follow-up hardening updated `AfterProhibitionFamily` to version `0.7.1`. Startup relationship audit now tries to flush pending dead-relationship cleanup first and defers with `reason=pending-dead-relationship-cleanup` while cleanup is still waiting for interactive startup state.
- Built `AfterProhibitionFamily` `0.7.1` and staged it to the current Personal and Public release plugin folders.
- Live validation of `0.7.1` showed the cleanup/audit ordering worked: the deferred startup batch cleaned thousands of dead peep relationship refs and the following relationship audit reported `deadRefs=0`.
- Follow-up hardening updated `AfterProhibitionFamily` to version `0.7.2` to reduce pending-cleanup audit deferral log volume. The audit still waits for cleanup, but logs only the first pending-cleanup deferral plus normal checkpoint attempts.
- Built `AfterProhibitionFamily` `0.7.2` and staged it to the current Personal and Public release plugin folders.

Prompt:

```txt
Move dead relationship cleanup into AfterProhibitionFamily. Patch PeopleTracker.MarkAsDead and PlayerCrew.ProcessCrewMemberDeath, preserving the current startup batch behavior and event enqueue behavior. GameplayTweaks should call the family plugin bridge or keep fallback while live logs prove the new owner is stable.

Validation:
- Dead person relationship source lists are removed.
- Incoming links to dead people are removed.
- Startup cleanup batches after runtime prompts are ready.
- Relationship change events are enqueued after cleanup.
- No relationship cleanup runs before relationship tracker data exists.
```

Expected logs:

```txt
[After Prohibition Family] dead-relationship-cleanup source=people-tracker peep=... sourceListRemoved=True outgoing=... incoming=... emptiedIncomingLists=...
[After Prohibition Family] dead-relationship-cleanup source=startup-batch peeps=... outgoing=... incoming=...
```

## Phase 9 - GameplayTweaks Family Delegation

Status: first implementation completed 2026-05-12.

Implementation notes:

- `GameplayTweaks` now delegates spouse search, startup family generation, and dead relationship cleanup when `AfterProhibitionFamily` owns those systems.
- `GameplayTweaks` pregnancy scheduling already calls the `AfterProhibitionFamily` bridge from the `Have Child` action and blocks invalid Family bridge rejections instead of silently falling back.
- Added a consolidated `GameplayTweaks` family delegation summary log that reports:
  - `spouseSafety`
  - `spouseSearch`
  - `pregnancy`
  - `startupGeneration`
  - `deadCleanup`
- Built `AfterProhibitionFamily` and `GameplayTweaks` in Release and staged both DLLs to the current Personal and Public release plugin folders.
- Live validation of the first Phase 9 pass showed the expected delegation summary:

```txt
[VERIFY-HOTFIX] [Family] delegation-summary source=afterprohibition-family spouseSafety=bridge-available spouseSearch=delegated pregnancy=bridge-available startupGeneration=delegated deadCleanup=delegated
```

Prompt:

```txt
Update GameplayTweaks to delegate family behavior to AfterProhibitionFamily when the plugin is loaded and bridge methods are available. Keep existing GameplayTweaks fallback paths enabled until at least one live validation pass proves each delegated area works.

Validation:
- Logs show which owner handled spouse safety, spouse search, pregnancy checks, startup parent fallback, and dead relationship cleanup.
- Missing AfterProhibitionFamily does not break GameplayTweaks.
- Both plugins loaded together do not double-patch the same behavior in a way that duplicates births, spouses, generated parents, or cleanup.
```

Expected logs:

```txt
[VERIFY-HOTFIX] [Family] source=afterprohibition-family spouseSafety=bridge-available spouseSearch=delegated pregnancy=gameplaytweaks-fallback deadCleanup=delegated
```

## Phase 10 - Packaging And Guide

Status: first implementation completed 2026-05-12.

Implementation notes:

- `AfterProhibitionFamily.dll` is staged in the current Personal and Public release plugin folders.
- Updated the root release `CHANGELOG.md` and `GUIDE.md` with `AfterProhibitionFamily` ownership and included runtime DLL details.
- Updated `Plain Text Guides\CHANGELOG.txt` and `Plain Text Guides\GUIDE.txt` with the same Family split-out release notes.
- Added `Plain Text Guides\AFTER_PROHIBITION_FAMILY_GUIDE.txt`.
- Copied `AFTER_PROHIBITION_FAMILY_GUIDE.txt`, `CHANGELOG.txt`, and `GUIDE.txt` into the Public release package root.
- Copied `AFTER_PROHIBITION_FAMILY_GUIDE.txt` into the Personal release root.

Prompt:

```txt
Stage AfterProhibitionFamily into the current public release package after the plugin builds and live logs show stable baseline or delegated behavior. Update the public guide and changelog with the new DLL, what it owns, and what remains in GameplayTweaks fallback.

Validation:
- dotnet build AfterProhibitionFamily\AfterProhibitionFamily.csproj -c Release
- Stage AfterProhibitionFamily.dll under Things To Have\Current After Prohibition Mod\BepInEx\plugins.
- Update release notes without claiming migrated behavior that is still fallback-only.
```

Expected release files:

```txt
Things To Have\Current After Prohibition Mod\BepInEx\plugins\AfterProhibitionFamily.dll
Things To Have\Current After Prohibition Mod\CHANGELOG.md
Things To Have\Current After Prohibition Mod\GUIDE.md
```

## Migration Rules

- Start read-only. Do not repair relationship or person state until audit logs identify the exact invalid condition.
- Classify before mutating. Relationship safety helpers should be callable by other plugins without changing game state.
- Keep UI outside the Family plugin. The Family plugin may return status and names, but should not create buttons, portraits, panels, or popups.
- Keep GameplayTweaks fallback until live logs prove delegation is stable in new games and old saves.
- Avoid broad unpatches. If both GameplayTweaks and AfterProhibitionFamily patch the same method during transition, gate behavior with explicit ownership checks.
- Old save support matters. Relationship cleanup and pregnancy validation should tolerate missing, stale, or partially generated relationship data.
- Log specific reasons. Every blocked spouse, invalid parent, skipped birth, or cleanup should log a concise reason when sampled.

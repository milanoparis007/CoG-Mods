# AfterProhibitionFamily Stabilization Plan

This plan moves and hardens family-only behavior in `AfterProhibitionFamily` without taking ownership of economy, shops, route authority, UI rendering, politics, inventory, or command execution.

## Phase 1 - Business-Owner Replacement And Hiring Safety

Status: completed with a follow-up startup hotfix in `GameplayTweaks`. The family plugin exposes read-only business-owner family safety classification and spouse safety also rejects business- or residence-assigned spouse candidates. GameplayTweaks keeps owner assignment, replacement, hiring execution, economy, and business mutation. The Family owner-safety bridge is diagnostic-only by default for automatic business-owner selection because strict rejection can exhaust vanilla owner pools during new-game map setup.

Scope:

- Inventory current business-owner replacement, hire-candidate filtering, and owner-safe family checks split between `GameplayTweaks` and `AfterProhibitionFamily`.
- Move only family eligibility classification and read-only safety bridges into `AfterProhibitionFamily`.
- Keep actual business ownership mutation, hiring execution, and economy behavior in `GameplayTweaks` or vanilla owners until a separate ownership handoff exists.
- Ensure business owners, generated replacement owners, and business-assigned candidates are not selected as unsafe spouse or family candidates.

Validation:

- Live logs should show `AfterProhibitionFamily` owning relationship/family eligibility checks.
- Hiring logs should still show `GameplayTweaks` owning execution guards.
- Business owners should not become spouse/future-kid candidates unless explicitly allowed by a safe bridge.

## Phase 2 - Dead Relationship Cleanup

Status: completed in `AfterProhibitionFamily` `0.8.1`. Live logs showed startup cleanup removing `sourceListsRemoved=2638`, `outgoing=56719`, and `incoming=51247` in one pass, so Phase 2 now chunks startup dead-relationship scrubs with `DeadRelationshipStartupChunkSize`.

Scope:

- Harden startup and death-time dead relationship cleanup in `AfterProhibitionFamily`.
- Keep cleanup idempotent and read/write scoped to relationship lists only.
- Add diagnostics that distinguish source-list removal, incoming-reference removal, and skipped unresolved entities.

Validation:

- Startup audit should converge toward `deadRefs=0` when diagnostics are enabled.
- Death-time cleanup should not throw when relationships are already partially removed.

## Phase 3 - Spouse And Future-Kid Validation

Status: completed in `AfterProhibitionFamily` `0.8.2`. Birth processing is now guarded by future-child validation so pending child entries with invalid mother/spouse state are removed before vanilla `PeopleTracker.ProcessBirths` runs.

Scope:

- Validate spouse search, spouse candidate scoring, pregnancy lifecycle, and future-child references against dead, invalid, related, or business-owner entities.
- Keep child creation/generation ownership explicit.
- Add diagnostics for rejected future-child parents and spouse candidates by reason.

Validation:

- No spouse or child reference should point at dead/invalid entities.
- Logs should separate rejected-family, rejected-dead, rejected-business-owner, and rejected-owner-replacement reasons.

## Phase 4 - New-Game Family Safehouse Reconciliation

Status: completed in `GameplayTweaks` as a scoped startup repair guard. Safehouse reconciliation now runs only for human new-game business-safehouse setup and skips when a different valid safehouse already exists, treating that as a true relocation/ownership handoff case.

Scope:

- Audit new-game safehouse reconciliation and determine whether it is truly family safehouse relocation or just safehouse ownership repair.
- If it is ownership repair, keep it out of `AfterProhibitionFamily` and document the owner.
- If family relocation is involved, add a family-only bridge that validates relationships without moving safehouse/economy state.

Validation:

- Startup logs should distinguish safehouse owner repair from family relocation.
- No family reconciliation should mutate business ownership, safehouse inventory, or route/economy state.

## Phase 5 - GameplayTweaks Cleanup

Status: completed as an ownership-audit/logging cleanup in `GameplayTweaks`. The registration summary now reports Family-owned future-kid validation and emits a separate ownership audit that keeps family classification/search/lifecycle surfaces distinct from GameplayTweaks-owned safehouse repair, business-owner mutation, hiring execution, and economy behavior.

Scope:

- Rename or reclassify remaining `GameplayTweaks` logs so family-owned classification is not confused with gameplay-owned execution.
- Keep fallback methods until all downstream callers are migrated.
- Remove duplicate patch registration only after live logs show the split plugin owns the surface.

Validation:

- `GameplayTweaks` and `AfterProhibitionFamily` both build.
- Live logs show no duplicate family patch ownership.

## Phase 6 - Packaging And Live Test Notes

Status: completed for the active root, Personal, and Public release packages. Past Build was intentionally left unchanged. Live logs show `AfterProhibitionFamily` `0.8.2`, `futureKidValidation=delegated`, the new `ownership-audit` boundary line, future-kid cleanup samples, and startup dead-relationship cleanup converging to `remaining=0`.

Scope:

- Stage `AfterProhibitionFamily.dll` into the intended public package only.
- Update release notes with ownership boundaries and live log markers.

Validation:

- Public package contains the expected DLL.
- Live logs distinguish `AfterProhibitionFamily` family ownership from `GameplayTweaks` gameplay/hiring/economy ownership.

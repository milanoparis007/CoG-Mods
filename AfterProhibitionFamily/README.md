# AfterProhibitionFamily

Standalone After Prohibition family and relationship plugin.

The plugin will own family-only diagnostics and staged migration work. It owns no UI, economy, compatibility, politics, route, combat, robbery, or territory behavior.

Initial purpose:

- prove the plugin loads independently from `GameplayTweaks`
- provide a clean family log prefix for later migration work
- keep future spouse search, pregnancy, child, family generation, dead relationship cleanup, and relationship safety fixes out of unrelated systems

Phase 1 is a scaffold only. It logs a delayed read-only baseline and does not patch or mutate gameplay behavior.

Phase 3 adds a read-only relationship and family audit. It waits for the session people cache and relationship tracker, then counts people, living/dead status, spouse links, parent/child links, family links, dead relationship refs, missing relationship targets, future child entries, and invalid future child entries. It does not repair relationships, create people, remove dead refs, schedule children, or change UI.

Version `0.3.0` adds a read-only relationship safety classifier bridge:

- `IsValidSpouseCandidate(Entity current, Entity other, out string reason)`
- `IsValidParentChildLink(Entity parent, Entity child, out string reason)`
- `IsRelationshipTargetAlive(EntityID peepId, out string reason)`
- `GetRelationshipSafetySummary(EntityID peepId)`

These methods classify relationship safety only. They do not move spouse search, mutate relationship data, remove dead refs, create people, schedule children, or change `GameplayTweaks` fallback behavior.

Version `0.4.0` migrates spouse search and spouse candidate scoring into this plugin. It patches:

- `PeopleTracker.FindRandoToMarry`
- `PeopleTracker.ScoreCandidate`
- `PeopleTracker.FindMatchAndLinkCouple`

The patch preserves the same basic age, business-owner, spouse, and relationship filters while using `AfterProhibitionFamily` relationship safety checks. `GameplayTweaks` is expected to skip its matching spouse-search patches when `OwnsSpouseSearchMigration()` is available.

Version `0.5.0` adds the pregnancy lifecycle bridge and audit:

- `TrySchedulePregnancyForCrewPeep(...)`
- `PeopleTracker.OnSystemTurn` read-only pregnancy audit

The bridge schedules pregnancies for `GameplayTweaks` when available. The turn audit counts pending future child entries, due births, invalid mothers/fathers, and duplicate due dates before vanilla birth processing. It does not create children, remove future child entries, or alter vanilla birth processing.

Version `0.5.1` keeps the pregnancy audit from logging during startup family catch-up by default. Set `Features.EnablePregnancyStartupAudit=true` only when investigating generated-family birth processing.

Version `0.6.0` migrates startup family generation fallback into this plugin. It patches:

- `CreatePlayersHumanHelper.PickAndSetValidParents`
- `PlayerSetup.GetAndRemoveCandidatePeep`
- `CreatePlayersCops.AssignEntitiesAsOfficers`
- `CreatePlayersFeds.SetUpFedPlayer`

This owns family/person fallback generation only. Safehouse ownership repair, business setup repair, starter-pack guards, and NPC safehouse owner guards remain in `GameplayTweaks`.

Version `0.7.0` migrates dead relationship cleanup into this plugin. It patches:

- `PeopleTracker.MarkAsDead`
- `PlayerCrew.ProcessCrewMemberDeath`

The cleanup removes relationship source lists for dead people, removes incoming relationship refs to dead people, batches early startup cleanup until the session is interactive, and enqueues `SomeEntityRelationshipChanged` after mutations. `GameplayTweaks` is expected to skip its matching cleanup hooks when `OwnsDeadRelationshipCleanupMigration()` is available.

Version `0.7.1` hardens startup audit timing. When pending dead-relationship cleanup exists, the startup relationship audit now waits for that cleanup to flush first so audit counts reflect the cleaned graph instead of the pre-cleanup startup state.

Version `0.7.2` reduces pending-cleanup audit deferral log volume. It still waits for dead-relationship cleanup before auditing, but logs only the first pending-cleanup deferral and normal checkpoint attempts.

Version `0.7.3` defers startup family and relationship audits until the session is interactive. This keeps read-only relationship scans out of new-game map/player setup.

Version `0.7.4` makes full startup family diagnostics opt-in behind `Features.EnableStartupFamilyDiagnostics=false` by default. Relationship safety, spouse search, pregnancy bridge, startup generation fallback, and dead-relationship cleanup remain active.

Version `0.8.0` starts the business-owner and hiring safety phase. It adds `Features.EnableBusinessOwnerFamilySafetyBridge` and exposes `IsBusinessOwnerFamilySafe(Entity person, out string reason)` as a read-only bridge for GameplayTweaks. AfterProhibitionFamily classifies family-unsafe owner candidates such as spouses, family-linked people, and future-child mothers, while GameplayTweaks still owns owner assignment, replacement, hire execution, economy, and business mutation.

Version `0.8.1` smooths startup dead-relationship cleanup. It adds `Features.DeadRelationshipStartupChunkSize` so deferred dead-person relationship scrubs run in bounded chunks instead of one large relationship-table sweep after map load. Cleanup semantics stay the same: dead source lists and incoming references are still removed.

Version `0.8.2` hardens spouse and future-child validation. It adds `Features.EnableFutureKidValidation` and validates pending future-child entries immediately before vanilla birth processing. Invalid mother/spouse state clears the pending entries instead of letting `PeopleTracker.ProcessBirths` throw or create invalid children.

Planned migration areas are documented in:

`docs\ai\after-prohibition-family-phase-prompts.md`

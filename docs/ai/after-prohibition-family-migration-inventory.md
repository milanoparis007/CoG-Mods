# AfterProhibitionFamily Migration Inventory

Created: 2026-05-12

This inventory captures current family and relationship behavior that still lives in `GameplayTweaks`, then assigns each area to `AfterProhibitionFamily`, another split-out plugin, or fallback-only status.

## Live Log Snapshot

Checked:

```txt
C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log
C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log
```

Current signal:

```txt
[VERIFY-HOTFIX] [RelationshipCleanup] patch applied crewDeath=True peopleDeath=True
[VERIFY-HOTFIX] [RelationshipCleanup] dead-peep scrub peep=... source=crew-death sourceListRemoved=True outgoing=2 incoming=2 emptiedIncomingLists=0
[VERIFY-HOTFIX] [NewGameFamily] startup-safehouse-reconciled building=... business=... owner=... front=...
```

No `After Prohibition Family` baseline appeared in the checked logs, so the new scaffold DLL is not active in the live plugin load yet. The live game is still using `GameplayTweaks` for relationship cleanup and startup family/safehouse repair behavior.

## Migration Table

| Area | Current owner | Current source | Current log tag | Type | Risk | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| Spouse relationship link guard | `GameplayTweaks` | `GameplayTweaks\Features\Relationships\GameplayTweaksPlugin.MarriageAndHiringPatches.cs` / `SpouseEthnicityLinkPatch` | none unless failure | behavior-changing | blocks invalid links during `PeopleTracker.FindMatchAndLinkCouple`; wrong behavior can allow family marriages | Move after classifier bridge proves safe |
| Spouse candidate score guard | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `SpouseEthnicityCandidatePatch` | none unless failure | behavior-changing | score override can hide valid candidates or allow invalid ones | Move with spouse safety classifier |
| Relationship safety predicate | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `IsForbiddenSpouseCandidate` | none | classifier-only | central logic for same person, dead person, same `famId`, and family links | Move early as public bridge |
| Random spouse selection | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `FindRandoToMarryPatch` | `[GameplayTweaks] FindRandoToMarryPatch failed` on failure | behavior-changing | direct replacement for spouse search; can cause father/spouse and same-family issues if wrong | Move after read-only audit and classifier |
| Spouse search state | `GameplayTweaks` | `GameplayTweaks\GameplayTweaksPlugin.cs` / `TryStartSpouseSearch`, `LookingForSpouse`, `SpouseSearchStartDay`, `NextSpouseSearchDay`, `SpouseSearchResolveNotBeforeDay`, spend fields | mixed `CrewRelations`/UI flow | behavior and UI-facing | stored crew state and popup text are tied to UI handler | Keep fallback until bridge exists; UI prompt stays outside Family |
| Spouse found popup | `GameplayTweaks` | `GameplayTweaksPlugin.cs` / `_spouseFoundPopup`, `_spouseFoundText`, `ShowSpouseFoundPopup` | none | UI-facing | popup docking/retheme belongs in UI split | Do not move to Family |
| Pregnancy scheduling button | `GameplayTweaks` | `GameplayTweaksPlugin.cs` / `OnMarryChild` | `[VERIFY-HOTFIX] [Family] pregnancy-scheduled ...` | behavior-changing | directly mutates `PersonData.futurekids`; bad checks can duplicate or crash births | Audit first; move to Family after child lifecycle phase |
| Pregnancy runtime state | `GameplayTweaks` | `GameplayTweaksPlugin.cs` / `AwaitingChildBirth`, `LastFutureKidsCount`, pregnancy constants | `[Family]` when scheduled | save/state | state is in existing crew save structure | Keep fallback until old-save compatibility is validated |
| Family hire popup | `GameplayTweaks` | `GameplayTweaksPlugin.cs` / `CreateFamilyHirePopup`, `RefreshFamilyHireList`, `CreateFamilyHireEntry`, `HireSpecificRelative` | `[CrewHire] family-hire-blocked ...` | UI-facing and behavior | UI list can show blank entries; hire action touches crew assignment | UI stays in `AfterProhibitionUI` or fallback; Family can expose relative classifier later |
| Relative discovery | `GameplayTweaks` | `GameplayTweaksPlugin.cs` / `FindAllRelatives`, `AddHireableRelativesForPeep`, `CanHireFamilyRelative` | none | classifier/listing | reads relationship graph and filters dead/nonhireable relatives | Move later as read-only family graph helper, not UI |
| Parent fallback | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `HumanStartupParentFallbackPatch` | `[NewGameFamily] startup-parent-fallback ...` | behavior-changing repair | creates or links parents and spouse; can affect new game family validity | Move after read-only startup audit |
| Root family candidate placement | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / root-family helper methods under parent fallback | `[NewGameFamily] startup-root-family-placement-warning ...` | behavior-changing repair | creates/positions generated family people | Move with startup family generation phase |
| Manual spouse candidate for generated parent | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `FindManualSpouseCandidate` and parent fallback helpers | currently under `[NewGameFamily]` flow | behavior-changing | can pick invalid relatives if classifier wrong | Move with spouse safety bridge |
| Human startup safehouse replacement | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `HumanStartupSafehouseReplacementPatch.Prefix` | `[NewGameFamily] startup-safehouse-replaced-missing-business ...` | startup/economy repair | touches safehouse building, business, owner, front setup | Do not move to Family in early phases |
| Human startup safehouse ownership reconcile | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `CreateSafehouseOrBusinessPostfix` | `[NewGameFamily] startup-safehouse-reconciled ...` | startup/economy repair | touches territory, safehouse data, dirty-cash territory refresh | Keep in GameplayTweaks or move to startup/economy owner later |
| Startup generated gang candidate | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `StartupGeneratedPopulationPatch.GangCandidatePostfix` | `[NewGameFamily] startup-generated-gang-candidate ...` | startup generation | can create candidate people for gangs | Move only person/family validity parts to Family |
| Startup generated cop candidates | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `StartupGeneratedPopulationPatch.CopsAssignPrefix` | `[NewGameFamily] startup-generated-cop-candidate ...` | startup generation | touches police station staffing | Move only generic generated-person safety later |
| Startup generated fed candidates | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `StartupGeneratedPopulationPatch.FedSetupPrefix` | `[NewGameFamily] startup-generated-fed-candidate ...` | startup generation | touches fed setup | Move only generic generated-person safety later |
| Standalone generated person reflection | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `TryCreateStandaloneCandidate`, `LogStandaloneCreationFailure` | `[NewGameFamily] startup-standalone-candidate-create-failed ...` | repair/reflection | signature-sensitive; can break map load if wrong | Audit and verify with live assembly before migration |
| Starter-pack null guard | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `StartupStarterPackNullGuardPatch` | `[NewGameFamily] startup-starter-pack-skipped ...` | startup guard | protects new game startup but not family-specific | Keep outside Family unless failure is person-data-only |
| NPC safehouse owner guard | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `StartupNpcSafehouseOwnerGuardPatch` | `[NewGameFamily] startup-npc-safehouse-owner-assignment-skipped ...` | startup guard | business/safehouse owner assignment, not pure family | Keep outside Family |
| Hireable age detour | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `HireableAgePatch` | `[VERIFY-HOTFIX] [Hiring] businessAssignedAllowed=...` | behavior-changing | broad hireability change can affect vanilla hire menus | Keep outside Family; Family may provide age helper only if needed |
| Business owner eligibility | `GameplayTweaks` | `MarriageAndHiringPatches.cs` / `PotentialBizOwnerEligibilityPatch` | `[VERIFY-HOTFIX] [Hiring] biz-owner-...` | behavior-changing | protects against politicians, crew, gang-affiliated candidates owning businesses | Keep in GameplayTweaks/compat until a hiring/startup owner exists |
| Dead relationship cleanup runtime reset | `GameplayTweaks` | `GameplayTweaks\Features\Relationships\GameplayTweaksPlugin.DeadRelationshipCleanupPatch.cs` / `ResetDeadRelationshipCleanupRuntime` | none | runtime state | prevents stale cleanup queues across new games | Move with cleanup migration |
| Dead relationship immediate scrub | `GameplayTweaks` | `DeadRelationshipCleanupPatch.cs` / `TryScrubDeadRelationshipState`, `TryScrubDeadRelationshipStateImmediate` | `[RelationshipCleanup] dead-peep scrub ...` | repair | removes outgoing and incoming links for dead peeps | Move after read-only audit validates counts |
| Dead relationship startup batch | `GameplayTweaks` | `DeadRelationshipCleanupPatch.cs` / `FlushPendingDeadRelationshipStartupScrubs` | `[RelationshipCleanup] startup-batch ...` | repair/performance | protects startup timing; bad timing can hit missing trackers | Move with cleanup migration, preserving deferred batching |
| Death hooks | `GameplayTweaks` | `DeadRelationshipCleanupPatch.cs` / `DeadRelationshipCleanupPatch.ApplyPatch` | `[RelationshipCleanup] patch applied crewDeath=True peopleDeath=True` | Harmony patch | patches `PlayerCrew.ProcessCrewMemberDeath` and `PeopleTracker.MarkAsDead` | Move after classifier/audit phases |
| Crew relationship menu/buttons | `GameplayTweaks` | `GameplayTweaksPlugin.cs` / `CrewRelationshipHandlerPatch` | `[CrewRelations] crew-button-added ...` | UI-facing | recent blank face/name and panel placement bugs are UI problems | Keep in UI split, not Family |

## Recommended Migration Order

1. Add read-only relationship/family audit in `AfterProhibitionFamily`.
2. Move relationship safety classification into a public bridge.
3. Delegate spouse candidate checks from `GameplayTweaks` while keeping fallback.
4. Add pregnancy and `futurekids` audit before moving scheduling or birth logic.
5. Move dead relationship cleanup only after audit logs show invalid/dead refs reliably.
6. Move startup parent fallback after new-game family audit is stable.
7. Leave safehouse ownership, business owner selection, hire menu UI, and crew relationship panels outside Family until their own owners are ready.

## Phase 3 Readiness Notes

Phase 3 should be read-only and should tolerate the plugin loading at the main menu before the session entity manager exists. Use a retry loop similar to the economy plugin's delayed diagnostics.

Minimum Phase 3 counts:

- total tracked people;
- living people;
- dead people;
- people with missing `PersonData`;
- spouse links;
- same-family spouse links;
- spouse links involving dead people;
- family links;
- dead incoming relationship refs;
- dead outgoing relationship source lists;
- future child entries;
- invalid future child mothers.

Expected first log shape:

```txt
[After Prohibition Family] relationship-audit source=start-1s people=... living=... dead=... missingPersonData=... spouseLinks=... invalidSpouses=... familyLinks=... deadRefs=... futureKids=... invalidFutureKids=...
```

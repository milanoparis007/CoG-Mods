# AfterProhibitionPolitics Phase Prompts

Use this plan to split political starter quests, political bribes, judge/law-office systems, and campaign/election helpers out of `GameplayTweaks` into a separate BepInEx plugin named `AfterProhibitionPolitics`.

## Goal

Create a politics-only owner for:

- the political starter quest, especially the New York start issue;
- mayor/political bribe state and cleanup;
- judge bribe state;
- law-office and lawyer-retainer helpers;
- campaign and election helper diagnostics;
- future politics-specific compatibility bridges.

This plugin must not own:

- shop buy/sell fixes, banks, warehouses, dirty-cash routing, or business stock repair, which belong in `AfterProhibitionEconomy`;
- popup docking, retheme work, portrait fixes, crew buttons, or sidebar visuals, which belong in `AfterProhibitionUI`;
- external DLL detection, broad-unpatch protection, or cheat DLL classification, which belongs in `AfterProhibitionCompatibility`;
- spouse search, pregnancy, children, family generation, or relationship cleanup, which belongs in `AfterProhibitionFamily`;
- route travel continuation, vehicle movement authority, combat, robbery, territory simulation, or safehouse relocation.

## Current Live Signals

Recent logs show politics behavior is still mostly owned by `GameplayTweaks`:

```txt
[GameplayTweaks] Multi-crew vehicle: CivicComponent.ShowPoliticsDialog building interaction scope applied
[GameplayTweaks] Gameplay Tweaks Extended loaded ... vehiclePoliticsEnabled=True
[VERIFY-HOTFIX] [Compat] cheat core=True election=False bossManager=False
[VERIFY-HOTFIX] [Compat] cheat authority=gameplaytweaks blockedElectionManager=False
[Info   :Dirty Cash Economy] [LegalBusiness] Found player-legal module: player-legal-lawoffice
```

No `AfterProhibitionPolitics` load line is present yet. That means the first pass should be a scaffold and read-only audit, not a behavior move.

## Phase 1 - Standalone Plugin Scaffold

Status: completed 2026-05-12.

Implementation notes:

- Added `AfterProhibitionPolitics\AfterProhibitionPolitics.csproj`.
- Added `AfterProhibitionPolitics\AfterProhibitionPoliticsPlugin.cs`.
- Added `AfterProhibitionPolitics\README.md`.
- Added a read-only `IsPoliticsBridgeAvailable()` and `GetPoliticsBridgeVersion()` bridge for later GameplayTweaks delegation checks.
- No political starter quest, bribe, judge, law-office, campaign, election, UI, economy, family, route, combat, compatibility, or save behavior was moved in this phase.

Prompt:

```txt
Create a new standalone BepInEx plugin project named AfterProhibitionPolitics. Keep it separate from GameplayTweaks, AfterProhibitionCompatibility, AfterProhibitionEconomy, AfterProhibitionFamily, AfterProhibitionUI, and AfterProhibitionAssets. Add a minimal plugin class, Harmony ID, logger, config section, and delayed politics baseline log. Do not move starter quest, bribe, judge, law-office, campaign, or election behavior yet.

Validation:
- dotnet build AfterProhibitionPolitics\AfterProhibitionPolitics.csproj -c Release
- Confirm no dependency on GameplayTweaks.
- Confirm no economy, UI, route, family, combat, or external-DLL detection patches are included.
```

Expected files:

```txt
AfterProhibitionPolitics/
  AfterProhibitionPolitics.csproj
  AfterProhibitionPoliticsPlugin.cs
  README.md
```

Expected logs:

```txt
[After Prohibition Politics] politics baseline scheduled ownsUi=False ownsEconomy=False ownsFamily=False ownsRoutes=False ownsCompatibility=False
[After Prohibition Politics] After Prohibition Politics loaded phase=scaffold
```

## Phase 2 - Politics Migration Inventory

Status: completed 2026-05-12.

Implementation notes:

- Checked live `Player.log` and `Player-prev.log`; `AfterProhibitionPolitics` was not present in the live run yet, while `GameplayTweaks` still owned `RunNewGamePoliticianSetup`, politics starter retries, political/election compatibility checks, retainer patches, and `CivicComponent.ShowPoliticsDialog` vehicle-scope handling.
- Added `docs\ai\after-prohibition-politics-migration-inventory.md`.
- Marked save/load fields, UI button creation, vehicle politics dialog scope, external election-cheat detection, and law-office module mutation as areas that should not move early.
- Identified Phase 3 as a read-only politics audit before any starter quest or bribe behavior migration.

Prompt:

```txt
Inventory current politics code in GameplayTweaks and decompiled game anchors. Create a migration table listing the current source file, method or patch, current log tag, behavior risk, and whether the item should move now, later, or stay.

Validation:
- No code migration yet.
- Include political starter quest, mayor/political bribes, judge bribes, lawyer retainer, law-office interactions, campaign helpers, election helpers, political favor use, and politics-dialog vehicle interaction patches.
- Mark read-only diagnostics, save-state fields, UI buttons, payment logic, and behavior-changing patches separately.
```

Suggested doc:

```txt
docs/ai/after-prohibition-politics-migration-inventory.md
```

Likely source anchors:

```txt
GameplayTweaks\PoliticsManagerPatches.cs
GameplayTweaks\GameplayTweaksPlugin.Pacts.cs
GameplayTweaks\GameplayTweaksPlugin.SaveLoad.cs
GameplayTweaks\GameplayTweaksPlugin.FeatureRegistrars.cs
GameplayTweaks\GameplayTweaksPlugin.Compat.cs
decompiled\Game.Session.Sim\PoliticsManager.cs
decompiled\Game.Session.Entities\CivicComponent.cs
decompiled\Game.Services\PoliticsSettings.cs
decompiled\Game.Session.Data\GrantDonationToPolitician.cs
decompiled\Game.Session.Data\GrantControlPolitician.cs
decompiled\Game.Session.Data\GrantCopsPaidOffInPrecinct.cs
```

## Phase 3 - Read-Only Politics Audit

Status: completed 2026-05-12.

Implementation notes:

- Updated `AfterProhibitionPolitics` to `0.2.0`.
- Added `AfterProhibitionPolitics\PoliticsStartupAudit.cs`.
- Added `Features.EnableStartupPoliticsAudit` and `Features.PoliticsAuditSampleLimit`.
- The audit waits for the politics manager, human player, quest manager, map config, entity manager, and a populated building cache before logging.
- Logs now include politics manager, map, procgen readiness, ward/politician counts, election stage/candidates, New York starter quest known-state, safehouse ward politician validity, fallback starter politician validity, and law-office template/module counts.
- This phase does not create quests, pay bribes, change elections, mutate law-office modules, change UI, move save data, or alter `GameplayTweaks` fallback behavior.

Prompt:

```txt
Move read-only politics diagnostics into AfterProhibitionPolitics. Add a startup audit that waits for the game session, then logs whether PoliticsManager exists, whether politicians are available, whether the human starting ward has a valid politician, whether the political starter quest looks active or completed, whether campaign/election data can be found, and whether law-office templates/modules exist.

Validation:
- Do not create quests.
- Do not alter bribe state.
- Do not spend money.
- Do not change elections, campaigns, candidates, politicians, judges, law offices, routes, UI, or save data.
- Logs must include a clear reason when starter quest prerequisites are missing.
```

Expected logs:

```txt
[After Prohibition Politics] politics-audit source=start-1s-ready-attempt-... map=... manager=True wards=... validCurrentPoliticians=... electionStage=...
[After Prohibition Politics] politics-starter-audit source=start-1s-ready-attempt-... active=... waiting=... completed=... safehousePoliticianValid=... fallbackPoliticianValid=... reason=...
[After Prohibition Politics] law-office-audit source=start-1s-ready-attempt-... scanned=... templateMatches=... moduleMatches=... playerOwned=...
```

## Phase 4 - Political Starter Quest Bridge

Status: completed 2026-05-12.

Implementation notes:

- Updated `AfterProhibitionPolitics` to `0.3.0`.
- Added `AfterProhibitionPolitics\PoliticsStarterQuestBridge.cs`.
- Added `Features.EnablePoliticalStarterQuestBridge`, default `true`.
- Hardened the Phase 3 audit readiness so it waits for non-empty politics wards instead of logging before `RunNewGamePoliticianSetup` populates ward data.
- The bridge patches `PoliticsManager.OnSystemTurn`, `PoliticsManager.IsTimeToTutorialize`, and `PlayerInfo.OnPlayerTurnStarted`.
- The bridge only starts `TutorialManager.QUEST_NY_POLITICS_STARTER` when the map is New York, procgen is ready, the starter quest is not active/waiting/completed, and a valid starter politician can be resolved.
- `GameplayTweaks` still keeps its fallback starter quest logic for installs that do not include `AfterProhibitionPolitics` or have the bridge disabled.
- This phase does not move new-game politician setup, bribes, judge/law-office systems, campaign/election behavior, UI buttons, vehicle politics dialog scope, external election-cheat detection, or save data.

Prompt:

```txt
Move the political starter quest decision logic into AfterProhibitionPolitics behind a config gate. GameplayTweaks should keep fallback behavior until live logs prove AfterProhibitionPolitics is loaded and owns the starter quest. Focus first on New York game starts where the political quest previously failed to show.

Validation:
- New York starts get the political starter quest when prerequisites are valid.
- Non-New York starts do not receive duplicate or invalid starter quests.
- Existing saves do not get duplicate starter quest grants.
- Logs show whether the new plugin owned the decision or GameplayTweaks fallback ran.
```

Expected bridge examples:

```txt
AfterProhibitionPoliticsPlugin.IsPoliticsBridgeAvailable()
AfterProhibitionPoliticsPlugin.OwnsPoliticalStarterQuest()
PoliticsStarterQuestBridge.TryEnsureNewYorkPoliticsStarter(...)
```

Expected logs:

```txt
[After Prohibition Politics] starter-quest ensure result=granted city=NewYork politician=...
[After Prohibition Politics] starter-quest ensure result=skipped reason=already-known
[GameplayTweaks] PoliticsStarter fallback skipped owner=AfterProhibitionPolitics
```

## Phase 5 - Bribe State Bridge

Status: completed 2026-05-12.

Implementation notes:

- Updated `AfterProhibitionPolitics` to `0.4.0`.
- Added `AfterProhibitionPolitics\PoliticalBribeStateBridge.cs`.
- Added `Features.EnableBribeStateAudit` and `Features.EnableBribeStateBridge`.
- Startup diagnostics now include a read-only `bribe-state-audit` after politics readiness.
- The bridge reads existing `GameplayTweaks` state through reflection and reports global human political bribe state, AI mayor bribe counts, per-crew mayor bribe counts, per-crew judge bribe counts, and expired bribe counts.
- Hardened politics audit readiness again so startup diagnostics wait for procgen readiness instead of logging before politician setup finishes.
- This phase does not move bribe payment, political favor execution, stale cleanup, judge bribe execution, retainer logic, UI buttons, save/load fields, campaigns, elections, routes, or economy behavior.

Prompt:

```txt
Move political/mayor bribe classification and stale-bribe cleanup into AfterProhibitionPolitics. Keep existing save fields in GameplayTweaks until the bridge has live validation. Do not move UI button creation in this phase.

Validation:
- Active political bribes still expire correctly.
- Stale bribe state is cleaned after load.
- Safehouse clean-cash checks still use the current payment owner.
- AI mayor bribe state remains readable.
- No existing save loses bribe or retainer state.
```

Expected logs:

```txt
[After Prohibition Politics] bribe-state-audit source=start-1s-ready-attempt-... gameplayTweaks=True humanGlobalActive=... humanGlobalExpireDay=... humanGlobalExpired=...
[After Prohibition Politics] bribe-state-audit source=start-1s-ready-attempt-... aiMayorActive=... aiMayorExpired=... crewMayorActive=... crewJudgeActive=... crewBribeExpired=...
```

## Phase 6 - Judge And Law-Office Bridge

Status: completed 2026-05-12.

Implementation notes:

- Updated `AfterProhibitionPolitics` to `0.5.0`.
- Added `AfterProhibitionPolitics\JudgeLawOfficeStateBridge.cs`.
- Added `Features.EnableJudgeLawOfficeAudit` and `Features.EnableJudgeLawOfficeBridge`.
- Startup diagnostics now include a read-only `judge-law-office-audit` after politics readiness.
- The bridge reads existing `GameplayTweaks` state through reflection and reports judge bribe counts, local heat/legal-risk counts, feds incoming, case dismissed, extra jail year flags, retainer counts, confirmed/unconfirmed retainer counts, retainer total, high retainer counts, law-office template counts, law-office module counts, and player-owned law-office counts.
- Quieted the starter quest bridge so New York pre-procgen turns no longer log repeated `procgen-not-ready` skip lines.
- This phase does not move judge bribe execution, law-office module mutation, lawyer retainer payment, retainer upkeep, trial outcomes, UI buttons, save/load fields, campaigns, elections, routes, or economy behavior.

Prompt:

```txt
Move judge bribe classification, law-office detection, and lawyer-retainer helper logic into AfterProhibitionPolitics. Keep payment execution and save ownership conservative until the helper bridge is proven stable. Do not move unrelated legal business economy behavior.

Validation:
- Judge bribe availability matches current wanted/heat rules.
- Lawyer retainer amount and confirmed state remain stable across save/load.
- Law-office templates/modules are detected without requiring economy ownership.
- Dirty Cash Economy or legal-front modules are not modified by this phase.
```

Expected logs:

```txt
[After Prohibition Politics] judge-law-office-audit source=start-1s-ready-attempt-... judgeActive=... localHeatActive=... fedsIncoming=...
[After Prohibition Politics] judge-law-office-audit source=start-1s-ready-attempt-... retainers=... retainerConfirmed=... retainerTotal=... lawOfficeTemplates=... lawOfficeModules=... lawOfficePlayerOwned=...
```

## Phase 7 - Campaign And Election Helpers

Status: completed 2026-05-12.

Implementation notes:

- Updated `AfterProhibitionPolitics` to `0.6.0`.
- Added `AfterProhibitionPolitics\CampaignElectionStateBridge.cs`.
- Added `Features.EnableCampaignElectionAudit` and `Features.EnableCampaignElectionBridge`.
- Startup diagnostics now include a read-only `campaign-election-audit` after politics readiness.
- The bridge logs active election wards, per-stage election ward counts, candidate counts, candidate stat counts, human-sponsored/supported candidate counts, total votes, pending candidate action entries, election event counts, and campaign action definition counts.
- Follow-up `0.6.1` hardens startup audit readiness so new-game logs wait for populated politician data instead of logging against empty ward shells during safehouse/start-location setup.
- This phase does not change vote totals, campaign actions, candidate ownership, politician control, election timing, UI, external election-cheat detection, save data, routes, economy, family, or compatibility behavior.

Prompt:

```txt
Add read-only campaign and election helper diagnostics to AfterProhibitionPolitics. Keep external ElectionVoteCheat blocking in AfterProhibitionCompatibility. Do not change vote totals, candidate ownership, campaign actions, election dates, or politician control in this phase.

Validation:
- Logs can identify campaign/election availability without mutating state.
- External election cheat detection remains owned by AfterProhibitionCompatibility or GameplayTweaks fallback until a compatibility bridge exists.
- No duplicate campaign actions or politician grants are created.
```

Expected logs:

```txt
[After Prohibition Politics] election-audit manager=True electionActive=... candidates=... humanControlled=...
[After Prohibition Politics] campaign-audit actions=... locked=... available=...
```

## Phase 8 - GameplayTweaks Delegation

Status: completed 2026-05-12 for starter quest delegation. Bribe, judge/law-office, and campaign/election delegation intentionally deferred.

Implementation notes:

- Added a reflection-based `AfterProhibitionPolitics` bridge check in `GameplayTweaks\PoliticsManagerPatches.cs`.
- `GameplayTweaks` now skips its New York starter quest fallback hooks when `AfterProhibitionPoliticsPlugin.OwnsPoliticalStarterQuest()` returns `true`.
- The fallback remains active when `AfterProhibitionPolitics` is missing, disabled, or missing the expected bridge method.
- Logs now show one-time starter delegation or fallback state from the `PoliticsStarter` tag.
- Live validation confirmed `After Prohibition Politics 0.6.1 loaded`, `PoliticsStarter delegated owner=AfterProhibitionPolitics`, and a populated startup audit with `currentPoliticians=21`, `validCurrentPoliticians=21`, and `campaign-election-audit scanErrors=0`.
- Bribe cleanup, judge/law-office behavior, campaign/election behavior, UI, save/load, and payment logic were not delegated in this pass because those politics bridges are still read-only.

Prompt:

```txt
Add delegation checks in GameplayTweaks so politics-specific behavior is skipped when AfterProhibitionPolitics is loaded and owns that slice. Keep fallback enabled for users who do not install AfterProhibitionPolitics.

Validation:
- GameplayTweaks still works alone.
- GameplayTweaks plus AfterProhibitionPolitics does not double-grant starter quests or double-clean bribe state.
- Logs clearly show delegated, fallback, and disabled states.
```

Expected logs:

```txt
[GameplayTweaks] PoliticsStarter delegated owner=AfterProhibitionPolitics
[GameplayTweaks] PoliticalBribe cleanup delegated owner=AfterProhibitionPolitics
[GameplayTweaks] Politics fallback active reason=bridge-missing
```

## Phase 9 - Packaging And Guide

Status: completed 2026-05-12.

Implementation notes:

- Staged `AfterProhibitionPolitics.dll` in the Personal release plugin folder.
- Staged `AfterProhibitionPolitics.dll` in the Public `Days Of Prohibition v1.3.98` release plugin folder.
- Updated the root release `GUIDE.md` and public `GUIDE.txt` to include `AfterProhibitionPolitics.dll`.
- Updated guide wording so `GameplayTweaks` remains required while standalone plugins delegate only proven slices and keep fallback behavior.
- Added politics log markers for starter delegation and read-only politics audits.

Prompt:

```txt
Stage AfterProhibitionPolitics in the Personal and Public release plugin folders. Update the release guide and changelog with install order, ownership boundaries, and fallback behavior.

Validation:
- The DLL is staged in both release plugin folders.
- The guide says GameplayTweaks remains required unless a later phase removes that dependency.
- The changelog states which politics behavior is read-only, delegated, or fully owned.
```

Expected staging paths:

```txt
Things To Have\Current After Prohibition Mod\Personal\BepInEx\plugins\AfterProhibitionPolitics.dll
Things To Have\Current After Prohibition Mod\Public\Days Of Prohibition v1.3.98\After Prohibition Mod\BepInEx\plugins\AfterProhibitionPolitics.dll
```

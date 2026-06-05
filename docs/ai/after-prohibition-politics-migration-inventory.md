# AfterProhibitionPolitics Migration Inventory

Inventory date: 2026-05-12.

Live log status:

- `After Prohibition Politics` is not present in the current live log yet.
- `GameplayTweaks` is still the active owner for politics-adjacent runtime patches.
- Current live politics-adjacent signals include `RunNewGamePoliticianSetup`, `CivicComponent.ShowPoliticsDialog`, `CopTrialRetainerPatch`, political/election compatibility checks, and `player-legal-lawoffice` module detection.

## Migration Rules

- Do not move save schema first. `GameplayTweaks` still owns existing save/load fields until bridge behavior is proven live.
- Do not move UI button creation first. The law/crew detail panel buttons are mixed with broader crew UI state.
- Do not move vehicle interaction scope first. `CivicComponent.ShowPoliticsDialog` is currently part of multi-crew vehicle presence and should stay with route/vehicle interaction ownership until politics logic is split cleanly.
- Do not move external election-cheat detection into politics yet. That remains compatibility ownership.
- Start with read-only audit and starter-quest bridge because those are isolated and directly tied to the earlier New York starter quest issue.

## Current Owner Table

| Area | Current source | Current behavior | Current log tag | Risk | Move decision |
| --- | --- | --- | --- | --- | --- |
| New game politician setup guard | `GameplayTweaks\PoliticsManagerPatches.cs` `RunNewGamePoliticianSetup_Prefix` | Replaces vanilla politician setup so empty ward politician lists do not crash and resets runtime campaign state for new games. | `NewGameReset`, source `RunNewGamePoliticianSetup` | High. It changes core setup and touches multiple systems during new game generation. | Later. Audit first, then migrate only after starter quest ownership is proven. |
| New York political starter quest | `GameplayTweaks\PoliticsManagerPatches.cs` `EnsureNewYorkPoliticsStarter` | Ensures the New York starter quest starts after procgen when a valid politician exists. | `PoliticsStarter` | Medium. Directly addresses missing starter quest but can duplicate quests if known-state checks are wrong. | Move next after read-only audit. |
| Starter quest known-state guard | `GameplayTweaks\PoliticsManagerPatches.cs` `IsStarterQuestKnown` and `IsTimeToTutorialize_Postfix` | Suppresses vanilla tutorial trigger if the starter quest is already active, waiting, or completed. | `PoliticsStarter` indirectly | Medium. Bad ownership can suppress vanilla quest without replacement. | Move with starter quest bridge. |
| Starter politician resolution | `GameplayTweaks\PoliticsManagerPatches.cs` `ResolveStarterPolitician`, `ResolveSafehouseWardPolitician`, `IsValidPolitician` | Chooses safehouse ward politician first, then any current/local valid politician. | `PoliticsStarter` skip/fail/start details | Medium. Null/invalid politician handling must be exact. | Move with starter quest bridge. |
| Politics manager system turn hook | `GameplayTweaks\PoliticsManagerPatches.cs` `OnSystemTurn_Postfix` | Retries starter quest ensure on politics system turn. | `PoliticsStarter` | Low to medium. Good retry point, but should not run after delegation twice. | Move with starter quest bridge. |
| Human turn starter retry | `GameplayTweaks\PoliticsManagerPatches.cs` `PlayerInfo_OnPlayerTurnStarted_Postfix` | Retries starter quest ensure at human turn start. | `PoliticsStarter` | Low to medium. Useful fallback timing. | Move with starter quest bridge. |
| Vanilla politics manager anchors | `decompiled\Game.Session.Sim\PoliticsManager.cs` | Owns wards, politicians, election timing, tutorial quest trigger, legislative ticker, election popup. | Game-owned | High. Do not mutate until exact target is needed. | Read-only source of truth. |
| Civic politics dialog | `decompiled\Game.Session.Entities\CivicComponent.cs`; patched by multi-crew vehicle code in `GameplayTweaks` | Shows politics dialog or civic conversation using crew selector or shift-click quick target. | `Multi-crew vehicle: CivicComponent.ShowPoliticsDialog building interaction scope applied` | High. This is route/vehicle presence sensitive, not pure politics. | Stay outside AfterProhibitionPolitics for now. |
| Political bribe UI button | `GameplayTweaks\GameplayTweaksPlugin.cs` `OnBribeMayor`, law/crew UI button creation and refresh | Adds/updates political bribe button, pays safehouse clean cash, activates city hall access. | `Political` | High. UI, payment, save state, and cop-war state are coupled. | Later. Bridge state first; UI remains in GameplayTweaks until stable. |
| Political favor UI/action | `GameplayTweaks\GameplayTweaksPlugin.cs` `OnPoliticalFavor`, `ProcessPendingPoliticalFavorUse`, `CanUsePoliticalFavor`, `TryUsePoliticalFavor` | Uses political bribe access to pay for cop-war relief via reflection into cop-war helpers. | `Political` | High. Coupled to combat/cop-war incident state and UI frame timing. | Later. Classify state first; execution stays in GameplayTweaks. |
| Human political bribe state | `GameplayTweaks\GameplayTweaksPlugin.cs` `_globalMayorBribeActive`, `_globalMayorBribeExpireDay`, `IsHumanPoliticalBribeActive`, `ExpireHumanPoliticalBribeIfNeeded` | Tracks global human political bribe access and expiration. | `Political` | Medium. State is simple but save-owned by GameplayTweaks. | Move as bridge after starter quest. Save fields stay in GameplayTweaks initially. |
| AI mayor bribe state | `GameplayTweaks\GameplayTweaksPlugin.cs` `AiGangMayorBribeExpireDayByGang`, `TryBribeMayorForGang`, `GetAiGangMayorBribeActive`; `GameplayTweaksPlugin.Pacts.cs` AI risk calls | Lets AI gangs use mayor bribes around boss/legal risk. | `AiCrewRelations`, warnings from bribe helper | Medium to high. AI decision loops and money are involved. | Later. Read-only classify before moving. |
| Judge bribe UI/action | `GameplayTweaks\GameplayTweaksPlugin.cs` `OnBribeJudge`, law/crew UI refresh | Pays clean safehouse cash and sets per-crew `JudgeBribeActive`. | `Political` | Medium. Per-peep state, local heat, and payment checks are coupled. | Later. Bridge availability/state before execution. |
| Lawyer retainer payment UI | `GameplayTweaks\GameplayTweaksPlugin.cs` `OnPayLawyer`, `OnPayLawyer10k`, `TryPayLawyerRetainer`, `CanPayLawyerRetainer` | Adds $1k/$10k to retainer from safehouse clean cash. | `Retainer`, `GameplayTweaks` | Medium. Payment and crew UI are coupled. | Later. Keep UI in GameplayTweaks until bridge helpers are stable. |
| Lawyer retainer storage and confirmation | `GameplayTweaks\GameplayTweaksPlugin.cs` `LawyerRetainer`, `LawyerRetainerConfirmed`, `LastRetainerDeductDay`, `OnToggleRetainerConfirm` | Per-crew retainer amount and active confirmation flag. | `Retainer` | Medium. Existing save compatibility matters. | Later. State bridge first; save stays in GameplayTweaks. |
| Retainer upkeep and trial assist | `GameplayTweaks\GameplayTweaksPlugin.cs` `ApplyRetainerUpkeep`; `CopTrialRetainerPatch` | Burns retainer over time and can assist trial/imprisonment outcomes. | `Retainer`, `CopTrialRetainerPatch applied` | High. Touches jail/trial outcomes. | Later after judge/law-office bridge audit. |
| AI lawyer retainer funding | `GameplayTweaks\GameplayTweaksPlugin.Pacts.cs` `TryFundAiLawyerRetainer` and risk calls | AI gangs fund retainers for bosses/crew when risk is high. | `AiCrewRelations` | Medium to high. AI money and risk loops are involved. | Later. Keep in GameplayTweaks until state bridge exists. |
| Save/load political fields | `GameplayTweaks\GameplayTweaksPlugin.SaveLoad.cs` | Saves global mayor bribe, AI bribe dictionaries, per-crew mayor/judge bribe, retainer amount, retainer confirmation, retainer deduct day, and verification counters. | `Political` stale cleanup on load | High. Bad migration can break old saves. | Stay in GameplayTweaks until final delegation phase. |
| Stale human bribe cleanup after load | `GameplayTweaks\GameplayTweaksPlugin.SaveLoad.cs` `NormalizeHumanPoliticalBribeAfterLoad` | Clears expired global human bribe after load. | `Political load-cleared-stale-bribe` | Low to medium. Good first bribe bridge candidate if save owner remains GameplayTweaks. | Move after starter bridge as delegated helper. |
| Election cheat detection | `GameplayTweaks\GameplayTweaksPlugin.Compat.cs`; future owner `AfterProhibitionCompatibility` | Detects `ElectionVoteCheat.dll` / `com.pia.electionmanager` and blocks high-risk overlap. | `Compat` | Medium. External mod safety belongs with compatibility matrix. | Stay outside politics for now. |
| Campaign/election data anchors | `decompiled\Game.Services\PoliticsSettings.cs`, `decompiled\Game.Session.Data\GrantDonationToPolitician.cs`, `GrantControlPolitician.cs`, `GrantCopsPaidOffInPrecinct.cs` | Define campaign actions, laws, grants, and politician/election effects. | Game-owned | Medium. Good diagnostic targets, risky mutation targets. | Read-only audit first. |
| Law-office module detection | Live data/log references to `player-legal-lawoffice`; economy/legal-front code also sees it | Law-office content exists as legal business/front data, but retainer/judge logic is runtime in GameplayTweaks. | Dirty Cash Economy `LegalBusiness` | Medium. Crosses economy and politics boundaries. | Read-only politics audit can detect it; do not mutate modules here. |

## Phase 3 Read-Only Audit Targets

The next implementation phase should add read-only code to `AfterProhibitionPolitics` that logs:

- whether the plugin is loaded in the live install;
- whether `Game.Game.ctx`, `simman`, `politics`, `players.Human`, and the human safehouse exist;
- map id and turn/procgen readiness;
- whether Shadow Government appears installed when the game exposes that state safely;
- total wards, wards with current politicians, wards with local politicians, and wards with no politicians;
- whether the human safehouse ward has a valid politician;
- whether the New York starter quest is active, waiting, completed, or missing;
- whether a fallback starter politician candidate can be resolved;
- election stage/timing if accessible without mutating state;
- law-office template/module counts if accessible by read-only lookups;
- current saved human global bribe state only if exposed through a bridge later.

## Suggested Migration Order

1. Add Phase 3 read-only politics audit in `AfterProhibitionPolitics`. Completed in `0.2.0`.
2. Add a starter quest bridge in `AfterProhibitionPolitics`, with `GameplayTweaks` fallback still active when the bridge is missing. Completed in `0.3.0`.
3. Add `GameplayTweaks` delegation checks for only starter quest ownership.
4. Add bribe state bridge methods, keeping save/load in `GameplayTweaks`. Read-only classification completed in `0.4.0`; cleanup/payment ownership remains in `GameplayTweaks`.
5. Add judge/law-office helper bridge, still read-only for module detection and conservative for payment execution. Read-only classification completed in `0.5.0`; judge payment, retainer payment/upkeep, trial outcomes, and UI remain in `GameplayTweaks`.
6. Add campaign/election read-only helpers. Completed in `0.6.0`; campaign actions, vote changes, politician control, election timing, external election-cheat detection, UI, and save data remain outside `AfterProhibitionPolitics`.
7. Move only proven behavior out of `GameplayTweaks`; leave vehicle dialog scope and external election detection with their current owners unless a later plan deliberately moves them. Starter quest fallback delegation from `GameplayTweaks` to `AfterProhibitionPolitics` is in progress; bribe cleanup, judge/law-office behavior, campaign/election behavior, UI, payments, and save data remain with current owners.

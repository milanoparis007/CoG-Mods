# AfterProhibitionCompatibility Migration Inventory

Date: 2026-05-11

Phase 2 goal: inventory current external-DLL compatibility code before moving behavior. This document separates read-only detection/logging from behavior gates and gameplay patches so `AfterProhibitionCompatibility` can take ownership in safe phases.

## Live Log Snapshot

Current live `Player.log` tail did not show `After Prohibition Compatibility`, so the new plugin has not run in the live session being inspected.

Relevant current signals:

```txt
[Info   :Dirty Cash Economy] [LegalBusiness] FindUpgradesOrNull Prefix: module=player-legal-funeral, isFrontRoom=True
[Info   :Dirty Cash Economy] [LegalBusiness] FindAllModuleDefsExpensive returned 890 configs
[Info   :Dirty Cash Economy] [LegalBusiness] Found 40 player-legal modules after filtering
[VERIFY-HOTFIX] [ResEvents] ticker-scan source=human-turn-start ...
```

This confirms the external Dirty Cash Economy DLL is active in the current profile, while the new compatibility plugin still needs live install/restart validation.

## Current Detection Owners

| Source | Detected external item | DLL / GUID / token | Current log tag | Kind | Risk | Move decision |
| --- | --- | --- | --- | --- | --- | --- |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Dirty Cash Economy | `DirtyCashEconomy.dll` | `[GameplayTweaks] DirtyCashEconomy.dll detected`, `[VERIFY-HOTFIX] [Compat] easy name=DirtyCashEconomy` | Detection-only plus behavior flag | High: money/runtime deferral affects purchase flow and route selection cleanup | Move now in Phase 3 as read-only detection; behavior stays |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Dirty Cash Volume Fix | `DirtyCashVolumeFix.dll` | `[GameplayTweaks] DirtyCashVolumeFix.dll detected`, `[VERIFY-HOTFIX] [Compat] easy name=DirtyCashVolumeFix` | Detection-only plus behavior flag | Medium: overlaps volume/stock patches | Move now in Phase 3 as read-only detection |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | 10K Button & Input / PIA UI Enhancer | `10K Button & Input.dll`, `com.pia.cityofgangsters.uienhancer`, `UIEnhancer` | `[VERIFY-HOTFIX] [Compat] easy name=UIEnhancer10K` | Detection-only plus UI gate | High: route/default button cleanup can remove valid choices if misclassified | Move now in Phase 3 as read-only detection; bridge in Phase 4 |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Safebox standalone UI | `Safebox.dll` | `[GameplayTweaks] Safebox.dll detected`, `[VERIFY-HOTFIX] [Compat] launcher ... safeboxSignal=` | Detection-only plus UI button gate | Medium: duplicate crew safebox buttons and launchers | Move now in Phase 3 as read-only detection |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Safebox via launcher | `ModLauncher.dll`, `ProhibitionLauncher.dll`, `TraplifeModLauncher.dll`, ASCII tokens `ToggleSafeBoxMod`, `ToggleSafebox`, `Safebox` | `[GameplayTweaks] ModLauncher safebox support detected` | Detection-only with binary token scan | Medium-high: byte scans are fragile and belong outside gameplay startup | Move later in Phase 3 after filename/GUID detection; keep token scan optional |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | ModLauncher bridge | `com.mods.modlauncher`, `ProhibitionLauncher.dll`, `TraplifeModLauncher.dll` | `[VERIFY-HOTFIX] [Compat] launcher bridge=` | Detection-only | Medium: launcher load order can influence other plugin UI | Move now in Phase 3 |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | PIA ModLauncher | `com.pia.modlauncher` | `[VERIFY-HOTFIX] [Compat] launcher ... pia=` | Detection-only | Medium | Move now in Phase 3 |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Crew Hire Manager | `com.pia.crewhiremanager`, `CrewHireManager.dll` | `[VERIFY-HOTFIX] [Compat] easy name=CrewHireManager` | Detection plus hiring behavior gate | Medium: hostile inspect/hire target overlap | Move detection now; keep hiring behavior in GameplayTweaks/Family |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Remote Interaction | `com.pia.remoteinteraction`, `RemoteInteraction.dll` | `[VERIFY-HOTFIX] [Compat] easy name=RemoteInteraction` | Detection plus hiring behavior gate | Medium: conversation target overlap | Move detection now; keep behavior gate as fallback until bridge |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | GameOptimizer | `com.mods.gameoptimizer`, `GameOptimizer` | `[VERIFY-HOTFIX] [Compat] easy name=GameOptimizer` | Detection-only | Low-medium: duplicate optimizer warning only | Move now in Phase 3 |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Third-party optimizer | `com.modding.cityofgangsters.optimizer`, `CityOfGangstersOptimizer.dll` | `[VERIFY-HOTFIX] [Compat] easy name=ThirdPartyOptimizer`, blocked-combo warning | Detection-only plus warning | Medium: dual optimizer profile warning | Move now in Phase 3 |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Mafia Hierarchy | `com.pia.mafiahierarchy`, `MafiaHierarchy` | `[VERIFY-HOTFIX] [Compat] adapter name=MafiaHierarchy` | Detection plus territory/war visual gate | High: territory, war, and UI overlap | Move detection now; behavior decisions later |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | GangWars | `com.pia.gangwars`, `GangWars.dll` | `[VERIFY-HOTFIX] [Compat] adapter name=GangWars` | Detection plus adapter gates | High: pacts, alliances, colors, retaliation, tribute | Move detection now; keep adapter behavior in GameplayTweaks until bridge |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Territory Expansion | `com.modder.territoryautoexpand`, `TerritoryExpansionPatch.dll` | `[VERIFY-HOTFIX] [Compat] adapter name=TerritoryExpansion` | Detection plus territory behavior gates | High: territory authority and visual ownership | Move detection now; keep gates local until bridge |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Ticker UI mods | `com.pia.tickerenhancer`, `Tickerenhancer.dll`, `OutpostTickerMod.dll` | `[VERIFY-HOTFIX] [Compat] easy name=TickerEnhancer` | Detection plus duplicate suppression gate | Medium: duplicate ticker suppression can hide event signals | Move detection now; keep suppression behavior local |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Core cheat menu | `com.pia.cogcheat`, `Cog Ultimate Cheat.dll` | `[VERIFY-HOTFIX] [Compat] cheat core=` | Detection plus authority gate | Medium-high: heat, war, trial, pact authority overlap | Move detection later after core bridge exists |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Election cheat | `com.pia.electionmanager`, `ElectionVoteCheat.dll` | `[VERIFY-HOTFIX] [Compat] cheat ... election=`, blocked warning | Detection plus blocked plugin decision | High: politics state overlap | Move detection later; keep block decision local until Politics split |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Boss Manager cheat | `com.pia.bossmanager`, `Boss Manager.dll`, `BossManager.dll` | `[VERIFY-HOTFIX] [Compat] cheat ... bossManager=`, blocked warning | Detection plus blocked plugin decision | High: boss/family/save overlap | Move detection later; keep block decision local |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | Crew List Sorter / Natural Death style mods | `com.pia.crewlistsorter`, `CrewListSorter.dll`, `Crew Editor.dll`, `CrewEditor.dll`, `com.pia.creweditor` | `[VERIFY-HOTFIX] [Compat] adapter name=CrewListSorter`, `NaturalDeathExternal` | Detection plus family/death behavior gate | High: family, age, death, crew list overlap | Move detection later with Family phase awareness |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | CoGCustomAssets and asset stack | `CoGCustomAssets.dll`, `CustomPortraits.dll`, `Traiticonlimitplugin.dll` | `[VERIFY-HOTFIX] [Compat] adapter name=CustomIcons` | Detection-only | Medium: asset/portrait ownership now belongs near Assets/UI | Move now or after Assets bridge; no gameplay behavior |

## Behavior Consumers And Gates

| Source | Current behavior | Reads detection | Current log tag | Kind | Migration decision |
| --- | --- | --- | --- | --- | --- |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | `ShouldUseExternalSafeboxUI()` disables built-in crew safebox button when safebox UI is detected | `ExternalSafeboxDetected`, `ExternalModLauncherSafeboxDetected` | `[GameplayTweaks] External safebox UI active`, `[VERIFY-HOTFIX] [Compat] activeSubsystems ... safeboxExternal=` | UI behavior gate | Keep until Phase 4 bridge; then query `AfterProhibitionCompatibility` |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | `ShouldDeferDirtyCashRuntime()` defers money/runtime interactions | `ExternalDirtyCashEconomyDetected` | `[VERIFY-HOTFIX] [Compat] activeSubsystems dirtyCashDeferral=` | Economy behavior gate | Keep in GameplayTweaks/Economy; bridge detection later |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | `ShouldSkipUiRetheme()` prevents retheme conflicts | `ExternalUIEnhancerDetected` plus config | `[VERIFY-HOTFIX] [Compat] uiRetheme=` | UI behavior gate | Keep until UI already owns retheme; bridge can replace detection only |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | `ShouldSuppressTickerDupes()` suppresses duplicate ticker events | `ExternalTickerEnhancerDetected` plus config | `[VERIFY-HOTFIX] [Compat] activeSubsystems ... tickerDupes=` | Event/UI behavior gate | Keep local until ticker ownership clarified |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | GangWars/Pact decisions for adapters, tribute, vassals, colors, aggro boost | `ExternalGangWarsDetected` and config | `[VERIFY-HOTFIX] [Compat] gangwars ...` | Gameplay behavior gate | Keep in GameplayTweaks; compatibility plugin should classify only |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | Territory expansion/visual decisions | `ExternalTerritoryExpansionDetected`, `ExternalMafiaHierarchyDetected`, `ExternalGangWarsDetected` | `[VERIFY-HOTFIX] [Compat] territory ...` | Territory behavior gate | Keep in GameplayTweaks until territory system split |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | Core cheat authority guard | `ExternalCoreCheatMenuDetected`, blocked flags | `[VERIFY-HOTFIX] [Compat] cheatCoreAdapter=` | Authority behavior gate | Keep local; move detection only after bridge exists |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | Business-assigned hiring candidate gate | `ExternalCrewHireManagerDetected`, `ExternalRemoteInteractionDetected` | `[VERIFY-HOTFIX] [Compat] hiring businessAssignedAllowed=` | Family/hiring behavior gate | Keep in Family/GameplayTweaks; move detection later |
| `GameplayTweaks/GameplayTweaksPlugin.cs` | Natural-cause death deferral | `ExternalCrewListSorterDetected`, `ExternalNaturalDeathModDetected` | `[VERIFY-HOTFIX] [Compat] activeSubsystems ... naturalDeathAuthority=` | Family/death behavior gate | Keep in Family/GameplayTweaks |
| `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs` | Shop/module list, install safety, illegal backroom, dirty-cash bridge, player money, territory repair, route-adjacent fixes | Dirty Cash state and external Harmony owner info | `[VERIFY-HOTFIX] [DirtyCash] ...` | Economy/gameplay patch set | Stay for now; too broad for compatibility plugin |
| `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.TerritoryAndGangWarsCompatibility.cs` | Territory color, border, corner pick respect fallback and GangWars adapter behavior | GangWars/Territory flags | `[GameplayTweaks] Territory color patch applied`, `[VERIFY-HOTFIX] [Compat] ...` | Territory/UI visual patch set | Stay for now; compatibility plugin should classify external owner only |
| `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.CivicNonpurchaseFallback.cs` | Blocks purchase/takeover prompts for banks, schools, churches | Civic templates, no external detection | `[VERIFY-HOTFIX] [CivicNonpurchase] ...` | Gameplay/content safety patch | Stay in GameplayTweaks/Economy or future Politics/Economy; not an external compatibility item |
| `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.AfterProhibitionAssemblyPort.cs` | Victory goals, season display, portrait table edits | No external detection | `[GameplayTweaks] After Prohibition Assembly-CSharp portability patches applied` | Portability/content patch | Stay out of compatibility plugin |

## Broad-Unpatch And External Patch Ownership

| Source | Current use | Owner target | Risk | Migration decision |
| --- | --- | --- | --- | --- |
| `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs` | Scans/removes or bypasses external Dirty Cash original overrides and patches external Dirty Cash hooks | `com.cogmod.dirtycasheconomy` | Very high: wrong owner classification can remove default game choices or external dirty-cash UI selections | Inventory only now; Phase 5 should expose protect/deny decisions before any behavior moves |
| `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` | ASCII token scans launcher DLL for safebox support | Launcher DLLs | Medium-high: brittle binary scan | Move to compatibility plugin in Phase 3 behind config; never required for gameplay startup |

## Recommended Migration Order

1. Move simple read-only detection into `AfterProhibitionCompatibility`: DirtyCashEconomy, DirtyCashVolumeFix, 10K Button/Input, PIA UI Enhancer, safebox DLL, ModLauncher/Pia launcher, CoGCustomAssets, CustomPortraits, GameOptimizer, third-party optimizer, ticker mods.
2. Add bridge methods and keep GameplayTweaks local fallback: `IsDirtyCashEconomyActive`, `IsTenKInputActive`, `ShouldUseExternalSafeboxUi`, `HasExternalMenuOrSidebarOwner`, `HasCustomAssets`, `HasCustomPortraits`, `HasExternalGangWarsOrTerritory`, `HasExternalTickerEnhancer`.
3. Move higher-risk detection after the bridge is live-tested: GangWars, TerritoryExpansion, MafiaHierarchy, cheat plugins, CrewHireManager, RemoteInteraction, CrewListSorter, CrewEditor/NaturalDeath.
4. Only after logs show delegated detection working, update GameplayTweaks behavior gates one family at a time.
5. Do not move shop, route, family, political, money, territory, or UI rendering patches into `AfterProhibitionCompatibility`.

## Phase 3 Input

Next implementation pass should add a durable detection model inside `AfterProhibitionCompatibility`:

- loaded BepInEx plugin GUIDs;
- loaded assembly names;
- plugin DLL filenames;
- optional ASCII token probes for launcher/safebox support;
- one concise matrix log per session;
- static read-only state with no external DLL compile dependencies.

Validation target:

```txt
dotnet build AfterProhibitionCompatibility\AfterProhibitionCompatibility.csproj -c Release
```

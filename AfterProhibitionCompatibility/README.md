# AfterProhibitionCompatibility

Standalone BepInEx compatibility owner for the After Prohibition mod suite.

Phase 13 adds read-only territory/gang-war/ticker classification on top of the external DLL audit, detection matrix, bridge methods, broad-unpatch owner classification, Dirty Cash classification, 10K Button/Input classification, and election/core cheat classification. It does not patch gameplay, economy, route, family, politics, inventory, shop, territory, or UI rendering behavior.

## Owns

- External DLL/plugin presence logging.
- Bounded all-plugin-DLL audit logging.
- Dirty Cash Economy compatibility classification.
- 10K Button/Input compatibility classification.
- Election/core cheat/boss-manager compatibility classification.
- Territory, gang-war, hierarchy, and ticker compatibility classification.
- Broad-unpatch safety classification.
- Reflection-safe bridge methods for GameplayTweaks and AfterProhibitionUI.
- Current read-only snapshot state for future bridge methods.

## Does Not Own

- Shop buy/sell behavior.
- Bank or warehouse purchase access.
- Delivery route execution.
- Family or relationship rules.
- Political quests.
- UI drawing or retheme patches.

## Runtime Check

Expected log lines:

```txt
[Info   :After Prohibition Compatibility] compat baseline scheduled ownsGameplay=False
[Info   :After Prohibition Compatibility] After Prohibition Compatibility 0.7.4 loaded. compatPatches=0 phase=all-dll-detection ownsGameplay=False
[Info   :After Prohibition Compatibility] external source=start-1s phase=detection-matrix ownsGameplay=False ...
[Info   :After Prohibition Compatibility] external-dll-audit source=start-1s phase=all-dll-detection ownsGameplay=False ...
[Info   :After Prohibition Compatibility] external-detail source=start-1s phase=detection-matrix ...
[Info   :After Prohibition Compatibility] broad-unpatch guard owner=DirtyCashEconomy action=protect reason=route-input ...
[Info   :After Prohibition Compatibility] dirty-cash-classifier active=True volumeFix=True protectRouteInput=True ...
[Info   :After Prohibition Compatibility] tenk-input-classifier tenKInput=True uiEnhancer=True protectRouteInput=True ...
[Info   :After Prohibition Compatibility] election-cheat-classifier coreCheat=True electionCheat=False bossManager=False ...
[Info   :After Prohibition Compatibility] territory-gangwar-classifier gangWars=False territoryExpansion=False mafiaHierarchy=True ...
```

The `external` and `external-detail` lines are read-only. They do not enable, disable, unpatch, or call external DLL behavior.

Bridge methods are public static and intended for optional reflection calls, for example:

```txt
IsDirtyCashEconomyActive()
IsTenKInputActive()
ShouldUseExternalSafeboxUi()
HasExternalMenuOrSidebarOwner()
HasExternalUiEnhancer()
HasCustomAssets()
HasCustomPortraits()
HasExternalGangWarsOrTerritory()
HasExternalGangWars()
HasExternalTerritoryExpansion()
HasExternalMafiaHierarchy()
HasExternalCoreCheatMenu()
HasExternalElectionCheat()
HasExternalBossManagerCheat()
GetCompatibilityBridgeSummary()
GetExternalDllAuditSummary()
GetDirtyCashCompatibilitySummary()
GetDirtyCashDllCheckSummary()
GetDirtyCashPatchOwnerSummary()
IsDirtyCashHarmonyPatchOwnerActive()
GetTenKInputDetectionSummary()
ShouldProtectDirtyCashRouteInput()
ShouldProtectDirtyCashOriginalMethodOverrides()
GetTenKInputCompatibilitySummary()
GetTenKInputOwnerSummary()
ShouldProtectTenKRouteInput()
ShouldProtectExternalButtonInput()
GetElectionCheatCompatibilitySummary()
GetElectionCheatDetectionSummary()
ShouldProtectElectionStateFromExternalCheats()
ShouldProtectBossStateFromExternalCheats()
GetTerritoryGangWarCompatibilitySummary()
GetTerritoryGangWarDetectionSummary()
ShouldProtectTerritoryVisualsFromExternalMods()
ShouldProtectGangWarStateFromExternalMods()
ShouldProtectTickerUiFromExternalMods()
ShouldProtectBroadUnpatchOwner(string owner)
ClassifyBroadUnpatchOwner(string owner)
```

When called before the delayed startup matrix has run, the bridge captures the same read-only snapshot once and logs a concise `bridge snapshot captured` line.

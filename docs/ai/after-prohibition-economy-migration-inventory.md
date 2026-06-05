# AfterProhibitionEconomy Migration Inventory

Created: 2026-05-11

Purpose: identify current economy-adjacent owners in `GameplayTweaks` before moving behavior into `AfterProhibitionEconomy`.

## Live Log Signals

Current live logs do not show `After Prohibition Economy` startup lines yet, which means the scaffold DLL has not been copied into the live BepInEx plugin folder for that run.

GameplayTweaks is still owning the important economy runtime work:

```txt
[GameplayTweaks] Business purchase stock refreshed source=business-update-initial ...
[GameplayTweaks] Empty business module repair source=business-update-initial candidates=2185 repaired=2185 failed=0 stockCandidates=32 stockRefreshed=21 stockFailed=11 day=700952
[GameplayTweaks] DirtyCash runtime module safety sweep source=update-business-modules ...
[Info   :Dirty Cash Economy] [LegalBusiness] Found 40 player-legal modules
```

## Migration Rules

- Move read-only audit and repair code before behavior-changing shop transaction code.
- Keep external DLL detection and broad-unpatch classification in `AfterProhibitionCompatibility`.
- Keep UI rendering, popup layout, and button drawing in `AfterProhibitionUI`.
- Keep route movement authority in `GameplayTweaks` until staged route shop orders are split behind a bridge.
- Do not move family, politics, combat, gang-war, or territory expansion logic into `AfterProhibitionEconomy`.

## Inventory Table

| Area | Current source | Current owner / entry point | Current log tag | Type | Risk | Move decision |
| --- | --- | --- | --- | --- | --- | --- |
| Dirty cash volume fix | `GameplayTweaks/Features/Economy/GameplayTweaksPlugin.DirtyCashAndFrontTracking.cs` | `DirtyCashPatches.ApplyPatches`, `VolumePostfix` on `Game.Services.Resource.FindTotalVolume` | `Dirty cash volume fix applied` | Repair | Medium; resource volume math affects storage and transport | Move later after bridge, but skip if `AfterProhibitionCompatibility` says external DirtyCashVolumeFix is active |
| Dirty cash income conversion | same file | `DirtyCashPatches.DoChangeMoneyPrefix` on `PlayerFinances.DoChangeMoney` | `Dirty cash income conversion patched` | Behavior-changing | High; money routing and dirty-cash storage | Move later, after read-only audit confirms external Dirty Cash owner and safehouse storage behavior |
| Dirty cash trade overdraw guard | same file | `DirtyCashPatches.TradeDoChangeMoneyPrefix` on `PlayerFinances.DoChangeMoney(InventoryModuleData, Price, MoneyReason, EntityID?)` | `Dirty cash trade overdraw guard applied`, `DirtyCashTradeGuard` | Behavior-changing | High; directly affects shop buy/sell and scheduled trade payment | Move after shop buy/sell bridge; this is an economy owner but must remain externally compatible |
| Laundering tick | same file | `ProcessLaundering` | `Laundered $... dirty cash` | Behavior-changing | Medium; safehouse dirty-cash balance and clean-cash payout | Move later with dirty-cash routing, after storage bridge exists |
| Front tracking on takeover | same file | `FrontTrackingPatch.ApplyPatch`, takeover prefix/postfix | `Front tracking patch applied`, `FRONT:` grapevine | Behavior-changing | Medium; front list and grapevine side effects | Move later under front/business resource phase; keep log/grapevine bridge in GameplayTweaks until separated |
| Civic nonpurchase guard | `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.CivicNonpurchaseFallback.cs` | `CivicNonpurchaseFallbackPatch.ApplyPatch` on takeover and special purchase checks | `CivicNonpurchase` | Behavior-changing guard | Medium; blocks buying banks, schools, and churches as territory businesses | Move with banks/warehouses access phase after read-only audit clarifies building purchase vs resource purchase |
| Owned business module choices | `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs` | `FindModulesToAddForSlotPostfix`, `FindUpgradesOrNullPostfix` | external Dirty Cash legal business logs plus GameplayTweaks warnings | Behavior-changing | High; controls which legal/illegal backroom modules appear | Move later with dirty-cash/front business logic; detection stays in Compatibility |
| Owned business install validation | same file | `CanInstallPostfix`, `OnModuleAddConfirmPrefix`, `HandleIllegalBackroomInstallConfirm` | dirty-cash/backroom install warnings | Behavior-changing | High; can block or allow modules | Move later after module-choice audit; keep as one migration unit with module choices |
| Broken business module repair | same file | `TryRepairEmptyBusinessModulesFromConfig`, helpers around line 4581 | `Empty business module repaired`, `Empty business module repair` | Repair | Medium; repairs NPC business module slots and business module lists | Move soon after audit matrix; this is a clean `AfterProhibitionEconomy` owner |
| NPC purchase-stock refresh | same file | `TryRefreshNpcPurchaseStockFromInstalledSources`, `TryForceBusinessModulesUpdate`, helpers around line 4762 | `Business purchase stock refreshed`, `Purchase stock refresh failed` | Repair | Medium-high; directly affects "shops only allow sells" symptom | Move soon, likely before full buy/sell transaction work |
| Runtime Dirty Cash safety sweeps | same file | `UpdateBusinessModulesPostfix`, `BusinessUpdateTickPostfix`, `PlayerInfoOnPlayerTurnStartedPostfix`, force-update helpers | `DirtyCash runtime module safety sweep`, `DirtyCash runtime ... force applied` | Repair / compatibility | High; updates external Dirty Cash backrooms and respect effects | Split: sweep logging and local repair move to Economy, external owner decisions remain Compatibility |
| External Dirty Cash hook protection | same file | `TryPatchExternalDirtyCashConsumerHooks`, `EnsureExternalDirtyCashOriginalOverridesRemoved`, unpatch helpers | `ExternalDirtyCash...` logs | Compatibility / broad-unpatch | High; can break external DLLs | Stay in `AfterProhibitionCompatibility`, not Economy |
| Dirty Cash territory respect refresh | same file | territory reconcile, deferred refresh, visual refresh, node ownership helpers | `TerritoryOwnership`, dirty-cash visual refresh logs | Territory / visuals | High; not pure economy | Stay in GameplayTweaks or move to a future territory system, not Economy |
| AI pickup/dropoff dirty cash guard | same file | `ExecuteAIPickUpDropOffPrefix` on `BuySellUtils.ExecuteAIPickUpDropOff` | warning-only unless blocked | Behavior-changing | Medium; AI buy/sell routing | Move later after human shop access is stable |
| Human shop physical access gate | `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.Interaction.cs` | `HumanBuySellVehiclePhysicalGatePatch` on `ConvoCallbacks.ShowBuySellPopup` and `BuySellUtils.ExecuteHumanBuySell` | `buy-sell-vehicle-physical-blocked`, `route-shop-stage-opened` | Behavior-changing / route-coupled | High; ties shop buy/sell to vehicle physical location | Keep in GameplayTweaks until route authority is stable; later expose economy commit bridge |
| Staged route shop orders | same file | `RouteShopStagingState` | `route-shop-staged`, `route-shop-commit-*`, `route-shop-price-drift` | Behavior-changing / route-coupled | High; crosses route, vehicle, shop, and money systems | Move last, as Phase 11, after shop buy/sell and route continuation are stable |
| Owned-building inventory panel / route-in helpers | same file | owned-building interaction and route-in shop support helpers | `ownedbiz-inventory-*`, route-in logs | UI / route / economy bridge | High; mixed UI and route context | Keep in GameplayTweaks or UI until bridge boundary is explicit |
| Startup safehouse missing-business repair | `GameplayTweaks/Features/Relationships/GameplayTweaksPlugin.MarriageAndHiringPatches.cs` | `HumanStartupSafehouseReplacementPatch` | `startup-safehouse-replaced-missing-business`, `startup-safehouse-reconciled` | New-game repair | High; new-game/family/player setup coupling | Stay in GameplayTweaks/Family setup for now; only revisit if a dedicated safehouse/business startup bridge is created |

## First Safe Move Order

1. Add a read-only economy audit in `AfterProhibitionEconomy` for shop stock, bank/warehouse templates, player-buy offers, and empty business modules.
2. Move business purchase-stock refresh from `DirtyCashEconomyCompatibilityPatch` after the audit can prove candidates and failures.
3. Move empty business module repair after stock refresh, guarded by a config flag and a GameplayTweaks owner-skip bridge.
4. Move bank/warehouse purchase-access fixes only after separating resource purchase access from building takeover blocking.
5. Move dirty-cash trade payment and overdraw logic after shop buy/sell works without external Dirty Cash conflicts.
6. Leave staged route shop orders for the final economy route phase.

## Keep Out Of Economy For Now

- `AfterProhibitionCompatibility` owns external DLL detection, Dirty Cash classifier decisions, 10K input classification, and broad-unpatch protection.
- `AfterProhibitionUI` owns visuals, popups, button rendering, stale portraits, and menu/sidebar retheme work.
- `GameplayTweaks` continues to own route movement authority, multi-crew vehicle physical-location rules, family startup repair, politics, combat, territory, and gang ops.

## Suggested Phase 3 Audit Targets

- Count buildings where `ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, playerBuys: true, playerSells: false)` returns no positive buy offers.
- Break counts out by building template and business template.
- Report bank and wholesale warehouse templates separately.
- Report businesses with installed purchase-producing modules but no positive player-buy offer.
- Report empty/mismatched business module slots without repairing them.
- Include an external-owner line from `AfterProhibitionCompatibility` when present.

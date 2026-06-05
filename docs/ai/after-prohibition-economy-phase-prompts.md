# AfterProhibitionEconomy Phase Prompts

Use this plan to split economy stability, shop purchasing, bank/warehouse access, dirty-cash routing, and front/business resource logic out of `GameplayTweaks` into a separate BepInEx plugin named `AfterProhibitionEconomy`.

## Goal

Create an economy-only owner for:

- shop buy/sell fixes;
- banks and wholesale warehouses purchase access;
- dirty-cash routing decisions;
- front/business resource setup and stock repair;
- business purchase-stock refreshes;
- future staged route shop orders.

This plugin must not own:

- external DLL detection or broad-unpatch classification, which belongs in `AfterProhibitionCompatibility`;
- UI rendering, popup docking, retheme work, portrait fixes, or crew buttons, which belong in `AfterProhibitionUI`;
- delivery-route travel continuation, route movement authority, or vehicle movement;
- spouse search, pregnancy, children, family generation, or relationship cleanup;
- political starter quests, bribes, judge/law office systems, campaign systems, AI combat, or territory simulation.

## Current Live Signals

Recent logs show economy stability is still active and should be isolated:

```txt
[GameplayTweaks] Empty business module repaired source=business-update-initial ...
[GameplayTweaks] Business purchase stock refreshed source=business-update-initial ...
[GameplayTweaks] Empty business module repair source=business-update-initial candidates=2086 repaired=2086 failed=0 stockCandidates=46 stockRefreshed=34 stockFailed=12 ...
[GameplayTweaks] DirtyCash runtime module safety sweep source=update-business-modules ...
[Info   :Dirty Cash Economy] [LegalBusiness] Found 40 player-legal modules
```

The key problem is not only whether shops currently work. The larger risk is that shop stock, legal-front module repair, dirty-cash routing, bank/warehouse purchasing, and route-order logic are mixed into the broad gameplay DLL. Moving economy work into its own plugin should make buy/sell bugs easier to diagnose without touching travel, UI, family, or politics.

## Phase 1 - Standalone Plugin Scaffold

Status: completed 2026-05-11.

Implementation notes:

- Added `AfterProhibitionEconomy\AfterProhibitionEconomy.csproj`.
- Added `AfterProhibitionEconomy\AfterProhibitionEconomyPlugin.cs`.
- Added `AfterProhibitionEconomy\README.md`.
- Added `AfterProhibitionEconomy` to `ClassLibrary1.sln`.
- Built the project in Release and staged the read-only scaffold DLL to the current Personal and Public release plugin folders.
- No economy behavior was moved in this phase.

Prompt:

```txt
Create a new standalone BepInEx plugin project named AfterProhibitionEconomy. Keep it separate from GameplayTweaks, AfterProhibitionCompatibility, AfterProhibitionUI, and AfterProhibitionAssets. Add a minimal plugin class, Harmony ID, logger, config section, and delayed economy baseline log. Do not move economy behavior yet.

Validation:
- dotnet build AfterProhibitionEconomy\AfterProhibitionEconomy.csproj -c Release
- Confirm no dependency on GameplayTweaks.
- Confirm no route travel, UI rendering, family, politics, combat, or external-DLL detection patches are included.
```

Expected files:

```txt
AfterProhibitionEconomy/
  AfterProhibitionEconomy.csproj
  AfterProhibitionEconomyPlugin.cs
```

Expected logs:

```txt
[After Prohibition Economy] economy baseline scheduled ownsRoutes=False ownsUi=False ownsFamily=False ownsPolitics=False
[After Prohibition Economy] After Prohibition Economy loaded phase=scaffold
```

## Phase 2 - Economy Migration Inventory

Status: completed 2026-05-11.

Implementation notes:

- Added `docs\ai\after-prohibition-economy-migration-inventory.md`.
- Checked live `Player.log` and `Player-prev.log`; `AfterProhibitionEconomy` was not present in the live run yet, while GameplayTweaks still logged purchase stock refresh, empty business module repair, and Dirty Cash runtime sweeps.
- Inventoried shop buy/sell access, business purchase stock refreshes, empty business module repair, bank/civic nonpurchase behavior, Dirty Cash routing, front/business setup, owned business install/upgrade fixes, and staged route shop-order hooks.
- No behavior was moved in this phase.

Prompt:

```txt
Inventory current economy code in GameplayTweaks. Create a migration table listing the current source file, method or patch, current log tag, behavior risk, and whether the item should move now, later, or stay.

Validation:
- No code migration yet.
- Include shop buy/sell access, business purchase stock refreshes, empty business module repair, bank/warehouse purchase access, dirty cash routing, front/business resource setup, owned business install/upgrade fixes, and staged route shop-order hooks.
- Mark detection-only, audit-only, repair, and behavior-changing items separately.
```

Suggested doc:

```txt
docs/ai/after-prohibition-economy-migration-inventory.md
```

## Phase 3 - Read-Only Economy Audit Matrix

Status: completed 2026-05-11.

Implementation notes:

- Added `AfterProhibitionEconomy\EconomyStartupAudit.cs`.
- Added `Features.EnableStartupEconomyAudit` config, default `true`.
- The audit reads cached business buildings, modules, recipes, inventory buy/sell offers, and template names only.
- It logs summary counts plus up to eight templates with missing buy stock or sell-only access.
- It does not install, remove, repair, refresh, or execute modules, stock, money, buy/sell, route, UI, family, politics, combat, or external-DLL detection behavior.

Prompt:

```txt
Move read-only economy diagnostics into AfterProhibitionEconomy. Add a startup audit that counts shops, banks, warehouses, legal fronts, businesses with empty modules, businesses with buy stock, businesses with sell stock, and businesses with missing purchase access. Do not mutate stock or modules in this phase.

Validation:
- No shop stock is changed.
- No module is installed, removed, or repaired.
- Logs clearly separate shop, bank, warehouse, front, dirty-cash, and stock audit counts.
```

Expected logs:

```txt
[After Prohibition Economy] economy-audit source=start-1s shops=... banks=... warehouses=... fronts=... emptyModules=... buyStockMissing=... sellStockOnly=...
```

## Phase 4 - Shop Buy/Sell Access Bridge

Status: completed 2026-05-11.

Implementation notes:

- Added `AfterProhibitionEconomy\ShopAccessClassifier.cs`.
- Updated `AfterProhibitionEconomy` to version `0.3.0`.
- Added public bridge methods:
  - `ShouldExposeShopBuyAccess(EntityID buildingId)`
  - `ShouldExposeShopSellAccess(EntityID buildingId)`
  - `GetShopAccessSummary(EntityID buildingId)`
- Added `Features.EnableShopAccessSampleLog` and `Features.ShopAccessSampleLimit`.
- Startup logs now include a read-only `shop-access-summary` and limited `shop-access` sample lines for sell-only, missing-purchase-stock, bank, and warehouse candidates.
- Updated to `0.3.1` after live logs showed the `0.2.0` audit ran before the game entity manager existed. Startup economy diagnostics now retry until the entity manager is ready or the retry budget expires.
- GameplayTweaks is not delegating to this bridge yet; existing shop behavior remains unchanged until the delegation phase.

Prompt:

```txt
Move shop buy/sell access classification into AfterProhibitionEconomy. Keep existing GameplayTweaks behavior as fallback. The economy plugin should answer whether a business should expose buy, sell, both, or neither based on current modules, stock, tags, and business type.

Validation:
- Shops that should sell to the player still sell.
- Shops that should buy from the player still buy.
- Shops that should offer purchases no longer show sell-only because of missing stock refresh.
- Logs include a concise reason when a shop is buy-disabled or sell-only.
```

Expected bridge examples:

```txt
ShouldExposeShopBuyAccess(...)
ShouldExposeShopSellAccess(...)
GetShopAccessSummary(...)
```

Expected logs:

```txt
[After Prohibition Economy] shop-access biz=... mode=buy-sell reason=stock-and-module-valid
[After Prohibition Economy] shop-access biz=... mode=sell-only reason=no-purchase-stock
```

## Phase 5 - Banks And Wholesale Warehouses Purchase Access

Status: completed 2026-05-11.

Implementation notes:

- Added `AfterProhibitionEconomy\CivicPurchaseAccessClassifier.cs`.
- Updated `AfterProhibitionEconomy` to version `0.4.0`.
- Added public bridge methods:
  - `ShouldExposeBankPurchaseAccess(EntityID buildingId)`
  - `ShouldExposeWarehousePurchaseAccess(EntityID buildingId)`
  - `GetCivicPurchaseAccessSummary(EntityID buildingId)`
- Added `Features.EnableCivicPurchaseAccessSampleLog` and `Features.CivicPurchaseAccessSampleLimit`.
- Startup diagnostics now include `civic-purchase-access` sample lines and a `civic-purchase-summary`.
- Bank access is classified separately from generic shop stock so bank/legal purchase interactions can be audited without requiring normal shop buy stock.
- Warehouse access remains stock-sensitive and logs explicit missing-stock or missing-module reasons.
- Updated to `0.4.1` after live logs showed the short `0.3.1` retry window could still expire before a game session entity manager existed. Startup diagnostics now retry longer with less log noise.
- GameplayTweaks does not delegate to this bridge yet; this phase does not change bank, warehouse, shop, route, UI, family, politics, combat, or external-DLL behavior.

Prompt:

```txt
Move bank and wholesale warehouse purchase-access classification into AfterProhibitionEconomy. Keep this focused on availability and stock access, not UI rendering. GameplayTweaks may keep fallback behavior until live logs prove the bridge works.

Validation:
- Banks that should provide financial/legal purchase interactions are not hidden by generic shop rules.
- Wholesale warehouses expose purchase options when their modules and stock are valid.
- Missing bank/warehouse resources log a specific reason instead of silently falling back to sell-only.
```

Expected logs:

```txt
[After Prohibition Economy] civic-purchase-access type=bank biz=... access=True reason=bank-module-valid
[After Prohibition Economy] civic-purchase-access type=warehouse biz=... access=True reason=warehouse-stock-valid
```

## Phase 6 - Business Purchase Stock Refresh Migration

Status: completed 2026-05-11.

Implementation notes:

- Added `AfterProhibitionEconomy\PurchaseStockRefreshPatch.cs`.
- Updated `AfterProhibitionEconomy` to version `0.5.0`.
- Added `Features.EnablePurchaseStockRefresh`, default `true`.
- Added `Features.EnablePurchaseStockRefreshSummaryWhenNoCandidates`, default `true`.
- The patch runs after `BusinessUpdate.UpdateBusinessModules` and scans NPC business buildings only.
- It skips safehouses, outposts, human-controlled buildings, buildings without inventory, buildings without purchase-producing modules, and buildings that already have positive player-buy offers.
- It invokes `ModulesComponent.DoUpdate(now, true)` only for candidate businesses, then verifies that positive player-buy offers exist.
- GameplayTweaks keeps its existing purchase-stock refresh fallback for now; delegation/removal waits for live validation.
- No route travel, UI rendering, family, politics, combat, bank takeover, or external-DLL detection behavior was moved in this phase.

Prompt:

```txt
Move business purchase-stock refresh logic into AfterProhibitionEconomy. This should cover the existing GameplayTweaks "Business purchase stock refreshed" behavior but keep a fallback in GameplayTweaks until the economy plugin has been live-tested.

Validation:
- Existing shops that were sell-only because of missing purchase stock recover purchase access.
- Logs summarize candidates, refreshed, failed, and skipped counts once per relevant startup/update pass.
- No route travel, UI, or family behavior is touched.
```

Expected logs:

```txt
[After Prohibition Economy] purchase-stock-refresh source=business-update-initial candidates=... refreshed=... failed=... skipped=...
```

## Phase 7 - Empty Business Module Repair Migration

Status: completed 2026-05-11.

Implementation notes:

- Added `AfterProhibitionEconomy\EmptyBusinessModuleRepairPatch.cs`.
- Updated `AfterProhibitionEconomy` to version `0.6.0`.
- Added `Features.EnableEmptyBusinessModuleRepair`, default `true`.
- Added `Features.EnableEmptyBusinessModuleRepairSummaryWhenNoCandidates`, default `true`.
- The patch runs after `BusinessUpdate.UpdateBusinessModules` and on human turn start.
- It skips human-controlled businesses, safehouses, outposts, invalid buildings, and buildings without module slots.
- It repairs only NPC business module lists derived from `BizConfig.PickModulesForBuilding` or existing business data.
- It can remove unexpected installed modules and install missing expected modules when valid slots exist, matching the current GameplayTweaks fallback behavior.
- GameplayTweaks keeps its existing empty-module repair fallback until live validation proves the economy plugin handles old saves and new games.
- No route travel, UI rendering, family, politics, combat, bank takeover, purchase execution, or external-DLL detection behavior was moved in this phase.

Prompt:

```txt
Move empty business module repair logic into AfterProhibitionEconomy. This should cover businesses that spawn with missing or empty module lists and currently require GameplayTweaks repair. Keep repair scoped to valid known business/module mappings.

Validation:
- Logs report repaired, failed, and skipped counts.
- Repairs do not install invalid modules on restricted, civic-only, family-only, or unrelated businesses.
- GameplayTweaks fallback remains until live validation proves the economy plugin handles old saves and new games.
```

Expected logs:

```txt
[After Prohibition Economy] empty-business-module-repair source=business-update-initial candidates=... repaired=... failed=... skipped=...
```

## Phase 8 - Dirty Cash Economy Routing Bridge

Status: completed 2026-05-11.

Implementation notes:

- Added `AfterProhibitionEconomy\DirtyCashRoutingClassifier.cs`.
- Updated `AfterProhibitionEconomy` to version `0.7.0`.
- Added `Features.EnableDirtyCashRoutingSampleLog`, default `true`.
- Added `Features.DirtyCashRoutingSampleLimit`, default `12`.
- Added public bridge methods:
  - `ShouldUseExternalDirtyCashRouting(EntityID buildingId)`
  - `ShouldTreatAsDirtyCashRoutingCandidate(EntityID buildingId)`
  - `GetDirtyCashRoutingSummary(EntityID buildingId)`
- The classifier queries `AfterProhibitionCompatibility` by reflection for Dirty Cash Economy and Dirty Cash Volume Fix ownership.
- It classifies safehouses, dirty-cash backrooms, player legal fronts/businesses, banks, and warehouses as local or external routing candidates.
- External DLL detection remains owned by `AfterProhibitionCompatibility`; this phase does not scan plugin DLLs directly.
- GameplayTweaks money movement, dirty cash storage, route input, UI, bank purchases, shop purchases, and front resource behavior are not changed in this phase.

Prompt:

```txt
Move dirty-cash routing classification into AfterProhibitionEconomy while keeping external DLL detection in AfterProhibitionCompatibility. AfterProhibitionEconomy should query AfterProhibitionCompatibility for Dirty Cash state, then answer how clean/dirty cash should route through legal fronts, banks, warehouses, backrooms, and player-owned businesses.

Validation:
- Dirty Cash Economy logs still appear from the external DLL.
- AfterProhibitionCompatibility remains the owner of external DLL detection/classification.
- AfterProhibitionEconomy owns only economy-routing decisions.
- GameplayTweaks money, shop, and route behavior is not changed until bridge delegation is added.
```

Expected logs:

```txt
[After Prohibition Economy] dirty-cash-routing source=start-1s externalDirtyCash=True volumeFix=True banks=... fronts=... backrooms=... routeMode=classified-only
```

## Phase 9 - Front And Business Resource Logic

Status: started 2026-05-12.

Implementation notes:

- Added `AfterProhibitionEconomy\FrontResourceAudit.cs`.
- Updated `AfterProhibitionEconomy` to version `0.8.0`.
- Added `Features.EnableFrontResourceAudit`, default `true`.
- Added `Features.FrontResourceAuditSampleLimit`, default `12`.
- Added public bridge method:
  - `GetFrontResourceSummary(EntityID buildingId)`
- Hardened startup economy diagnostics so they wait for the cached building list to contain at least one building before logging audits. This fixes the earlier `dirty-cash-routing-summary scanned=0` startup race.
- This phase is read-only first. It classifies safehouses, legal fronts/player-legal modules, banks, warehouses, dirty-cash backrooms, purchase-producing modules, buy stock, sell stock, and missing inventory.
- It does not repair inventory, resources, stock, money, route orders, UI, family, politics, combat, or external-DLL detection behavior yet.
- Updated to `0.8.1` after live logs showed startup diagnostics could run with a partial building cache (`scanned=346`) before business update saw the full business set. Added `PostBusinessUpdateDiagnosticsPatch` so Dirty Cash routing and front/resource audits also run once after `BusinessUpdate.UpdateBusinessModules`, using the full building cache.
- Updated to `0.8.2` after a live new-game startup showed no hard map-generation exception but did show thousands of individual `empty-business-module-repaired` lines during the initial business update. Individual repair details are now disabled by default and capped when enabled, while the grouped repair summary remains active. The post-business diagnostics patch now runs at low priority and logs one compact `post-business-update-diagnostics` trace so the full-cache Dirty Cash/front-resource audit pass can be confirmed without flooding startup.
- Updated to `0.8.3` after the next live map-startup log stopped immediately after `post-business-update-diagnostics source=business-update-initial`. The full-cache Dirty Cash/front-resource post-business audit is now behind `Features.EnablePostBusinessUpdateDiagnostics`, default `false`, so map startup is not blocked by inline diagnostic scans. Startup and bridge classification remain available, and the full post-business audit can be re-enabled only when specifically debugging full-cache economy state.

Prompt:

```txt
Move front/business resource setup checks into AfterProhibitionEconomy. This phase should classify and repair resource logic needed by legal fronts, backrooms, warehouses, banks, and player-owned business modules.

Validation:
- Legal front modules have the resources they need for buy/sell and upgrade flows.
- Player-owned fronts do not lose stock or resource state on turn start.
- Business resource repair logs are concise and grouped by business/module family.
```

Expected logs:

```txt
[After Prohibition Economy] front-resource-audit source=turn-start fronts=... repaired=... skipped=... failed=...
```

## Phase 10 - GameplayTweaks Economy Delegation

Status: started 2026-05-11.

Implementation notes:

- Added public Economy ownership bridge methods:
  - `OwnsPurchaseStockRefresh()`
  - `OwnsEmptyBusinessModuleRepair()`
  - `GetEconomyOwnershipSummary()`
- Added a reflection-only GameplayTweaks delegation gate for the combined empty business module repair and purchase-stock refresh fallback.
- When AfterProhibitionEconomy is loaded and owns both features, GameplayTweaks skips `TryRepairEmptyBusinessModulesFromConfig` and logs a one-time `source=afterprohibition-economy` delegation line.
- When AfterProhibitionEconomy is missing, disabled, or does not expose both ownership methods, GameplayTweaks fallback remains unchanged.
- Updated AfterProhibitionEconomy to `0.8.4` with ownership bridge methods for read-only shop access and bank/warehouse purchase-access classification:
  - `OwnsShopAccessClassification()`
  - `OwnsCivicPurchaseAccessClassification()`
- Updated GameplayTweaks economy delegation logging so its one-time `source=afterprohibition-economy` line reports whether shop and bank/warehouse classifier bridges are available. This is validation-only; GameplayTweaks still keeps behavior fallback for shop/civic UI and purchase execution.
- Live logs confirmed `AfterProhibitionEconomy 0.8.4` loaded and the economy bridge summary reported `shopAccess=True` and `civicPurchaseAccess=True` while post-business diagnostics stayed disabled for startup safety.
- Updated AfterProhibitionEconomy to `0.8.5` with ownership bridge methods for read-only Dirty Cash routing and front/business resource classification:
  - `OwnsDirtyCashRoutingClassification()`
  - `OwnsFrontResourceClassification()`
- Updated GameplayTweaks economy delegation logging so the one-time `source=afterprohibition-economy` line also reports Dirty Cash routing and front-resource classifier bridge availability. This remains validation-only; money routing, front resource mutation, shop/civic UI, and route behavior still use existing fallback behavior.
- Route shop orders are not delegated yet.

Prompt:

```txt
Update GameplayTweaks economy checks to ask AfterProhibitionEconomy first. Keep local GameplayTweaks economy repair and shop access fallback until the bridge is live-tested across missing, present, old-save, and malformed states.

Validation:
- GameplayTweaks builds.
- AfterProhibitionEconomy builds.
- Logs show delegated economy source when the bridge is available.
- Logs show local fallback when the bridge is unavailable.
```

Suggested migration order:

1. read-only shop access summaries;
2. bank/warehouse purchase access;
3. purchase stock refresh;
4. empty module repair;
5. front resource logic;
6. dirty-cash routing classification.

Expected logs:

```txt
[VERIFY-HOTFIX] [Economy] source=afterprohibition-economy shopAccess=delegated bankAccess=delegated warehouseAccess=delegated fallback=True
```

## Phase 11 - Staged Route Shop Orders

Status: started 2026-05-12.

Implementation notes:

- Live logs confirmed `AfterProhibitionEconomy 0.8.5` loaded and the economy bridge summary reported `dirtyCashRouting=bridge-available` and `frontResource=bridge-available` while keeping `behaviorFallback=True`.
- Added `AfterProhibitionEconomy\RouteShopOrderClassifier.cs`.
- Updated AfterProhibitionEconomy to `0.9.0`.
- Added `Features.EnableRouteShopOrderBridge`, default `true`.
- Added public bridge methods:
  - `OwnsRouteShopOrderClassification()`
  - `IsRouteShopOrderValid(EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)`
  - `GetRouteShopOrderSummary(EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)`
- The classifier is read-only. It checks building/business/module/inventory presence, resource validity, requested quantity, current buy/sell offer availability, negotiated unit price, estimated money delta, and optional available-cash constraints.
- Updated GameplayTweaks economy bridge logging so its one-time `source=afterprohibition-economy` line reports `routeShopOrders=bridge-available` when the classifier bridge exists.
- This phase does not move route travel continuation, route UI rendering, staged order storage, arrival commit, buy/sell execution, inventory mutation, cash mutation, or vehicle authority out of GameplayTweaks.
- Follow-up `0.9.1` hardens startup economy diagnostic readiness after live map-start logs showed full-map classifiers running against a half-built entity cache (`businessBuildings=0`, `shops=0`, `neither=8761`) before procgen completed. Startup economy diagnostics now wait for procgen readiness and at least one real business building before running the audit, shop access, civic access, Dirty Cash routing, and front-resource classifier passes.

Prompt:

```txt
Add staged route shop-order classification to AfterProhibitionEconomy after base shop access is stable. Keep actual delivery-route travel and route continuation outside this plugin. The economy plugin should only answer whether a staged buy/sell order is valid and what stock/cash/resource constraints apply.

Validation:
- Route travel continuation remains owned by the route system.
- Route UI rendering remains owned by GameplayTweaks or AfterProhibitionUI.
- Economy plugin only classifies buy/sell order validity, stock, price, and cash/resource constraints.
```

Expected logs:

```txt
[After Prohibition Economy] route-shop-order source=classification routeStep=... mode=buy valid=True reason=stock-and-cash-valid
```

## Phase 12 - Packaging And Guide

Status: completed 2026-05-12.

Implementation notes:

- Added `Things To Have\Current After Prohibition Mod\Plain Text Guides\AFTER_PROHIBITION_ECONOMY_GUIDE.txt`.
- Staged `AfterProhibitionEconomy.dll` into the Personal and Public release `BepInEx\plugins` folders.
- Copied the economy guide into the Personal and Public release roots.
- Validated `AfterProhibitionEconomy` with a targeted Release build.
- Live logs before packaging confirmed `AfterProhibitionEconomy 0.9.0` loaded, purchase stock and empty business module repair were delegated, and route shop-order classification bridge availability was reported while post-business diagnostics stayed disabled for startup safety.

Prompt:

```txt
Add release packaging for AfterProhibitionEconomy. Stage the DLL into Personal/Public release plugin folders and add a plain-text guide describing what the plugin owns, what it explicitly does not own, config switches, expected logs, and how to verify shop/bank/warehouse access.

Validation:
- Build and stage DLL.
- Confirm guide is plain text.
- Confirm live logs clearly distinguish AfterProhibitionEconomy from GameplayTweaks, AfterProhibitionCompatibility, and AfterProhibitionUI.
```

Expected release files:

```txt
BepInEx/plugins/AfterProhibitionEconomy.dll
AFTER_PROHIBITION_ECONOMY_GUIDE.txt
```

## Migration Rules

- Move read-only audit before behavior.
- Move classification before repair.
- Keep GameplayTweaks fallback until live validation proves the economy bridge works.
- Do not move external DLL detection into this plugin; query AfterProhibitionCompatibility instead.
- Do not move route travel continuation into this plugin.
- Do not move UI rendering into this plugin.
- Do not move family or politics behavior into this plugin.
- Log once per session or once per business-update phase, not every frame.
- Group repair logs by counts and reason summaries unless debugging a specific business.

# AfterProhibitionEconomy

Standalone After Prohibition economy plugin.

The plugin owns economy-only diagnostics and staged shop/business repair behavior. It owns no route, UI, family, politics, combat, or compatibility behavior.

Initial purpose:

- prove the plugin loads independently from `GameplayTweaks`
- provide a clean economy log prefix for later migration work
- keep future shop, bank, warehouse, dirty-cash, front, and business resource fixes out of unrelated systems

Phase 3 adds a read-only startup audit. It counts business buildings, shops, banks, warehouses, legal fronts, dirty-cash backrooms, empty/missing modules, buy stock, sell stock, sell-only businesses, and purchase-producing businesses with missing buy stock. It does not repair or mutate stock/modules.

Phase 4 adds a read-only shop access classifier bridge:

- `ShouldExposeShopBuyAccess(EntityID buildingId)`
- `ShouldExposeShopSellAccess(EntityID buildingId)`
- `GetShopAccessSummary(EntityID buildingId)`

These methods classify current stock/module state only. They do not change shop UI, stock, money, routes, modules, or GameplayTweaks fallback behavior.

Version `0.3.1` defers startup economy diagnostics until the game entity manager exists, so early main-menu/plugin-load timing does not produce a permanent missing-entity-manager audit.

Phase 5 adds a read-only civic purchase-access classifier bridge:

- `ShouldExposeBankPurchaseAccess(EntityID buildingId)`
- `ShouldExposeWarehousePurchaseAccess(EntityID buildingId)`
- `GetCivicPurchaseAccessSummary(EntityID buildingId)`

Banks are classified separately from generic shop stock so financial/legal purchase interactions can be diagnosed without requiring ordinary shop buy stock. Warehouses remain stock-sensitive and report missing purchase stock explicitly.

Version `0.4.1` extends the delayed startup diagnostic retry window so the audit can wait through main-menu and new-game loading before the session entity manager exists.

Phase 6 moves NPC business purchase-stock refresh into this plugin. It patches `BusinessUpdate.UpdateBusinessModules` and refreshes NPC businesses that have installed purchase-producing modules but no positive player-buy offers. GameplayTweaks keeps its existing fallback path until the economy plugin has been live-tested.

Phase 7 moves NPC empty business module repair into this plugin. It patches `BusinessUpdate.UpdateBusinessModules` and human turn start, skips human-controlled buildings, safehouses, and outposts, and repairs NPC business module lists from their business configuration. GameplayTweaks keeps its existing fallback path until the economy plugin has been live-tested.

Phase 8 adds read-only Dirty Cash routing classification. It queries `AfterProhibitionCompatibility` by reflection for external Dirty Cash Economy and Dirty Cash Volume Fix ownership, then classifies safehouses, dirty-cash backrooms, legal fronts, banks, and warehouses as local or external routing candidates. It does not move money, change inventory, patch routes, or detect external DLLs itself.

Phase 9 starts front/business resource work with a read-only audit. It waits for the cached building list to be populated, then classifies human-controlled safehouses, legal fronts, banks, warehouses, dirty-cash backrooms, and resource-relevant modules. It does not repair inventory, resources, stock, money, or route orders yet.

Version `0.8.1` also logs Dirty Cash routing and front/resource audits after the first business update, because the startup building cache can be partially populated before the full business set exists.

Version `0.8.3` disables post-business-update Dirty Cash/front-resource diagnostics by default after live startup logs showed full-cache diagnostic scans could block new-game map startup. Startup diagnostics and bridge classification remain available.

Version `0.9.0` adds read-only staged route shop-order classification:

- `OwnsRouteShopOrderClassification()`
- `IsRouteShopOrderValid(EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)`
- `GetRouteShopOrderSummary(EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)`

The route-shop-order bridge only classifies resource, stock, cash, price, and buy/sell direction validity. It does not own delivery-route travel continuation, route UI, staged-order storage, arrival commits, inventory mutation, or cash mutation.

Version `0.9.1` hardens startup economy diagnostic readiness. The startup audit now waits for procgen readiness and at least one real business building before running full-map economy classifiers, avoiding scans against half-built map/entity caches during new-game loading.

Version `0.9.2` tightens map-load safety again. Startup economy audits and full-map classifiers now wait until `Game.ctx.IsInteractive` is true, so they cannot run in the middle of `OnPreInteractiveAIGen` after the turn/date has advanced but before new-game player setup finishes.

Version `0.9.3` makes full startup economy diagnostics opt-in behind `Features.EnableStartupEconomyDiagnostics=false` by default. Existing bridge methods, purchase-stock refresh, and empty-business-module repair remain active, but old per-audit config values can no longer force a full-map startup audit during map load unless the new top-level gate is enabled.

Version `0.9.4` adds a read-only Dirty Cash runtime sweep source bridge:

- `OwnsDirtyCashRuntimeSweepClassification()`
- `GetDirtyCashRuntimeSweepSummary(string source)`

The bridge classifies the remaining GameplayTweaks runtime sweep sources and records the ownership boundary. It does not move runtime sweep mutation out of GameplayTweaks.

Version `0.9.5` adds a read-only player legal business consumer bridge:

- `OwnsPlayerLegalBusinessConsumerClassification()`
- `GetPlayerLegalBusinessConsumerSummary(EntityID buildingId, string moduleId)`

The bridge classifies human-owned `player-legal-*` consumer modules and idle/not-due consumer state. It does not move consumer update mutation out of GameplayTweaks.

Version `0.9.6` adds a runtime-detail bridge summary used by GameplayTweaks log validation:

- `GetPlayerLegalBusinessConsumerRuntimeSummary(EntityID buildingId, string moduleId, bool initial, bool enabled, string result, int consumeDays, int currentDay, int lastUpdateDay)`

The richer summary lets live logs confirm Economy's player-legal consumer classification against the exact observed update and idle/not-due timing path before any mutation moves out of GameplayTweaks.

Version `0.9.7` experimentally moved the player-legal consumer candidate decision behind the Economy bridge while keeping consumer execution and mutation in GameplayTweaks:

- `ShouldObservePlayerLegalBusinessConsumer(EntityID buildingId, string moduleId)`

Version `0.9.8` rolls that live decision handoff back out of GameplayTweaks after a map-load regression was observed in a new-game run (`BoardLoad` rose from about 10.7s on `0.9.6` to about 230.7s on `0.9.7`). The richer runtime-detail bridge summaries remain available for validation, but GameplayTweaks keeps the local hot-path candidate check until a lower-cost delegation design exists.

Version `0.9.9` adds live validation logs for the existing Dirty Cash runtime sweep source bridge. GameplayTweaks still executes the sweep, but each existing sweep summary can now be paired with:

- `[VERIFY-HOTFIX] [EconomySweep] dirty-cash-runtime-sweep ...`

This keeps Phase 1/2 evidence moving without moving more hot-path ownership.

Release packaging is documented in:

`Things To Have\Current After Prohibition Mod\Plain Text Guides\AFTER_PROHIBITION_ECONOMY_GUIDE.txt`

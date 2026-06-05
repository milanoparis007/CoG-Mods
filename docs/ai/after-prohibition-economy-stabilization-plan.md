# AfterProhibitionEconomy Stabilization Plan

Created: 2026-05-13

Goal: finish the remaining economy ownership split without reintroducing map-load stalls, shop regressions, or external Dirty Cash conflicts. Past Build is intentionally out of scope and must remain unchanged.

## Phase 1 - Remaining Dirty Cash Runtime Module Sweeps

Status: started 2026-05-13.

Current live logs show `AfterProhibitionEconomy 0.9.3` owns purchase-stock refresh, empty business module repair, shop access classification, bank/warehouse classification, Dirty Cash routing classification, front/resource classification, and route shop-order classification. The remaining Dirty Cash runtime module safety sweeps still execute in `GameplayTweaks`, usually with `candidates=0`.

Plan:

- Add an Economy bridge that classifies runtime sweep sources and states the execution boundary.
- Keep actual runtime sweep mutation in `GameplayTweaks` until live validation proves a move is safe.
- Update the GameplayTweaks economy delegation log so it reports whether the Economy runtime sweep bridge is available.
- Avoid full-map startup scans; source classification must be string/read-only only.

Validation:

- `AfterProhibitionEconomy` builds.
- `GameplayTweaks` builds if bridge logging changes.
- Live log should show Economy ownership summary with `dirtyCashRuntimeSweeps=True` and GameplayTweaks verification with `dirtyCashRuntimeSweeps=bridge-available`.

## Phase 2 - Player Legal Business Consumer Logic

Status: started 2026-05-13.

Move player-legal business consumer decisions only after Phase 1 proves the runtime sweep boundary. Keep external Dirty Cash DLL detection in `AfterProhibitionCompatibility`; Economy should only classify legal business modules, consumer/manufacture type, and route/routing mode. Actual consumer update mutation can remain in GameplayTweaks until malformed-state logs are clean.

Phase 2 first step adds `AfterProhibitionEconomy` read-only player legal business consumer classification and updates the GameplayTweaks delegation line to report `playerLegalBusinessConsumer=bridge-available`. This does not change consumer timing, resource mutation, money mutation, external Dirty Cash hooks, or upgrade-list filtering.

Phase 2 second step adds runtime-detail bridge summaries and GameplayTweaks verification logs for the exact player-legal consumer update and idle/not-due paths already observed in live logs. Mutation still remains in GameplayTweaks; this is classification validation only.

Phase 2 third step briefly moved the player-legal consumer candidate decision behind `AfterProhibitionEconomy`, but the next live new-game run showed a severe startup regression: `BoardLoad` increased from about `10.7s` on `0.9.6` to about `230.7s` on `0.9.7`. The handoff was rolled back in `0.9.8`; GameplayTweaks keeps the local hot-path candidate check while the runtime-detail bridge summaries stay in place for validation.

After the `0.9.8` rollback restored new-game load time to about `9.1s`, `0.9.9` continues safely by adding `EconomySweep` verification lines beside the existing Dirty Cash runtime sweep logs. This is still classification-only validation; execution remains in GameplayTweaks.

## Phase 3 - Bank/Warehouse Purchase Access Final Ownership

Economy already owns read-only civic purchase classification. Final ownership should verify whether any GameplayTweaks UI or purchase execution gates still override bank/warehouse access, then move only economy-specific checks. Building takeover restrictions and civic nonpurchase protection stay separate from resource purchase access.

Phase 3 first step adds `EconomyCivic` boundary diagnostics in the existing GameplayTweaks civic nonpurchase takeover guards. These logs only fire when GameplayTweaks suppresses takeover-style civic purchase UI/runtime actions, and they pair that guard with `AfterProhibitionEconomy.GetCivicPurchaseAccessSummary(...)` so the next live pass can confirm takeover blocking remains separate from bank/warehouse resource purchase access classification.

Phase 3 second step adds a read-only `EconomyCivic` observation on `CheckCanBuySell.CheckAvailability(...)`, which is the actual buy/sell requirement gate used by shop conversations. GameplayTweaks now emits deduped `source=buy-sell-availability` bridge lines only for Economy-classified banks or warehouses, reporting the vanilla requirement result beside the Economy civic purchase-access summary without changing access behavior.

Phase 3 third step adds an Economy-side `civic-purchase-runtime-summary` during the existing `business-update-initial` purchase-stock refresh sweep. It reuses that already-running building pass, prefilters likely bank/warehouse templates, and reports aggregate bank/warehouse access counts without adding a second startup scan or changing purchase behavior.

Phase 3 fourth step adds one-shot `civic-purchase-runtime-blocked` detail lines for any bank or warehouse counted as blocked by that same startup summary. This keeps the pass aggregate-first while exposing the exact blocked candidate that must be understood before moving any bank/warehouse access behavior.

## Phase 4 - Shop Route Order Staging Decision

Economy currently classifies route shop orders, but GameplayTweaks owns staged order storage, route continuation, UI, arrival commits, inventory mutation, and cash mutation. Keep that split unless the route system becomes stable enough to hand Economy a narrow commit bridge. If the staging system is not kept, remove only the route-staging hooks and leave ordinary shop access classification intact.

## Phase 5 - Packaging And Runtime Verification

Stage only the current release roots after builds pass. Do not update `Things To Have\Past Build`. Verify live logs after the next game restart before moving behavior out of GameplayTweaks fallbacks.

Runtime side-pass on 2026-05-13: repeated `V2 save failed; using legacy JSON fallback...` lines were traced to turn-start persistence running before a fresh session had any resolved save slot/path. GameplayTweaks now skips that pathless save attempt quietly, preserving normal V2 behavior once a real save path exists.

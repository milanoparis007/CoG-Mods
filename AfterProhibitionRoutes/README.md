# AfterProhibitionRoutes

Standalone After Prohibition routes plugin.

The plugin will own routes-only diagnostics and staged migration work. It owns no economy, UI, family, politics, compatibility, combat, robbery, territory, or safehouse relocation behavior.

Current purpose:

- prove the plugin loads independently from `GameplayTweaks`
- provide a clean routes log prefix for later migration work
- report read-only `GameplayTweaks` route state counts at startup and human turn start
- expose read-only route authority classifier bridge methods for diagnostics
- expose read-only travel-continuation decision bridge methods for GameplayTweaks fallback diagnostics
- expose read-only vehicle-node authority decision bridge methods for GameplayTweaks fallback diagnostics
- expose read-only delivery-route pump decision bridge methods for GameplayTweaks fallback diagnostics
- expose read-only route-simulated destination access decision bridge methods for GameplayTweaks fallback diagnostics
- keep future travel continuation, vehicle node authority, route-simulated destination access, and delivery-route pump fixes out of unrelated systems

Phase 8 is a read-only route-simulated destination access decision bridge. It logs a delayed read-only baseline, exposes bridge-version and route-state summary checks, reflects `GameplayTweaks` route, delivery-pump, and route-shop staging dictionaries for counts and per-vehicle decisions, classifies route continuation, vehicle node authority, delivery pump safety, and route-sim access safety. It reports whether route-sim access should allow, block, or fall back. It does not open conversations, stage shop orders, commit buy/sell orders, process command queues, run automation steps, record arrivals, start, stop, resume, clear, finalize, sync occupants, move vehicles, or mutate route, travel, delivery, shop, storage, UI, or save behavior.

Phase 9 adds safe `GameplayTweaks` delegation gates. `AfterProhibitionRoutes` exposes full-behavior ownership methods, but they return `false` until a later phase actually moves mutating behavior into this plugin. `GameplayTweaks` keeps fallback behavior active unless `AfterProhibitionRoutes` explicitly owns a full route behavior slice.

Phase 10 packages the plugin in the Personal and Public release plugin folders and documents the install boundary. `GameplayTweaks` remains required. Current route behavior is split as follows:

- fully owned by `AfterProhibitionRoutes`: read-only audits and decision/classification bridges
- delegated to `AfterProhibitionRoutes`: decision logging/classification consumed by `GameplayTweaks`
- still fully owned by `GameplayTweaks`: mutating travel, route continuation, vehicle node authority, delivery pump, route-simulated access, route shop staging, goods/cash movement, UI mutation, and save behavior

Planned migration areas are documented in:

`docs\ai\after-prohibition-routes-phase-prompts.md`

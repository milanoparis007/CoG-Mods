# Dev Build Status

## 2026-06-05 Robbery Project Dev Snapshot

Branch target: `dev-robbery-log-cleanup`

This snapshot is meant for GitHub tracking of the cleanup branch. The June 5 public release wording and package docs have now been refreshed, but the public branch/tag still need to be created separately from this dev branch.

Current staged public build signals:

- phase: `8EZ live validated + public docs refreshed`
- `GameplayTweaks.dll` timestamp: `2026-06-05 11:16:42`
- `GameplayTweaks.dll` size: `2526208`
- `AfterProhibitionEconomy.dll` timestamp: `2026-06-05 12:16:27`
- `AfterProhibitionEconomy.dll` size: `122880`
- validation: `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors
- solution validation: `dotnet build ClassLibrary1.sln -c Release` passed with `0` warnings and `0` errors

Why this remains a dev build:

- robbery prompt duplicate cleanup from Phase 8ET passed live verification, and Phase 8EV compact verification cleanup also passed one live log review;
- Phase 8EW successfully removed cache-hit and low-ms limiter spam in live logs, and Phase 8EX live review confirmed low-ms AI dispatch/command traces, routine heatmap/residence lines, and ordinary system-maintenance totals are quiet by default;
- detailed robbery/front/combat/vehicle/compatibility diagnostics, detailed performance telemetry, and routine economy batch-progress/mutation-detail telemetry are opt-in;
- public package docs now lead with the cleaned public release status instead of the dev snapshot warning.

Public log cleanup policy:

- Keep always-on Unity errors and compact GameplayTweaks verification breadcrumbs for first bug reports.
- Keep compact breadcrumbs focused on player-facing outcomes, queued/resolved robbery responses, front/business closure outcomes, combat commits, and anomalies such as failed, abandoned, timed-out, or lost-node actions.
- Gate noisy proof traces behind `Diagnostics.EnableVerboseVerificationLogs` or the area-specific diagnostics toggles.
- Gate detailed `[PERF]` turn-slicing/cache-hit telemetry behind `Diagnostics.EnablePerformanceDiagnostics`; keep only high wall-clock spikes and totals in normal logs.
- Do not require a player to enable verbose diagnostics before sending the first log; use the compact breadcrumbs first, then ask for an opt-in repro only when the first log does not contain enough evidence.
- Leave dev snapshot tags separate from the cleaned public release tag.

Public cleanup checklist:

- verified `pair-already-queued` kept repeated same-outfit same-turn robbery contacts from stacking in the Phase 8EU live log;
- verified live Phase 8EV reduced verification spam to compact breadcrumbs while preserving robbery/business-closure evidence;
- verified live Phase 8EX removed low-ms command/AI dispatch traces and routine per-turn system totals while keeping real 80ms+ action spikes and 750ms+ system-maintenance stalls;
- added Phase 8EY economy cleanup so healthy chunked stock-refresh/module-repair progress and routine Dirty Cash/legal-consumer runtime summaries are quiet by default while failure progress, warnings, one-time ownership markers, and completion summaries still print;
- added Phase 8EZ economy detail cleanup so individual business stock-refresh lines and repeated Dirty Cash correction details are quiet by default while aggregate summaries and first correction evidence remain available;
- verified live Phase 8EZ economy detail cleanup: default logs showed `0` `purchase-stock-refreshed`, `0` `purchase-stock-refresh-progress`, `0` `empty-business-module-repair-progress`, and `0` `player-legal-business-consumer-runtime` routine lines with no C# runtime exceptions;
- rebuilt and refreshed the final public v1.3.98 BepInEx package DLLs from current Release outputs;
- updated `CHANGELOG.md`, `GUIDE.md`, public package `CHANGELOG.txt`, and public package `GUIDE.txt` with cleaned public-release wording;
- push/tag the cleaned public release separately from this dev snapshot.

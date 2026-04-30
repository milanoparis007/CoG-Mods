# Performance Turn Simulation And Gameplay Smoothness Pass

Use this prompt when big maps have slow next-turn processing, delayed clicks, map movement hitches, business screen lag, crew selection lag, or other gameplay smoothness regressions.

## Reusable Prompt

Do a performance pass in `GameplayTweaks` focused on turn simulation and gameplay smoothness. Check the live SomaSim logs, not repo-root log copies:

- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`

Look for repeated turn-sim work and noisy diagnostics: `[PERF]`, `GangOps`, `deferred-human-territory`, `gang-collapse-territory`, `territory-audit`, `RuntimeChecklist`, exceptions, repeated warnings, and repeated frame/day entries. Compare with decompiled vanilla paths before changing behavior. Optimize by coalescing duplicate work, caching reflection, skipping redundant full-board scans, throttling repeated refreshes/logging, and avoiding UI rebuilds when state did not change. Build `GameplayTweaks` after edits.

## Current Hotspots To Check

- Deferred territory refresh/rebuild:
  - `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs`
  - `RequestDeferredHumanTerritoryRefresh`
  - `RequestDeferredHumanTerritoryFullRebuild`
  - `FlushDeferredHumanTerritoryRefresh`
  - `NotifyGangMemberDeathForTerritoryAudit`
- Gang Ops turn work:
  - `GameplayTweaks/GameplayTweaksPlugin.cs`
  - `RunGangOpsTurn`
  - `RunAutoProtectPass`
  - `TriggerCoordinatedAttacksNow`
  - `TryRunAiOutpostTerritoryExpansionPass`
- UI/input smoothness:
  - selection, crew pick, popup, owned business, scope-out, map overlay refresh patches
  - avoid repeated refreshes in the same frame or for the same target
- Save/load:
  - `GameplayTweaks/GameplayTweaksPlugin.SaveLoad.cs`
  - existing `[PERF][TweaksLoad]` timing logs

## Acceptance Checks

- No repeated `gang-collapse-territory-audit-deferred` spam for dozens of defeated zero-territory gangs in the same frame.
- Full territory rebuild requests coalesce while a rebuild is already pending.
- A full territory rebuild should not be immediately followed by redundant all-player outpost-target respect recompute in the same deferred refresh pass.
- Gang Ops logs should show bounded node counts on big maps; `AutoProtectRange` should stay `1` unless deliberately testing wider sweeps.
- Gameplay logs should remain useful but not emit per-frame or per-gang spam during normal turns.
- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passes.

## Next Tuning Levers

- Add short-lived `[PERF][TurnSim]` timing around `RunGangOpsTurn`, deferred territory refresh, and dirty-cash safety sweeps when symptoms persist.
- Cache hot reflection lookups that still happen during turn or UI refresh paths.
- Convert repeated LINQ in hot turn loops to direct loops when a log or timing signal shows repeated cost.
- Split expensive full-board reconciliation into smaller targeted node sets where ownership/respect changes are known.
- Suppress duplicate UI refreshes by frame and selected entity/corner key.

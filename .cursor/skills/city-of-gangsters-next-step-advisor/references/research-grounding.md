# Research Grounding

Use these rules when turning repo signals into next-step recommendations. They are distilled from the main City of Gangsters mod skill and reinforced by current repo evidence.

## Main-skill grounding

Mirrored playbook material lives alongside this skill:

1. [../../city-of-gangsters-mod-builder/references/patch-reliability.md](../../city-of-gangsters-mod-builder/references/patch-reliability.md)
2. [../../city-of-gangsters-mod-builder/references/performance-playbook.md](../../city-of-gangsters-mod-builder/references/performance-playbook.md)
3. [../../city-of-gangsters-mod-builder/references/project-map.md](../../city-of-gangsters-mod-builder/references/project-map.md)

## Engine and patching facts

1. This repo targets BepInEx 5.4.23.4 and Unity 2020.3.28 with heavy Harmony usage.
2. Prefer `prefix`, `postfix`, or `finalizer` before transpilers when prioritizing future work.
3. Verify exact signatures in `decompiled/` before recommending expansion of a fragile patch.
4. Patch implemented methods, not abstract or base virtual declarations, unless the shared base entry point is intentionally required.
5. Cache reflection results outside hot loops and avoid repeated `AccessTools` trees in frequently-called paths.

## Unity performance facts that matter for prioritization

These come from the main skill's Unity performance grounding and match current repo patterns:

1. Reduce temporary allocations in per-frame, per-turn, and per-iteration code paths.
2. Reuse collections, buffers, and caches when the data lifetime allows it.
3. Debounce repeated rebuilds and refreshes when many triggers collapse into one visible outcome.
4. Prefer batching or coroutine chunking when startup or map-generation work is large.
5. Keep diagnostics narrow and guarded so logging does not become the hot path.

When deciding between a new feature and a hot-path cleanup, prefer the cleanup if the affected code runs every turn, every UI refresh, or every map-generation batch.

## Current repo-specific evidence

Treat these as strong hints, then re-check them from live files:

1. `GameOptimizer/GameOptimizerPlugin.cs` explicitly documents disabled patches because Harmony DMD or type resolution fails for some methods.
2. `GameOptimizer` already uses manual patch registration, reverse patches, and coroutine replacements when Harmony cannot patch cleanly.
3. `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.cs` is now a very large subsystem and already carries queueing, arrival authority, selection, and building interaction complexity.
4. `Player.log` currently shows:
   - missing-type drift around `Game.UI.Session.Picks.MobilePickUtil`
   - repeated `HostileMobileSelectionFixPatch` failures caused by `Microsoft.CSharp` load issues
   - repeated `GangOps.Pact ... skipped=no-eligible-gangs`
   - repeated `BossRoleSetter` null warnings
5. `docs/multi-crew-vehicle-plan.md` provides a concrete follow-up slice, which is stronger evidence than a vague feature idea.

## Recommendation bias rules

1. Recommend fixing a live Harmony, assembly, or type-resolution issue before proposing a fresh patch into the same area.
2. Recommend hardening recent additions before enabling compatibility paths that are still marked fallback-only or default-off.
3. Recommend a contained vertical slice when the game-side method anchors are already known.
4. Recommend decompiled verification first when a planned task depends on uncertain game internals.

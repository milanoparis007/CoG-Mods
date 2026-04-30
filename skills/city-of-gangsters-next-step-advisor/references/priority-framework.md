# Priority Framework

Score each candidate next step across these lenses. Use simple `high`, `medium`, or `low` language unless the user asks for a formal matrix.

## Primary scoring lenses

1. Live pain
   Favor tasks backed by repeated warnings, failed patches, disabled features, or obvious runtime oddities in the AppData `Player.log`.
2. Recency pressure
   Favor hardening work in files or projects touched by the latest commit before adding more feature breadth.
3. Loop leverage
   Favor tasks that improve a whole gameplay loop, not just one isolated button or helper.
4. Anchor quality
   Favor tasks with exact owner files, method names, docs, and decompiled anchors already visible.
5. Blast radius
   Favor the smallest change that meaningfully reduces risk or unlocks the next layer of work.
6. Compatibility fit
   Favor tasks that align with the stable-core defaults instead of requiring several config authority changes at once.

## CoG-specific tie breakers

Use these repo-specific tie breakers when several tasks look similar:

1. Choose assembly-load and missing-type fixes before balance or new content.
2. Choose repeated warning cleanup before broad refactors.
3. Choose hardening in `GameplayTweaks`, `CopKilling`, or `GameOptimizer` before expanding smaller side mods when the latest commit centered on those projects.
4. Choose the next documented vertical slice in `docs/` only if startup and runtime warnings are already acceptable.
5. Choose one owner project's next step over a cross-project expansion when both are viable.

## Current repo signals to weigh heavily

These are not permanent truths. Re-check them from live files before recommending work:

1. The latest repo commit added `CopKilling` and expanded `GameplayTweaks` and `GameOptimizer`.
2. `GameplayTweaks` and `GameplayTweaksPlugin.MultiCrewVehicle.cs` are the largest method surfaces in the repo.
3. `GameOptimizer` contains disabled or manual-patch-only optimizations because Harmony DMD and type resolution can fail in this environment.
4. The AppData `Player.log` currently shows `HostileMobileSelectionFixPatch` failing on `Microsoft.CSharp` load, repeated `GangOps.Pact ... skipped=no-eligible-gangs`, `MobilePickUtil` type drift, and repeated `BossRoleSetter` warnings.
5. `docs/multi-crew-vehicle-plan.md` already documents a concrete follow-up path for that subsystem.

## Default recommendation format

Always give:

1. one best next step
2. one paragraph explaining why it wins now
3. a short ordered follow-up queue
4. a defer list
5. exact files to inspect first

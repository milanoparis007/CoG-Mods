---
name: city-of-gangsters-next-step-advisor
description: Analyze the City of Gangsters mod suite and recommend the highest-value next steps from live repo state, recent additions, method-heavy mod files, AppData Player.log symptoms, compatibility defaults, and decompiled game anchors. Use when Codex needs to answer what the ClassLibrary1 CoG mods should build or fix next, what remains unfinished after recent additions, how to prioritize stabilization versus new features, or which partial systems in GameplayTweaks, GameOptimizer, CopKilling, BossDeath, BossBuildings, OrgChartMod, AutoLevelup, and ModLauncher should be completed first.
---

# City of Gangsters Next Step Advisor

## Overview

Use this skill to infer the best next City of Gangsters mod step from the current repo, not from memory alone. Prefer live evidence from `c:\Users\User\source\repos\ClassLibrary1`, then turn that evidence into one recommended next step, a short ordered queue, and a defer list.

Keep repo routing and research grounding aligned with the main City of Gangsters mod skill. After choosing the next step, hand implementation off to `city-of-gangsters-mod-builder` or continue with normal repo editing workflow.

## Workflow

### 1. Gather live project signals first

Read only the sources needed for the current recommendation:

1. Run `scripts/scan_city_of_gangsters_next_steps.ps1`.
2. Read [references/project-sources.md](references/project-sources.md) to route the question to the owning project and relevant decompiled anchors.
3. Read [references/research-grounding.md](references/research-grounding.md) before recommending work that touches Harmony reliability, Unity performance, reflection-heavy patches, or decompiled game behavior.
4. Read `docs/progress.md`, `session_summary.md`, and `docs/compatibility-matrix.md` to understand recent additions and current stable-core defaults.
5. Inspect the live AppData logs at `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log` or `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log` when the recommendation depends on runtime symptoms, repeated warnings, disabled subsystems, or compatibility drift. Do not rely on repo copies of the logs.
6. Search the repo for narrow subsystem terms such as `Multi-crew vehicle`, `GangOps`, `CopWar`, `territory`, `safebox`, `HostileMobileSelectionFixPatch`, `BossRoleSetter`, `ReversePatch`, `pending`, or exact method/type names when the question is focused.

Do not recommend broad new systems before checking whether recent additions are still unstable, noisy in logs, or blocked by compatibility gates.

### 2. Sort findings into priority lanes

Classify unfinished work into these lanes:

1. Load and compatibility stability
   Examples: missing assemblies, missing types, Harmony target drift, startup warnings, plugin conflicts.
2. Recent-addition hardening
   Examples: CopKilling follow-through, new GameplayTweaks branches, multi-crew vehicle reliability, GameOptimizer manual-patch debt after the latest expansion commit.
3. Core loop correctness
   Examples: gang ops actually doing work, pacts behaving as intended, dirty cash/front tracking, jail/heat, vehicle authority, witness escalation.
4. Exposed-but-shallow feature completion
   Examples: documented follow-up plans, UI exposed without full consequences, config-gated systems that still need one vertical slice.
5. Performance and maintenance debt
   Examples: disabled optimizer patches, oversized monoliths, repeated reflection in heavy paths, debouncing/caching opportunities, noisy verification hooks.
6. Small-mod cleanup
   Examples: BossDeath null spam, BossBuildings regressions, OrgChart or AutoLevelup polish.

If one task spans several lanes, classify it by the riskiest lane.

### 3. Score candidate next steps

Use the scoring rules in [references/priority-framework.md](references/priority-framework.md).

Prefer tasks that:

1. repair a live runtime failure or repeated warning
2. stabilize the newest large subsystem before adding another one
3. close the most obvious gap in a player-visible loop
4. have exact file, method, or decompiled anchors already visible in the repo
5. reduce future regression risk across several mods
6. fit the repo's documented compatibility defaults instead of fighting them

Delay tasks that:

1. depend on unresolved Harmony or assembly-load failures
2. expand content while log spam or disabled patches remain active
3. require a major design decision with no code anchors yet
4. touch multiple owners when a single-owner stabilization pass is available first

When several candidates seem plausible, prefer the one that closes the largest live gap with the fewest new concepts.

### 4. Produce a decision, not just a list

Always end with:

1. `Best next step now`
   One concrete recommendation with why it beats the alternatives.
2. `Why now`
   Explain the dependency, leverage, user impact, and recent-addition context.
3. `Do after that`
   List the next 2-4 follow-up tasks in order.
4. `Defer for now`
   Name the larger ideas that should wait and why.
5. `Files to inspect first`
   Point to exact repo files or decompiled anchors that should drive the next implementation pass.

If the user asks for strategy only, stop there. If the user asks to build the chosen item, switch to implementation work next.

### 5. Keep recommendations honest

Use these default heuristics:

1. Choose stability over breadth when the AppData `Player.log` shows repeated failures, missing assemblies, missing types, or warning spam.
2. Treat the latest large commit as probationary: prefer hardening the newly-expanded areas before inventing the next major subsystem.
3. Treat method-heavy owner files as evidence of integration complexity, not as an automatic rewrite target.
4. If docs already contain a focused follow-up plan, prefer that plan over an unrelated fresh feature only after blockers are quiet.
5. If a subsystem is config-gated or fallback-only in `docs/compatibility-matrix.md`, do not recommend turning it on by default unless the repo clearly shows the stability work is already done.
6. If decompiled game anchors or Harmony signatures are uncertain, recommend signature verification before any new patch expansion.
7. If a feature is loaded, visible, and noisy but not broken enough to block play, prefer one vertical slice of completion over a second sibling feature.

### 6. Pair with implementation skills when needed

After choosing the next step:

1. Use `city-of-gangsters-mod-builder` if the job turns into repo implementation, log triage, patch hardening, optimization, or cleanup.
2. Keep [references/research-grounding.md](references/research-grounding.md) open when the chosen step depends on Harmony limitations, Unity allocation behavior, or decompiled method drift.

## Local Resources

Read these references only when needed:

1. [references/priority-framework.md](references/priority-framework.md)
2. [references/project-sources.md](references/project-sources.md)
3. [references/research-grounding.md](references/research-grounding.md)

Run this helper first for quick signal gathering:

1. `scripts/scan_city_of_gangsters_next_steps.ps1`

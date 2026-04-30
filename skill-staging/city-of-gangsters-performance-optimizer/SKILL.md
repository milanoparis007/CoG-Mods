---
name: city-of-gangsters-performance-optimizer
description: Investigate and optimize City of Gangsters performance using the ClassLibrary1 repo as the primary reference, then the decompiled game files, live game logs/files, and focused web research when needed. Use when diagnosing lag, slow simulation turns, relationship cache churn, map loading delays, UI/input hitching, popup or hover sluggishness, repeated refresh loops, or any performance regression in GameplayTweaks or authored City of Gangsters mod content.
---

# City Of Gangsters Performance Optimizer

Use this skill to investigate performance problems in City of Gangsters with `C:\Users\User\source\repos\ClassLibrary1` as the main implementation reference.

## Workflow

1. Identify the symptom precisely.
   Capture whether the issue is simulation slowdown, map loading delay, UI/input hitching, hover lag, popup delay, selection lag, or a mixed symptom.

2. Separate simulation cost from UI-thread cost.
   Treat turn processing, daily updates, AI loops, relationship recalculation, and load-time reconciliation as simulation paths.
   Treat hover handlers, selection refresh, crew picks, popups, dialog refresh, map overlays, and colorized text generation as UI/input paths.

3. Trace stock behavior before proposing a patch.
   Read the matching path in `decompiled\Game.Session.*` or `decompiled\Game.UI.Session.*` first.
   Confirm what the base game does before assuming the mod introduced the problem.

4. Search the repo for existing fixes before inventing a new one.
   Start with repo-owned guards, caches, refresh throttles, compatibility bridges, instrumentation, and save/load normalization already present in `GameplayTweaks`.

5. Prefer the smallest safe fix.
   Choose targeted caching, deduping, refresh suppression, guard clauses, or memoized lookups before broader rewrites.
   Avoid speculative architectural changes without evidence.

6. Validate narrowly.
   Build the smallest relevant project first.
   Summarize what was validated locally and what still needs an in-game check.

## Repo-First Anchors

Check these paths first:
- `GameplayTweaks\Features\Stability\`
- `GameplayTweaks\GameplayTweaksPlugin.cs` and related partials
- `decompiled\Game.Session.*`
- `decompiled\Game.UI.Session.*`
- `docs\`
- `Things To Have\Current After Prohibition Mod\StreamingAssets\`
- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`

When looking for existing repo patterns, prioritize:
- refresh guards
- relationship or state caches
- post-load reconciliation logic
- compatibility shims
- Harmony patches that already touch the same method family
- verification logging already used by this repo

## Standing Priorities

Always bias investigation toward:
- simulation speed
- relationship caches
- map loading
- UI/input lag
- selection, hover, popup, and dialog responsiveness
- repeated work that causes clicks or turn progression to hitch

## High-Value Suspects

Give extra scrutiny to:
- relationship lookups and relationship-history scans
- repeated LINQ or collection walks over gangs, crew, entities, precincts, or map state
- map color or label rebuilds and territory refreshes
- map loading and post-load reconciliation
- crew pick refreshes, selection focus, hover handlers, and popup refresh loops
- repeated reflection lookups that should be cached
- repeated string formatting, loc generation, and color wrapping
- anything rebuilding the same UI state more than once per frame, per turn, or per input event

## Investigation Rules

- Identify the exact slow symptom first.
- Separate simulation-time cost from UI-thread cost whenever possible.
- Trace the stock game path in `decompiled\` before proposing a patch.
- Search this repo for guards, caches, refresh controls, compatibility bridges, and instrumentation in the same area.
- Prefer the smallest safe fix with the lowest blast radius.
- Avoid speculative rewrites without evidence.
- Use the web only for concrete technical questions.
- When using the web, prefer primary sources such as Unity, Harmony, TextMeshPro, BepInEx, or official documentation.

## Common Tactics

- Cache repeated reflection lookups instead of calling `GetMethod`, `GetField`, or `AccessTools` inside hot paths.
- Replace repeated LINQ in per-frame or per-turn paths with direct loops when the hot path is proven.
- Suppress duplicate UI rebuilds when the same selection or target set is already current.
- Reuse already-computed relationship or display state where the repo has an existing authority helper.
- Guard null-prone or stale UI refresh paths before they cascade into retries or repeated rebuilds.
- Keep verification logging targeted; do not add noisy per-frame logs unless the diagnostic window is short-lived.

## Validation

Default build target:
`dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`

Use a full solution build only when shared contracts changed:
`dotnet build ClassLibrary1.sln -c Release`

When runtime confirmation matters, inspect:
- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`

## Output Shape

Return results in this shape:
- ranked bottlenecks or suspects
- evidence for each suspect
- exact repo files and methods to inspect or patch
- expected impact
- smallest safe implementation plan
- explicit notes on simulation-speed impact, relationship-cache impact, map-loading impact, and UI/input-lag impact

If you implement a fix, also report:
- what changed
- what was validated
- what remains unverified in-game

# Optimization Skill Prompt

Create a Codex skill for optimizing City of Gangsters using this repo as the main reference:
`C:\Users\User\source\repos\ClassLibrary1`

The skill should guide investigation and optimization work using four sources together:
- this repo,
- the decompiled game files in `decompiled\`,
- live game files and logs,
- focused web research when it helps confirm Unity, Harmony, TextMeshPro, BepInEx, or City of Gangsters performance behavior.

The skill should treat this repo as the primary implementation reference. It should prefer existing repo patterns over inventing a new workflow, and it should explicitly point the model toward `GameplayTweaks`, `docs`, authored `StreamingAssets` files, and the decompiled game sources before suggesting changes.

The skill's standing optimization priorities should be:
- simulation speed,
- relationship caches,
- map loading,
- UI/input lag,
- selection, hover, popup, and dialog responsiveness,
- any repeated work that causes inputs to hitch or the turn simulation to stall.

The skill should instruct the model to:
1. Identify the exact slow symptom first.
2. Separate simulation-time cost from UI-thread cost whenever possible.
3. Trace the stock game path in `decompiled\` before proposing a patch.
4. Search this repo for existing guards, caches, refresh controls, compatibility bridges, and instrumentation in the same area.
5. Prefer the smallest safe fix with the lowest blast radius.
6. Avoid speculative rewrites without evidence.
7. Use the web only for concrete technical questions, and prefer primary sources.
8. Validate with the narrowest relevant local build and summarize any unverified in-game risk.

The skill should always give extra scrutiny to:
- relationship lookups and relationship-history scans,
- repeated LINQ or collection walks over gangs, crew, entities, precincts, or map state,
- map color/label rebuilds and territory refreshes,
- map loading and post-load reconciliation,
- crew pick refreshes, selection focus, hover handlers, and popup refresh loops,
- repeated reflection lookups that should be cached,
- repeated string formatting, loc generation, and color wrapping,
- anything rebuilding the same UI state more than once per frame, turn, or input event.

The skill should direct the model to use these repo anchors first:
- `GameplayTweaks\Features\Stability\`
- `GameplayTweaks\GameplayTweaksPlugin.cs` and related partials
- `decompiled\Game.Session.*`
- `decompiled\Game.UI.Session.*`
- `docs\`
- live logs at `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`

The skill should require outputs in this shape:
- ranked bottlenecks or suspects,
- evidence for each suspect,
- exact repo files and methods to inspect or patch,
- expected impact,
- smallest safe implementation plan,
- explicit notes on simulation-speed impact, relationship-cache impact, map-loading impact, and UI/input-lag impact.

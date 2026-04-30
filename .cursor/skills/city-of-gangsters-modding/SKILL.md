---
name: city-of-gangsters-modding
description: Helps design and implement City of Gangsters BepInEx 5 mods in this solution, using Harmony 2 patches, shared save data, and existing project conventions. Use when working on City of Gangsters mods, adding or changing Harmony patches, or reasoning about GameplayTweaks, CopKilling, GameOptimizer, or related plugins in this repository.
---

# City of Gangsters Modding

## Routing vs other CoG skills

- **This skill (`city-of-gangsters-modding`)** — High-level solution context, conventions, save data, and where to put new logic. Use for orientation and general modding questions.
- **`city-of-gangsters-mod-builder`** — Focused playbooks: project map, patch reliability, cleanup, performance, log triage, and validation. Use when implementing, hardening, or triaging `Player.log`.
- **`city-of-gangsters-next-step-advisor`** — Live repo signals and prioritization (what to build or fix next). Use for roadmaps and ordering work; hand implementation to `city-of-gangsters-mod-builder` when execution starts.

## Quick context

This repository contains a suite of **BepInEx 5** mods for the Unity game **City of Gangsters**, targeting **.NET Framework 4.7.2**.

Key projects:

- **ModLauncher (Bridge)** — bridge launcher, outputs `ProhibitionLauncher.dll`
- **GameplayTweaks** — core gameplay tweaks and **shared save data** (`ModSaveData`, `CrewModState`, `G`, `ModConstants`)
- **GameOptimizer** — performance-related patches (lot creation, map nodes, rendering, territory rebuild debouncing)
- **CopKilling** — cop war system, depends on `GameplayTweaks`
- **BossBuildings**, **BossDeath**, **OrgChartMod**, **AutoLevelup** — feature-specific plugins

Patching uses **Harmony 2** and **MonoMod RuntimeDetour** against the game’s `Assembly-CSharp` and `UnityEngine` assemblies.

## When to use this skill

Use these instructions whenever:

- The user asks to **add or modify a mod** for City of Gangsters in this solution.
- The task involves **BepInEx plugins**, **Harmony patches**, or **MonoMod detours**.
- The change touches **shared save data** (e.g. `ModSaveData`, `SaveLoadPatch`) or **cross-mod state**.
- The user is refactoring or extending **GameplayTweaks**, **CopKilling**, or **GameOptimizer**.
- The user asks how to **deploy** updated DLLs into the game’s `BepInEx/plugins` folder.

## Modding workflow in this repo

When implementing or changing a modded feature:

1. **Identify the right project**
   - **GameplayTweaks**: core gameplay changes, shared state, systems most other mods need.
   - **GameOptimizer**: performance and optimization patches.
   - **CopKilling**: anything about attacking cops, witnesses, or cop truces (depends on `GameplayTweaks`).
   - Other feature-specific projects for their respective domains.

2. **Understand the game API**
   - Assume game types live in `Assembly-CSharp` and `UnityEngine`.
   - Look for appropriate target classes and methods (e.g. crew actions, city simulation ticks, UI interactions).
   - Prefer patching **stable, central points** (like controller methods) over many scattered leaves.

3. **Design Harmony patches**
   - Prefer **`[HarmonyPatch(typeof(TargetType), "MethodName")]`** style patches.
   - Choose **prefix/postfix** carefully:
     - Use **prefix** to **validate, short-circuit, or adjust inputs**.
     - Use **postfix** to **react to results, update mod state, or log**.
   - Avoid heavy work in patches that run very frequently (per-frame or per-tick) unless placed in `GameOptimizer`.

4. **Use shared save data correctly**
   - Treat `GameplayTweaks`’s `ModSaveData` as the **central store** for mod-specific persistent state.
   - Add new fields thoughtfully, keeping backward compatibility in mind where possible.
   - Rely on the existing `SaveLoadPatch` hooks to serialize/deserialize as JSON; integrate new fields there if needed.
   - For **cross-mod** reads/writes, go through the public/static access points exposed by `GameplayTweaks` (e.g. `GameplayTweaksPlugin.SaveData`, shared RNG).

5. **Respect project conventions**
   - Use **tab indentation**, not spaces.
   - Use debug logging in the form: `Debug.Log("[ModName] message")` (where `ModName` matches the plugin).
   - Prefer small, focused helper methods over very large patches for readability.

6. **Build and deploy**
   - Build with:
     - `dotnet build ClassLibrary1.sln` (Debug or Release as appropriate)
     - Or the provided scripts (e.g. `scripts/build-mods.ps1` for lock-hardened builds).
   - Deploy plugin DLLs from each project’s `bin\Debug\` or `bin\Release\` into:
     - `C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\BepInEx\plugins`
   - Remember: only the **game’s** `BepInEx\plugins` folder is actually loaded; staging folders are not.

## Common patterns and best practices

### Choosing where to put new logic

- **Gameplay tweaks that might be reused** by multiple mods → put shared logic and data in `GameplayTweaks`.
- **Performance fixes** → prefer `GameOptimizer` so they’re centralized and can be toggled or reasoned about together.
- **Cop interaction systems** (attacking cops, truces, witnesses) → extend `CopKilling`, and use its dependency on `GameplayTweaks` for shared state.
- **One-off UI/visualization features** like organization displays → keep logic near the relevant project (`OrgChartMod`, `BossBuildings`, etc.).

### Harmony patching style

When writing patches:

- Keep patches **thin**: do the minimum necessary and delegate to helper methods in the same project.
- Avoid **silent failures**:
  - Guard reflection lookups and casts with null checks.
  - Log failures with enough context to debug in-game.
- Be mindful of **patch ordering** and compatibility with other mods:
  - Where possible, patch **non-critical** methods or use checks so other mods can coexist.
  - Avoid hard assumptions about other plugins unless this suite controls them.

### Save/load and shared state

- Use `ModSaveData` for:
  - Crew-related flags, relationships, and persistent stats.
  - Mod-specific systems (e.g. jail/wanted, dirty cash) that need to survive across loads.
- Keep `SaveLoadPatch` as the **single point** where data is serialized:
  - Update it when adding/removing major fields.
  - Avoid ad-hoc JSON persistence scattered across projects.

### Deployment and testing

- Test new features in **Debug** builds first.
- If there is a build script such as `scripts/sync-stablecore-plugins.ps1`, prefer using it to sync the stable plugin set into the game folder.
- Be cautious when:
  - Adding destructive or irreversible systems (e.g. boss death, cop killing).
  - Changing how money, inventory, or territory is handled.
  - Modifying anything that runs on every game tick or frame.

## How to ask for help (examples)

When users ask for help related to this skill, interpret their requests in this context. Examples:

- “Add a new crew trait that affects jail time”  
  → Suggest adding the core logic and save fields in `GameplayTweaks`, with Harmony patches on the relevant game systems, plus JSON integration via `ModSaveData`.

- “Optimize territory recalculation, it’s too laggy at end of turn”  
  → Propose placing or extending patches in `GameOptimizer` targeting the territory rebuild logic, ensuring debouncing and incremental recalculation where possible.

- “Let my crew attack cops during certain events”  
  → Extend `CopKilling`, use `GameplayTweaks` shared state, and patch the combat/interaction controllers accordingly, while tracking wanted level and truces.

When in doubt, prefer:
- Centralized shared systems in `GameplayTweaks`.
- Performance-sensitive logic in `GameOptimizer`.
- Feature-specific behaviors in the corresponding plugin projects.

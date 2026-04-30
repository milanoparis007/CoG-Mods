---
name: city-of-gangsters-mod-builder
description: Build, fix, clean up, and optimize City of Gangsters BepInEx mods in the ClassLibrary1 solution. Use when implementing gameplay/UI patches, triaging Player.log issues, hardening Harmony patches, cleaning up mod code, validating compatibility, or improving performance in GameplayTweaks, GameOptimizer, CopKilling, OrgChartMod, BossBuildings, BossDeath, AutoLevelup, and ModLauncher.
---

# City of Gangsters Mod Builder

Use this skill for repo-specific City of Gangsters mod work. It is a thin router backed by focused playbooks for patch reliability, cleanup, performance, and log triage.

The guidance here is grounded in this repo plus official BepInEx, Harmony, and Unity performance docs.

## Start Here

1. Route the task to the owning project with [references/project-map.md](references/project-map.md).
2. Search the current implementation with `rg`, then read the nearby code before proposing changes.
3. Scan the relevant `Player.log` or `Player-prev.log` lines when the task mentions errors, warnings, regressions, load order, or compatibility.
4. Pick the right playbook:
   - Reliability, patch target drift, reflection failures, Harmony warnings:
     [references/patch-reliability.md](references/patch-reliability.md)
   - Refactors, deduplication, helper extraction, oversized files:
     [references/code-cleanup-playbook.md](references/code-cleanup-playbook.md)
   - Hot paths, turn-time spikes, coroutine churn, allocation pressure:
     [references/performance-playbook.md](references/performance-playbook.md)
   - Unknown warnings/errors, plugin conflicts, load failures:
     [references/log-triage.md](references/log-triage.md)

## Workflow

### 1. Route first

- Use [references/project-map.md](references/project-map.md).
- If the symptom starts from a runtime warning/error and project ownership is unclear, start with [references/log-triage.md](references/log-triage.md), then route to the owner.

### 2. Inspect before editing

- Search exact types, methods, config keys, and log signatures with `rg`.
- Read the surrounding patch or subsystem, not just the matched line.
- Check `decompiled/` when the patch target or signature is uncertain.
- Prefer the project that already owns the code path instead of introducing a second patch site.

### 3. Choose the lane

- `patch-reliability.md`
  - Missing methods, unstable reflection, Harmony target drift, virtual/base patch warnings, compatibility hardening.
- `code-cleanup-playbook.md`
  - Repeated reflection, duplicate constants, large patch classes, risky refactors, helper reuse.
- `performance-playbook.md`
  - Map generation, rendering, territory refresh, turn update, coroutine batching, cache-per-turn work.
- `log-triage.md`
  - Player.log triage, plugin conflicts, duplicate GUID/load issues, missing type/method failures.

### 4. Implement minimally

- Reuse the owning project's existing Harmony pattern.
- Prefer postfix/prefix/finalizer before transpilers.
- Prefer small helpers over new reflection trees.
- Keep risky gameplay changes behind config flags.
- Preserve compatibility gates unless the task explicitly changes ownership or authority.

### 5. Validate narrowly

- Use [references/workflow-checklist.md](references/workflow-checklist.md).
- Build the changed project first:
  - `dotnet build <project>.csproj -c Release`
- Build the full solution only when:
  - public/internal signatures changed,
  - shared types/helpers changed,
  - another project now calls the new members.

## Repo Defaults

- `GameplayTweaks` owns most gameplay state, save data, and major UI behavior.
- `CopKilling` owns cop-war behavior and reads GameplayTweaks state.
- `GameOptimizer` owns performance-only changes.
- The runtime plugin folder is only:
  - `C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\BepInEx\plugins`
- Staging folders are not runtime-loaded unless explicitly copied.

## Common Commands

```powershell
# Search current implementation
rg -n "<pattern>" GameplayTweaks GameOptimizer CopKilling -S

# Search logs
rg -n "<pattern>" Player.log Player-prev.log -S

# Build one mod
dotnet build GameplayTweaks/GameplayTweaks.csproj -c Release

# Build all mods
dotnet build ClassLibrary1.sln -c Release
```

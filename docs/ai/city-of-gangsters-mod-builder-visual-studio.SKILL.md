---
name: city-of-gangsters-mod-builder-visual-studio
description: Build, fix, clean up, and optimize City of Gangsters BepInEx mods in ClassLibrary1 with Visual Studio-first context. Use when implementing gameplay or UI patches from Visual Studio, triaging live SomaSim logs, hardening Harmony patches, validating compatibility, or translating repo workflow into Visual Studio search, Error List, and solution context without depending on rg.
---

# City of Gangsters Mod Builder Visual Studio

Use this skill for hands-on City of Gangsters mod implementation when Visual Studio is the primary editor over the same `ClassLibrary1.sln` workspace.

Keep it Visual Studio-aware but still shell-backed:

1. Prefer Solution Explorer, Find in Files, Go To, Call Hierarchy, and Error List for navigation.
2. Prefer Developer PowerShell or Terminal inside Visual Studio for builds and log inspection.
3. Use PowerShell search defaults instead of assuming `rg` exists.
4. Feed workflow improvements back into `city-of-gangsters-mod-builder` so the editor-neutral skill stays current for VS Code and shell usage too.
5. Keep runtime-log, project-owner, and validation rules aligned with the original builder skill.
6. Always treat the After Prohibition mod as the main `StreamingAssets` project when comparing or merging game data.

Durable local tooling and content roots:

1. Decompiler: `C:\Users\User\Documents\Modding Programs\DNSPEX`
2. Video tools: `C:\Users\User\Documents\COG Modding Stuff\ffmpeg-8.1-full_build`
3. Local comparison/content root: `C:\Users\User\source\repos\ClassLibrary1\Things To Have`
4. Live game `StreamingAssets`: `%ProgramFiles(x86)%\Steam\steamapps\common\City of Gangsters\CoG_Data\StreamingAssets`

Shared workspace reference:

1. `docs/ai/city-of-gangsters-workspace-conventions.md`

## When To Use It

Use this skill when the task is to:

1. change `GameplayTweaks`, `GameOptimizer`, `CopKilling`, `BossBuildings`, `BossDeath`, `OrgChartMod`, `AutoLevelup`, or `ModLauncher` from Visual Studio
2. debug a runtime regression while using Visual Studio navigation and solution context
3. harden Harmony or reflection patches after checking exact signatures in decompiled or solution code
4. validate build errors or warnings through Visual Studio Error List plus terminal builds
5. keep Visual Studio-specific workflow knowledge in sync with `city-of-gangsters-mod-builder`

## Visual Studio Workflow

Use this workflow in Visual Studio:

1. open `ClassLibrary1.sln`
2. route ownership with `references/project-map.md`
3. inspect live logs in Terminal or Developer PowerShell
4. search with Find in Files, Go To All, or PowerShell `Select-String`
5. read the owning file and nearby subsystem before editing
6. build the touched project from Terminal first
7. use Error List only as a secondary surface over the terminal build result
8. summarize changed files, validation, and remaining runtime risk

Visual Studio defaults:

1. Find in Files is the primary code search surface.
2. Solution Explorer is the primary ownership map.
3. Call Hierarchy and Find All References are preferred before adding new patch sites.
4. Terminal commands remain the source of truth for build and runtime validation.

## Search Defaults

Prefer these commands and Visual Studio equivalents:

```powershell
# PowerShell fallback search
Get-ChildItem GameplayTweaks,GameOptimizer,CopKilling -Recurse -File | Select-String -Pattern "<pattern>"

# Live logs
Get-Content "$env:USERPROFILE\AppData\LocalLow\SomaSim\City of Gangsters\Player.log" -Tail 200
Get-Content "$env:USERPROFILE\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log" -Tail 200

# Build one project
dotnet build CopKilling/CopKilling.csproj -c Release

# Build full solution only when shared contracts changed
dotnet build ClassLibrary1.sln -c Release
```

Visual Studio equivalents:

1. Find in Files for type names, methods, config keys, and log strings.
2. Go To All for exact filenames and symbols.
3. Find All References before duplicating patch ownership.
4. Error List after terminal build, not instead of it.

Use these local references deliberately:

1. Open `DNSPEX` when game-side signatures or call chains are uncertain, especially for AI, convo, arrest, raid, and combat flows.
2. Use `ffmpeg.exe` and `ffprobe.exe` from the local `ffmpeg-8.1-full_build\bin` folder when video trimming or frame-level bug review is needed.
3. Use `Things To Have\Current After Prohibition Mod` as the primary authored `StreamingAssets` baseline.
4. Use `Things To Have\Vanilla Streamingassets and Csharp` and `Things To Have\PIA Mod Vanilla` as comparison donors, not as the main project.

## Native Crash Workflow

When a City of Gangsters crash hard-closes without a useful managed stack, switch from pure log triage to dump capture early.

Use this local ProcDump path:

1. `C:\Users\User\Documents\Modding Programs\ProcDump\procdump.exe`

Typical attach command while the game is already running:

```powershell
& "C:\Users\User\Documents\Modding Programs\ProcDump\procdump.exe" -e -ma -t CoG "C:\Users\User\Documents\CrashDumps"
```

Interpretation guidance:

1. Repeated `406D1388` lines are usually thread-name noise, not the real crash.
2. `C00000FD.STACK_OVERFLOW` followed by `C0000005.ACCESS_VIOLATION` strongly suggests runaway recursion or re-entry.
3. Open the resulting `.dmp` in Visual Studio with `Debug with Native Only`.
4. If the dump only shows `ntdll.dll` and `KERNELBASE.dll`, keep walking down to the first `UnityPlayer.dll` frame before drawing conclusions.
5. A shallow native tail stack after a stack overflow often means the original managed/game caller has already unwound; in that case, feed the result back into code-side re-entry hardening instead of over-trusting the dump.

Current durable inference from this project:

1. The latest confirmed hard crash class was `STACK_OVERFLOW` first, then native access violation.
2. That pattern fit the `CopKilling` re-entry / UI refresh / hostility-application debugging path better than a random pointer bug.

## Start Here

1. Use the original builder skill references, especially `references/project-map.md`.
2. If the issue mentions a runtime regression, inspect live logs first.
3. If Harmony targets are uncertain, inspect decompiled anchors before patching.
4. If the issue is map-authoring or exported-map editing, hand off to `city-of-gangsters-map-maker`.
5. If the task becomes strategy or prioritization, hand off to `city-of-gangsters-next-step-advisor`.
6. When a task touches `StreamingAssets`, inspect the live game folders and the `Things To Have` mirrors together before editing assumptions.

## StreamingAssets Priorities

Keep these priorities explicit:

1. `Current After Prohibition Mod\StreamingAssets` is the primary project being evolved.
2. Live game `StreamingAssets` is the runtime truth for current behavior and load order.
3. `Vanilla Streamingassets and Csharp` is the clean reference for isolating regressions.
4. `PIA Mod Vanilla` is a donor/reference set for selected legal-business and related merges.

Main live `StreamingAssets` folders to inspect first:

1. `Entities`
2. `Settings`
3. `Maps`
4. `Loc`
5. `UI`

## Visual Studio Notes By Task

### Runtime triage

1. Keep `Player.log` and `Player-prev.log` open in an external tab or terminal.
2. Match the crash signature to the owning subsystem in Solution Explorer.
3. Use Find in Files on exact exception methods before editing.

### Harmony patching

1. Confirm signatures with decompiled code or reflection helpers.
2. Prefer prefix, postfix, or finalizer over transpiler when practical.
3. Keep risky runtime slices behind config gates.

### Large-file maintenance

1. Use the Document Outline, method dropdowns, and Find All References to avoid blind edits in oversized files.
2. Prefer extracting helpers only when ownership stays obvious.

## Sync Back To The Original Builder

When this skill gains a useful Visual Studio workflow improvement, mirror the durable repo-agnostic parts back into `city-of-gangsters-mod-builder` so the shell/VS Code skill stays current.

Mirror back:

1. better ownership-routing guidance
2. improved log-triage sequence
3. safer build/validation defaults
4. reflection or Harmony hardening patterns

Do not mirror back:

1. Visual Studio UI-only advice that does not help shell or VS Code workflows
2. assumptions that require `rg`

## Repo Defaults

1. `GameplayTweaks` owns most gameplay state, save data, crew HUD behavior, and major UI flows.
2. `CopKilling` owns cop-war behavior and reads `GameplayTweaks` state.
3. `GameOptimizer` owns performance-only patches.
4. Use live SomaSim logs under `%USERPROFILE%\AppData\LocalLow\SomaSim\City of Gangsters`.
5. Treat Visual Studio as a frontend over the same repo and command workflow, not a separate system.

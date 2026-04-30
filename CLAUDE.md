# City of Gangsters - BepInEx Mod Suite

## Project Overview
A collection of BepInEx 5 mods for the Unity game "City of Gangsters". Each mod is a separate .NET 4.7.2 class library that produces a BepInEx plugin DLL.

## Tech Stack
- **Framework**: .NET Framework 4.7.2
- **Mod Loader**: BepInEx 5
- **Patching**: Harmony 2, MonoMod RuntimeDetour
- **Engine**: Unity (via Assembly-CSharp, UnityEngine)
- **Build**: MSBuild / Visual Studio 2022

## Solution Structure

| Project | Plugin ID | Description |
|---------|-----------|-------------|
| ModLauncher (Bridge) | `com.mods.modlauncher` | Bridge launcher GUID for compatibility; outputs `ProhibitionLauncher.dll` to avoid collision with external `ModLauncher.dll` |
| GameplayTweaks | `com.mods.gameplaytweaks` | Core gameplay mod - crew stats, gang pacts, dirty cash, jail system, wanted system, booze trading, safebox, marriage/hiring tweaks |
| GameOptimizer | `com.mods.gameoptimizer` | Performance patches - lot creation, map nodes, rendering, territory rebuild debouncing |
| CopKilling | `com.mods.copkilling` | Cop war system - allows attacking cops, witness tracking, cop truces. Depends on GameplayTweaks |
| BossBuildings | `com.mods.bossbuildings` | Boss building management features |
| BossDeath | `com.mods.bossdeath` | Boss death handling |
| OrgChartMod | `com.mods.orgchart` | Organization chart display |
| AutoLevelup | `com.mods.autolevelup` | Automatic crew leveling |

## Key Architecture Notes
- **GameplayTweaks** is the core mod that most others depend on. It manages shared save data (`ModSaveData`), crew state tracking (`CrewModState`), and provides utility classes (`G`, `ModConstants`).
- **CopKilling** depends on GameplayTweaks via `[BepInDependency]` and accesses shared state through `GameplayTweaksPlugin.SaveData` and `GameplayTweaksPlugin.SharedRng`.
- Save/load is handled by `SaveLoadPatch` in GameplayTweaks which hooks the game's save system and serializes `ModSaveData` as JSON.
- Most game API access uses Harmony patches (prefix/postfix) and reflection for internal types.

## Build
```
dotnet build ClassLibrary1.sln
```
Output DLLs go to each project's `bin\Debug\` folder. Copy to `BepInEx\plugins\` in the game directory.

For lock-hardened deterministic builds, use:
```
powershell -ExecutionPolicy Bypass -File scripts/build-mods.ps1 -Configuration Release
```

## Game Install Path
```
C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\
```

## Plugin Deployment Notes
- Runtime plugin load path is only:
  - `C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\BepInEx\plugins`
- Live runtime logs are in AppData:
  - `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
  - `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`
- Repo-root log copies are not authoritative and should not be used for current runtime triage.
- Staging folders (for example `C:\Users\User\Documents\COG Modding Stuff\Notes\New folder`) are **not** auto-loaded by the game.
- Stable-core staging sync helper:
  - `powershell -ExecutionPolicy Bypass -File scripts/sync-stablecore-plugins.ps1`
- Optional removal of denylisted high-conflict plugins during sync:
  - `powershell -ExecutionPolicy Bypass -File scripts/sync-stablecore-plugins.ps1 -RemoveDenylistedFromPlugins`

## Conventions
- All mods use tab indentation
- Reflection is used extensively for accessing internal game APIs
- Debug logging uses `Debug.Log("[ModName] message")` pattern
- Decompiled game source is in `decompiled/` for reference (not committed)

## Skills (`.claude/skills/`)
- **mod-dev** — Full reference for creating mods, Harmony patching patterns, project scaffolding, save data integration, UI modding, and build/deploy. Read this before writing any mod code.
- **game-research** — Workflow for investigating game internals: decompiling, mapping systems, and understanding game namespaces. Read this before patching unfamiliar systems.

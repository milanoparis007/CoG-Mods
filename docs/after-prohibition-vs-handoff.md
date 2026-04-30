# After Prohibition VS Handoff

## Current state

This repo now has two separate compatibility tracks for removing the custom `Assembly-CSharp.dll` dependency:

1. `GameplayTweaks` runtime Harmony patches for non-core assembly edits
2. `AfterProhibitionPreloader` BepInEx preloader patcher for core type/member additions that plugins and `StreamingAssets` expect

The preloader is now loading and patching successfully at game startup.

Confirmed in:

- `C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\BepInEx\LogOutput.log`

Relevant log lines:

- `Loaded 1 patcher method from [AfterProhibitionPreloader 0.0.0.0]`
- `Patching [Assembly-CSharp] with [AfterProhibitionPreloader.AfterProhibitionAssemblyPatcher]`
- `Added label field Game.Session.Sim.ResourceConstants::DIRTYCASH -> dirty-cash`
- `Added Fixnum field Game.Services.VictorySettings::amtDirtyCash`

## What is already ported

### GameplayTweaks side

File:

- `GameplayTweaks\Features\Compatibility\GameplayTweaksPlugin.AfterProhibitionAssemblyPort.cs`

This already ports:

- extra victory goals
- faster `SeasonManager.FindDisplayedTimeOfDay()` behavior
- portrait table tweaks

Important details in that file:

- reads `amtDirtyCash`, `amtCounterfeit`, and `amtStolenGoods` from `StreamingAssets\Settings\Settings.sim`
- injects custom goals if they are missing
- uses runtime subgoals instead of relying on the edited `VictoryTracker` class

Relevant spots:

- custom goal regex and settings parsing
- `victory.dirtycash.goal.name`
- `victory.drugs.goal.name`
- `victory.homes.goal.name`

### Preloader side

Project:

- `AfterProhibitionPreloader\AfterProhibitionPreloader.csproj`

Main files:

- `AfterProhibitionPreloader\AfterProhibitionAssemblyPatcher.cs`
- `AfterProhibitionPreloader\AssemblyPatchManifest.cs`
- `AfterProhibitionPreloader\AssemblyPatchManifest.json`
- `AfterProhibitionPreloader\README.md`

The preloader currently adds these `ResourceConstants` fields:

- `COUNTERFEITCASH -> counterfeitcash`
- `DIRTYCASH -> dirty-cash`
- `STOLENGOODS -> stolenelectronics`
- `CANNABIS -> cannabis-pound`
- `COCAINE -> cocaine-tiles`
- `HEROIN -> heroin-packs`
- `HOMES -> home-mid`
- `JAZZ -> bandlow`

It also adds these `VictorySettings` fields:

- `amtDirtyCash`
- `amtStolenGoods`
- `amtCounterfeit`

## What is still broken

The current blocker is not the preloader placement anymore. The patcher is running.

The remaining core compatibility issue is still:

- `Unexpected key: dirty-cash in type Game.Core.Price`

Confirmed in:

- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`

This means `Game.Core.Price` still does not understand the extra serialized `dirty-cash` value from `StreamingAssets` / save load paths. So even though:

- `ResourceConstants.DIRTYCASH` now exists
- `VictorySettings.amtDirtyCash` now exists

the game still cannot deserialize dirty-cash-aware price data.

The startup crash that still follows is:

- `The type initializer for 'DirtyCashEconomy.DirtyCashManager' threw an exception.`

Likely interpretation:

1. the plugin now gets further than before because `ResourceConstants.DIRTYCASH` exists
2. but it still hits a path that assumes dirty-cash-aware core money/price behavior
3. the missing `Price` support is the next hard blocker

## Most likely next task

Patch `Game.Core.Price` in the preloader.

That is the next place to work in Visual Studio.

Goal:

- make vanilla `Price` accept the serialized extra `dirty-cash` member expected by the mod data and/or dirty-cash plugin

This probably means comparing the edited custom assembly against vanilla specifically for:

- `Game.Core.Price`
- any serializer helpers or custom serialization hooks touching `Price`
- any money/price conversion code used by `DirtyCashEconomy`

## Files to inspect first in Visual Studio

### Repo files

- `AfterProhibitionPreloader\AfterProhibitionAssemblyPatcher.cs`
- `AfterProhibitionPreloader\AssemblyPatchManifest.json`
- `GameplayTweaks\Features\Compatibility\GameplayTweaksPlugin.AfterProhibitionAssemblyPort.cs`
- `.codex_tmp\dirtycash_economy\DirtyCashEconomy\DirtyCashManager.cs`
- `.codex_tmp\dirtycash_economy\DirtyCashEconomy\IllegalResources.cs`
- `decompiled\Game.Core\Price.cs`
- `decompiled\Game.Services\SerializerService.cs`

### Custom assembly comparison files

- `.codex_tmp\asmcmp_afterprohibition\Assembly-CSharp\Game\Core\Price.cs`
- `.codex_tmp\asmcmp_vanilla\Assembly-CSharp\Game\Core\Price.cs`
- `.codex_tmp\asmcmp_afterprohibition\Assembly-CSharp\Game\Session\Sim\ResourceConstants.cs`
- `.codex_tmp\asmcmp_afterprohibition\Assembly-CSharp\Game\Services\VictorySettings.cs`

## Suggested debugging sequence

1. Open `AfterProhibitionPreloader` in Visual Studio and keep `BepInEx\LogOutput.log` open.
2. Confirm preloader still logs the added fields every run.
3. Compare vanilla vs custom `Price` again using the real shipped edited DLL, not only the old decompile snapshot.
4. Search the decompiled game and dirty-cash plugin for all uses of `Price`, `DoChangeMoney`, and `dirty-cash`.
5. Extend the preloader to patch the next required core type/member set.
6. Rebuild and retest with vanilla `Assembly-CSharp.dll`.

## Known log signals

### Good

- preloader loaded
- `Assembly-CSharp` patched
- custom `ResourceConstants` fields added
- custom `VictorySettings` fields added

### Still bad

- `Unexpected key: dirty-cash in type Game.Core.Price`
- `The type initializer for 'DirtyCashEconomy.DirtyCashManager' threw an exception.`

### Not the main issue right now

There are many other `Unexpected key` warnings in `Player.log`:

- `maxRel`
- `maxBuildings`
- `mods`
- `grants`
- `sink`
- `aoe`
- `above`
- `sell`

Those may matter later, but the current dirty-cash crash path points to `Price` first.

## Build commands

### Build preloader

```powershell
dotnet build AfterProhibitionPreloader\AfterProhibitionPreloader.csproj -c Release
```

### Build GameplayTweaks

```powershell
dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release
```

## Install locations

Preloader output goes to:

- `BepInEx\patchers\AfterProhibitionPreloader.dll`
- `BepInEx\patchers\AssemblyPatchManifest.json`

Regular plugin output goes to:

- `BepInEx\plugins\`

## Short summary

The custom goals are partially handled already through `GameplayTweaks`, and the preloader now correctly restores the missing `ResourceConstants` and `VictorySettings` members. The next missing compatibility layer is almost certainly `Game.Core.Price`, which is why dirty-cash content still does not load cleanly even though the preloader is finally running.

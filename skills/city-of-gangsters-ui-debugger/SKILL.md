---
name: city-of-gangsters-ui-debugger
description: Diagnose, stabilize, and patch City of Gangsters UI bugs in the ClassLibrary1 repo across GameplayTweaks Harmony patches, decompiled Game.UI/Game.UI.Session sources, live SomaSim logs, and BepInEx compatibility surfaces. Use when investigating lingering portraits, stale crew picks or map icons, popup/dialog hide-show bugs, mouseover/context leaks, null-prone UI refreshes, selection/focus issues, retheme collisions, or duplicate/conflicting buttons and panels from overlapping mods.
---

# City of Gangsters UI Debugger

## Overview

Use this skill for UI triage first when a City of Gangsters bug touches HUD state, popups, crew picks, portraits, dialog teardown, or external UI-mod overlap.

Keep the workflow narrow:

1. Check live logs before patching.
2. Identify the exact UI surface by path and owning class.
3. Prefer minimal-burst fixes that clear stale state instead of broad rewrites.
4. Re-check compatibility flags before blaming the base game.

## Start Here

Read these first:

1. `docs/ai/city-of-gangsters-workspace-conventions.md`
2. `.cursor/skills/city-of-gangsters-mod-builder/references/project-map.md`
3. `references/ui-surfaces-and-log-signals.md`

Use live logs only:

1. `%USERPROFILE%\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
2. `%USERPROFILE%\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`

## Investigation Flow

### 1. Triage logs first

Check for:

1. `AggroUI`
2. `HostileMobileSelect`
3. `VehicleNodeAuthority`
4. compatibility matrix lines
5. UI exceptions such as `NullReferenceException` or `MissingReferenceException`

Interpretation defaults:

1. Repeated dirty/flush cycles without asset failures usually point to stale UI state, pooled objects, or additive refresh logic.
2. A repaired refresh after a `NullReferenceException` often means the visual survived a partial update.
3. Compatibility noise matters when external UI mods, custom asset loaders, or duplicate HUD/menu patches are active.

### 2. Identify the exact surface before patching

Use the owning class and path, not a vague "portrait bug" label.

Common surfaces:

1. `Game.UI.Session.Picks.CrewPick` -> `Button/Portrait`
2. `Game.UI.Session.Convo.ConversationDialog` -> `Owner/Portrait/Portrait`
3. `Game.UI.Session.PersonInfoDialog` -> `Owner/Portrait/Portrait`
4. `Game.UI.CrewPeepInspectPopup` -> `Character/Biography/Mugshot/Portrait/Portrait`
5. `Game.UI.Session.Popups.PortraitPopup` -> `Panel/Portrait/Portrait`

If the bug is on-map, inspect pick pooling, refresh, remove, and target-reconcile paths first.

If the bug is inside a dialog or popup, inspect show/hide lifecycle methods first:

1. `OnBeforeShow`
2. `RefreshContents`
3. `OnBeforeHide`
4. `OnAfterHide`
5. `Reset`
6. `Release`

### 3. Prefer minimal-burst fixes

Use these fix patterns in this order:

1. Prune stale pooled UI state.
2. Clear rendered state on reset/deactivate.
3. Reconcile before refresh when base logic is additive-only.
4. Use prefix/postfix/finalizer guards before considering transpilers.

Avoid:

1. large transpilers for simple teardown bugs
2. broad "refresh everything" patches unless the base UI is already built around that pattern
3. speculative asset changes when the logs show state churn instead

## Compatibility Pass

Always inspect `GameplayTweaksPlugin.Compat.cs` and current compatibility logs before assuming a pure base-game issue.

Pay special attention to:

1. external UI enhancers
2. custom portraits or custom asset stacks
3. duplicate optimizer/runtime mods that add log noise or timing churn
4. overlapping HUD/menu/selection patches

Use compatibility findings to classify the bug:

1. `asset`
2. `state`
3. `pooling`
4. `selection`
5. `compatibility`

## Validation

Build the narrowest touched project first:

```powershell
dotnet build GameplayTweaks/GameplayTweaks.csproj -c Release
```

After the build:

1. summarize the likely owning UI class
2. state whether the bug is asset, state, pooling, selection, or compatibility driven
3. list bypass options
4. list full-fix options
5. call out unverified runtime risk if the game was not repro-tested

## External Unity Sources

Use outside Unity sources only when the repo and decompiled game code do not already answer:

1. object-pooling lifecycle order
2. event ordering
3. Unity UI teardown behavior
4. edge-case component activation semantics

Prefer repo code and decompiled anchors over generic Unity advice.

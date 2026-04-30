# UI Surfaces And Log Signals

Use this file when the bug report is UI-shaped but the owning surface is still unclear.

## Core UI surfaces

### On-map picks and map-adjacent indicators

1. `decompiled/Game.UI.Session.Picks/CrewPick.cs`
   Portrait-bearing crew/map indicator. Primary suspect for lingering on-map portraits and stale crew icons.
2. `decompiled/Game.UI.Session.Picks/PickContainer.cs`
   Add/refresh/remove flow for pooled picks.
3. `decompiled/Game.UI.Session.Picks/PickPool.cs`
   Pool allocate/free/reset lifecycle.
4. `decompiled/Game.UI.Session.Picks/PickManager.cs`
   Event-driven refresh and removal entry points.

Look for:

1. additive refresh logic
2. missing prune/remove calls
3. pooled objects keeping rendered state
4. stale target reconciliation

### Dialog and popup portraits

1. `decompiled/Game.UI.Session.Convo/ConversationDialog.cs`
2. `decompiled/Game.UI.Session/PersonInfoDialog.cs`
3. `decompiled/Game.UI/CrewPeepInspectPopup.cs`
4. `decompiled/Game.UI.Session.Popups/PortraitPopup.cs`

Look for:

1. `OnBeforeShow`
2. `RefreshContents`
3. `OnBeforeHide`
4. `OnAfterHide`
5. `Release`

## Repo-owned patch surfaces

Start in:

1. `GameplayTweaks/GameplayTweaksPlugin.FeatureRegistrars.cs`
2. `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs`
3. `GameplayTweaks/GameplayTweaksPlugin.cs`
4. `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle*.cs`
5. `GameplayTweaks/GameplayTweaksPlugin.Compat.cs`

## Log strings that usually matter

### State/pooling churn

1. `AggroUI`
2. `HostileMobileSelect`
3. `VehicleNodeAuthority`

Useful interpretations:

1. `dirty` and `flush` storms suggest repeated targeted UI refreshes.
2. `crew-pick-refresh-repaired` indicates a partial refresh failure path that may leave stale visuals behind.
3. `crew-picks-cleared` or `crew-pick-pruned` confirms the mod is actively reconciling stale pick targets.

### Compatibility

1. `Compat`
2. `uiRetheme`
3. `externalEnhancer`
4. `Custom Portraits`
5. `CoGCustomAssets`

Useful interpretations:

1. external UI enhancers can change hierarchy assumptions or add duplicate controls
2. custom portrait stacks matter for asset failures, but not every lingering portrait is an asset bug
3. duplicate optimizer/runtime mods can distort timing and produce extra UI churn

## Practical diagnosis shortcuts

### If the portrait lingers on the map

Suspect in order:

1. `CrewPick`
2. `PickManager.RefreshAllPlayerCrewPicks`
3. pooled pick reset/clear behavior
4. external patches that normalize or prune hostile vehicle picks

### If the portrait lingers in a sidebar or dialog

Suspect in order:

1. `ConversationDialog`
2. `PersonInfoDialog`
3. shared HUD hide/show grouping in `HUDManager` and `BaseHUDDialog`

### If there is no exception

Bias toward:

1. stale state
2. missed teardown
3. additive-only refresh
4. compatibility overlap

### If there is an exception

Bias toward:

1. finalizer/postfix guard
2. stale object prune
3. backing-target reconciliation

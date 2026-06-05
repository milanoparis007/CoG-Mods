# AfterProhibitionCompatibility Phase Prompts

Use this plan to split external-DLL detection, compatibility gates, and broad-unpatch protection out of `GameplayTweaks` into a separate BepInEx plugin named `AfterProhibitionCompatibility`.

## Goal

Create a compatibility-only owner for unstable external integrations:

- Dirty Cash Economy DLL detection and bridge state;
- 10K Button & Input DLL detection;
- external safebox/menu/launcher detection;
- safe broad-unpatch prevention;
- compatibility matrix logging;
- read-only bridge methods that gameplay/UI plugins can query.

This plugin must not own:

- shop stock, buy/sell rules, bank/warehouse purchasing, or resource generation;
- delivery route execution or staged shop orders;
- family, spouse, politics, AI, combat, territory, or economy balance;
- UI rendering that already belongs in `AfterProhibitionUI`.

## Current Live Signals

Recent logs show external compatibility is still an active risk:

```txt
[Info   :Dirty Cash Economy] [LegalBusiness] FindAllModuleDefsExpensive returned 890 configs
[Info   :Dirty Cash Economy] [LegalBusiness] Found 40 player-legal modules
[VERIFY-HOTFIX] [Compat] activeSubsystems dirtyCashDeferral=True safeboxExternal=True ...
[VERIFY-HOTFIX] [Compat] uiRetheme=delegated source=afterprohibition-ui-bridge bridgeAvailable=True ...
```

The key problem is not that Dirty Cash Economy is necessarily broken. The risk is that external DLL detection and compatibility behavior are mixed into the base gameplay mod, so unstable DLLs can affect unrelated economy, route, family, and UI work.

## Phase 1 - Standalone Plugin Scaffold

Prompt:

```txt
Create a new standalone BepInEx plugin project named AfterProhibitionCompatibility. Keep it separate from GameplayTweaks, AfterProhibitionAssets, and AfterProhibitionUI. Add a minimal plugin class, its own Harmony ID, logger, config section, and delayed compatibility log. Do not move compatibility behavior yet.

Validation:
- dotnet build AfterProhibitionCompatibility\AfterProhibitionCompatibility.csproj -c Release
- Confirm no dependency on GameplayTweaks.
- Confirm no gameplay, economy, route, family, politics, UI rendering, or inventory patches are included.
```

Expected files:

```txt
AfterProhibitionCompatibility/
  AfterProhibitionCompatibility.csproj
  AfterProhibitionCompatibilityPlugin.cs
```

Expected logs:

```txt
[After Prohibition Compatibility] compat baseline scheduled ownsGameplay=False
[After Prohibition Compatibility] loaded phase=scaffold
```

## Phase 2 - Compatibility Inventory

Prompt:

```txt
Inventory current external-DLL compatibility code in GameplayTweaks. Create a migration table listing the current source file, detected external DLL or plugin GUID, current log tag, risk, and whether each item should move now, later, or stay.

Validation:
- No code migration yet.
- Include Dirty Cash Economy, 10K Button & Input, safebox launchers, PIA UI Enhancer, CoGCustomAssets, CustomPortraits, ModLauncher/ProhibitionLauncher/TraplifeModLauncher, cheat/menu/sidebar mods, GangWars/Territory, and ticker/UI mods.
- Mark behavior-only gates separately from detection-only gates.
```

Suggested doc:

```txt
docs/ai/after-prohibition-compatibility-migration-inventory.md
```

Status:

```txt
Completed 2026-05-11. Inventory doc added at docs/ai/after-prohibition-compatibility-migration-inventory.md.
No code migration performed in this phase.
```

## Phase 3 - External DLL Detection Matrix

Prompt:

```txt
Move read-only external DLL and plugin GUID detection into AfterProhibitionCompatibility. Detection should scan loaded BepInEx plugin GUIDs, loaded assemblies, plugin DLL names, and selected ASCII tokens only when needed. Keep all results read-only and log a concise compatibility matrix once per session.

Validation:
- No hard compile dependency on external DLLs.
- No crash if an external DLL is absent or malformed.
- Logs clearly list DirtyCashEconomy, 10K Button/Input, safebox UI, CoGCustomAssets, PIA UI Enhancer, CustomPortraits, launcher DLLs, cheat/menu/sidebar mods, GangWars/Territory, and ticker mods.
```

Expected logs:

```txt
[After Prohibition Compatibility] external dirtyCash=True dirtyCashCore=True tenKInput=True safeboxUi=True ...
```

Status:

```txt
Completed 2026-05-11. AfterProhibitionCompatibility 0.2.0 now captures a read-only matrix from BepInEx plugin GUIDs, loaded assemblies, plugin DLL filenames, and targeted optional launcher/safebox ASCII probes.
No GameplayTweaks behavior gates were changed in this phase.
```

## Phase 4 - Reflection Bridge For Other Plugins

Prompt:

```txt
Add static reflection-safe query methods in AfterProhibitionCompatibility so GameplayTweaks and AfterProhibitionUI can ask whether external subsystems are active without duplicating detection.

Validation:
- GameplayTweaks can query the bridge without a compile-time dependency.
- AfterProhibitionUI can query the bridge without a compile-time dependency.
- Missing bridge falls back to existing local detection.
```

Bridge examples:

```txt
IsDirtyCashEconomyActive()
IsTenKInputActive()
ShouldUseExternalSafeboxUi()
HasExternalMenuOrSidebarOwner()
HasExternalUiEnhancer()
HasCustomAssets()
HasCustomPortraits()
HasExternalGangWarsOrTerritory()
```

Status:

```txt
Completed 2026-05-11. AfterProhibitionCompatibility 0.3.0 now exposes public static read-only bridge methods for Dirty Cash Economy, 10K input, safebox UI, external menu/sidebar/UI enhancer ownership, custom assets/portraits, GangWars/Territory, ticker, and cheat/menu detection.
GameplayTweaks and AfterProhibitionUI behavior gates were not changed in this phase.
```

## Phase 5 - Safe Broad-Unpatch Prevention

Prompt:

```txt
Move broad-unpatch safety classification into AfterProhibitionCompatibility. This should identify unsafe external patch owners and produce allow/deny decisions for broad unpatch attempts. Do not directly unpatch gameplay methods in this phase; expose decisions and logs only.

Validation:
- Logs explain why an external owner is protected or ignored.
- Existing dirty-cash route selections and default route selections remain visible.
- No external DLL method is invoked directly unless already loaded and reflection-checked.
```

Expected logs:

```txt
[After Prohibition Compatibility] broad-unpatch guard owner=DirtyCashEconomy action=protect reason=route-input
[After Prohibition Compatibility] broad-unpatch guard owner=10KButtonInput action=protect reason=button-input
```

Status:

```txt
Completed 2026-05-11. AfterProhibitionCompatibility 0.4.0 now owns read-only broad-unpatch owner classification and logs detected external owners as protect or ignore with a reason.
No GameplayTweaks unpatch behavior was changed in this phase.
```

## Phase 6 - GameplayTweaks Compatibility Delegation

Prompt:

```txt
Update GameplayTweaks compatibility checks to ask AfterProhibitionCompatibility first. Keep local GameplayTweaks detection as fallback when the compatibility plugin is missing or disabled. Move one detection family at a time.

Validation:
- GameplayTweaks builds.
- AfterProhibitionCompatibility builds.
- Live logs show delegated source when bridge is available.
- Live logs show local fallback when bridge is unavailable.
```

Suggested migration order:

1. external safebox UI detection;
2. Dirty Cash Economy detection;
3. 10K Button/Input detection;
4. external UI/menu/sidebar detection;
5. GangWars/Territory detection;
6. ticker/cheat/menu detection.

Expected logs:

```txt
[VERIFY-HOTFIX] [Compat] source=afterprohibition-compatibility dirtyCashExternal=True safeboxExternal=True ...
```

Status:

```txt
Started 2026-05-11. First migration slice delegates external safebox UI detection through the AfterProhibitionCompatibility bridge while preserving GameplayTweaks local detection as fallback.
Continued 2026-05-11. Dirty Cash Economy and Dirty Cash Volume Fix detection now also query the AfterProhibitionCompatibility bridge while preserving GameplayTweaks local detection as fallback.
Continued 2026-05-11. 10K Button/Input and UI Enhancer detection now query the AfterProhibitionCompatibility bridge while preserving GameplayTweaks local detection as fallback.
Continued 2026-05-11. External menu/sidebar detection is now logged through the AfterProhibitionCompatibility bridge, and ticker plus cheat/menu detection now query the bridge while preserving GameplayTweaks local detection as fallback.
Completed 2026-05-11. GangWars, TerritoryExpansion, and MafiaHierarchy detection now query the AfterProhibitionCompatibility bridge while preserving GameplayTweaks local detection as fallback.
```

## Phase 7 - Dirty Cash Economy Integration Pass

Prompt:

```txt
Move Dirty Cash Economy compatibility classification into AfterProhibitionCompatibility. Keep actual money, shop, route, and economy behavior in GameplayTweaks or a future AfterProhibitionEconomy plugin. This phase should only answer what external Dirty Cash DLLs are present, which patches they own, and what protections other plugins should apply.

Validation:
- Dirty Cash Economy logs still appear from the external DLL, but AfterProhibitionCompatibility logs a concise summary.
- GameplayTweaks no longer needs to scan Dirty Cash DLL names/tokens directly when the bridge is available.
- Route input options and dirty-cash selections are not removed by broad-unpatch cleanup.
```

Status:

```txt
Started 2026-05-11. AfterProhibitionCompatibility 0.5.0 now exposes read-only Dirty Cash compatibility classification, including route-input protection, original-method override protection, money-routing protection, and a Harmony owner summary for key Dirty Cash method targets.
No GameplayTweaks money, shop, route, economy, or broad-unpatch behavior was changed in this phase.
```

## Phase 8 - 10K Button/Input Integration Pass

Prompt:

```txt
Move 10K Button & Input compatibility classification into AfterProhibitionCompatibility. Detect the DLL, assembly, GUID if present, and likely route/button-input ownership. Expose this to GameplayTweaks so input and route selection cleanup can avoid removing default or external selections.

Validation:
- The detection works when the DLL is present or absent.
- Logs are concise and not spammed every frame.
- GameplayTweaks can distinguish external input ownership from its own route UI patches.
```

Status:

```txt
Started 2026-05-11. AfterProhibitionCompatibility 0.6.0 now exposes read-only 10K Button/Input compatibility classification, including route-input protection, button-input protection, menu/sidebar protection, and owner summary bridge methods.
No GameplayTweaks route UI, input cleanup, menu, or broad-unpatch behavior was changed in this phase.
```

## Phase 9 - Packaging And Guide

Prompt:

```txt
Add release packaging for AfterProhibitionCompatibility. Stage the DLL into Personal/Public release plugin folders and add a plain-text guide describing what the plugin owns, what it explicitly does not own, and how to verify external DLL compatibility logs.

Validation:
- Build and stage DLL.
- Confirm guide is plain text.
- Confirm live logs clearly distinguish AfterProhibitionCompatibility from GameplayTweaks and AfterProhibitionUI.
```

Expected release files:

```txt
BepInEx/plugins/AfterProhibitionCompatibility.dll
AFTER_PROHIBITION_COMPATIBILITY_GUIDE.txt
```

Status:

```txt
Completed 2026-05-11. Added AFTER_PROHIBITION_COMPATIBILITY_GUIDE.txt with plugin ownership, non-ownership boundaries, config switches, expected logs, bridge methods, and live validation checklist.
The guide is staged with the Personal/Public release folders.
```

## Phase 10 - Cleanup And Fallback Hardening

Prompt:

```txt
After AfterProhibitionCompatibility owns a detection family, simplify GameplayTweaks to use the bridge and keep only minimal fallback detection. Do not remove fallback until live validation proves the bridge works across missing, present, and malformed external DLL cases.

Validation:
- No duplicate compatibility matrix spam.
- GameplayTweaks still builds.
- AfterProhibitionCompatibility still builds.
- Live logs show delegated detection when available and local fallback when disabled.
```

Status:

```txt
Started 2026-05-11. GameplayTweaks now caches the AfterProhibitionCompatibility plugin type and bridge version once per compatibility scan, logs the compatibility bridge version in the matrix, and keeps all local fallback detection paths intact.
No fallback removal or behavior ownership change was made in this pass.
```

## Migration Rules

- Move detection before behavior.
- Keep external DLL calls reflection-only and optional.
- Do not move economy behavior into this plugin.
- Do not move UI rendering into this plugin.
- Do not broad-unpatch external owners from this plugin; classify and protect first.
- Keep local fallback checks until the bridge is live-tested.
- Log once per session unless a state changes.
- Prefer plugin GUID and loaded assembly checks before scanning file bytes.

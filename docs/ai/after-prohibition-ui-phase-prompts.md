# AfterProhibitionUI Phase Prompts

Use this plan to split UI-only work out of `GameplayTweaks` and into a separate BepInEx plugin named `AfterProhibitionUI`. The goal is to isolate visual, popup, portrait, sidebar, and retheme patches from economy, route, family, politics, and AI logic.

## Goal

Create a standalone `AfterProhibitionUI` plugin that can own:

- popup docking and dialog placement;
- retheme work;
- crew info buttons;
- blank/stale portrait fixes;
- menu/sidebar visual patches;
- compatibility with external UI mods such as PIA UI Enhancer and CoGCustomAssets.

This plugin must not own:

- economy stock or buy/sell behavior;
- route authority or turn continuation;
- family, spouse, or relation sorting logic;
- politics quests;
- AI hiring, war, territory, or retaliation logic.

## Current Log Signals

Recent live logs show these UI-related anchors:

```txt
[VERIFY-HOTFIX] [CrewInfoButtons] inspect patch methods=2
[VERIFY-HOTFIX] [UISanitize] TMP replacement-character sanitizer patched=3
[VERIFY-HOTFIX] [AggroUI] crew-pick refresh stability patch applied
[VERIFY-HOTFIX] [Jail] crew sidebar jail-bars patch applied
[VERIFY-HOTFIX] [PactColorUI] patch applied text=True crewPick=True
[VERIFY-HOTFIX] [Compat] uiRetheme=active source=gameplaytweaks-owned-roots externalEnhancer=True
[GameplayTweaks] External safebox UI active - crew management safebox button disabled to prevent duplicates.
```

These are good candidates to audit before moving anything out of `GameplayTweaks`.

## Phase 1 - Standalone Plugin Scaffold

Prompt:

```txt
Create a new standalone BepInEx plugin project named AfterProhibitionUI. Keep it separate from GameplayTweaks and AfterProhibitionAssets. Add a minimal plugin class, its own Harmony ID, logger, compatibility log header, and targeted Release build. Do not migrate UI patches yet.

Validation:
- dotnet build AfterProhibitionUI\AfterProhibitionUI.csproj -c Release
- Confirm no dependency on GameplayTweaks.
- Confirm no gameplay/economy/route/family patches are included.
```

Expected files:

```txt
AfterProhibitionUI/
  AfterProhibitionUI.csproj
  AfterProhibitionUIPlugin.cs
```

## Phase 2 - UI Surface Inventory

Prompt:

```txt
Inventory current UI patches in GameplayTweaks and classify each one by surface: popup, crew card, crew pick, portrait, retheme, sidebar, menu, mouseover, or compatibility. Create a migration table that records current class/file, Harmony target, log tag, risk, and whether the patch should move now, later, or stay.

Validation:
- No code migration yet.
- Table includes every UI log tag currently emitted by GameplayTweaks.
- Each candidate has a target owner class from decompiled Game.UI or Game.UI.Session.
```

Suggested doc:

```txt
docs/ai/after-prohibition-ui-migration-inventory.md
```

## Phase 3 - Compatibility Baseline

Prompt:

```txt
Add soft detection in AfterProhibitionUI for external UI mods: PIA UI Enhancer, CoGCustomAssets, CustomPortraits, safebox UI, and menu/sidebar patches. Log a concise compatibility matrix. Do not block or disable anything yet.

Validation:
- No hard compile dependency on external DLLs.
- No crash when external DLLs are absent.
- Logs show detected/absent states once per session.
```

Expected logs:

```txt
[AfterProhibitionUI] compat externalEnhancer=True cogCustomAssets=True customPortraits=True safeboxUi=True
```

## Phase 4 - Popup Docking

Prompt:

```txt
Move or implement popup docking in AfterProhibitionUI. Target only popup/dialog placement, size, visibility, and z-order behavior. Avoid changing command validation or gameplay results. Add guards for missing UI paths and external overlay mods.

Validation:
- Build AfterProhibitionUI.
- Open common popups and confirm no overlap with sidebars/menus.
- Confirm popup close/open lifecycle does not leave stale GameObjects active.
```

Likely surfaces:

```txt
Game.Services.BasePopup
Game.UI.Session.Deliveries.DeliveriesDialog
Game.UI.Session.Crew.CrewManagementPopup
Game.UI.Session.PersonInfoDialog
Game.UI.Session.Convo.ConversationDialog
```

## Phase 5 - Retheme Work

Prompt:

```txt
Move retheme-only patches into AfterProhibitionUI. Keep this limited to colors, labels, text sanitization, button backgrounds, and visual hierarchy. Do not move logic that changes economy, routes, command availability, or simulation state.

Validation:
- Build AfterProhibitionUI.
- Confirm existing retheme logs move from GameplayTweaks to AfterProhibitionUI.
- Confirm external UI enhancer detection remains visible in logs.
```

Candidate existing logs:

```txt
[UISanitize]
[PactColorUI]
[Compat] uiRetheme=active
```

## Phase 6 - Crew Info Buttons

Prompt:

```txt
Move crew info button UI patches into AfterProhibitionUI. Keep button creation, labels, click routing, and duplicate prevention in this plugin. Keep underlying gameplay actions in their owning gameplay plugins or vanilla callbacks.

Validation:
- Inspect crew info dialog, crew card, and crew management popup.
- Confirm duplicate buttons are not created when external safebox UI is active.
- Confirm missing external features hide or disable buttons safely.
```

Candidate existing log:

```txt
[CrewInfoButtons] inspect patch methods=2
```

## Phase 7 - Blank/Stale Portrait Fixes

Prompt:

```txt
Move blank and stale portrait fixes into AfterProhibitionUI. Target UI state and pooling only: clear stale sprites on reset/deactivate, reconcile target before refresh, and guard missing portrait assets. Do not change peep data, family data, crew assignment, or portrait generation rules.

Validation:
- Check crew picks, person info, conversation owner portrait, portrait popup, and crew inspect popup.
- Confirm hidden/reused cards do not show a previous person's portrait.
- Confirm missing portrait assets fall back to vanilla or blank safely.
```

Likely surfaces:

```txt
Game.UI.Session.Picks.CrewPick -> Button/Portrait
Game.UI.Session.Convo.ConversationDialog -> Owner/Portrait/Portrait
Game.UI.Session.PersonInfoDialog -> Owner/Portrait/Portrait
Game.UI.CrewPeepInspectPopup -> Character/Biography/Mugshot/Portrait/Portrait
Game.UI.Session.Popups.PortraitPopup -> Panel/Portrait/Portrait
```

## Phase 8 - Menu and Sidebar Visual Patches

Prompt:

```txt
Move menu/sidebar visual patches into AfterProhibitionUI. Include sidebar icon state, jail bars, crew pick visual labels, and menu/sidebar polish. Do not move route authority, command execution, or vehicle assignment logic even if the UI element displays route/vehicle state.

Validation:
- Confirm crew sidebar visuals refresh after jail, vehicle, route, death, and selection changes.
- Confirm no stale route/vehicle visual labels remain after route clear or turn transition.
- Confirm menu/sidebar patches do not fight CoGCustomAssets or PIA UI Enhancer.
```

Candidate existing logs:

```txt
[Jail] crew sidebar jail-bars patch applied
[AggroUI] crew-pick refresh stability patch applied
```

## Phase 9 - Packaging and Live Test Guide

Prompt:

```txt
Add release packaging for AfterProhibitionUI. Stage the DLL into Personal/Public release plugin folders and add a plain-text guide describing what this plugin owns, what it explicitly does not own, and how to test popup docking, crew buttons, portraits, and sidebar visuals.

Validation:
- Build and stage DLL.
- Confirm guide is plain text.
- Confirm live logs clearly distinguish AfterProhibitionUI from GameplayTweaks.
```

Expected release files:

```txt
BepInEx/plugins/AfterProhibitionUI.dll
AFTER_PROHIBITION_UI_GUIDE.txt
```

## Phase 10 - GameplayTweaks Cleanup

Prompt:

```txt
After AfterProhibitionUI owns and validates a UI patch, remove or disable the old GameplayTweaks copy so duplicate Harmony patches do not run. Move one patch family at a time and keep a migration ledger.

Validation:
- No duplicate logs for moved patch tags.
- GameplayTweaks still builds.
- AfterProhibitionUI still builds.
- Live log shows moved UI tag only from AfterProhibitionUI.
```

## Migration Rules

- Move one UI patch family per pass.
- Do not migrate gameplay behavior just because a UI patch calls into it.
- Do not introduce a dependency from AfterProhibitionUI to GameplayTweaks.
- Prefer reflection or soft detection for external UI mods.
- Missing UI paths must warn and skip, not crash.
- Duplicate external buttons must be hidden or skipped, not layered.
- Portrait fixes must clear stale render state without mutating peep/family data.
- Retheme patches must be reversible by disabling the plugin.

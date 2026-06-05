# AfterProhibitionCompatibility Stabilization Plan

This plan keeps `AfterProhibitionCompatibility` read-only. It detects external plugin/DLL presence, classifies overlap risk, and exposes reflection-safe bridge answers for other After Prohibition plugins. It must not move shop, economy, route, family, UI rendering, territory mutation, or politics behavior into the compatibility plugin.

## Phase 1 - All External DLL Detection

Status: completed in `AfterProhibitionCompatibility` `0.7.0`. Added a complete plugin-DLL inventory/audit line so live logs show which plugin DLLs were seen, which are known external compatibility owners, which are unknown third-party DLLs, and which belong to the local After Prohibition/mod suite. The existing concise matrix remains intact.

Scope:

- Capture every DLL under the BepInEx plugin folder when DLL scanning is enabled.
- Classify After Prohibition-owned DLLs, known external compatibility DLLs, and unknown external DLLs.
- Keep output bounded and non-crashing when plugin folders are missing or inaccessible.
- Expose the inventory through the existing bridge summary so GameplayTweaks/UI can diagnose missing or unexpected external owners without rescanning.

Validation:

- `AfterProhibitionCompatibility` builds.
- Live logs include `external-dll-audit`.
- No external DLL method is invoked directly.

## Phase 2 - Dirty Cash DLL Checks

Status: completed in `AfterProhibitionCompatibility` `0.7.1`. Dirty Cash classification now distinguishes DLL/GUID presence, Dirty Cash Volume Fix presence, active Harmony owner patches, and the protection decisions driven by those signals.

Scope:

- Tighten Dirty Cash Economy and Dirty Cash Volume Fix detection across GUID, assembly, DLL filename, and Harmony owner signals.
- Keep Dirty Cash shop/economy behavior in `GameplayTweaks` or `AfterProhibitionEconomy`.
- Add clearer logs distinguishing external DLL present, external patches active, and After Prohibition fallback protections.

Validation:

- Dirty Cash classifier logs are concise and include patch-owner counts.
- GameplayTweaks can query the bridge and still use local fallback if the compatibility plugin is absent.

## Phase 3 - 10K Input And UI Enhancer Checks

Status: completed in `AfterProhibitionCompatibility` `0.7.2`. 10K/UI classification now distinguishes direct 10K DLL/token signals, direct PIA UI Enhancer GUID/DLL signals, menu/sidebar token signals, launcher contribution, and the route/button/menu protection reasons driven by those signals.

Scope:

- Expand 10K Button/Input and PIA UI Enhancer detection using GUID, assembly, DLL filename, and owner strings.
- Separate route/button input ownership from menu/sidebar ownership.
- Keep input cleanup and UI behavior outside the compatibility plugin.

Validation:

- Logs distinguish `tenKInput`, `uiEnhancer`, `menuSidebar`, and protection reasons.
- GameplayTweaks route/input cleanup has a bridge answer and local fallback.

## Phase 4 - Election Cheat Checks

Status: completed in `AfterProhibitionCompatibility` `0.7.3`. Added read-only election/core cheat/boss-manager classification that distinguishes core cheat menu presence from election-state and boss-state blockers, with bridge methods for election-specific and boss-specific protection checks.

Scope:

- Harden detection for election cheat, boss manager, core cheat/menu, and related politics/boss authority modifiers.
- Add bridge methods that separate election-state risk from generic cheat/menu risk.
- Keep politics state, election behavior, boss behavior, and blocking decisions in their owning gameplay plugins.

Validation:

- Logs distinguish election cheat from boss/core cheat.
- Gameplay/politics owners can query election-specific risk without parsing generic menu flags.

## Phase 5 - Territory And Gang-War External Mod Checks

Status: completed in `AfterProhibitionCompatibility` `0.7.4`. Added read-only territory/gang-war/ticker classification that separates GangWars behavior authority, TerritoryExpansion/MafiaHierarchy visual risk, and ticker/outpost UI ownership, with bridge methods for each protection surface.

Scope:

- Harden detection for GangWars, TerritoryExpansion, MafiaHierarchy, ticker/outpost UI, and related territory visual owners.
- Add a territory/gang-war classifier that separates read-only detection from behavior authority.
- Keep pact, territory, colors, retaliation, tribute, and AI behavior in GameplayTweaks or future dedicated owners.

Validation:

- Logs show territory/gang-war external ownership and risk category.
- No territory or gang-war behavior is moved into `AfterProhibitionCompatibility`.

## Phase 6 - Packaging And Live Validation

Scope:

- Build and stage `AfterProhibitionCompatibility.dll` into active root, Personal, and Public packages only.
- Leave Past Build unchanged unless explicitly requested.
- Update release notes with compatibility ownership and live log markers.

Validation:

- Active root, Personal, and Public DLL hashes match the built DLL.
- Live logs show `external-dll-audit` plus each enabled classifier.

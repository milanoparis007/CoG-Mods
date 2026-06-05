# AfterProhibitionUI Stabilization Plan

This plan moves and hardens UI-only behavior in `AfterProhibitionUI` without taking ownership of gameplay, route authority, economy, family, politics, AI, inventory, or command execution.

## Phase 1 - CrewPick Stale/Blank Portraits And Warning Noise

Status: implemented in `AfterProhibitionUI` `0.2.3`; live log tail no longer showed the missing `CrewPick.go` property warning after the patch loaded.

Scope:

- Fix pooled `CrewPick` visual cleanup for stale or blank portraits.
- Clear reused CrewPick text, portrait, warning, bar, and state icon before target/refresh transitions.
- Suppress repeated invalid resolver warnings caused by looking for non-existent UI members.
- Keep vehicle/route reconciliation in `GameplayTweaks`.

Validation:

- `dotnet build AfterProhibitionUI/AfterProhibitionUI.csproj -c Release`
- Live logs should no longer repeat `AccessTools.Property: Could not find property for type Game.UI.Session.Picks.CrewPick and name go`.
- Crew picks should not retain prior portrait/text/warning state after reuse.

## Phase 2 - Crew HUD Refresh And Rebuild

Status: started in `AfterProhibitionUI` `0.2.4` with a coalesced crew HUD refresh bridge. `GameplayTweaks` now delegates visual-only refresh/rebuild requests to the bridge when present and keeps its direct refresh fallback.

Scope:

- Inventory all crew HUD/sidebar refresh entry points now split between `GameplayTweaks` and `AfterProhibitionUI`.
- Move only visual rebuild/refresh guards into `AfterProhibitionUI`.
- Keep route labels, vehicle assignment truth, jail command blocking, and crew state mutations in their gameplay owners.

Validation:

- Sidebar refreshes after selection, turn transition, vehicle assignment, jail/federal custody, route clear, and death.
- No duplicate crew HUD rebuild patches run from both plugins.

## Phase 3 - Aggro UI Refresh

Status: started in `AfterProhibitionUI` `0.2.5` with a crew-pick aggro refresh bridge. `AfterProhibitionUI` owns dirty/flush scheduling and diagnostics; `GameplayTweaks` keeps the vehicle/custody reconciliation callback used to execute each refresh.

Scope:

- Move aggro visual refresh stability into `AfterProhibitionUI`.
- Keep combat permission, hostility, retaliation, and attack eligibility logic in gameplay plugins.
- Add deduped `AggroUI` diagnostics owned by `AfterProhibitionUI`.

Validation:

- Hostile crew picks update warning borders after aggro changes.
- No repeated stale warning borders after aggro clears.

## Phase 4 - Crew Relation Buttons

Status: started in `AfterProhibitionUI` `0.2.6`. The crew inspect footer now removes legacy duplicate action rows, hides migrated buttons when the GameplayTweaks action bridge is unavailable, and clamps the managed button row width before reordering controls.

Scope:

- Keep button rendering, duplicate prevention, layout, and visibility in `AfterProhibitionUI`.
- Continue routing gameplay actions through reflection bridges to the owning gameplay systems.
- Hide or disable missing-action buttons without creating duplicates.

Validation:

- Crew inspect/info dialogs show expected relation buttons once.
- Missing gameplay owners do not crash UI.

## Phase 5 - Popup Docking

Status: started in `AfterProhibitionUI` `0.2.7`. Popup docking now coalesces duplicate dock requests, clears queued state when a popup deactivates, and centers oversized modal panels when they cannot fully fit inside the parent viewport. The same build also reduces no-op AggroUI bridge log spam from startup cop/fed refreshes.

Scope:

- Harden modal popup docking, placement, and teardown.
- Keep docking independent of command validation and gameplay results.
- Exclude side-panel HUD dialogs unless they are explicitly targeted.

Validation:

- Common popups remain inside viewport and do not overlap sidebars incoherently.
- Hide/show cycles do not leave active stale children.

## Phase 6 - Retheme, Menu, And Sidebar Visual Patches

Status: started in `AfterProhibitionUI` `0.2.8`. The retheme bridge now skips portrait, preview, map, sprite, logo, stamp, deco, pattern, and likely asset-backed images while still owning panel/button retheme requests delegated by GameplayTweaks. It also applies TMP button text color and dedupes retheme diagnostics by root/signature.

Scope:

- Move visual-only retheme/menu/sidebar work into `AfterProhibitionUI`.
- Keep pact color semantics, route labels, and gameplay-coupled states in their owning systems until they can expose read-only UI bridges.
- Respect external UI enhancer and custom asset detection.

Validation:

- Startup compatibility log clearly shows external UI/asset mods.
- Retheme/menu/sidebar logs emit from `AfterProhibitionUI` only after migration.

## Phase 7 - GameplayTweaks Cleanup

Status: started. `GameplayTweaks` keeps the crew-pick aggro stability patch as the execution/fallback surface because it still owns vehicle, custody, and hostile-presentation reconciliation. Its startup diagnostic now reports `scheduler=AfterProhibitionUI fallback=GameplayTweaks` when the AfterProhibitionUI bridge is available, instead of implying GameplayTweaks still owns the visible aggro refresh scheduler.

Scope:

- Remove or disable one migrated UI patch family at a time from `GameplayTweaks`.
- Keep bridge methods until all downstream calls are moved or retired.

Validation:

- No duplicate Harmony patches or duplicate log tags for moved surfaces.
- `GameplayTweaks` and `AfterProhibitionUI` both build.

## Phase 8 - Packaging And Live Test Notes

Status: started. `AfterProhibitionUI.dll` and the rebuilt `GameplayTweaks.dll` are staged in `Things To Have\Current After Prohibition Mod\BepInEx\plugins`, and the public changelog/guide now document the UI ownership split and expected live log markers.

Scope:

- Stage `AfterProhibitionUI.dll` into the intended release package only.
- Update release notes with ownership boundaries and live log markers.

Validation:

- Public package contains the expected DLL.
- Live logs distinguish `AfterProhibitionUI` UI ownership from `GameplayTweaks` gameplay ownership.

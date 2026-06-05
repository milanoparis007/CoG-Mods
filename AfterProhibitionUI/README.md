# AfterProhibitionUI

Standalone BepInEx plugin for After Prohibition UI-only patches.

Phase 1 provides the plugin scaffold and a separate Harmony ID. Phase 3 adds delayed compatibility baseline logging for external UI and asset plugins after BepInEx finishes loading the plugin list.

The first migrated UI patch is the TMP replacement-character sanitizer, which removes broken replacement glyphs from TMP text setters.

The second migrated UI patch is the crew sidebar jail-bars visual patch. It only toggles the existing sidebar bars object and reads custody state through a soft bridge or vanilla fallback.

Phase 4 adds the popup docking foundation. It patches modal base popup activation and keeps visible panels inside their parent viewport without changing command validation or gameplay results. It intentionally does not reposition `BaseHUDDialog` side panels such as the crew and person-info dialogs.

Runtime config is written under BepInEx config for:

```txt
Features.EnableTextSanitizer
Features.EnableCrewSidebarJailBars
Features.EnablePopupDocking
Features.EnableMenuRethemeBridge
Features.EnableCrewInfoButtons
Features.EnableStalePortraitGuards
Features.EnableCrewManagementJailVisuals
Features.EnableCrewHudRefreshBridge
Features.EnableAggroUiRefreshBridge
```

The menu retheme bridge owns generic menu/popup button and panel retheme requests delegated by GameplayTweaks. GameplayTweaks still owns gameplay-coupled UI such as pact color refreshes and crew/action button logic.

The crew-info action bridge is reflection-only. It lets future AfterProhibitionUI button rendering call GameplayTweaks-owned actions for crew relations, pacts, grapevine, and safebox without a compile-time dependency.

Crew inspect footer button rendering now lives in AfterProhibitionUI. The buttons still delegate their gameplay actions back into GameplayTweaks through the reflection bridge. Vehicle-specific indicators and richer icon styling remain in GameplayTweaks/future UI work.

Stale portrait guards now clear reused portrait widgets and suppress invalid connections-tab cards in AfterProhibitionUI. The guard does not mutate relationship or family data. Vehicle/route crew-pick reconciliation remains in GameplayTweaks.

Version `0.2.3` fixes the CrewPick visual resolver to use the vanilla `BasePick.go` field instead of a missing `CrewPick.go` property. It also clears pooled CrewPick portrait/text/warning visuals before `RefreshContents`, reducing stale portraits and repeated warning-border carryover.

Version `0.2.4` adds a crew HUD refresh bridge. GameplayTweaks can now delegate visual-only crew sidebar refresh/rebuild requests to AfterProhibitionUI, where requests are coalesced to the next frame before repainting.

Version `0.2.5` adds the crew-pick aggro refresh bridge. AfterProhibitionUI now owns dirty/flush scheduling while delegating GameplayTweaks-owned vehicle and custody reconciliation through a narrow reflection callback.

Version `0.2.6` hardens crew inspect relation buttons by clearing legacy duplicate action rows, hiding buttons whose gameplay bridge is unavailable, and stabilizing footer row width before ordering controls.

Version `0.2.7` reduces no-op AggroUI bridge logging and hardens popup docking by coalescing duplicate dock requests, clearing queued state on popup deactivation, and centering oversized modal panels when they cannot fit inside the parent viewport.

Version `0.2.8` hardens the retheme bridge by skipping portrait, preview, map, sprite, logo, stamp, deco, pattern, and likely asset-backed images while keeping panel/button retheme ownership in AfterProhibitionUI. It also colors TMP button text consistently and dedupes retheme logs per root/signature.

Crew-management jail status text now lives in AfterProhibitionUI. GameplayTweaks still owns jailed crew assignment blocking and command safety.

This project intentionally has no compile-time dependency on `GameplayTweaks`.

Ownership:

```txt
Owns: popup docking, retheme work, crew info buttons, stale portrait UI fixes, menu/sidebar visual patches.
Does not own: economy, routes, family, politics, AI, inventory, buy/sell, or simulation behavior.
```

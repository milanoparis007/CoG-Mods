# AfterProhibitionUI Migration Inventory

This inventory records UI-only patch families that are still owned by `GameplayTweaks`. Move one family at a time into `AfterProhibitionUI`, then disable or remove the old `GameplayTweaks` copy after live validation.

## Live Log Snapshot

Recent `Player.log` signals:

```txt
[After Prohibition UI] After Prohibition UI 0.1.0 loaded. uiPatches=0 phase=scaffold
[VERIFY-HOTFIX] [CrewInfoButtons] inspect patch methods=2
[VERIFY-HOTFIX] [UISanitize] TMP replacement-character sanitizer patched=3
[VERIFY-HOTFIX] [AggroUI] crew-pick refresh stability patch applied
[VERIFY-HOTFIX] [Jail] crew sidebar jail-bars patch applied
[VERIFY-HOTFIX] [PactColorUI] patch applied text=True crewPick=True
[VERIFY-HOTFIX] [Compat] uiRetheme=active source=gameplaytweaks-owned-roots externalEnhancer=True
[GameplayTweaks] External safebox UI active - crew management safebox button disabled to prevent duplicates.
```

## Migration Table

| Candidate / log tag | Current source | Target owner class or Harmony target | Surface | Risk | Decision | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `UISanitize` | `AfterProhibitionUI/TmpReplacementCharacterSanitizerPatch.cs`; legacy source remains in `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs` as fallback | `TMP_Text.text`, `TextMeshProUGUI.text`, `TextMeshPro.text` setters | retheme, text sanitation | Low | Moved | Pure display cleanup. `GameplayTweaks` now skips its old copy only when the AfterProhibitionUI text sanitizer feature is enabled. |
| `Jail` | `AfterProhibitionUI/CrewSidebarJailBarsPatch.cs`; legacy source remains in `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs` as fallback | `Game.UI.Session.Crew.CrewCardContext.RefreshPanel` | sidebar, crew card | Low-medium | Moved | Visual-only jail bars toggle. `AfterProhibitionUI` reads custody state through a soft `GameplayTweaks` bridge or vanilla CopTracker fallback. `GameplayTweaks` now skips its old copy only when the AfterProhibitionUI sidebar jail-bars feature is enabled. |
| `PopupDocking` | `AfterProhibitionUI/PopupDockingPatch.cs` | `Game.Services.BasePopup.OnActivated` | popup placement | Medium | Foundation added | Clamps active modal popup panels inside their parent viewport after activation and settle. The broad `BaseHUDDialog` hook was removed after it moved side panels such as `CrewDialog`; HUD dialogs need targeted handling only. Does not move gameplay-coupled combat popup logic out of `GameplayTweaks`. |
| `Feature config` | `AfterProhibitionUI/AfterProhibitionUIPlugin.cs` | BepInEx config entries under `Features` | compatibility, operations | Low | Added | Adds runtime switches for text sanitizer, sidebar jail bars, popup docking, and the menu retheme bridge. The delayed compat line reports feature states and retheme bridge ownership. |
| `PactColorUI` | `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs` / `PactColorUiPatch` | `Game.Session.Board.MapDisplayManager.GetColorForPlayerText`, `Game.UI.Session.Picks.CrewPick.RefreshContents` | crew pick, text color, retheme | Medium-high | Move later | Looks visual, but relies on pact/alliance state and pending refresh queues. Keep in `GameplayTweaks` until pact color contracts are separated. |
| `AggroUI` | `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs` / `CrewPickAggroRefreshStabilityPatch`; helpers in `GameplayTweaksPlugin.cs` | `Game.UI.Session.Picks.PickManager.RefreshAllPlayerCrewPicks`, `PickContainer.AddOrRefreshPick`, `CrewPick.Reset` | crew pick, pooling, stale portrait/state | High | Move later | Fixes stale/police/enemy vehicle crew picks and calls route/arrest/vehicle helpers. Separate the pure pooling clear first, leave route authority hooks in `GameplayTweaks`. |
| `StalePortraitGuards` | `AfterProhibitionUI/StalePortraitAndConnectionGuardPatch.cs`; route-coupled `AggroUI` remains in `GameplayTweaks` | `CrewPick.Reset`, `CrewPick.SetTarget`, person/conversation/portrait popup refresh and hide hooks | portrait, pooled UI state | Medium | Moved foundation | Clears reused portrait/text UI state without changing peep, family, route, or vehicle data. Crew-pick route/vehicle reconciliation remains in GameplayTweaks. |
| `ConnectionsTab` render guard | `AfterProhibitionUI/StalePortraitAndConnectionGuardPatch.cs`; legacy source remains in `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs` as fallback | `Game.UI.Session.ConnectionsTabSubview.CreateFamilyLinks`, `RefreshAllCards`, `RefreshSubview`, `InitializeCard` | person info, connections cards | Medium | Moved UI guard | Filters invalid card render data and suppresses blank cards without pruning relationship lists. GameplayTweaks now skips its old connections-tab null/data-prune patch when the AfterProhibitionUI stale portrait guard is enabled. |
| `CrewInfoButtons` | `AfterProhibitionUI/CrewInfoButtonsPatch.cs`; fallback remains in `GameplayTweaks/GameplayTweaksPlugin.cs` / `CrewPeepInspectModButtonsPatch` | `Game.UI.CrewPeepInspectPopup.InitializeOnPush`, `RefreshDialog` | popup, crew info buttons | High | Moved foundation | Button rendering now lives in `AfterProhibitionUI` and actions call back into GameplayTweaks through the reflection bridge. The first moved version uses simple text labels and does not carry over vehicle-indicator UI yet. GameplayTweaks skips its old copy when the new UI patch type is present. |
| `CrewInfoActionBridge` | `AfterProhibitionUI/CrewInfoActionBridge.cs` | Reflection-only calls into `GameplayTweaks.GameplayTweaksPlugin` external UI methods | crew info buttons, action bridge | Medium | Added | Exposes `OpenCrewRelations`, `OpenGangPacts`, `OpenMyPact`, `OpenGrapevine`, `OpenSafebox`, eligibility checks, and crew-menu close checks without a compile-time dependency on `GameplayTweaks`. |
| `Compat uiRetheme` | `AfterProhibitionUI/AfterProhibitionUIPlugin.cs`; `GameplayTweaks/GameplayTweaksPlugin.Compat.cs` reports delegated state | BepInEx plugin/file detection; menu root retheme calls | compatibility, retheme | Medium | Partly moved | `AfterProhibitionUI` delayed compat logging now reports `rethemeOwner=AfterProhibitionUIBridge` when the bridge is enabled. GameplayTweaks compat logs report `uiRetheme=delegated`. |
| `UITheme` | `AfterProhibitionUI/UiRethemeBridge.cs`; fallback remains in `GameplayTweaks/GameplayTweaksPlugin.cs` | Menu/popup root hierarchies passed by GameplayTweaks UI builders | retheme, menu, popup | Medium | Bridge moved | `GameplayTweaks.RethemeMenuHierarchy(root)` now delegates generic button/panel theming to `AfterProhibitionUI.UiRethemeBridge` first, with fallback to old local theming if the UI plugin or bridge is unavailable. Pact colors and gameplay-coupled UI remain in GameplayTweaks. |
| External safebox button suppression | `GameplayTweaks/GameplayTweaksPlugin.Compat.cs`; `CrewPeepInspectModButtonsPatch.EnsureButtons` | Safebox button inside `Game.UI.CrewPeepInspectPopup` footer | compatibility, crew info buttons | Medium | Stay until CrewInfoButtons move | Detection can move now, but the behavior is tied to CrewInfoButtons. Do not duplicate button suppression in both plugins. |
| Driver/vehicle crew pick portrait patches | `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.cs`, `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.CrewUi.cs` | `Game.UI.Session.Picks.CrewPick.SetTarget`, `RefreshContents`; crew management popup/dialog methods | portrait, crew pick, vehicle UI | High | Move later | User-facing UI bug area, but strongly coupled to multi-crew vehicle authority. Split stale portrait clearing from vehicle assignment logic before migration. |
| Crew-management jail text visuals | `AfterProhibitionUI/CrewManagementJailVisualPatch.cs`; gameplay blocking remains in `GameplayTweaks/GameplayTweaksPlugin.MultiCrewVehicle.Interaction.cs` | `CrewManagementPopup.SetCardDesc`, `CrewManagementPopup.GetArrestedDesc`, `CrewInfoGen.DescribeCrewPeep` | crew management popup, crew info list | Medium | Moved visual slice | AfterProhibitionUI owns jail status text display through the reflection bridge. GameplayTweaks still owns jailed crew unavailable state, assignment blocking, button disabling, and route/vehicle safety. |
| `SelectionManagerRightClickGuardPatch` | `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs` | `Game.Session.Board.SelectionManager.HandleClosePopupOrDeselect` | selection, popup close behavior | Medium | Move later | UI behavior, but closes GameplayTweaks-owned external menus. Needs a menu registry before migration. |

## First Safe Move Order

1. Compatibility baseline in `AfterProhibitionUI`.
2. `UISanitize`.
3. `CrewSidebarJailBarsPatch`, after adding a local read-only jail resolver.
4. Popup docking foundation.
5. Retheme helper extraction for menu/popup roots.
6. Crew info buttons with an action bridge.
7. Portrait/pooling fixes split away from vehicle and route authority.

## Stay Out Of Scope

Do not move economy buy/sell, route continuation, family/spouse sorting, political quests, AI hiring, territory, retaliation, or pact simulation behavior into `AfterProhibitionUI`.

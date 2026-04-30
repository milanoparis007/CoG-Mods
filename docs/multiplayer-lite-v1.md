# Multiplayer-Lite V1

Implementation-ready outline for a local hotseat mode in City of Gangsters using the existing BepInEx/Harmony mod stack.

This document is intentionally narrow. It does not try to create true multiplayer, networking, or parity with the single-player campaign. The goal is a first playable local mode that keeps the board-game feel alive while avoiding the deepest `PlayerID.HumanPlayer` assumptions.

## Summary

V1 is a `local hotseat gang mode`.

- Local only, one machine, one save, no networking.
- One canonical `HumanPlayer` remains in the engine.
- Additional local players are mapped onto existing non-human gang slots.
- Each participating local player takes a full turn when their gang's turn comes up.
- The mode keeps economy, crew control, territory, combat, and a reduced interaction set.
- The mode disables or simplifies systems that are heavily hardcoded to `PlayerID.HumanPlayer`.

This is the most realistic path that still feels like multiple gangs taking turns around a board.

## Core Decision

Do not attempt to create a second true `HumanPlayer`.

The game hardcodes `PlayerID.HumanPlayer` in too many places to make that practical:

- `decompiled/Game.Core/PlayerID.cs`
- `decompiled/Game.Session.Player/AllPlayersManager.cs`
- `decompiled/Game.UI.Session/BaseHUDDialog.cs`
- `decompiled/Game.UI/SchemePopup.cs`
- `decompiled/Game.UI/LawPopup.cs`
- `decompiled/Game.Session/SessionContext.cs`

Instead:

- Keep the engine's real human player as-is.
- Add a mod-level concept of `ActiveLocalPlayer`.
- Treat selected gang slots as `locally controlled` during their turns.
- Patch only the interaction surfaces needed for those turns.

## Product Shape

The intended user flow is:

1. Start a special `Multiplayer-Lite` new game or save mode.
2. Choose `2-4` local players.
3. Assign each local player to one gang slot.
4. On a local player's turn, the UI shows `Player N` and enables local control for that gang.
5. When the player ends turn, control passes to the next gang/system turn.
6. A handoff overlay asks the group to pass control to the next local player.

The intended feel is:

- multiple gangs
- local rivalry and expansion
- shared-screen hotseat rhythm
- reduced campaign/story complexity

## V1 Feature Scope

### Keep

- Turn order and normal clock advancement
- Crew movement and orders
- Vehicles
- Territory capture and ownership
- Building control and production
- Combat and retaliation
- Basic gang-to-gang hostility and diplomacy
- Basic economic interaction
- Save/load

### Keep If Patch Cost Is Acceptable

- Schemes
- Basic conversations required for gang gameplay
- Building interactions needed for expansion and management

### Disable For V1

- Tutorial
- Achievements
- Politics and mayor office flows
- Law system
- Human-story goals and campaign progression
- Human-only quest chains
- Any UI or event flow that cannot be redirected quickly away from `PlayerID.HumanPlayer`

### Explicitly Out Of Scope

- Online multiplayer
- Simultaneous turns
- Lockstep networking
- desync recovery
- Steam lobby integration
- cross-machine save authority

## Architecture

V1 should live in `GameplayTweaks`, because this repo already treats it as the owner of major gameplay state, save data, and UI behavior.

Use a dedicated subsystem instead of scattering ad hoc checks.

Suggested mod-side components:

- `MultiplayerLiteState`
- `MultiplayerLiteConfig`
- `MultiplayerLiteSaveData`
- `MultiplayerLiteRuntime`
- `MultiplayerLiteUi`
- `MultiplayerLitePatches`

Suggested source file grouping:

- `GameplayTweaks/Features/MultiplayerLite/MultiplayerLiteState.cs`
- `GameplayTweaks/Features/MultiplayerLite/MultiplayerLiteSave.cs`
- `GameplayTweaks/Features/MultiplayerLite/MultiplayerLitePatches.cs`
- `GameplayTweaks/Features/MultiplayerLite/MultiplayerLiteUI.cs`
- `GameplayTweaks/Features/MultiplayerLite/MultiplayerLiteRouting.cs`

## Mode Rules

### Canonical Human Rule

`PlayerID.HumanPlayer` remains the engine's special player and is not replaced.

### Local Control Rule

A player slot is `locally controlled` when:

- multiplayer-lite mode is enabled
- the slot is in the configured local roster
- `Game.ctx.clock.CurrentPlayer` matches that slot

### Turn Completion Rule

For locally controlled gang slots:

- the AI must not auto-finish the turn
- the turn should wait for local input
- the local player ends the turn through a button or hotkey

### Handoff Rule

When one locally controlled gang ends turn and the next locally controlled gang becomes active:

- show a blocking handoff overlay
- show player name/color/slot
- optionally hide sensitive information until confirmed

## Save Model

Persist only mod-owned multiplayer-lite metadata.

Suggested save payload:

- `Enabled`
- `Version`
- `Roster`
- `DisplayNames`
- `SlotOrder`
- `OptionalTurnTimerEnabled`
- `OptionalTurnTimerSeconds`
- `CurrentLocalIndex` if needed for UI only

Do not attempt to alter vanilla player serialization for V1.

## Patch Strategy

### 1. Turn Authority

Primary goal: let selected non-human gang turns behave like human turns.

Primary classes:

- `decompiled/Game.Session/SessionContext.cs`
- `decompiled/Game.Session.Player/AllPlayersManager.cs`
- `decompiled/Game.Session.Player.AI/PlayerAI.cs`
- `decompiled/Game.UI.Session.HUD/HUDBar.cs`

Primary patch targets:

- `PlayerAI.OnPlayerTurnStarted()`
- `PlayerAI.GetPlayerTurnStatus()`
- `PlayerAI.MarkPlayerTurnAsDone()`
- `AllPlayersManager.FinishActivePlayerTurn()`
- `SessionContext.AdvanceToNextPlayerOrSystem()`
- `HUDBar.UpdateNextTurnButton()`
- `HUDBar.OnNextTurnClick()`

Implementation intent:

- For configured local gang slots, force `PlayerAI` to wait for input instead of instantly finishing.
- Reuse the existing human turn completion pattern where possible by calling `MarkPlayerTurnAsDone()`.
- Make the next-turn button visible and usable for active local gang slots.
- Add handoff UI when the active local slot changes.

Important note:

`PlayerAI` already has a waiting model for the real human player:

- `PlayerAI.GetPlayerTurnStatus()` returns `TurnWaitingForInput` when `waitForEndOfTurn` is true.
- `PlayerAI.MarkPlayerTurnAsDone()` clears that wait.

V1 should reuse that model instead of inventing a second turn system.

### 2. Active Local Player Routing

Primary goal: provide one mod-level helper that returns the player id we want UI and interaction patches to use.

Suggested API:

- `MultiplayerLiteState.IsEnabled`
- `MultiplayerLiteState.IsLocalControlled(PlayerID pid)`
- `MultiplayerLiteState.ActiveLocalPlayer`
- `MultiplayerLiteState.GetInteractionPlayerOrHumanFallback()`
- `MultiplayerLiteState.IsCurrentTurnLocalGang()`

This helper should be the first dependency for every multiplayer-lite patch.

### 3. Input and Action Routing

Primary goal: redirect a small set of high-value action entry points away from hardcoded `PlayerID.HumanPlayer`.

Initial action surfaces to target:

- crew movement
- vehicle movement
- basic building interaction
- selection-based commands

Likely classes:

- `decompiled/Game.Session.Input/CarInputMode.cs`
- `decompiled/Game.Session.Board/SelectionManager.cs`
- `decompiled/Game.Session.Player/CommandExecutor.cs`
- `decompiled/Game.Session.Player/PlayerCrew.cs`

V1 should favor patching top-level action creation sites over deep simulation code.

If an action is currently created like:

- `new CommandGoto(PlayerID.HumanPlayer, ...)`

the patch should substitute:

- `MultiplayerLiteState.GetInteractionPlayerOrHumanFallback()`

### 4. UI Surfaces

Primary goal: make a minimum viable local-gang HUD instead of trying to fully generalize the entire UI framework.

Important constraint:

- `BaseHUDDialog.UIPlayer` is hardcoded to `PlayerID.HumanPlayer`.

Do not try to fully rewrite all HUD dialogs in V1.

Instead, patch only the key local-play surfaces:

- next turn button
- active player label
- handoff overlay
- maybe money/crew labels if the wrong player's values are too confusing

Candidate classes:

- `decompiled/Game.UI.Session.HUD/HUDBar.cs`
- `decompiled/Game.UI.Session/HUDManager.cs`
- custom mod overlay in `GameplayTweaks`

Recommended V1 behavior:

- show current local player name and color in a custom overlay
- change next-turn label to `End Turn`
- show `Pass to Player X` dialog between local turns
- leave some vanilla HUD readouts human-centric if they are not gameplay-blocking

### 5. Interaction/UI Routing Candidates

High-value candidates for later V1 or V1.1:

- `decompiled/Game.UI.Session.Convo/ConversationController.cs`
- `decompiled/Game.UI.Session/RoleAssignPopup.cs`
- `decompiled/Game.UI/SchemePopup.cs`
- `decompiled/Game.Session.Player/PlayerScheme.cs`

These are the areas most likely to decide whether schemes survive V1.

### 6. Hard Disable Set

These systems should be disabled early behind a single `MultiplayerLiteState.IsEnabled` gate:

- politics
- law popup
- tutorial
- achievement triggers
- human quest UI

Likely classes:

- `decompiled/Game.UI/LawPopup.cs`
- `decompiled/Game.UI.Session.Politics/*.cs`
- `decompiled/Game.Session.Quests/*.cs`
- `decompiled/Game.Session.Tutorial/*.cs`
- `decompiled/Game.Session.Achievements/*.cs`

## V1 Milestones

### Milestone 1: Turn Skeleton

Goal:

- local gang slots can wait for input
- local gang turns can be ended manually
- handoff overlay appears

Done means:

- player 1 gang can act
- player 2 gang can act on its own turn
- AI gangs still auto-run
- no crash on turn transition

### Milestone 2: Basic Orders

Goal:

- active local gang can move crew and vehicles
- basic commands use the active local player id instead of hardcoded human id

Done means:

- at least one non-human local gang can move, travel, and interact with buildings
- end turn works reliably

### Milestone 3: Territory and Combat

Goal:

- local gangs can fight and expand

Done means:

- combat resolves correctly for local gangs
- ownership and territory updates are attributable to the acting local gang

### Milestone 4: Reduced Board-Game Loop

Goal:

- complete short multiplayer-lite sessions are playable

Done means:

- new game setup works
- two to four local gangs can play multiple turns
- save/load preserves roster
- disabled systems stay out of the way

### Milestone 5: Scheme Evaluation

Goal:

- determine whether schemes are salvageable for V1

Pass condition:

- key scheme entry points can be routed through active local player patches

Fail condition:

- schemes remain too hardcoded to `HumanPlayer`

If fail:

- disable schemes in V1 and move them to V1.1

## First Implementation Pass

This is the recommended coding order.

1. Add config and save scaffolding in `GameplayTweaks`.
2. Add `MultiplayerLiteState` with roster and active-slot helpers.
3. Patch `PlayerAI` turn waiting for selected gang slots.
4. Patch `HUDBar` next-turn behavior for selected gang slots.
5. Add a simple custom handoff overlay.
6. Add a minimum new-game or debug activation path.
7. Patch one movement/action entry path for a non-human local gang.
8. Validate that a second local gang can move and end turn.
9. Disable politics/tutorial/achievements/quest UI.
10. Iterate on missing action routes.

## Activation Path

V1 should not depend on a full custom menu initially.

Recommended activation order:

- config-driven enable flag
- config list of local player slots
- optional debug console command for quick iteration

Possible future activation:

- custom new-game popup
- local player count picker
- name entry

## Config Proposal

Suggested initial config entries:

- `MultiplayerLite.Enabled`
- `MultiplayerLite.LocalSlots`
- `MultiplayerLite.ShowHandoffOverlay`
- `MultiplayerLite.EnableTurnTimer`
- `MultiplayerLite.TurnTimerSeconds`
- `MultiplayerLite.DisablePolitics`
- `MultiplayerLite.DisableQuests`
- `MultiplayerLite.DisableAchievements`
- `MultiplayerLite.DisableTutorial`
- `MultiplayerLite.EnableSchemes`

`LocalSlots` should start as a simple comma-separated list of player ids.

## UI Proposal

V1 custom overlay should show:

- active local player display name
- gang/player color
- current turn
- optional timer
- `End Turn` button

V1 handoff popup should show:

- `Pass to Player X`
- gang color and maybe icon
- `Ready` button

V1 should not try to rebuild the whole HUD.

## System Compatibility Table

### Safe To Keep Early

- turn loop
- AI gangs not in local roster
- basic resource economy
- basic combat
- territory ownership

### Requires Focused Patches

- command creation
- selection interaction
- conversations
- schemes
- reports that assume `HumanPlayer`

### Disable Early

- laws
- politics
- tutorial
- achievements
- quest bar and human quest progression

## Main Risks

### Risk 1: Action Construction Is Hardcoded

Many action paths create `VisitState`, `ModQuery`, `Command...`, or UI events with `PlayerID.HumanPlayer`.

Mitigation:

- patch top-level action creation sites first
- do not try to fix every deep simulation call at once

### Risk 2: HUD Remains Human-Centric

Many HUD screens may still show human values while another local gang is active.

Mitigation:

- add a clear custom overlay for whose turn it is
- patch only the HUD widgets that become misleading enough to block play

### Risk 3: Schemes May Still Be Too Human-Centric

The scheme popup and related flow currently reference `PlayerID.HumanPlayer`.

Mitigation:

- treat schemes as a milestone gate, not a V1 assumption

### Risk 4: Save/Load Edge Cases

The roster is mod-owned while player data is vanilla-owned.

Mitigation:

- keep save metadata small
- validate roster on load
- drop invalid configured slots safely

## Test Matrix

Minimum test set:

1. Start a new multiplayer-lite game with 2 local gang slots.
2. Play 3 full global turns without crashes.
3. Move crews for both local gangs.
4. End turns manually for both local gangs.
5. Confirm AI gangs still auto-run.
6. Save during one local gang turn and reload.
7. Confirm handoff overlay appears after reload.
8. Verify politics/law UI does not block play.
9. Verify disabled quest/tutorial UI does not spam errors.
10. Attempt one combat interaction for a non-human local gang.

## Recommended Go/No-Go Criteria

Proceed with V1 if:

- local gang turns can wait for input cleanly
- one non-human gang can issue basic movement commands
- save/load remains stable

Pause or narrow further if:

- command routing requires rewriting too many unrelated UI flows
- non-human local gangs cannot reliably issue basic orders without major engine surgery

## Initial Code Owners

Use `GameplayTweaks` as the owning project.

Likely touch points:

- `GameplayTweaks/GameplayTweaksPlugin.Core.cs`
- `GameplayTweaks/GameplayTweaksPlugin.cs`
- new `GameplayTweaks/Features/MultiplayerLite/*`

Likely decompiled anchors during implementation:

- `decompiled/Game.Session/SessionContext.cs`
- `decompiled/Game.Session.Player/AllPlayersManager.cs`
- `decompiled/Game.Session.Player.AI/PlayerAI.cs`
- `decompiled/Game.UI.Session.HUD/HUDBar.cs`
- `decompiled/Game.UI.Session/BaseHUDDialog.cs`
- `decompiled/Game.Session.Input/CarInputMode.cs`
- `decompiled/Game.Session.Board/SelectionManager.cs`
- `decompiled/Game.UI/SchemePopup.cs`
- `decompiled/Game.UI/LawPopup.cs`

## Practical Recommendation

Build V1 as `hotseat gangs with heavy scope control`.

Do not chase completeness early.

The fastest path to something real is:

- full local turn ownership
- basic orders
- combat
- territory
- save/load
- clean disable list

If that works, expand outward from there.

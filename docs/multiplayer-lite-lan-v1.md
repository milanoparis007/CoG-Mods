# Multiplayer-Lite LAN V1

Implementation-ready outline for a LAN-capable multiplayer-lite mode in City of Gangsters using a host-authoritative architecture.

This document is a companion to `docs/multiplayer-lite-v1.md`.

It assumes the same core constraint:

- do not try to create multiple true `PlayerID.HumanPlayer` identities

Instead, LAN V1 builds on the same reduced-scope philosophy:

- one host machine owns the simulation
- clients send inputs only
- the host advances turns and publishes results
- politics, laws, tutorial, achievements, and other human-hardcoded systems remain disabled unless later proven safe

## Summary

V1 is a `host-authoritative LAN hotseat replacement`.

- Local network only
- One player hosts
- Other players join from LAN clients
- The host runs the actual game simulation
- Clients submit turn actions for the gang they control
- Only one active local/remote gang has turn authority at a time

This is not true peer-to-peer multiplayer and not a dedicated server model.

## Core Decision

Use `host-authoritative client/server`, not peer-to-peer.

The game already has a single authoritative local session loop in:

- `decompiled/Game.Session/SessionContext.cs`
- `decompiled/Game.Session.Player/AllPlayersManager.cs`
- `decompiled/Game.Session.Player.AI/PlayerAI.cs`

That strongly favors one machine owning:

- turn order
- command validation
- game clock progression
- save/load
- AI execution
- final state

If multiple machines simulate independently, the amount of desync risk is too high for V1.

## Product Shape

The intended user flow is:

1. Host launches City of Gangsters with multiplayer-lite LAN enabled.
2. Host chooses `Host LAN Game`.
3. Host configures local player slots and open client slots.
4. Clients choose `Join LAN Game`.
5. Host assigns each connected client to one gang slot.
6. During play, the host simulation advances normally.
7. When a remote-controlled gang becomes active, that client receives turn authority.
8. The client submits actions to the host.
9. The host validates, applies, and broadcasts results/state changes.

The intended feel is:

- real turn ownership across machines
- one gang per player
- LAN board-game pacing
- no internet service dependency

## V1 Scope

### Keep

- Host-owned turn order
- Crew movement and orders
- Vehicles
- Territory and ownership
- Building control and production
- Combat
- Save/load on host
- Join/leave before play begins

### Keep If Patch Cost Is Acceptable

- Reduced conversations
- Reduced schemes
- Limited reports and overlays

### Disable For V1

- Tutorial
- Achievements
- Politics
- Law system
- Human story quests
- Campaign goal chains tightly bound to `PlayerID.HumanPlayer`
- Mid-session slot reassignment
- Reconnect after disconnect

### Explicitly Out Of Scope

- Internet play
- Steam matchmaking
- Peer-to-peer lockstep
- Dedicated server
- Spectators
- rollback netcode

## Architecture

### Host

The host runs:

- full session state
- AI players
- turn progression
- save/load
- command validation
- event routing
- state replication

### Client

The client runs:

- connection/session UI
- local input capture
- minimal local presentation
- predicted or non-predicted UI feedback
- remote command submission

The client should not be trusted as simulation authority.

### Transport

V1 should use a simple reliable LAN transport.

Recommended requirements:

- TCP or a reliable message layer over LAN
- simple request/response and broadcast messages
- version handshake
- host discovery optional for V1

V1 should prioritize boring and debuggable over fast and fancy.

## Relationship To Hotseat V1

Do not build LAN V1 from scratch if hotseat V1 is not working.

The hotseat plan should produce the core abstractions LAN needs:

- active player routing
- local turn authority helpers
- human-hardcoded system disable gates
- action routing helpers

Then LAN swaps:

- local handoff

for:

- remote turn authority and command transport

Recommended dependency order:

1. Hotseat turn authority works
2. One non-human local gang can issue commands
3. LAN transport layer is added
4. Remote client replaces local handoff for that gang

## Session Model

### Canonical Human Rule

The engine's real `PlayerID.HumanPlayer` still exists and remains special on the host.

### Controlled Slot Rule

Each player slot in the LAN match is one of:

- host-controlled
- client-controlled
- AI-controlled

### Active Slot Rule

At any time, only the current turn owner may issue gameplay commands.

### Command Rule

Clients never mutate simulation state directly.

Clients send:

- intent messages

The host performs:

- validation
- application
- result broadcast

## Networking Model

### Host-Authoritative Loop

The host owns the real call path through:

- `SessionContext`
- `AllPlayersManager`
- `PlayerAI`
- gameplay systems and submanagers

The client does not run gameplay turns independently.

### Message Types

V1 needs only a small message set.

Suggested categories:

- handshake
- lobby/session info
- slot assignment
- ready state
- turn ownership
- gameplay command
- command result
- state snapshot
- error/rejection
- end-turn

### Discovery

Optional for V1.

If discovery slows progress, use direct IP connect.

## Command Strategy

Use `high-level game commands`, not raw object synchronization.

Examples:

- move crew to node
- interact with building
- start conversation
- start combat action
- end turn

Do not try to stream entire object state every frame.

The host should receive a small gameplay command, validate it, and apply the same internal action path that the host would have used for local play.

## State Sync Strategy

V1 should prefer `host snapshots at controlled sync points`, not constant full replication.

Recommended sync moments:

- join complete
- turn start
- command accepted/applied
- turn end
- major popup-triggering events if needed

Possible payload styles:

- custom reduced multiplayer snapshot
- full mod-owned snapshot plus selected vanilla-derived values

V1 should avoid trying to serialize the entire game object graph across the network.

## Save Model

Host is the only source of truth for saves.

Host persists:

- normal game save
- multiplayer-lite LAN metadata

Suggested LAN metadata:

- mode enabled
- version
- player slot ownership map
- display names
- host slot ids
- client slot ids
- disabled systems mask

Clients do not save authoritative match state.

## Patch Strategy

### 1. Turn Authority

Primary classes:

- `decompiled/Game.Session/SessionContext.cs`
- `decompiled/Game.Session.Player/AllPlayersManager.cs`
- `decompiled/Game.Session.Player.AI/PlayerAI.cs`
- `decompiled/Game.UI.Session.HUD/HUDBar.cs`

Primary goals:

- host controls whose turn is active
- only the owner of the active slot can issue commands
- non-owning clients are read-only during other turns

### 2. Active Player Routing

Reuse the same multiplayer-lite helper idea:

- `IsEnabled`
- `IsLocalControlled`
- `IsRemoteControlled`
- `IsHostControlled`
- `ActiveControlledPlayer`
- `GetInteractionPlayerOrHumanFallback()`

The host uses this for simulation authority.
The client uses this for UI gating.

### 3. Input Interception

V1 must intercept top-level input surfaces and decide:

- local apply on host
- remote send on client
- blocked when not active owner

Likely first targets:

- `decompiled/Game.Session.Input/CarInputMode.cs`
- `decompiled/Game.Session.Board/SelectionManager.cs`
- `decompiled/Game.Session.Player/CommandExecutor.cs`
- `decompiled/Game.Session.Player/PlayerCrew.cs`

### 4. UI Gating

The client UI must reflect when the player is:

- active
- waiting
- disconnected
- blocked from acting

Likely first UI surfaces:

- `decompiled/Game.UI.Session.HUD/HUDBar.cs`
- custom mod overlay
- handoff/turn-owner popup replacement

V1 should rely heavily on custom overlay UI rather than trying to generalize all vanilla HUD dialogs.

### 5. Human-Hardcoded System Disable Gates

Apply the same disable approach as hotseat V1:

- politics
- laws
- tutorial
- achievements
- human quest UI

Likely classes:

- `decompiled/Game.UI/LawPopup.cs`
- `decompiled/Game.UI.Session.Politics/*.cs`
- `decompiled/Game.Session.Quests/*.cs`
- `decompiled/Game.Session.Tutorial/*.cs`
- `decompiled/Game.Session.Achievements/*.cs`

## Transport Layer Proposal

V1 should isolate the transport from gameplay patches.

Suggested mod-side components:

- `MultiplayerLiteLanServer`
- `MultiplayerLiteLanClient`
- `MultiplayerLiteLanProtocol`
- `MultiplayerLiteLanSession`
- `MultiplayerLiteLanSerializer`

Suggested file grouping:

- `GameplayTweaks/Features/MultiplayerLiteLan/MultiplayerLiteLanServer.cs`
- `GameplayTweaks/Features/MultiplayerLiteLan/MultiplayerLiteLanClient.cs`
- `GameplayTweaks/Features/MultiplayerLiteLan/MultiplayerLiteLanProtocol.cs`
- `GameplayTweaks/Features/MultiplayerLiteLan/MultiplayerLiteLanSession.cs`
- `GameplayTweaks/Features/MultiplayerLiteLan/MultiplayerLiteLanUI.cs`

Keep network code separate from turn-routing code as much as possible.

## Protocol Proposal

Start with explicit message contracts.

Suggested initial messages:

- `Hello`
- `HelloAck`
- `CreateLobby`
- `JoinLobby`
- `LobbyState`
- `AssignSlot`
- `ReadyState`
- `GameStart`
- `TurnOwnerChanged`
- `SubmitCommand`
- `CommandAccepted`
- `CommandRejected`
- `TurnEnded`
- `StateSync`
- `Disconnect`

All messages should include:

- protocol version
- session id if active
- sender id

## Milestones

### Milestone 1: LAN Skeleton

Goal:

- host can start a LAN session
- client can connect
- slot ownership can be assigned

Done means:

- host/client handshake succeeds
- client sees assigned slot
- no gameplay control yet

### Milestone 2: Remote Turn Ownership

Goal:

- host can notify client when their gang turn is active

Done means:

- active player handoff is visible
- non-active clients are blocked
- active client sees `Your Turn`

### Milestone 3: One Remote Command Path

Goal:

- a client-controlled gang can execute one basic command through host validation

Done means:

- remote player can issue a movement command
- host validates and applies it
- both host and client reflect the result

### Milestone 4: End Turn and Multi-Turn Stability

Goal:

- a full turn cycle across host/client/AI works repeatedly

Done means:

- two or more global turns complete
- remote end turn works
- AI still advances correctly

### Milestone 5: Reduced Playable Match

Goal:

- short LAN matches are playable

Done means:

- 2 players can move, fight, and expand
- disabled systems stay out of the way
- save/load on host remains valid

## First Implementation Pass

Recommended order:

1. Finish hotseat-style `ActiveLocalPlayer` and local turn authority helpers.
2. Add LAN session config and protocol types.
3. Implement host listener and direct-IP client connect.
4. Add lobby/slot assignment UI.
5. Broadcast turn-owner changes.
6. Patch one command entry point to send remote intent instead of applying locally on the client.
7. Apply the command on the host.
8. Broadcast post-command state update.
9. Add remote `End Turn`.
10. Expand to more action surfaces.

## UI Proposal

### Host

The host UI should show:

- hosting status
- connected players
- slot ownership
- current turn owner

### Client

The client UI should show:

- connection state
- assigned gang/player slot
- `Your Turn` / `Waiting`
- last command accepted/rejected

### Shared

Both sides should use a mod overlay for multiplayer state rather than relying on vanilla HUD assumptions.

## Validation Rules

The host must validate at least:

- command sender owns the active player slot
- it is actually that slot's turn
- referenced entity ids are valid
- movement/action legality still passes vanilla rules

If validation fails:

- reject the command
- send a clear reason to the client

## Main Risks

### Risk 1: Command Routing Surface Is Large

Many actions are still created locally with `PlayerID.HumanPlayer`.

Mitigation:

- start with one action family only
- build a reusable send-to-host wrapper for top-level command sites

### Risk 2: Client Presentation Diverges From Host State

Without good sync points, client UI may lie.

Mitigation:

- use host snapshots at turn start and post-command
- keep client prediction minimal in V1

### Risk 3: Human-Centric UI Still Leaks Through

Some vanilla HUD and popup systems will still assume human-only ownership.

Mitigation:

- custom overlay for multiplayer state
- disable high-risk systems early

### Risk 4: Save/Load and Rejoin Are Complex

Host-only save is manageable.
Reconnect logic is not.

Mitigation:

- no reconnect in V1
- no mid-session join in V1 unless trivial

## Test Matrix

Minimum LAN test set:

1. Host starts session on LAN.
2. Client joins by IP.
3. Host assigns client to one gang slot.
4. Turn ownership changes are visible on both machines.
5. Client cannot act outside their turn.
6. Client can issue one move command on their turn.
7. Host validates and applies the move.
8. End turn works from the client.
9. AI gangs still auto-run.
10. Host save/load does not corrupt slot metadata.

## Go/No-Go Criteria

Proceed with LAN V1 if:

- hotseat turn authority logic is already working
- one remote command path works reliably
- host remains stable across several turn cycles

Pause or narrow further if:

- even one basic remote command requires deep rewrites across many unrelated systems
- client UI cannot be kept coherent with host state using simple sync points

## Initial Code Owners

Use `GameplayTweaks` as the owning project.

Likely touch points:

- `GameplayTweaks/GameplayTweaksPlugin.Core.cs`
- `GameplayTweaks/GameplayTweaksPlugin.cs`
- new `GameplayTweaks/Features/MultiplayerLite/*`
- new `GameplayTweaks/Features/MultiplayerLiteLan/*`

Likely decompiled anchors:

- `decompiled/Game.Session/SessionContext.cs`
- `decompiled/Game.Session.Player/AllPlayersManager.cs`
- `decompiled/Game.Session.Player.AI/PlayerAI.cs`
- `decompiled/Game.UI.Session.HUD/HUDBar.cs`
- `decompiled/Game.Session.Input/CarInputMode.cs`
- `decompiled/Game.Session.Board/SelectionManager.cs`
- `decompiled/Game.Session.Player/CommandExecutor.cs`
- `decompiled/Game.UI/SchemePopup.cs`
- `decompiled/Game.UI/LawPopup.cs`

## Practical Recommendation

Treat LAN V1 as `hotseat-plus-network-transport`, not as a separate fresh design.

The safest path is:

- first make multiple gang turns locally controllable
- then replace local handoff with remote command ownership
- then add LAN transport for those commands

If hotseat V1 is not working, LAN V1 is not ready.

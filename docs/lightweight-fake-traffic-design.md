# Lightweight Fake Traffic Design

## Goal

Increase visible street traffic on large custom maps without paying the cost of full `TransitManager` ambient vehicles. The fake traffic system should exist for map presence only, should not participate in crew, pathfinding, combat, or turn simulation, and should avoid visible pop-in during normal camera movement.

## Why this exists

The stock game ambient traffic path in [decompiled/Game.Session.Sim/TransitManager.cs](../decompiled/Game.Session.Sim/TransitManager.cs) spawns real vehicle entities and gives them real scripted navigation:

- `MaybeAddAmbientCar()`
- `CanSpawnAmbientCar()`
- `FindNodesForAmbientCar()`
- `SpawnAmbientCar()`

That path is expensive because every ambient car still uses:

- a real entity
- a real road path lookup
- a real action/script queue
- normal mobile lifecycle and destruction

That is acceptable for low counts, but it does not scale well when the goal is "make the city feel busier."

## Non-goals

- No gameplay interaction.
- No selection, hover, inspect, or ownership.
- No contribution to turn resolution.
- No combat, cargo, crew, raid, police, or heat behavior.
- No dependency on territory ownership or known-node logic beyond optional spawn bias.

## Core approach

Build a dedicated fake-traffic manager that renders pooled visual vehicles moving along cached road lanes. The manager should not create `Entity` instances for normal traffic. It should maintain its own lightweight traffic records and update them in a batched visual loop.

## Architecture

### 1. Route graph cache

Build a lightweight road graph once per board/session:

- source nodes: board nodes with `HasRoad`
- edges: direct road-neighbor links
- segment metadata:
  - world start/end
  - segment length
  - road heading
  - district/territory tag if useful for density bias

This cache should be independent from game pathfinding. It only needs enough data to move visuals from segment to segment.

### 2. Fake traffic manager

Recommended ownership:

- new subsystem in `GameplayTweaks` or a separate `FakeTraffic` mod
- initialized after the board is interactive
- disabled during city generation, save/load transitions, and paused/non-interactive states

Responsibilities:

- maintain pooled visual instances
- choose spawn lanes
- advance traffic records
- despawn traffic outside the active simulation ring
- react to camera movement and zoom

### 3. Traffic records

Each fake vehicle should store only lightweight state:

- pool/render instance id
- current lane/segment id
- distance along segment
- speed scalar
- visual type
- direction
- last visible frame
- optional lane-change target or turn target

No `EntityID`, no script queue, no `PathData`, no AI owner.

### 4. Rendering

Prefer one of these approaches:

1. pooled `GameObject` visuals with disabled gameplay components
2. batched/instanced meshes if available in the existing render path

Start with pooled `GameObject` visuals for implementation speed, then move to instancing only if counts justify it.

Requirements:

- no colliders
- no selection handlers
- no vehicle scripts
- no simulated cargo/crew components

## Spawn model

Use camera-relative density instead of map-wide entity counts.

Recommended rings:

- inner ring: fully animated fake traffic near the camera
- middle ring: lower-density traffic with reduced update cadence
- outer ring: no fake traffic

Suggested first-pass rules:

- spawn only on-screen or near-screen road segments
- cap by visible-road length, not by owned territory count
- bias toward main roads, intersections, districts, and active neighborhoods
- avoid spawning directly in front of the camera when the segment was empty last frame

## Update model

Do not update every fake vehicle every frame.

Use staggered buckets:

- bucket A updates this frame
- bucket B next frame
- bucket C the frame after

Visual interpolation can hide the lower simulation cadence.

Recommended first pass:

- near camera: update every frame
- mid ring: update every 2-3 frames
- far ring: no update because it should not exist

## Movement rules

Keep movement simple:

- move linearly along the current segment
- on segment end, pick the next connected road segment using weighted random continuity
- prefer continuing forward over sharp turns
- despawn after N segments or after leaving the active ring

Avoid:

- global path searches
- destination solving
- multi-hop path validation
- interaction with blocked nodes or live vehicles

## Density controls

Tie density to visible road supply so large maps feel active without exploding cost.

Useful controls:

- `FakeTrafficEnabled`
- `FakeTrafficMaxVisible`
- `FakeTrafficInnerRingRadius`
- `FakeTrafficSpawnPerRoadKm`
- `FakeTrafficUpdateBuckets`
- `FakeTrafficVehicleScaleVariance`
- `FakeTrafficSpeedMin` / `FakeTrafficSpeedMax`

## Visual quality safeguards

To avoid obvious fakery:

- keep 3-5 vehicle visual variants
- vary speed slightly per instance
- vary spawn delay and segment lifetime
- avoid exact overlap by reserving short spawn cooldowns per segment
- despawn behind the camera or when hidden long enough

Optional later polish:

- headlights at night
- district-biased delivery trucks vs. cars
- low-frequency horn/engine audio emitters near the camera only

## Relationship to current ambient traffic

Short term:

- keep vanilla/modded ambient traffic for a small number of real vehicles
- add fake traffic on top as the density layer

Long term:

- reduce or disable real ambient traffic when fake traffic is active on large maps
- reserve real ambient vehicles for special cases only

That split keeps believable city motion while moving the bulk of "background life" off the expensive simulation path.

## Phased implementation

### Phase 1: Visual prototype

- cache road segments
- pool 20-40 fake vehicle visuals
- spawn near camera
- advance on simple segment chaining
- despawn when out of ring

Success condition:

- map feels busier with no noticeable turn-time regression

### Phase 2: Camera-aware density

- add visible-road-length density budget
- add spawn suppression near user-selected vehicle/building overlays
- add update buckets and ring-based cadence

Success condition:

- higher map presence on large maps with stable frame pacing

### Phase 3: Integration polish

- config entries
- optional coexistence rules with real ambient traffic
- optional district/biome vehicle weighting
- lightweight diagnostics counters

Success condition:

- stable enough to ship as default-on for large custom maps

## What not to do

- Do not clone stock ambient traffic by spawning more real `vehicle-ambient-*` entities.
- Do not call stock road pathfinding for each fake vehicle.
- Do not attach crew or authority state.
- Do not let fake traffic enter selection or hover systems.
- Do not route fake traffic through `VehicleNodeAuthority`, `CrewAssignment`, or normal mobile scripts.

## Best next implementation step

Build Phase 1 behind a config gate and keep it fully separate from the current ambient vehicle logic. Treat it as a render-presence system first, not a gameplay system.

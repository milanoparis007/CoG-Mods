# Multi-Crew Vehicle: Follow-Up Plan

Plan for two features: **Scout action** (passenger spends 1 MP to reveal one adjacent node) and **Assign-at-owned-building hardening** (reliable guard + optional UI).

---

## 1. Scout action (passenger: spend 1 MP, reveal one adjacent node)

**Goal:** Allow a passenger to spend 1 movement point to reveal one adjacent node (fog/scout) without moving the vehicle. No separate sprite; just the rule.

**Steps:**

1. **Find visibility/reveal APIs**
   - Search decompiled code for: node “known”, “reveal”, “scout”, “MarkNodeAsKnown”, “known.Get(PlayerID)”, fog, visibility.
   - Identify how the game marks a node as seen for the human player (e.g. `player.meetings.MarkNodeAsKnown` or similar).
   - Identify how to get “nodes adjacent to the vehicle’s current node”.

2. **Define the command or action**
   - **Option A – New command:** Add a custom command (e.g. `CommandScoutOneNode`) that:
     - Is only available when the crew is in a vehicle and is **not** the driver.
     - Costs 1 MP (deduct from `peep.data.agent.movesLeft` or via existing cost API).
     - Takes a target node (adjacent to vehicle node).
     - Calls the game’s “mark node as known” for that node.
   - **Option B – Hook existing flow:** If there is an existing “scout” or “reveal” action, add a Harmony patch that allows passengers to use it (and deduct 1 MP) when the condition above is met.

3. **UI**
   - Add a “Scout” (or “Scout one corner”) button on the crew card when the crew is a **passenger** (in vehicle, not driver).
   - On click: show a picker for adjacent nodes (or auto-pick first adjacent unknown node), then run the command/action.
   - If the game already has a “scout” or “peep” action that reveals, consider reusing that and only changing availability/cost for passengers.

4. **Validation**
   - Ensure only 1 MP is spent per use.
   - Ensure the vehicle does not move.
   - Ensure the chosen node is adjacent to the vehicle’s current node.

**Risks:** Depends on finding the correct “mark node known” and “adjacent nodes” APIs; if they are internal or not exposed, reflection may be needed.

---

## 2. Harden AssignCrewToVehicle prefix (assign only at owned buildings)

**Goal:** The rule “crew can only enter vehicles at owned buildings” is already implemented; make it reliable and optionally reinforce with UI.

**Steps:**

1. **Harden the prefix**
   - In `AssignCrewToVehiclePatch.Prefix`:
     - Isolate any call that can throw (e.g. `FindEntity`, `IsVehicleAtOwnedBuilding` using reflection). Prefer null checks and early returns over broad catches.
     - If a **narrow** exception can still occur (e.g. missing method in reflection), catch only that and **return false** (block assign), and log. Do **not** return `true` in the catch so that “unknown” cases fall back to vanilla.
     - Return `true` in the catch only for a specific, harmless case (e.g. during initial map creation where the check is intentionally skipped), and only when that case is clearly identified (e.g. by a flag or by checking that we are not in a gameplay context).
   - Optionally: detect “setup” or “map creation” (e.g. no human player yet, or a specific game state) and allow vanilla only in that case; otherwise always enforce the rule or block on failure.

2. **Optional: UI**
   - Find where the “add to vehicle” (or “assign crew to vehicle”) action is shown (e.g. crew management popup, vehicle panel, or crew card).
   - Use the same condition as in the prefix: `IsVehicleAtOwnedBuilding(vehicle)` (or a shared helper that wraps it and catches so the UI doesn’t throw).
   - When the selected vehicle is **not** at an owned building: disable or hide the “add to vehicle” control, and optionally show a short tooltip (“Park at your building to add crew”).
   - Ensures the player can’t even try when the action would be rejected by the prefix.

3. **Testing**
   - With hardened prefix: try assigning at an owned building (should work), then at a non-owned node (should block and show message).
   - With UI: confirm the button is disabled/hidden when vehicle is not at an owned building.

**Risks:** If `IsVehicleAtOwnedBuilding` or its dependencies throw during normal play, hardening must ensure we either fix the throw or block assign and log instead of allowing vanilla.

---

## Order of work

- **Quick win:** Harden the prefix first (smaller change, immediate reliability).
- **Then:** Optional UI for “add to vehicle” to improve UX.
- **Then:** Scout action (needs research and possibly a new command or hook).

---

## Files to touch

| Feature              | Files / areas |
|----------------------|---------------|
| Scout action         | New command or patch; crew card or command UI; GameplayTweaks (e.g. MultiCrewVehicle or new file). |
| Harden prefix        | `GameplayTweaksPlugin.MultiCrewVehicle.cs` – `AssignCrewToVehiclePatch.Prefix`, and optionally `IsVehicleAtOwnedBuilding` / reflection. |
| Assign-at-building UI| Same mod; locate “add to vehicle” in decompiled UI (e.g. CrewManagementPopup, vehicle list, crew panel) and add enable/disable + tooltip. |

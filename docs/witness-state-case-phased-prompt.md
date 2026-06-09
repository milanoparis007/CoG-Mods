# Murder Witness Search And State Case Phased Prompt

Use this prompt to continue the witness redesign as a separate stabilization track from robbery meetings, front closures, and turn-end performance.

## Goal

Make murder witnesses harder to erase and make repeated murder witnesses create one crew-member-specific murder case.

The case should attach to the crew member who collected the murder witnesses, not the whole outfit. After enough murder witness pressure builds up, that crew member should face a state/local murder case that works alongside the existing local important witness custody flow.

This is a murder-case pilot. Do not apply the search/threaten/silence redesign to every witness source in the first implementation. Keep room for other witness case profiles later.

## Blast Radius

Full murder-case feature blast radius: medium.

Reason:

- Save data needs new per-witness murder state, not only `WitnessCount`.
- Crew member UI needs new button states and action flow.
- Threaten witness behavior already exists and must be migrated without breaking old saves.
- Murder case progress needs to run on turn processing without stacking too fast.
- Existing federal/national heat and local important witness custody systems must stay stable.

Safe first pass blast radius: low.

Start with markers, counters, and UI labels before changing arrests or deleting witness behavior.

## Current Witness Sources

Current code has these practical witness buckets:

- Normal kill witnesses from human crew kill stat increments.
- Boss-murder witness evidence used for pact/retaliation escalation.
- Cop-kill important witnesses, which are federal/national heat witnesses.
- Cop-assault important witnesses, which are attack/nonlethal cop witnesses.

For this phased prompt, only normal murder/kill witnesses and boss-murder witnesses should feed the new murder case test.

Do not include in the first pass:

- Cop-kill federal witnesses.
- Cop-assault important witnesses.
- Generic fight/attack witnesses.
- Snitch/federal case progress unrelated to murder witnesses.

## Existing Anchors

- `CrewModState.WitnessCount`
- `CrewModState.HasWitness`
- `CrewModState.WitnessThreatAttempted`
- `CrewModState.WitnessThreatenedSuccessfully`
- `CrewModState.ExtraJailYears`
- `ImportantWitnessEntry`
- `NationalHeatState`
- `SaveData.SnitchCaseProgress`
- `OnThreatenWitness`
- `TryThreatenWitnessForPeep`
- `ProcessNationalHeatTurn`
- `ExecuteLocalImportantWitnessArrest`
- `GetNationalHeatUiLabel`
- kill witness increment in `GameplayTweaksPlugin.Combat.cs`
- boss-murder witness evidence in `GameplayTweaksPlugin.Combat.cs`

## Design Rules

- Three active murder witnesses on one crew member starts murder case pressure.
- Only one severe case profile should exist for the player in this pilot: murder case.
- Murder case pressure is per crew member.
- Witnesses remain separate. Handling one witness does not erase the others.
- The player should first find a murder witness before threatening or silencing them.
- Searching costs money each turn.
- A second search for the same witness costs double.
- Threatening can fail and can add heat.
- A failed threat locks out future threat attempts for that witness.
- After a failed threat, finding that witness again only allows silence.
- Silencing can fail.
- Failed silence starts a murder case immediately.
- Failed silence doubles that witness's case gain.
- Federal/national heat should remain reserved for federal-level events.
- Attack/cop-assault witnesses should keep using their current behavior until a later case profile exists.

## Suggested Numbers

- Murder witness threshold: `3`
- Search cost: `$2-$10` per murder witness search turn
- Default first search cost: `$5`
- Second search cost: double
- Normal murder case gain: `1%-3%` per active murder witness per intake
- Failed silence case gain: `2%-6%` for that witness
- Threaten success: start near current behavior, then tune from logs
- Silence success: lower than threaten, because it is stronger
- Murder case arrest trigger: `100%`

## Phase 1: Evidence And Dry-Run Markers

Status: implemented in the first debug/dry-run pass.

Add compact log markers only. Do not change gameplay yet.

Temporary test hook:

- `Ctrl+Shift+F10` adds one non-federal murder witness to the selected human crew member, or to the human boss if no selected crew member is available.
- Remove this hotkey before the final public phase push.

Markers:

- `murder-witness-case-threshold-check`
- `murder-witness-case-dryrun-start`
- `murder-witness-case-dryrun-gain`
- `murder-witness-action-current-state`
- `murder-witness-debug-added`
- `murder-witness-debug-blocked`

Pass criteria:

- Logs show when a crew member has `3+` murder witnesses.
- Logs show projected murder case gain without changing saves.
- Existing `Threaten Witness` behavior still works.
- Federal important witness and cop-assault witness behavior is unchanged.

## Phase 2: Per-Witness Save Data

Status: planned.

Add per-crew murder witness records while keeping old scalar fields as compatibility fallbacks.

Proposed fields per witness:

- witness id
- owning crew peep id
- added day
- found state
- search attempts
- threat attempted
- threat failed
- threatened successfully
- silence attempted
- silence failed
- silenced successfully
- case gain multiplier
- closed flag
- case profile, default `murder`

Proposed per-crew case fields:

- murder case active
- murder case progress
- murder case started day
- last case intake day
- failed silence multiplier active

Pass criteria:

- Old saves with only `WitnessCount` load safely.
- Existing non-federal murder witnesses can be represented as anonymous murder witness records.
- Saving and loading preserves witness records.
- No arrest behavior changes yet.

## Phase 3: Search Before Action

Status: planned.

Replace direct witness action with a search step.

UI behavior:

- If murder witnesses exist but none are found, show `Look For Murder Witness`.
- If one murder witness is found and threat is allowed, show `Threaten Witness`.
- If one murder witness is found and threat is locked out, show `Silence Witness`.
- If no active murder witnesses remain, show `No Murder Witness`.

Search behavior:

- Deduct search cost from clean safehouse money.
- Cost is per witness search turn.
- First search uses the base cost.
- Second search for the same witness costs double.
- Search can take one or more turns if needed, but first pass can resolve instantly with clear logs.

Markers:

- `murder-witness-search-started`
- `murder-witness-search-paid`
- `murder-witness-search-found`
- `murder-witness-search-failed`
- `murder-witness-search-blocked`

Pass criteria:

- Player can no longer threaten a murder witness without finding one.
- Cost is paid only when a search starts or advances.
- UI makes the next available action clear.

## Phase 4: Threaten And Silence Outcomes

Status: planned.

Threaten behavior:

- Success closes only the selected witness.
- Failure marks that witness as threat failed.
- Failure adds heat risk.
- Failure prevents threatening that witness again.
- Other witnesses remain active.

Silence behavior:

- Success closes only the selected witness.
- Failure closes player access to that witness.
- Failure starts the murder case immediately.
- Failure doubles that witness's future case gain.

Markers:

- `murder-witness-threat-success`
- `murder-witness-threat-failed`
- `murder-witness-silence-success`
- `murder-witness-silence-failed`
- `murder-witness-action-locked`

Pass criteria:

- A handled witness does not clear all witnesses.
- Failed threat routes correctly to silence-only behavior.
- Failed silence starts or escalates the crew member's murder case.

## Phase 5: Murder Case Progression

Status: planned.

Start murder case pressure when a crew member has `3+` active murder witnesses or after a failed silence.

Progression:

- Each active murder witness adds `1%-3%` per intake.
- Failed silence murder witness adds `2%-6%`.
- Closed witnesses no longer add progress.
- Progress belongs to the crew member.
- At `100%`, trigger local custody/state-case-style behavior for that crew member.

Use existing local custody where possible:

- `ExecuteLocalImportantWitnessArrest`
- `MarkLocalImportantWitnessCustodyPending`
- `TrialDaysRemaining`
- `ExtraJailYears`

Markers:

- `murder-case-started`
- `murder-case-gain`
- `murder-case-threshold`
- `murder-case-custody-staged`
- `murder-case-closed`

Pass criteria:

- Case progress does not affect the entire outfit.
- Case progress does not duplicate federal/national heat.
- Arrest/custody happens once.
- Case progress pauses or defers cleanly if the crew member is missing, dead, hidden, or already jailed.
- Attack and cop-assault witnesses do not start murder case progress.

## Phase 6: AI And Balance Follow-Up

Status: planned.

After human flow is stable, decide whether AI gangs use the same search/threat/silence rules.

Keep first AI pass conservative:

- AI can search only when it has enough clean money.
- AI should not spam witness actions every turn.
- AI should prefer legal bribes or hideout if those are already active choices.
- AI witness actions should not interrupt routes or combat actions.

Pass criteria:

- AI behavior is visible in logs.
- AI does not drain money too fast.
- AI does not clear witnesses unrealistically fast.

## Implementation Order

1. Add dry-run markers and UI-only projected labels.
2. Add save-safe per-murder-witness records.
3. Gate direct threaten behind search.
4. Add threat/silence per-witness outcomes.
5. Add per-crew murder case progression.
6. Tune costs, chances, and case gain from live logs.

## Retest Checklist

- Load an old save with existing witness counts.
- Confirm old witnesses migrate without exceptions.
- Create one murder witness and confirm no murder case starts.
- Create three murder witnesses on one crew member and confirm murder case dry-run/progress starts.
- Threaten one murder witness and confirm other murder witnesses remain.
- Fail a threat and confirm that witness cannot be threatened again.
- Find the same witness again and confirm only silence is available.
- Fail silence and confirm murder case starts immediately.
- Confirm federal witness/protected witness behavior still shows as protected.
- Confirm cop-assault witnesses do not start murder case progress.
- Confirm murder case arrest affects only the crew member with the murder witnesses.

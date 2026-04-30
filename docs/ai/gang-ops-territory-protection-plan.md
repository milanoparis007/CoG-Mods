# Gang Ops Territory Protection Pass

Use this prompt when Gang Ops needs another tuning pass for AI gang territory growth, front protection, or stale territory cleanup.

## Reusable Prompt

Do another Gang Ops territory/protection pass in `GameplayTweaks`. Check live `Player.log` and `Player-prev.log` for `GangOps.*`, `gangops-auto-protect`, `ai-outpost-territory-pass`, `territory-audit`, `outpost-count-drop`, and `Cannot double-start outpost`. Gangs should open outpost target corners faster, keep front corners protected so fronts do not close from support debt, and still avoid overriding player-custom Gang Ops settings. Prioritize pact and independent behavior separately, with independent gangs slightly stronger on territory takeover so they can keep up without pact bonuses. Build `GameplayTweaks` after edits.

## Current Ownership

- Runtime turn loop: `GameplayTweaks/GameplayTweaksPlugin.cs`
  - `RunGangOpsTurn`
  - `RunAutoProtectPass`
  - Gang Ops defaults and profile migration
- Territory reconcile: `GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs`
  - `TryRunGangOpsAutoProtectPass`
  - `TryRunAiOutpostTerritoryExpansionPass`
  - `ClaimNeutralNodesFromCurrentRespect`
  - `ReconcileTerritoryOwnershipFromCurrentRespectPass`
- Config defaults: `GameplayTweaks/GameplayTweaksPlugin.Core.cs`

## Acceptance Checks

- `GangOps.Pact` and `GangOps.Independent` turn logs should no longer sit at `protect=0 territoryExpand=0` every cycle when eligible gangs have fronts.
- `gangops-auto-protect` should report front count, protected nodes, support resets, and ownership switches when applicable.
- Corner-info respect values should stay vanilla-scale. Do not call `BusinessUpdate.UpdateRespectFromTerritory` by itself in recurring Gang Ops passes because it is additive and vanilla normally runs it after clearing per-turn respect sources.
- Recurring Gang Ops territory passes should call `HeatAndRespect.RecomputeRespectForAllPlayers(node, false)` so current respect moves through vanilla velocity instead of snapping to full goal values.
- AI fronts with support debt should have the debt reset before the vanilla three-month closure path removes them.
- `ai-outpost-territory-pass` should run from native Gang Ops when no external territory-expansion mod owns that behavior.
- Saved/default migrations should only update old default values, not custom player-tuned Gang Ops settings.

## Next Tuning Levers

- Lower `AutoProtectIntervalDays` if fronts still close between monthly upkeep cycles.
- Raise `TerritoryTakeoverAggressionPercent` up to the existing 300 clamp if target corners still lag.
- Keep `AutoProtectRange` at `1` by default. Increase it only for deliberate experiments after checking corner-info respect values; radius `2+` can touch hundreds of nodes on dense maps.
- Add richer scoring before further expansion changes: outpost target count, neutral-node count, contested-node count, and front support-debt count per gang.

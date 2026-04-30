# Gang War Mediation Conversation Plan

## Status

First implementation pass is in place:

- Runtime-injected `ConvoGangs` button using `GameplayTweaksMediateWarOpen`.
- C# popup lists the current conversation outfit's active wars.
- Acceptance uses relationship, player power, WarHeat, and pact context.
- Accepted mediation clears aggro, both directed WarHeat entries, both directed revenge entries, and adds the truce relationship buff.
- Successful mediation consumes the active conversation action and writes `GangWarMediation` verification logs.

Next validation pass should exercise the cases below in-game and tune thresholds if inter-pact wars are too easy or independent wars are too stubborn.

## Goal

Add a ConvoGangs button that lets the player ask an outfit to mediate one active war. The button opens a list of gangs that the current outfit is at war with, lets the player choose one, then accepts or refuses based on war pressure, player leverage, and pact context.

## Feasibility

This is feasible, but it should be a C#-backed conversation feature instead of a pure `ConvoGangs.sim` branch. The SIM data can show a static button, but the game data requirements do not appear to provide a reusable "list all active wars for this gang" picker. GameplayTweaks already has the safer pattern for this through custom conversation callbacks in `GameplayTweaksPlugin.UiStabilityPatches.cs`.

## Implementation Shape

1. Add a top-level `ConvoGangs.sim` button, for example `convo.gangs.mediate-war.button`, routed to a small state with `onClick GameplayTweaksMediateWarOpen`.
2. Register and intercept `GameplayTweaksMediateWarOpen` the same way the pact and dirty-cash custom callbacks are handled.
3. In C#, enumerate candidate wars for the current conversation outfit:
   - Include pairs where either side is actively aggro against the other.
   - Exclude dead, missing, truced, or same-player entries.
   - Include the human outfit only if the target outfit is at war with the player.
   - Tag each row as independent, pact-vs-independent, same-pact, or inter-pact.
4. Show a small selection popup/list with the opponent name, pact context, current WarHeat, and rough difficulty.
5. On selection, compute acceptance and either clear the war or show a refusal result.

## Acceptance Score

Use existing systems first:

- War pressure: use `GetWarHeat(channel, aPid, bPid)` in both directions and take the higher value. Higher heat lowers acceptance.
- Player leverage: compare `CalculateGangPower(G.GetHumanPlayer())` against the combined or stronger side of the target war. Stronger player leverage raises acceptance.
- Relationship: current relationship with the conversation outfit raises or lowers acceptance.
- Pact context:
  - Independent-vs-independent should be the easiest to mediate.
  - Pact-vs-independent should be harder, especially if the pact side is winning.
  - Inter-pact wars should be hardest because ending one pair can affect alliance behavior.
  - Same-pact conflicts should either be blocked or treated as a special low-cost internal truce if the game can produce them.

Initial scoring target:

```text
acceptance = 50
  + relationshipBonus
  + playerPowerBonus
  - warHeatPenalty
  - pactContextPenalty
  - recentRefusalPenalty
```

Suggested thresholds:

- `>= 60`: accept and end the selected war.
- `40-59`: accept only with a cash/favor cost in a later pass.
- `< 40`: refuse and apply a short cooldown.

## War Resolution

On acceptance:

1. Call `ClearWarBetweenPlayers(a, b)`.
2. Remove both directed WarHeat entries for the pair from the relevant WarHeat store.
3. Clear pending revenge entries involving the pair if the helper surface is available.
4. Add a short truce or cooldown relbuff so the same war is not immediately re-opened.
5. Log the result with the pair, heat, score, pact context, and player leverage.

## Validation Cases

Test these runtime cases:

1. Player outfit mediates an independent-vs-independent war.
2. Player outfit mediates player-vs-gang war from the enemy outfit conversation.
3. Player outfit tries to mediate a pact-vs-independent war.
4. Player outfit tries to mediate inter-pact war.
5. No active wars: button hides or opens a clean "no wars" result.
6. Accepted mediation does not leave pending revenge that restarts the war immediately.

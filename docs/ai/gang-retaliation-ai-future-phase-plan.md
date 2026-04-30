# Gang Retaliation AI Future Phase Plan

## Current Pass Baseline

- Human attacks against AI gangs now feed Gang Ops WarHeat and revenge queues instead of only boss-kill special cases.
- Retaliation responses first try disruptive pressure, such as force-closing a target business/front, unless the gang's overall aggression or WarHeat is high enough for direct violence.
- Pact alliance and mutual truce checks should skip retaliation before consuming coordinated attack cooldowns or revenge execution.
- Route final goals remain preview authority, but active owned-building visits should not treat a pending delivery final goal as the current interaction node.

## Phase 2 Goal

Make retaliation feel intentional: a gang should pick the response that best protects its income, territory, and reputation rather than blindly rolling direct attacks.

## Proposed Retaliation Scorer

Create a shared `GangRetaliationOpportunity` scorer used by revenge, coordinated attacks, and AttackAdvisor hooks.

Inputs:

- Recent WarHeat for the pair and whether the attacker hit crew, boss, safehouse, or owned business.
- Gang Ops settings: territory takeover %, close fronts %, overall aggression %, revenge delay.
- Gang profile: pact member, independent, pact leader, weak gang, aggressive type.
- Player/gang strength comparison: living crew, weapons, known fronts, controlled territory, safehouse status.
- Relationship state: truce, pact alliance, inter-pact alliance, active war.
- Economy pressure: enemy businesses/fronts with trade, owned businesses near contested territory, outpost/safehouse proximity.

Outputs:

- `CloseFront`: force-close a business/front tied to the attacker.
- `HitCrew`: dispatch 1-2 hitters after the attacker or boss.
- `AttackBuilding`: damage a controlled business or outpost.
- `TakeTerritory`: expand into weak or low-respect territory.
- `CallPactSupport`: request pact member participation when the victim is in a pact.
- `StandDown`: skip due to truce, alliance, weakness, or low heat.

## Priority Rules

1. Never retaliate through a truce or protected pact alliance.
2. If the gang was recently attacked, evaluate retaliation before normal coordinated attacks.
3. Prefer `CloseFront` or `AttackBuilding` when aggression is below 125% and a valid front/business exists.
4. Prefer `HitCrew` when overall aggression is high, WarHeat is high, a boss was hit, or no valid business/front exists.
5. Let independent gangs use lower WarHeat thresholds so they can keep up without pact support.
6. Let pact gangs share only severe events by default; repeated attacks or boss kills can escalate to `CallPactSupport`.

## Logging And Tuning

Add one compact log line per decision:

`GangOps.RetaliationScore attacker=<pid> defender=<pid> heat=<n> action=<action> score=<n> reason=<reason>`

Use that to tune:

- Retaliation frequency after 1, 2, and 3 attacks.
- Closure vs direct attack ratio.
- Pact member support frequency.
- Cases where no action was possible.

## Suggested Next Implementation Slice

Implement the scorer as a pure helper first, then swap revenge and coordinated attack selection over to it. Keep AttackAdvisor hooks as a later slice so the core behavior can be validated through logs before broad AI-sim interception.

## Implementation Pass - 2026-04-29

- Added the shared retaliation scorer in `GameplayTweaksPlugin.cs`.
- Revenge execution and WarHeat-backed coordinated attack automation now score `CloseFront`, `HitCrew`, `TakeTerritory`, `CallPactSupport`, or `StandDown` before spending the response.
- Human attacks now queue delayed revenge on first hit, while threshold heat can still trigger immediate retaliation.
- Pact boss-death blame now prefers recent WarHeat evidence before falling back to active aggro, reducing cases where a pact blames the player for an AI slaying.
- New tuning log:

`GangOps.RetaliationScore attacker=<pid> defender=<pid> channel=<Pact|Independent> heat=<n> action=<action> score=<n> reason=<reason> close=<bool> support=<bool> territory=<bool> severe=<bool>`

Next tuning pass should use this log to adjust score weights after testing one-hit, repeated-hit, boss-kill, truce, and pact-support cases.

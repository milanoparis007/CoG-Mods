# CopKilling AttackAdvisor Cleanup Pass Prompt

Use this later when the route-simulation pass is stable and the remaining live-log noise is mostly CopKilling advisor nullrefs.

## Prompt

Check the live SomaSim logs and clean up the recurring CopKilling `AttackAdvisor` swallowed nullrefs without changing route-simulation behavior.

Primary live logs:

- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`

Current recurring signals:

- `[CopKilling] AttackAdvisor.OnTurnUpdate swallowed exception during cop-war stabilization: NullReferenceException: Object reference not set to an instance of an object`
- `[CopKilling] AttackAdvisor.TryPickBuilding swallowed exception during cop-war stabilization: NullReferenceException: Object reference not set to an instance of an object`

Starting code anchors:

- `CopKilling\CopKillingPlugin.cs`
  - `AttackAdvisorTryPickBuildingPrefix`
  - `AttackAdvisorOnTurnUpdatePrefix`
  - `AttackAdvisorTryPickBuildingFinalizer`
  - `AttackAdvisorOnTurnUpdateFinalizer`
  - config gates:
    - `EnableAttackSuppressionTryPickBuildingGuard`
    - `EnableAttackSuppressionAttackAdvisorOnTurnUpdateGuard`
- Decompiled vanilla anchors:
  - `decompiled\Game.Session.Player.AI\AttackAdvisor.cs`
    - `OnTurnUpdate`
    - `TryPickBuilding`
    - `FindBuildingTargets`

Investigation goals:

1. Identify which vanilla `AttackAdvisor` state is null or stale: advisor config, advisor data, player territory, headquarters node, controlled building entity, building component, relationship/combat state, or target list contents.
2. Add diagnostic context before swallowing the exception so the next log shows player id, gang/human status, suppression state, clock day, config section, and the suspected null/stale entity.
3. Prefer a narrow prefix guard or stale-target filter over broad finalizer-only swallowing once the failing state is known.
4. Keep the current finalizers as safety nets, but dedupe their logs so long runs do not spam hundreds of identical lines.
5. Do not weaken the cop-war stabilization guard that prevents crashes.

Validation:

- Build `CopKilling\CopKilling.csproj -c Release`.
- Check the next live log for either:
  - no recurring `AttackAdvisor.* swallowed exception` lines, or
  - one deduped diagnostic line with enough context to patch the exact stale state in the following pass.
- Confirm no new route logs regress: `route-requeue-allowed`, `delivery-route-pump-wait`, and `route-resume` should still appear normally when delivery routes are active.

Do not copy the DLL into the live Steam install by default. Stage public release files only if the build is meant for the public package.

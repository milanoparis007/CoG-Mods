# Patch Reliability

Use this when the job is about Harmony target stability, reflection safety, patch conflicts, or log-driven regressions.

This repo runs on BepInEx 5.4.23.4 under Unity 2020.3.28 and uses Harmony heavily. Favor stable, inspectable patch points over clever ones.

## Default patch order

1. Find the owning project with `project-map.md`.
2. Search current patches and target methods with `rg`.
3. Verify the exact signature in `decompiled/` when there is any doubt.
4. Prefer `postfix`, `prefix`, or `finalizer`.
5. Escalate to manual patching or reverse patching only when the target shape or Harmony limits require it.
6. Use transpilers only as a last resort.

## Reliability rules

- Patch implemented methods, not abstract/base virtual declarations, unless there is a documented reason to intercept the base entrypoint.
- Null-check every reflection lookup and emit a concrete log message when the target is missing.
- Log the exact type and method name that failed so log triage is actionable.
- Cache reflection results in static fields or initialization paths, not in hot loops.
- Prefer small, named helper methods for target resolution instead of repeating `AccessTools` trees.
- When multiple mods may touch the same system, preserve compatibility gates and ownership checks.

## When to use manual or reverse patches

Use manual patch registration or reverse patches only when simpler patterns are not viable, for example:

- Harmony cannot patch the method shape cleanly.
- The target has runtime type resolution issues.
- The existing owning project already uses a manual patch pattern for the same subsystem.

In this repo, `GameOptimizer` already contains cases where Harmony DMD or type resolution limits forced manual approaches. Follow that local pattern instead of inventing a new one.

## Current repo warning patterns

### Missing patch target

Example from `Player.log`:
- `AccessTools.DeclaredMethod: Could not find method for type Game.Session.Data.CheckOwnerRelbuffs and name Explain`

Action:
- Re-check the decompiled signature and game version.
- Confirm the method still exists, was renamed, or moved.
- Update the target resolver only after confirming the real method location.
- If the patch is optional, log and skip cleanly instead of crashing plugin load.

### Patching base virtual declarations

Example from `Player.log`:
- Harmony warning about patching `Game.Session.Input.BaseInputMode::OnPrimary` and related methods instead of the declared implementation.

Action:
- Repoint the patch to the concrete implemented method unless the goal is explicitly to intercept the shared base path.
- Document the reason if you intentionally keep the base method.

### Type lookup failure

Example from `Player.log`:
- `AccessTools.TypeByName: Could not find type named Game.UI.Session.Picks.MobilePickUtil`

Action:
- Search `decompiled/` for namespace drift or renamed types.
- Add a narrow fallback lookup only if the type genuinely moved across versions.
- Do not broaden lookups so far that unrelated types can match.

## BepInEx and Harmony defaults

- Use BepInEx logging for actionable plugin startup and patch registration messages.
- Keep config-driven risky behavior behind `ConfigEntry` toggles.
- Prefer deterministic patch registration over `PatchAll` when only a few fragile targets are involved.

## Sources

- BepInEx runtime patching: https://docs.bepinex.dev/master/articles/dev_guide/runtime_patching.html
- BepInEx plugin tutorial: https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html
- Harmony prefix/postfix/finalizer docs:
  - https://harmony.pardeike.net/articles/patching-prefix.html
  - https://harmony.pardeike.net/articles/patching-postfix.html
  - https://harmony.pardeike.net/articles/patching-finalizer.html

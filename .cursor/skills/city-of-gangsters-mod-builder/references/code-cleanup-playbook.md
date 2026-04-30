# Code Cleanup Playbook

Use this when improving readability, reducing duplication, or making patch code safer to extend.

## Cleanup defaults

- Reuse helpers in `GameplayTweaks` before adding new reflection, save-state, or context access helpers.
- Make the smallest structural change that removes repeated logic.
- Keep behavior stable unless the task explicitly includes a functional change.
- If cleanup changes gameplay semantics or compatibility behavior, put it behind config.

## Preferred refactors

- Extract repeated reflection target resolution into cached helpers or constants.
- Replace repeated string literals for type names, method names, config keys, and log prefixes with shared constants when they are reused enough to drift.
- Split oversized files only by real subsystem boundaries, not arbitrary line count.
- Keep one owner per subsystem. Do not spread the same behavior across multiple mods unless compatibility requires it.
- Move complicated conditional logic into small named helpers when the patch body is hard to scan.

## When not to refactor

- Do not split a file if the result would hide patch ordering or shared state flow.
- Do not introduce a shared helper library for a one-off cleanup.
- Do not rewrite working manual patch logic just to make it look uniform if the current pattern exists for a Harmony limitation.
- Do not remove compatibility guards unless ownership rules are being intentionally changed.

## Repo-specific smells to target

- Repeated `AccessTools` lookups for the same target in the same subsystem.
- Reflection inside per-turn, per-frame, or coroutine inner loops.
- Copy-pasted log prefixes or method-name strings across related patch helpers.
- Large patch files where unrelated systems are mixed together and the edit risk is rising.

## Safe cleanup flow

1. Route to the owning project.
2. Search for existing helpers before adding new ones.
3. Read the surrounding subsystem to find shared assumptions.
4. Extract only the repeated pieces.
5. Build the narrowest project.
6. Re-scan touched logs or code paths for hidden coupling.

## Related references

- Reliability-sensitive refactors: [patch-reliability.md](patch-reliability.md)
- Performance-sensitive refactors: [performance-playbook.md](performance-playbook.md)
- Final validation: [workflow-checklist.md](workflow-checklist.md)

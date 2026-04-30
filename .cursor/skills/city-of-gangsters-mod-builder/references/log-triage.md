# Log Triage

Use this when the task starts from `Player.log`, `Player-prev.log`, plugin load failures, or compatibility symptoms.

## Default triage flow

1. Search the exact warning or error string in `Player.log` and `Player-prev.log`.
2. Identify the plugin GUID or log prefix that owns the message.
3. Route to the owning project with `project-map.md`.
4. If the target is missing or renamed, inspect `decompiled/` before editing code.
5. Check `docs/compatibility-matrix.md` when multiple mods may own the same behavior.

## Common log shapes

### Missing patch target

Signal:
- `Could not find method`
- `Patching exception in method null`

Action:
- Verify the exact method signature in `decompiled/`.
- Confirm the game build still matches the patch assumptions.
- Update the resolver or skip cleanly if the patch is optional.
- Read [patch-reliability.md](patch-reliability.md).

### Harmony "implemented methods" warning

Signal:
- Warning that you should patch the declared implemented method instead of a base virtual declaration.

Action:
- Find the concrete implementation and patch that method.
- Keep the base patch only if you can justify the shared interception point.
- Read [patch-reliability.md](patch-reliability.md).

### Type lookup failure

Signal:
- `AccessTools.TypeByName: Could not find type named ...`

Action:
- Search `decompiled/` for namespace drift or renamed types.
- Add a narrow fallback only if version drift is real.
- Avoid broad fuzzy resolution.
- Read [patch-reliability.md](patch-reliability.md).

### Plugin conflict or duplicate behavior

Signal:
- duplicate functionality,
- skipped plugin because newer version exists,
- multiple mods touching the same subsystem,
- unexpected authority/ownership behavior.

Action:
- Check plugin GUID ownership and the compatibility matrix first.
- Confirm which mod is meant to be authoritative.
- Avoid "fixing" behavior by adding another overlapping patch in a second project.

### Performance symptom in logs

Signal:
- startup stalls,
- repeated expensive warnings,
- noisy diagnostics around turn updates, map setup, or rendering.

Action:
- Route to `GameOptimizer` if the change is performance-only.
- Inspect existing optimizer patches for the same subsystem.
- Read [performance-playbook.md](performance-playbook.md).

## Useful commands

```powershell
rg -n "Could not find method|TypeByName|implemented methods|Skipping \\[" Player.log Player-prev.log -S
rg -n "<type-or-method-name>" decompiled -S
```

## Related references

- Project ownership: [project-map.md](project-map.md)
- Reliability fixes: [patch-reliability.md](patch-reliability.md)
- Performance fixes: [performance-playbook.md](performance-playbook.md)

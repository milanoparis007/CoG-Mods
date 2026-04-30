# Workflow Checklist

Use this checklist before finalizing any mod change.

## Before Editing
- Confirm target project from `project-map.md`.
- Locate exact method/class with `rg`.
- Read nearby code for existing guards/flags/compat logic.
- Scan relevant `Player.log` or `Player-prev.log` lines when the task starts from runtime behavior.
- Load the matching playbook when the task is mainly reliability, cleanup, performance, or log triage:
  - `patch-reliability.md`
  - `code-cleanup-playbook.md`
  - `performance-playbook.md`
  - `log-triage.md`

## During Editing
- Keep changes minimal and local.
- Prefer existing helper patterns over new reflection trees.
- Preserve plugin compatibility gates unless task explicitly changes them.

## Build Validation
- Run targeted build:
  - `dotnet build <target>.csproj -c Release`
- If shared/public members changed, run:
  - `dotnet build ClassLibrary1.sln -c Release`

## Runtime Validation (manual)
- Verify the exact UI/gameplay scenario touched.
- Verify right-click/close/navigation interactions for UI changes.
- Verify role/visibility/permission behavior for crew/gang UI changes.
- Verify no regression in related popups or hotkeys.

## Reporting
Include:
- Files changed.
- What behavior changed.
- Build result.
- Any untested risks or assumptions.

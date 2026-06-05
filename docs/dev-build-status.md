# Dev Build Status

## 2026-06-05 Robbery Project Dev Snapshot

Branch target: `dev-robbery-log-cleanup`

This snapshot is meant for GitHub tracking while the robbery/front-pressure project still needs log cleanup. It should not be treated as the cleaned public release.

Current staged GameplayTweaks build:

- phase: `8ET`
- DLL timestamp: `2026-06-05 09:00:42`
- DLL size: `2517504`
- validation: `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors
- solution validation: `dotnet build ClassLibrary1.sln -c Release` passed with `0` warnings and `0` errors

Why this remains a dev build:

- robbery prompt duplicate cleanup still needs one live verification pass;
- important-business closure and front-pressure diagnostics are intentionally verbose;
- `VerificationLog` output still contains phase-level proof markers that should be gated, reduced, or removed before the public package;
- public package docs should be refreshed after cleanup so they no longer lead with the dev snapshot warning.

Public cleanup checklist:

- verify `pair-already-queued` / `pair-queued-replaced` produces one next-turn robbery response per robber outfit;
- reduce or gate robbery/front-pressure verification spam after behavior is confirmed;
- rebuild and restage the public DLLs;
- update `CHANGELOG.md` and `GUIDE.md` with cleaned public-release wording;
- push/tag the cleaned public release separately from this dev snapshot.

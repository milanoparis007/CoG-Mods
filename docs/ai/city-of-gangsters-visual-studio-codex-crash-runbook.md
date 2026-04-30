# City of Gangsters Visual Studio Codex Crash Runbook

Paste the block below into the Visual Studio Codex terminal when you want Codex to resume the current native-crash investigation with the right context.

```md
Use `city-of-gangsters-mod-builder-visual-studio`.

We are working in:
- `C:\Users\User\source\repos\ClassLibrary1`

Primary repo/solution:
- `ClassLibrary1.sln`

Primary live logs:
- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
- `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`

Decompiler:
- `C:\Users\User\Documents\Modding Programs\DNSPEX`

Video tools:
- `C:\Users\User\Documents\COG Modding Stuff\ffmpeg-8.1-full_build`

Main authored StreamingAssets project:
- `C:\Users\User\source\repos\ClassLibrary1\Things To Have\Current After Prohibition Mod`

Crash-dump workflow:
- ProcDump path: `C:\Users\User\Documents\Modding Programs\ProcDump\procdump.exe`
- Dump folder: `C:\Users\User\Documents\CrashDumps`
- Latest captured dump:
  `C:\Users\User\Documents\CrashDumps\CoG.exe_260410_082532.dmp`

What we confirmed:
- The hard crash is native-side, not just a normal managed exception.
- ProcDump captured:
  - `C00000FD.STACK_OVERFLOW`
  - `C0000005.ACCESS_VIOLATION`
- The shallow native call stack ended in:
  - `ntdll.dll!00007ffa47acd624`
  - `KERNELBASE.dll!00007ffa4515b6ae`
  - `UnityPlayer.dll!00007ff935425a92`
  - `UnityPlayer.dll!00007ff93483c3d7`
  - `UnityPlayer.dll!00007ff934c47878`
  - `kernel32.dll!00007ffa470a7374`
  - `ntdll.dll!00007ffa47a7cc91`
- The dump supports a runaway recursion or re-entry loop first, then access violation second.
- Current leading suspicion is still a re-entry loop in `CopKilling`, especially the later kill/hostility application path or a UI refresh path it triggers.

Current mod-side state:
- `CopKilling` was being hardened into queue-only cop-war mode.
- `EnableQueuedCopWarAggro` is disabled.
- `EnableCopWitnessArrestEscalation` is disabled.
- `F10` debug assault had been stabilized through staged diagnostics.
- `F9` debug kill was isolated as safe through setup, fresh-retaliation, and explicit-state-prep.
- The remaining unstable area is after explicit-state-prep in the kill hostility path.

What to do next:
1. Read the current live `Player.log` and `Player-prev.log`.
2. Inspect `CopKilling/CopKillingPlugin.cs`, especially the kill debug path and `ApplyExplicitPrecinctHostilityState(...)`.
3. Prefer the smallest code-side hardening step against recursion/re-entry after explicit-state-prep.
4. Build the narrowest touched project first:
   - `dotnet build CopKilling/CopKilling.csproj -c Release /nodeReuse:false`
5. Summarize what changed, what was validated, and remaining risk.
```

## Notes

Use this runbook when you want a fresh Codex session in Visual Studio to pick up the current crash investigation without re-explaining the entire history.

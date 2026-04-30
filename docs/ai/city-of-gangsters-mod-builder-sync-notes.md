Use `city-of-gangsters-mod-builder-visual-studio` when Visual Studio is explicitly the primary editor.

Mirror durable improvements back into `city-of-gangsters-mod-builder` when they help:

1. project ownership routing
2. live log triage order
3. safer build and validation defaults
4. Harmony and reflection hardening patterns
5. durable local tooling paths:
   `C:\Users\User\Documents\Modding Programs\DNSPEX`
   `C:\Users\User\Documents\COG Modding Stuff\ffmpeg-8.1-full_build`
   `C:\Users\User\Documents\Modding Programs\ProcDump\procdump.exe`
6. `Things To Have` as the local comparison root
7. After Prohibition as the primary `StreamingAssets` project
8. native-crash workflow:
   use ProcDump when logs only show hard closes
   treat repeated `406D1388` as thread-name noise
   `C00000FD.STACK_OVERFLOW` plus `C0000005.ACCESS_VIOLATION` strongly suggests recursion or re-entry
   use dump results to steer code-side hardening, not just more log splitting

Do not mirror back Visual Studio UI-only guidance or `rg` assumptions.

Copy target when writable access is available:

1. `C:\Users\User\.codex\skills\city-of-gangsters-mod-builder-visual-studio\SKILL.md`

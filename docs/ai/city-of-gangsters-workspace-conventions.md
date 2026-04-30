# City of Gangsters Workspace Conventions

Use this file as the shared workspace workflow reference for the City of Gangsters skills.

## Workspace Rules

1. Treat the current workspace root containing `ClassLibrary1.sln` as the repo root.
2. Prefer shell commands and file paths that work in Visual Studio, VS Code, and Cursor.
3. Do not depend on editor-specific task UIs when a normal terminal command works.
4. Prefer targeted builds before solution-wide builds.
5. Use live SomaSim app-data logs for runtime triage.
6. Treat `Current After Prohibition Mod` as the main authored `StreamingAssets` project.
7. Keep the local decompiler and video tooling paths in mind:
   `C:\Users\User\Documents\Modding Programs\DNSPEX`
   `C:\Users\User\Documents\COG Modding Stuff\ffmpeg-8.1-full_build`
8. Use `C:\Users\User\source\repos\ClassLibrary1\Things To Have` as the local `StreamingAssets` comparison root.

## Search And Inspection

Prefer:

```powershell
rg -n "<pattern>" GameplayTweaks GameOptimizer CopKilling -S
```

If `rg` is unavailable, use:

```powershell
Get-ChildItem GameplayTweaks,GameOptimizer,CopKilling -Recurse -File | Select-String -Pattern "<pattern>"
```

For `StreamingAssets` and mod-data work, also inspect:

```powershell
Get-ChildItem "C:\Users\User\source\repos\ClassLibrary1\Things To Have" -Force
Get-ChildItem "$env:ProgramFiles(x86)\Steam\steamapps\common\City of Gangsters\CoG_Data\StreamingAssets" -Force
Get-ChildItem "$env:ProgramFiles(x86)\Steam\steamapps\common\City of Gangsters\CoG_Data\StreamingAssets\Entities" -Force
Get-ChildItem "$env:ProgramFiles(x86)\Steam\steamapps\common\City of Gangsters\CoG_Data\StreamingAssets\Settings" -Force
Get-ChildItem "$env:ProgramFiles(x86)\Steam\steamapps\common\City of Gangsters\CoG_Data\StreamingAssets\Maps" -Force
```

For decompiled game inspection, prefer the local decompiler install:

```powershell
Get-ChildItem "C:\Users\User\Documents\Modding Programs\DNSPEX" -Force
```

Use dnSpy console as the live-signature fallback when checked-in decompiled files might be stale or incomplete, especially for Harmony targets, reflection overloads, private methods, and metadata-token checks:

```powershell
& "C:\Users\User\Documents\Modding Programs\DNSPEX\dnSpy.Console.exe" -t Game.Session.Entities.BizComponent "<Assembly-CSharp.dll>"
& "C:\Users\User\Documents\Modding Programs\DNSPEX\dnSpy.Console.exe" --md 0x06000123 "<Assembly-CSharp.dll>"
```

Prefer targeted type/member decompiles to a temporary output folder over broad re-decompiles unless a whole assembly comparison is needed.

For native crash capture, prefer the local ProcDump install:

```powershell
& "C:\Users\User\Documents\Modding Programs\ProcDump\procdump.exe" -e -ma -t CoG "C:\Users\User\Documents\CrashDumps"
```

Interpret ProcDump output carefully:

1. Repeated `406D1388` lines are usually benign thread-name exceptions.
2. `C00000FD.STACK_OVERFLOW` followed by `C0000005.ACCESS_VIOLATION` points to runaway recursion or re-entry before the final native crash.
3. Open the resulting dump in Visual Studio with `Debug with Native Only`.

For video trimming/snipping when reviewing bug captures, prefer the local ffmpeg build:

```powershell
Get-ChildItem "C:\Users\User\Documents\COG Modding Stuff\ffmpeg-8.1-full_build\bin" -Force
```

## Live Logs

Use the current user profile's live logs:

1. `%USERPROFILE%\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
2. `%USERPROFILE%\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`

Example:

```powershell
Get-Content "$env:USERPROFILE\AppData\LocalLow\SomaSim\City of Gangsters\Player.log" -Tail 200
```

## Build Defaults

Prefer:

```powershell
dotnet build GameplayTweaks/GameplayTweaks.csproj -c Release
```

Build the full solution only when shared contracts, shared helpers, or cross-project callers changed:

```powershell
dotnet build ClassLibrary1.sln -c Release
```

## Skill Boundaries

1. `city-of-gangsters-mod-builder` owns implementation, debugging, validation, Harmony patching, and runtime triage.
2. `city-of-gangsters-next-step-advisor` owns prioritization and deciding what should happen next.
3. `city-of-gangsters-map-maker` owns exported-map authoring, `Data.txt`, `Preview.png`, and map pipeline explanation.
4. Both mod-builder skills should remember that After Prohibition is the main `StreamingAssets` project and keep donor/reference distinctions clear.

## Validation Defaults

1. Validate the narrowest changed surface first.
2. Re-check live logs when a task was driven by runtime symptoms.
3. Report remaining runtime risk explicitly when in-game verification was not possible.

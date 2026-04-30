# Skill: mod-dev — City of Gangsters BepInEx Mod Development

Use this skill when creating new mods, adding features to existing mods, writing Harmony patches, or working with the game's internal APIs.

## Before Writing Any Code

1. **Read the target mod's plugin file** to understand its current structure, dependencies, and initialization.
2. **Check `decompiled/`** for the game type you need to patch — find the exact class name, method signature, and field types before writing any patch.
3. **Check GameplayTweaks first** — it likely already has a utility, helper, or shared state for what you need. Don't duplicate.
4. **Check if the feature touches save data** — if so, integrate with `ModSaveData` in GameplayTweaks rather than creating a new save system.

## Project Scaffolding (New Mod)

When creating a new mod project:

1. Create a new folder at the solution root: `NewModName/`
2. Create the `.csproj` using .NET Framework 4.7.2 (old-style, not SDK-style):
   - Copy structure from `CopKilling/CopKilling.csproj` as a template
   - Set unique `<ProjectGuid>`, `<RootNamespace>`, `<AssemblyName>`
   - Reference BepInEx, 0Harmony, Assembly-CSharp, UnityEngine, UnityEngine.CoreModule from the game install path
   - If depending on GameplayTweaks, add a reference to `GameplayTweaks.dll` from its bin output
   - All game/BepInEx references use `<Private>false</Private>`
3. Create the main plugin file `NewModNamePlugin.cs`:
   ```csharp
   using BepInEx;
   using HarmonyLib;

   namespace NewModName
   {
       [BepInPlugin("com.mods.newmodname", "New Mod Name", "1.0.0")]
       public class NewModNamePlugin : BaseUnityPlugin
       {
           private void Awake()
           {
               var harmony = new Harmony("com.mods.newmodname");
               harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
               Logger.LogInfo("NewModName mod loaded.");
           }
       }
   }
   ```
4. Add the project to `ClassLibrary1.sln`
5. Update CLAUDE.md solution structure table

## Harmony Patching Patterns

### Standard attribute-based patch (preferred for simple cases)
```csharp
[HarmonyPatch]
static class SomeFeaturePatch
{
    static MethodBase TargetMethod()
    {
        // Use reflection to find the target — game types are often internal
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "Assembly-CSharp");
        var type = asm.GetTypes().First(t => t.Name == "TargetClassName");
        return AccessTools.Method(type, "TargetMethodName");
    }

    static void Postfix(object __instance, ref SomeType __result)
    {
        // Modify behavior after original runs
    }
}
```

### Manual patch (preferred for complex or conditional patches)
```csharp
internal static class SomeSystem
{
    public static void ApplyPatch(Harmony harmony)
    {
        var targetMethod = AccessTools.Method(typeof(SomeClass), "SomeMethod");
        var prefix = new HarmonyMethod(typeof(SomeSystem), nameof(SomePrefix));
        harmony.Patch(targetMethod, prefix: prefix);
    }

    private static bool SomePrefix(/* original params */)
    {
        // return false to skip original, true to continue
        return true;
    }
}
```

### Key rules
- Use `TargetMethod()` with reflection when the type is internal/private
- Use `AccessTools` for finding methods, fields, and properties
- Prefix returns `bool` — `false` skips the original method
- Postfix can use `ref __result` to modify return values
- Use `__instance` to access the patched object
- Transpilers are a last resort — prefer prefix/postfix

## Accessing Game Internals via Reflection

The game's `Game.Game` class conflicts with the `Game` namespace. Use the `G` helper pattern:

```csharp
internal static class G
{
    private static readonly Type GameType;
    private static readonly FieldInfo CtxField;

    static G()
    {
        GameType = typeof(GameClock).Assembly.GetType("Game.Game");
        if (GameType != null)
            CtxField = GameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static);
    }

    public static dynamic ctx => CtxField?.GetValue(null);
}
```

Access game state through `G.ctx`:
- `G.ctx.simman` — simulation manager
- `G.ctx.players` — player manager
- `G.ctx.clock` — game clock

Cache reflection lookups in static fields — never reflect inside hot loops.

## Save Data Integration

If your feature needs persistent state, add fields to `ModSaveData` in GameplayTweaks:

1. Add serializable fields/properties to `ModSaveData`
2. Initialize defaults in the constructor or `OnNewGame()`
3. Access via `GameplayTweaksPlugin.SaveData`
4. The existing `SaveLoadPatch` handles JSON serialization automatically

Do NOT create separate save files per mod.

## Inter-Mod Dependencies

```csharp
// Hard dependency — mod won't load without it
[BepInDependency("com.mods.gameplaytweaks")]

// Soft dependency — mod loads regardless
[BepInDependency("com.mods.modlauncher", BepInDependency.DependencyFlags.SoftDependency)]
```

If you depend on GameplayTweaks, also add `[assembly: InternalsVisibleTo("YourModName")]` in GameplayTweaks to access internal types.

## UI Modification

When modifying Unity UI elements:
- Use `Traverse.Create(__instance)` to access private fields
- Use `GameObject.Find()` or traverse the hierarchy to locate UI elements
- TMPro (`TextMeshProUGUI`) is used for text — import `TMPro` namespace
- For new UI elements, instantiate from existing prefabs when possible

## Configuration

Use BepInEx ConfigEntry for user-configurable values:

```csharp
internal static ConfigEntry<bool> EnableFeature;
internal static ConfigEntry<int> SomeValue;

private void Awake()
{
    EnableFeature = Config.Bind("General", "EnableFeature", true, "Description");
    SomeValue = Config.Bind("General", "SomeValue", 10, "Description");
}
```

## Conventions Checklist

- [ ] Tab indentation (not spaces)
- [ ] Debug logging: `Debug.Log("[ModName] message")` or `Logger.LogInfo("message")`
- [ ] Plugin ID format: `com.mods.modname`
- [ ] Namespace matches folder name
- [ ] All game DLL references are `<Private>false</Private>`
- [ ] Decompiled source consulted before writing patches
- [ ] No duplicated utilities — reuse from GameplayTweaks where possible

## Build & Deploy

```bash
# Build all mods
dotnet build ClassLibrary1.sln

# Lock-hardened build
powershell -ExecutionPolicy Bypass -File scripts/build-mods.ps1 -Configuration Release

# Sync to game plugins folder
powershell -ExecutionPolicy Bypass -File scripts/sync-stablecore-plugins.ps1
```

Output DLLs go to `bin\Debug\` or `bin\Release\` per project. The game loads plugins only from:
`C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\BepInEx\plugins`

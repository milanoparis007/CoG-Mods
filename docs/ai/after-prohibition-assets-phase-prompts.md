# AfterProhibitionAssets Phase Prompts

Use this plan to split the external image/custom-assets work out of `GameplayTweaks` and into a separate BepInEx plugin. Each phase is intentionally narrow so the plugin can be built, staged, and tested without destabilizing gameplay patches.

## Goal

Create a standalone `AfterProhibitionAssets` plugin that can:

- load PNG/JPG images from its own plugin folder;
- expose images by stable keys;
- force a configured main menu image to show first;
- optionally fall back to default game resources;
- later integrate or replace the existing COG custom assets DLL.

Recommended release layout:

```txt
BepInEx/
  plugins/
    AfterProhibitionAssets.dll
    AfterProhibitionAssets/
      images.txt
      images/
        main_menu.png
```

## Phase 1 - Standalone Plugin Scaffold

Prompt:

```txt
Create a new standalone BepInEx plugin project named AfterProhibitionAssets in this repo. Keep it separate from GameplayTweaks. Add a minimal plugin class, its own Harmony ID, a small logger helper, and a targeted Release build path. Do not add gameplay patches yet. Stage the built DLL under Things To Have\Current After Prohibition Mod\BepInEx\plugins or the repo's current Personal/Public release plugin folders, following existing packaging conventions.

Validation:
- dotnet build AfterProhibitionAssets\AfterProhibitionAssets.csproj -c Release
- Confirm the DLL builds without warnings/errors.
- Confirm the plugin has no dependency on GameplayTweaks.
```

Expected files:

```txt
AfterProhibitionAssets/
  AfterProhibitionAssets.csproj
  AfterProhibitionAssetsPlugin.cs
```

## Phase 2 - Image Folder and Manifest Loader

Prompt:

```txt
Implement a simple image manifest for AfterProhibitionAssets. Load images.txt from BepInEx/plugins/AfterProhibitionAssets/images.txt. Support key=value entries, comments starting with #, and relative paths under the AfterProhibitionAssets folder. Load PNG/JPG files into Texture2D with LoadImage, create Sprites, and cache them by key. Add clear logs for loaded images, missing files, duplicate keys, and invalid lines. Do not patch any UI yet.

Validation:
- Build the project.
- Add a sample images.txt and sample placeholder path.
- Confirm missing/invalid manifest entries log warnings but do not crash.
```

Expected files:

```txt
AfterProhibitionAssets/
  AssetRegistry.cs
  ImageManifest.cs
```

Example manifest:

```txt
# key=relative path
main-menu.primary=images/main_menu.png
```

## Phase 3 - Main Menu Image Override

Prompt:

```txt
Patch the City of Gangsters main menu UI from AfterProhibitionAssets so the configured image key main-menu.primary is applied as the menu background. The patch should run after the main menu is shown/refreshed and include a short deferred retry so vanilla or another mod cannot immediately overwrite it. If the external PNG is missing, fall back to vanilla behavior. Keep this patch isolated to main menu UI only.

Validation:
- Build the project.
- Run the game with a test main_menu.png.
- Confirm the custom image appears first on the main menu.
- Confirm missing image falls back safely with a warning, not a crash.
```

Expected files:

```txt
AfterProhibitionAssets/
  MainMenuImagePatch.cs
```

## Phase 4 - Default Game Resource Fallback

Prompt:

```txt
Extend AfterProhibitionAssets so manifest entries can reference default game resources using game:<resource-path>. Resolve these with Resources.Load<Texture2D> or Resources.Load<Sprite>, depending on what the base game exposes. Add diagnostics that list whether an asset came from file, game resource, or fallback. Use this to support a default main menu picture from the base game resources when no custom PNG is provided.

Validation:
- Identify and document the exact resource path used for the desired default main menu picture.
- Confirm game:<resource-path> loads.
- Confirm main-menu.primary can point to either a file path or game resource.
```

Example manifest:

```txt
main-menu.primary=game:MainMenu/default_background
main-menu.custom=images/main_menu.png
```

## Phase 5 - Public Asset API

Prompt:

```txt
Add a tiny public API to AfterProhibitionAssets so other mods can query loaded assets without taking a hard compile-time dependency. Provide public static methods such as TryGetSprite(string key, out Sprite sprite), TryGetTexture(string key, out Texture2D texture), and Reload(). Keep the API safe if called before the plugin finishes loading. Document the reflection call pattern for GameplayTweaks or future plugins.

Validation:
- Build the project.
- Add logs showing registry readiness and asset counts.
- Confirm API calls return false instead of throwing when a key is missing.
```

Optional reflection pattern:

```csharp
Type api = Type.GetType("AfterProhibitionAssets.AssetRegistry, AfterProhibitionAssets");
```

## Phase 6 - Packaging and User Guide

Prompt:

```txt
Add release packaging for AfterProhibitionAssets. Stage the DLL and default folder structure into the repo's Personal/Public release folders. Add a short plain-text guide explaining where to put PNG/JPG files, how to edit images.txt, how to force the main menu image, and how to disable the override by removing/commenting main-menu.primary.

Validation:
- Build and stage the DLL.
- Confirm release folders contain AfterProhibitionAssets.dll, AfterProhibitionAssets/images.txt, and AfterProhibitionAssets/images/.
- Confirm the guide is plain text and concise.
```

Expected release files:

```txt
BepInEx/plugins/AfterProhibitionAssets.dll
BepInEx/plugins/AfterProhibitionAssets/images.txt
BepInEx/plugins/AfterProhibitionAssets/images/
```

## Phase 7 - Existing COG Custom Assets DLL Bridge

Prompt:

```txt
Investigate the existing COG custom assets DLL. Use dnSpy or local source if available to identify what it loads, where it stores assets, and whether it exposes public methods. Add an optional compatibility bridge in AfterProhibitionAssets only if it can be done without a hard dependency. Prefer importing or recreating useful behavior over requiring the DLL. Log whether the bridge is unavailable, detected, or active.

Validation:
- No crash when the external DLL is absent.
- No hard compile dependency unless explicitly approved.
- Document which features were absorbed, bridged, or skipped.
```

## Phase 8 - Future Consumers

Prompt:

```txt
Prepare future integration points for other plugins to consume AfterProhibitionAssets. Do not migrate gameplay systems yet. Add examples for main menu, loading screen, custom icons, and future portrait replacement. Keep this as documentation and a small API test only.

Validation:
- Build remains clean.
- No GameplayTweaks dependency is introduced.
- Asset system still works standalone.
```

## Stability Rules

- `AfterProhibitionAssets` must not patch economy, routes, family, politics, or AI systems.
- Missing images must warn and fall back, never crash.
- Manifest reloads should replace cached custom assets cleanly.
- Game resource fallback should be optional.
- External COG custom assets DLL support should be optional and soft-detected.
- Other plugins should use reflection or a small shared API package, not direct gameplay coupling.

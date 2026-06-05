# AfterProhibitionAssets

Standalone BepInEx plugin for After Prohibition image and asset integration.

Phase 3 provides the plugin scaffold, asset folder creation, a simple image manifest loader, and an optional main menu image override. Later phases add game-resource fallback and optional bridge support for the existing COG custom assets DLL.

Runtime folder:

```txt
BepInEx/plugins/AfterProhibitionAssets/
  images.txt
  images/
```

Manifest format:

```txt
# key=relative path
main-menu.primary=images/main_menu.png
main-menu.alt1=images/main_menu_alt1.png
main-menu.alt2=images/main_menu_alt2.png
```

Manifest paths must stay inside the `AfterProhibitionAssets` folder. Missing files, invalid lines, unsupported extensions, and duplicate keys log warnings without stopping the game.

The main menu override uses the `main-menu.primary` key. Optional rotating slots can be added with `main-menu.alt1` and `main-menu.alt2`. Each time the main menu opens, the plugin chooses one loaded slot and reapplies that same choice through its delayed retry window so the background does not flicker.
Leave all `main-menu.*` lines commented to keep vanilla behavior. If no main-menu slot is loaded, the plugin captures the embedded default main-menu sprite from the vanilla UI prefab and reapplies it after other menu patches.

Game resources can be referenced with `game:<resource path>` when the base game exposes the asset through `Resources.Load`.

Other plugins can query loaded assets by reflection without a compile-time dependency:

```csharp
Type api = Type.GetType("AfterProhibitionAssets.AssetRegistry, AfterProhibitionAssets");
```

Public methods include `TryGetSprite(string key, out Sprite sprite)`, `TryGetTexture(string key, out Texture2D texture)`, `Reload()`, `ContainsKey(string key)`, and `GetSourcePath(string key)`.

`CoGCustomAssetsBridge` soft-detects the existing CoG custom assets DLL by reflection. It reports whether the DLL is unavailable, detected, or active, and counts manifest/UI/main-menu assets when available. It does not decrypt or import CoGCustomAssets packed files and has no hard compile-time dependency on that DLL.

This project intentionally has no compile-time dependency on `GameplayTweaks`.

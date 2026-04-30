# AfterProhibitionPreloader

Drop these two files into `BepInEx\patchers`:

- `AfterProhibitionPreloader.dll`
- `AssemblyPatchManifest.json`

What it does:

- patches `Assembly-CSharp.dll` before the game loads
- adds missing static `ResourceConstants` label fields
- adds missing `VictorySettings` `Fixnum` fields
- lets future resource labels and goal fields be added by editing the manifest instead of shipping a hand-edited game DLL

Current built-in defaults mirror the custom After Prohibition assembly:

- `COUNTERFEITCASH -> counterfeitcash`
- `DIRTYCASH -> dirty-cash`
- `STOLENGOODS -> stolenelectronics`
- `CANNABIS -> cannabis-pound`
- `COCAINE -> cocaine-tiles`
- `HEROIN -> heroin-packs`
- `HOMES -> home-mid`
- `JAZZ -> bandlow`
- `amtDirtyCash`
- `amtStolenGoods`
- `amtCounterfeit`

To add more later:

1. add another entry to `labelFields` if a plugin or core code expects a new `ResourceConstants` field
2. add another entry to `fixnumFields` if `Settings.sim` needs a new numeric field on `VictorySettings`
3. rebuild the project or edit the copied manifest beside the DLL

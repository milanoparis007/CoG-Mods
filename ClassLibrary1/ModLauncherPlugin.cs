using BepInEx;
using BepInEx.Logging;

namespace ModLauncher
{
    [BepInPlugin("com.mods.modlauncher", "Prohibition Launcher Bridge", "1.0.0")]
    public class ModLauncherPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            Log.LogInfo("Prohibition Launcher Bridge loaded (GUID com.mods.modlauncher).");
        }
    }
}

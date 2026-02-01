using BepInEx;
using BepInEx.Logging;

namespace ModLauncher
{
    [BepInPlugin("com.mods.modlauncher", "Mod Launcher", "1.0.0")]
    public class ModLauncherPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            Log.LogInfo("Mod Launcher loaded.");
        }
    }
}

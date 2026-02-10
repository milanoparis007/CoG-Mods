using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BossBuildings
{
    [BepInPlugin("com.mods.bossbuildings", "Boss Buildings", "1.0.0")]
    [BepInDependency("com.mods.modlauncher")]
    public class BossBuildingsPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            var harmony = new Harmony("com.mods.bossbuildings");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Boss Buildings mod loaded. Boss can now be assigned to buildings.");
        }
    }

    [HarmonyPatch]
    static class BossBuildingPatch
    {
        static MethodBase TargetMethod()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "Assembly-CSharp");
            var outer = asm.GetTypes().First(t => t.Name == "CrewManagementPopup");
            var inner = outer.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                .First(t => t.Name == "ButtonStatus");
            return AccessTools.Method(inner, "Update");
        }

        static void Postfix(object __instance)
        {
            var traverse = Traverse.Create(__instance);
            bool isBoss = traverse.Field("buildingFailIsBoss").GetValue<bool>();
            bool isInBuilding = traverse.Field("isInBuilding").GetValue<bool>();
            if (isBoss && !isInBuilding)
            {
                bool noBuildings = traverse.Field("buildingFailNoneLeft").GetValue<bool>();
                bool canShow = traverse.Field("canShowButtonsBuilding").GetValue<bool>();
                if (canShow && !noBuildings)
                {
                    traverse.Field("canAddToBuilding").SetValue(true);
                    traverse.Field("buildingFailIsBoss").SetValue(false);
                }
            }
        }
    }
}

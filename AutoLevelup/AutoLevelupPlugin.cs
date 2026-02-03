using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Game.Session.Entities;
using HarmonyLib;
using UnityEngine;

namespace AutoLevelup
{
    [BepInPlugin("com.mods.autolevelup", "Auto Levelup", "1.0.0")]
    public class AutoLevelupPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> Enabled;

        private void Awake()
        {
            Enabled = Config.Bind("AutoLevelup", "Enabled", true,
                "Automatically reopen the levelup screen when a crew member still has enough XP for another levelup.");

            var harmony = new Harmony("com.mods.autolevelup");
            harmony.PatchAll(typeof(GrantLevelupPatch));

            Logger.LogInfo($"Auto Levelup loaded. Enabled: {Enabled.Value}");
        }

        [HarmonyPatch(typeof(AgentComponent), "GrantLevelup")]
        private static class GrantLevelupPatch
        {
            [HarmonyPostfix]
            static void Postfix(AgentComponent __instance)
            {
                if (!Enabled.Value) return;

                try
                {
                    if (__instance.CanShowLevelupPopup())
                    {
                        __instance.ShowLevelupPopup();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AutoLevelup] GrantLevelupPatch error: {e}");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace CopKilling
{
    [BepInPlugin("com.mods.copkilling", "Cop Killing", "1.0.0")]
    [BepInDependency("com.mods.gameplaytweaks")]
    public class CopKillingPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("com.mods.copkilling");
            CopWarSystem.Initialize();
            CopWarSystem.ApplyPatch(harmony);
            CombatNameDisplayPatch.ApplyPatch(harmony);
            Logger.LogInfo("CopKilling mod loaded.");
        }
    }

    internal static class CopWarSystem
    {
        private static Type _copUtilType;
        private static MethodInfo _isCopMethod;
        private static bool _initialized;

        public static bool IsCopWarActive => GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive;

        public static void Initialize()
        {
            try
            {
                _copUtilType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CopUtil");
                if (_copUtilType != null)
                {
                    _isCopMethod = _copUtilType.GetMethod("IsCop", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(Entity) }, null);
                    _initialized = true;
                    Debug.Log("[CopKilling] CopWarSystem initialized");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CopWarSystem init failed: {ex}");
            }
        }

        public static bool IsCop(Entity peep)
        {
            if (!_initialized || _isCopMethod == null || peep == null)
                return false;
            try
            {
                return (bool)_isCopMethod.Invoke(null, new object[] { peep });
            }
            catch
            {
                return false;
            }
        }

        public static float GetWitnessChanceForVictim(Entity victim)
        {
            return IsCop(victim) ? 0.5f : 0.3f;
        }

        public static void OnCopKilledWithWitness(Entity killer)
        {
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            saveData.CopWarActive = true;
            saveData.CopWarWitnessCount++;
            saveData.LastCopKillDay = GameplayTweaks.G.GetNow().days;

            string killerName;
            if (killer?.data?.person != null)
                killerName = killer.data.person.FullName;
            else
                killerName = "Unknown";

            Debug.Log("[CopKilling] COP WAR: " + killerName + " killed a cop with a witness! War is now active.");
            GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP WAR: Police are hunting " + killerName + " after witnessed cop killing!");
        }

        public static bool CanCallCopTruce()
        {
            if (!GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive)
                return false;
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null)
                    return false;

                foreach (PlayerInfo player in GameplayTweaks.G.GetAllPlayers())
                {
                    if (player == null) continue;
                    PropertyInfo isJustCopProp = player.GetType().GetProperty("IsJustCop");
                    if (isJustCopProp == null || !(bool)isJustCopProp.GetValue(player))
                        continue;

                    MemberInfo aiMember = (MemberInfo)player.GetType().GetField("ai")
                        ?? (MemberInfo)player.GetType().GetProperty("ai")?.GetGetMethod();
                    object aiObj = null;
                    if (aiMember is FieldInfo fi)
                        aiObj = fi.GetValue(player);
                    else if (aiMember is PropertyInfo pi)
                        aiObj = pi.GetValue(player);
                    if (aiObj == null) continue;

                    object precinctObj = null;
                    PropertyInfo precinctProp = aiObj.GetType().GetProperty("precinct");
                    if (precinctProp != null)
                        precinctObj = precinctProp.GetValue(aiObj);
                    else
                    {
                        FieldInfo precinctField = aiObj.GetType().GetField("precinct");
                        if (precinctField != null)
                            precinctObj = precinctField.GetValue(aiObj);
                    }
                    if (precinctObj == null) continue;

                    MethodInfo hasDonation = precinctObj.GetType().GetMethod("HasDonationFrom");
                    if (hasDonation != null)
                    {
                        object result = hasDonation.Invoke(precinctObj, new object[] { humanPlayer.PID });
                        if (result != null && (int)result == 2)
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CanCallCopTruce failed: {ex}");
            }
            return false;
        }

        public static bool CallCopTruce()
        {
            if (!CanCallCopTruce())
                return false;
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            saveData.CopWarActive = false;
            saveData.CopWarWitnessCount = 0;
            Debug.Log("[CopKilling] COP TRUCE: Peace has been negotiated with the police.");
            GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: The heat is off - cops have been paid to look the other way.");
            return true;
        }

        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                Type combatType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
                if (combatType != null)
                {
                    MethodInfo performCombat = combatType.GetMethod("PerformCombat", BindingFlags.Static | BindingFlags.Public);
                    if (performCombat != null)
                    {
                        harmony.Patch(performCombat, null, new HarmonyMethod(typeof(CopWarSystem), "CombatPostfix", null), null, null, null);
                        Debug.Log("[CopKilling] Cop combat patch applied");
                    }
                    MethodInfo canAttack = combatType.GetMethod("CanAttackTarget", BindingFlags.Static | BindingFlags.NonPublic);
                    if (canAttack != null)
                    {
                        harmony.Patch(canAttack, null, new HarmonyMethod(typeof(CopWarSystem), "CanAttackTargetPostfix", null), null, null, null);
                        Debug.Log("[CopKilling] Cop attack target patch applied - cops can now be attacked!");
                    }
                    else
                    {
                        Debug.LogWarning("[CopKilling] Could not find CanAttackTarget method");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CopWarSystem patch failed: {ex}");
            }
        }

        private static void CanAttackTargetPostfix(PlayerID pid, EntityID target, ref bool __result)
        {
            try
            {
                if (__result || !pid.IsHumanPlayer)
                    return;
                Entity targetEntity = EntityIDExtensions.FindEntity(target);
                if (targetEntity == null)
                    return;
                PlayerID? targetPid = targetEntity.data?.agent?.pid;
                if (!targetPid.HasValue)
                    return;
                PlayerInfo targetPlayer = PlayerIDExtensions.FindPlayer(targetPid.Value);
                if (targetPlayer == null)
                    return;

                bool isCopOrFed = false;
                PropertyInfo prop = targetPlayer.GetType().GetProperty("IsCopOrFed");
                if (prop != null)
                    isCopOrFed = (bool)prop.GetValue(targetPlayer);
                else
                {
                    FieldInfo field = targetPlayer.GetType().GetField("IsCopOrFed");
                    if (field != null)
                        isCopOrFed = (bool)field.GetValue(targetPlayer);
                }
                if (!isCopOrFed)
                    return;

                CrewAssignment crewForPeep = targetPlayer.crew.GetCrewForPeep(target);
                Entity peep = crewForPeep.GetPeep();
                bool isAlive = peep?.data?.person?.IsAlive == true;
                bool isInVehicle = crewForPeep.IsInVehicle;
                if (isAlive && isInVehicle)
                {
                    __result = true;
                    string name = targetEntity.data?.person?.FullName ?? "Unknown";
                    Debug.Log("[CopKilling] Allowing attack on cop: " + name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CanAttackTargetPostfix error: {ex}");
            }
        }

        private static void CombatPostfix(object __result)
        {
            try
            {
                if (__result == null) return;

                FieldInfo attackerField = __result.GetType().GetField("attacker");
                FieldInfo targetField = __result.GetType().GetField("target");
                if (attackerField == null || targetField == null) return;

                object attacker = attackerField.GetValue(__result);
                object target = targetField.GetValue(__result);
                if (attacker == null || target == null) return;

                bool isDead = false;
                PropertyInfo isDeadProp = target.GetType().GetProperty("IsDead");
                if (isDeadProp != null)
                    isDead = (bool)isDeadProp.GetValue(target);
                else
                {
                    FieldInfo isDeadField = target.GetType().GetField("IsDead");
                    if (isDeadField != null)
                        isDead = (bool)isDeadField.GetValue(target);
                }
                if (!isDead) return;

                FieldInfo attackerPeepField = attacker.GetType().GetField("peep");
                FieldInfo targetPeepField = target.GetType().GetField("peep");
                if (attackerPeepField == null || targetPeepField == null) return;

                Entity attackerPeep = attackerPeepField.GetValue(attacker) as Entity;
                Entity targetPeep = targetPeepField.GetValue(target) as Entity;
                if (attackerPeep == null || targetPeep == null) return;

                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null) return;

                PlayerID? attackerPid = attackerPeep.data?.agent?.pid;
                PlayerID humanPid = humanPlayer.PID;
                if (!attackerPid.HasValue || attackerPid.Value != humanPid || !IsCop(targetPeep))
                    return;

                if (GameplayTweaks.GameplayTweaksPlugin.SharedRng.NextDouble() < 0.5)
                {
                    var crewState = GameplayTweaks.GameplayTweaksPlugin.GetOrCreateCrewState(attackerPeep.Id);
                    if (crewState != null)
                    {
                        crewState.HasWitness = true;
                        crewState.WitnessThreatenedSuccessfully = false;
                        crewState.WantedProgress = Mathf.Clamp01(crewState.WantedProgress + 0.35f);
                        if (crewState.WantedProgress >= 0.75f)
                            crewState.WantedLevel = GameplayTweaks.WantedLevel.High;
                        else if (crewState.WantedProgress >= 0.5f)
                            crewState.WantedLevel = GameplayTweaks.WantedLevel.Medium;
                        else if (crewState.WantedProgress >= 0.25f)
                            crewState.WantedLevel = GameplayTweaks.WantedLevel.Low;
                    }
                    OnCopKilledWithWitness(attackerPeep);
                }
                else
                {
                    Debug.Log("[CopKilling] " + attackerPeep.data.person.FullName + " killed a cop but no witness saw it!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CombatPostfix error: {ex}");
            }
        }
    }

    internal static class CombatNameDisplayPatch
    {
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                Type convoType = typeof(GameClock).Assembly.GetType("Game.UI.Session.Convo.ConvoDataCombat");
                if (convoType != null)
                {
                    MethodInfo method = convoType.GetMethod("MakeReplacements", BindingFlags.Instance | BindingFlags.Public);
                    if (method != null)
                    {
                        harmony.Patch(method, null, new HarmonyMethod(typeof(CombatNameDisplayPatch), "MakeReplacementsPostfix", null), null, null, null);
                        Debug.Log("[CopKilling] Combat name display patch applied");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CombatNameDisplayPatch failed: {ex}");
            }
        }

        private static void MakeReplacementsPostfix(object __instance, ref string[] __result)
        {
            try
            {
                FieldInfo targetField = __instance.GetType().GetField("target");
                if (targetField == null) return;

                object targetObj = targetField.GetValue(__instance);
                if (targetObj == null) return;

                MethodInfo getPeep = targetObj.GetType().GetMethod("GetPeep");
                if (getPeep == null) return;

                Entity peep = getPeep.Invoke(targetObj, null) as Entity;
                if (peep == null) return;

                string name = peep.data?.person?.FullName ?? "Unknown";
                string gang = "";

                PlayerID? pid = peep.data?.agent?.pid;
                if (pid.HasValue && !pid.Value.IsNotAnyPlayer)
                {
                    PlayerInfo player = PlayerIDExtensions.FindPlayer(pid.Value);
                    if (player != null)
                    {
                        gang = player.social?.PlayerGroupName ?? "";
                        PropertyInfo isJustCop = player.GetType().GetProperty("IsJustCop");
                        if (isJustCop != null && (bool)isJustCop.GetValue(player))
                            gang = "Police";
                    }
                }

                if (!string.IsNullOrEmpty(gang))
                    __result = new string[] { name, gang, name + " (" + gang + ")" };
                else
                    __result = new string[] { name, "", name };

                Debug.Log("[CopKilling] Combat target: " + name + " from " + gang);
            }
            catch { }
        }
    }
}

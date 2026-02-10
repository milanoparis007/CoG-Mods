using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace AICrewLevelup
{
    /// <summary>
    /// Allows AI crew members to automatically level up when they have enough XP.
    /// Without this mod, AI crew never gains abilities even with accumulated XP.
    /// </summary>
    [BepInPlugin("com.mods.aicrewlevelup", "AI Crew Levelup", "1.0.0")]
    public class AICrewLevelupPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxLevelupsPerCrewPerTurn;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Allow AI crew members to automatically level up when they have enough XP.");
            MaxLevelupsPerCrewPerTurn = Config.Bind("General", "MaxLevelupsPerCrewPerTurn", 3,
                "Maximum levelups a single AI crew member can gain per turn.");

            var harmony = new Harmony("com.mods.aicrewlevelup");
            harmony.PatchAll(typeof(AICrewLevelupPatch));

            Logger.LogInfo($"AI Crew Levelup loaded. Enabled: {Enabled.Value}, MaxPerTurn: {MaxLevelupsPerCrewPerTurn.Value}");
        }

        [HarmonyPatch(typeof(PlayerCrew), "OnPlayerTurnStarted")]
        private static class AICrewLevelupPatch
        {
            private static FieldInfo _pidField;
            private static FieldInfo _crewdataField;
            private static FieldInfo _rawcrewField;
            private static MethodInfo _hasXPToGainLevelupMethod;
            private static System.Random _rng = new System.Random();

            static AICrewLevelupPatch()
            {
                try
                {
                    _pidField = AccessTools.Field(typeof(PlayerSubmanager), "_pid");
                    _crewdataField = AccessTools.Field(typeof(PlayerCrew), "_crewdata");
                    if (_crewdataField != null)
                        _rawcrewField = AccessTools.Field(_crewdataField.FieldType, "rawcrew");
                    _hasXPToGainLevelupMethod = AccessTools.Method(typeof(AgentComponent), "HasXPToGainLevelup");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AICrewLevelup] Reflection setup failed: {e}");
                }
            }

            [HarmonyPostfix]
            static void Postfix(PlayerCrew __instance)
            {
                if (!Enabled.Value) return;

                try
                {
                    if (_pidField == null || _crewdataField == null ||
                        _rawcrewField == null || _hasXPToGainLevelupMethod == null)
                        return;

                    var pid = (PlayerID)_pidField.GetValue(__instance);
                    // Only process AI players, not the human player
                    if (pid.IsHumanPlayer) return;

                    var crewdata = _crewdataField.GetValue(__instance);
                    var rawcrew = (List<CrewAssignment>)_rawcrewField.GetValue(crewdata);
                    if (rawcrew == null) return;

                    int maxPerTurn = MaxLevelupsPerCrewPerTurn.Value;

                    foreach (var crew in rawcrew)
                    {
                        // Skip dead or unassigned crew
                        if (crew.IsDead || crew.IsNotAssigned) continue;

                        Entity peep = crew.GetPeep();
                        if (peep == null) continue;

                        AgentComponent agent = peep.components.agent;
                        if (agent == null) continue;

                        int levelsGained = 0;

                        // Process levelups while there's enough XP and we haven't hit the cap
                        while (levelsGained < maxPerTurn &&
                               (bool)_hasXPToGainLevelupMethod.Invoke(agent, null))
                        {
                            var available = agent.GetAvailableLevelups(false).ToList();
                            if (available.Count == 0) break;

                            // Randomly pick a levelup (AI doesn't have preferences)
                            LevelupDescription pick = available[_rng.Next(available.Count)];

                            XP xp = peep.data.agent.xp;
                            xp.lastThreshold++;
                            xp.SetLevelupLevel(pick.levelup.id, pick.nextLevel);

                            levelsGained++;
                        }

                        if (levelsGained > 0)
                        {
                            Debug.Log($"[AICrewLevelup] AI crew '{peep.data.person.FullName}' gained {levelsGained} levelup(s)");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AICrewLevelup] Error: {e}");
                }
            }
        }
    }
}

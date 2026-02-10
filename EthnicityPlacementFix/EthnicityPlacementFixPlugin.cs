using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim;
using UnityEngine;

namespace EthnicityPlacementFix
{
    /// <summary>
    /// Helper to access Game.Game.ctx via reflection (avoids namespace conflict with Game class).
    /// </summary>
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

    /// <summary>
    /// Fixes ethnicity placement bias: when ethnic heatmaps are empty/tied, the game picks
    /// the first alphabetical ethnicity, causing one group to cluster near industrial zones.
    /// This mod randomly selects among tied ethnicities to create more natural distribution.
    /// </summary>
    [BepInPlugin("com.mods.ethnicityplacementfix", "Ethnicity Placement Fix", "1.0.0")]
    public class EthnicityPlacementFixPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> Enabled;

        private bool _hasRun = false;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Fix ethnicity placement bias by randomly selecting among tied ethnicities instead of alphabetical order.");

            if (Enabled.Value)
            {
                StartCoroutine(EthnicityPlacementFixCoroutine());
            }

            Logger.LogInfo($"Ethnicity Placement Fix loaded. Enabled: {Enabled.Value}");
        }

        private IEnumerator EthnicityPlacementFixCoroutine()
        {
            var rng = new System.Random();

            // Poll until the game session is interactive
            while (true)
            {
                yield return new WaitForSeconds(1f);

                try
                {
                    dynamic ctx = G.ctx;
                    if (ctx == null) continue;

                    // Check if game state is interactive (SessionState.Interactive == 9)
                    int state = (int)ctx.State;
                    if (state < 9) continue;

                    // Only run once per game session
                    if (_hasRun) yield break;
                    _hasRun = true;

                    // Only run on new games, not loaded saves
                    bool hasSave = ctx.HasSaveFile;
                    if (hasSave)
                    {
                        Debug.Log("[EthnicityPlacementFix] Skipping loaded save");
                        yield break;
                    }

                    List<Label> ethnicities = ctx.session.mapconfig.GetEthnicitiesUniqueSorted();
                    if (ethnicities == null || ethnicities.Count <= 1) yield break;

                    // Get all businesses via entity tag
                    var tagConstants = typeof(GameClock).Assembly.GetType("Game.Session.Entities.TagConstants");
                    if (tagConstants == null) yield break;
                    var tagField = tagConstants.GetField("TAG_BUSINESS_ALL", BindingFlags.Public | BindingFlags.Static);
                    if (tagField == null) yield break;
                    Label bizTag = (Label)tagField.GetValue(null);

                    dynamic entityman = ctx.entityman;
                    IEnumerable<Entity> allBiz = entityman.GetCachedEntitiesByTagUnsafe(bizTag);
                    int fixedCount = 0;

                    foreach (Entity biz in allBiz)
                    {
                        // Only process fake owners (not real player-owned businesses)
                        if (biz.data.biz.owner.IsReal) continue;
                        if (!biz.data.biz.owner.IsFake) continue;

                        WorldPos pos = biz.data.board.worldpos;

                        float bestValue = float.NegativeInfinity;
                        var candidates = new List<Label>();

                        // Find all ethnicities tied for highest heatmap value at this location
                        foreach (Label eth in ethnicities)
                        {
                            dynamic heatmap = ctx.heatmaps.FindEthnicityMap(eth);
                            float val = (float)heatmap.GetValueSafe(pos);

                            if (val > bestValue)
                            {
                                bestValue = val;
                                candidates.Clear();
                                candidates.Add(eth);
                            }
                            else if (val == bestValue)
                            {
                                candidates.Add(eth);
                            }
                        }

                        // If there's a tie, randomly select instead of using alphabetical order
                        if (candidates.Count > 1)
                        {
                            Label newEth = candidates[rng.Next(candidates.Count)];
                            biz.components.biz.AssignFakeOwner(newEth);
                            fixedCount++;
                        }
                    }

                    Debug.Log($"[EthnicityPlacementFix] Reassigned {fixedCount} fake business owners with random tiebreaking");
                    yield break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EthnicityPlacementFix] Error: {e}");
                    yield break;
                }
            }
        }
    }
}

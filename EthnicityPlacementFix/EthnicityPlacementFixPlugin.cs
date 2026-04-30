using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Game;
using Game.Core;
using Game.Session;
using Game.Session.Entities;
using Game.Session.Heatmaps;

namespace EthnicityPlacementFix
{
	[BepInPlugin("com.mods.ethnicityplacementfix", "Ethnicity Placement Fix", "1.0.0")]
	public sealed class EthnicityPlacementFixPlugin : BaseUnityPlugin
	{
		private static readonly MethodInfo FindEthnicityMapMethod = typeof(HeatmapManager).GetMethod("FindEthnicityMap", BindingFlags.Instance | BindingFlags.NonPublic);

		private ConfigEntry<bool> _enabled;

		private bool _hasRun;

		private bool _disabledByGameOptimizer;

		private void Awake()
		{
			_enabled = Config.Bind("General", "Enabled", defaultValue: true, "Fix ethnicity placement bias by randomly selecting among tied ethnicities instead of alphabetical order.");
			Logger.LogInfo($"Ethnicity Placement Fix loaded. Enabled: {_enabled.Value}");
		}

		private void Update()
		{
			if (!_enabled.Value || _hasRun || _disabledByGameOptimizer)
			{
				return;
			}

			if (!_disabledByGameOptimizer && IsGameOptimizerLoaded())
			{
				_disabledByGameOptimizer = true;
				_hasRun = true;
				Logger.LogInfo("Game Optimizer is loaded; standalone Ethnicity Placement Fix is disabled to avoid duplicate reassignment logic.");
				return;
			}

			SessionContext ctx = global::Game.Game.ctx;
			if (ctx == null || ctx.State < SessionState.Interactive)
			{
				return;
			}

			_hasRun = true;

			try
			{
				if (ctx.HasSaveFile)
				{
					Logger.LogInfo("Skipping loaded save.");
					return;
				}

				int reassigned = ReassignFakeOwners(ctx);
				Logger.LogInfo($"Reassigned {reassigned} fake business owners with random tiebreaking");
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error: {ex}");
			}
		}

		private static bool IsGameOptimizerLoaded()
		{
			if (Chainloader.PluginInfos.ContainsKey("com.mods.gameoptimizer"))
			{
				return true;
			}

			return AppDomain.CurrentDomain.GetAssemblies().Any(delegate(Assembly assembly)
			{
				return string.Equals(assembly.GetName().Name, "GameOptimizer", StringComparison.OrdinalIgnoreCase);
			});
		}

		private static int ReassignFakeOwners(SessionContext ctx)
		{
			if (ctx == null || FindEthnicityMapMethod == null || ctx.heatmaps == null)
			{
				return 0;
			}

			List<Label> ethnicities = ctx.session?.mapconfig?.GetEthnicitiesUniqueSorted();
			if (ethnicities == null || ethnicities.Count <= 1)
			{
				return 0;
			}

			HashSet<Entity> businesses = ctx.entityman?.GetCachedEntitiesBizUnsafe();
			if (businesses == null || businesses.Count == 0)
			{
				return 0;
			}

			System.Random rng = new System.Random();
			List<Label> tiedEthnicities = new List<Label>(ethnicities.Count);
			int reassigned = 0;

			foreach (Entity business in businesses)
			{
				if (business == null || business.data == null || business.components == null || business.components.biz == null)
				{
					continue;
				}

				BizOwner owner = business.data.biz.owner;
				if (owner.IsReal || !owner.IsFake)
				{
					continue;
				}

				WorldPos worldPos = business.data.board.worldpos;
				float bestScore = float.NegativeInfinity;
				tiedEthnicities.Clear();

				foreach (Label ethnicity in ethnicities)
				{
					Heatmap heatmap = (Heatmap)FindEthnicityMapMethod.Invoke(ctx.heatmaps, new object[] { ethnicity });
					if (heatmap == null)
					{
						continue;
					}

					float score = heatmap.GetValueSafe(worldPos);
					if (score > bestScore)
					{
						bestScore = score;
						tiedEthnicities.Clear();
						tiedEthnicities.Add(ethnicity);
					}
					else if (score == bestScore)
					{
						tiedEthnicities.Add(ethnicity);
					}
				}

				if (tiedEthnicities.Count > 1)
				{
					Label replacement = tiedEthnicities[rng.Next(tiedEthnicities.Count)];
					business.components.biz.AssignFakeOwner(replacement);
					reassigned++;
				}
			}

			return reassigned;
		}
	}
}

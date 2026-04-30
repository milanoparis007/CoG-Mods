using System;
using System.Reflection;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class JailedManagerGuardPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo method = typeof(ModulesUtil).GetMethod("GetManagerOrNull", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Entity) }, null);
				if (method == null)
				{
					return;
				}
				harmony.Patch(method, postfix: new HarmonyMethod(typeof(JailedManagerGuardPatch), nameof(Postfix)));
				VerificationLog("Compat", "manager jailed guard patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] JailedManagerGuardPatch failed: " + ex.Message);
			}
		}

		[HarmonyPostfix]
		internal static void Postfix(Entity building, ref ValueTuple<PlayerID, Entity> __result)
		{
			try
			{
				Entity manager = __result.Item2;
				if (manager == null || !manager.Id.IsValid || !IsCrewCurrentlyJailed(manager.Id))
				{
					return;
				}
				VerificationLog("Compat", $"manager-blocked reason=jailed building={building?.Id.id ?? 0UL} manager={manager.Id.id} source=ModulesUtil");
				__result = new ValueTuple<PlayerID, Entity>(__result.Item1, null);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] JailedManagerGuardPatch.Postfix: " + ex.Message);
			}
		}
	}

	internal static class AgentArrestHeatPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Entities.AgentComponent");
				if (type == null)
				{
					Debug.LogWarning("[GameplayTweaks] AgentComponent type not found for arrest heat patch");
					return;
				}
				MethodInfo method = type.GetMethod("RememberArrestAtThisTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method == null)
				{
					Debug.LogWarning("[GameplayTweaks] AgentComponent.RememberArrestAtThisTime not found for arrest heat patch");
					return;
				}
				harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(AgentArrestHeatPatch), nameof(RememberArrestAtThisTimePostfix)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				Debug.Log("[GameplayTweaks] Agent arrest heat patch applied");
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] AgentArrestHeatPatch failed: {arg}");
			}
		}

		private static void RememberArrestAtThisTimePostfix(AgentComponent __instance)
		{
			try
			{
				if (!GameplayTweaksPlugin.TryRaiseLocalHeatFromAgentArrest(__instance, out var peepId, out var node, out var nodeHeat, out var heatBefore, out var heatAfter, out var progressBefore, out var progressAfter))
				{
					return;
				}
				GameplayTweaksPlugin.VerificationLog("CornerHeatRaid", $"arrest-agent peep={peepId.id} node={node.id} heat={nodeHeat} heatBefore={heatBefore} heatAfter={heatAfter} progressBefore={progressBefore:0.00} progressAfter={progressAfter:0.00} witnessAdded=false federalWitnessAdded=false");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Agent arrest heat observer failed: " + ex.Message);
			}
		}
	}
}
}

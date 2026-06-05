using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using HarmonyLib;

namespace AfterProhibitionEconomy
{
	internal static class PlayerLegalBusinessConsumerRuntimePatch
	{
		private static readonly HashSet<string> LoggedConsumerUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedConsumerIdleStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				if (!AfterProhibitionEconomyPlugin.OwnsPlayerLegalBusinessConsumerMutation())
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo("player-legal-business-consumer runtime patch skipped owner=GameplayTweaks");
					return;
				}

				var method = AccessTools.Method(typeof(ConsumerModule), "DoUpdate");
				if (method == null)
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("player-legal-business-consumer runtime patch skipped reason=missing-ConsumerModule.DoUpdate");
					return;
				}

				harmony.Patch(
					method,
					postfix: new HarmonyMethod(typeof(PlayerLegalBusinessConsumerRuntimePatch), nameof(ConsumerModuleDoUpdatePostfix)));
				AfterProhibitionEconomyPlugin.Log?.LogInfo("player-legal-business-consumer runtime patch applied owner=AfterProhibitionEconomy");
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("player-legal-business-consumer runtime patch failed: " + ex.Message);
			}
		}

		private static void ConsumerModuleDoUpdatePostfix(ConsumerModule __instance, ModuleQuery q, bool initial, bool enabled, ModuleResult __result)
		{
			try
			{
				if (!AfterProhibitionEconomyPlugin.OwnsPlayerLegalBusinessConsumerMutation())
				{
					return;
				}

				IModuleConfig config = __instance?.ModuleConfig;
				PlayerLegalBusinessConsumerSummary summary = PlayerLegalBusinessConsumerClassifier.Classify(
					config,
					q.container,
					q.pid,
					initial,
					enabled,
					__result,
					consumeDays: 0,
					currentDay: 0,
					lastUpdateDay: 0);
				if (!summary.ShouldObserve)
				{
					return;
				}

				string updateKey = BuildUpdateLogKey(summary.ModuleId, initial, enabled, __result);
				if (!string.IsNullOrEmpty(summary.ModuleId) && LoggedConsumerUpdates.Add(updateKey))
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"player-legal-business-consumer observed id=" + summary.ModuleId +
						" qpid=" + q.pid +
						" buildingOwner=" + summary.PlayerID +
						" initial=" + initial +
						" enabled=" + enabled +
						" result=" + __result +
						" owner=AfterProhibitionEconomy");
				}

				AfterProhibitionEconomyPlugin.Log?.LogInfo("player-legal-business-consumer-runtime source=consumer-update " + summary.FormatBridgeSummary());
				LogConsumerIdleState(__instance, q, initial, enabled, __result);
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("player-legal-business-consumer runtime observation failed: " + ex.Message);
			}
		}

		private static void LogConsumerIdleState(ConsumerModule module, ModuleQuery q, bool initial, bool enabled, ModuleResult result)
		{
			if (module?.config?.sink == null || initial || !enabled || result != ModuleResult.Default)
			{
				return;
			}

			string moduleId = module.config.Id.String;
			if (string.IsNullOrEmpty(moduleId))
			{
				return;
			}

			int consumeDays = module.config.sink.ModConsumeDays(q, module);
			if (consumeDays <= 1)
			{
				return;
			}

			int currentDay = global::Game.Game.ctx?.clock?.Now.days ?? module.data.lastUpdate.days;
			int lastUpdateDay = module.data.lastUpdate.days;
			int daysSinceLast = Math.Max(currentDay - lastUpdateDay, 0);
			int daysUntilNext = Math.Max(consumeDays - daysSinceLast, 0);
			string logKey = moduleId + "|consumeDays=" + consumeDays + "|daysUntilNext=" + daysUntilNext;
			if (!LoggedConsumerIdleStates.Add(logKey))
			{
				return;
			}

			AfterProhibitionEconomyPlugin.Log?.LogInfo(
				"player-legal-business-consumer active-not-due id=" + moduleId +
				" consumeDays=" + consumeDays +
				" daysSinceLast=" + daysSinceLast +
				" daysUntilNext=" + daysUntilNext +
				" currentDay=" + currentDay +
				" lastUpdateDay=" + lastUpdateDay +
				" owner=AfterProhibitionEconomy");

			PlayerLegalBusinessConsumerSummary summary = PlayerLegalBusinessConsumerClassifier.Classify(
				q.container?.Id ?? EntityID.INVALID,
				moduleId,
				initial,
				enabled,
				result.ToString(),
				consumeDays,
				currentDay,
				lastUpdateDay);
			AfterProhibitionEconomyPlugin.Log?.LogInfo("player-legal-business-consumer-runtime source=consumer-idle " + summary.FormatBridgeSummary());
		}

		private static string BuildUpdateLogKey(string moduleId, bool initial, bool enabled, ModuleResult result)
		{
			return (moduleId ?? string.Empty) +
				"|initial=" + initial +
				"|enabled=" + enabled +
				"|result=" + result;
		}
	}
}

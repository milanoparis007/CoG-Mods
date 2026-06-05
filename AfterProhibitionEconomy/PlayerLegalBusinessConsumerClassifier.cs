using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace AfterProhibitionEconomy
{
	internal static class PlayerLegalBusinessConsumerClassifier
	{
		internal static PlayerLegalBusinessConsumerSummary Classify(EntityID buildingId, string moduleId)
		{
			Entity building = buildingId.IsValid ? buildingId.FindEntity() : null;
			IModuleConfig config = FindInstalledModuleConfig(building, moduleId);
			return Classify(config, building, GetControllingPlayer(building), initial: false, enabled: false, result: ModuleResult.Default, consumeDays: 0, currentDay: 0, lastUpdateDay: 0);
		}

		internal static PlayerLegalBusinessConsumerSummary Classify(
			EntityID buildingId,
			string moduleId,
			bool initial,
			bool enabled,
			string result,
			int consumeDays,
			int currentDay,
			int lastUpdateDay)
		{
			Entity building = buildingId.IsValid ? buildingId.FindEntity() : null;
			IModuleConfig config = FindInstalledModuleConfig(building, moduleId);
			ModuleResult parsedResult = ParseModuleResult(result);
			return Classify(config, building, GetControllingPlayer(building), initial, enabled, parsedResult, consumeDays, currentDay, lastUpdateDay);
		}

		internal static PlayerLegalBusinessConsumerSummary Classify(IModuleConfig config, Entity container, PlayerID pid, bool initial, bool enabled, ModuleResult result, int consumeDays, int currentDay, int lastUpdateDay)
		{
			PlayerLegalBusinessConsumerSummary summary = new PlayerLegalBusinessConsumerSummary
			{
				BuildingID = container?.Id ?? EntityID.INVALID,
				ModuleId = config?.Id.String ?? string.Empty,
				PlayerID = pid,
				Initial = initial,
				Enabled = enabled,
				Result = result.ToString(),
				ConsumeDays = Math.Max(consumeDays, 0),
				CurrentDay = currentDay,
				LastUpdateDay = lastUpdateDay,
				ExecutionOwner = AfterProhibitionEconomyPlugin.OwnsPlayerLegalBusinessConsumerMutation() ? "AfterProhibitionEconomy" : "GameplayTweaks",
				ClassificationOwner = "AfterProhibitionEconomy",
				MutationMovedToEconomy = AfterProhibitionEconomyPlugin.OwnsPlayerLegalBusinessConsumerMutation()
			};

			summary.IsConsumer = config is ConsumerModuleConfig;
			summary.IsPlayerLegalBusinessModule = IsPlayerLegalBusinessModule(config);
			summary.HumanOwned = pid.IsHumanPlayer || IsHumanOwnedBuilding(container);
			summary.ShouldObserve = summary.IsConsumer && summary.IsPlayerLegalBusinessModule && summary.HumanOwned;
			summary.DaysSinceLast = currentDay > 0 && lastUpdateDay > 0 ? Math.Max(currentDay - lastUpdateDay, 0) : 0;
			summary.DaysUntilNext = summary.ConsumeDays > 0 ? Math.Max(summary.ConsumeDays - summary.DaysSinceLast, 0) : 0;
			summary.IsIdleWaiting = summary.ShouldObserve
				&& !initial
				&& enabled
				&& result == ModuleResult.Default
				&& summary.ConsumeDays > 1
				&& summary.DaysUntilNext > 0;

			if (!summary.IsPlayerLegalBusinessModule)
			{
				summary.Reason = "not-player-legal-business-module";
			}
			else if (!summary.IsConsumer)
			{
				summary.Reason = "not-consumer-module";
			}
			else if (!summary.HumanOwned)
			{
				summary.Reason = "not-human-owned";
			}
			else if (summary.IsIdleWaiting)
			{
				summary.Reason = "consumer-active-not-due";
			}
			else
			{
				summary.Reason = "consumer-observed";
			}

			return summary;
		}

		private static IModuleConfig FindInstalledModuleConfig(Entity building, string moduleId)
		{
			if (building?.components?.modules == null || string.IsNullOrWhiteSpace(moduleId))
			{
				return null;
			}

			List<IModule> slots = building.components.modules.GetAllSlotsUnsafe();
			if (slots == null)
			{
				return null;
			}

			foreach (IModule slot in slots)
			{
				IModuleConfig config = slot?.ModuleConfig;
				if (string.Equals(config?.Id.String, moduleId, StringComparison.OrdinalIgnoreCase))
				{
					return config;
				}
			}

			return null;
		}

		private static PlayerID GetControllingPlayer(Entity building)
		{
			try
			{
				return building?.data?.building?.controlled.Get() ?? PlayerID.INVALID;
			}
			catch
			{
				return PlayerID.INVALID;
			}
		}

		private static bool IsHumanOwnedBuilding(Entity building)
		{
			try
			{
				return building?.data?.building != null && building.data.building.controlled.Get().IsHumanPlayer;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsPlayerLegalBusinessModule(IModuleConfig config)
		{
			if (config?.Common?.tags == null || config is ExplanationModuleConfig || config is VehicleModuleConfig)
			{
				return false;
			}

			string id = config.Id.String;
			if (string.IsNullOrEmpty(id) || !id.StartsWith("player-legal-", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return config.Common.tags.Contains((Label)"tag-player-legal-biz") &&
				(config is ManufactureModuleConfig || config is ConsumerModuleConfig);
		}

		private static ModuleResult ParseModuleResult(string result)
		{
			return Enum.TryParse(result ?? string.Empty, ignoreCase: true, out ModuleResult parsed)
				? parsed
				: ModuleResult.Default;
		}
	}

	public sealed class PlayerLegalBusinessConsumerSummary
	{
		public EntityID BuildingID { get; internal set; }
		public string ModuleId { get; internal set; }
		public PlayerID PlayerID { get; internal set; }
		public bool IsConsumer { get; internal set; }
		public bool IsPlayerLegalBusinessModule { get; internal set; }
		public bool HumanOwned { get; internal set; }
		public bool ShouldObserve { get; internal set; }
		public bool Initial { get; internal set; }
		public bool Enabled { get; internal set; }
		public string Result { get; internal set; }
		public int ConsumeDays { get; internal set; }
		public int CurrentDay { get; internal set; }
		public int LastUpdateDay { get; internal set; }
		public int DaysSinceLast { get; internal set; }
		public int DaysUntilNext { get; internal set; }
		public bool IsIdleWaiting { get; internal set; }
		public string Reason { get; internal set; }
		public string ExecutionOwner { get; internal set; }
		public string ClassificationOwner { get; internal set; }
		public bool MutationMovedToEconomy { get; internal set; }

		internal string FormatBridgeSummary()
		{
			return "player-legal-business-consumer building=" + BuildingID +
				" module=" + (ModuleId ?? string.Empty) +
				" player=" + PlayerID +
				" candidate=" + ShouldObserve +
				" reason=" + (Reason ?? "unknown") +
				" consumer=" + IsConsumer +
				" humanOwned=" + HumanOwned +
				" initial=" + Initial +
				" enabled=" + Enabled +
				" result=" + (Result ?? "unknown") +
				" consumeDays=" + ConsumeDays +
				" currentDay=" + CurrentDay +
				" lastUpdateDay=" + LastUpdateDay +
				" daysSinceLast=" + DaysSinceLast +
				" daysUntilNext=" + DaysUntilNext +
				" classificationOwner=" + (ClassificationOwner ?? "unknown") +
				" executionOwner=" + (ExecutionOwner ?? "unknown") +
				" mutationMovedToEconomy=" + MutationMovedToEconomy;
		}
	}
}

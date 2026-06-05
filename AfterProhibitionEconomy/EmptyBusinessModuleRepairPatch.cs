using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using HarmonyLib;

namespace AfterProhibitionEconomy
{
	internal static class EmptyBusinessModuleRepairPatch
	{
		private const int CleanHumanTurnRepairCooldownDays = 28;
		private const int CleanRuntimeRepairCooldownDays = 28;
		private const int RuntimeRepairScanBudget = 512;
		private const int RuntimeRepairTimeBudgetMs = 8;

		private static readonly HashSet<string> LoggedRepairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static int _lastRepairDay = int.MinValue;
		private static int _skipCleanHumanTurnRepairUntilDay = int.MinValue;
		private static int _skipCleanRuntimeRepairUntilDay = int.MinValue;
		private static int _lastHumanTurnRepairCooldownLogDay = int.MinValue;
		private static int _lastRuntimeRepairCooldownLogDay = int.MinValue;
		private static int _loggedRepairDetails;
		private static bool _loggedRepairDetailsSuppressed;
		private static RuntimeRepairScanState _runtimeRepairScanState;

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo updateBusinessModulesMethod = AccessTools.Method(typeof(BusinessUpdate), "UpdateBusinessModules");
				if (updateBusinessModulesMethod != null)
				{
					harmony.Patch(
						updateBusinessModulesMethod,
						postfix: new HarmonyMethod(typeof(EmptyBusinessModuleRepairPatch), nameof(UpdateBusinessModulesPostfix)));
					AfterProhibitionEconomyPlugin.Log?.LogInfo("empty business module repair patch applied target=BusinessUpdate.UpdateBusinessModules");
				}
				else
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("empty business module repair patch skipped target=BusinessUpdate.UpdateBusinessModules reason=method-not-found");
				}

				MethodInfo playerTurnStartedMethod = AccessTools.Method(typeof(PlayerInfo), "OnPlayerTurnStarted");
				if (playerTurnStartedMethod != null)
				{
					harmony.Patch(
						playerTurnStartedMethod,
						postfix: new HarmonyMethod(typeof(EmptyBusinessModuleRepairPatch), nameof(PlayerInfoOnPlayerTurnStartedPostfix)));
					AfterProhibitionEconomyPlugin.Log?.LogInfo("empty business module repair patch applied target=PlayerInfo.OnPlayerTurnStarted");
				}
				else
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("empty business module repair patch skipped target=PlayerInfo.OnPlayerTurnStarted reason=method-not-found");
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("empty business module repair patch setup failed: " + ex.Message);
			}
		}

		private static void UpdateBusinessModulesPostfix(bool initial)
		{
			if (!(AfterProhibitionEconomyPlugin.EnableEmptyBusinessModuleRepair?.Value ?? false))
			{
				return;
			}

			Run(initial ? "business-update-initial" : "business-update", force: false);
		}

		private static void PlayerInfoOnPlayerTurnStartedPostfix(PlayerInfo __instance)
		{
			if (__instance == null || !__instance.IsHuman || !(AfterProhibitionEconomyPlugin.EnableEmptyBusinessModuleRepair?.Value ?? false))
			{
				return;
			}

			Run("human-turn-start", force: false);
		}

		internal static void Run(string source, bool force)
		{
			try
			{
				if (global::Game.Game.ctx?.entityman == null || global::Game.Game.ctx?.clock == null)
				{
					return;
				}

				SimTime now = global::Game.Game.ctx.clock.Now;
				string sourceKey = string.IsNullOrEmpty(source) ? "unknown" : source;
				bool isInitialBusinessUpdate = string.Equals(sourceKey, "business-update-initial", StringComparison.Ordinal);
				bool isHumanTurnStart = string.Equals(sourceKey, "human-turn-start", StringComparison.Ordinal);
				bool isRuntimeBusinessUpdate = string.Equals(sourceKey, "business-update", StringComparison.Ordinal);
				if (!force && isInitialBusinessUpdate)
				{
					_lastRepairDay = now.days;
					_skipCleanHumanTurnRepairUntilDay = Math.Max(_skipCleanHumanTurnRepairUntilDay, now.days + CleanHumanTurnRepairCooldownDays);
					_skipCleanRuntimeRepairUntilDay = Math.Max(_skipCleanRuntimeRepairUntilDay, now.days + CleanRuntimeRepairCooldownDays);
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"empty-business-module-repair skipped source=" + sourceKey +
						" reason=startup-safety humanCooldownUntilDay=" + _skipCleanHumanTurnRepairUntilDay +
						" runtimeCooldownUntilDay=" + _skipCleanRuntimeRepairUntilDay +
						" day=" + now.days);
					return;
				}
				if (!force && isHumanTurnStart)
				{
					if (_lastHumanTurnRepairCooldownLogDay == int.MinValue || now.days >= _skipCleanHumanTurnRepairUntilDay)
					{
						_lastHumanTurnRepairCooldownLogDay = now.days;
						_skipCleanHumanTurnRepairUntilDay = Math.Max(_skipCleanHumanTurnRepairUntilDay, now.days + CleanHumanTurnRepairCooldownDays);
						AfterProhibitionEconomyPlugin.Log?.LogInfo(
							"empty-business-module-repair skipped source=" + sourceKey +
							" reason=turn-start-deferred untilDay=" + _skipCleanHumanTurnRepairUntilDay +
							" day=" + now.days);
					}
					return;
				}
				if (!force && isHumanTurnStart && now.days < _skipCleanHumanTurnRepairUntilDay)
				{
					if (_lastHumanTurnRepairCooldownLogDay != now.days)
					{
						_lastHumanTurnRepairCooldownLogDay = now.days;
						AfterProhibitionEconomyPlugin.Log?.LogInfo(
							"empty-business-module-repair skipped source=" + sourceKey +
							" reason=clean-cooldown untilDay=" + _skipCleanHumanTurnRepairUntilDay +
							" day=" + now.days);
					}
					return;
				}
				if (!force && isRuntimeBusinessUpdate && now.days < _skipCleanRuntimeRepairUntilDay)
				{
					if (_lastRuntimeRepairCooldownLogDay != now.days)
					{
						_lastRuntimeRepairCooldownLogDay = now.days;
						AfterProhibitionEconomyPlugin.Log?.LogInfo(
							"empty-business-module-repair skipped source=" + sourceKey +
							" reason=clean-cooldown untilDay=" + _skipCleanRuntimeRepairUntilDay +
							" day=" + now.days);
					}
					return;
				}
				if (!force && isRuntimeBusinessUpdate)
				{
					if (_lastRepairDay == now.days)
					{
						return;
					}

					_lastRepairDay = now.days;
					RunRuntimeBusinessUpdateBatch(sourceKey, now);
					return;
				}
				if (!force && _lastRepairDay == now.days)
				{
					return;
				}

				_lastRepairDay = now.days;
				int scanned = 0;
				int candidates = 0;
				int repaired = 0;
				int failed = 0;
				int skipped = 0;
				int skippedHuman = 0;
				int skippedNoConfigModules = 0;
				int skippedNoSlots = 0;
				int skippedAlreadyValid = 0;

				IEnumerable<Entity> buildings = global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return;
				}

				foreach (Entity building in buildings)
				{
					scanned++;
					if (!CanRepairBusinessModules(building, out string skipReason))
					{
						skipped++;
						TrackSkip(skipReason, ref skippedHuman, ref skippedNoSlots);
						continue;
					}

					Entity biz = BuildingUtil.FindBizForBuilding(building);
					BizConfig config = biz?.components?.biz?.Config;
					List<Label> expectedModuleIds = GetBusinessModuleIdsForRepair(biz, config);
					if (expectedModuleIds == null || expectedModuleIds.Count == 0)
					{
						skipped++;
						skippedNoConfigModules++;
						continue;
					}

					ModulesComponent modules = building.components.modules;
					List<Label> unexpectedModuleIds = GetUnexpectedBusinessModuleIds(modules, expectedModuleIds);
					List<Label> missingModuleIds = GetInstallableMissingBusinessModuleIds(modules, expectedModuleIds);
					if (unexpectedModuleIds.Count == 0 && missingModuleIds.Count == 0)
					{
						skipped++;
						skippedAlreadyValid++;
						continue;
					}

					candidates++;
					if (TryRepairBusinessModules(sourceKey, now, building, biz, modules, expectedModuleIds, unexpectedModuleIds, missingModuleIds))
					{
						repaired++;
					}
					else
					{
						failed++;
					}
				}

				if (candidates > 0 || repaired > 0 || failed > 0 || AfterProhibitionEconomyPlugin.EnableEmptyBusinessModuleRepairSummaryWhenNoCandidates.Value)
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"empty-business-module-repair source=" + sourceKey +
						" scanned=" + scanned +
						" candidates=" + candidates +
						" repaired=" + repaired +
						" failed=" + failed +
						" skipped=" + skipped +
						" skippedHuman=" + skippedHuman +
						" skippedNoSlots=" + skippedNoSlots +
						" skippedNoConfigModules=" + skippedNoConfigModules +
						" skippedAlreadyValid=" + skippedAlreadyValid +
						" day=" + now.days);
				}

				if (failed == 0 && (isHumanTurnStart || isInitialBusinessUpdate))
				{
					_skipCleanHumanTurnRepairUntilDay = Math.Max(_skipCleanHumanTurnRepairUntilDay, now.days + CleanHumanTurnRepairCooldownDays);
				}
				if (failed == 0 && (isRuntimeBusinessUpdate || isInitialBusinessUpdate))
				{
					_skipCleanRuntimeRepairUntilDay = Math.Max(_skipCleanRuntimeRepairUntilDay, now.days + CleanRuntimeRepairCooldownDays);
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("empty-business-module-repair failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void RunRuntimeBusinessUpdateBatch(string sourceKey, SimTime now)
		{
			object currentContext = global::Game.Game.ctx;
			RuntimeRepairScanState state = _runtimeRepairScanState;
			if (state != null && (!ReferenceEquals(state.Context, currentContext) || now.days < state.StartedDay))
			{
				state = null;
				_runtimeRepairScanState = null;
			}

			if (state == null)
			{
				IEnumerable<Entity> buildings = global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return;
				}

				state = new RuntimeRepairScanState(sourceKey, now.days, buildings, currentContext);
				_runtimeRepairScanState = state;
			}

			Stopwatch stopwatch = Stopwatch.StartNew();
			int processed = 0;
			int budget = Math.Max(1, RuntimeRepairScanBudget);
			int timeBudgetMs = Math.Max(1, RuntimeRepairTimeBudgetMs);
			while (state.Cursor < state.Buildings.Count && processed < budget)
			{
				Entity building = state.Buildings[state.Cursor++];
				processed++;
				state.Scanned++;

				if (!CanRepairBusinessModules(building, out string skipReason))
				{
					state.Skipped++;
					TrackSkip(skipReason, ref state.SkippedHuman, ref state.SkippedNoSlots);
					if (stopwatch.ElapsedMilliseconds >= timeBudgetMs)
					{
						break;
					}

					continue;
				}

				Entity biz = BuildingUtil.FindBizForBuilding(building);
				BizConfig config = biz?.components?.biz?.Config;
				List<Label> expectedModuleIds = GetBusinessModuleIdsForRepair(biz, config);
				if (expectedModuleIds == null || expectedModuleIds.Count == 0)
				{
					state.Skipped++;
					state.SkippedNoConfigModules++;
					if (stopwatch.ElapsedMilliseconds >= timeBudgetMs)
					{
						break;
					}

					continue;
				}

				ModulesComponent modules = building.components.modules;
				List<Label> unexpectedModuleIds = GetUnexpectedBusinessModuleIds(modules, expectedModuleIds);
				List<Label> missingModuleIds = GetInstallableMissingBusinessModuleIds(modules, expectedModuleIds);
				if (unexpectedModuleIds.Count == 0 && missingModuleIds.Count == 0)
				{
					state.Skipped++;
					state.SkippedAlreadyValid++;
					if (stopwatch.ElapsedMilliseconds >= timeBudgetMs)
					{
						break;
					}

					continue;
				}

				state.Candidates++;
				if (TryRepairBusinessModules(sourceKey, now, building, biz, modules, expectedModuleIds, unexpectedModuleIds, missingModuleIds))
				{
					state.Repaired++;
				}
				else
				{
					state.Failed++;
				}

				if (stopwatch.ElapsedMilliseconds >= timeBudgetMs)
				{
					break;
				}
			}
			stopwatch.Stop();

			bool completed = state.Cursor >= state.Buildings.Count;
			if (!completed)
			{
				if (state.Failed > 0 || AfterProhibitionEconomyPlugin.ShouldLogRuntimeEconomyProgress())
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"empty-business-module-repair-progress source=" + sourceKey +
						" scannedBatch=" + processed +
						" scannedTotal=" + state.Scanned +
						" totalBuildings=" + state.Buildings.Count +
						" candidates=" + state.Candidates +
						" repaired=" + state.Repaired +
						" failed=" + state.Failed +
						" elapsedMs=" + stopwatch.ElapsedMilliseconds +
						" day=" + now.days);
				}
				return;
			}

			if (state.Candidates > 0 || state.Repaired > 0 || state.Failed > 0 || AfterProhibitionEconomyPlugin.EnableEmptyBusinessModuleRepairSummaryWhenNoCandidates.Value)
			{
				AfterProhibitionEconomyPlugin.Log?.LogInfo(
					"empty-business-module-repair source=" + sourceKey +
					" phase=complete" +
					" scanned=" + state.Scanned +
					" candidates=" + state.Candidates +
					" repaired=" + state.Repaired +
					" failed=" + state.Failed +
					" skipped=" + state.Skipped +
					" skippedHuman=" + state.SkippedHuman +
					" skippedNoSlots=" + state.SkippedNoSlots +
					" skippedNoConfigModules=" + state.SkippedNoConfigModules +
					" skippedAlreadyValid=" + state.SkippedAlreadyValid +
					" day=" + now.days);
			}

			if (state.Failed == 0)
			{
				_skipCleanRuntimeRepairUntilDay = Math.Max(_skipCleanRuntimeRepairUntilDay, now.days + CleanRuntimeRepairCooldownDays);
			}

			_runtimeRepairScanState = null;
		}

		private sealed class RuntimeRepairScanState
		{
			internal readonly string SourceKey;
			internal readonly int StartedDay;
			internal readonly object Context;
			internal readonly List<Entity> Buildings;
			internal int Cursor;
			internal int Scanned;
			internal int Candidates;
			internal int Repaired;
			internal int Failed;
			internal int Skipped;
			internal int SkippedHuman;
			internal int SkippedNoConfigModules;
			internal int SkippedNoSlots;
			internal int SkippedAlreadyValid;

			internal RuntimeRepairScanState(string sourceKey, int startedDay, IEnumerable<Entity> buildings, object context)
			{
				SourceKey = sourceKey;
				StartedDay = startedDay;
				Context = context;
				Buildings = new List<Entity>(buildings);
			}
		}

		private static bool TryRepairBusinessModules(
			string source,
			SimTime now,
			Entity building,
			Entity biz,
			ModulesComponent modules,
			List<Label> expectedModuleIds,
			List<Label> unexpectedModuleIds,
			List<Label> missingModuleIds)
		{
			try
			{
				biz.data.biz.modules = expectedModuleIds;
				if (unexpectedModuleIds.Count > 0)
				{
					modules.RemoveModules(unexpectedModuleIds, shutdown: false);
					missingModuleIds = GetInstallableMissingBusinessModuleIds(modules, expectedModuleIds);
				}

				if (missingModuleIds.Count > 0)
				{
					modules.InstallModules(missingModuleIds, now);
				}

				if (HasAnyExpectedBusinessModuleInstalled(modules, expectedModuleIds) && !HasMissingInstallableBusinessModule(modules, expectedModuleIds))
				{
					LogRepair(source, building, biz, expectedModuleIds, unexpectedModuleIds, missingModuleIds);
					return true;
				}

				LogFailure(source, building, biz, expectedModuleIds);
				return false;
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning(
					"empty-business-module-repair failed-business source=" + source +
					" building=" + (building?.Id.ToString() ?? "null") +
					" biz=" + (biz?.Id.ToString() ?? "null") +
					" error=" + ex.GetType().Name + ":" + ex.Message);
				return false;
			}
		}

		private static bool CanRepairBusinessModules(Entity building, out string skipReason)
		{
			skipReason = "unknown";
			if (building?.components?.building == null || building.components.modules == null || building.data?.building == null)
			{
				skipReason = "invalid-building";
				return false;
			}

			if (!building.components.building.IsBusinessBuildingType || building.components.building.IsSafehouse || building.components.building.IsOutpost)
			{
				skipReason = "not-repairable-business";
				return false;
			}

			if (building.data.building.controlled.Get().IsHumanPlayer)
			{
				skipReason = "human-controlled";
				return false;
			}

			List<IModule> slots = building.components.modules.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				skipReason = "no-slots";
				return false;
			}

			return true;
		}

		private static void TrackSkip(string skipReason, ref int skippedHuman, ref int skippedNoSlots)
		{
			switch (skipReason)
			{
			case "human-controlled":
				skippedHuman++;
				break;
			case "no-slots":
				skippedNoSlots++;
				break;
			}
		}

		private static List<Label> GetBusinessModuleIdsForRepair(Entity biz, BizConfig config)
		{
			if (biz?.data?.biz == null || config == null)
			{
				return null;
			}

			List<Label> existingModules = biz.data.biz.modules;
			if (existingModules != null && existingModules.Count > 0)
			{
				return existingModules;
			}

			if (config.modulesInBuilding == null || config.modulesInBuilding.Count == 0)
			{
				return null;
			}

			return config.PickModulesForBuilding(biz);
		}

		private static List<Label> GetInstallableMissingBusinessModuleIds(ModulesComponent modules, List<Label> expectedModuleIds)
		{
			List<Label> missing = new List<Label>();
			if (modules == null || expectedModuleIds == null)
			{
				return missing;
			}

			foreach (Label moduleId in expectedModuleIds)
			{
				IModuleConfig moduleConfig = ModulesUtil.FindModuleDef(moduleId);
				if (moduleConfig != null && !modules.HasModuleInstalled(moduleId) && HasAvailableSlotForModule(modules, moduleConfig))
				{
					missing.Add(moduleId);
				}
			}

			return missing;
		}

		private static bool HasMissingInstallableBusinessModule(ModulesComponent modules, List<Label> expectedModuleIds)
		{
			return GetInstallableMissingBusinessModuleIds(modules, expectedModuleIds).Count > 0;
		}

		private static bool HasAnyExpectedBusinessModuleInstalled(ModulesComponent modules, List<Label> expectedModuleIds)
		{
			if (modules == null || expectedModuleIds == null || expectedModuleIds.Count == 0)
			{
				return false;
			}

			return expectedModuleIds.Any(moduleId => modules.HasModuleInstalled(moduleId));
		}

		private static bool HasAvailableSlotForModule(ModulesComponent modules, IModuleConfig moduleConfig)
		{
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			List<ModuleSlot> slotConfigs = modules?.Config?.slots;
			if (slots == null || slotConfigs == null || moduleConfig == null)
			{
				return false;
			}

			int count = Math.Min(slots.Count, slotConfigs.Count);
			for (int i = 0; i < count; i++)
			{
				if (slots[i] == null && slotConfigs[i]?.CanSlotHouseThisModule(moduleConfig) == true)
				{
					return true;
				}
			}

			return false;
		}

		private static List<Label> GetUnexpectedBusinessModuleIds(ModulesComponent modules, List<Label> expectedModuleIds)
		{
			List<Label> unexpected = new List<Label>();
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || expectedModuleIds == null || expectedModuleIds.Count == 0)
			{
				return unexpected;
			}

			foreach (IModule slot in slots)
			{
				Label? installedId = slot?.ModuleConfig?.Id;
				if (installedId.HasValue && !expectedModuleIds.Contains(installedId.Value))
				{
					unexpected.Add(installedId.Value);
				}
			}

			return unexpected;
		}

		private static void LogRepair(string source, Entity building, Entity biz, List<Label> expectedModuleIds, List<Label> removedModuleIds, List<Label> installedModuleIds)
		{
			if (!(AfterProhibitionEconomyPlugin.EnableEmptyBusinessModuleRepairDetailLog?.Value ?? false))
			{
				return;
			}

			string key = (building?.Id.ToString() ?? "null") + "|" + (biz?.Id.ToString() ?? "null");
			if (!LoggedRepairs.Add(key))
			{
				return;
			}

			int detailLimit = Math.Max(0, AfterProhibitionEconomyPlugin.EmptyBusinessModuleRepairDetailLimit?.Value ?? 0);
			if (_loggedRepairDetails >= detailLimit)
			{
				if (!_loggedRepairDetailsSuppressed)
				{
					_loggedRepairDetailsSuppressed = true;
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"empty-business-module-repaired-details-suppressed source=" + source +
						" limit=" + detailLimit);
				}

				return;
			}

			_loggedRepairDetails++;
			AfterProhibitionEconomyPlugin.Log?.LogInfo(
				"empty-business-module-repaired source=" + source +
				" building=" + (building?.Id.ToString() ?? "null") +
				" biz=" + (biz?.Id.ToString() ?? "null") +
				" expected=" + JoinLabels(expectedModuleIds) +
				" installed=" + JoinLabels(installedModuleIds) +
				" removed=" + JoinLabels(removedModuleIds));
		}

		private static void LogFailure(string source, Entity building, Entity biz, List<Label> expectedModuleIds)
		{
			string key = source + "|" + (building?.Id.ToString() ?? "null") + "|" + (biz?.Id.ToString() ?? "null");
			if (!LoggedFailures.Add(key) || LoggedFailures.Count > 25)
			{
				return;
			}

			ModulesComponent modules = building?.components?.modules;
			List<Label> missing = expectedModuleIds?
				.Where(moduleId => modules == null || !modules.HasModuleInstalled(moduleId))
				.ToList() ?? new List<Label>();
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			IEnumerable<string> installed = slots == null
				? Enumerable.Empty<string>()
				: slots.Select(slot => slot?.ModuleConfig?.Id.ToString() ?? "empty");

			AfterProhibitionEconomyPlugin.Log?.LogWarning(
				"empty-business-module-repair-incomplete source=" + source +
				" building=" + (building?.Id.ToString() ?? "null") +
				" biz=" + (biz?.Id.ToString() ?? "null") +
				" expected=" + JoinLabels(expectedModuleIds) +
				" missing=" + JoinLabels(missing) +
				" installed=" + string.Join(",", installed.ToArray()));
		}

		private static string JoinLabels(IEnumerable<Label> labels)
		{
			return string.Join(",", labels?.Select(id => id.ToString()).ToArray() ?? new string[0]);
		}
	}
}

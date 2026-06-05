using System;
using System.Collections.Generic;
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
	internal static class DirtyCashRuntimeSweepPatch
	{
		private static readonly HashSet<string> ObservedIllegalBackroomBuildingUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> PreSystemForcedIllegalBackroomBuildingUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<EntityID> UpdatedIllegalBackroomBuildingsThisTick = new HashSet<EntityID>();
		private static readonly HashSet<string> LoggedIllegalBackroomBusinessTickFallbacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedIllegalBackroomSafetySweepSummaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static int _lastDirtyCashBackroomCandidateDay = int.MinValue;
		private static List<Entity> _cachedDirtyCashBackroomCandidates;
		private static MethodInfo _modulesComponentDoUpdateMethod;
		private static bool _trackingIllegalBackroomBusinessTick;

		internal static void BeginBusinessUpdate()
		{
			if (!AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation())
			{
				return;
			}

			_trackingIllegalBackroomBusinessTick = true;
			UpdatedIllegalBackroomBuildingsThisTick.Clear();
		}

		internal static void CompleteBusinessUpdate(bool initial)
		{
			try
			{
				if (AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation())
				{
					TryFallbackUpdateMissingIllegalBackroomBuildings(initial);
				}
			}
			finally
			{
				_trackingIllegalBackroomBusinessTick = false;
			}
		}

		internal static void EndBusinessUpdate()
		{
			_trackingIllegalBackroomBusinessTick = false;
		}

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				PatchIfFound(harmony, AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new[] { typeof(SimTime), typeof(bool) }),
					prefixName: nameof(ModulesComponentDoUpdatePrefix),
					postfixName: nameof(ModulesComponentDoUpdatePostfix),
					finalizerName: null,
					label: "ModulesComponent.DoUpdate");

				PatchIfFound(harmony, AccessTools.Method(typeof(BusinessUpdate), "UpdateBusinessModules"),
					prefixName: nameof(UpdateBusinessModulesPrefix),
					postfixName: nameof(UpdateBusinessModulesPostfix),
					finalizerName: nameof(UpdateBusinessModulesFinalizer),
					label: "BusinessUpdate.UpdateBusinessModules");

				PatchIfFound(harmony, AccessTools.Method(typeof(BusinessUpdate), "Tick"),
					prefixName: null,
					postfixName: nameof(BusinessUpdateTickPostfix),
					finalizerName: null,
					label: "BusinessUpdate.Tick");

				PatchIfFound(harmony, AccessTools.Method(typeof(BusinessUpdate), "UpdateGamblingModules"),
					prefixName: null,
					postfixName: nameof(UpdateGamblingModulesPostfix),
					finalizerName: null,
					label: "BusinessUpdate.UpdateGamblingModules");

				PatchIfFound(harmony, AccessTools.Method(typeof(BusinessUpdate), "RecalculateHeatAndRespectForNodes"),
					prefixName: nameof(RecalculateHeatAndRespectForNodesPrefix),
					postfixName: null,
					finalizerName: null,
					label: "BusinessUpdate.RecalculateHeatAndRespectForNodes");

				PatchIfFound(harmony, AccessTools.Method(typeof(PlayerFinances), "OnGlobalTurnSetAdvanced"),
					prefixName: null,
					postfixName: nameof(PlayerFinancesOnGlobalTurnSetAdvancedPostfix),
					finalizerName: null,
					label: "PlayerFinances.OnGlobalTurnSetAdvanced");

				AfterProhibitionEconomyPlugin.Log?.LogInfo("dirty-cash-runtime-sweep runtime patch applied owner=AfterProhibitionEconomy");
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("dirty-cash-runtime-sweep patch setup failed: " + ex.Message);
			}
		}

		private static void PatchIfFound(Harmony harmony, MethodInfo method, string prefixName, string postfixName, string finalizerName, string label)
		{
			if (method == null)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("dirty-cash-runtime-sweep patch skipped target=" + label + " reason=method-not-found");
				return;
			}

			HarmonyMethod prefix = prefixName == null ? null : new HarmonyMethod(typeof(DirtyCashRuntimeSweepPatch), prefixName);
			HarmonyMethod postfix = postfixName == null ? null : new HarmonyMethod(typeof(DirtyCashRuntimeSweepPatch), postfixName);
			HarmonyMethod finalizer = finalizerName == null ? null : new HarmonyMethod(typeof(DirtyCashRuntimeSweepPatch), finalizerName);
			harmony.Patch(method, prefix: prefix, postfix: postfix, finalizer: finalizer);
		}

		private static bool UpdateBusinessModulesPrefix()
		{
			BeginBusinessUpdate();
			return true;
		}

		private static void UpdateBusinessModulesPostfix(bool initial)
		{
			try
			{
				CompleteBusinessUpdate(initial);
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("dirty-cash-runtime-sweep business update fallback failed: " + ex.Message);
			}
			finally
			{
				EndBusinessUpdate();
			}
		}

		private static Exception UpdateBusinessModulesFinalizer(Exception __exception)
		{
			EndBusinessUpdate();
			return __exception;
		}

		private static bool ModulesComponentDoUpdatePrefix(ModulesComponent __instance, SimTime time, bool initial)
		{
			if (!AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation())
			{
				return true;
			}

			Entity building = __instance?.entity;
			if (building == null || !HasInstalledDirtyCashRuntimeBypassModule(__instance))
			{
				return true;
			}

			string updateKey = BuildIllegalBackroomBuildingUpdateKey(building.Id, time, initial);
			if (_trackingIllegalBackroomBusinessTick && PreSystemForcedIllegalBackroomBuildingUpdates.Contains(updateKey))
			{
				ObservedIllegalBackroomBuildingUpdates.Add(updateKey);
				UpdatedIllegalBackroomBuildingsThisTick.Add(building.Id);
				return false;
			}

			if (IsHumanOwnedBuilding(building))
			{
				ObservedIllegalBackroomBuildingUpdates.Add(updateKey);
			}

			if (_trackingIllegalBackroomBusinessTick)
			{
				UpdatedIllegalBackroomBuildingsThisTick.Add(building.Id);
			}

			return true;
		}

		private static void ModulesComponentDoUpdatePostfix(ModulesComponent __instance, SimTime time, bool initial)
		{
		}

		private static void BusinessUpdateTickPostfix(bool initial)
		{
			try
			{
				if (AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation())
				{
					TryForceUpdateMissedIllegalBackroomBuildings(initial, "business-tick");
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("dirty-cash-runtime-sweep tick force failed: " + ex.Message);
			}
		}

		private static void UpdateGamblingModulesPostfix(bool initial)
		{
			try
			{
				if (AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation())
				{
					TryForceUpdateMissedIllegalBackroomBuildings(initial, "after-gambling-pre-respect");
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("dirty-cash-runtime-sweep pre-respect force failed: " + ex.Message);
			}
		}

		private static void RecalculateHeatAndRespectForNodesPrefix(bool initial)
		{
			try
			{
				if (AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation())
				{
					TryForceUpdateMissedIllegalBackroomBuildings(initial, "pre-recalculate");
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("dirty-cash-runtime-sweep pre-recalculate force failed: " + ex.Message);
			}
		}

		private static void PlayerFinancesOnGlobalTurnSetAdvancedPostfix(PlayerFinances __instance)
		{
			if (!AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation() ||
				__instance == null ||
				global::Game.Game.ctx?.players?.Human?.finances != __instance)
			{
				return;
			}

			try
			{
				TryForceUpdateMissedIllegalBackroomBuildingsBeforeSystemTurn();
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("dirty-cash-runtime-sweep pre-system force failed: " + ex.Message);
			}
		}

		private static void TryForceUpdateMissedIllegalBackroomBuildingsBeforeSystemTurn()
		{
			TryForceUpdateMissedIllegalBackroomBuildings(false, "pre-system-turn");
		}

		private static void TryFallbackUpdateMissingIllegalBackroomBuildings(bool initial)
		{
			MethodInfo doUpdateMethod = GetModulesComponentDoUpdateMethod();
			if (doUpdateMethod == null)
			{
				return;
			}

			SimTime now = global::Game.Game.ctx.clock.Now;
			List<Entity> buildings = GetHumanControlledDirtyCashBackroomBuildings();
			int candidateCount = buildings.Count;
			int alreadyUpdatedCount = 0;
			int fallbackAppliedCount = 0;
			foreach (Entity building in buildings)
			{
				EntityID buildingId = building?.Id ?? EntityID.INVALID;
				if (!buildingId.IsValid || UpdatedIllegalBackroomBuildingsThisTick.Contains(buildingId))
				{
					if (buildingId.IsValid)
					{
						alreadyUpdatedCount++;
					}

					continue;
				}

				ModulesComponent modules = building?.components?.modules;
				if (!HasInstalledDirtyCashRuntimeBypassModule(modules))
				{
					continue;
				}

				doUpdateMethod.Invoke(modules, new object[] { now, initial });
				fallbackAppliedCount++;

				string logKey = BuildRuntimeCorrectionLogKey("fallback", buildingId, now, initial);
				if (LoggedIllegalBackroomBusinessTickFallbacks.Add(logKey))
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"dirty-cash-runtime business tick fallback applied building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" initial=" +
						initial +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary("update-business-modules", now, initial, candidateCount, fallbackAppliedCount, 0, alreadyUpdatedCount);
		}

		private static void TryForceUpdateMissedIllegalBackroomBuildings(bool initial, string source)
		{
			MethodInfo doUpdateMethod = GetModulesComponentDoUpdateMethod();
			if (doUpdateMethod == null)
			{
				return;
			}

			SimTime now = global::Game.Game.ctx.clock.Now;
			List<Entity> buildings = GetHumanControlledDirtyCashBackroomBuildings();
			int candidateCount = buildings.Count;
			int observedCount = 0;
			int forceAppliedCount = 0;
			foreach (Entity building in buildings)
			{
				EntityID buildingId = building?.Id ?? EntityID.INVALID;
				if (!buildingId.IsValid)
				{
					continue;
				}

				ModulesComponent modules = building?.components?.modules;
				if (!HasInstalledDirtyCashRuntimeBypassModule(modules))
				{
					continue;
				}

				string updateKey = BuildIllegalBackroomBuildingUpdateKey(buildingId, now, initial);
				if (ObservedIllegalBackroomBuildingUpdates.Contains(updateKey))
				{
					observedCount++;
					continue;
				}

				doUpdateMethod.Invoke(modules, new object[] { now, initial });
				if (string.Equals(source, "pre-system-turn", StringComparison.Ordinal))
				{
					PreSystemForcedIllegalBackroomBuildingUpdates.Add(updateKey);
				}

				forceAppliedCount++;

				string logKey = BuildRuntimeCorrectionLogKey(source, buildingId, now, initial);
				if (LoggedIllegalBackroomBusinessTickFallbacks.Add(logKey))
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"dirty-cash-runtime force applied source=" +
						source +
						" building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" initial=" +
						initial +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary(source, now, initial, candidateCount, forceAppliedCount, observedCount, 0);
		}

		private static MethodInfo GetModulesComponentDoUpdateMethod()
		{
			return _modulesComponentDoUpdateMethod ?? (_modulesComponentDoUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new[] { typeof(SimTime), typeof(bool) }));
		}

		private static List<Entity> GetHumanControlledDirtyCashBackroomBuildings()
		{
			int currentDay = global::Game.Game.ctx?.clock != null ? global::Game.Game.ctx.clock.Now.days : int.MinValue;
			if (_cachedDirtyCashBackroomCandidates != null && _lastDirtyCashBackroomCandidateDay == currentDay)
			{
				return _cachedDirtyCashBackroomCandidates;
			}

			List<Entity> results = new List<Entity>();
			IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
			if (buildings != null)
			{
				foreach (Entity building in buildings)
				{
					if (building == null || !IsHumanOwnedBuilding(building))
					{
						continue;
					}

					if (HasInstalledDirtyCashRuntimeBypassModule(building.components?.modules))
					{
						results.Add(building);
					}
				}
			}

			_cachedDirtyCashBackroomCandidates = results;
			_lastDirtyCashBackroomCandidateDay = currentDay;
			return results;
		}

		private static bool IsHumanOwnedBuilding(Entity container)
		{
			if (container?.data?.building == null)
			{
				return false;
			}

			try
			{
				if (container.data.building.controlled.Get().IsHumanPlayer)
				{
					return true;
				}
			}
			catch
			{
			}

			try
			{
				return global::Game.Game.ctx?.players?.Human?.territory?.IsControlled(container) ?? false;
			}
			catch
			{
				return false;
			}
		}

		private static bool HasInstalledDirtyCashRuntimeBypassModule(ModulesComponent modules)
		{
			if (modules == null)
			{
				return false;
			}

			if (modules.bizmodules != null)
			{
				for (int i = 0; i < modules.bizmodules.Count; i++)
				{
					if (IsDirtyCashRuntimeBypassModule(modules.bizmodules[i]?.ModuleConfig))
					{
						return true;
					}
				}
			}

			List<IModule> slots = modules.GetAllSlotsUnsafe();
			if (slots == null)
			{
				return false;
			}

			for (int i = 0; i < slots.Count; i++)
			{
				if (IsDirtyCashRuntimeBypassModule(slots[i]?.ModuleConfig))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsDirtyCashRuntimeBypassModule(IModuleConfig config)
		{
			return IsDirtyCashBackroomModule(config) || IsPlayerLegalDirtyCashBusinessModule(config);
		}

		private static bool IsDirtyCashBackroomModule(IModuleConfig config)
		{
			if (config?.Common?.tags == null || !config.Common.tags.Contains(TagConstants.TAG_SAFEHOUSE_BACKROOMS))
			{
				return false;
			}

			if (config is ExplanationModuleConfig || config is VehicleModuleConfig)
			{
				return false;
			}

			string id = config.Id.String;
			if (string.IsNullOrEmpty(id))
			{
				return false;
			}

			if (id.StartsWith("explanation-", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("player-garage", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("player-truck-garage", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("garage-", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("truck-garage", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return config is ManufactureModuleConfig || config is ConsumerModuleConfig;
		}

		private static bool IsPlayerLegalDirtyCashBusinessModule(IModuleConfig config)
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

		private static void LogIllegalBackroomSafetySweepSummary(string source, SimTime now, bool initial, int candidateCount, int appliedCount, int observedCount, int alreadyUpdatedCount)
		{
			bool noWork = candidateCount == 0 && appliedCount == 0 && observedCount == 0 && alreadyUpdatedCount == 0;
			bool shouldLog = appliedCount > 0 || AfterProhibitionEconomyPlugin.ShouldLogRuntimeEconomyRoutine();
			if (!shouldLog)
			{
				return;
			}

			string logKey = noWork
				? source + "|zero-work|initial=" + initial
				: BuildRuntimeSweepSummaryLogKey(source, now, initial, candidateCount, appliedCount, observedCount, alreadyUpdatedCount);
			if (!LoggedIllegalBackroomSafetySweepSummaries.Add(logKey))
			{
				return;
			}

			AfterProhibitionEconomyPlugin.Log?.LogInfo(
				"dirty-cash-runtime-sweep source=" +
				source +
				" candidates=" +
				candidateCount +
				" applied=" +
				appliedCount +
				" observed=" +
				observedCount +
				" alreadyUpdated=" +
				alreadyUpdatedCount +
				" initial=" +
				initial +
				" day=" +
				now.days +
				(noWork ? " repeatZeroWorkSuppressed=True" : string.Empty));

			AfterProhibitionEconomyPlugin.Log?.LogInfo(DirtyCashRuntimeSweepClassifier.Classify(source).FormatBridgeSummary());
		}

		private static string BuildRuntimeCorrectionLogKey(string source, EntityID buildingId, SimTime now, bool initial)
		{
			if (AfterProhibitionEconomyPlugin.ShouldLogRuntimeEconomyRoutine())
			{
				return source + "|" + buildingId + "|day=" + now.days + "|initial=" + initial;
			}

			return source + "|" + buildingId + "|initial=" + initial;
		}

		private static string BuildRuntimeSweepSummaryLogKey(string source, SimTime now, bool initial, int candidateCount, int appliedCount, int observedCount, int alreadyUpdatedCount)
		{
			if (AfterProhibitionEconomyPlugin.ShouldLogRuntimeEconomyRoutine())
			{
				return source + "|day=" + now.days + "|initial=" + initial + "|candidates=" + candidateCount + "|applied=" + appliedCount + "|observed=" + observedCount + "|alreadyUpdated=" + alreadyUpdatedCount;
			}

			return source + "|initial=" + initial + "|candidates=" + candidateCount + "|applied=" + appliedCount + "|observed=" + observedCount + "|alreadyUpdated=" + alreadyUpdatedCount;
		}

		private static string BuildIllegalBackroomBuildingUpdateKey(EntityID buildingId, SimTime time, bool initial)
		{
			return buildingId + "|day=" + time.days + "|initial=" + initial;
		}

		private static string GetControllingPlayerString(Entity container)
		{
			if (container?.data?.building == null)
			{
				return "none";
			}

			return container.data.building.controlled.Get().ToString();
		}
	}
}

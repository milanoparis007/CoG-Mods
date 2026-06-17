using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using HarmonyLib;
using SomaSim.Util;

namespace AfterProhibitionEconomy
{
	internal static class DirtyCashRuntimeSweepPatch
	{
		private static readonly HashSet<string> ObservedIllegalBackroomBuildingUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> PreSystemForcedIllegalBackroomBuildingUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<EntityID> UpdatedIllegalBackroomBuildingsThisTick = new HashSet<EntityID>();
		private static readonly HashSet<string> LoggedIllegalBackroomBusinessTickFallbacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedIllegalBackroomSafetySweepSummaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedDirtyCashTerritorySwitchDiagnostics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedDirtyCashTerritoryOwnerMismatchDiagnostics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<EntityID, List<DirtyCashAOEContributionEntry>> RecordedDirtyCashAOEContributionsByBuilding = new Dictionary<EntityID, List<DirtyCashAOEContributionEntry>>();
		private static readonly Dictionary<NodeID, DirtyCashTerritoryOwnerSnapshot> DirtyCashTerritoryOwnerSnapshotsByNode = new Dictionary<NodeID, DirtyCashTerritoryOwnerSnapshot>();
		private const int MaxTerritoryReconcilePasses = 4;
		private static int _lastDirtyCashBackroomCandidateDay = int.MinValue;
		private static List<Entity> _cachedDirtyCashBackroomCandidates;
		private static MethodInfo _modulesComponentDoUpdateMethod;
		private static bool _trackingIllegalBackroomBusinessTick;

		private sealed class DirtyCashAOEContributionEntry
		{
			public NodeID NodeId;
			public PlayerID Pid;
			public Fixnum Delta;
		}

		private sealed class DirtyCashTerritoryOwnerSnapshot
		{
			public EntityID BuildingId;
			public PlayerID ExpectedOwner;
			public string Source;
			public int Day;
		}

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
				RestoreRecordedDirtyCashAOEContribution(building, "pre-system-skip-restore");
				return false;
			}

			if (IsHumanOwnedBuilding(building))
			{
				ClearRecordedDirtyCashAOEContribution(building);
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
			if (!AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation())
			{
				return;
			}

			Entity building = __instance?.entity;
			if (building == null || !IsHumanOwnedBuilding(building) || !HasInstalledDirtyCashBackroomModule(__instance))
			{
				return;
			}

			CaptureRecordedDirtyCashAOEContribution(building);
			if (!_trackingIllegalBackroomBusinessTick)
			{
				RefreshRecordedDirtyCashAOEContributionNodes(building, "late-update");
			}
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
				if (HasInstalledDirtyCashBackroomModule(modules))
				{
					RefreshRecordedDirtyCashAOEContributionNodes(building, "fallback");
				}
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
				else if (HasInstalledDirtyCashBackroomModule(modules))
				{
					RefreshRecordedDirtyCashAOEContributionNodes(building, source);
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

		private static bool HasInstalledDirtyCashBackroomModule(ModulesComponent modules)
		{
			if (modules == null)
			{
				return false;
			}

			if (modules.bizmodules != null)
			{
				for (int i = 0; i < modules.bizmodules.Count; i++)
				{
					if (IsDirtyCashBackroomModule(modules.bizmodules[i]?.ModuleConfig))
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
				if (IsDirtyCashBackroomModule(slots[i]?.ModuleConfig))
				{
					return true;
				}
			}

			return false;
		}

		private static void ClearRecordedDirtyCashAOEContribution(Entity building)
		{
			EntityID buildingId = building?.Id ?? EntityID.INVALID;
			if (!buildingId.IsValid ||
				!RecordedDirtyCashAOEContributionsByBuilding.TryGetValue(buildingId, out List<DirtyCashAOEContributionEntry> contributions) ||
				contributions == null)
			{
				return;
			}

			for (int i = 0; i < contributions.Count; i++)
			{
				DirtyCashAOEContributionEntry entry = contributions[i];
				Node node = entry?.NodeId.FindNode();
				Respect respect = node?.respect?.GetOrNull(entry.Pid);
				if (respect == null)
				{
					continue;
				}

				Fixnum updated = respect.fromAOE - entry.Delta;
				respect.fromAOE = updated < Fixnum.ZERO ? Fixnum.ZERO : updated;
			}

			RecordedDirtyCashAOEContributionsByBuilding.Remove(buildingId);
		}

		private static void CaptureRecordedDirtyCashAOEContribution(Entity building)
		{
			EntityID buildingId = building?.Id ?? EntityID.INVALID;
			if (!buildingId.IsValid)
			{
				return;
			}

			List<DirtyCashAOEContributionEntry> contributions = BuildDirtyCashAOEContributionEntries(building);
			if (contributions == null || contributions.Count == 0)
			{
				RecordedDirtyCashAOEContributionsByBuilding.Remove(buildingId);
				return;
			}

			RecordedDirtyCashAOEContributionsByBuilding[buildingId] = contributions;
		}

		private static void RestoreRecordedDirtyCashAOEContribution(Entity building, string source)
		{
			EntityID buildingId = building?.Id ?? EntityID.INVALID;
			if (!buildingId.IsValid ||
				!RecordedDirtyCashAOEContributionsByBuilding.TryGetValue(buildingId, out List<DirtyCashAOEContributionEntry> contributions) ||
				contributions == null ||
				contributions.Count == 0)
			{
				return;
			}

			HashSet<Node> affectedNodes = new HashSet<Node>();
			for (int i = 0; i < contributions.Count; i++)
			{
				DirtyCashAOEContributionEntry entry = contributions[i];
				Node node = entry?.NodeId.FindNode();
				if (node == null || entry.Delta == Fixnum.ZERO)
				{
					continue;
				}

				node.respect.IncrementAOERespect(entry.Pid, entry.Delta);
				affectedNodes.Add(node);
			}

			RefreshDirtyCashAOEAffectedNodes(affectedNodes, buildingId, source);
		}

		private static void RefreshRecordedDirtyCashAOEContributionNodes(Entity building, string source)
		{
			EntityID buildingId = building?.Id ?? EntityID.INVALID;
			if (!buildingId.IsValid ||
				!RecordedDirtyCashAOEContributionsByBuilding.TryGetValue(buildingId, out List<DirtyCashAOEContributionEntry> contributions) ||
				contributions == null ||
				contributions.Count == 0)
			{
				return;
			}

			HashSet<Node> affectedNodes = new HashSet<Node>();
			for (int i = 0; i < contributions.Count; i++)
			{
				Node node = contributions[i]?.NodeId.FindNode();
				if (node != null)
				{
					affectedNodes.Add(node);
				}
			}

			RefreshDirtyCashAOEAffectedNodes(affectedNodes, buildingId, source);
		}

		private static void RefreshDirtyCashAOEAffectedNodes(HashSet<Node> affectedNodes, EntityID buildingId, string source)
		{
			if (affectedNodes == null || affectedNodes.Count == 0)
			{
				return;
			}

			LogDirtyCashTerritoryOwnerMismatches(affectedNodes, buildingId, source);
			string logKey = "aoe-respect|" + source + "|" + buildingId + "|day=" + (global::Game.Game.ctx?.clock?.Now.days ?? -1);
			if (LoggedIllegalBackroomBusinessTickFallbacks.Add(logKey))
			{
				AfterProhibitionEconomyPlugin.Log?.LogInfo(
					"dirty-cash-runtime aoe-respect restored source=" +
					source +
					" building=" +
					buildingId +
					" nodes=" +
					affectedNodes.Count +
					" ownershipAuthority=deferred-to-business-recalculate" +
					" ownershipSwitches=0");
			}
		}

		private static List<DirtyCashAOEContributionEntry> BuildDirtyCashAOEContributionEntries(Entity building)
		{
			ModulesComponent modules = building?.components?.modules;
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				return null;
			}

			ModuleQuery moduleQuery = ModulesUtil.MakeModuleQuery(building);
			if (!moduleQuery.OwnerIsHumanPlayer)
			{
				return null;
			}

			Node originNode = building?.data?.board?.bead.nodeId.FindNode();
			if (originNode == null)
			{
				return null;
			}

			int currentDay = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;
			Dictionary<string, DirtyCashAOEContributionEntry> aggregated = new Dictionary<string, DirtyCashAOEContributionEntry>(StringComparer.Ordinal);
			for (int i = 0; i < slots.Count; i++)
			{
				if (!(slots[i] is ConsumerModule consumer) || !IsDirtyCashBackroomModule(consumer.ModuleConfig))
				{
					continue;
				}

				bool activeThisTurn = consumer.DidModuleConsumeThisTurn;
				if (!activeThisTurn)
				{
					try
					{
						activeThisTurn = currentDay != int.MinValue && consumer.data != null && currentDay >= consumer.data.EnableTime.days;
					}
					catch
					{
						activeThisTurn = false;
					}
				}

				if (!activeThisTurn)
				{
					continue;
				}

				ConsumerRecipe.AreaDef aoe = consumer.config?.sink?.aoe;
				if (aoe?.respectRadius == null || aoe.respectPointsInRadius == null)
				{
					continue;
				}

				ModQuery managerQuery = moduleQuery.MakeManagerModQuery();
				managerQuery.nodeId = originNode.id;
				Fixnum radius = aoe.respectRadius.Evaluate(managerQuery);
				Fixnum delta = aoe.respectPointsInRadius.Evaluate(managerQuery);
				if (!delta.IsNotZero)
				{
					continue;
				}

				List<Node> nodesInRadius = global::Game.Game.ctx.board.nodes.FindAndSortNodesInRadius(originNode.pos, (float)radius, sort: false);
				for (int j = 0; j < nodesInRadius.Count; j++)
				{
					Node node = nodesInRadius[j];
					if (node == null)
					{
						continue;
					}

					string key = node.id + "|" + moduleQuery.pid;
					if (!aggregated.TryGetValue(key, out DirtyCashAOEContributionEntry entry))
					{
						entry = new DirtyCashAOEContributionEntry
						{
							NodeId = node.id,
							Pid = moduleQuery.pid,
							Delta = Fixnum.ZERO
						};
						aggregated[key] = entry;
					}

					entry.Delta += delta;
				}
			}

			return aggregated.Count == 0 ? null : new List<DirtyCashAOEContributionEntry>(aggregated.Values);
		}

		private static int ReconcileHumanTerritoryOwnershipUntilStable(IEnumerable<Node> nodes, EntityID buildingId, string source)
		{
			List<Node> nodeList = BuildNodeList(nodes);
			if (nodeList.Count == 0)
			{
				return 0;
			}

			HashSet<NodeID> sourceAOENodeIds = new HashSet<NodeID>();
			for (int i = 0; i < nodeList.Count; i++)
			{
				if (nodeList[i]?.id.IsValid == true)
				{
					sourceAOENodeIds.Add(nodeList[i].id);
				}
			}

			int totalSwitchCount = 0;
			HashSet<PlayerID> affectedPlayers = new HashSet<PlayerID>();
			for (int pass = 0; pass < MaxTerritoryReconcilePasses; pass++)
			{
				int passSwitchCount = ReconcileHumanTerritoryOwnershipPass(
					nodeList,
					affectedPlayers,
					sourceAOENodeIds,
					buildingId,
					source,
					pass);
				totalSwitchCount += passSwitchCount;
				if (passSwitchCount <= 0)
				{
					break;
				}

				nodeList = RefreshTerritoryDerivedRespectAfterOwnershipChange();
				if (nodeList.Count == 0)
				{
					break;
				}
			}

			return totalSwitchCount;
		}

		private static int ReconcileHumanTerritoryOwnershipPass(
			IEnumerable<Node> nodes,
			HashSet<PlayerID> affectedPlayers,
			HashSet<NodeID> sourceAOENodeIds,
			EntityID buildingId,
			string source,
			int pass)
		{
			PlayerInfo humanPlayer = global::Game.Game.ctx?.players?.Human;
			PlayerID humanPid = humanPlayer?.PID ?? PlayerID.INVALID;
			if (humanPid.IsNotValid || nodes == null || affectedPlayers == null)
			{
				return 0;
			}

			global::Game.Services.RespectSettings settings = global::Game.Game.serv?.globals?.settings?.people?.social?.respect;
			if (settings == null)
			{
				return 0;
			}

			int switchCount = 0;
			foreach (Node node in nodes)
			{
				if (node?.respect?.data == null || node.respect.data.Count == 0)
				{
					continue;
				}

				Respect humanRespect = node.respect.GetOrNull(humanPid);
				if (humanRespect == null)
				{
					continue;
				}

				PlayerID nodeOwner = node.owner.Get();
				Fixnum claimStrength = humanRespect.current;
				Fixnum lossThreshold = settings.lossThreshold.Evaluate(humanPid);
				if (claimStrength <= lossThreshold && nodeOwner == humanPid)
				{
					LogDirtyCashTerritorySwitch(
						node,
						buildingId,
						source,
						pass,
						sourceAOENodeIds?.Contains(node.id) == true,
						humanPid,
						nodeOwner,
						PlayerID.INVALID,
						humanRespect,
						gainThreshold: settings.gainThreshold.Evaluate(new ModQuery(humanPid, node)),
						lossThreshold);
					PlayerTerritory.ClearNodeOwner(node, humanPid, PlayerID.INVALID);
					affectedPlayers.Add(humanPid);
					RecordDirtyCashTerritoryOwnerSnapshot(node, buildingId, PlayerID.INVALID, source);
					switchCount++;
					continue;
				}

				if (nodeOwner == humanPid)
				{
					continue;
				}

				Fixnum gainThreshold = settings.gainThreshold.Evaluate(new ModQuery(humanPid, node));
				bool shouldClaimUnowned = nodeOwner.IsNotValid && claimStrength >= gainThreshold;
				bool shouldTakeOwned = nodeOwner.IsValid &&
					claimStrength >= gainThreshold &&
					claimStrength >= GetHighestCompetingCurrentRespect(node, humanPid);
				if (!shouldClaimUnowned && !shouldTakeOwned)
				{
					continue;
				}

				LogDirtyCashTerritorySwitch(
					node,
					buildingId,
					source,
					pass,
					sourceAOENodeIds?.Contains(node.id) == true,
					humanPid,
					nodeOwner,
					humanPid,
					humanRespect,
					gainThreshold,
					lossThreshold);
				if (nodeOwner.IsValid)
				{
					PlayerTerritory.ClearNodeOwner(node, nodeOwner, humanPid);
					affectedPlayers.Add(nodeOwner);
				}

				PlayerTerritory.SetNodeOwner(node, humanPid);
				affectedPlayers.Add(humanPid);
				RecordDirtyCashTerritoryOwnerSnapshot(node, buildingId, humanPid, source);
				switchCount++;
			}

			return switchCount;
		}

		private static List<Node> RefreshTerritoryDerivedRespectAfterOwnershipChange()
		{
			List<Node> allNodes = BuildNodeList(global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe());
			if (allNodes.Count == 0)
			{
				return allNodes;
			}

			foreach (Node node in allNodes)
			{
				if (node?.respect?.data == null)
				{
					continue;
				}

				for (int i = 0; i < node.respect.data.Count; i++)
				{
					if (node.respect.data[i] != null)
					{
						node.respect.data[i].fromNeighbors = Fixnum.ZERO;
					}
				}
			}

			MethodInfo updateRespectFromTerritory = AccessTools.Method(typeof(BusinessUpdate), "UpdateRespectFromTerritory");
			try
			{
				updateRespectFromTerritory?.Invoke(null, null);
			}
			catch
			{
			}

			foreach (Node node in allNodes)
			{
				if (node != null)
				{
					HeatAndRespect.RecomputeRespectForAllPlayers(node, true);
				}
			}

			return allNodes;
		}

		private static List<Node> BuildNodeList(IEnumerable<Node> nodes)
		{
			List<Node> nodeList = new List<Node>();
			if (nodes == null)
			{
				return nodeList;
			}

			foreach (Node node in nodes)
			{
				if (node != null)
				{
					nodeList.Add(node);
				}
			}

			return nodeList;
		}

		private static Fixnum GetHighestCompetingCurrentRespect(Node node, PlayerID excludedPid)
		{
			if (node?.respect?.data == null || node.respect.data.Count == 0)
			{
				return Fixnum.ZERO;
			}

			Fixnum highest = Fixnum.ZERO;
			for (int i = 0; i < node.respect.data.Count; i++)
			{
				Respect respect = node.respect.data[i];
				if (respect == null || respect.pid == excludedPid)
				{
					continue;
				}

				if (respect.current > highest)
				{
					highest = respect.current;
				}
			}

			return highest;
		}

		private static void LogDirtyCashTerritoryOwnerMismatches(IEnumerable<Node> affectedNodes, EntityID buildingId, string source)
		{
			if (affectedNodes == null)
			{
				return;
			}

			int day = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;
			foreach (Node node in affectedNodes)
			{
				if (node == null ||
					!DirtyCashTerritoryOwnerSnapshotsByNode.TryGetValue(node.id, out DirtyCashTerritoryOwnerSnapshot snapshot))
				{
					continue;
				}

				PlayerID actualOwner = node.owner.Get();
				if (actualOwner == snapshot.ExpectedOwner)
				{
					continue;
				}

				string logKey = node.id + "|" + day + "|" + source + "|" + buildingId;
				if (!LoggedDirtyCashTerritoryOwnerMismatchDiagnostics.Add(logKey))
				{
					continue;
				}

				AfterProhibitionEconomyPlugin.Log?.LogWarning(
					"dirty-cash-territory owner-mismatch-before-restore" +
					" node=" + node.id +
					" expectedOwner=" + snapshot.ExpectedOwner +
					" actualOwner=" + actualOwner +
					" priorBuilding=" + snapshot.BuildingId +
					" priorSource=" + snapshot.Source +
					" priorDay=" + snapshot.Day +
					" building=" + buildingId +
					" source=" + source +
					" day=" + day);
			}
		}

		private static void LogDirtyCashTerritorySwitch(
			Node node,
			EntityID buildingId,
			string source,
			int pass,
			bool insideSourceAOE,
			PlayerID humanPid,
			PlayerID previousOwner,
			PlayerID newOwner,
			Respect humanRespect,
			Fixnum gainThreshold,
			Fixnum lossThreshold,
			string authority = "direct-reconcile")
		{
			if (node == null || humanRespect == null)
			{
				return;
			}

			int day = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;
			string logKey = node.id + "|" + day + "|" + source + "|" + buildingId + "|" + previousOwner + "|" + newOwner;
			if (!LoggedDirtyCashTerritorySwitchDiagnostics.Add(logKey))
			{
				return;
			}

			Respect competitorRespect = FindHighestCompetingRespect(node, humanPid);
			PlayerID competitorPid = competitorRespect?.pid ?? PlayerID.INVALID;
			PlayerInfo competitor = competitorPid.IsValid ? competitorPid.FindPlayer() : null;
			EntityID safehouseId = competitor?.territory?.Safehouse ?? EntityID.INVALID;
			Entity safehouse = safehouseId.IsValid ? safehouseId.FindEntity() : null;
			int outpostCount = competitor?.outposts?.GetOutpostEntriesUnsafe()?.Count ?? 0;
			int controlledBuildingCount = 0;
			try
			{
				controlledBuildingCount = competitor?.territory?.CountControlledBuildings() ?? 0;
			}
			catch
			{
				controlledBuildingCount = -1;
			}

			string competitorType = competitor == null
				? "none"
				: competitor.IsJustGoon
					? "troublemaker"
					: competitor.IsJustGang
						? "gang"
						: competitor.PlayerType.ToString();

			AfterProhibitionEconomyPlugin.Log?.LogWarning(
				"dirty-cash-territory ownership-switch" +
				" authority=" + authority +
				" building=" + buildingId +
				" source=" + source +
				" pass=" + pass +
				" node=" + node.id +
				" insideSourceAOE=" + insideSourceAOE +
				" previousOwner=" + previousOwner +
				" newOwner=" + newOwner +
				" humanCurrent=" + humanRespect.current +
				" humanGoal=" + humanRespect.goal +
				" humanAOE=" + humanRespect.fromAOE +
				" humanNeighbors=" + humanRespect.fromNeighbors +
				" humanSafehouse=" + humanRespect.fromSafehouse +
				" gainThreshold=" + gainThreshold +
				" lossThreshold=" + lossThreshold +
				" competitorPid=" + competitorPid +
				" competitorType=" + competitorType +
				" competitorCurrent=" + (competitorRespect?.current ?? Fixnum.ZERO) +
				" competitorGoal=" + (competitorRespect?.goal ?? Fixnum.ZERO) +
				" competitorAOE=" + (competitorRespect?.fromAOE ?? Fixnum.ZERO) +
				" competitorNeighbors=" + (competitorRespect?.fromNeighbors ?? Fixnum.ZERO) +
				" competitorSafehouse=" + (competitorRespect?.fromSafehouse ?? Fixnum.ZERO) +
				" competitorDefeated=" + (competitor?.crew?.IsCrewDefeated ?? false) +
				" competitorOutposts=" + outpostCount +
				" competitorBuildings=" + controlledBuildingCount +
				" competitorOwnedNodes=" + (competitor?.territory?.OwnedNodeCount ?? 0) +
				" safehouseId=" + safehouseId +
				" safehouseExists=" + (safehouse != null) +
				" safehouseFlag=" + (safehouse?.components?.building?.IsSafehouse ?? false) +
				" safehouseOwner=" + (safehouse?.components?.building?.SafehouseOwner ?? PlayerID.INVALID) +
				" safehouseVanquished=" + (competitor?.territory?.IsSafehouseVanquished ?? true) +
				" day=" + day);
		}

		private static void RecordDirtyCashTerritoryOwnerSnapshot(Node node, EntityID buildingId, PlayerID expectedOwner, string source)
		{
			if (node == null)
			{
				return;
			}

			DirtyCashTerritoryOwnerSnapshotsByNode[node.id] = new DirtyCashTerritoryOwnerSnapshot
			{
				BuildingId = buildingId,
				ExpectedOwner = expectedOwner,
				Source = source ?? string.Empty,
				Day = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue
			};
		}

		private static Respect FindHighestCompetingRespect(Node node, PlayerID excludedPid)
		{
			if (node?.respect?.data == null)
			{
				return null;
			}

			Respect highest = null;
			for (int i = 0; i < node.respect.data.Count; i++)
			{
				Respect candidate = node.respect.data[i];
				if (candidate == null || candidate.pid == excludedPid)
				{
					continue;
				}

				if (highest == null || candidate.current > highest.current)
				{
					highest = candidate;
				}
			}

			return highest;
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

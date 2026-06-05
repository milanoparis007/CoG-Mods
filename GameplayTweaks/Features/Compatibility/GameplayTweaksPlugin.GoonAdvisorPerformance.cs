using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Sim.Modules;
using HarmonyLib;
using SomaSim.Util;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace GameplayTweaks
{
	internal static class GoonAdvisorPerformancePatch
	{
		private const int FastTargetMaxNodes = 64;
		private const int FastTargetMaxCandidates = 3;
		private const long FastTargetLogThresholdMs = 12;

		private sealed class FastTargetTimingState
		{
			public long VisitInvokeMs;
			public long SafetyMs;
			public long FindBuildingsMs;
			public long PredicateMs;
			public int PredicateCalls;
			public long MaxPredicateMs;
			public string MaxPredicateBuildingId = "none";
			public string MaxPredicateTemplate = "unknown";
		}

		private static readonly FieldInfo GoonAdvisorDefField = AccessTools.Field(typeof(GoonAdvisor), "_def");
		private static readonly FieldInfo AiAdvisorPidField = AccessTools.Field(typeof(AIAdvisor), "_pid");
		private static readonly MethodInfo VisitNeighborhoodBfsMethod = AccessTools.Method(typeof(NodeManager), "VisitNeighborhoodBFS");

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo findTargetWithBfs = AccessTools.Method(typeof(GoonAdvisor), "FindTargetWithBFS");
				if (findTargetWithBfs == null)
				{
					Debug.LogWarning("[GameplayTweaks] GoonAdvisor.FindTargetWithBFS not found for performance guard");
					return;
				}

				harmony.Patch(
					findTargetWithBfs,
					prefix: new HarmonyMethod(typeof(GoonAdvisorPerformancePatch), nameof(FindTargetWithBfsPrefix)));

				MethodInfo buildingHasInventoryToSteal = AccessTools.Method(typeof(GoonAdvisor), "BuildingHasInventoryToSteal", new[] { typeof(Entity) });
				if (buildingHasInventoryToSteal != null)
				{
					harmony.Patch(
						buildingHasInventoryToSteal,
						prefix: new HarmonyMethod(typeof(GoonAdvisorPerformancePatch), nameof(BuildingHasInventoryToStealPrefix)));
				}

				Debug.Log("[GameplayTweaks] GoonAdvisor fast target search enabled");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GoonAdvisor performance patch setup failed: " + ex.Message);
			}
		}

		private static bool FindTargetWithBfsPrefix(GoonAdvisor __instance, Node start, Predicate<Entity> test, ref Entity __result)
		{
			long startTicks = Stopwatch.GetTimestamp();
			int visitedNodes = 0;
			int scannedBuildings = 0;
			FastTargetTimingState timing = new FastTargetTimingState();
			try
			{
				__result = null;
				object nodeManager = global::Game.Game.ctx?.board?.nodes;
				if (__instance == null || start == null || test == null || nodeManager == null)
				{
					return false;
				}

				if (VisitNeighborhoodBfsMethod == null)
				{
					return true;
				}

				float maxDistance = TryGetTargetRange(__instance);
				List<Entity> candidates = new List<Entity>(FastTargetMaxCandidates);
				Action<Node> visitNode = delegate(Node node)
				{
					visitedNodes++;
					Entity target = FindBuildingAtNodeFast(__instance, node, test, timing, out int nodeBuildings);
					scannedBuildings += nodeBuildings;
					if (target != null && candidates.Count < FastTargetMaxCandidates)
					{
						candidates.Add(target);
					}
				};
				Predicate<Node> nodeTest = node => node != null
						&& candidates.Count < FastTargetMaxCandidates
						&& visitedNodes < FastTargetMaxNodes
						&& (start.pos - node.pos).Magnitude <= maxDistance;
				long visitTicks = Stopwatch.GetTimestamp();
				VisitNeighborhoodBfsMethod.Invoke(
					nodeManager,
					new object[]
					{
						start,
						FastTargetMaxNodes,
						visitNode,
						nodeTest,
						null,
						null,
						true
					});
				timing.VisitInvokeMs = GetElapsedMilliseconds(visitTicks);

				if (candidates.Count > 0)
				{
					__result = candidates[SelectCandidateIndex(__instance, candidates.Count)];
				}

				LogIfSlow(__instance, startTicks, visitedNodes, scannedBuildings, candidates.Count, __result != null, timing);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GoonAdvisor fast target search failed; falling back to vanilla. " + ex.Message);
				return true;
			}
		}

		private static bool BuildingHasInventoryToStealPrefix(Entity building, ref bool __result)
		{
			try
			{
				InventoryModule inventory = ModulesUtil.GetInventory(building);
				List<ResourceAndQty> contents = inventory?.data?.contents;
				if (contents == null || contents.Count == 0)
				{
					__result = false;
					return false;
				}

				for (int i = 0; i < contents.Count; i++)
				{
					if (contents[i].qty > 0)
					{
						__result = true;
						return false;
					}
				}

				__result = false;
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static Entity FindBuildingAtNodeFast(GoonAdvisor advisor, Node node, Predicate<Entity> test, FastTargetTimingState timing, out int scannedBuildings)
		{
			scannedBuildings = 0;
			if (node == null)
			{
				return null;
			}

			long safetyTicks = Stopwatch.GetTimestamp();
			bool isSafe = advisor.IsNodeSafeFromTargetting(node);
			timing.SafetyMs += GetElapsedMilliseconds(safetyTicks);
			if (isSafe)
			{
				return null;
			}

			using (ListPool<Entity>.PooledBlockList buildings = ListPool<Entity>.Allocate())
			{
				long findBuildingsTicks = Stopwatch.GetTimestamp();
				node.FindAllInterestingBuildings(buildings);
				timing.FindBuildingsMs += GetElapsedMilliseconds(findBuildingsTicks);
				foreach (Entity building in buildings)
				{
					scannedBuildings++;
					if (building?.data?.building == null || !building.data.building.business.IsValid)
					{
						continue;
					}

					long predicateTicks = Stopwatch.GetTimestamp();
					bool matches;
					try
					{
						matches = test(building);
					}
					finally
					{
						long predicateMs = GetElapsedMilliseconds(predicateTicks);
						timing.PredicateMs += predicateMs;
						timing.PredicateCalls++;
						if (predicateMs > timing.MaxPredicateMs)
						{
							timing.MaxPredicateMs = predicateMs;
							timing.MaxPredicateBuildingId = building.Id.ToString();
							timing.MaxPredicateTemplate = building.config?.Template.String ?? "unknown";
						}
					}

					if (matches)
					{
						return building;
					}
				}
			}

			return null;
		}

		private static float TryGetTargetRange(GoonAdvisor advisor)
		{
			try
			{
				if (GoonAdvisorDefField?.GetValue(advisor) is GoonAdvisorConfig def && def.targetRange > 0f)
				{
					return def.targetRange;
				}
			}
			catch
			{
			}

			return 18f;
		}

		private static int SelectCandidateIndex(GoonAdvisor advisor, int count)
		{
			if (count <= 1)
			{
				return 0;
			}

			int pid = TryGetPid(advisor);
			int day = -1;
			try
			{
				day = global::Game.Game.ctx?.clock?.Now.days ?? -1;
			}
			catch
			{
			}

			int hash = ((pid * 397) ^ (day * 17)) & int.MaxValue;
			return hash % count;
		}

		private static int TryGetPid(GoonAdvisor advisor)
		{
			try
			{
				if (AiAdvisorPidField?.GetValue(advisor) is PlayerID pid)
				{
					return pid.id;
				}
			}
			catch
			{
			}

			return -1;
		}

		private static void LogIfSlow(GoonAdvisor advisor, long startTicks, int visitedNodes, int scannedBuildings, int candidates, bool found, FastTargetTimingState timing)
		{
			long elapsedMs = GetElapsedMilliseconds(startTicks);
			if (elapsedMs < FastTargetLogThresholdMs)
			{
				return;
			}

			GameplayTweaksPlugin.VerificationLog(
				"GoonAdvisor",
				$"fast-target pid={TryGetPid(advisor)} ms={elapsedMs} visitedNodes={visitedNodes} scannedBuildings={scannedBuildings} candidates={candidates} found={found} cap={FastTargetMaxNodes} visitInvokeMs={timing.VisitInvokeMs} safetyMs={timing.SafetyMs} findBuildingsMs={timing.FindBuildingsMs} predicateMs={timing.PredicateMs} predicateCalls={timing.PredicateCalls} maxPredicateMs={timing.MaxPredicateMs} maxPredicateBuilding={timing.MaxPredicateBuildingId} maxPredicateTemplate={timing.MaxPredicateTemplate}");
		}

		private static long GetElapsedMilliseconds(long startTicks)
		{
			return (Stopwatch.GetTimestamp() - startTicks) * 1000L / Stopwatch.Frequency;
		}
	}

	internal static class UnitsAdvisorPerformancePatch
	{
		private const int RecruitCandidateCacheLimit = 256;
		private const int RecruitPickAttempts = 12;
		private const long RecruitCacheLogThresholdMs = 12;

		private static readonly FieldInfo UnitsAdvisorDefField = AccessTools.Field(typeof(UnitsAdvisor), "_def");
		private static readonly FieldInfo UnitsAdvisorDataField = AccessTools.Field(typeof(UnitsAdvisor), "_data");
		private static readonly FieldInfo AiAdvisorPidField = AccessTools.Field(typeof(AIAdvisor), "_pid");
		private static readonly FieldInfo AiAdvisorPlayerField = AccessTools.Field(typeof(AIAdvisor), "_player");

		private static readonly List<Entity> EligibleRecruitCache = new List<Entity>(RecruitCandidateCacheLimit);
		private static int _eligibleRecruitCacheDay = int.MinValue;

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo grow = AccessTools.Method(typeof(UnitsAdvisor), "Grow");
				if (grow == null)
				{
					Debug.LogWarning("[GameplayTweaks] UnitsAdvisor.Grow not found for performance guard");
					return;
				}

				harmony.Patch(
					grow,
					prefix: new HarmonyMethod(typeof(UnitsAdvisorPerformancePatch), nameof(GrowPrefix)));
				Debug.Log("[GameplayTweaks] UnitsAdvisor fast recruit search enabled");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] UnitsAdvisor performance patch setup failed: " + ex.Message);
			}
		}

		private static bool GrowPrefix(UnitsAdvisor __instance)
		{
			long startTicks = Stopwatch.GetTimestamp();
			try
			{
				if (__instance == null
					|| !(UnitsAdvisorDefField?.GetValue(__instance) is UnitsAdvisorConfig def)
					|| !(UnitsAdvisorDataField?.GetValue(__instance) is UnitsAdvisorData data)
					|| !(AiAdvisorPlayerField?.GetValue(__instance) is PlayerInfo player)
					|| !(AiAdvisorPidField?.GetValue(__instance) is PlayerID pid))
				{
					return true;
				}

				float probability = (float)def.growthChance.Evaluate(pid);
				if (!data.rng.CheckProbability(probability))
				{
					return false;
				}

				Entity recruit = PickEligibleRecruit(data);
				if (recruit == null)
				{
					LogRecruitSearch(__instance, startTicks, "none", 0);
					return false;
				}

				Node headquartersNode = player.territory.GetHeadquartersNode();
				if (headquartersNode == null)
				{
					return false;
				}

				player.crew.HireNewCrewInVehicle(headquartersNode, recruit, null, isBoss: false);
				EligibleRecruitCache.Remove(recruit);
				AILog.LogAIDecision(pid, __instance, $"Added new crew member, total = {player.crew.TotalCrewCount}");
				AILog.LogMilestone(pid, recruit.Id, $"{pid} added new crew member, total = {player.crew.TotalCrewCount}");
				LogRecruitSearch(__instance, startTicks, "hired", EligibleRecruitCache.Count);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] UnitsAdvisor fast recruit search failed; falling back to vanilla. " + ex.Message);
				return true;
			}
		}

		private static Entity PickEligibleRecruit(UnitsAdvisorData data)
		{
			RefreshRecruitCacheIfNeeded();
			for (int attempt = 0; attempt < RecruitPickAttempts && EligibleRecruitCache.Count > 0; attempt++)
			{
				Entity candidate = data.rng.PickElement(EligibleRecruitCache);
				if (candidate != null && PlayerSocial.IsEligibleCrewMember(global::Game.Game.ctx.clock.Now, candidate))
				{
					return candidate;
				}

				EligibleRecruitCache.Remove(candidate);
			}

			return null;
		}

		private static void RefreshRecruitCacheIfNeeded()
		{
			SimTime now = global::Game.Game.ctx.clock.Now;
			if (_eligibleRecruitCacheDay == now.days)
			{
				return;
			}

			long startTicks = Stopwatch.GetTimestamp();
			_eligibleRecruitCacheDay = now.days;
			EligibleRecruitCache.Clear();
			HashSet<Entity> people = global::Game.Game.ctx.entityman.GetCachedEntitiesPersonsUnsafe();
			if (people != null)
			{
				foreach (Entity person in people)
				{
					if (person == null || !PlayerSocial.IsEligibleCrewMember(now, person))
					{
						continue;
					}

					EligibleRecruitCache.Add(person);
					if (EligibleRecruitCache.Count >= RecruitCandidateCacheLimit)
					{
						break;
					}
				}
			}

			long elapsedMs = (Stopwatch.GetTimestamp() - startTicks) * 1000L / Stopwatch.Frequency;
			if (elapsedMs >= RecruitCacheLogThresholdMs)
			{
				GameplayTweaksPlugin.VerificationLog("UnitsAdvisor", $"recruit-cache day={now.days} candidates={EligibleRecruitCache.Count} ms={elapsedMs} limit={RecruitCandidateCacheLimit}");
			}
		}

		private static void LogRecruitSearch(UnitsAdvisor advisor, long startTicks, string result, int remainingCandidates)
		{
			long elapsedMs = (Stopwatch.GetTimestamp() - startTicks) * 1000L / Stopwatch.Frequency;
			if (elapsedMs < RecruitCacheLogThresholdMs)
			{
				return;
			}

			int pid = -1;
			try
			{
				if (AiAdvisorPidField?.GetValue(advisor) is PlayerID playerId)
				{
					pid = playerId.id;
				}
			}
			catch
			{
			}

			GameplayTweaksPlugin.VerificationLog("UnitsAdvisor", $"fast-grow pid={pid} result={result} ms={elapsedMs} remainingCandidates={remainingCandidates}");
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Popups;
using HarmonyLib;
using SomaSim.Util;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class DirtyCashEconomyCompatibilityPatch
	{
		private static readonly string[] DuplicateChoiceSuffixes = new string[]
		{
			"-alpha",
			"-return",
			"-upgraded-upgrade",
			"-upgraded",
			"-improved-upgrade",
			"-improved"
		};

		private static readonly string[] CitySuffixes = new string[]
		{
			"-atlantic-city",
			"-pittsburgh",
			"-cincinnati",
			"-detroit",
			"-new-york"
		};

		private const string ExternalDirtyCashHarmonyId = "com.cogmod.dirtycasheconomy";
		private const int MaxHumanBackgroundTerritoryReconcilePasses = 8;
		private const int IllegalBackroomVisualAuditRetryBudget = 4;
		private const int GangOpsStackedRespectRepairThreshold = 500;
		private static readonly Stack<bool> ExternalConsumerBypassStack = new Stack<bool>();
		private static readonly HashSet<string> LoggedIllegalBackroomManufactureUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedIllegalBackroomConsumerUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedIllegalBackroomConsumerIdleStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedPlayerLegalBusinessConsumerUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedPlayerLegalBusinessConsumerIdleStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> ObservedIllegalBackroomBuildingUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> PreSystemForcedIllegalBackroomBuildingUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<EntityID> UpdatedIllegalBackroomBuildingsThisTick = new HashSet<EntityID>();
		private static readonly HashSet<string> LoggedIllegalBackroomBusinessTickFallbacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedIllegalBackroomSafetySweepSummaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedIllegalBackroomDeferredVisualRefreshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<int, int> LastGangCollapseTerritoryAuditDayByPid = new Dictionary<int, int>();
		private static readonly Dictionary<int, int> LastGangCollapseTerritoryDeferredDayByPid = new Dictionary<int, int>();
		private static readonly Dictionary<int, int> LastObservedOutpostCountByPid = new Dictionary<int, int>();
		private static readonly HashSet<int> PendingGangCollapseTerritoryRebuildPids = new HashSet<int>();
		private static readonly Dictionary<EntityID, List<DirtyCashAOEContributionEntry>> RecordedDirtyCashAOEContributionsByBuilding = new Dictionary<EntityID, List<DirtyCashAOEContributionEntry>>();
		private static readonly Dictionary<EntityID, PendingIllegalBackroomVisualRefresh> PendingIllegalBackroomVisualRefreshes = new Dictionary<EntityID, PendingIllegalBackroomVisualRefresh>();
		private static readonly Dictionary<string, LateForcedDirtyCashRespectSnapshot> LateForcedDirtyCashRespectCurrentSnapshot = new Dictionary<string, LateForcedDirtyCashRespectSnapshot>(StringComparer.Ordinal);
		private static Harmony _compatHarmony;
		private static MethodInfo _externalDirtyCashAddIncomeMethod;
		private static object _externalDirtyCashBackroomReason;
		private static int LateForcedDirtyCashRespectSnapshotDay = int.MinValue;
		private static bool _pendingDeferredHumanTerritoryRefresh;
		private static int _pendingDeferredHumanTerritoryRefreshEarliestFrame = -1;
		private static int _pendingDeferredHumanTerritoryRefreshRemainingPasses;
		private static string _pendingDeferredHumanTerritoryRefreshSource = string.Empty;
		private static bool _pendingDeferredHumanTerritoryFullRebuild;
		private static string _pendingDeferredHumanTerritoryFullRebuildSource = string.Empty;
		private static bool _loadedSessionTerritoryRefreshCompleted;
		private static string _lastDeferredHumanTerritoryRefreshWaitReason = string.Empty;
		private static string _lastDeferredHumanTerritoryRefreshScheduleReason = string.Empty;
		private static bool _loggedExternalManufactureBypass;
		private static bool _loggedExternalConsumerBypass;
		private static bool _loggedExternalDirtyCashOriginalOverrideRemoval;
		private static bool _trackingIllegalBackroomBusinessTick;
		private static bool _externalDirtyCashOriginalOverridesRemoved;
		private static bool _gangOpsStackedTerritoryRespectRepairAttempted;
		private static int _deferredHumanTerritoryRefreshDuplicateRequestCount;
		private static int _gangCollapseTerritoryDeferredSuppressedCount;
		private static MethodInfo _getPickContainerMethod;
		private static MethodInfo _pickContainerAddOrRefreshMethod;
		[ThreadStatic]
		private static Stack<EntityID> _illegalBackroomDirtyCashMoneyContextStack;

		private sealed class LateForcedDirtyCashRespectSnapshot
		{
			public Fixnum Current;
			public Fixnum FromRelationships;
			public Fixnum FromNeighbors;
			public Fixnum FromEthnicity;
			public Fixnum FromSafehouse;
		}

		private sealed class DirtyCashAOEContributionEntry
		{
			public NodeID NodeId;
			public PlayerID Pid;
			public Fixnum Delta;
		}

		private sealed class PendingIllegalBackroomVisualRefresh
		{
			public int EarliestFrame = -1;
			public int RemainingPasses;
			public int RemainingAuditRetries = IllegalBackroomVisualAuditRetryBudget;
			public string Source = string.Empty;
			public string LastWaitReason = string.Empty;
			public bool AppliedLateRespectSync;
		}

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				_compatHarmony = harmony;
				MethodInfo findModulesMethod = AccessTools.Method(typeof(OwnedBizController), "FindModulesToAddForSlot");
				if (findModulesMethod != null)
				{
					harmony.Patch(findModulesMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(FindModulesToAddForSlotPostfix)));
				}

				MethodInfo findUpgradesMethod = AccessTools.Method(typeof(ModulesUtil), "FindUpgradesOrNull");
				if (findUpgradesMethod != null)
				{
					harmony.Patch(findUpgradesMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(FindUpgradesOrNullPostfix)));
				}

				MethodInfo canInstallMethod = AccessTools.Method(typeof(OwnedBizController), "CanInstall");
				if (canInstallMethod != null)
				{
					harmony.Patch(canInstallMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(CanInstallPostfix)));
				}

				MethodInfo onModuleAddConfirmMethod = AccessTools.Method(typeof(OwnedBizController), "OnModuleAddConfirm");
				if (onModuleAddConfirmMethod != null)
				{
					harmony.Patch(onModuleAddConfirmMethod, prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(OnModuleAddConfirmPrefix)));
				}

				MethodInfo manufactureDoUpdateMethod = AccessTools.Method(typeof(ManufactureModule), "DoUpdate");
				if (manufactureDoUpdateMethod != null)
				{
					harmony.Patch(manufactureDoUpdateMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(IllegalBackroomManufactureDoUpdatePostfix)));
				}

				MethodInfo consumerDoUpdateMethod = AccessTools.Method(typeof(ConsumerModule), "DoUpdate");
				if (consumerDoUpdateMethod != null)
				{
					harmony.Patch(consumerDoUpdateMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(IllegalBackroomConsumerDoUpdatePostfix)));
				}

				MethodInfo consumerDoConsumeAndPayMethod = AccessTools.Method(typeof(ConsumerModule), "DoConsumeAndPay", new Type[]
				{
					typeof(ModuleQuery),
					typeof(InventoryModule),
					typeof(Fixnum),
					typeof(int)
				});
				if (consumerDoConsumeAndPayMethod != null)
				{
					harmony.Patch(consumerDoConsumeAndPayMethod,
						prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(ConsumerDoConsumeAndPayPrefix)),
						postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(ConsumerDoConsumeAndPayPostfix)));
				}

				MethodInfo manufactureDoConsumeAndProduceMethod = AccessTools.Method(typeof(ManufactureModule), "DoConsumeAndProduce", new Type[]
				{
					typeof(InventoryModule),
					typeof(Recipe),
					typeof(ModuleQuery)
				});
				if (manufactureDoConsumeAndProduceMethod != null)
				{
					harmony.Patch(manufactureDoConsumeAndProduceMethod,
						prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(ManufactureDoConsumeAndProduceInnerPrefix)),
						postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(ManufactureDoConsumeAndProduceInnerPostfix)));
				}

				MethodInfo modulesComponentDoUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new Type[]
				{
					typeof(SimTime),
					typeof(bool)
				});
				if (modulesComponentDoUpdateMethod != null)
				{
					harmony.Patch(modulesComponentDoUpdateMethod,
						prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(ModulesComponentDoUpdatePrefix)),
						postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(ModulesComponentDoUpdatePostfix)));
				}

				MethodInfo updateBusinessModulesMethod = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "UpdateBusinessModules");
				if (updateBusinessModulesMethod != null)
				{
					harmony.Patch(updateBusinessModulesMethod,
						prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(UpdateBusinessModulesPrefix)),
						postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(UpdateBusinessModulesPostfix)),
						finalizer: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(UpdateBusinessModulesFinalizer)));
				}

				MethodInfo updateGamblingModulesMethod = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "UpdateGamblingModules");
				if (updateGamblingModulesMethod != null)
				{
					harmony.Patch(updateGamblingModulesMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(UpdateGamblingModulesPostfix)));
				}

				MethodInfo businessTickMethod = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "Tick");
				if (businessTickMethod != null)
				{
					harmony.Patch(businessTickMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(BusinessUpdateTickPostfix)));
				}

				MethodInfo recalculateHeatAndRespectMethod = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "RecalculateHeatAndRespectForNodes");
				if (recalculateHeatAndRespectMethod != null)
				{
					harmony.Patch(recalculateHeatAndRespectMethod,
						prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(RecalculateHeatAndRespectForNodesPrefix)),
						postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(RecalculateHeatAndRespectForNodesPostfix)));
				}

				MethodInfo playerTurnStartedMethod = AccessTools.Method(typeof(PlayerInfo), "OnPlayerTurnStarted");
				if (playerTurnStartedMethod != null)
				{
					harmony.Patch(playerTurnStartedMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(PlayerInfoOnPlayerTurnStartedPostfix)));
				}

				MethodInfo globalTurnSetAdvancedMethod = AccessTools.Method(typeof(PlayerFinances), "OnGlobalTurnSetAdvanced");
				if (globalTurnSetAdvancedMethod != null)
				{
					harmony.Patch(globalTurnSetAdvancedMethod, postfix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(PlayerFinancesOnGlobalTurnSetAdvancedPostfix)));
				}

				MethodInfo doChangeMoneyEntityMethod = AccessTools.Method(typeof(PlayerFinances), "DoChangeMoney", new Type[]
				{
					typeof(Entity),
					typeof(Price),
					typeof(MoneyReason),
					typeof(EntityID?)
				});
				if (doChangeMoneyEntityMethod != null)
				{
					harmony.Patch(doChangeMoneyEntityMethod, prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(PlayerFinancesDoChangeMoneyEntityPrefix)));
				}

				TryPatchExternalDirtyCashConsumerHooks(harmony);
				TryPatchExternalDirtyCashManufacturePrefix(harmony);
				EnsureExternalDirtyCashOriginalOverridesRemoved();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Dirty cash compatibility patch setup failed: " + ex.Message);
			}
		}

		internal static void ResetRuntimeSessionState(string sourceTag)
		{
			ExternalConsumerBypassStack.Clear();
			LoggedIllegalBackroomManufactureUpdates.Clear();
			LoggedIllegalBackroomConsumerUpdates.Clear();
			LoggedIllegalBackroomConsumerIdleStates.Clear();
			LoggedPlayerLegalBusinessConsumerUpdates.Clear();
			LoggedPlayerLegalBusinessConsumerIdleStates.Clear();
			ObservedIllegalBackroomBuildingUpdates.Clear();
			PreSystemForcedIllegalBackroomBuildingUpdates.Clear();
			UpdatedIllegalBackroomBuildingsThisTick.Clear();
			LoggedIllegalBackroomBusinessTickFallbacks.Clear();
			LoggedIllegalBackroomSafetySweepSummaries.Clear();
			LoggedIllegalBackroomDeferredVisualRefreshes.Clear();
			RecordedDirtyCashAOEContributionsByBuilding.Clear();
			PendingIllegalBackroomVisualRefreshes.Clear();
			LateForcedDirtyCashRespectCurrentSnapshot.Clear();
			LateForcedDirtyCashRespectSnapshotDay = int.MinValue;
			_pendingDeferredHumanTerritoryRefresh = false;
			_pendingDeferredHumanTerritoryRefreshEarliestFrame = -1;
			_pendingDeferredHumanTerritoryRefreshRemainingPasses = 0;
			_pendingDeferredHumanTerritoryRefreshSource = string.Empty;
			_loadedSessionTerritoryRefreshCompleted = false;
			_lastDeferredHumanTerritoryRefreshWaitReason = string.Empty;
			_lastDeferredHumanTerritoryRefreshScheduleReason = string.Empty;
			_loggedExternalManufactureBypass = false;
			_loggedExternalConsumerBypass = false;
			_trackingIllegalBackroomBusinessTick = false;
			_getPickContainerMethod = null;
			_pickContainerAddOrRefreshMethod = null;
			_illegalBackroomDirtyCashMoneyContextStack = null;
			VerificationLog("Compat", $"dirtycash-runtime-reset source={sourceTag}");
		}

		private static void TryPatchExternalDirtyCashConsumerHooks(Harmony harmony)
		{
			if (!ExternalDirtyCashEconomyDetected)
			{
				return;
			}

			try
			{
				Type type = AccessTools.TypeByName("DirtyCashEconomy.BackroomPatches");
				if (type == null)
				{
					return;
				}

				MethodInfo prefixMethod = AccessTools.Method(type, "DoConsumeAndPay_Prefix", new Type[]
				{
					typeof(ConsumerModule)
				});
				if (prefixMethod == null)
				{
					prefixMethod = AccessTools.Method(type, "DoConsumeAndPay_Prefix");
				}
				if (prefixMethod != null)
				{
					harmony.Patch(prefixMethod, prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(DirtyCashConsumerPrefixPrefix)));
				}

				MethodInfo postfixMethod = AccessTools.Method(type, "DoConsumeAndPay_Postfix", Type.EmptyTypes);
				if (postfixMethod == null)
				{
					postfixMethod = AccessTools.Method(type, "DoConsumeAndPay_Postfix");
				}
				if (postfixMethod != null)
				{
					harmony.Patch(postfixMethod, prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(DirtyCashConsumerPostfixPrefix)));
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to patch DirtyCashEconomy consumer hooks: " + ex.Message);
			}
		}

		private static void TryPatchExternalDirtyCashManufacturePrefix(Harmony harmony)
		{
			if (!ExternalDirtyCashEconomyDetected)
			{
				return;
			}

			try
			{
				Type type = AccessTools.TypeByName("DirtyCashEconomy.MultiThreadManufacturePatches");
				if (type == null)
				{
					return;
				}

				MethodInfo method = AccessTools.Method(type, "DoConsumeAndProduce_Prefix", new Type[]
				{
					typeof(ManufactureModule),
					typeof(ModuleQuery),
					typeof(InventoryModule),
					typeof(ModuleResult).MakeByRefType()
				});
				if (method == null)
				{
					method = AccessTools.Method(type, "DoConsumeAndProduce_Prefix");
				}
				if (method == null)
				{
					return;
				}

				harmony.Patch(method, prefix: new HarmonyMethod(typeof(DirtyCashEconomyCompatibilityPatch), nameof(DirtyCashManufacturePrefixPrefix)));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to patch DirtyCashEconomy manufacture prefix: " + ex.Message);
			}
		}

		private static bool DirtyCashConsumerPrefixPrefix(ConsumerModule __instance)
		{
			bool shouldBypass = ShouldBypassDirtyCashBackroomConsumer(__instance);
			ExternalConsumerBypassStack.Push(shouldBypass);
			if (!shouldBypass)
			{
				return true;
			}

			if (!_loggedExternalConsumerBypass)
			{
				_loggedExternalConsumerBypass = true;
				Debug.Log("[GameplayTweaks] DirtyCashEconomy consumer override bypassed for illegal backroom/legal business consumption; vanilla ConsumerModule flow restored.");
			}
			return false;
		}

		private static bool DirtyCashConsumerPostfixPrefix()
		{
			if (ExternalConsumerBypassStack.Count == 0)
			{
				return true;
			}

			return !ExternalConsumerBypassStack.Pop();
		}

		private static bool DirtyCashManufacturePrefixPrefix(ManufactureModule __instance, ModuleQuery q, InventoryModule inventory, ref bool __result)
		{
			if (!ShouldBypassDirtyCashBackroomManufacture(__instance, q, inventory))
			{
				return true;
			}

			__result = true;
			if (!_loggedExternalManufactureBypass)
			{
				_loggedExternalManufactureBypass = true;
				Debug.Log("[GameplayTweaks] DirtyCashEconomy manufacture override bypassed for human backroom/legal business operations; vanilla consume/produce logic restored.");
			}
			return false;
		}

		private static bool ShouldBypassDirtyCashBackroomManufacture(ManufactureModule module, ModuleQuery q, InventoryModule inventory)
		{
			if (!ExternalDirtyCashEconomyDetected || module == null || inventory == null)
			{
				return false;
			}
			if (!IsHumanOwnedDirtyCashRuntimeBypassModule(module.ModuleConfig, q.container, q.pid))
			{
				return false;
			}
			return true;
		}

		private static bool ShouldBypassDirtyCashBackroomConsumer(ConsumerModule module)
		{
			if (!ExternalDirtyCashEconomyDetected || module == null)
			{
				return false;
			}

			return IsDirtyCashRuntimeBypassModule(module.ModuleConfig);
		}

		private static void ConsumerDoConsumeAndPayPrefix(ConsumerModule __instance, ModuleQuery q)
		{
			EnsureExternalDirtyCashOriginalOverridesRemoved();
			PushIllegalBackroomDirtyCashMoneyContext(ShouldTrackIllegalBackroomDirtyCashMoney(__instance?.ModuleConfig, q.container, q.pid) ? q.container?.Id ?? EntityID.INVALID : EntityID.INVALID);
		}

		private static void ConsumerDoConsumeAndPayPostfix()
		{
			PopIllegalBackroomDirtyCashMoneyContext();
		}

		private static void ManufactureDoConsumeAndProduceInnerPrefix(ManufactureModule __instance, Recipe recipe, ModuleQuery q)
		{
			EnsureExternalDirtyCashOriginalOverridesRemoved();
			PushIllegalBackroomDirtyCashMoneyContext(ShouldTrackIllegalBackroomDirtyCashMoney(__instance?.ModuleConfig, q.container, q.pid) ? q.container?.Id ?? EntityID.INVALID : EntityID.INVALID);
		}

		private static void ManufactureDoConsumeAndProduceInnerPostfix()
		{
			PopIllegalBackroomDirtyCashMoneyContext();
		}

		private static bool PlayerFinancesDoChangeMoneyEntityPrefix(PlayerFinances __instance, Entity container, Price delta, MoneyReason reason, EntityID? target)
		{
			EnsureExternalDirtyCashOriginalOverridesRemoved();
			if (!ShouldConvertIllegalBackroomBusinessIncome(__instance, container, delta, reason))
			{
				return true;
			}

			if (!TryAddDirtyCashIncomeForIllegalBackroom(container, delta.cash, target ?? EntityID.INVALID))
			{
				return true;
			}

			__instance.RefreshPlayerFinances();
			return false;
		}

		private static void PlayerFinancesOnGlobalTurnSetAdvancedPostfix(PlayerFinances __instance)
		{
			if (__instance == null || global::Game.Game.ctx?.players?.Human?.finances != __instance)
			{
				return;
			}

			try
			{
				EnsureExternalDirtyCashOriginalOverridesRemoved();
				TryForceUpdateMissedIllegalBackroomBuildingsBeforeSystemTurn();
				TryReconcileHumanBackgroundTerritoryOwnership("global-turn");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Illegal backroom pre-system force failed: " + ex.Message);
			}
		}

		private static bool UpdateBusinessModulesPrefix()
		{
			EnsureExternalDirtyCashOriginalOverridesRemoved();
			_trackingIllegalBackroomBusinessTick = true;
			UpdatedIllegalBackroomBuildingsThisTick.Clear();
			return true;
		}

		private static void UpdateBusinessModulesPostfix(bool initial)
		{
			try
			{
				TryFallbackUpdateMissingIllegalBackroomBuildings(initial);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Illegal backroom business tick fallback failed: " + ex.Message);
			}
			finally
			{
				_trackingIllegalBackroomBusinessTick = false;
			}
		}

		private static Exception UpdateBusinessModulesFinalizer(Exception __exception)
		{
			_trackingIllegalBackroomBusinessTick = false;
			return __exception;
		}

		private static bool ModulesComponentDoUpdatePrefix(ModulesComponent __instance, SimTime time, bool initial)
		{
			EnsureExternalDirtyCashOriginalOverridesRemoved();
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
				ClearRecordedDirtyCashAOEContribution(building);
				ObservedIllegalBackroomBuildingUpdates.Add(updateKey);
			}

			if (!_trackingIllegalBackroomBusinessTick)
			{
				return true;
			}

			UpdatedIllegalBackroomBuildingsThisTick.Add(building.Id);
			return true;
		}

		private static void ModulesComponentDoUpdatePostfix(ModulesComponent __instance, SimTime time, bool initial)
		{
			Entity building = __instance?.entity;
			if (building == null || !IsHumanOwnedBuilding(building) || !HasInstalledDirtyCashBackroomModule(__instance))
			{
				return;
			}

			CaptureRecordedDirtyCashAOEContribution(building);
			if (_trackingIllegalBackroomBusinessTick)
			{
				return;
			}

			RefreshLateForcedDirtyCashAffectedNodes(building);
		}

		private static void BusinessUpdateTickPostfix(bool initial)
		{
			try
			{
				TryForceUpdateMissedIllegalBackroomBuildings(initial);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Illegal backroom tick force failed: " + ex.Message);
			}
		}

		private static void UpdateGamblingModulesPostfix(bool initial)
		{
			try
			{
				TryForceUpdateMissedIllegalBackroomBuildingsBeforeRespect(initial);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Illegal backroom pre-respect force failed: " + ex.Message);
			}
		}

		private static void RecalculateHeatAndRespectForNodesPrefix(bool initial)
		{
			try
			{
				TryForceUpdateMissedIllegalBackroomBuildingsBeforeRecalculate(initial);
				CaptureLateForcedDirtyCashRespectSnapshot();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to prepare illegal backroom respect recompute state: " + ex.Message);
			}
		}

		private static void RecalculateHeatAndRespectForNodesPostfix(bool initial)
		{
			try
			{
				TryReconcileHumanBackgroundTerritoryOwnership("post-recalculate");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Human background territory post-recalculate reconcile failed: " + ex.Message);
			}
		}

		private static void PlayerInfoOnPlayerTurnStartedPostfix(PlayerInfo __instance)
		{
			if (__instance == null || !__instance.IsHuman)
			{
				return;
			}

			try
			{
				TryForceUpdateMissedIllegalBackroomBuildingsOnHumanTurnStart();
				TryRefreshHumanOutpostTargetRespect("human-turn-start");
				TryReconcileHumanBackgroundTerritoryOwnership("human-turn-start");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Illegal backroom human turn-start force failed: " + ex.Message);
			}
		}

		private static void TryForceUpdateMissedIllegalBackroomBuildingsBeforeSystemTurn()
		{
			MethodInfo doUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new Type[]
			{
				typeof(SimTime),
				typeof(bool)
			});
			if (doUpdateMethod == null)
			{
				return;
			}

			SimTime now = global::Game.Game.ctx.clock.Now;
			bool initial = false;
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

				doUpdateMethod.Invoke(modules, new object[]
				{
					now,
					initial
				});
				PreSystemForcedIllegalBackroomBuildingUpdates.Add(updateKey);
				forceAppliedCount++;

				if (LoggedIllegalBackroomBusinessTickFallbacks.Add("presystem|" + updateKey))
				{
					Debug.Log("[GameplayTweaks] DirtyCash runtime pre-system force applied building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary(
				"pre-system-turn",
				now,
				initial,
				candidateCount,
				forceAppliedCount,
				observedCount,
				alreadyUpdatedCount: 0);
		}

		private static void TryFallbackUpdateMissingIllegalBackroomBuildings(bool initial)
		{
			MethodInfo doUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new Type[]
			{
				typeof(SimTime),
				typeof(bool)
			});
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

				doUpdateMethod.Invoke(modules, new object[]
				{
					now,
					initial
				});
				fallbackAppliedCount++;

				string logKey = buildingId + "|day=" + now.days + "|initial=" + initial;
				if (LoggedIllegalBackroomBusinessTickFallbacks.Add(logKey))
				{
					Debug.Log("[GameplayTweaks] DirtyCash runtime business tick fallback applied building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" initial=" +
						initial +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary(
				"update-business-modules",
				now,
				initial,
				candidateCount,
				fallbackAppliedCount,
				observedCount: 0,
				alreadyUpdatedCount);
		}

		private static void TryForceUpdateMissedIllegalBackroomBuildings(bool initial)
		{
			MethodInfo doUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new Type[]
			{
				typeof(SimTime),
				typeof(bool)
			});
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

				doUpdateMethod.Invoke(modules, new object[]
				{
					now,
					initial
				});
				if (HasInstalledDirtyCashBackroomModule(modules))
				{
					RefreshHeatAndRespectForLateForcedDirtyCashBackroom(building, initial, "business-tick");
				}
				forceAppliedCount++;

				if (LoggedIllegalBackroomBusinessTickFallbacks.Add("tickforce|" + updateKey))
				{
					Debug.Log("[GameplayTweaks] DirtyCash runtime tick force applied building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" initial=" +
						initial +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary(
				"business-tick",
				now,
				initial,
				candidateCount,
				forceAppliedCount,
				observedCount,
				alreadyUpdatedCount: 0);
		}

		private static void TryForceUpdateMissedIllegalBackroomBuildingsBeforeRespect(bool initial)
		{
			MethodInfo doUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new Type[]
			{
				typeof(SimTime),
				typeof(bool)
			});
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

				doUpdateMethod.Invoke(modules, new object[]
				{
					now,
					initial
				});
				forceAppliedCount++;

				if (LoggedIllegalBackroomBusinessTickFallbacks.Add("prerespect|" + updateKey))
				{
					Debug.Log("[GameplayTweaks] DirtyCash runtime pre-respect force applied building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" initial=" +
						initial +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary(
				"after-gambling-pre-respect",
				now,
				initial,
				candidateCount,
				forceAppliedCount,
				observedCount,
				alreadyUpdatedCount: 0);
		}

		private static void TryForceUpdateMissedIllegalBackroomBuildingsBeforeRecalculate(bool initial)
		{
			MethodInfo doUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new Type[]
			{
				typeof(SimTime),
				typeof(bool)
			});
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

				doUpdateMethod.Invoke(modules, new object[]
				{
					now,
					initial
				});
				forceAppliedCount++;

				if (LoggedIllegalBackroomBusinessTickFallbacks.Add("prerecalc|" + updateKey))
				{
					Debug.Log("[GameplayTweaks] DirtyCash runtime pre-recalculate force applied building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" initial=" +
						initial +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary(
				"pre-recalculate",
				now,
				initial,
				candidateCount,
				forceAppliedCount,
				observedCount,
				alreadyUpdatedCount: 0);
		}

		private static void TryForceUpdateMissedIllegalBackroomBuildingsOnHumanTurnStart()
		{
			MethodInfo doUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new Type[]
			{
				typeof(SimTime),
				typeof(bool)
			});
			if (doUpdateMethod == null)
			{
				return;
			}

			SimTime now = global::Game.Game.ctx.clock.Now;
			bool initial = false;
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

				doUpdateMethod.Invoke(modules, new object[]
				{
					now,
					initial
				});
				if (HasInstalledDirtyCashBackroomModule(modules))
				{
					RefreshHeatAndRespectForLateForcedDirtyCashBackroom(building, initial, "human-turn-start");
				}
				forceAppliedCount++;

				if (LoggedIllegalBackroomBusinessTickFallbacks.Add("humanturn|" + updateKey))
				{
					Debug.Log("[GameplayTweaks] DirtyCash runtime human turn-start force applied building=" +
						buildingId +
						" owner=" +
						GetControllingPlayerString(building) +
						" day=" +
						now.days);
				}
			}

			LogIllegalBackroomSafetySweepSummary(
				"human-turn-start",
				now,
				initial,
				candidateCount,
				forceAppliedCount,
				observedCount,
				alreadyUpdatedCount: 0);
		}

		private static void RefreshHeatAndRespectForLateForcedDirtyCashBackroom(Entity building, bool initial, string source)
		{
			int ownershipSwitchCount;
			int nodeCount = RebuildLateForcedDirtyCashRespectState(initial, out ownershipSwitchCount);
			if (nodeCount <= 0)
			{
				return;
			}

			string buildingLogKey = "resync|" + source + "|" + building.Id + "|day=" + (global::Game.Game.ctx?.clock?.Now.days ?? -1);
			if (LoggedIllegalBackroomBusinessTickFallbacks.Add(buildingLogKey))
			{
				Debug.Log("[GameplayTweaks] Illegal backroom late respect sync applied source=" +
					source +
					" building=" +
					building.Id +
					" nodes=" +
					nodeCount +
					" ownershipSwitches=" +
					ownershipSwitchCount);
			}
		}

		private static int RebuildLateForcedDirtyCashRespectState(bool initial, out int ownershipSwitchCount)
		{
			ownershipSwitchCount = 0;
			IEnumerable<Node> allNodes = global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe();
			if (allNodes == null)
			{
				return 0;
			}

			int nodeCount = 0;
			foreach (Node node in allNodes)
			{
				if (node?.respect?.data == null)
				{
					continue;
				}

				nodeCount++;
				for (int i = 0; i < node.respect.data.Count; i++)
				{
					node.respect.data[i].fromRelationships = 0;
					node.respect.data[i].fromNeighbors = 0;
					node.respect.data[i].fromEthnicity = 0;
					node.respect.data[i].fromSafehouse = 0;
				}
			}

			if (nodeCount == 0)
			{
				return 0;
			}

			bool restoredFromSnapshot = RestoreLateForcedDirtyCashRespectSnapshotForCurrentDay(allNodes);
			if (!restoredFromSnapshot)
			{
				MethodInfo updateRespectFromRelationships = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "UpdateRespectFromRelationships");
				MethodInfo updateRespectFromTerritory = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "UpdateRespectFromTerritory");
				MethodInfo updateRespectFromEthnicity = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "UpdateRespectFromEthnicity");
				MethodInfo updateRespectFromSafehouses = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "UpdateRespectFromSafehouses");
				if (updateRespectFromRelationships == null || updateRespectFromTerritory == null || updateRespectFromEthnicity == null || updateRespectFromSafehouses == null)
				{
					return nodeCount;
				}

				updateRespectFromRelationships.Invoke(null, null);
				updateRespectFromTerritory.Invoke(null, null);
				updateRespectFromEthnicity.Invoke(null, null);
				updateRespectFromSafehouses.Invoke(null, null);
			}

			foreach (Node node in allNodes)
			{
				if (node == null)
				{
					continue;
				}

				// This late correction runs after vanilla's respect pass, so force current/goal
				// to the rebuilt source state instead of letting velocity smoothing keep stale values.
				HeatAndRespect.RecomputeRespectForAllPlayers(node, true);
			}

			ownershipSwitchCount = ReconcileLateForcedDirtyCashNodeOwnership(allNodes);
			return nodeCount;
		}

		internal static bool TryRunGangCollapseTerritoryAudit(PlayerInfo owner, string source)
		{
			if (owner == null || owner.PID.IsNotValid || owner.PID.IsHumanPlayer || owner.IsJustCop)
			{
				return false;
			}

			List<OutpostEntry> outposts = owner.outposts?.GetOutpostEntriesUnsafe();
			int outpostCount = outposts?.Count ?? 0;
			if (outpostCount > 0)
			{
				LastGangCollapseTerritoryAuditDayByPid.Remove(owner.PID.id);
				return false;
			}

			List<Node> allNodes = BuildNodeList(global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe());
			if (allNodes.Count == 0)
			{
				return false;
			}

			int ownedNodeCount = 0;
			int respectedNodeCount = 0;
			foreach (Node node in allNodes)
			{
				if (node == null)
				{
					continue;
				}

				if (node.owner.Get() == owner.PID)
				{
					ownedNodeCount++;
				}

				if (node.respect?.data == null)
				{
					continue;
				}

				for (int i = 0; i < node.respect.data.Count; i++)
				{
					Respect respect = node.respect.data[i];
					if (respect == null || respect.pid != owner.PID)
					{
						continue;
					}

					if (respect.current > Fixnum.ZERO || GetDisplayedRespectValue(respect) > Fixnum.ZERO)
					{
						respectedNodeCount++;
						break;
					}
				}
			}

			if (ownedNodeCount <= 0 && respectedNodeCount <= 0)
			{
				LastGangCollapseTerritoryAuditDayByPid.Remove(owner.PID.id);
				return false;
			}

			int day = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;
			if (LastGangCollapseTerritoryAuditDayByPid.TryGetValue(owner.PID.id, out int lastAuditDay) && lastAuditDay == day)
			{
				return false;
			}

			LastGangCollapseTerritoryAuditDayByPid[owner.PID.id] = day;
			int livingCrewCount = owner.crew?.AllCrew.Count(assignment => assignment.IsValid && assignment.IsNotDead && owner.crew.IsOnBoard(assignment.peepId)) ?? 0;
			int ownershipSwitchCount;
			int rebuiltNodeCount = RebuildLateForcedDirtyCashRespectState(initial: false, out ownershipSwitchCount);
			if (rebuiltNodeCount <= 0)
			{
				return false;
			}

			ForceRefreshHumanTerritoryVisuals();
			TerritoryColorPatch.RefreshAllTerritoryColors();
			VerificationLog(
				"Compat",
				$"gang-collapse-territory-audit pid={owner.PID.id} source={source} outposts={outpostCount} livingCrew={livingCrewCount} ownedNodes={ownedNodeCount} respectedNodes={respectedNodeCount} nodes={rebuiltNodeCount} ownershipSwitches={ownershipSwitchCount}");
			return true;
		}

		internal static void NotifyGangMemberDeathForTerritoryAudit(PlayerInfo owner, string source)
		{
			if (owner == null || owner.PID.IsNotValid || owner.PID.IsHumanPlayer || owner.IsJustCop)
			{
				return;
			}

			int outpostCount = owner.outposts?.GetOutpostEntriesUnsafe()?.Count ?? 0;
			bool crewDefeated = owner.crew?.IsCrewDefeated ?? false;
			if (!crewDefeated || outpostCount > 0)
			{
				return;
			}

			int ownedNodeCount = owner.territory?.OwnedNodeCount ?? 0;
			int controlledBuildingCount = 0;
			try
			{
				controlledBuildingCount = owner.territory?.CountControlledBuildings() ?? 0;
			}
			catch
			{
				controlledBuildingCount = 0;
			}
			if (ownedNodeCount <= 0 && controlledBuildingCount <= 0)
			{
				return;
			}

			int day = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;
			if (LastGangCollapseTerritoryDeferredDayByPid.TryGetValue(owner.PID.id, out int lastDeferredDay) && lastDeferredDay == day)
			{
				_gangCollapseTerritoryDeferredSuppressedCount++;
				return;
			}

			LastGangCollapseTerritoryDeferredDayByPid[owner.PID.id] = day;
			PendingGangCollapseTerritoryRebuildPids.Add(owner.PID.id);
			if (_pendingDeferredHumanTerritoryFullRebuild)
			{
				_gangCollapseTerritoryDeferredSuppressedCount++;
				return;
			}

			RequestDeferredHumanTerritoryFullRebuild(string.IsNullOrEmpty(source) ? "gang-death" : (source + "-gang-death"), delayFrames: 12, passes: 1);
			VerificationLog("Compat", $"gang-collapse-territory-audit-deferred pid={owner.PID.id} source={source} outposts={outpostCount} ownedNodes={ownedNodeCount} buildings={controlledBuildingCount} crewDefeated={crewDefeated} mode=full-rebuild pendingPids={PendingGangCollapseTerritoryRebuildPids.Count} frame={Time.frameCount}");
		}

		private static int ReconcileLateForcedDirtyCashNodeOwnership(IEnumerable<Node> allNodes)
		{
			return ReconcileHumanTerritoryOwnershipUntilStable(
				allNodes,
				"dirtycash-late",
				useDirtyCashPreviousCurrent: true,
				refreshHumanTerritoryStateWhenNoChanges: true);
		}

		private static void TryReconcileHumanBackgroundTerritoryOwnership(string source)
		{
			if (!ShouldEnableHumanBackgroundTerritoryReconcile())
			{
				return;
			}

			IEnumerable<Node> allNodes = global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe();
			if (allNodes == null)
			{
				return;
			}

			ReconcileHumanTerritoryOwnershipUntilStable(
				allNodes,
				source,
				useDirtyCashPreviousCurrent: false,
				refreshHumanTerritoryStateWhenNoChanges: false);
		}

		private static void TryRefreshHumanOutpostTargetRespect(string source)
		{
			PlayerInfo humanPlayer = global::Game.Game.ctx?.players?.Human;
			PlayerTerritory territory = humanPlayer?.territory;
			List<OutpostEntry> outposts = humanPlayer?.outposts?.GetOutpostEntriesUnsafe();
			if (territory == null || outposts == null || outposts.Count == 0)
			{
				return;
			}

			HashSet<int> refreshedNodeIds = new HashSet<int>();
			int refreshedCount = 0;
			for (int outpostIndex = 0; outpostIndex < outposts.Count; outpostIndex++)
			{
				OutpostEntry outpost = outposts[outpostIndex];
				List<NodeEntry> targetNodes = outpost?.targetNodes;
				if (targetNodes == null)
				{
					continue;
				}

				for (int nodeIndex = 0; nodeIndex < targetNodes.Count; nodeIndex++)
				{
					Node node = targetNodes[nodeIndex]?.nodeId.FindNode();
					if (node == null || !refreshedNodeIds.Add(node.id.index))
					{
						continue;
					}

					territory.RecomputeRespect(node, forceCurrent: true);
					refreshedCount++;
				}
			}

			if (refreshedCount > 0)
			{
				Debug.Log("[GameplayTweaks] Human outpost target respect refreshed source=" + source + " nodes=" + refreshedCount);
			}
		}

		private static void TryRefreshAllPlayerOutpostTargetRespect(string source)
		{
			IEnumerable<PlayerInfo> allPlayers = G.GetAllPlayers();
			if (allPlayers == null)
			{
				return;
			}

			HashSet<int> refreshedNodeIds = new HashSet<int>();
			int refreshedCount = 0;
			int refreshedPlayerCount = 0;
			foreach (PlayerInfo player in allPlayers)
			{
				List<OutpostEntry> outposts = player?.outposts?.GetOutpostEntriesUnsafe();
				TrackObservedOutpostCountAndAudit(player, outposts?.Count ?? 0, source);
				if (outposts == null || outposts.Count == 0)
				{
					continue;
				}

				refreshedPlayerCount++;
				for (int outpostIndex = 0; outpostIndex < outposts.Count; outpostIndex++)
				{
					OutpostEntry outpost = outposts[outpostIndex];
					List<NodeEntry> targetNodes = outpost?.targetNodes;
					if (targetNodes == null)
					{
						continue;
					}

					for (int nodeIndex = 0; nodeIndex < targetNodes.Count; nodeIndex++)
					{
						Node node = targetNodes[nodeIndex]?.nodeId.FindNode();
						if (node == null || !refreshedNodeIds.Add(node.id.index))
						{
							continue;
						}

						HeatAndRespect.RecomputeRespectForAllPlayers(node, true);
						refreshedCount++;
					}
				}
			}

			if (refreshedCount > 0)
			{
				Debug.Log("[GameplayTweaks] All player outpost target respect refreshed source=" + source + " nodes=" + refreshedCount + " players=" + refreshedPlayerCount);
			}
		}

		private static void TrackObservedOutpostCountAndAudit(PlayerInfo player, int currentOutpostCount, string source)
		{
			if (player == null || player.PID.IsNotValid || player.PID.IsHumanPlayer || player.IsJustCop)
			{
				return;
			}

			int pid = player.PID.id;
			if (LastObservedOutpostCountByPid.TryGetValue(pid, out int previousOutpostCount) && currentOutpostCount < previousOutpostCount)
			{
				VerificationLog("Compat", $"outpost-count-drop pid={pid} previous={previousOutpostCount} current={currentOutpostCount} source={source}");
				RequestDeferredHumanTerritoryFullRebuild(source + "-outpost-drop", delayFrames: 12, passes: 1);
			}

			if (currentOutpostCount > 0 || IsAliveGangPlayer(player))
			{
				LastObservedOutpostCountByPid[pid] = currentOutpostCount;
			}
			else
			{
				LastObservedOutpostCountByPid.Remove(pid);
			}
		}

		internal static int TryRunGangOpsAutoProtectPass(
			GangOpsChannel channel,
			PlayerInfo player,
			int range,
			string source,
			out int outpostCount,
			out int protectedNodeCount,
			out int supportResetCount)
		{
			outpostCount = 0;
			protectedNodeCount = 0;
			supportResetCount = 0;
			if (!IsGangEligibleForChannel(player, channel))
			{
				return 0;
			}

			global::Game.Services.RespectSettings respectSettings = global::Game.Game.serv?.globals?.settings?.people?.social?.respect;
			List<OutpostEntry> outposts = player?.outposts?.GetOutpostEntriesUnsafe();
			if (respectSettings == null || outposts == null || outposts.Count == 0)
			{
				return 0;
			}

			outpostCount = outposts.Count;
			HashSet<int> seenNodeIds = new HashSet<int>();
			List<Node> targetNodes = new List<Node>();
			int clampedRange = Mathf.Clamp(range, 1, 6);
			for (int outpostIndex = 0; outpostIndex < outposts.Count; outpostIndex++)
			{
				OutpostEntry outpost = outposts[outpostIndex];
				if (outpost == null)
				{
					continue;
				}

				MoneyStatus money = outpost.money;
				if (money != null && (money.NeedsSupport || (money.months >= 2 && money.Delta <= Fixnum.ZERO)))
				{
					money.Reset();
					supportResetCount++;
				}

				List<NodeEntry> targetNodeEntries = outpost.targetNodes;
				if (targetNodeEntries == null)
				{
					continue;
				}

				for (int nodeIndex = 0; nodeIndex < targetNodeEntries.Count; nodeIndex++)
				{
					Node node = targetNodeEntries[nodeIndex]?.nodeId.FindNode();
					AddGangOpsAutoProtectNode(node, seenNodeIds, targetNodes);
				}

				Node outpostNode = targetNodeEntries.Count > 0 ? targetNodeEntries[0]?.nodeId.FindNode() : null;
				AddGangOpsAutoProtectNodeRange(outpostNode, clampedRange, seenNodeIds, targetNodes);
			}

			protectedNodeCount = targetNodes.Count;
			if (protectedNodeCount == 0)
			{
				return 0;
			}

			TryRepairStackedGangOpsTerritoryRespect(targetNodes, source);

			for (int i = 0; i < targetNodes.Count; i++)
			{
				HeatAndRespect.RecomputeRespectForAllPlayers(targetNodes[i], false);
			}

			HashSet<PlayerID> affectedPlayers = new HashSet<PlayerID>();
			int claimedNeutralNodes = ClaimNeutralNodesFromCurrentRespect(targetNodes, respectSettings, affectedPlayers, allowRelaxedUnownedClaim: true);
			int ownershipSwitches = ReconcileTerritoryOwnershipFromCurrentRespectPass(targetNodes, respectSettings, affectedPlayers, allowRelaxedUnownedClaim: true);
			int totalSwitches = claimedNeutralNodes + ownershipSwitches;
			if (affectedPlayers.Count > 0)
			{
				RefreshLateForcedDirtyCashTerritoryState(affectedPlayers);
			}

			if (totalSwitches > 0 || supportResetCount > 0)
			{
				VerificationLog("Compat", $"gangops-auto-protect source={source} channel={GetGangOpsChannelTag(channel)} pid={player.PID.id} outposts={outpostCount} nodes={protectedNodeCount} neutralClaims={claimedNeutralNodes} ownershipSwitches={ownershipSwitches} supportResets={supportResetCount} range={clampedRange}");
			}
			return totalSwitches;
		}

		private static void AddGangOpsAutoProtectNode(Node node, HashSet<int> seenNodeIds, List<Node> targetNodes)
		{
			if (node == null || seenNodeIds == null || targetNodes == null || !seenNodeIds.Add(node.id.index))
			{
				return;
			}

			targetNodes.Add(node);
		}

		private static void AddGangOpsAutoProtectNodeRange(Node source, int range, HashSet<int> seenNodeIds, List<Node> targetNodes)
		{
			if (source == null || range <= 1)
			{
				return;
			}

			Queue<Node> queue = new Queue<Node>();
			Dictionary<int, int> distanceByNode = new Dictionary<int, int>();
			queue.Enqueue(source);
			distanceByNode[source.id.index] = 0;
			while (queue.Count > 0)
			{
				Node current = queue.Dequeue();
				int distance = distanceByNode[current.id.index];
				if (distance >= range)
				{
					continue;
				}

				IEnumerable<Node> neighbors = global::Game.Game.ctx?.board?.nodes?.FindNeighborNodes(current, IsGangOpsAutoProtectNodeCandidate, IsGangOpsAutoProtectRoadEdge);
				if (neighbors == null)
				{
					continue;
				}

				foreach (Node neighbor in neighbors)
				{
					if (neighbor == null || distanceByNode.ContainsKey(neighbor.id.index))
					{
						continue;
					}

					distanceByNode[neighbor.id.index] = distance + 1;
					AddGangOpsAutoProtectNode(neighbor, seenNodeIds, targetNodes);
					queue.Enqueue(neighbor);
				}
			}
		}

		private static bool IsGangOpsAutoProtectNodeCandidate(Node node)
		{
			return node != null && ((node.contained != null && node.contained.Count > 0) || node.IsConnectionNode);
		}

		private static bool IsGangOpsAutoProtectRoadEdge(NodeEdge edge)
		{
			return edge != null && edge.IsRoad;
		}

		private static void TryRepairStackedGangOpsTerritoryRespect(IEnumerable<Node> sampleNodes, string source)
		{
			if (_gangOpsStackedTerritoryRespectRepairAttempted || sampleNodes == null)
			{
				return;
			}

			int suspectNodeCount = 0;
			Fixnum repairThreshold = GangOpsStackedRespectRepairThreshold;
			foreach (Node node in sampleNodes)
			{
				if (node?.respect?.data == null)
				{
					continue;
				}

				for (int i = 0; i < node.respect.data.Count; i++)
				{
					Respect respect = node.respect.data[i];
					if (respect == null)
					{
						continue;
					}

					if (respect.fromNeighbors > repairThreshold || respect.current > repairThreshold || respect.goal > repairThreshold)
					{
						suspectNodeCount++;
						break;
					}
				}
			}

			if (suspectNodeCount <= 0)
			{
				return;
			}

			_gangOpsStackedTerritoryRespectRepairAttempted = true;
			List<Node> refreshedNodes = RefreshTerritoryDerivedRespectAfterOwnershipChange();
			VerificationLog("Compat", $"gangops-respect-repair source={source} suspectNodes={suspectNodeCount} refreshedNodes={refreshedNodes.Count} threshold={GangOpsStackedRespectRepairThreshold}");
		}

		internal static int TryRunAiOutpostTerritoryExpansionPass(GangOpsChannel channel, string source, IEnumerable<int> eligibleGangIds = null)
		{
			if (!ShouldAllowGangTerritoryExpansion())
			{
				return 0;
			}

			global::Game.Services.RespectSettings respectSettings = global::Game.Game.serv?.globals?.settings?.people?.social?.respect;
			if (respectSettings == null)
			{
				return 0;
			}

			HashSet<int> seenNodeIds = new HashSet<int>();
			List<Node> targetNodes = new List<Node>();
			int outpostCount = 0;
			int playerCount = 0;
			foreach (int gangId in eligibleGangIds ?? GetEligibleGangIdsForChannel(channel))
			{
				PlayerInfo player = G.FindPlayerById(gangId);
				if (!IsGangEligibleForChannel(player, channel))
				{
					continue;
				}

				List<OutpostEntry> outposts = player?.outposts?.GetOutpostEntriesUnsafe();
				if (outposts == null || outposts.Count == 0)
				{
					continue;
				}

				playerCount++;
				outpostCount += outposts.Count;
				for (int outpostIndex = 0; outpostIndex < outposts.Count; outpostIndex++)
				{
					List<NodeEntry> nodeEntries = outposts[outpostIndex]?.targetNodes;
					if (nodeEntries == null)
					{
						continue;
					}

					for (int nodeIndex = 0; nodeIndex < nodeEntries.Count; nodeIndex++)
					{
						Node node = nodeEntries[nodeIndex]?.nodeId.FindNode();
						if (node == null || !seenNodeIds.Add(node.id.index))
						{
							continue;
						}

						targetNodes.Add(node);
					}
				}
			}

			if (targetNodes.Count == 0)
			{
				return 0;
			}

			TryRepairStackedGangOpsTerritoryRespect(targetNodes, source);

			for (int i = 0; i < targetNodes.Count; i++)
			{
				HeatAndRespect.RecomputeRespectForAllPlayers(targetNodes[i], false);
			}

			HashSet<PlayerID> affectedPlayers = new HashSet<PlayerID>();
			int aggressionPercent = GetGangOpsTerritoryTakeoverAggressionPercent(channel);
			bool relaxedOwnershipSwitches = aggressionPercent >= 150;
			int claimedNeutralNodes = ClaimNeutralNodesFromCurrentRespect(targetNodes, respectSettings, affectedPlayers, allowRelaxedUnownedClaim: true);
			int ownershipSwitches = ReconcileTerritoryOwnershipFromCurrentRespectPass(targetNodes, respectSettings, affectedPlayers, allowRelaxedUnownedClaim: relaxedOwnershipSwitches);
			int extraSettlePasses = aggressionPercent >= 250 ? 2 : (aggressionPercent >= 200 ? 1 : 0);
			for (int settlePass = 0; settlePass < extraSettlePasses; settlePass++)
			{
				if (settlePass > 0 && ownershipSwitches <= 0)
				{
					break;
				}
				for (int i = 0; i < targetNodes.Count; i++)
				{
					HeatAndRespect.RecomputeRespectForAllPlayers(targetNodes[i], false);
				}
				int settleSwitches = ReconcileTerritoryOwnershipFromCurrentRespectPass(targetNodes, respectSettings, affectedPlayers, allowRelaxedUnownedClaim: true);
				ownershipSwitches += settleSwitches;
				if (settleSwitches <= 0)
				{
					break;
				}
			}
			int totalSwitches = claimedNeutralNodes + ownershipSwitches;
			if (affectedPlayers.Count > 0)
			{
				RefreshLateForcedDirtyCashTerritoryState(affectedPlayers);
			}

			VerificationLog("Compat", $"ai-outpost-territory-pass source={source} channel={GetGangOpsChannelTag(channel)} players={playerCount} outposts={outpostCount} nodes={targetNodes.Count} neutralClaims={claimedNeutralNodes} ownershipSwitches={ownershipSwitches} relaxedNeutralClaims=True relaxedSwitches={relaxedOwnershipSwitches} territoryAggro={aggressionPercent}");
			return totalSwitches;
		}

		private static int ReconcileHumanTerritoryOwnershipUntilStable(
			IEnumerable<Node> nodes,
			string source,
			bool useDirtyCashPreviousCurrent,
			bool refreshHumanTerritoryStateWhenNoChanges)
		{
			List<Node> nodeList = BuildNodeList(nodes);
			if (nodeList.Count == 0)
			{
				return 0;
			}

			PlayerInfo humanPlayer = global::Game.Game.ctx?.players?.Human;
			PlayerID humanPid = humanPlayer?.PID ?? PlayerID.INVALID;
			if (humanPid.IsNotValid)
			{
				return 0;
			}

			int totalSwitchCount = 0;
			int passes = 0;
			HashSet<PlayerID> affectedPlayers = new HashSet<PlayerID>();
			for (int pass = 0; pass < MaxHumanBackgroundTerritoryReconcilePasses; pass++)
			{
				passes = pass + 1;
				int passSwitchCount = ReconcileHumanTerritoryOwnershipPass(
					nodeList,
					useDirtyCashPreviousCurrent && pass == 0,
					affectedPlayers);
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

			if (affectedPlayers.Count == 0)
			{
				if (!refreshHumanTerritoryStateWhenNoChanges)
				{
					return totalSwitchCount;
				}

				affectedPlayers.Add(humanPid);
			}

			RefreshLateForcedDirtyCashTerritoryState(affectedPlayers);
			if (totalSwitchCount > 0)
			{
				Debug.Log("[GameplayTweaks] Human background territory reconcile applied source=" +
					source +
					" switches=" +
					totalSwitchCount +
					" passes=" +
					passes +
					" nearbyBizPct=" +
					GetHumanBackgroundTerritoryBusinessRespectPercent());
			}

			return totalSwitchCount;
		}

		private static int ReconcileHumanTerritoryOwnership(IEnumerable<Node> nodes, bool useDirtyCashPreviousCurrent, bool refreshHumanTerritoryStateWhenNoChanges)
		{
			if (nodes == null)
			{
				return 0;
			}

			PlayerInfo humanPlayer = global::Game.Game.ctx?.players?.Human;
			PlayerID humanPid = humanPlayer?.PID ?? PlayerID.INVALID;
			if (humanPid.IsNotValid)
			{
				return 0;
			}

			HashSet<PlayerID> affectedPlayers = new HashSet<PlayerID>();
			int switchCount = ReconcileHumanTerritoryOwnershipPass(nodes, useDirtyCashPreviousCurrent, affectedPlayers);
			if (affectedPlayers.Count == 0)
			{
				if (!refreshHumanTerritoryStateWhenNoChanges)
				{
					return switchCount;
				}

				affectedPlayers.Add(humanPid);
			}

			RefreshLateForcedDirtyCashTerritoryState(affectedPlayers);
			return switchCount;
		}

		private static int ReconcileHumanTerritoryOwnershipPass(
			IEnumerable<Node> nodes,
			bool useDirtyCashPreviousCurrent,
			HashSet<PlayerID> affectedPlayers)
		{
			if (nodes == null || affectedPlayers == null)
			{
				return 0;
			}

			PlayerInfo humanPlayer = global::Game.Game.ctx?.players?.Human;
			PlayerID humanPid = humanPlayer?.PID ?? PlayerID.INVALID;
			if (humanPid.IsNotValid)
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
				Fixnum claimStrength = GetHumanTerritoryClaimStrength(humanRespect);
				Fixnum previousCurrent = useDirtyCashPreviousCurrent ? FindLateForcedDirtyCashPreviousCurrent(node.id, humanPid) : humanRespect.current;
				Fixnum lossThreshold = settings.lossThreshold.Evaluate(humanPid);
				if (previousCurrent > lossThreshold && claimStrength <= lossThreshold && nodeOwner == humanPid)
				{
					PlayerTerritory.ClearNodeOwner(node, humanPid, PlayerID.INVALID);
					affectedPlayers.Add(humanPid);
					switchCount++;
					continue;
				}

				if (nodeOwner == humanPid)
				{
					continue;
				}

				ModQuery humanNodeQuery = new ModQuery(humanPid, node);
				Fixnum gainThreshold = settings.gainThreshold.Evaluate(humanNodeQuery);
				bool shouldClaimUnowned = nodeOwner.IsNotValid && claimStrength >= gainThreshold;
				bool shouldTakeOwned = nodeOwner.IsValid &&
					claimStrength >= gainThreshold &&
					claimStrength >= GetHighestCompetingCurrentRespect(node, humanPid);
				if (!shouldClaimUnowned && !shouldTakeOwned)
				{
					continue;
				}

				if (nodeOwner.IsValid)
				{
					PlayerTerritory.ClearNodeOwner(node, nodeOwner, humanPid);
					affectedPlayers.Add(nodeOwner);
				}

				PlayerTerritory.SetNodeOwner(node, humanPid);
				affectedPlayers.Add(humanPid);
				switchCount++;
			}

			return switchCount;
		}

		private static int ReconcileTerritoryOwnershipFromCurrentRespectUntilStable(
			IEnumerable<Node> nodes,
			string source,
			bool refreshVisualsWhenNoChanges,
			bool allowRelaxedUnownedClaim = false)
		{
			List<Node> nodeList = BuildNodeList(nodes);
			if (nodeList.Count == 0)
			{
				return 0;
			}

			global::Game.Services.RespectSettings settings = global::Game.Game.serv?.globals?.settings?.people?.social?.respect;
			if (settings == null)
			{
				return 0;
			}

			int totalSwitchCount = 0;
			int passes = 0;
			int lastPassSwitchCount = 0;
			HashSet<PlayerID> affectedPlayers = new HashSet<PlayerID>();
			for (int pass = 0; pass < MaxHumanBackgroundTerritoryReconcilePasses; pass++)
			{
				passes = pass + 1;
				int passSwitchCount = ReconcileTerritoryOwnershipFromCurrentRespectPass(nodeList, settings, affectedPlayers, allowRelaxedUnownedClaim);
				lastPassSwitchCount = passSwitchCount;
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

			if (lastPassSwitchCount > 0 && nodeList.Count > 0)
			{
				int finalSettleSwitchCount = ReconcileTerritoryOwnershipFromCurrentRespectPass(nodeList, settings, affectedPlayers, allowRelaxedUnownedClaim);
				if (finalSettleSwitchCount > 0)
				{
					totalSwitchCount += finalSettleSwitchCount;
					passes++;
					VerificationLog("Compat", $"territory-final-settle source={source} switches={finalSettleSwitchCount} passes={passes}");
				}
			}

			if (affectedPlayers.Count > 0)
			{
				RefreshLateForcedDirtyCashTerritoryState(affectedPlayers);
			}
			else if (refreshVisualsWhenNoChanges)
			{
				ForceRefreshHumanTerritoryVisuals();
			}

			if (totalSwitchCount > 0)
			{
				Debug.Log("[GameplayTweaks] Territory ownership seeded from current respect source=" +
					source +
					" switches=" +
					totalSwitchCount +
					" passes=" +
					passes);
			}

			return totalSwitchCount;
		}

		private static int ReconcileTerritoryOwnershipFromCurrentRespectPass(
			IEnumerable<Node> nodes,
			global::Game.Services.RespectSettings settings,
			HashSet<PlayerID> affectedPlayers,
			bool allowRelaxedUnownedClaim)
		{
			if (nodes == null || settings == null || affectedPlayers == null)
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

				PlayerID currentOwner = node.owner.Get();
				PlayerID desiredOwner = DetermineDesiredNodeOwnerFromCurrentRespect(node, settings, currentOwner, allowRelaxedUnownedClaim);
				if (desiredOwner == currentOwner)
				{
					continue;
				}

				if (currentOwner.IsValid)
				{
					PlayerTerritory.ClearNodeOwner(node, currentOwner, desiredOwner);
					affectedPlayers.Add(currentOwner);
				}

				if (desiredOwner.IsValid)
				{
					PlayerTerritory.SetNodeOwner(node, desiredOwner);
					affectedPlayers.Add(desiredOwner);
				}

				switchCount++;
			}

			return switchCount;
		}

		private static int ClaimNeutralNodesFromCurrentRespect(
			IEnumerable<Node> nodes,
			global::Game.Services.RespectSettings settings,
			HashSet<PlayerID> affectedPlayers,
			bool allowRelaxedUnownedClaim = false)
		{
			if (nodes == null || settings == null || affectedPlayers == null)
			{
				return 0;
			}

			int switchCount = 0;
			foreach (Node node in nodes)
			{
				if (node?.respect?.data == null || node.respect.data.Count == 0 || node.owner.Get().IsValid)
				{
					continue;
				}

				PlayerID desiredOwner = TryDetermineProtectedTerritorySupportOwner(node, out PlayerID supportedOwner)
					? supportedOwner
					: DetermineDesiredNodeOwnerFromCurrentRespect(node, settings, PlayerID.INVALID, allowRelaxedUnownedClaim);
				if (desiredOwner.IsNotValid)
				{
					continue;
				}

				PlayerTerritory.SetNodeOwner(node, desiredOwner);
				affectedPlayers.Add(desiredOwner);
				switchCount++;
			}

			return switchCount;
		}

		private static PlayerID DetermineDesiredNodeOwnerFromCurrentRespect(
			Node node,
			global::Game.Services.RespectSettings settings,
			PlayerID currentOwner,
			bool allowRelaxedUnownedClaim)
		{
			if (node?.respect?.data == null || node.respect.data.Count == 0 || settings == null)
			{
				return PlayerID.INVALID;
			}

			PlayerID highestPid = PlayerID.INVALID;
			Fixnum highestCurrent = Fixnum.MIN_VALUE;
			Fixnum ownerCurrent = Fixnum.ZERO;
			bool ownerCurrentFound = false;
			for (int i = 0; i < node.respect.data.Count; i++)
			{
				Respect respect = node.respect.data[i];
				if (respect == null)
				{
					continue;
				}

				if (respect.current > highestCurrent)
				{
					highestCurrent = respect.current;
					highestPid = respect.pid;
				}

				if (respect.pid == currentOwner)
				{
					ownerCurrent = respect.current;
					ownerCurrentFound = true;
				}
			}

			if (highestPid.IsNotValid)
			{
				return PlayerID.INVALID;
			}
			if (TryDetermineProtectedTerritorySupportOwner(node, out PlayerID supportedOwner))
			{
				return supportedOwner;
			}

			Fixnum highestGainThreshold = settings.gainThreshold.Evaluate(new ModQuery(highestPid, node));
			bool highestCanOwn = highestCurrent >= highestGainThreshold;
			if (!currentOwner.IsValid)
			{
				if (allowRelaxedUnownedClaim && !highestCanOwn && TryDetermineRelaxedDisplayedRespectOwner(node, out PlayerID relaxedOwner))
				{
					return relaxedOwner;
				}

				return highestCanOwn ? highestPid : PlayerID.INVALID;
			}

			if (!ownerCurrentFound)
			{
				return highestCanOwn ? highestPid : PlayerID.INVALID;
			}

			Fixnum ownerLossThreshold = settings.lossThreshold.Evaluate(currentOwner);
			bool ownerStillQualifies = ownerCurrent > ownerLossThreshold;
			PlayerInfo currentOwnerPlayer = currentOwner.FindPlayer();
			if (ownerStillQualifies && currentOwnerPlayer != null && currentOwnerPlayer.IsJustGang)
			{
				int currentOwnerOutpostCount = currentOwnerPlayer.outposts?.GetOutpostEntriesUnsafe()?.Count ?? 0;
				bool currentOwnerCollapsed = currentOwnerOutpostCount <= 0
					|| currentOwnerPlayer.crew == null
					|| currentOwnerPlayer.crew.IsCrewDefeated
					|| currentOwnerPlayer.crew.LivingCrewCount <= 0;
				if (currentOwnerCollapsed)
				{
					Fixnum ownerGainThreshold = settings.gainThreshold.Evaluate(new ModQuery(currentOwner, node));
					ownerStillQualifies = ownerCurrent >= ownerGainThreshold;
				}
			}
			if (!ownerStillQualifies)
			{
				return highestCanOwn ? highestPid : PlayerID.INVALID;
			}

			if (highestPid == currentOwner)
			{
				return currentOwner;
			}

			if (!highestCanOwn)
			{
				return currentOwner;
			}

			return (highestCurrent >= ownerCurrent) ? highestPid : currentOwner;
		}

		private static bool TryDetermineRelaxedDisplayedRespectOwner(Node node, out PlayerID relaxedOwner)
		{
			relaxedOwner = PlayerID.INVALID;
			if (node?.respect?.data == null || node.respect.data.Count == 0)
			{
				return false;
			}

			List<PlayerID> tiedHighestPids = new List<PlayerID>();
			Fixnum highestDisplayedRespect = Fixnum.ZERO;
			for (int i = 0; i < node.respect.data.Count; i++)
			{
				Respect respect = node.respect.data[i];
				if (respect == null || respect.pid.IsNotValid)
				{
					continue;
				}

				Fixnum displayedRespect = GetDisplayedRespectValue(respect);
				if (displayedRespect <= Fixnum.ZERO)
				{
					continue;
				}

				if (tiedHighestPids.Count == 0 || displayedRespect > highestDisplayedRespect)
				{
					highestDisplayedRespect = displayedRespect;
					tiedHighestPids.Clear();
					tiedHighestPids.Add(respect.pid);
				}
				else if (displayedRespect == highestDisplayedRespect && !tiedHighestPids.Contains(respect.pid))
				{
					tiedHighestPids.Add(respect.pid);
				}
			}

			if (tiedHighestPids.Count == 0)
			{
				return false;
			}

			if (tiedHighestPids.Count == 1)
			{
				relaxedOwner = tiedHighestPids[0];
				return true;
			}

			PlayerID lastOwner = node.owner?.lastpid ?? PlayerID.INVALID;
			if (lastOwner.IsValid && tiedHighestPids.Contains(lastOwner))
			{
				relaxedOwner = lastOwner;
				return true;
			}

			PlayerID humanPid = global::Game.Game.ctx?.players?.Human?.PID ?? PlayerID.INVALID;
			if (humanPid.IsValid && tiedHighestPids.Contains(humanPid))
			{
				relaxedOwner = humanPid;
				return true;
			}

			PlayerID chosenPid = tiedHighestPids[0];
			for (int i = 1; i < tiedHighestPids.Count; i++)
			{
				if (tiedHighestPids[i].id < chosenPid.id)
				{
					chosenPid = tiedHighestPids[i];
				}
			}

			relaxedOwner = chosenPid;
			return true;
		}

		private static Fixnum GetDisplayedRespectValue(Respect respect)
		{
			if (respect == null)
			{
				return Fixnum.ZERO;
			}

			return respect.goal > respect.current ? respect.goal : respect.current;
		}

		private static void LogTerritoryOwnershipAudit(string sourceTag, int maxEntries = 12)
		{
			IEnumerable<Node> allNodes = global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe();
			if (allNodes == null)
			{
				return;
			}

			int neutralMismatchCount = 0;
			int conflictingOwnerCount = 0;
			int logged = 0;
			foreach (Node node in allNodes)
			{
				if (node?.respect?.data == null || node.respect.data.Count == 0)
				{
					continue;
				}

				PlayerID currentOwner = node.owner.Get();
				if (!TryDetermineDominantDisplayedRespectOwner(node, out PlayerID dominantPid, out Fixnum dominantRespect, out Fixnum secondHighestRespect))
				{
					continue;
				}

				if (!currentOwner.IsValid)
				{
					neutralMismatchCount++;
					if (logged < maxEntries)
					{
						logged++;
						VerificationLog("Compat", $"territory-audit source={sourceTag} state=neutral node={node.id} dominantPid={dominantPid.id} dominant={dominantRespect} second={secondHighestRespect}");
					}
					continue;
				}

				if (currentOwner != dominantPid)
				{
					conflictingOwnerCount++;
					if (logged < maxEntries)
					{
						logged++;
						VerificationLog("Compat", $"territory-audit source={sourceTag} state=conflict node={node.id} ownerPid={currentOwner.id} dominantPid={dominantPid.id} dominant={dominantRespect} second={secondHighestRespect}");
					}
				}
			}

			VerificationLog("Compat", $"territory-audit-summary source={sourceTag} neutral={neutralMismatchCount} conflicts={conflictingOwnerCount}");
		}

		private static bool TryDetermineDominantDisplayedRespectOwner(Node node, out PlayerID dominantPid, out Fixnum dominantRespect, out Fixnum secondHighestRespect)
		{
			dominantPid = PlayerID.INVALID;
			dominantRespect = Fixnum.ZERO;
			secondHighestRespect = Fixnum.ZERO;
			if (node?.respect?.data == null || node.respect.data.Count == 0)
			{
				return false;
			}

			for (int i = 0; i < node.respect.data.Count; i++)
			{
				Respect respect = node.respect.data[i];
				if (respect == null || respect.pid.IsNotValid)
				{
					continue;
				}

				Fixnum displayedRespect = GetDisplayedRespectValue(respect);
				if (displayedRespect <= Fixnum.ZERO)
				{
					continue;
				}

				if (dominantPid.IsNotValid || displayedRespect > dominantRespect)
				{
					secondHighestRespect = dominantRespect;
					dominantRespect = displayedRespect;
					dominantPid = respect.pid;
				}
				else if (displayedRespect > secondHighestRespect)
				{
					secondHighestRespect = displayedRespect;
				}
			}

			return dominantPid.IsValid;
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
				if (node?.respect?.data == null || node.respect.data.Count == 0)
				{
					continue;
				}

				for (int i = 0; i < node.respect.data.Count; i++)
				{
					Respect respect = node.respect.data[i];
					if (respect == null)
					{
						continue;
					}

					respect.fromNeighbors = Fixnum.ZERO;
				}
			}

			MethodInfo updateRespectFromTerritory = AccessTools.Method(typeof(global::Game.Session.Sim.BusinessUpdate), "UpdateRespectFromTerritory");
			try
			{
				updateRespectFromTerritory?.Invoke(null, null);
			}
			catch
			{
			}

			foreach (Node node in allNodes)
			{
				if (node == null)
				{
					continue;
				}

				HeatAndRespect.RecomputeRespectForAllPlayers(node, true);
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
				if (node == null)
				{
					continue;
				}

				nodeList.Add(node);
			}

			return nodeList;
		}

		private static Fixnum GetHumanTerritoryClaimStrength(Respect respect)
		{
			if (respect == null)
			{
				return Fixnum.ZERO;
			}

			Fixnum current = GetEffectiveTerritoryRespectValue(respect);
			if (respect.fromAOE <= Fixnum.ZERO)
			{
				return current;
			}

			int nearbyBizPercent = GetHumanBackgroundTerritoryBusinessRespectPercent();
			if (nearbyBizPercent <= 100)
			{
				return current;
			}

			return current + (respect.fromAOE * (Fixnum)(nearbyBizPercent - 100)) / 100;
		}

		private static Fixnum GetEffectiveTerritoryRespectValue(Respect respect)
		{
			if (respect == null)
			{
				return Fixnum.ZERO;
			}

			return respect.current;
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

				Fixnum effectiveStrength = GetEffectiveTerritoryRespectValue(respect);
				if (effectiveStrength > highest)
				{
					highest = effectiveStrength;
				}
			}

			return highest;
		}

		private static void ForceRefreshHumanTerritoryVisuals()
		{
			TryForceRefreshHumanTerritoryVisuals(out _);
		}

		private static bool TryForceRefreshIllegalBackroomAffectedVisuals(Entity building, out string waitReason)
		{
			waitReason = string.Empty;
			HashSet<Node> affectedNodes = GetPotentialDirtyCashBackroomAffectedNodes(building);
			if (affectedNodes == null || affectedNodes.Count == 0)
			{
				return TryForceRefreshHumanTerritoryVisuals(out waitReason);
			}

			try
			{
				TerritoryColorPatch.RefreshAllTerritoryColors();
			}
			catch
			{
			}

			try
			{
				if (!TryResolveHudPickManager(out object pickManager))
				{
					waitReason = "no-pick-manager";
					return false;
				}

				Type pickManagerType = pickManager.GetType();
				MethodInfo refreshPicksOnKnownBuildingsMethod = pickManagerType.GetMethod(
					"RefreshPicksOnKnownBuildings",
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
					null,
					new[] { typeof(PlayerInfo) },
					null);
				MethodInfo refreshPickContainersMethod = pickManagerType.GetMethod(
					"RefreshPickContainers",
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
					null,
					new[] { typeof(bool), typeof(bool) },
					null);

				if (_cachedHudPickManager != pickManager
					|| _refreshBuildingPicksOnNodeMethod == null
					|| _refreshVisibleSummaryPicksMethod == null
					|| _refreshVisibleCornerPicksMethod == null
					|| _refreshVisibleBuildingPicksMethod == null)
				{
					_cachedHudPickManager = pickManager;
					_refreshBuildingPicksOnNodeMethod = pickManagerType.GetMethod("RefreshBuildingPicksOnNode", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(Node) }, null);
					_refreshVisibleSummaryPicksMethod = pickManagerType.GetMethod("RefreshVisibleSummaryPicks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					_refreshVisibleCornerPicksMethod = pickManagerType.GetMethod("RefreshVisibleCornerPicks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					_refreshVisibleBuildingPicksMethod = pickManagerType.GetMethod("RefreshVisibleBuildingPicks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				}

				if (_refreshBuildingPicksOnNodeMethod == null
					|| _refreshVisibleSummaryPicksMethod == null
					|| _refreshVisibleCornerPicksMethod == null
					|| _refreshVisibleBuildingPicksMethod == null)
				{
					waitReason = "missing-pick-refresh-method";
					return false;
				}

				TryGetHumanPlayer(out PlayerInfo humanPlayer);
				if (humanPlayer != null)
				{
					refreshPicksOnKnownBuildingsMethod?.Invoke(pickManager, new object[] { humanPlayer });
				}

				HashSet<int> refreshedNodeIds = new HashSet<int>();
				foreach (Node affectedNode in affectedNodes)
				{
					if (affectedNode == null || !affectedNode.id.IsValid || !refreshedNodeIds.Add(affectedNode.id.GetHashCode()))
					{
						continue;
					}

					_refreshBuildingPicksOnNodeMethod.Invoke(pickManager, new object[] { affectedNode });
				}

				_refreshVisibleSummaryPicksMethod.Invoke(pickManager, null);
				_refreshVisibleCornerPicksMethod.Invoke(pickManager, null);
				_refreshVisibleBuildingPicksMethod.Invoke(pickManager, null);
				refreshPickContainersMethod?.Invoke(pickManager, new object[] { false, true });

				if (!TryAuditAndRepairIllegalBackroomAffectedCornerPicks(building, pickManager, out string auditWaitReason, out int staleCornerCount, out int repairedCornerPickCount))
				{
					TryForceRefreshHumanTerritoryVisuals(out _);
					if (!TryAuditAndRepairIllegalBackroomAffectedCornerPicks(building, pickManager, out auditWaitReason, out staleCornerCount, out repairedCornerPickCount))
					{
						waitReason = auditWaitReason;
						return false;
					}
				}

				return true;
			}
			catch
			{
				waitReason = "pick-refresh-exception";
				return false;
			}
		}

		private static bool TryAuditAndRepairIllegalBackroomAffectedCornerPicks(Entity building, object pickManager, out string waitReason, out int staleCornerCount, out int repairedCornerPickCount)
		{
			waitReason = string.Empty;
			staleCornerCount = 0;
			repairedCornerPickCount = 0;
			if (building == null || pickManager == null)
			{
				return true;
			}

			PlayerID controllingPid = GetControllingPlayerId(building);
			if (controllingPid.IsNotValid)
			{
				return true;
			}

			HashSet<Node> affectedNodes = GetPotentialDirtyCashBackroomAffectedNodes(building);
			if (affectedNodes == null || affectedNodes.Count == 0)
			{
				return true;
			}

			global::Game.UI.Session.Picks.PickContainer cornerContainer = TryGetPickContainer(pickManager, global::Game.UI.Session.Picks.PickType.CornerPick);
			global::Game.UI.Session.Picks.PickContainer summaryContainer = TryGetPickContainer(pickManager, global::Game.UI.Session.Picks.PickType.SummaryPick);
			if (cornerContainer == null && summaryContainer == null)
			{
				waitReason = "missing-pick-container";
				return false;
			}

			HashSet<ulong> expectedCornerIds = new HashSet<ulong>();
			foreach (Node affectedNode in affectedNodes)
			{
				if (affectedNode == null || !affectedNode.id.IsValid)
				{
					continue;
				}

				if (GetDisplayedRespectForPlayer(affectedNode, controllingPid) <= Fixnum.ZERO)
				{
					continue;
				}

				var cornerResult = global::Game.Game.ctx?.board?.CornerCache.FindCorner(affectedNode.id) ?? default;
				Entity corner = cornerResult.valid ? cornerResult.corner : null;
				if (corner?.Id.IsValid == true)
				{
					expectedCornerIds.Add(corner.Id.id);
				}
			}

			if (expectedCornerIds.Count == 0)
			{
				return true;
			}

			bool repairedAny = false;
			foreach (ulong expectedCornerId in expectedCornerIds)
			{
				EntityID cornerId = EntityID.FromID(expectedCornerId);
				global::Game.UI.Session.Picks.PickTarget target = new global::Game.UI.Session.Picks.PickTarget(cornerId);
				bool hasCornerPick = cornerContainer?.picks?.ContainsKey(target) == true;
				bool hasSummaryPick = summaryContainer?.picks?.ContainsKey(target) == true;
				if (hasCornerPick && hasSummaryPick)
				{
					continue;
				}

				staleCornerCount++;
				if (!hasCornerPick && TryAddOrRefreshPick(cornerContainer, target))
				{
					repairedCornerPickCount++;
					repairedAny = true;
				}

				if (!hasSummaryPick && TryAddOrRefreshPick(summaryContainer, target))
				{
					repairedCornerPickCount++;
					repairedAny = true;
				}
			}

			if (repairedAny)
			{
				try
				{
					_refreshVisibleSummaryPicksMethod?.Invoke(pickManager, null);
					_refreshVisibleCornerPicksMethod?.Invoke(pickManager, null);
					_refreshVisibleBuildingPicksMethod?.Invoke(pickManager, null);
				}
				catch
				{
				}
			}

			int remainingStaleCornerCount = 0;
			foreach (ulong expectedCornerId in expectedCornerIds)
			{
				EntityID cornerId = EntityID.FromID(expectedCornerId);
				global::Game.UI.Session.Picks.PickTarget target = new global::Game.UI.Session.Picks.PickTarget(cornerId);
				bool hasCornerPick = cornerContainer?.picks?.ContainsKey(target) == true;
				bool hasSummaryPick = summaryContainer?.picks?.ContainsKey(target) == true;
				if (!hasCornerPick || !hasSummaryPick)
				{
					remainingStaleCornerCount++;
				}
			}

			if (remainingStaleCornerCount > 0)
			{
				staleCornerCount = remainingStaleCornerCount;
				waitReason = "stale-corner-picks:" + remainingStaleCornerCount;
				return false;
			}

			if (repairedAny || staleCornerCount > 0)
			{
				VerificationLog("Compat", $"illegal-backroom-visual-audit building={building.Id} expected={expectedCornerIds.Count} repaired={repairedCornerPickCount} stale={remainingStaleCornerCount}");
			}

			return true;
		}

		private static global::Game.UI.Session.Picks.PickContainer TryGetPickContainer(object pickManager, global::Game.UI.Session.Picks.PickType pickType)
		{
			if (pickManager == null)
			{
				return null;
			}

			try
			{
				if (_getPickContainerMethod == null || _getPickContainerMethod.DeclaringType != pickManager.GetType())
				{
					_getPickContainerMethod = pickManager.GetType().GetMethod("GetContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(global::Game.UI.Session.Picks.PickType) }, null);
				}

				return _getPickContainerMethod?.Invoke(pickManager, new object[] { pickType }) as global::Game.UI.Session.Picks.PickContainer;
			}
			catch
			{
				return null;
			}
		}

		private static bool TryAddOrRefreshPick(global::Game.UI.Session.Picks.PickContainer container, global::Game.UI.Session.Picks.PickTarget target)
		{
			if (container == null)
			{
				return false;
			}

			try
			{
				if (_pickContainerAddOrRefreshMethod == null || _pickContainerAddOrRefreshMethod.DeclaringType != container.GetType())
				{
					_pickContainerAddOrRefreshMethod = container.GetType().GetMethod("AddOrRefreshPick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(global::Game.UI.Session.Picks.PickTarget), typeof(bool) }, null);
				}

				if (_pickContainerAddOrRefreshMethod == null)
				{
					return false;
				}

				_pickContainerAddOrRefreshMethod.Invoke(container, new object[] { target, false });
				return container.picks?.ContainsKey(target) == true;
			}
			catch
			{
				return false;
			}
		}

		private static PlayerID GetControllingPlayerId(Entity building)
		{
			return building?.data?.building?.controlled.Get() ?? PlayerID.INVALID;
		}

		private static Fixnum GetDisplayedRespectForPlayer(Node node, PlayerID pid)
		{
			Respect respect = node?.respect?.GetOrNull(pid);
			if (respect == null)
			{
				return Fixnum.ZERO;
			}

			return respect.goal > respect.current ? respect.goal : respect.current;
		}

		private static bool TryForceRefreshHumanTerritoryVisuals(out string waitReason)
		{
			waitReason = string.Empty;
			try
			{
				TerritoryColorPatch.RefreshAllTerritoryColors();
			}
			catch
			{
			}

			try
			{
				if (!TryResolveHudPickManager(out object pickManager))
				{
					waitReason = "no-pick-manager";
					return false;
				}

				Type pickManagerType = pickManager.GetType();
				MethodInfo refreshPicksOnKnownBuildingsMethod = pickManagerType.GetMethod(
					"RefreshPicksOnKnownBuildings",
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
					null,
					new[] { typeof(PlayerInfo) },
					null);
				MethodInfo refreshPickContainersMethod = pickManagerType.GetMethod(
					"RefreshPickContainers",
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
					null,
					new[] { typeof(bool), typeof(bool) },
					null);

				PlayerInfo humanPlayer = global::Game.Game.ctx?.players?.Human;
				if (humanPlayer != null)
				{
					refreshPicksOnKnownBuildingsMethod?.Invoke(pickManager, new object[] { humanPlayer });
				}

				if (_cachedHudPickManager != pickManager
					|| _refreshVisibleSummaryPicksMethod == null
					|| _refreshVisibleCornerPicksMethod == null
					|| _refreshVisibleBuildingPicksMethod == null)
				{
					_cachedHudPickManager = pickManager;
					_refreshVisibleSummaryPicksMethod = pickManagerType.GetMethod("RefreshVisibleSummaryPicks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					_refreshVisibleCornerPicksMethod = pickManagerType.GetMethod("RefreshVisibleCornerPicks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					_refreshVisibleBuildingPicksMethod = pickManagerType.GetMethod("RefreshVisibleBuildingPicks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				}

				if (_refreshVisibleSummaryPicksMethod == null
					|| _refreshVisibleCornerPicksMethod == null
					|| _refreshVisibleBuildingPicksMethod == null)
				{
					waitReason = "missing-pick-refresh-method";
					return false;
				}

				_refreshVisibleSummaryPicksMethod.Invoke(pickManager, null);
				_refreshVisibleCornerPicksMethod.Invoke(pickManager, null);
				_refreshVisibleBuildingPicksMethod.Invoke(pickManager, null);
				refreshPickContainersMethod?.Invoke(pickManager, new object[] { false, true });
				return true;
			}
			catch
			{
				waitReason = "pick-refresh-exception";
				return false;
			}
		}

		internal static void RequestDeferredHumanTerritoryRefresh(string sourceTag, int delayFrames = 0, int passes = 3)
		{
			bool wasPending = _pendingDeferredHumanTerritoryRefresh;
			int previousEarliestFrame = _pendingDeferredHumanTerritoryRefreshEarliestFrame;
			int previousRemainingPasses = _pendingDeferredHumanTerritoryRefreshRemainingPasses;
			_loadedSessionTerritoryRefreshCompleted = false;
			_pendingDeferredHumanTerritoryRefresh = true;
			int requestedEarliestFrame = Time.frameCount + Mathf.Max(0, delayFrames);
			if (_pendingDeferredHumanTerritoryRefreshEarliestFrame < 0)
			{
				_pendingDeferredHumanTerritoryRefreshEarliestFrame = requestedEarliestFrame;
			}
			else
			{
				_pendingDeferredHumanTerritoryRefreshEarliestFrame = Mathf.Min(_pendingDeferredHumanTerritoryRefreshEarliestFrame, requestedEarliestFrame);
			}

			_pendingDeferredHumanTerritoryRefreshRemainingPasses = Mathf.Max(_pendingDeferredHumanTerritoryRefreshRemainingPasses, Mathf.Max(1, passes));
			_pendingDeferredHumanTerritoryRefreshSource = string.IsNullOrEmpty(sourceTag) ? "unknown" : sourceTag;
			_lastDeferredHumanTerritoryRefreshWaitReason = string.Empty;
			_lastDeferredHumanTerritoryRefreshScheduleReason = string.Empty;
			bool changedRequest = !wasPending
				|| previousEarliestFrame != _pendingDeferredHumanTerritoryRefreshEarliestFrame
				|| previousRemainingPasses != _pendingDeferredHumanTerritoryRefreshRemainingPasses;
			if (changedRequest)
			{
				int duplicateRequests = _deferredHumanTerritoryRefreshDuplicateRequestCount;
				_deferredHumanTerritoryRefreshDuplicateRequestCount = 0;
				VerificationLog(
					"Compat",
					$"deferred-human-territory-refresh-requested source={_pendingDeferredHumanTerritoryRefreshSource} earliestFrame={_pendingDeferredHumanTerritoryRefreshEarliestFrame} passes={_pendingDeferredHumanTerritoryRefreshRemainingPasses} duplicateRequests={duplicateRequests} frame={Time.frameCount}");
			}
			else
			{
				_deferredHumanTerritoryRefreshDuplicateRequestCount++;
			}
		}

		private static void RequestDeferredHumanTerritoryFullRebuild(string sourceTag, int delayFrames = 0, int passes = 1)
		{
			bool wasPending = _pendingDeferredHumanTerritoryFullRebuild;
			_pendingDeferredHumanTerritoryFullRebuild = true;
			if (!wasPending || string.IsNullOrEmpty(_pendingDeferredHumanTerritoryFullRebuildSource))
			{
				_pendingDeferredHumanTerritoryFullRebuildSource = string.IsNullOrEmpty(sourceTag) ? "unknown" : sourceTag;
			}
			RequestDeferredHumanTerritoryRefresh(sourceTag, delayFrames, passes);
			if (!wasPending)
			{
				VerificationLog(
					"Compat",
					$"deferred-human-territory-full-rebuild-requested source={_pendingDeferredHumanTerritoryFullRebuildSource} earliestFrame={_pendingDeferredHumanTerritoryRefreshEarliestFrame} passes={Mathf.Max(1, passes)} frame={Time.frameCount}");
			}
		}

		internal static void EnsureDeferredHumanTerritoryRefreshScheduledForLoadedSession(string sourceTag)
		{
			if (_loadedSessionTerritoryRefreshCompleted || _pendingDeferredHumanTerritoryRefresh)
			{
				return;
			}

			global::Game.Session.SessionContext ctx = global::Game.Game.ctx;
			if (ctx == null)
			{
				const string waitReason = "ctx-null";
				if (!string.Equals(_lastDeferredHumanTerritoryRefreshScheduleReason, waitReason, StringComparison.Ordinal))
				{
					_lastDeferredHumanTerritoryRefreshScheduleReason = waitReason;
					VerificationLog("Compat", $"deferred-human-territory-refresh-skip source={sourceTag} reason={waitReason} frame={Time.frameCount}");
				}
				return;
			}

			if (!TryGetHumanPlayer(out PlayerInfo _))
			{
				const string waitReason = "human-player-null";
				if (!string.Equals(_lastDeferredHumanTerritoryRefreshScheduleReason, waitReason, StringComparison.Ordinal))
				{
					_lastDeferredHumanTerritoryRefreshScheduleReason = waitReason;
					VerificationLog("Compat", $"deferred-human-territory-refresh-skip source={sourceTag} reason={waitReason} frame={Time.frameCount}");
				}
				return;
			}

			if (ctx.board?.nodes == null)
			{
				const string waitReason = "board-nodes-null";
				if (!string.Equals(_lastDeferredHumanTerritoryRefreshScheduleReason, waitReason, StringComparison.Ordinal))
				{
					_lastDeferredHumanTerritoryRefreshScheduleReason = waitReason;
					VerificationLog("Compat", $"deferred-human-territory-refresh-skip source={sourceTag} reason={waitReason} frame={Time.frameCount}");
				}
				return;
			}

			RequestDeferredHumanTerritoryRefresh(sourceTag, 0, ctx.IsSessionFromNewGame ? 6 : 5);
			VerificationLog("Compat", $"deferred-human-territory-refresh-scheduled source={sourceTag} mode={(ctx.IsSessionFromNewGame ? "new-game" : "loaded")} frame={Time.frameCount}");
		}

		internal static void FlushDeferredHumanTerritoryRefresh(string sourceTag)
		{
			if (!_pendingDeferredHumanTerritoryRefresh)
			{
				return;
			}

			if (Time.frameCount < _pendingDeferredHumanTerritoryRefreshEarliestFrame)
			{
				return;
			}

			TryGetHumanPlayer(out PlayerInfo humanPlayer);
			IEnumerable<Node> allNodes = global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe();
			if (humanPlayer?.territory == null || allNodes == null)
			{
				string waitReason = (humanPlayer?.territory == null) ? "human-territory-null" : "board-nodes-null";
				if (!string.Equals(_lastDeferredHumanTerritoryRefreshWaitReason, waitReason, StringComparison.Ordinal))
				{
					_lastDeferredHumanTerritoryRefreshWaitReason = waitReason;
					VerificationLog("Compat", $"deferred-human-territory-refresh-wait source={sourceTag} reason={waitReason} frame={Time.frameCount}");
				}
				_pendingDeferredHumanTerritoryRefreshEarliestFrame = Time.frameCount + 2;
				return;
			}
			_lastDeferredHumanTerritoryRefreshWaitReason = string.Empty;

			try
			{
				bool didFullRebuild = false;
				if (_pendingDeferredHumanTerritoryFullRebuild)
				{
					string rebuildSource = _pendingDeferredHumanTerritoryFullRebuildSource;
					int ownershipSwitchCount;
					int rebuiltNodeCount = RebuildLateForcedDirtyCashRespectState(initial: false, out ownershipSwitchCount);
					VerificationLog(
						"Compat",
						$"deferred-human-territory-full-rebuild source={rebuildSource}->{sourceTag} nodes={rebuiltNodeCount} ownershipSwitches={ownershipSwitchCount} gangCollapsePids={PendingGangCollapseTerritoryRebuildPids.Count} suppressedCollapseRequests={_gangCollapseTerritoryDeferredSuppressedCount}");
					PendingGangCollapseTerritoryRebuildPids.Clear();
					_gangCollapseTerritoryDeferredSuppressedCount = 0;
					_pendingDeferredHumanTerritoryFullRebuild = false;
					_pendingDeferredHumanTerritoryFullRebuildSource = string.Empty;
					didFullRebuild = true;
				}

				if (!didFullRebuild)
				{
					TryRefreshAllPlayerOutpostTargetRespect(_pendingDeferredHumanTerritoryRefreshSource);
				}
				HashSet<PlayerID> neutralNodeClaims = new HashSet<PlayerID>();
				global::Game.Services.RespectSettings respectSettings = global::Game.Game.serv?.globals?.settings?.people?.social?.respect;
				int claimedNeutralNodes = ClaimNeutralNodesFromCurrentRespect(allNodes, respectSettings, neutralNodeClaims);
				if (claimedNeutralNodes > 0)
				{
					RefreshLateForcedDirtyCashTerritoryState(neutralNodeClaims);
					VerificationLog("Compat", $"territory-neutral-claim source={_pendingDeferredHumanTerritoryRefreshSource} switches={claimedNeutralNodes}");
				}
				if (ShouldEnableHumanBackgroundTerritoryReconcile())
				{
					ReconcileTerritoryOwnershipFromCurrentRespectUntilStable(
						allNodes,
						_pendingDeferredHumanTerritoryRefreshSource,
						refreshVisualsWhenNoChanges: false,
						allowRelaxedUnownedClaim: true);
				}
				if (!TryForceRefreshHumanTerritoryVisuals(out string visualWaitReason))
				{
					if (!string.Equals(_lastDeferredHumanTerritoryRefreshWaitReason, visualWaitReason, StringComparison.Ordinal))
					{
						_lastDeferredHumanTerritoryRefreshWaitReason = visualWaitReason;
						VerificationLog("Compat", $"deferred-human-territory-refresh-wait source={sourceTag} reason={visualWaitReason} frame={Time.frameCount}");
					}
					_pendingDeferredHumanTerritoryRefreshEarliestFrame = Time.frameCount + 2;
					return;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Deferred human territory refresh failed: " + ex.Message);
			}
			_lastDeferredHumanTerritoryRefreshWaitReason = string.Empty;

			_pendingDeferredHumanTerritoryRefreshRemainingPasses = Math.Max(0, _pendingDeferredHumanTerritoryRefreshRemainingPasses - 1);
			if (_pendingDeferredHumanTerritoryRefreshRemainingPasses > 0)
			{
				_pendingDeferredHumanTerritoryRefreshEarliestFrame = Time.frameCount + 5;
				VerificationLog("Compat", $"deferred-human-territory-refresh source={_pendingDeferredHumanTerritoryRefreshSource}->{sourceTag} remaining={_pendingDeferredHumanTerritoryRefreshRemainingPasses}");
				return;
			}

			_pendingDeferredHumanTerritoryRefresh = false;
			_pendingDeferredHumanTerritoryRefreshEarliestFrame = -1;
			_pendingDeferredHumanTerritoryRefreshRemainingPasses = 0;
			_pendingDeferredHumanTerritoryRefreshSource = string.Empty;
			_loadedSessionTerritoryRefreshCompleted = true;
			LogTerritoryOwnershipAudit(sourceTag);
			VerificationLog("Compat", $"deferred-human-territory-refresh-complete source={sourceTag}");
		}

		internal static void FlushDeferredIllegalBackroomVisualRefreshes(string sourceTag)
		{
			if (PendingIllegalBackroomVisualRefreshes.Count == 0)
			{
				return;
			}

			List<EntityID> buildingIds = new List<EntityID>(PendingIllegalBackroomVisualRefreshes.Keys);
			for (int i = 0; i < buildingIds.Count; i++)
			{
				EntityID buildingId = buildingIds[i];
				if (!PendingIllegalBackroomVisualRefreshes.TryGetValue(buildingId, out PendingIllegalBackroomVisualRefresh pending))
				{
					continue;
				}

				if (Time.frameCount < pending.EarliestFrame)
				{
					continue;
				}

				Entity building = buildingId.FindEntity();
				if (building == null || !HasInstalledDirtyCashBackroomModule(building.components?.modules))
				{
					PendingIllegalBackroomVisualRefreshes.Remove(buildingId);
					continue;
				}

				if (!pending.AppliedLateRespectSync)
				{
					RefreshHeatAndRespectForLateForcedDirtyCashBackroom(building, initial: false, source: "deferred-illegal-backroom");
					pending.AppliedLateRespectSync = true;
				}
				else
				{
					RefreshLateForcedDirtyCashAffectedNodes(building);
				}

				if (!TryForceRefreshIllegalBackroomAffectedVisuals(building, out string waitReason))
				{
					bool staleCornerAuditFailure = !string.IsNullOrEmpty(waitReason) && waitReason.StartsWith("stale-corner-picks:", StringComparison.Ordinal);
					if (!string.Equals(pending.LastWaitReason, waitReason, StringComparison.Ordinal))
					{
						pending.LastWaitReason = waitReason;
						VerificationLog("Compat", $"deferred-illegal-backroom-visual-refresh-wait source={pending.Source}->{sourceTag} building={buildingId} reason={waitReason} frame={Time.frameCount}");
					}

					if (staleCornerAuditFailure)
					{
						pending.RemainingAuditRetries = Math.Max(0, pending.RemainingAuditRetries - 1);
						if (pending.RemainingAuditRetries <= 0)
						{
							PendingIllegalBackroomVisualRefreshes.Remove(buildingId);
							VerificationLog("Compat", $"deferred-illegal-backroom-visual-refresh-audit-exhausted source={pending.Source}->{sourceTag} building={buildingId} reason={waitReason}");
							continue;
						}
					}

					pending.EarliestFrame = Time.frameCount + 10;
					PendingIllegalBackroomVisualRefreshes[buildingId] = pending;
					continue;
				}

				pending.LastWaitReason = string.Empty;
				pending.RemainingPasses = Math.Max(0, pending.RemainingPasses - 1);
				if (pending.RemainingPasses > 0)
				{
					pending.EarliestFrame = Time.frameCount + 15;
					PendingIllegalBackroomVisualRefreshes[buildingId] = pending;
					VerificationLog("Compat", $"deferred-illegal-backroom-visual-refresh source={pending.Source}->{sourceTag} building={buildingId} remaining={pending.RemainingPasses}");
					continue;
				}

				PendingIllegalBackroomVisualRefreshes.Remove(buildingId);
				VerificationLog("Compat", $"deferred-illegal-backroom-visual-refresh-complete source={pending.Source}->{sourceTag} building={buildingId}");
			}
		}

		private static void RefreshLateForcedDirtyCashTerritoryState(IEnumerable<PlayerID> affectedPlayers)
		{
			if (affectedPlayers == null)
			{
				return;
			}

			MethodInfo updateNodeDataOnTerritoryChange = AccessTools.Method(typeof(PlayerTerritory), "UpdateNodeDataOnTerritoryChange");
			FieldInfo cachedPotentialsField = AccessTools.Field(typeof(PlayerTerritory), "_cachedPotentials");
			MethodInfo onTerritoryChangedMethod = cachedPotentialsField != null
				? AccessTools.Method(cachedPotentialsField.FieldType, "OnTerritoryChanged")
				: null;
			HashSet<int> refreshedPlayerIds = new HashSet<int>();
			foreach (PlayerID affectedPlayer in affectedPlayers)
			{
				if (affectedPlayer.IsNotValid || !refreshedPlayerIds.Add(affectedPlayer.id))
				{
					continue;
				}

				PlayerTerritory territory = G.FindPlayerById(affectedPlayer.id)?.territory;
				if (territory == null)
				{
					continue;
				}

				try
				{
					updateNodeDataOnTerritoryChange?.Invoke(territory, null);
				}
				catch
				{
				}

				try
				{
					object cachedPotentials = cachedPotentialsField?.GetValue(territory);
					onTerritoryChangedMethod?.Invoke(cachedPotentials, null);
				}
				catch
				{
				}
			}

			ForceRefreshHumanTerritoryVisuals();
		}

		private static bool TryGetHumanPlayer(out PlayerInfo humanPlayer)
		{
			humanPlayer = null;
			try
			{
				humanPlayer = global::Game.Game.ctx?.players?.Human;
			}
			catch
			{
				humanPlayer = null;
			}

			return humanPlayer != null;
		}

		private static Fixnum FindLateForcedDirtyCashPreviousCurrent(NodeID nodeId, PlayerID pid)
		{
			int currentDay = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;
			if (LateForcedDirtyCashRespectSnapshotDay != currentDay)
			{
				return Fixnum.ZERO;
			}

			if (LateForcedDirtyCashRespectCurrentSnapshot.TryGetValue(BuildLateForcedDirtyCashRespectSnapshotKey(nodeId, pid), out LateForcedDirtyCashRespectSnapshot snapshot))
			{
				return snapshot.Current;
			}

			return Fixnum.ZERO;
		}

		private static void CaptureLateForcedDirtyCashRespectSnapshot()
		{
			IEnumerable<Node> allNodes = global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe();
			if (allNodes == null)
			{
				return;
			}

			LateForcedDirtyCashRespectCurrentSnapshot.Clear();
			LateForcedDirtyCashRespectSnapshotDay = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;

			foreach (Node node in allNodes)
			{
				if (node?.respect?.data == null)
				{
					continue;
				}

				for (int i = 0; i < node.respect.data.Count; i++)
				{
					Respect respect = node.respect.data[i];
					if (respect == null)
					{
						continue;
					}

					LateForcedDirtyCashRespectCurrentSnapshot[BuildLateForcedDirtyCashRespectSnapshotKey(node.id, respect.pid)] = new LateForcedDirtyCashRespectSnapshot
					{
						Current = respect.current,
						FromRelationships = respect.fromRelationships,
						FromNeighbors = respect.fromNeighbors,
						FromEthnicity = respect.fromEthnicity,
						FromSafehouse = respect.fromSafehouse
					};
				}
			}
		}

		private static bool RestoreLateForcedDirtyCashRespectSnapshotForCurrentDay(IEnumerable<Node> allNodes)
		{
			if (allNodes == null)
			{
				return false;
			}

			int currentDay = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue;
			if (LateForcedDirtyCashRespectSnapshotDay != currentDay || LateForcedDirtyCashRespectCurrentSnapshot.Count == 0)
			{
				return false;
			}

			foreach (Node node in allNodes)
			{
				if (node?.respect?.data == null)
				{
					continue;
				}

				for (int i = 0; i < node.respect.data.Count; i++)
				{
					Respect respect = node.respect.data[i];
					if (respect == null)
					{
						continue;
					}

					if (LateForcedDirtyCashRespectCurrentSnapshot.TryGetValue(BuildLateForcedDirtyCashRespectSnapshotKey(node.id, respect.pid), out LateForcedDirtyCashRespectSnapshot snapshot))
					{
						respect.current = snapshot.Current;
						respect.fromRelationships = snapshot.FromRelationships;
						respect.fromNeighbors = snapshot.FromNeighbors;
						respect.fromEthnicity = snapshot.FromEthnicity;
						respect.fromSafehouse = snapshot.FromSafehouse;
					}
					else
					{
						respect.current = 0;
					}
				}
			}

			return true;
		}

		private static string BuildLateForcedDirtyCashRespectSnapshotKey(NodeID nodeId, PlayerID pid)
		{
			return nodeId + "|" + pid;
		}

		private static HashSet<Node> GetPotentialDirtyCashBackroomAffectedNodes(Entity building)
		{
			HashSet<Node> results = new HashSet<Node>();
			Node originNode = building?.data?.board?.bead.nodeId.FindNode();
			if (originNode == null)
			{
				return results;
			}

			results.Add(originNode);

			ModulesComponent modules = building.components?.modules;
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				return results;
			}

			ModuleQuery moduleQuery = ModulesUtil.MakeModuleQuery(building);
			ModQuery managerQuery = moduleQuery.MakeManagerModQuery();
			managerQuery.nodeId = originNode.id;

			for (int i = 0; i < slots.Count; i++)
			{
				if (!(slots[i] is ConsumerModule consumer) || !IsDirtyCashBackroomModule(consumer.ModuleConfig))
				{
					continue;
				}

				ConsumerRecipe.AreaDef aoe = consumer.config?.sink?.aoe;
				if (aoe?.respectRadius == null || aoe.respectPointsInRadius == null)
				{
					continue;
				}

				Fixnum radius = aoe.respectRadius.Evaluate(managerQuery);
				List<Node> nodesInRadius = global::Game.Game.ctx.board.nodes.FindAndSortNodesInRadius(originNode.pos, (float)radius, sort: false);
				for (int j = 0; j < nodesInRadius.Count; j++)
				{
					results.Add(nodesInRadius[j]);
				}
			}

			return results;
		}

		private static void RefreshLateForcedDirtyCashAffectedNodes(Entity building)
		{
			HashSet<Node> affectedNodes = GetPotentialDirtyCashBackroomAffectedNodes(building);
			if (affectedNodes == null || affectedNodes.Count == 0)
			{
				return;
			}

			foreach (Node node in affectedNodes)
			{
				if (node == null)
				{
					continue;
				}

				HeatAndRespect.RecomputeRespectForAllPlayers(node, true);
			}

			ReconcileHumanTerritoryOwnershipUntilStable(
				affectedNodes,
				"localized-aoe",
				useDirtyCashPreviousCurrent: false,
				refreshHumanTerritoryStateWhenNoChanges: false);
		}

		private static void RequestDeferredIllegalBackroomVisualRefresh(Entity building, string sourceTag, int delayFrames = 8, int passes = 2)
		{
			EntityID buildingId = building?.Id ?? EntityID.INVALID;
			if (!buildingId.IsValid || !HasInstalledDirtyCashBackroomModule(building?.components?.modules))
			{
				return;
			}

			if (!PendingIllegalBackroomVisualRefreshes.TryGetValue(buildingId, out PendingIllegalBackroomVisualRefresh pending))
			{
				pending = new PendingIllegalBackroomVisualRefresh();
				PendingIllegalBackroomVisualRefreshes[buildingId] = pending;
			}

			int requestedEarliestFrame = Time.frameCount + Mathf.Max(0, delayFrames);
			if (pending.EarliestFrame < 0)
			{
				pending.EarliestFrame = requestedEarliestFrame;
			}
			else
			{
				pending.EarliestFrame = Mathf.Min(pending.EarliestFrame, requestedEarliestFrame);
			}

			pending.RemainingPasses = Math.Max(pending.RemainingPasses, Math.Max(1, passes));
			pending.RemainingAuditRetries = Math.Max(pending.RemainingAuditRetries, IllegalBackroomVisualAuditRetryBudget);
			pending.Source = string.IsNullOrEmpty(sourceTag) ? "unknown" : sourceTag;
			pending.LastWaitReason = string.Empty;
			PendingIllegalBackroomVisualRefreshes[buildingId] = pending;

			string logKey = pending.Source + "|" + buildingId + "|frame=" + pending.EarliestFrame + "|passes=" + pending.RemainingPasses;
			if (!LoggedIllegalBackroomDeferredVisualRefreshes.Add(logKey))
			{
				return;
			}

			VerificationLog("Compat", $"deferred-illegal-backroom-visual-refresh-requested source={pending.Source} building={buildingId} earliestFrame={pending.EarliestFrame} passes={pending.RemainingPasses}");
		}

		private static void IllegalBackroomManufactureDoUpdatePostfix(ManufactureModule __instance, ModuleQuery q, bool initial, bool enabled, ModuleResult __result)
		{
			try
			{
				if (!IsHumanOwnedDirtyCashBackroomModule(__instance?.ModuleConfig, q.container, q.pid))
				{
					return;
				}

				string moduleId = __instance?.ModuleConfig?.Id.String;
				SimTime now = global::Game.Game.ctx?.clock?.Now ?? __instance.data.lastUpdate;
				SimTime enableTime = __instance.data.EnableTime;
				int daysUntilEnabled = Math.Max(enableTime.days - now.days, 0);
				string logKey = moduleId +
					"|initial=" +
					initial +
					"|enabled=" +
					enabled +
					"|result=" +
					__result +
					"|day=" +
					now.days +
					"|enableDay=" +
					enableTime.days;
				if (string.IsNullOrEmpty(moduleId) || !LoggedIllegalBackroomManufactureUpdates.Add(logKey))
				{
					return;
				}

				Debug.Log("[GameplayTweaks] Illegal backroom manufacture update observed id=" +
					moduleId +
					" qpid=" +
					q.pid +
					" buildingOwner=" +
					GetControllingPlayerString(q.container) +
					" initial=" +
					initial +
					" enabled=" +
					enabled +
					" nowDay=" +
					now.days +
					" enableDay=" +
					enableTime.days +
					" daysUntilEnabled=" +
					daysUntilEnabled +
					" result=" +
					__result);

				if (enabled)
				{
					RequestDeferredIllegalBackroomVisualRefresh(q.container, initial ? "manufacture-enabled-initial" : "manufacture-enabled");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to log illegal backroom manufacture update: " + ex.Message);
			}
		}

		private static void IllegalBackroomConsumerDoUpdatePostfix(ConsumerModule __instance, ModuleQuery q, bool initial, bool enabled, ModuleResult __result)
		{
			try
			{
				IModuleConfig config = __instance?.ModuleConfig;
				bool isDirtyBackroom = IsHumanOwnedDirtyCashBackroomModule(config, q.container, q.pid);
				bool isPlayerLegalBusiness = IsHumanOwnedPlayerLegalDirtyCashBusinessModule(config, q.container, q.pid);
				if (!isDirtyBackroom && !isPlayerLegalBusiness)
				{
					return;
				}

				string moduleId = __instance?.ModuleConfig?.Id.String;
				string logKey = BuildIllegalBackroomUpdateLogKey(moduleId, initial, enabled, __result);
				HashSet<string> updateLog = isPlayerLegalBusiness ? LoggedPlayerLegalBusinessConsumerUpdates : LoggedIllegalBackroomConsumerUpdates;
				if (string.IsNullOrEmpty(moduleId) || !updateLog.Add(logKey))
				{
					return;
				}

				string label = isPlayerLegalBusiness ? "Player legal business" : "Illegal backroom";
				Debug.Log("[GameplayTweaks] " + label + " consumer update observed id=" +
					moduleId +
					" qpid=" +
					q.pid +
					" buildingOwner=" +
					GetControllingPlayerString(q.container) +
					" initial=" +
					initial +
					" enabled=" +
					enabled +
					" result=" +
					__result);

				if (enabled && isDirtyBackroom)
				{
					RequestDeferredIllegalBackroomVisualRefresh(q.container, initial ? "consumer-enabled-initial" : "consumer-enabled");
				}

				LogDirtyCashConsumerIdleState(__instance, q, initial, enabled, __result, label, isPlayerLegalBusiness ? LoggedPlayerLegalBusinessConsumerIdleStates : LoggedIllegalBackroomConsumerIdleStates);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to log DirtyCash runtime consumer update: " + ex.Message);
			}
		}

		private static void LogDirtyCashConsumerIdleState(ConsumerModule module, ModuleQuery q, bool initial, bool enabled, ModuleResult result, string label, HashSet<string> idleLog)
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
			string logKey = moduleId + "|consumeDayz=" + consumeDays + "|daysUntilNext=" + daysUntilNext;
			if (idleLog == null || !idleLog.Add(logKey))
			{
				return;
			}

			Debug.Log("[GameplayTweaks] " + label + " consumer active but not due yet id=" +
				moduleId +
				" consumeDayz=" +
				consumeDays +
				" daysSinceLast=" +
				daysSinceLast +
				" daysUntilNext=" +
				daysUntilNext +
				" currentDay=" +
				currentDay +
				" lastUpdateDay=" +
				lastUpdateDay);
		}

		private static bool IsHumanOwnedIllegalBackroom(IModuleConfig config, Entity container, PlayerID pid)
		{
			if (!IsIllegalBackroomModule(config))
			{
				return false;
			}

			return pid.IsHumanPlayer || IsHumanOwnedBuilding(container);
		}

		private static bool IsHumanOwnedDirtyCashBackroomModule(IModuleConfig config, Entity container, PlayerID pid)
		{
			if (!IsDirtyCashBackroomModule(config))
			{
				return false;
			}

			return pid.IsHumanPlayer || IsHumanOwnedBuilding(container);
		}

		private static bool IsHumanOwnedDirtyCashRuntimeBypassModule(IModuleConfig config, Entity container, PlayerID pid)
		{
			if (!IsDirtyCashRuntimeBypassModule(config))
			{
				return false;
			}

			return pid.IsHumanPlayer || IsHumanOwnedBuilding(container);
		}

		private static bool IsHumanOwnedPlayerLegalDirtyCashBusinessModule(IModuleConfig config, Entity container, PlayerID pid)
		{
			if (!IsPlayerLegalDirtyCashBusinessModule(config))
			{
				return false;
			}

			return pid.IsHumanPlayer || IsHumanOwnedBuilding(container);
		}

		private static bool ShouldTrackIllegalBackroomDirtyCashMoney(IModuleConfig config, Entity container, PlayerID pid)
		{
			if (!ExternalDirtyCashEconomyDetected || !_externalDirtyCashOriginalOverridesRemoved)
			{
				return false;
			}

			return IsHumanOwnedIllegalBackroom(config, container, pid);
		}

		private static Stack<EntityID> GetIllegalBackroomDirtyCashMoneyContextStack()
		{
			if (_illegalBackroomDirtyCashMoneyContextStack == null)
			{
				_illegalBackroomDirtyCashMoneyContextStack = new Stack<EntityID>();
			}

			return _illegalBackroomDirtyCashMoneyContextStack;
		}

		private static void PushIllegalBackroomDirtyCashMoneyContext(EntityID buildingId)
		{
			GetIllegalBackroomDirtyCashMoneyContextStack().Push(buildingId);
		}

		private static void PopIllegalBackroomDirtyCashMoneyContext()
		{
			Stack<EntityID> stack = GetIllegalBackroomDirtyCashMoneyContextStack();
			if (stack.Count > 0)
			{
				stack.Pop();
			}
		}

		private static EntityID GetCurrentIllegalBackroomDirtyCashMoneyContext()
		{
			Stack<EntityID> stack = GetIllegalBackroomDirtyCashMoneyContextStack();
			if (stack.Count == 0)
			{
				return EntityID.INVALID;
			}

			return stack.Peek();
		}

		private static bool ShouldConvertIllegalBackroomBusinessIncome(PlayerFinances finances, Entity container, Price delta, MoneyReason reason)
		{
			if (!ExternalDirtyCashEconomyDetected || !_externalDirtyCashOriginalOverridesRemoved || finances == null || container == null)
			{
				return false;
			}

			if (reason != MoneyReason.BusinessIncome || !delta.IsPositive)
			{
				return false;
			}

			if (global::Game.Game.ctx?.players?.Human?.finances != finances)
			{
				return false;
			}

			EntityID contextBuildingId = GetCurrentIllegalBackroomDirtyCashMoneyContext();
			return contextBuildingId.IsValid && contextBuildingId == container.Id;
		}

		private static bool TryAddDirtyCashIncomeForIllegalBackroom(Entity container, Fixnum amount, EntityID target)
		{
			if (container == null || amount <= Fixnum.ZERO)
			{
				return false;
			}

			if (EnsureExternalDirtyCashAddIncomeMethod())
			{
				try
				{
					_externalDirtyCashAddIncomeMethod.Invoke(null, new object[]
					{
						container,
						amount,
						_externalDirtyCashBackroomReason,
						target
					});
					return true;
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] Failed to route illegal backroom income through DirtyCashEconomy manager: " + ex.Message);
				}
			}

			InventoryModule inventory = ModulesUtil.GetInventory(container);
			if (inventory?.data == null)
			{
				return false;
			}

			inventory.data.Increment((Label)"dirty-cash", amount);
			return true;
		}

		private static bool EnsureExternalDirtyCashAddIncomeMethod()
		{
			if (_externalDirtyCashAddIncomeMethod != null && _externalDirtyCashBackroomReason != null)
			{
				return true;
			}

			try
			{
				Type managerType = AccessTools.TypeByName("DirtyCashEconomy.DirtyCashManager");
				Type reasonType = AccessTools.TypeByName("DirtyCashEconomy.DirtyMoneyReason");
				if (managerType == null || reasonType == null)
				{
					return false;
				}

				MethodInfo addIncomeMethod = AccessTools.Method(managerType, "AddDirtyCashIncome", new Type[]
				{
					typeof(Entity),
					typeof(Fixnum),
					reasonType,
					typeof(EntityID)
				});
				if (addIncomeMethod == null)
				{
					addIncomeMethod = AccessTools.Method(managerType, "AddDirtyCashIncome");
				}
				if (addIncomeMethod == null)
				{
					return false;
				}

				_externalDirtyCashBackroomReason = Enum.Parse(reasonType, "Backroom");
				_externalDirtyCashAddIncomeMethod = addIncomeMethod;
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to bind DirtyCashEconomy income bridge: " + ex.Message);
				return false;
			}
		}

		private static void EnsureExternalDirtyCashOriginalOverridesRemoved()
		{
			if (_externalDirtyCashOriginalOverridesRemoved || !ExternalDirtyCashEconomyDetected || _compatHarmony == null)
			{
				return;
			}

			try
			{
				MethodInfo consumerDoConsumeAndPayMethod = AccessTools.Method(typeof(ConsumerModule), "DoConsumeAndPay", new Type[]
				{
					typeof(ModuleQuery),
					typeof(InventoryModule),
					typeof(Fixnum),
					typeof(int)
				});
				if (consumerDoConsumeAndPayMethod != null)
				{
					_compatHarmony.Unpatch(consumerDoConsumeAndPayMethod, HarmonyPatchType.Prefix, ExternalDirtyCashHarmonyId);
					_compatHarmony.Unpatch(consumerDoConsumeAndPayMethod, HarmonyPatchType.Postfix, ExternalDirtyCashHarmonyId);
				}

				MethodInfo manufactureDoConsumeAndProduceMethod = AccessTools.Method(typeof(ManufactureModule), "DoConsumeAndProduce", new Type[]
				{
					typeof(ModuleQuery),
					typeof(InventoryModule)
				});
				if (manufactureDoConsumeAndProduceMethod != null)
				{
					_compatHarmony.Unpatch(manufactureDoConsumeAndProduceMethod, HarmonyPatchType.Prefix, ExternalDirtyCashHarmonyId);
				}

				MethodInfo doChangeMoneyEntityMethod = AccessTools.Method(typeof(PlayerFinances), "DoChangeMoney", new Type[]
				{
					typeof(Entity),
					typeof(Price),
					typeof(MoneyReason),
					typeof(EntityID?)
				});
				if (doChangeMoneyEntityMethod != null)
				{
					_compatHarmony.Unpatch(doChangeMoneyEntityMethod, HarmonyPatchType.Prefix, ExternalDirtyCashHarmonyId);
				}

				_externalDirtyCashOriginalOverridesRemoved = true;
				if (!_loggedExternalDirtyCashOriginalOverrideRemoval)
				{
					_loggedExternalDirtyCashOriginalOverrideRemoval = true;
					Debug.Log("[GameplayTweaks] DirtyCashEconomy original consumer/manufacture income overrides detached for human illegal backrooms; GameplayTweaks bridge will route dirty cash on vanilla module flow.");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to detach DirtyCashEconomy original overrides: " + ex.Message);
			}
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
			if (slots == null || slots.Count == 0)
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
			if (slots == null || slots.Count == 0)
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

		private static void ClearRecordedDirtyCashAOEContribution(Entity building)
		{
			EntityID buildingId = building?.Id ?? EntityID.INVALID;
			if (!buildingId.IsValid || !RecordedDirtyCashAOEContributionsByBuilding.TryGetValue(buildingId, out List<DirtyCashAOEContributionEntry> contributions) || contributions == null)
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
				respect.fromAOE = (updated < 0) ? Fixnum.ZERO : updated;
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
							Delta = 0
						};
						aggregated[key] = entry;
					}

					entry.Delta += delta;
				}
			}

			if (aggregated.Count == 0)
			{
				return null;
			}

			return new List<DirtyCashAOEContributionEntry>(aggregated.Values);
		}

		private static List<Entity> GetHumanControlledDirtyCashBackroomBuildings()
		{
			List<Entity> results = new List<Entity>();
			IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
			if (buildings == null)
			{
				return results;
			}

			foreach (Entity building in buildings)
			{
				if (building == null || !IsHumanOwnedBuilding(building))
				{
					continue;
				}

				if (!HasInstalledDirtyCashRuntimeBypassModule(building.components?.modules))
				{
					continue;
				}

				results.Add(building);
			}

			return results;
		}

		private static bool TryGetControlledBuildingIdsForFallback(object territory, out IEnumerable<EntityID> entityIds)
		{
			entityIds = null;
			if (territory == null)
			{
				return false;
			}

			try
			{
				Type territoryType = territory.GetType();
				const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				object value = territoryType.GetMethod("GetAllControlledBuildingsUnsafe", flags)?.Invoke(territory, null);
				if (value == null)
				{
					value = territoryType.GetField("_buildings", flags)?.GetValue(territory) ??
						territoryType.GetField("buildings", flags)?.GetValue(territory) ??
						territoryType.GetProperty("OwnedBuildings", flags)?.GetValue(territory) ??
						territoryType.GetProperty("Buildings", flags)?.GetValue(territory);
				}

				entityIds = value as IEnumerable<EntityID>;
				return entityIds != null;
			}
			catch
			{
				entityIds = null;
				return false;
			}
		}

		private static void LogIllegalBackroomSafetySweepSummary(string source, SimTime now, bool initial, int candidateCount, int appliedCount, int observedCount, int alreadyUpdatedCount)
		{
			string logKey = source +
				"|day=" +
				now.days +
				"|initial=" +
				initial +
				"|candidates=" +
				candidateCount +
				"|applied=" +
				appliedCount +
				"|observed=" +
				observedCount +
				"|alreadyUpdated=" +
				alreadyUpdatedCount;
			if (!LoggedIllegalBackroomSafetySweepSummaries.Add(logKey))
			{
				return;
			}

			Debug.Log("[GameplayTweaks] DirtyCash runtime module safety sweep source=" +
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
				now.days);
		}

		private static string GetControllingPlayerString(Entity container)
		{
			if (container?.data?.building == null)
			{
				return "none";
			}

			return container.data.building.controlled.Get().ToString();
		}

		private static void FindModulesToAddForSlotPostfix(object __instance, ref List<AddModuleDef> __result)
		{
			try
			{
				if (__result == null || __result.Count == 0)
				{
					return;
				}

				object currentSlot = Traverse.Create(__instance).Property("Model").Field("currentSlot").GetValue<object>();
				if (currentSlot == null)
				{
					return;
				}

				IModule installedModule = Traverse.Create(currentSlot).Field("module").GetValue<IModule>();
				object slotdef = Traverse.Create(currentSlot).Field("slotdef").GetValue<object>();
				if (!IsBackroomSlot(slotdef))
				{
					return;
				}

				RemoveDisallowedBackroomChoices(__result);

				if (FindConflictingIllegalBackroom(FindCurrentBuilding(__instance)?.components?.modules, installedModule) != null)
				{
					RemoveIllegalBackroomChoices(__result);
					return;
				}

				if (installedModule != null)
				{
					return;
				}

				FilterDuplicateIllegalBackroomChoices(__result, currentFamily: null);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to filter illegal backroom add choices: " + ex.Message);
			}
		}

		private static void FindUpgradesOrNullPostfix(IModule module, ref List<AddModuleDef> __result)
		{
			try
			{
				if (__result == null || __result.Count == 0 || !IsIllegalBackroomModule(module?.ModuleConfig))
				{
					return;
				}

				RemoveDisallowedBackroomChoices(__result);
				if (__result.Count < 2)
				{
					return;
				}

				string currentFamily = NormalizeIllegalBackroomFamily(module.ModuleConfig.Id.String);
				FilterDuplicateIllegalBackroomChoices(__result, currentFamily);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to filter illegal backroom upgrades: " + ex.Message);
			}
		}

		private static void CanInstallPostfix(OwnedBizController __instance, AddModuleDef moduledef, ref bool __result)
		{
			if (!__result)
			{
				return;
			}

			try
			{
				if (ShouldBlockIllegalBackroomInstall(__instance, moduledef.config, out _))
				{
					__result = false;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to validate illegal backroom install availability: " + ex.Message);
			}
		}

		private static bool OnModuleAddConfirmPrefix(OwnedBizController __instance, AddModuleDef moduledef, Label previous, object slotdef)
		{
			try
			{
				if (!ShouldBlockIllegalBackroomInstall(__instance, moduledef.config, out IModule conflictingModule))
				{
					if (!IsIllegalBackroomModule(moduledef.config))
					{
						return true;
					}

					return HandleIllegalBackroomInstallConfirm(__instance, moduledef, previous, slotdef);
				}

				Debug.LogWarning("[GameplayTweaks] Blocked duplicate illegal backroom install selected=" +
					(moduledef.config?.Id.String ?? "null") +
					" existing=" +
					(conflictingModule?.ModuleConfig?.Id.String ?? "unknown"));
				OkPopup.ShowOk("This business already has an illegal backroom installed. Remove or upgrade the existing one first.", delegate
				{
				});
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to enforce illegal backroom install guard: " + ex.Message);
				return true;
			}
		}

		private static bool HandleIllegalBackroomInstallConfirm(OwnedBizController controller, AddModuleDef moduledef, Label previous, object slotdef)
		{
			Entity building = FindCurrentBuilding(controller);
			ModulesComponent modules = building?.components?.modules;
			string selectedId = moduledef.config?.Id.String ?? "null";
			string previousId = previous.IsSet ? previous.String : "none";
			if (modules == null)
			{
				Debug.LogWarning("[GameplayTweaks] Illegal backroom install failed: missing modules component selected=" + selectedId);
				return true;
			}

			if (!HasCompatibleInstallSlot(modules, moduledef.config, previous))
			{
				Debug.LogWarning("[GameplayTweaks] Illegal backroom install preflight failed selected=" +
					selectedId +
					" previous=" +
					previousId +
					" building=" +
					(building?.Id.ToString() ?? "null"));
				OkPopup.ShowOk("This illegal backroom could not be installed because no compatible backroom slot was available.", delegate
				{
				});
				return false;
			}

			if (previous.IsSet)
			{
				modules.RemoveModules(new List<Label> { previous }, shutdown: false);
				ModulesUtil.MakeModuleQuery(building).manager?.components.agent.IncrementStat(CrewStats.ExpansionsUpgradesBuilt, 1);
			}

			bool installed = modules.InstallModuleFromUI(moduledef.config.Id, controller.Model.visit.time);
			Debug.Log("[GameplayTweaks] Illegal backroom install attempt selected=" +
				selectedId +
				" previous=" +
				previousId +
				" installed=" +
				installed +
				" owner=" +
				GetControllingPlayerString(building));
			if (!installed)
			{
				OkPopup.ShowOk("This illegal backroom failed to install. No cost was charged.", delegate
				{
				});
				return false;
			}

			moduledef.config.Common.purchase.DoSpendAndConsume(controller.Model.visit.pid, building, MoneyReason.OwnedBizCosts);
			Traverse.Create(controller.View).Method("ControllerRequestsFullRefresh", new Type[] { typeof(bool) }).GetValue(true);
			Traverse.Create(controller.View).Method("ControllerRequestsModuleTab", new Type[] { typeof(ModuleSlot) }).GetValue(slotdef as ModuleSlot);
			return false;
		}

		private static bool ShouldBlockIllegalBackroomInstall(object controllerInstance, IModuleConfig config, out IModule conflictingModule)
		{
			conflictingModule = null;
			if (!IsIllegalBackroomModule(config))
			{
				return false;
			}

			Entity building = FindCurrentBuilding(controllerInstance);
			if (building == null)
			{
				return false;
			}

			IModule currentSlotModule = FindCurrentSlotModule(controllerInstance);
			conflictingModule = FindConflictingIllegalBackroom(building.components?.modules, currentSlotModule);
			return conflictingModule != null;
		}

		private static bool HasCompatibleInstallSlot(ModulesComponent modules, IModuleConfig config, Label previous)
		{
			if (modules?.Config?.slots == null || config == null)
			{
				return false;
			}

			List<IModule> installedSlots = modules.GetAllSlotsUnsafe();
			for (int i = 0; i < modules.Config.slots.Count; i++)
			{
				ModuleSlot candidate = modules.Config.slots[i];
				if (candidate == null || !candidate.CanSlotHouseThisModule(config))
				{
					continue;
				}

				IModule installed = (installedSlots != null && i < installedSlots.Count) ? installedSlots[i] : null;
				if (installed == null)
				{
					return true;
				}

				if (previous.IsSet && installed.ModuleData?.Id == previous)
				{
					return true;
				}
			}

			return false;
		}

		private static Entity FindCurrentBuilding(object controllerInstance)
		{
			object model = Traverse.Create(controllerInstance).Property("Model").GetValue<object>();
			if (model == null)
			{
				return null;
			}

			object visit = Traverse.Create(model).Field("visit").GetValue<object>();
			if (visit == null)
			{
				visit = Traverse.Create(model).Property("visit").GetValue<object>();
			}
			if (visit == null)
			{
				return null;
			}

			Entity building = Traverse.Create(visit).Field("building").GetValue<Entity>();
			if (building == null)
			{
				building = Traverse.Create(visit).Property("building").GetValue<Entity>();
			}
			return building;
		}

		private static IModule FindCurrentSlotModule(object controllerInstance)
		{
			object model = Traverse.Create(controllerInstance).Property("Model").GetValue<object>();
			if (model == null)
			{
				return null;
			}

			object currentSlot = Traverse.Create(model).Field("currentSlot").GetValue<object>();
			if (currentSlot == null)
			{
				return null;
			}

			return Traverse.Create(currentSlot).Field("module").GetValue<IModule>();
		}

		private static IModule FindConflictingIllegalBackroom(ModulesComponent modules, IModule currentSlotModule)
		{
			List<IModule> installedModules = modules?.GetAllSlotsUnsafe();
			if (installedModules == null)
			{
				return null;
			}

			for (int i = 0; i < installedModules.Count; i++)
			{
				IModule installedModule = installedModules[i];
				if (installedModule == null || ReferenceEquals(installedModule, currentSlotModule))
				{
					continue;
				}

				if (IsIllegalBackroomModule(installedModule.ModuleConfig))
				{
					return installedModule;
				}
			}

			return null;
		}

		private static void RemoveIllegalBackroomChoices(List<AddModuleDef> defs)
		{
			if (defs == null || defs.Count == 0)
			{
				return;
			}

			for (int i = defs.Count - 1; i >= 0; i--)
			{
				if (IsIllegalBackroomModule(defs[i].config))
				{
					defs.RemoveAt(i);
				}
			}
		}

		private static void RemoveDisallowedBackroomChoices(List<AddModuleDef> defs)
		{
			if (defs == null || defs.Count == 0)
			{
				return;
			}

			for (int i = defs.Count - 1; i >= 0; i--)
			{
				if (IsDisallowedBackroomChoice(defs[i].config))
				{
					defs.RemoveAt(i);
				}
			}
		}

		private static void FilterDuplicateIllegalBackroomChoices(List<AddModuleDef> defs, string currentFamily)
		{
			if (defs == null || defs.Count < 2)
			{
				return;
			}

			Dictionary<string, int> bestIndexByFamily = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			List<int> removeIndexes = new List<int>();

			for (int i = 0; i < defs.Count; i++)
			{
				IModuleConfig config = defs[i].config;
				if (!IsIllegalBackroomModule(config))
				{
					continue;
				}

				string id = config.Id.String;
				string family = NormalizeIllegalBackroomFamily(id);
				if (string.IsNullOrEmpty(family))
				{
					continue;
				}

				if (!string.IsNullOrEmpty(currentFamily) && !family.Equals(currentFamily, StringComparison.OrdinalIgnoreCase))
				{
					removeIndexes.Add(i);
					continue;
				}

				if (!bestIndexByFamily.TryGetValue(family, out int bestIndex))
				{
					bestIndexByFamily[family] = i;
					continue;
				}

				string bestId = defs[bestIndex].config?.Id.String ?? string.Empty;
				int currentScore = ScoreIllegalBackroomChoice(id, currentFamily);
				int bestScore = ScoreIllegalBackroomChoice(bestId, currentFamily);
				if (currentScore > bestScore || (currentScore == bestScore && string.CompareOrdinal(id, bestId) < 0))
				{
					removeIndexes.Add(bestIndex);
					bestIndexByFamily[family] = i;
				}
				else
				{
					removeIndexes.Add(i);
				}
			}

			if (removeIndexes.Count == 0)
			{
				return;
			}

			removeIndexes.Sort();
			for (int j = removeIndexes.Count - 1; j >= 0; j--)
			{
				int index = removeIndexes[j];
				if (index >= 0 && index < defs.Count)
				{
					defs.RemoveAt(index);
				}
			}
		}

		private static bool IsBackroomSlot(object slot)
		{
			if (slot == null)
			{
				return false;
			}

			TagList tags = Traverse.Create(slot).Field("tags").GetValue<TagList>();
			return tags != null && tags.Contains(TagConstants.TAG_SAFEHOUSE_BACKROOMS);
		}

		private static bool IsDisallowedBackroomChoice(IModuleConfig config)
		{
			if (config == null)
			{
				return false;
			}

			string id = config.Id.String;
			if (string.IsNullOrEmpty(id))
			{
				return false;
			}

			if (config is VehicleModuleConfig)
			{
				return id.StartsWith("player-garage", StringComparison.OrdinalIgnoreCase) ||
					id.StartsWith("player-truck-garage", StringComparison.OrdinalIgnoreCase) ||
					id.StartsWith("garage-", StringComparison.OrdinalIgnoreCase) ||
					id.StartsWith("truck-garage", StringComparison.OrdinalIgnoreCase);
			}

			return false;
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

		private static bool IsDirtyCashRuntimeBypassModule(IModuleConfig config)
		{
			return IsDirtyCashBackroomModule(config) || IsPlayerLegalDirtyCashBusinessModule(config);
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

		private static bool IsIllegalBackroomModule(IModuleConfig config)
		{
			if (!IsDirtyCashBackroomModule(config))
			{
				return false;
			}

			string id = config.Id.String;
			return !string.IsNullOrEmpty(id) &&
				!id.StartsWith("inventory-", StringComparison.OrdinalIgnoreCase);
		}

		private static string BuildIllegalBackroomUpdateLogKey(string moduleId, bool initial, bool enabled, ModuleResult result)
		{
			if (string.IsNullOrEmpty(moduleId))
			{
				return string.Empty;
			}

			return moduleId + "|initial=" + initial + "|enabled=" + enabled + "|result=" + result;
		}

		private static string BuildIllegalBackroomBuildingUpdateKey(EntityID buildingId, SimTime time, bool initial)
		{
			return buildingId + "|day=" + time.days + "|initial=" + initial;
		}

		private static string NormalizeIllegalBackroomFamily(string moduleId)
		{
			if (string.IsNullOrWhiteSpace(moduleId))
			{
				return string.Empty;
			}

			string value = moduleId.Trim().ToLowerInvariant();
			bool changed;
			do
			{
				changed = false;

				int upgradeIndex = value.IndexOf("-upgrade-", StringComparison.Ordinal);
				if (upgradeIndex > 0)
				{
					value = value.Substring(0, upgradeIndex);
					changed = true;
				}

				for (int i = 0; i < CitySuffixes.Length; i++)
				{
					string suffix = CitySuffixes[i];
					if (value.EndsWith(suffix, StringComparison.Ordinal))
					{
						value = value.Substring(0, value.Length - suffix.Length);
						changed = true;
						break;
					}
				}

				if (changed)
				{
					continue;
				}

				for (int j = 0; j < DuplicateChoiceSuffixes.Length; j++)
				{
					string suffix2 = DuplicateChoiceSuffixes[j];
					if (value.EndsWith(suffix2, StringComparison.Ordinal))
					{
						value = value.Substring(0, value.Length - suffix2.Length);
						changed = true;
						break;
					}
				}
			}
			while (changed);

			return value;
		}

		private static int ScoreIllegalBackroomChoice(string moduleId, string currentFamily)
		{
			if (string.IsNullOrWhiteSpace(moduleId))
			{
				return int.MinValue;
			}

			int score = 100;
			string id = moduleId.ToLowerInvariant();
			string family = NormalizeIllegalBackroomFamily(id);

			if (!string.IsNullOrEmpty(currentFamily) && family.Equals(currentFamily, StringComparison.OrdinalIgnoreCase))
			{
				score += 40;
			}
			if (HasAnySuffix(id, CitySuffixes))
			{
				score += 20;
			}
			if (id.Contains("-alpha"))
			{
				score -= 15;
			}
			if (id.Contains("-return"))
			{
				score -= 25;
			}
			if (id.Contains("-upgraded-upgrade") || id.Contains("-improved-upgrade"))
			{
				score -= 35;
			}
			else if (id.Contains("-upgraded") || id.Contains("-improved"))
			{
				score -= 20;
			}
			if (id.Contains("-upgrade-"))
			{
				score -= 30;
			}

			return score;
		}

		private static bool HasAnySuffix(string value, string[] suffixes)
		{
			for (int i = 0; i < suffixes.Length; i++)
			{
				if (value.EndsWith(suffixes[i], StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}
	}
}
}

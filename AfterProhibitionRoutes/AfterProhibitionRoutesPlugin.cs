using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using HarmonyLib;

namespace AfterProhibitionRoutes
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class AfterProhibitionRoutesPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.routes";
		public const string PluginName = "After Prohibition Routes";
		public const string PluginVersion = "0.8.0";
		public const string HarmonyId = PluginGuid;

		internal static ManualLogSource Log { get; private set; }

		internal static AfterProhibitionRoutesPlugin Instance { get; private set; }

		internal static ConfigEntry<bool> EnableRoutesBaselineLog { get; private set; }

		internal static ConfigEntry<bool> EnableStartupRouteStateAudit { get; private set; }

		internal static ConfigEntry<bool> EnableHumanTurnRouteStateAudit { get; private set; }

		internal static ConfigEntry<bool> EnableTravelContinuationDecisionBridge { get; private set; }

		internal static ConfigEntry<bool> EnableVehicleNodeAuthorityDecisionBridge { get; private set; }

		internal static ConfigEntry<bool> EnableDeliveryPumpDecisionBridge { get; private set; }

		internal static ConfigEntry<bool> EnableRouteSimAccessDecisionBridge { get; private set; }

		private Harmony _harmony;
		private bool _loggedRoutesBaseline;
		private bool _loggedStartupRouteStateAudit;
		private bool _loggedEmptyHumanTurnRouteStateAudit;
		private int _lastHumanTurnRouteStateAuditDay = int.MinValue;

		private const BindingFlags StaticFieldFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const int EmptyHumanTurnRouteStateAuditIntervalDays = 90;

		private void Awake()
		{
			Instance = this;
			Log = Logger;

			BindConfig();

			Logger.LogInfo("routes baseline scheduled ownsEconomy=False ownsUi=False ownsFamily=False ownsPolitics=False ownsCompatibility=False");

			_harmony = new Harmony(HarmonyId);
			ApplyHarmonyPatches();

			Logger.LogInfo($"{PluginName} {PluginVersion} loaded phase=gameplaytweaks-delegation ownsTravelContinuation=False ownsTravelContinuationDecision=True ownsVehicleNodeAuthority=False ownsVehicleNodeAuthorityDecision=True ownsDeliveryPump=False ownsDeliveryPumpDecision=True ownsRouteSimAccess=False ownsRouteSimAccessDecision=True ownsShopStaging=False ownsUi=False ownsEconomy=False ownsFamily=False ownsPolitics=False ownsCompatibility=False");
		}

		private IEnumerator Start()
		{
			yield return null;
			yield return new UnityEngine.WaitForSecondsRealtime(1f);
			LogRoutesBaseline("start-1s");
			LogStartupRouteStateAudit("start-1s");
		}

		private void OnDestroy()
		{
			try
			{
				_harmony?.UnpatchSelf();
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Could not unpatch Harmony hooks: " + ex.Message);
			}

			if (ReferenceEquals(Instance, this))
			{
				Instance = null;
			}
		}

		private void BindConfig()
		{
			EnableRoutesBaselineLog = Config.Bind(
				"Features",
				"EnableRoutesBaselineLog",
				true,
				"Logs a concise read-only routes baseline once shortly after startup.");

			EnableStartupRouteStateAudit = Config.Bind(
				"Features",
				"EnableStartupRouteStateAudit",
				true,
				"Logs read-only GameplayTweaks route state counts once shortly after startup. Does not mutate route state.");

			EnableHumanTurnRouteStateAudit = Config.Bind(
				"Features",
				"EnableHumanTurnRouteStateAudit",
				false,
				"Logs read-only GameplayTweaks route state counts at human turn start. Does not mutate route state.");

			EnableTravelContinuationDecisionBridge = Config.Bind(
				"Features",
				"EnableTravelContinuationDecisionBridge",
				true,
				"Exposes read-only travel-continuation decision bridge methods for GameplayTweaks. Does not resume routes or mutate vehicle state.");

			EnableVehicleNodeAuthorityDecisionBridge = Config.Bind(
				"Features",
				"EnableVehicleNodeAuthorityDecisionBridge",
				true,
				"Exposes read-only vehicle node authority decision bridge methods for GameplayTweaks. Does not record observed arrivals or mutate vehicle state.");

			EnableDeliveryPumpDecisionBridge = Config.Bind(
				"Features",
				"EnableDeliveryPumpDecisionBridge",
				true,
				"Exposes read-only delivery route pump decision bridge methods for GameplayTweaks. Does not process command queues, run automation, or mutate delivery state.");

			EnableRouteSimAccessDecisionBridge = Config.Bind(
				"Features",
				"EnableRouteSimAccessDecisionBridge",
				true,
				"Exposes read-only route-simulated destination access decision bridge methods for GameplayTweaks. Does not open conversations, stage shop orders, transfer goods, or mutate cash.");
		}

		private void ApplyHarmonyPatches()
		{
			if (!(EnableHumanTurnRouteStateAudit?.Value ?? false))
			{
				Logger.LogInfo("route-state-audit human-turn hook skipped reason=config-disabled");
				return;
			}

			try
			{
				MethodInfo onPlayerTurnStarted = AccessTools.Method(typeof(CommandExecutor), "OnPlayerTurnStarted");
				if (onPlayerTurnStarted != null)
				{
					_harmony.Patch(onPlayerTurnStarted, postfix: new HarmonyMethod(typeof(CommandExecutorRouteStateAuditPatch), nameof(CommandExecutorRouteStateAuditPatch.Postfix)));
					Logger.LogInfo("route-state-audit human-turn hook applied target=CommandExecutor.OnPlayerTurnStarted");
				}
				else
				{
					Logger.LogWarning("route-state-audit human-turn hook skipped reason=missing-CommandExecutor.OnPlayerTurnStarted");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("route-state-audit hook failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private void LogRoutesBaseline(string source)
		{
			if (_loggedRoutesBaseline || !EnableRoutesBaselineLog.Value)
			{
				return;
			}

			_loggedRoutesBaseline = true;
			Logger.LogInfo("routes-baseline source=" + source + " phase=gameplaytweaks-delegation ownsTravelContinuation=False ownsTravelContinuationDecision=True ownsVehicleNodeAuthority=False ownsVehicleNodeAuthorityDecision=True ownsDeliveryPump=False ownsDeliveryPumpDecision=True ownsRouteSimAccess=False ownsRouteSimAccessDecision=True ownsShopStaging=False ownsUi=False ownsEconomy=False ownsFamily=False ownsPolitics=False ownsCompatibility=False");
		}

		private void LogStartupRouteStateAudit(string source)
		{
			if (_loggedStartupRouteStateAudit || !EnableStartupRouteStateAudit.Value)
			{
				return;
			}

			_loggedStartupRouteStateAudit = true;
			LogRouteStateAudit(source);
		}

		internal void LogHumanTurnRouteStateAudit(CommandExecutor executor)
		{
			if (!EnableHumanTurnRouteStateAudit.Value || executor?.PID.IsHumanPlayer != true)
			{
				return;
			}

			int day = GetCurrentDay();
			if (_loggedEmptyHumanTurnRouteStateAudit
				&& _lastHumanTurnRouteStateAuditDay >= 0
				&& day >= 0
				&& day - _lastHumanTurnRouteStateAuditDay < EmptyHumanTurnRouteStateAuditIntervalDays)
			{
				return;
			}

			RouteStateAuditSnapshot snapshot = LogRouteStateAudit("human-turn-start");
			_lastHumanTurnRouteStateAuditDay = day;
			_loggedEmptyHumanTurnRouteStateAudit = snapshot != null && snapshot.IsEmpty;
		}

		private RouteStateAuditSnapshot LogRouteStateAudit(string source)
		{
			RouteStateAuditSnapshot snapshot = CaptureRouteStateAudit();
			Logger.LogInfo(
				"route-state-audit source=" + source +
				" gameplayTweaks=" + snapshot.GameplayTweaksLoaded +
				" helper=" + snapshot.HelperReadable +
				" pendingVehicles=" + snapshot.PendingVehicles +
				" activeVehicles=" + snapshot.ActiveVehicles +
				" queuedResume=" + snapshot.QueuedResumeVehicles +
				" recentRequeues=" + snapshot.RecentRequeues +
				" turnStartFinalized=" + snapshot.TurnStartFinalized +
				" turnStartDeferred=" + snapshot.TurnStartDeferred +
				" deliveryPumpGuards=" + snapshot.DeliveryPumpGuards +
				" deliveryPumpGuardFrames=" + snapshot.DeliveryPumpGuardFrames +
				" deliveryPumpGuardTargets=" + snapshot.DeliveryPumpGuardTargets +
				" manualDeliveryStops=" + snapshot.ManualDeliveryStops +
				" stagedShopOrderVehicles=" + snapshot.StagedShopOrderVehicles +
				" stagedShopOrders=" + snapshot.StagedShopOrders +
				" routeInConversations=" + snapshot.RouteInConversations +
				" openShopPickers=" + snapshot.OpenShopPickers +
				" routeModeLabelCandidates=" + snapshot.RouteModeLabelCandidates +
				" missing=\"" + snapshot.MissingSummary + "\"" +
				" reason=" + snapshot.Reason);
			return snapshot;
		}

		private static int GetCurrentDay()
		{
			try
			{
				return global::Game.Game.ctx?.clock?.Now.days ?? -1;
			}
			catch
			{
				return -1;
			}
		}

		private static RouteStateAuditSnapshot CaptureRouteStateAudit()
		{
			RouteStateAuditSnapshot snapshot = new RouteStateAuditSnapshot();
			Type helperType = FindType("GameplayTweaks.MultiCrewVehicleHelper");
			Type travelEndType = FindType("GameplayTweaks.HumanVehicleTravelEndSyncPatch");
			Type shopStateType = FindType("GameplayTweaks.RouteShopStagingState");

			snapshot.GameplayTweaksLoaded = helperType != null || travelEndType != null || shopStateType != null || FindAssembly("GameplayTweaks") != null;
			snapshot.HelperReadable = helperType != null;

			if (helperType == null)
			{
				snapshot.Missing.Add("GameplayTweaks.MultiCrewVehicleHelper");
			}
			else
			{
				object pending = GetStaticFieldValue(helperType, "_pendingVehicleTravelByVehicleId", snapshot);
				object active = GetStaticFieldValue(helperType, "_activeHumanVehicleTravel", snapshot);

				snapshot.PendingVehicles = CountItems(pending);
				snapshot.ActiveVehicles = CountItems(active);
				snapshot.QueuedResumeVehicles = CountItemsWithBooleanField(pending, "ResumeQueued");
				snapshot.RecentRequeues = CountItems(GetStaticFieldValue(helperType, "_recentHumanVehicleRequeueByVehicleId", snapshot));
				snapshot.TurnStartFinalized = CountItems(GetStaticFieldValue(helperType, "_turnStartFinalizedQueuedRouteVehicleIds", snapshot));
				snapshot.TurnStartDeferred = CountItems(GetStaticFieldValue(helperType, "_turnStartDeferredArrivalVehicleIds", snapshot));
				snapshot.RouteModeLabelCandidates = snapshot.PendingVehicles;
			}

			if (travelEndType == null)
			{
				snapshot.Missing.Add("GameplayTweaks.HumanVehicleTravelEndSyncPatch");
			}
			else
			{
				snapshot.DeliveryPumpGuardFrames = CountItems(GetStaticFieldValue(travelEndType, "DeliveryPumpSegmentStartFrameByVehicleId", snapshot));
				snapshot.DeliveryPumpGuardTargets = CountItems(GetStaticFieldValue(travelEndType, "DeliveryPumpSegmentTargetByVehicleId", snapshot));
				snapshot.ManualDeliveryStops = CountItems(GetStaticFieldValue(travelEndType, "ManualDeliveryStopByVehicleId", snapshot));
				snapshot.DeliveryPumpGuards = Math.Max(snapshot.DeliveryPumpGuardFrames, snapshot.DeliveryPumpGuardTargets);
			}

			if (shopStateType == null)
			{
				snapshot.Missing.Add("GameplayTweaks.RouteShopStagingState");
			}
			else
			{
				object ordersByVehicle = GetStaticFieldValue(shopStateType, "_ordersByVehicleId", snapshot);
				snapshot.StagedShopOrderVehicles = CountItems(ordersByVehicle);
				snapshot.StagedShopOrders = CountNestedCollectionItems(ordersByVehicle);
				snapshot.RouteInConversations = CountItems(GetStaticFieldValue(shopStateType, "_recentRouteInConversationByVehicleId", snapshot));
				snapshot.OpenShopPickers = CountItems(GetStaticFieldValue(shopStateType, "_openPickerByVehicleId", snapshot));
			}

			snapshot.Reason = snapshot.GameplayTweaksLoaded && snapshot.HelperReadable
				? (snapshot.Missing.Count == 0 ? "readable" : "partial")
				: "gameplaytweaks-route-state-missing";
			return snapshot;
		}

		public static string GetRouteStateSummary()
		{
			RouteStateAuditSnapshot snapshot = CaptureRouteStateAudit();
			return "route-state-summary" +
				" version=" + PluginVersion +
				" gameplayTweaks=" + snapshot.GameplayTweaksLoaded +
				" helper=" + snapshot.HelperReadable +
				" pendingVehicles=" + snapshot.PendingVehicles +
				" activeVehicles=" + snapshot.ActiveVehicles +
				" queuedResume=" + snapshot.QueuedResumeVehicles +
				" deliveryPumpGuards=" + snapshot.DeliveryPumpGuards +
				" stagedShopOrders=" + snapshot.StagedShopOrders +
				" routeModeLabelCandidates=" + snapshot.RouteModeLabelCandidates +
				" reason=" + snapshot.Reason +
				" missing=\"" + snapshot.MissingSummary + "\"";
		}

		public static RouteAuthorityClassification ClassifyVehicleRouteAuthority(EntityID vehicleId)
		{
			return ClassifyVehicleRouteAuthority(vehicleId, NodeID.INVALID, "none");
		}

		public static RouteAuthorityClassification ClassifyVehicleRouteAuthority(EntityID vehicleId, NodeID queryNodeId)
		{
			return ClassifyVehicleRouteAuthority(vehicleId, queryNodeId, "none");
		}

		public static RouteAuthorityClassification ClassifyVehicleRouteAuthority(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			RouteAuthorityClassification result = new RouteAuthorityClassification
			{
				VehicleID = vehicleId,
				QueryNodeID = queryNodeId,
				ActionType = string.IsNullOrWhiteSpace(actionType) ? "none" : actionType.Trim(),
				RouteModeLabel = "Unknown",
				Reason = "unread"
			};

			Type helperType = FindType("GameplayTweaks.MultiCrewVehicleHelper");
			result.GameplayTweaksLoaded = helperType != null || FindAssembly("GameplayTweaks") != null;
			result.HelperReadable = helperType != null;
			if (helperType == null)
			{
				result.Reason = "gameplaytweaks-route-state-missing";
				result.Missing = "GameplayTweaks.MultiCrewVehicleHelper";
				return result;
			}

			List<string> missing = new List<string>();
			object pendingByVehicle = GetStaticFieldValue(helperType, "_pendingVehicleTravelByVehicleId", missing);
			object activeVehicles = GetStaticFieldValue(helperType, "_activeHumanVehicleTravel", missing);
			object finalizedVehicles = GetStaticFieldValue(helperType, "_turnStartFinalizedQueuedRouteVehicleIds", missing);
			object deferredVehicles = GetStaticFieldValue(helperType, "_turnStartDeferredArrivalVehicleIds", missing);

			result.ActiveTravel = ContainsLong(activeVehicles, unchecked((long)vehicleId.id));
			result.TurnStartFinalized = ContainsLong(finalizedVehicles, unchecked((long)vehicleId.id));
			result.TurnStartDeferred = ContainsLong(deferredVehicles, unchecked((long)vehicleId.id));

			if (TryGetPendingRouteInfo(pendingByVehicle, vehicleId, out PendingRouteInfo pending))
			{
				result.HasPendingRoute = true;
				result.PeepID = pending.PeepID;
				result.StartNodeID = pending.StartNodeID;
				result.ExpectedNodeID = pending.ExpectedNodeID;
				result.GoalNodeID = pending.GoalNodeID;
				result.ResumeQueued = pending.ResumeQueued;
				result.QueuedResume = pending.ResumeQueued && pending.GoalNodeID.IsValid && !result.ActiveTravel;
				result.HasExpectedNode = pending.ExpectedNodeID.IsValid;
				result.HasGoalNode = pending.GoalNodeID.IsValid;
			}

			if (TryGetLiveVehicleNodeId(vehicleId, out NodeID liveNodeId, out string liveNodeSource))
			{
				result.VehicleFound = true;
				result.HasPhysicalNode = liveNodeId.IsValid;
				result.PhysicalNodeID = liveNodeId;
				result.PhysicalNodeSource = liveNodeSource;
			}
			else
			{
				result.VehicleFound = vehicleId.IsValid && vehicleId.FindEntity() != null;
				result.PhysicalNodeSource = "none";
			}

			if (queryNodeId.IsValid)
			{
				result.PhysicalAtQueryNode = result.HasPhysicalNode && result.PhysicalNodeID == queryNodeId;
				result.QueryMatchesExpectedNode = result.HasExpectedNode && result.ExpectedNodeID == queryNodeId;
				result.QueryMatchesGoalNode = result.HasGoalNode && result.GoalNodeID == queryNodeId;
			}

			result.RouteModeLabel = ResolveRouteModeLabel(result);
			result.RouteSimConvenienceAllowed = ClassifyRouteSimConvenience(result, out string routeSimReason);
			result.RouteSimReason = routeSimReason;
			result.Missing = missing.Count == 0 ? "none" : string.Join(",", missing.ToArray());
			result.Reason = result.GameplayTweaksLoaded && result.HelperReadable
				? (missing.Count == 0 ? "readable" : "partial")
				: "gameplaytweaks-route-state-missing";
			return result;
		}

		public static string ClassifyVehicleRouteAuthoritySummary(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			return ClassifyVehicleRouteAuthority(vehicleId, queryNodeId, actionType).ToLogString();
		}

		public static bool OwnsTravelContinuationDecision()
		{
			return Instance != null && (EnableTravelContinuationDecisionBridge?.Value ?? false);
		}

		public static bool OwnsVehicleNodeAuthorityDecision()
		{
			return Instance != null && (EnableVehicleNodeAuthorityDecisionBridge?.Value ?? false);
		}

		public static bool OwnsDeliveryPumpDecision()
		{
			return Instance != null && (EnableDeliveryPumpDecisionBridge?.Value ?? false);
		}

		public static bool OwnsRouteSimAccessDecision()
		{
			return Instance != null && (EnableRouteSimAccessDecisionBridge?.Value ?? false);
		}

		public static RouteSimAccessDecision ClassifyRouteSimAccess(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			RouteSimAccessDecision decision = new RouteSimAccessDecision
			{
				VehicleID = vehicleId,
				QueryNodeID = queryNodeId,
				ActionType = string.IsNullOrWhiteSpace(actionType) ? "none" : actionType.Trim(),
				Decision = "fallback",
				Reason = "bridge-disabled",
				OwnsDecision = OwnsRouteSimAccessDecision(),
				AllowGameplayTweaksFallback = true
			};

			if (!decision.OwnsDecision)
			{
				return decision;
			}

			RouteAuthorityClassification authority = ClassifyVehicleRouteAuthority(vehicleId, queryNodeId, "route-sim-" + decision.ActionType);
			decision.GameplayTweaksLoaded = authority.GameplayTweaksLoaded;
			decision.HelperReadable = authority.HelperReadable;
			decision.VehicleFound = authority.VehicleFound;
			decision.PhysicalNodeID = authority.PhysicalNodeID;
			decision.PhysicalNodeSource = authority.PhysicalNodeSource ?? "none";
			decision.StartNodeID = authority.StartNodeID;
			decision.ExpectedNodeID = authority.ExpectedNodeID;
			decision.GoalNodeID = authority.GoalNodeID;
			decision.ActiveTravel = authority.ActiveTravel;
			decision.ResumeQueued = authority.ResumeQueued;
			decision.QueuedResume = authority.QueuedResume;
			decision.HasPendingRoute = authority.HasPendingRoute;
			decision.QueryMatchesExpectedNode = authority.QueryMatchesExpectedNode;
			decision.QueryMatchesGoalNode = authority.QueryMatchesGoalNode;
			decision.RouteModeLabel = authority.RouteModeLabel ?? "Unknown";
			decision.Missing = authority.Missing ?? "none";
			decision.PhysicalOnlyAction = IsPhysicalOnlyAction(decision.ActionType);
			decision.RouteSimConvenienceAllowed = authority.RouteSimConvenienceAllowed;
			decision.RouteSimReason = authority.RouteSimReason ?? "none";

			CaptureRouteShopStagingState(vehicleId, decision);

			if (!authority.GameplayTweaksLoaded || !authority.HelperReadable)
			{
				decision.Decision = "fallback";
				decision.Reason = "gameplaytweaks-state-unreadable";
				return decision;
			}

			if (!queryNodeId.IsValid)
			{
				decision.Decision = "block";
				decision.Reason = "no-query-node";
				return decision;
			}

			if (decision.PhysicalOnlyAction)
			{
				decision.Decision = "block";
				decision.Reason = "physical-only-action";
				return decision;
			}

			if (!authority.HasPendingRoute)
			{
				decision.Decision = "block";
				decision.Reason = "no-pending-route";
				return decision;
			}

			if (authority.QueryMatchesExpectedNode)
			{
				decision.Decision = "allow";
				decision.Reason = "route-sim-expected";
				decision.AccessSource = "route-sim-expected";
				return decision;
			}

			if (authority.QueryMatchesGoalNode)
			{
				if (IsRouteSimExpectedOnlyAction(decision.ActionType))
				{
					decision.Decision = "block";
					decision.Reason = "expected-node-only";
					return decision;
				}

				decision.Decision = "allow";
				decision.Reason = "route-sim-goal";
				decision.AccessSource = "route-sim-goal";
				return decision;
			}

			decision.Decision = "block";
			decision.Reason = "wrong-route-destination";
			return decision;
		}

		public static string ClassifyRouteSimAccessSummary(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			return ClassifyRouteSimAccess(vehicleId, queryNodeId, actionType).ToLogString();
		}

		public static string LogRouteSimAccessDecision(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			RouteSimAccessDecision decision = ClassifyRouteSimAccess(vehicleId, queryNodeId, actionType);
			string summary = decision.ToLogString();
			Log?.LogInfo(summary);
			return summary;
		}

		public static DeliveryPumpDecision ClassifyDeliveryPump(EntityID vehicleId, NodeID finalNodeId, string source, int queuePumps, int automationPumps)
		{
			DeliveryPumpDecision decision = new DeliveryPumpDecision
			{
				VehicleID = vehicleId,
				FinalNodeID = finalNodeId,
				Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim(),
				Decision = "fallback",
				Reason = "bridge-disabled",
				OwnsDecision = OwnsDeliveryPumpDecision(),
				AllowGameplayTweaksFallback = true,
				QueuePumps = queuePumps,
				AutomationPumps = automationPumps
			};

			if (!decision.OwnsDecision)
			{
				return decision;
			}

			RouteAuthorityClassification authority = ClassifyVehicleRouteAuthority(vehicleId, finalNodeId, "delivery-pump");
			decision.GameplayTweaksLoaded = authority.GameplayTweaksLoaded;
			decision.HelperReadable = authority.HelperReadable;
			decision.VehicleFound = authority.VehicleFound;
			decision.PhysicalNodeID = authority.PhysicalNodeID;
			decision.PhysicalNodeSource = authority.PhysicalNodeSource ?? "none";
			decision.ExpectedNodeID = authority.ExpectedNodeID;
			decision.GoalNodeID = authority.GoalNodeID;
			decision.ActiveTravel = authority.ActiveTravel;
			decision.ResumeQueued = authority.ResumeQueued;
			decision.QueuedResume = authority.QueuedResume;
			decision.RouteModeLabel = authority.RouteModeLabel ?? "Unknown";
			decision.Missing = authority.Missing ?? "none";

			if (!authority.GameplayTweaksLoaded || !authority.HelperReadable)
			{
				decision.Decision = "fallback";
				decision.Reason = "gameplaytweaks-state-unreadable";
				return decision;
			}

			CaptureDeliveryPumpGuardState(vehicleId, decision);

			if (decision.HasManualStop && (!decision.ManualStopNodeID.IsValid || !finalNodeId.IsValid || decision.ManualStopNodeID == finalNodeId))
			{
				decision.Decision = "hold";
				decision.Reason = "manual-stop-suppression";
				return decision;
			}

			if (authority.ActiveTravel)
			{
				decision.Decision = "defer";
				decision.Reason = "active-route";
				return decision;
			}

			if (authority.QueuedResume || authority.ResumeQueued)
			{
				decision.Decision = "defer";
				decision.Reason = "queued-resume";
				return decision;
			}

			if (decision.HasGuardTarget && decision.GuardTargetNodeID.IsValid && finalNodeId.IsValid && decision.GuardTargetNodeID != finalNodeId)
			{
				decision.Decision = "defer";
				decision.Reason = "await-guard-target";
				return decision;
			}

			if (automationPumps > 0)
			{
				decision.Decision = "stop";
				decision.Reason = "one-automation-step-budget";
				return decision;
			}

			if (queuePumps > 0)
			{
				decision.Decision = "allow";
				decision.Reason = "continue-after-queue-pump";
				return decision;
			}

			decision.Decision = "allow";
			decision.Reason = "pump-safe";
			return decision;
		}

		public static string ClassifyDeliveryPumpSummary(EntityID vehicleId, NodeID finalNodeId, string source, int queuePumps, int automationPumps)
		{
			return ClassifyDeliveryPump(vehicleId, finalNodeId, source, queuePumps, automationPumps).ToLogString();
		}

		public static string LogDeliveryPumpDecision(EntityID vehicleId, NodeID finalNodeId, string source, int queuePumps, int automationPumps)
		{
			DeliveryPumpDecision decision = ClassifyDeliveryPump(vehicleId, finalNodeId, source, queuePumps, automationPumps);
			string summary = decision.ToLogString();
			Log?.LogInfo(summary);
			return summary;
		}

		public static VehicleNodeAuthorityDecision ClassifyVehicleNodeAuthority(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			VehicleNodeAuthorityDecision decision = new VehicleNodeAuthorityDecision
			{
				VehicleID = vehicleId,
				QueryNodeID = queryNodeId,
				ActionType = string.IsNullOrWhiteSpace(actionType) ? "none" : actionType.Trim(),
				Decision = "fallback",
				Reason = "bridge-disabled",
				OwnsDecision = OwnsVehicleNodeAuthorityDecision(),
				AllowGameplayTweaksFallback = true,
				ObservedFrame = -1
			};

			if (!decision.OwnsDecision)
			{
				return decision;
			}

			RouteAuthorityClassification authority = ClassifyVehicleRouteAuthority(vehicleId, queryNodeId, "node-authority-" + decision.ActionType);
			decision.PhysicalNodeID = authority.PhysicalNodeID;
			decision.PhysicalNodeSource = authority.PhysicalNodeSource ?? "none";
			decision.StartNodeID = authority.StartNodeID;
			decision.ExpectedNodeID = authority.ExpectedNodeID;
			decision.GoalNodeID = authority.GoalNodeID;
			decision.RouteModeLabel = authority.RouteModeLabel ?? "Unknown";
			decision.Missing = authority.Missing ?? "none";
			decision.GameplayTweaksLoaded = authority.GameplayTweaksLoaded;
			decision.HelperReadable = authority.HelperReadable;
			decision.VehicleFound = authority.VehicleFound;
			decision.HasPendingRoute = authority.HasPendingRoute;
			decision.ActiveTravel = authority.ActiveTravel;
			decision.ResumeQueued = authority.ResumeQueued;
			decision.QueuedResume = authority.QueuedResume;
			decision.PhysicalAtQueryNode = authority.PhysicalAtQueryNode;
			decision.QueryMatchesExpectedNode = authority.QueryMatchesExpectedNode;
			decision.QueryMatchesGoalNode = authority.QueryMatchesGoalNode;

			if (!vehicleId.IsValid)
			{
				decision.Decision = "fallback";
				decision.Reason = "invalid-vehicle";
				return decision;
			}

			if (TryGetStrictPhysicalVehicleNodeId(vehicleId, out NodeID strictPhysicalNodeId, out string strictPhysicalSource))
			{
				decision.StrictPhysicalNodeID = strictPhysicalNodeId;
				decision.StrictPhysicalSource = strictPhysicalSource;
				decision.StrictPhysicalAtQueryNode = queryNodeId.IsValid && strictPhysicalNodeId == queryNodeId;
			}
			else
			{
				decision.StrictPhysicalSource = "none";
			}

			if (TryGetObservedReachedInfo(vehicleId, out NodeID observedNodeId, out int observedFrame, out string observedSource))
			{
				decision.ObservedNodeID = observedNodeId;
				decision.ObservedFrame = observedFrame;
				decision.ObservedSource = observedSource;
				decision.ObservedAtQueryNode = queryNodeId.IsValid && observedNodeId == queryNodeId;
				decision.ObservedExpectedArrival = authority.HasExpectedNode && observedNodeId == authority.ExpectedNodeID;
			}
			else
			{
				decision.ObservedSource = "none";
			}

			decision.PhysicalOnlyAction = IsPhysicalOnlyAction(decision.ActionType);
			decision.StaleLogicalNodeRejected = queryNodeId.IsValid
				&& !decision.StrictPhysicalAtQueryNode
				&& !decision.PhysicalAtQueryNode
				&& (decision.QueryMatchesExpectedNode || decision.QueryMatchesGoalNode);

			if (decision.StrictPhysicalAtQueryNode)
			{
				decision.Decision = "allow";
				decision.Reason = "strict-physical";
				return decision;
			}

			if (decision.PhysicalAtQueryNode && !decision.HasPendingRoute)
			{
				decision.Decision = "allow";
				decision.Reason = "settled-live-node";
				return decision;
			}

			if (decision.PhysicalOnlyAction && queryNodeId.IsValid && !decision.StrictPhysicalAtQueryNode)
			{
				decision.Decision = "reject";
				decision.Reason = decision.StaleLogicalNodeRejected ? "stale-logical-node-physical-only" : "not-strict-physical";
				return decision;
			}

			if (decision.QueryMatchesExpectedNode && decision.ActiveTravel)
			{
				if (decision.ObservedExpectedArrival || decision.PhysicalAtQueryNode)
				{
					decision.Decision = "finalize";
					decision.Reason = decision.ObservedExpectedArrival ? "observed-expected-arrival" : "live-expected-node";
					return decision;
				}

				decision.Decision = "defer";
				decision.Reason = "await-observed-arrival";
				return decision;
			}

			if (decision.StaleLogicalNodeRejected)
			{
				decision.Decision = "reject";
				decision.Reason = "stale-logical-node";
				return decision;
			}

			if (decision.PhysicalAtQueryNode)
			{
				decision.Decision = "allow";
				decision.Reason = "live-authority-node";
				return decision;
			}

			decision.Decision = "fallback";
			decision.Reason = authority.Reason ?? "unclassified-node-authority";
			return decision;
		}

		public static string ClassifyVehicleNodeAuthoritySummary(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			return ClassifyVehicleNodeAuthority(vehicleId, queryNodeId, actionType).ToLogString();
		}

		public static string LogVehicleNodeAuthorityDecision(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			VehicleNodeAuthorityDecision decision = ClassifyVehicleNodeAuthority(vehicleId, queryNodeId, actionType);
			string summary = decision.ToLogString();
			Log?.LogInfo(summary);
			return summary;
		}

		public static TravelContinuationDecision ClassifyTravelContinuation(EntityID vehicleId)
		{
			TravelContinuationDecision decision = new TravelContinuationDecision
			{
				VehicleID = vehicleId,
				Decision = "fallback",
				Reason = "bridge-disabled",
				AllowGameplayTweaksFallback = true,
				OwnsDecision = OwnsTravelContinuationDecision()
			};

			if (!decision.OwnsDecision)
			{
				return decision;
			}

			RouteAuthorityClassification authority = ClassifyVehicleRouteAuthority(vehicleId, NodeID.INVALID, "travel-continuation");
			decision.RouteModeLabel = authority.RouteModeLabel ?? "Unknown";
			decision.PhysicalNodeID = authority.PhysicalNodeID;
			decision.PhysicalNodeSource = authority.PhysicalNodeSource ?? "none";
			decision.StartNodeID = authority.StartNodeID;
			decision.ExpectedNodeID = authority.ExpectedNodeID;
			decision.GoalNodeID = authority.GoalNodeID;
			decision.HasPendingRoute = authority.HasPendingRoute;
			decision.ActiveTravel = authority.ActiveTravel;
			decision.ResumeQueued = authority.ResumeQueued;
			decision.QueuedResume = authority.QueuedResume;
			decision.TurnStartFinalized = authority.TurnStartFinalized;
			decision.TurnStartDeferred = authority.TurnStartDeferred;
			decision.Reason = authority.Reason ?? "unread";
			decision.Missing = authority.Missing ?? "none";

			if (!authority.GameplayTweaksLoaded || !authority.HelperReadable)
			{
				decision.Decision = "fallback";
				decision.Reason = "gameplaytweaks-state-unreadable";
				return decision;
			}

			if (!authority.HasPendingRoute)
			{
				decision.Decision = "ignore";
				decision.Reason = "no-pending-route";
				return decision;
			}

			if (authority.ActiveTravel && authority.HasExpectedNode && !authority.TurnStartDeferred)
			{
				decision.Decision = "defer";
				decision.Reason = "await-observed-arrival";
				return decision;
			}

			if (authority.TurnStartDeferred)
			{
				decision.Decision = "defer";
				decision.Reason = "turn-start-deferred-arrival";
				return decision;
			}

			if (authority.QueuedResume && authority.HasGoalNode)
			{
				decision.Decision = "resume";
				decision.Reason = authority.TurnStartFinalized ? "turn-start-finalized" : "queued-resume";
				return decision;
			}

			if (authority.ResumeQueued && authority.HasGoalNode)
			{
				decision.Decision = "resume";
				decision.Reason = "resume-queued";
				return decision;
			}

			if (authority.HasGoalNode && authority.PhysicalNodeID.IsValid && authority.PhysicalNodeID == authority.GoalNodeID)
			{
				decision.Decision = "clear-arrived";
				decision.Reason = "already-at-final-goal";
				return decision;
			}

			decision.Decision = "fallback";
			decision.Reason = "unclassified-route-state";
			return decision;
		}

		public static string ClassifyTravelContinuationSummary(EntityID vehicleId)
		{
			return ClassifyTravelContinuation(vehicleId).ToLogString();
		}

		public static string LogTravelContinuationDecision(EntityID vehicleId, string source)
		{
			TravelContinuationDecision decision = ClassifyTravelContinuation(vehicleId);
			string summary = decision.ToLogString() + " source=" + (string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim());
			Log?.LogInfo(summary);
			return summary;
		}

		private static string ResolveRouteModeLabel(RouteAuthorityClassification classification)
		{
			if (classification.HasPendingRoute
				&& (classification.ActiveTravel
					|| classification.QueuedResume
					|| classification.HasExpectedNode
					|| classification.HasGoalNode))
			{
				if (classification.ActiveTravel
					&& classification.HasExpectedNode
					&& classification.HasGoalNode
					&& classification.ExpectedNodeID == classification.GoalNodeID
					&& !classification.ResumeQueued)
				{
					return "Arriving";
				}

				return "In route";
			}

			return classification.HasPhysicalNode ? "At corner" : "Unknown";
		}

		private static bool ClassifyRouteSimConvenience(RouteAuthorityClassification classification, out string reason)
		{
			if (!classification.QueryNodeID.IsValid)
			{
				reason = "no-query-node";
				return false;
			}

			if (classification.PhysicalAtQueryNode)
			{
				reason = "physical-node";
				return false;
			}

			if (!classification.HasPendingRoute || !classification.HasGoalNode)
			{
				reason = "no-pending-route-goal";
				return false;
			}

			if (!classification.QueryMatchesGoalNode)
			{
				reason = "query-not-final-goal";
				return false;
			}

			if (!classification.ActiveTravel && !classification.QueuedResume && !classification.HasExpectedNode)
			{
				reason = "route-not-active-or-queued";
				return false;
			}

			string action = classification.ActionType ?? string.Empty;
			if (action.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("combat", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("hostile", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("module", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("commit", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				reason = "physical-only-action";
				return false;
			}

			if (action.IndexOf("convo", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("conversation", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0
				|| action.IndexOf("biz", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				reason = "route-final-goal-convenience";
				return true;
			}

			reason = "action-not-route-sim";
			return false;
		}

		private static bool TryGetPendingRouteInfo(object pendingByVehicle, EntityID vehicleId, out PendingRouteInfo pending)
		{
			pending = default;
			if (!vehicleId.IsValid || pendingByVehicle == null)
			{
				return false;
			}

			long vehicleKey = unchecked((long)vehicleId.id);
			foreach (object value in EnumerateDictionaryValuesForKey(pendingByVehicle, vehicleKey))
			{
				if (value == null)
				{
					continue;
				}

				if (!TryReadField(value, "VehicleID", out EntityID pendingVehicleId) || pendingVehicleId != vehicleId)
				{
					continue;
				}

				TryReadField(value, "PeepID", out pending.PeepID);
				TryReadField(value, "StartNodeID", out pending.StartNodeID);
				TryReadField(value, "ExpectedNodeID", out pending.ExpectedNodeID);
				TryReadField(value, "GoalNodeID", out pending.GoalNodeID);
				TryReadField(value, "ResumeQueued", out pending.ResumeQueued);
				return true;
			}

			return false;
		}

		private static bool TryGetLiveVehicleNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			Entity vehicle = vehicleId.IsValid ? vehicleId.FindEntity() : null;
			if (vehicle == null)
			{
				return false;
			}

			NodeID mobileNodeId = vehicle.components?.mobile?.FindNodeNearThisMobile() ?? NodeID.INVALID;
			NodeID boardNodeId = vehicle.data?.board?.bead.nodeId ?? NodeID.INVALID;
			NodeID agentNodeId = vehicle.data?.agent?.nid ?? NodeID.INVALID;

			if (boardNodeId.IsValid && agentNodeId.IsValid && boardNodeId == agentNodeId && (!mobileNodeId.IsValid || mobileNodeId != boardNodeId))
			{
				nodeId = boardNodeId;
				source = "board";
				return true;
			}

			if (!mobileNodeId.IsValid)
			{
				if (boardNodeId.IsValid)
				{
					nodeId = boardNodeId;
					source = "board";
					return true;
				}

				if (agentNodeId.IsValid)
				{
					nodeId = agentNodeId;
					source = "agent";
					return true;
				}
			}

			if (mobileNodeId.IsValid)
			{
				nodeId = mobileNodeId;
				source = "mobile";
				return true;
			}

			if (boardNodeId.IsValid)
			{
				nodeId = boardNodeId;
				source = "board";
				return true;
			}

			if (agentNodeId.IsValid)
			{
				nodeId = agentNodeId;
				source = "agent";
				return true;
			}

			return false;
		}

		private static bool TryGetStrictPhysicalVehicleNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid || global::Game.Game.ctx?.board?.nodes == null)
			{
				return false;
			}

			Entity vehicle = vehicleId.FindEntity();
			if (vehicle?.data?.board == null)
			{
				return false;
			}

			try
			{
				WorldPos worldPos = vehicle.data.board.worldpos;
				if (worldPos.IsZero)
				{
					return false;
				}

				Node snappedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(worldPos, 5f);
				if (snappedNode == null || !snappedNode.id.IsValid)
				{
					return false;
				}

				nodeId = snappedNode.id;
				source = "physical";
				return true;
			}
			catch
			{
				nodeId = NodeID.INVALID;
				source = "none";
				return false;
			}
		}

		private static bool TryGetObservedReachedInfo(EntityID vehicleId, out NodeID nodeId, out int frame, out string source)
		{
			nodeId = NodeID.INVALID;
			frame = -1;
			source = "none";
			Type helperType = FindType("GameplayTweaks.MultiCrewVehicleHelper");
			if (helperType == null || !vehicleId.IsValid)
			{
				return false;
			}

			List<string> missing = new List<string>();
			long vehicleKey = unchecked((long)vehicleId.id);
			object observedNodes = GetStaticFieldValue(helperType, "_observedHumanVehicleReachedNodeByVehicleId", missing);
			object observedFrames = GetStaticFieldValue(helperType, "_observedHumanVehicleReachedFrameByVehicleId", missing);
			object observedSources = GetStaticFieldValue(helperType, "_observedHumanVehicleReachedSourceByVehicleId", missing);

			if (!TryGetDictionaryValue(observedNodes, vehicleKey, out nodeId) || !nodeId.IsValid)
			{
				return false;
			}

			TryGetDictionaryValue(observedFrames, vehicleKey, out frame);
			if (!TryGetDictionaryValue(observedSources, vehicleKey, out source) || string.IsNullOrWhiteSpace(source))
			{
				source = "mobile";
			}

			return true;
		}

		private static bool IsPhysicalOnlyAction(string actionType)
		{
			if (string.IsNullOrWhiteSpace(actionType))
			{
				return false;
			}

			string normalized = actionType.Trim().ToLowerInvariant();
			return normalized.Contains("storage")
				|| normalized.Contains("combat")
				|| normalized.Contains("hostile")
				|| normalized.Contains("module")
				|| normalized.Contains("shop-commit")
				|| normalized.Contains("buy-sell-commit")
				|| normalized.Contains("owned-building")
				|| normalized.Contains("safehouse")
				|| normalized.Contains("physical");
		}

		private static bool IsRouteSimExpectedOnlyAction(string actionType)
		{
			if (string.IsNullOrWhiteSpace(actionType))
			{
				return false;
			}

			string normalized = actionType.Trim().ToLowerInvariant();
			return normalized.Contains("heal");
		}

		private static void CaptureDeliveryPumpGuardState(EntityID vehicleId, DeliveryPumpDecision decision)
		{
			Type travelEndType = FindType("GameplayTweaks.HumanVehicleTravelEndSyncPatch");
			if (travelEndType == null || !vehicleId.IsValid)
			{
				decision.Missing = AppendMissing(decision.Missing, "GameplayTweaks.HumanVehicleTravelEndSyncPatch");
				return;
			}

			long vehicleKey = unchecked((long)vehicleId.id);
			List<string> missing = new List<string>();
			object guardFrames = GetStaticFieldValue(travelEndType, "DeliveryPumpSegmentStartFrameByVehicleId", missing);
			object guardTargets = GetStaticFieldValue(travelEndType, "DeliveryPumpSegmentTargetByVehicleId", missing);
			object manualStops = GetStaticFieldValue(travelEndType, "ManualDeliveryStopByVehicleId", missing);

			if (TryGetDictionaryValue(guardFrames, vehicleKey, out int guardFrame))
			{
				decision.HasGuardFrame = true;
				decision.GuardStartFrame = guardFrame;
			}

			if (TryGetDictionaryValue(guardTargets, vehicleKey, out NodeID guardTargetNodeId))
			{
				decision.HasGuardTarget = guardTargetNodeId.IsValid;
				decision.GuardTargetNodeID = guardTargetNodeId;
			}

			if (TryGetDictionaryValue(manualStops, vehicleKey, out object manualStop) && manualStop != null)
			{
				decision.HasManualStop = true;
				TryReadField(manualStop, "StopNodeId", out decision.ManualStopNodeID);
				TryReadField(manualStop, "AutomationTargetNodeId", out decision.ManualAutomationTargetNodeID);
				TryReadField(manualStop, "NextStep", out decision.ManualNextStep);
				TryReadField(manualStop, "Frame", out decision.ManualFrame);
				if (TryReadField(manualStop, "SequenceId", out object sequenceId) && sequenceId != null)
				{
					decision.ManualSequence = sequenceId.ToString();
				}
			}

			if (missing.Count > 0)
			{
				decision.Missing = AppendMissing(decision.Missing, string.Join(",", missing.ToArray()));
			}
		}

		private static void CaptureRouteShopStagingState(EntityID vehicleId, RouteSimAccessDecision decision)
		{
			Type shopStateType = FindType("GameplayTweaks.RouteShopStagingState");
			if (shopStateType == null || !vehicleId.IsValid)
			{
				decision.Missing = AppendMissing(decision.Missing, "GameplayTweaks.RouteShopStagingState");
				return;
			}

			long vehicleKey = unchecked((long)vehicleId.id);
			List<string> missing = new List<string>();
			object ordersByVehicle = GetStaticFieldValue(shopStateType, "_ordersByVehicleId", missing);
			object routeInConversations = GetStaticFieldValue(shopStateType, "_recentRouteInConversationByVehicleId", missing);
			object openPickers = GetStaticFieldValue(shopStateType, "_openPickerByVehicleId", missing);

			decision.HasStagedOrders = TryGetDictionaryValue(ordersByVehicle, vehicleKey, out object orders) && CountItems(orders) > 0;
			decision.StagedOrderCount = decision.HasStagedOrders ? CountItems(orders) : 0;
			decision.HasRouteInConversation = TryGetDictionaryValue(routeInConversations, vehicleKey, out object conversation) && conversation != null;
			if (decision.HasRouteInConversation)
			{
				TryReadField(conversation, "NodeID", out decision.RouteInConversationNodeID);
				TryReadField(conversation, "Context", out decision.RouteInConversationContext);
				TryReadField(conversation, "Source", out decision.RouteInConversationSource);
				TryReadField(conversation, "Frame", out decision.RouteInConversationFrame);
			}

			decision.HasOpenPicker = TryGetDictionaryValue(openPickers, vehicleKey, out object picker) && picker != null;
			if (decision.HasOpenPicker)
			{
				TryReadField(picker, "BuildingID", out decision.OpenPickerBuildingID);
				TryReadField(picker, "DestinationNodeID", out decision.OpenPickerDestinationNodeID);
				TryReadField(picker, "Source", out decision.OpenPickerSource);
				TryReadField(picker, "Frame", out decision.OpenPickerFrame);
			}

			if (missing.Count > 0)
			{
				decision.Missing = AppendMissing(decision.Missing, string.Join(",", missing.ToArray()));
			}
		}

		private static string AppendMissing(string existing, string value)
		{
			if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
			{
				return string.IsNullOrWhiteSpace(existing) ? "none" : existing;
			}

			if (string.IsNullOrWhiteSpace(existing) || string.Equals(existing, "none", StringComparison.OrdinalIgnoreCase))
			{
				return value;
			}

			return existing + "," + value;
		}

		private static Assembly FindAssembly(string assemblyName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
				{
					return assembly;
				}
			}

			return null;
		}

		private static Type FindType(string fullName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type type = assembly.GetType(fullName, false);
				if (type != null)
				{
					return type;
				}
			}

			return null;
		}

		private static object GetStaticFieldValue(Type type, string fieldName, RouteStateAuditSnapshot snapshot)
		{
			return GetStaticFieldValue(type, fieldName, snapshot.Missing);
		}

		private static object GetStaticFieldValue(Type type, string fieldName, List<string> missing)
		{
			FieldInfo field = type?.GetField(fieldName, StaticFieldFlags);
			if (field == null)
			{
				missing.Add((type?.FullName ?? "unknown") + "." + fieldName);
				return null;
			}

			try
			{
				return field.GetValue(null);
			}
			catch (Exception ex)
			{
				missing.Add(type.FullName + "." + fieldName + ":" + ex.GetType().Name);
				return null;
			}
		}

		private static int CountItems(object value)
		{
			if (value == null)
			{
				return 0;
			}

			if (value is ICollection collection)
			{
				return collection.Count;
			}

			PropertyInfo countProperty = value.GetType().GetProperty("Count", InstanceMemberFlags);
			if (countProperty != null && countProperty.PropertyType == typeof(int))
			{
				try
				{
					return (int)countProperty.GetValue(value, null);
				}
				catch
				{
					return 0;
				}
			}

			int count = 0;
			if (value is IEnumerable enumerable)
			{
				foreach (object _ in enumerable)
				{
					count++;
				}
			}

			return count;
		}

		private static int CountItemsWithBooleanField(object dictionaryValue, string fieldName)
		{
			int count = 0;
			foreach (object value in EnumerateDictionaryValues(dictionaryValue))
			{
				if (TryReadBooleanField(value, fieldName, out bool fieldValue) && fieldValue)
				{
					count++;
				}
			}

			return count;
		}

		private static int CountNestedCollectionItems(object dictionaryValue)
		{
			int count = 0;
			foreach (object value in EnumerateDictionaryValues(dictionaryValue))
			{
				count += CountItems(value);
			}

			return count;
		}

		private static IEnumerable<object> EnumerateDictionaryValues(object dictionaryValue)
		{
			if (dictionaryValue is IDictionary dictionary)
			{
				foreach (DictionaryEntry entry in dictionary)
				{
					yield return entry.Value;
				}

				yield break;
			}

			if (!(dictionaryValue is IEnumerable enumerable))
			{
				yield break;
			}

			foreach (object entry in enumerable)
			{
				if (entry == null)
				{
					continue;
				}

				PropertyInfo valueProperty = entry.GetType().GetProperty("Value", InstanceMemberFlags);
				yield return valueProperty != null ? valueProperty.GetValue(entry, null) : entry;
			}
		}

		private static bool TryReadBooleanField(object value, string fieldName, out bool fieldValue)
		{
			fieldValue = false;
			if (value == null)
			{
				return false;
			}

			FieldInfo field = value.GetType().GetField(fieldName, InstanceMemberFlags);
			if (field == null || field.FieldType != typeof(bool))
			{
				return false;
			}

			fieldValue = (bool)field.GetValue(value);
			return true;
		}

		private static bool TryReadField<T>(object value, string fieldName, out T fieldValue)
		{
			fieldValue = default;
			if (value == null)
			{
				return false;
			}

			FieldInfo field = value.GetType().GetField(fieldName, InstanceMemberFlags);
			if (field == null)
			{
				return false;
			}

			object rawValue = field.GetValue(value);
			if (rawValue is T typedValue)
			{
				fieldValue = typedValue;
				return true;
			}

			return false;
		}

		private static bool TryGetDictionaryValue<T>(object dictionaryValue, long key, out T value)
		{
			value = default;
			if (dictionaryValue == null)
			{
				return false;
			}

			if (dictionaryValue is IDictionary dictionary)
			{
				if (!dictionary.Contains(key))
				{
					return false;
				}

				object rawValue = dictionary[key];
				if (rawValue is T typedValue)
				{
					value = typedValue;
					return true;
				}

				return false;
			}

			foreach (object entry in EnumerateDictionaryValuesForKey(dictionaryValue, key))
			{
				if (entry is T typedValue)
				{
					value = typedValue;
					return true;
				}
			}

			return false;
		}

		private static bool ContainsLong(object collectionValue, long key)
		{
			if (collectionValue == null)
			{
				return false;
			}

			if (collectionValue is IDictionary dictionary)
			{
				return dictionary.Contains(key);
			}

			if (!(collectionValue is IEnumerable enumerable))
			{
				return false;
			}

			foreach (object item in enumerable)
			{
				if (item == null)
				{
					continue;
				}

				if (item is long longValue && longValue == key)
				{
					return true;
				}

				try
				{
					if (Convert.ToInt64(item) == key)
					{
						return true;
					}
				}
				catch
				{
					// Ignore non-numeric collection entries.
				}
			}

			return false;
		}

		private static IEnumerable<object> EnumerateDictionaryValuesForKey(object dictionaryValue, long key)
		{
			if (dictionaryValue is IDictionary dictionary)
			{
				if (dictionary.Contains(key))
				{
					yield return dictionary[key];
				}

				yield break;
			}

			if (!(dictionaryValue is IEnumerable enumerable))
			{
				yield break;
			}

			foreach (object entry in enumerable)
			{
				if (entry == null)
				{
					continue;
				}

				PropertyInfo keyProperty = entry.GetType().GetProperty("Key", InstanceMemberFlags);
				PropertyInfo valueProperty = entry.GetType().GetProperty("Value", InstanceMemberFlags);
				if (keyProperty == null || valueProperty == null)
				{
					continue;
				}

				object rawKey = keyProperty.GetValue(entry, null);
				if (TryConvertInt64(rawKey, out long entryKey) && entryKey == key)
				{
					yield return valueProperty.GetValue(entry, null);
					yield break;
				}
			}
		}

		private static bool TryConvertInt64(object value, out long result)
		{
			result = 0;
			try
			{
				result = Convert.ToInt64(value);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static bool IsRoutesBridgeAvailable()
		{
			return Instance != null;
		}

		public static bool OwnsTravelContinuation()
		{
			return false;
		}

		public static bool OwnsVehicleNodeAuthority()
		{
			return false;
		}

		public static bool OwnsDeliveryPump()
		{
			return false;
		}

		public static bool OwnsRouteSimAccess()
		{
			return false;
		}

		public static bool OwnsRouteBehaviorSlice(string slice)
		{
			if (string.IsNullOrWhiteSpace(slice))
			{
				return false;
			}

			string normalized = slice.Trim().ToLowerInvariant();
			switch (normalized)
			{
				case "travel-continuation":
				case "route-continuation":
				case "queued-route-resume":
					return OwnsTravelContinuation();
				case "vehicle-node-authority":
				case "node-authority":
					return OwnsVehicleNodeAuthority();
				case "delivery-pump":
				case "delivery-route-pump":
					return OwnsDeliveryPump();
				case "route-sim-access":
				case "route-sim":
					return OwnsRouteSimAccess();
				default:
					return false;
			}
		}

		public static string GetRoutesBridgeVersion()
		{
			return PluginVersion;
		}

		public static string GetRoutesBridgeSummary()
		{
			return "routes-bridge version=" + PluginVersion +
				" phase=gameplaytweaks-delegation" +
				" ownsTravelContinuation=" + OwnsTravelContinuation() +
				" ownsTravelContinuationDecision=" + OwnsTravelContinuationDecision() +
				" ownsVehicleNodeAuthority=" + OwnsVehicleNodeAuthority() +
				" ownsVehicleNodeAuthorityDecision=" + OwnsVehicleNodeAuthorityDecision() +
				" ownsDeliveryPump=" + OwnsDeliveryPump() +
				" ownsDeliveryPumpDecision=" + OwnsDeliveryPumpDecision() +
				" ownsRouteSimAccess=" + OwnsRouteSimAccess() +
				" ownsRouteSimAccessDecision=" + OwnsRouteSimAccessDecision() +
				" ownsShopStaging=False" +
				" ownsUi=False" +
				" ownsEconomy=False" +
				" ownsFamily=False" +
				" ownsPolitics=False" +
				" ownsCompatibility=False";
		}

		private struct PendingRouteInfo
		{
			public EntityID PeepID;
			public NodeID StartNodeID;
			public NodeID ExpectedNodeID;
			public NodeID GoalNodeID;
			public bool ResumeQueued;
		}

		public sealed class RouteAuthorityClassification
		{
			public EntityID VehicleID;
			public EntityID PeepID;
			public NodeID QueryNodeID;
			public NodeID PhysicalNodeID;
			public NodeID StartNodeID;
			public NodeID ExpectedNodeID;
			public NodeID GoalNodeID;
			public string ActionType;
			public string PhysicalNodeSource;
			public string RouteModeLabel;
			public string RouteSimReason;
			public string Missing;
			public string Reason;
			public bool GameplayTweaksLoaded;
			public bool HelperReadable;
			public bool VehicleFound;
			public bool HasPhysicalNode;
			public bool PhysicalAtQueryNode;
			public bool HasPendingRoute;
			public bool ActiveTravel;
			public bool ResumeQueued;
			public bool QueuedResume;
			public bool TurnStartFinalized;
			public bool TurnStartDeferred;
			public bool HasExpectedNode;
			public bool HasGoalNode;
			public bool QueryMatchesExpectedNode;
			public bool QueryMatchesGoalNode;
			public bool RouteSimConvenienceAllowed;

			public string ToLogString()
			{
				return "route-authority" +
					" version=" + PluginVersion +
					" vehicle=" + VehicleID.id +
					" queryNode=" + QueryNodeID +
					" action=" + (ActionType ?? "none") +
					" gameplayTweaks=" + GameplayTweaksLoaded +
					" helper=" + HelperReadable +
					" vehicleFound=" + VehicleFound +
					" physicalNode=" + PhysicalNodeID +
					" physicalSource=" + (PhysicalNodeSource ?? "none") +
					" physicalAtQuery=" + PhysicalAtQueryNode +
					" startNode=" + StartNodeID +
					" expectedNode=" + ExpectedNodeID +
					" finalGoal=" + GoalNodeID +
					" active=" + ActiveTravel +
					" resumeQueued=" + ResumeQueued +
					" queuedResume=" + QueuedResume +
					" turnStartFinalized=" + TurnStartFinalized +
					" turnStartDeferred=" + TurnStartDeferred +
					" label=\"" + (RouteModeLabel ?? "Unknown") + "\"" +
					" queryMatchesExpected=" + QueryMatchesExpectedNode +
					" queryMatchesGoal=" + QueryMatchesGoalNode +
					" routeSimAllowed=" + RouteSimConvenienceAllowed +
					" routeSimReason=" + (RouteSimReason ?? "none") +
					" missing=\"" + (Missing ?? "none") + "\"" +
					" reason=" + (Reason ?? "unread");
			}
		}

		public sealed class VehicleNodeAuthorityDecision
		{
			public EntityID VehicleID;
			public NodeID QueryNodeID;
			public NodeID PhysicalNodeID;
			public NodeID StrictPhysicalNodeID;
			public NodeID ObservedNodeID;
			public NodeID StartNodeID;
			public NodeID ExpectedNodeID;
			public NodeID GoalNodeID;
			public string ActionType;
			public string PhysicalNodeSource;
			public string StrictPhysicalSource;
			public string ObservedSource;
			public string RouteModeLabel;
			public string Decision;
			public string Reason;
			public string Missing;
			public bool OwnsDecision;
			public bool AllowGameplayTweaksFallback;
			public bool GameplayTweaksLoaded;
			public bool HelperReadable;
			public bool VehicleFound;
			public bool HasPendingRoute;
			public bool ActiveTravel;
			public bool ResumeQueued;
			public bool QueuedResume;
			public bool PhysicalOnlyAction;
			public bool PhysicalAtQueryNode;
			public bool StrictPhysicalAtQueryNode;
			public bool ObservedAtQueryNode;
			public bool ObservedExpectedArrival;
			public bool QueryMatchesExpectedNode;
			public bool QueryMatchesGoalNode;
			public bool StaleLogicalNodeRejected;
			public int ObservedFrame;

			public string ToLogString()
			{
				return "vehicle-node-authority" +
					" version=" + PluginVersion +
					" vehicle=" + VehicleID.id +
					" queryNode=" + QueryNodeID +
					" action=" + (ActionType ?? "none") +
					" ownsDecision=" + OwnsDecision +
					" decision=" + (Decision ?? "fallback") +
					" reason=" + (Reason ?? "unread") +
					" fallback=" + AllowGameplayTweaksFallback +
					" gameplayTweaks=" + GameplayTweaksLoaded +
					" helper=" + HelperReadable +
					" vehicleFound=" + VehicleFound +
					" physicalNode=" + PhysicalNodeID +
					" physicalSource=" + (PhysicalNodeSource ?? "none") +
					" strictPhysicalNode=" + StrictPhysicalNodeID +
					" strictPhysicalSource=" + (StrictPhysicalSource ?? "none") +
					" observedNode=" + ObservedNodeID +
					" observedSource=" + (ObservedSource ?? "none") +
					" observedFrame=" + ObservedFrame +
					" startNode=" + StartNodeID +
					" expectedNode=" + ExpectedNodeID +
					" finalGoal=" + GoalNodeID +
					" active=" + ActiveTravel +
					" resumeQueued=" + ResumeQueued +
					" queuedResume=" + QueuedResume +
					" physicalOnly=" + PhysicalOnlyAction +
					" physicalAtQuery=" + PhysicalAtQueryNode +
					" strictAtQuery=" + StrictPhysicalAtQueryNode +
					" observedAtQuery=" + ObservedAtQueryNode +
					" observedExpected=" + ObservedExpectedArrival +
					" queryMatchesExpected=" + QueryMatchesExpectedNode +
					" queryMatchesGoal=" + QueryMatchesGoalNode +
					" staleRejected=" + StaleLogicalNodeRejected +
					" label=\"" + (RouteModeLabel ?? "Unknown") + "\"" +
					" missing=\"" + (Missing ?? "none") + "\"";
			}
		}

		public sealed class DeliveryPumpDecision
		{
			public EntityID VehicleID;
			public NodeID FinalNodeID;
			public NodeID PhysicalNodeID;
			public NodeID ExpectedNodeID;
			public NodeID GoalNodeID;
			public NodeID GuardTargetNodeID;
			public NodeID ManualStopNodeID;
			public NodeID ManualAutomationTargetNodeID;
			public string Source;
			public string PhysicalNodeSource;
			public string RouteModeLabel;
			public string Decision;
			public string Reason;
			public string Missing;
			public string ManualSequence;
			public bool OwnsDecision;
			public bool AllowGameplayTweaksFallback;
			public bool GameplayTweaksLoaded;
			public bool HelperReadable;
			public bool VehicleFound;
			public bool ActiveTravel;
			public bool ResumeQueued;
			public bool QueuedResume;
			public bool HasGuardFrame;
			public bool HasGuardTarget;
			public bool HasManualStop;
			public int GuardStartFrame;
			public int ManualFrame;
			public int ManualNextStep;
			public int QueuePumps;
			public int AutomationPumps;

			public string ToLogString()
			{
				return "delivery-route-pump" +
					" version=" + PluginVersion +
					" vehicle=" + VehicleID.id +
					" finalNode=" + FinalNodeID +
					" source=" + (Source ?? "unknown") +
					" ownsDecision=" + OwnsDecision +
					" decision=" + (Decision ?? "fallback") +
					" reason=" + (Reason ?? "unread") +
					" fallback=" + AllowGameplayTweaksFallback +
					" gameplayTweaks=" + GameplayTweaksLoaded +
					" helper=" + HelperReadable +
					" vehicleFound=" + VehicleFound +
					" physicalNode=" + PhysicalNodeID +
					" physicalSource=" + (PhysicalNodeSource ?? "none") +
					" expectedNode=" + ExpectedNodeID +
					" finalGoal=" + GoalNodeID +
					" active=" + ActiveTravel +
					" resumeQueued=" + ResumeQueued +
					" queuedResume=" + QueuedResume +
					" guardFrame=" + GuardStartFrame +
					" hasGuardFrame=" + HasGuardFrame +
					" guardTarget=" + GuardTargetNodeID +
					" hasGuardTarget=" + HasGuardTarget +
					" hasManualStop=" + HasManualStop +
					" manualStop=" + ManualStopNodeID +
					" manualAutomationTarget=" + ManualAutomationTargetNodeID +
					" manualRoute=" + (ManualSequence ?? "none") +
					" manualNextStep=" + ManualNextStep +
					" manualFrame=" + ManualFrame +
					" queuePumps=" + QueuePumps +
					" automationPumps=" + AutomationPumps +
					" label=\"" + (RouteModeLabel ?? "Unknown") + "\"" +
					" missing=\"" + (Missing ?? "none") + "\"";
			}
		}

		public sealed class RouteSimAccessDecision
		{
			public EntityID VehicleID;
			public EntityID OpenPickerBuildingID;
			public NodeID QueryNodeID;
			public NodeID PhysicalNodeID;
			public NodeID StartNodeID;
			public NodeID ExpectedNodeID;
			public NodeID GoalNodeID;
			public NodeID RouteInConversationNodeID;
			public NodeID OpenPickerDestinationNodeID;
			public string ActionType;
			public string AccessSource;
			public string PhysicalNodeSource;
			public string RouteModeLabel;
			public string RouteSimReason;
			public string Decision;
			public string Reason;
			public string Missing;
			public string RouteInConversationContext;
			public string RouteInConversationSource;
			public string OpenPickerSource;
			public bool OwnsDecision;
			public bool AllowGameplayTweaksFallback;
			public bool GameplayTweaksLoaded;
			public bool HelperReadable;
			public bool VehicleFound;
			public bool HasPendingRoute;
			public bool ActiveTravel;
			public bool ResumeQueued;
			public bool QueuedResume;
			public bool QueryMatchesExpectedNode;
			public bool QueryMatchesGoalNode;
			public bool PhysicalOnlyAction;
			public bool RouteSimConvenienceAllowed;
			public bool HasRouteInConversation;
			public bool HasOpenPicker;
			public bool HasStagedOrders;
			public int RouteInConversationFrame;
			public int OpenPickerFrame;
			public int StagedOrderCount;

			public string ToLogString()
			{
				return "route-sim-access" +
					" version=" + PluginVersion +
					" vehicle=" + VehicleID.id +
					" queryNode=" + QueryNodeID +
					" action=" + (ActionType ?? "none") +
					" ownsDecision=" + OwnsDecision +
					" decision=" + (Decision ?? "fallback") +
					" reason=" + (Reason ?? "unread") +
					" fallback=" + AllowGameplayTweaksFallback +
					" accessSource=" + (AccessSource ?? "none") +
					" gameplayTweaks=" + GameplayTweaksLoaded +
					" helper=" + HelperReadable +
					" vehicleFound=" + VehicleFound +
					" physicalNode=" + PhysicalNodeID +
					" physicalSource=" + (PhysicalNodeSource ?? "none") +
					" startNode=" + StartNodeID +
					" expectedNode=" + ExpectedNodeID +
					" finalGoal=" + GoalNodeID +
					" active=" + ActiveTravel +
					" resumeQueued=" + ResumeQueued +
					" queuedResume=" + QueuedResume +
					" queryMatchesExpected=" + QueryMatchesExpectedNode +
					" queryMatchesGoal=" + QueryMatchesGoalNode +
					" physicalOnly=" + PhysicalOnlyAction +
					" routeSimAllowed=" + RouteSimConvenienceAllowed +
					" routeSimReason=" + (RouteSimReason ?? "none") +
					" hasRouteInConversation=" + HasRouteInConversation +
					" routeInNode=" + RouteInConversationNodeID +
					" routeInContext=" + (RouteInConversationContext ?? "none") +
					" routeInSource=" + (RouteInConversationSource ?? "none") +
					" routeInFrame=" + RouteInConversationFrame +
					" hasOpenPicker=" + HasOpenPicker +
					" openPickerBuilding=" + OpenPickerBuildingID.id +
					" openPickerNode=" + OpenPickerDestinationNodeID +
					" openPickerSource=" + (OpenPickerSource ?? "none") +
					" openPickerFrame=" + OpenPickerFrame +
					" hasStagedOrders=" + HasStagedOrders +
					" stagedOrders=" + StagedOrderCount +
					" label=\"" + (RouteModeLabel ?? "Unknown") + "\"" +
					" missing=\"" + (Missing ?? "none") + "\"";
			}
		}

		public sealed class TravelContinuationDecision
		{
			public EntityID VehicleID;
			public NodeID PhysicalNodeID;
			public NodeID StartNodeID;
			public NodeID ExpectedNodeID;
			public NodeID GoalNodeID;
			public string PhysicalNodeSource;
			public string RouteModeLabel;
			public string Decision;
			public string Reason;
			public string Missing;
			public bool OwnsDecision;
			public bool AllowGameplayTweaksFallback;
			public bool HasPendingRoute;
			public bool ActiveTravel;
			public bool ResumeQueued;
			public bool QueuedResume;
			public bool TurnStartFinalized;
			public bool TurnStartDeferred;

			public string ToLogString()
			{
				return "travel-continuation" +
					" version=" + PluginVersion +
					" vehicle=" + VehicleID.id +
					" ownsDecision=" + OwnsDecision +
					" decision=" + (Decision ?? "fallback") +
					" reason=" + (Reason ?? "unread") +
					" fallback=" + AllowGameplayTweaksFallback +
					" physicalNode=" + PhysicalNodeID +
					" physicalSource=" + (PhysicalNodeSource ?? "none") +
					" startNode=" + StartNodeID +
					" expectedNode=" + ExpectedNodeID +
					" finalGoal=" + GoalNodeID +
					" active=" + ActiveTravel +
					" resumeQueued=" + ResumeQueued +
					" queuedResume=" + QueuedResume +
					" turnStartFinalized=" + TurnStartFinalized +
					" turnStartDeferred=" + TurnStartDeferred +
					" label=\"" + (RouteModeLabel ?? "Unknown") + "\"" +
					" missing=\"" + (Missing ?? "none") + "\"";
			}
		}

		private sealed class RouteStateAuditSnapshot
		{
			public bool GameplayTweaksLoaded;
			public bool HelperReadable;
			public int PendingVehicles;
			public int ActiveVehicles;
			public int QueuedResumeVehicles;
			public int RecentRequeues;
			public int TurnStartFinalized;
			public int TurnStartDeferred;
			public int DeliveryPumpGuards;
			public int DeliveryPumpGuardFrames;
			public int DeliveryPumpGuardTargets;
			public int ManualDeliveryStops;
			public int StagedShopOrderVehicles;
			public int StagedShopOrders;
			public int RouteInConversations;
			public int OpenShopPickers;
			public int RouteModeLabelCandidates;
			public string Reason = "unread";
			public readonly List<string> Missing = new List<string>();

			public bool IsEmpty =>
				PendingVehicles == 0 &&
				ActiveVehicles == 0 &&
				QueuedResumeVehicles == 0 &&
				RecentRequeues == 0 &&
				TurnStartFinalized == 0 &&
				TurnStartDeferred == 0 &&
				DeliveryPumpGuards == 0 &&
				DeliveryPumpGuardFrames == 0 &&
				DeliveryPumpGuardTargets == 0 &&
				ManualDeliveryStops == 0 &&
				StagedShopOrderVehicles == 0 &&
				StagedShopOrders == 0 &&
				RouteInConversations == 0 &&
				OpenShopPickers == 0 &&
				RouteModeLabelCandidates == 0;

			public string MissingSummary => Missing.Count == 0 ? "none" : string.Join(",", Missing.ToArray());
		}

		private static class CommandExecutorRouteStateAuditPatch
		{
			internal static void Postfix(CommandExecutor __instance)
			{
				try
				{
					Instance?.LogHumanTurnRouteStateAudit(__instance);
				}
				catch (Exception ex)
				{
					Log?.LogWarning("route-state-audit human-turn failed: " + ex.GetType().Name + ": " + ex.Message);
				}
			}
		}
	}
}

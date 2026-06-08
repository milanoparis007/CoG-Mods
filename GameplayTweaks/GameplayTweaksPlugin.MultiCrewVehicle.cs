using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core;
using Game.Session.Actions;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Data;
using Game.Session;
using Game.Session.Player;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.Session.Input;
using Game.Services;
using Game.UI.Session.Combat;
using Game.UI.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using Game.UI.Session.Picks;
using Game.UI.Session.Popups;
using Game.UI.Util;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayTweaks
{
	/// <summary>
	/// Multi-crew per vehicle with per-vehicle slot limits plus explicit per-vehicle driver mapping.
	/// </summary>
	internal static class MultiCrewVehicleHelper
	{
		/// <summary>Default crew slots when vehicle template is unknown.</summary>
		public const int DefaultCrewSlots = 1;

		/// <summary>Max movement points a non-driver passenger can use per turn.</summary>
		public const int PassengerMoveCap = 7;

		/// <summary>Per-vehicle template crew slot limits. Unknown vehicles default to DefaultCrewSlots.</summary>
		private static readonly Dictionary<string, int> VehicleCrewSlots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			{ "vehicle-car", 4 },
			{ "vehicle-town-car", 4 },
			{ "vehicle-car-preorder", 4 },
			{ "vehicle-sedan", 4 },
			{ "vehicle-sports-car", 2 },
			{ "vehicle-sportsbenz", 4 },
			{ "vehicle-bulletproof-car", 4 },
			{ "vehicle-bulletproof-sports-car", 4 },
			{ "vehicle-transit", 1 },
			{ "vehicle-lseries", 4 },
			{ "vehicle-independence", 4 },
			{ "vehicle-bmw327", 4 },
			{ "vehicle-benz-ssk", 4 },
			{ "vehicle-jaguarm", 2 },
			{ "vehicle-bulletproof-independence", 4 },
			{ "vehicle-bulletproof-jaguar", 2 },
			{ "vehicle-lancer", 4 },
			{ "vehicle-customline", 4 },
			{ "vehicle-skylark", 4 },
			{ "vehicle-safari", 4 },
			{ "vehicle-electra", 4 },
			{ "vehicle-phantom", 4 },
			{ "vehicle-bmw", 4 },
			{ "vehicle-benz", 4 },
			{ "vehicle-jaguar", 2 },
			{ "vehicle-bulletproof-phantom", 4 },
			{ "vehicle-pickup-truck", 7 },
			{ "vehicle-dseries", 7 },
			{ "vehicle-camino", 4 },
			{ "vehicle-bulletproof-camino", 2 },
			{ "vehicle-bulletproof-truck", 2 },
			{ "vehicle-small-delivery-truck", 8 },
			{ "vehicle-large-delivery-truck", 10 },
		};

		/// <summary>Max crew slots for any vehicle (for UI portrait row size).</summary>
		public static readonly int MaxSlotsAnyVehicle = 10;

		private static Dictionary<long, long> DriverMap
		{
			get
			{
				var map = GameplayTweaksPlugin.SaveData.VehicleDriverByVehicleId;
				if (map == null)
				{
					map = new Dictionary<long, long>();
					GameplayTweaksPlugin.SaveData.VehicleDriverByVehicleId = map;
				}
				return map;
			}
		}

		private struct PendingVehicleTravelState
		{
			public EntityID VehicleID;

			public EntityID PeepID;

			public int CommandOwnerKey;

			public NodeID StartNodeID;

			public NodeID ExpectedNodeID;

			public NodeID GoalNodeID;

			public bool ResumeQueued;
		}

		private struct HumanVehicleRequeueState
		{
			public NodeID StartNodeID;

			public NodeID GoalNodeID;

			public int Day;

			public NodeID LastFinalizedStartNodeID;

			public NodeID LastFinalizedArrivalNodeID;

			public NodeID LastBlockedReverseStartNodeID;

			public NodeID LastBlockedReverseGoalNodeID;

			public int LastBlockedReverseDay;
		}

		private struct AiRecentVehicleMoveState
		{
			public NodeID StartNodeID;

			public NodeID GoalNodeID;

			public int Day;

			public NodeID LastBlockedReverseStartNodeID;

			public NodeID LastBlockedReverseGoalNodeID;

			public int LastBlockedReverseDay;
		}

		private enum QueuedHumanVehiclePathBuildResult
		{
			Invalid,
			NoMovesYet,
			InsufficientMovesForSegment,
			BuiltEmpty,
			BuiltValid
		}

		internal enum CrewNodeResolutionMode
		{
			LiveAuthoritative,
			PendingAllowed,
			PendingCurrentSegmentAllowed,
			QueuedFinalGoalAllowed,
			BuildingCommittedOnly
		}

		private static MethodInfo _getControlledBuildingsMethod;

		private static MethodInfo _getNodeIdMethod;

		private static Type _afterProhibitionRoutesPluginType;

		private static MethodInfo _afterProhibitionRoutesOwnsTravelContinuationDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesLogTravelContinuationDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesOwnsVehicleNodeAuthorityDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesLogVehicleNodeAuthorityDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesOwnsDeliveryPumpDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesLogDeliveryPumpDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesOwnsRouteSimAccessDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesLogRouteSimAccessDecisionMethod;

		private static MethodInfo _afterProhibitionRoutesOwnsRouteBehaviorSliceMethod;

		private static bool _loggedAfterProhibitionRoutesTravelContinuationDelegated;

		private static bool _loggedAfterProhibitionRoutesTravelContinuationFallback;

		private static bool _loggedAfterProhibitionRoutesVehicleNodeAuthorityDelegated;

		private static bool _loggedAfterProhibitionRoutesVehicleNodeAuthorityFallback;

		private static bool _loggedAfterProhibitionRoutesDeliveryPumpDelegated;

		private static bool _loggedAfterProhibitionRoutesDeliveryPumpFallback;

		private static bool _loggedAfterProhibitionRoutesRouteSimAccessDelegated;

		private static bool _loggedAfterProhibitionRoutesRouteSimAccessFallback;

		private static readonly HashSet<string> _loggedAfterProhibitionRoutesBehaviorDelegatedSlices = new HashSet<string>(StringComparer.Ordinal);

		private static readonly HashSet<string> _loggedAfterProhibitionRoutesBehaviorFallbackSlices = new HashSet<string>(StringComparer.Ordinal);

		private static readonly FieldInfo _playerCrewDataField = typeof(PlayerCrew).GetField("_crewdata", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly Dictionary<long, long> StartupCanonicalVehicleByPlayerId = new Dictionary<long, long>();

		private static readonly Dictionary<long, long> StartupFounderAssignmentByPlayerId = new Dictionary<long, long>();

		private static bool _loggedOwnedBuildingProbeFallbackOnce;

		private static bool _loggedSetDriverFailOpenOnce;

		private static bool _loggedSetDriverUnexpectedFailureOnce;

		private static bool _loggedSetDriverCardSuccessOnce;

		private static bool _loggedSetDriverNodeSyncOnce;

		private static bool _loggedPickupDirectAssignOnce;

		private static bool _loggedPickupSelectorOnce;

		private static bool _loggedPickupAssignStickFailureOnce;

		private static bool _loggedFounderIdentityDriftOnce;

		private static bool _loggedStartupCanonicalVehicleOnce;

		private static bool _loggedStartupFounderRepairOnce;

		private static bool _setDriverUiInProgress;

		private static readonly HashSet<string> _loggedVehicleAuthorityKeys = new HashSet<string>(StringComparer.Ordinal);

		private static readonly HashSet<string> _loggedAfterProhibitionRoutesTravelContinuationDecisionKeys = new HashSet<string>(StringComparer.Ordinal);

		private static readonly HashSet<string> _loggedAfterProhibitionRoutesVehicleAuthorityDecisionKeys = new HashSet<string>(StringComparer.Ordinal);

		private static readonly HashSet<string> _loggedAfterProhibitionRoutesDeliveryPumpDecisionKeys = new HashSet<string>(StringComparer.Ordinal);

		private static readonly HashSet<string> _loggedAfterProhibitionRoutesRouteSimAccessDecisionKeys = new HashSet<string>(StringComparer.Ordinal);

		private static readonly Dictionary<string, bool> _afterProhibitionRoutesBehaviorOwnerBySlice = new Dictionary<string, bool>(StringComparer.Ordinal);

		private static readonly string[] AfterProhibitionRoutesBehaviorOwnerWarmSlices =
		{
			"delivery-pump",
			"vehicle-node-authority",
			"route-sim-access",
			"travel-continuation"
		};

		private static bool _afterProhibitionRoutesBehaviorOwnerWarmComplete;

		private static int _afterProhibitionRoutesBehaviorOwnerWarmLastAttemptFrame = -100000;

		private static int _afterProhibitionRoutesBehaviorOwnerWarmAttempts;

		private static readonly bool _verboseVehicleAuthorityPreviewLogs = string.Equals(Environment.GetEnvironmentVariable("COG_VERBOSE_VEHICLE_AUTHORITY"), "1", StringComparison.Ordinal);

		private static readonly bool _verboseAfterProhibitionRoutesDecisionBridge = string.Equals(Environment.GetEnvironmentVariable("COG_VERBOSE_ROUTES_BRIDGE"), "1", StringComparison.Ordinal)
			|| string.Equals(Environment.GetEnvironmentVariable("COG_VERBOSE_VEHICLE_AUTHORITY"), "1", StringComparison.Ordinal);

		private static readonly FieldInfo CrewDialogAllCardsField = typeof(CrewDialog).GetField("_allCards", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly Dictionary<long, PendingVehicleTravelState> _pendingVehicleTravelByVehicleId = new Dictionary<long, PendingVehicleTravelState>();

		private static readonly HashSet<long> _activeHumanVehicleTravel = new HashSet<long>();

		private static int _aiHireNormalizationScopeDepth;

		private static int _aiHireNormalizationDepth;

		private static readonly Dictionary<long, NodeID> _queuedArrivalCommittedNodeByVehicleId = new Dictionary<long, NodeID>();

		private static readonly Dictionary<long, NodeID> _observedHumanVehicleReachedNodeByVehicleId = new Dictionary<long, NodeID>();

		private static readonly Dictionary<long, int> _observedHumanVehicleReachedFrameByVehicleId = new Dictionary<long, int>();

		private static readonly Dictionary<long, string> _observedHumanVehicleReachedSourceByVehicleId = new Dictionary<long, string>();

		private static readonly Dictionary<long, NodeID> _recentQueuedHumanVehicleDestinationPreviewByVehicleId = new Dictionary<long, NodeID>();

		private static readonly Dictionary<long, NodeID> _recentQueuedHumanVehicleDestinationPreviewExpectedByVehicleId = new Dictionary<long, NodeID>();

		private static readonly Dictionary<long, int> _recentQueuedHumanVehicleDestinationPreviewFrameByVehicleId = new Dictionary<long, int>();

		private const int RecentQueuedHumanVehicleDestinationPreviewPreserveFrames = 45;

		private struct CachedVehicleNodeAuthority
		{
			public int Frame;
			public bool HasNode;
			public NodeID NodeId;
			public string Source;
		}

		private static readonly Dictionary<long, CachedVehicleNodeAuthority> _vehicleLiveAuthorityNodeCacheByVehicleId = new Dictionary<long, CachedVehicleNodeAuthority>();

		private static readonly Dictionary<long, CachedVehicleNodeAuthority> _vehiclePhysicalNodeCacheByVehicleId = new Dictionary<long, CachedVehicleNodeAuthority>();

		private static bool _deferredQueuedHumanVehicleRouteResumePending;

		private static int _deferredQueuedHumanVehicleRouteResumeEarliestFrame;

		private static string _deferredQueuedHumanVehicleRouteResumeSource = string.Empty;

		private static readonly Dictionary<long, NodeID> _interruptExpectedStartNodeByVehicleId = new Dictionary<long, NodeID>();

		private static readonly HashSet<long> _turnStartFinalizedQueuedRouteVehicleIds = new HashSet<long>();

		private static readonly HashSet<long> _turnStartDeferredArrivalVehicleIds = new HashSet<long>();

		private static readonly HashSet<long> _ambientTrafficVehicleIds = new HashSet<long>();

		private static readonly HashSet<long> _vehicleSearchBlockedBySurvivors = new HashSet<long>();

		private static readonly Dictionary<long, long> _recentEnemyVehicleDeathPeepByVehicleId = new Dictionary<long, long>();

		private static readonly Dictionary<long, int> _suppressFlushQueueRouteClearByPeepId = new Dictionary<long, int>();

		private static readonly Dictionary<long, int> _suppressPreviewStopRouteClearFrameByPeepId = new Dictionary<long, int>();

		private static readonly Dictionary<long, string> _suppressPreviewStopRouteClearReasonByPeepId = new Dictionary<long, string>();

		internal sealed class VehicleDeathSnapshot
		{
			public EntityID DeadPeepId = EntityID.INVALID;

			public EntityID VehicleID = EntityID.INVALID;

			public PlayerID OwnerPid = PlayerID.INVALID;

			public EntityID PriorDriverPeepId = EntityID.INVALID;

			public bool WasDriver;

			public string Source = string.Empty;

			public NodeID LiveNode = NodeID.INVALID;

			public List<EntityID> SurvivorPeepIds = new List<EntityID>();

			public bool IsValid => DeadPeepId.IsValid && VehicleID.IsValid && OwnerPid.IsValid;
		}

		internal enum EnemyVehiclePresentationMode
		{
			None,
			ActiveCrew,
			DeadCrew,
			Empty
		}

		internal sealed class EnemyVehicleDisplayState
		{
			public EntityID VehicleID = EntityID.INVALID;

			public PlayerID OwnerPid = PlayerID.INVALID;

			public EntityID DriverPeepId = EntityID.INVALID;

			public EntityID RepresentativePeepId = EntityID.INVALID;

			public EntityID InspectablePeepId = EntityID.INVALID;

			public int LiveOccupantCount;

			public int PassengerCount;

			public int InspectableOccupantCount;

			public int InspectablePassengerCount;

			public int CrewSlots;

			public EnemyVehiclePresentationMode Presentation = EnemyVehiclePresentationMode.None;

			public bool HasLiveOccupants => Presentation == EnemyVehiclePresentationMode.ActiveCrew && LiveOccupantCount > 0;

			public bool HasInspectableOccupants => InspectableOccupantCount > 0 && InspectablePeepId.IsValid;

			public Entity RepresentativePeep => RepresentativePeepId.IsValid ? RepresentativePeepId.FindEntity() : null;

			public Entity InspectablePeep => InspectablePeepId.IsValid ? InspectablePeepId.FindEntity() : null;
		}

		private const int QueuedRouteResumeCommandOwnerKey = -1;

		private static int _pendingPresenceSelectionScopeDepth;

		private static int _pendingBuildingInteractionScopeDepth;

		private static readonly Stack<string> _pendingBuildingInteractionSourceStack = new Stack<string>();

		private const int AmbientVisibleTrafficDayFloor = 16;

		private const int AmbientVisibleTrafficNightFloor = 6;

		private const int AmbientVisibleTrafficMaxTarget = 28;

		private const int AmbientVisibleTrafficHardCapBuffer = 4;

		private static string _pendingAmbientSpawnReason = "normal";

		private static int _cachedAmbientCityPopulation = -1;

		private static int _cachedAmbientCityPopulationNodeCount = -1;

		private static int _cachedAmbientCityPopulationBoardKey = -1;

		private static readonly List<NodeID> _cachedAmbientRoadNodeIds = new List<NodeID>();

		private static readonly List<NodeID> _cachedAmbientVisitNodeIds = new List<NodeID>();

		private static int _cachedAmbientRoadNodeBoardKey = -1;

		private static int _cachedAmbientRoadNodeSourceCount = -1;

		private static double _cachedAmbientTerritoryCap = -1.0;

		private static int _cachedAmbientTerritoryCapFrame = -1;

		private static int _cachedAmbientTerritoryCapBoardKey = -1;

		private static MethodInfo _ambientOwnedNodesMethod;

		private static int _cachedAmbientLiveCount = -1;

		private static int _cachedAmbientLiveCountFrame = -1;

		private static readonly Dictionary<long, NodeID> _recentFinalizedNodeByVehicleId = new Dictionary<long, NodeID>();

		private static readonly Dictionary<long, int> _recentFinalizedFrameByVehicleId = new Dictionary<long, int>();

		private const int RecentFinalizeStableBridgeMaxFrames = 8;

		private static readonly Dictionary<long, AiRecentVehicleMoveState> _recentAiVehicleMoveByVehicleId = new Dictionary<long, AiRecentVehicleMoveState>();

		private static readonly Dictionary<long, NodeID> _recentAiCommittedNodeByVehicleId = new Dictionary<long, NodeID>();

		private static readonly Dictionary<long, HumanVehicleRequeueState> _recentHumanVehicleRequeueByVehicleId = new Dictionary<long, HumanVehicleRequeueState>();

		private static readonly Dictionary<long, long> _lastEnemyVehicleRepresentativeByVehicleId = new Dictionary<long, long>();

		private static int _enemyVehicleRepresentativeSwapSummaryDay = int.MinValue;

		private static int _enemyVehicleRepresentativeSwapSummaryTotal;

		private static readonly Dictionary<string, int> _enemyVehicleRepresentativeSwapSummaryBySource = new Dictionary<string, int>(StringComparer.Ordinal);

		private static readonly Dictionary<long, int> _humanPassengerUncappedMovesByPeepId = new Dictionary<long, int>();

		private static long _lastSelectedHumanVehicleId;

		private static readonly HashSet<long> _freshStartupPendingTravelPrunedByPlayerId = new HashSet<long>();

		internal static void ResetTransientEnemyVehiclePresentationState(string sourceTag)
		{
			FlushEnemyVehicleRepresentativeSwapSummary("reset:" + sourceTag);
			_vehicleSearchBlockedBySurvivors.Clear();
			_recentEnemyVehicleDeathPeepByVehicleId.Clear();
			_lastEnemyVehicleRepresentativeByVehicleId.Clear();
			GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"enemy-vehicle-transient-reset source={sourceTag}");
		}

		private static long GetPlayerKey(PlayerID pid)
		{
			return (long)pid.id;
		}

		internal static bool IsFreshHumanStartup(PlayerID pid)
		{
			try
			{
				return pid.IsHumanPlayer && global::Game.Game.ctx != null && !global::Game.Game.ctx.HasSaveFile;
			}
			catch
			{
				return false;
			}
		}

		internal static CrewNodeResolutionMode GetInteractiveCrewNodeResolutionMode()
		{
			return global::Game.Game.ctx?.IsInteractive == true
				? CrewNodeResolutionMode.QueuedFinalGoalAllowed
				: CrewNodeResolutionMode.LiveAuthoritative;
		}

		internal static CrewNodeResolutionMode GetMapScopeCrewNodeResolutionMode()
		{
			return CrewNodeResolutionMode.BuildingCommittedOnly;
		}

		internal static CrewNodeResolutionMode GetActualHumanInteractionCrewNodeResolutionMode()
		{
			return CrewNodeResolutionMode.BuildingCommittedOnly;
		}

		private static bool TryGetSelectedHumanVehiclePendingFinalGoalNodeId(EntityID vehicleId, out NodeID nodeId, out string selectedSource)
		{
			nodeId = NodeID.INVALID;
			selectedSource = "none";
			if (!vehicleId.IsValid
				|| !TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				|| !pendingState.GoalNodeID.IsValid)
			{
				return false;
			}

			bool hasActiveOrQueuedGoal = pendingState.ExpectedNodeID.IsValid
				|| pendingState.ResumeQueued
				|| IsHumanVehicleTravelActive(vehicleId);
			if (!hasActiveOrQueuedGoal)
			{
				return false;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null
				|| !TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out selectedSource)
				|| selectedVehicleId != vehicleId)
			{
				return false;
			}

			nodeId = pendingState.GoalNodeID;
			return nodeId.IsValid;
		}

		private static bool ShouldPreferFinalGoalForScopePreview(string contextTag)
		{
			return string.Equals(contextTag, "scope-preview", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-building", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-compare", StringComparison.Ordinal)
				|| string.Equals(contextTag, "crewhud-scope", StringComparison.Ordinal)
				|| string.Equals(contextTag, "preview-scope-direct", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-command", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-feedback", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-feedback-refresh", StringComparison.Ordinal)
				|| string.Equals(contextTag, "attack-popup-preview", StringComparison.Ordinal);
		}

		private static bool ShouldPreferCommittedNodeBeforeFinalGoalForScope(string contextTag)
		{
			return string.Equals(contextTag, "scope-building", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-command", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-feedback", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-feedback-refresh", StringComparison.Ordinal)
				|| string.Equals(contextTag, "preview-scope-direct", StringComparison.Ordinal)
				|| string.Equals(contextTag, "crewhud-scope", StringComparison.Ordinal);
		}

		internal static bool TryGetHumanVehicleScopePreviewNodeId(EntityID vehicleId, out NodeID nodeId, out string source, string contextTag = null)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "scope-preview");

			bool hasSelectedVehicle = TryGetSelectedHumanVehicleForMapScope(G.GetHumanCrew(), out EntityID selectedVehicleId, out string selectedSource);
			bool selectedVehicleMatches = hasSelectedVehicle && selectedVehicleId == vehicleId;
			bool hasPendingRouteState = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState);
			bool hasPendingState = hasPendingRouteState && pendingState.ExpectedNodeID.IsValid;
			string startupSuppressionReason = "none";
			bool startupSuppressed = hasPendingState
				&& TryGetFreshStartupHumanVehicleAuthoritySuppression(vehicleId, pendingState, out startupSuppressionReason);
			bool hasQueuedResumeRoute = hasPendingRouteState
				&& pendingState.ResumeQueued
				&& pendingState.GoalNodeID.IsValid;
			bool hasTravelPreviewContext = hasPendingRouteState || IsHumanVehicleTravelActive(vehicleId) || hasQueuedResumeRoute;
			NodeID rememberedFinalNodeId = NodeID.INVALID;
			bool hasRememberedFinalNode = selectedVehicleMatches
				&& GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(vehicleId, out rememberedFinalNodeId)
				&& rememberedFinalNodeId.IsValid;

			if (selectedVehicleMatches
				&& ShouldPreferCommittedNodeBeforeFinalGoalForScope(contextTag)
				&& TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out nodeId, out source)
				&& nodeId.IsValid)
			{
				LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{selectedSource}:committed-before-final", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={(hasPendingRouteState ? pendingState.GoalNodeID.ToString() : NodeID.INVALID.ToString())} reason=committed-before-final context={contextTag}", dedupe: false);
				return true;
			}

			bool preferFinalGoalScopeNode = selectedVehicleMatches
				&& hasPendingRouteState
				&& !startupSuppressed
				&& ShouldPreferFinalGoalForScopePreview(contextTag)
				&& pendingState.GoalNodeID.IsValid
				&& (pendingState.ExpectedNodeID.IsValid || pendingState.ResumeQueued || IsHumanVehicleTravelActive(vehicleId));
			if (preferFinalGoalScopeNode)
			{
				nodeId = pendingState.GoalNodeID;
				source = pendingState.ResumeQueued && !pendingState.ExpectedNodeID.IsValid
					? "queued-final-goal"
					: "route-final-goal";
				LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{selectedSource}:final-first", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={pendingState.GoalNodeID} reason=final-goal-first context={contextTag}", dedupe: false);
				return true;
			}

			bool preferCommittedScopeNode = string.Equals(contextTag, "scope-preview", StringComparison.Ordinal)
				|| string.Equals(contextTag, "scope-feedback", StringComparison.Ordinal)
				|| ShouldPreferCommittedHumanVehicleInteractiveNode();
			if (preferCommittedScopeNode
				&& TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out nodeId, out source)
				&& nodeId.IsValid)
			{
				LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:committed-first", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={(selectedVehicleMatches ? selectedSource : "not-selected")} finalGoal={(hasPendingRouteState ? pendingState.GoalNodeID.ToString() : NodeID.INVALID.ToString())} reason=committed-first", dedupe: false);
				return true;
			}

			if (selectedVehicleMatches
				&& TryGetSelectedHumanVehicleUiAuthorityNodeId(vehicleId, out nodeId, out source, out string selectedUiReason, allowInterruptExpectedBridge: true)
				&& nodeId.IsValid)
			{
				bool transientUiSource = IsTransientScopePreviewAuthoritySource(source);
				if (!transientUiSource
					|| !hasRememberedFinalNode
					|| rememberedFinalNodeId == nodeId
					|| !hasTravelPreviewContext)
				{
					LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{selectedUiReason}", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={(hasPendingRouteState ? pendingState.GoalNodeID.ToString() : NodeID.INVALID.ToString())}", dedupe: false);
					return true;
				}
				LogVehicleAuthority("scope-preview-source-bypassed", $"{vehicleId.id}:{nodeId}:{source}:{rememberedFinalNodeId}", $"scope-preview-source-bypassed vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} rememberedFinal={rememberedFinalNodeId} reason=transient-ui-authority", dedupe: false);
			}

			if (selectedVehicleMatches && hasPendingRouteState && !startupSuppressed)
			{
				bool travelOrQueuedResumeActive = IsHumanVehicleTravelActive(vehicleId) || hasQueuedResumeRoute;
				if (hasPendingState && travelOrQueuedResumeActive)
				{
					nodeId = pendingState.ExpectedNodeID;
					source = hasQueuedResumeRoute ? "queued-resume-expected" : "current-segment";
					LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{selectedSource}", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={pendingState.GoalNodeID}", dedupe: false);
					return true;
				}

				if (travelOrQueuedResumeActive && pendingState.GoalNodeID.IsValid)
				{
					nodeId = pendingState.GoalNodeID;
					source = "selected-final-goal";
					LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{selectedSource}", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={pendingState.GoalNodeID}", dedupe: false);
					return true;
				}

				if (hasQueuedResumeRoute)
				{
					nodeId = pendingState.GoalNodeID;
					source = "queued-resume-goal";
					LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{selectedSource}", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={pendingState.GoalNodeID}", dedupe: false);
					return true;
				}
			}

			if (hasRememberedFinalNode)
			{
				bool canUseRememberedFinalNode = hasTravelPreviewContext;
				if (!canUseRememberedFinalNode)
				{
					canUseRememberedFinalNode = !TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out _)
						|| !liveNodeId.IsValid
						|| liveNodeId == rememberedFinalNodeId;
				}

				if (canUseRememberedFinalNode)
				{
					nodeId = rememberedFinalNodeId;
					source = "selected-final-goal-memory";
					LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{selectedSource}", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={(hasPendingRouteState ? pendingState.GoalNodeID.ToString() : NodeID.INVALID.ToString())}", dedupe: false);
					return true;
				}
			}

			if (TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out nodeId, out source) && nodeId.IsValid)
			{
				LogVehicleAuthority("scope-preview-source", $"{vehicleId.id}:{nodeId}:{source}:committed", $"scope-preview-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={(selectedVehicleMatches ? selectedSource : "not-selected")} finalGoal={(hasPendingRouteState ? pendingState.GoalNodeID.ToString() : NodeID.INVALID.ToString())}", dedupe: false);
				return true;
			}

			if (hasPendingRouteState)
			{
				string reason = !selectedVehicleMatches
					? "not-selected"
					: startupSuppressed
						? "startup-" + startupSuppressionReason
						: "no-preview-authority";
				NodeID pendingNodeId = pendingState.ExpectedNodeID.IsValid ? pendingState.ExpectedNodeID : pendingState.GoalNodeID;
				LogVehicleAuthority("scope-preview-blocked", $"{vehicleId.id}:{pendingNodeId}:{reason}", $"scope-preview-blocked vehicle={vehicleId.id} pendingNode={pendingNodeId} finalGoal={pendingState.GoalNodeID} reason={reason}", dedupe: false);
			}

			return false;
		}

		private static bool IsTransientScopePreviewAuthoritySource(string source)
		{
			return string.Equals(source, "mobile", StringComparison.Ordinal)
				|| string.Equals(source, "recent-finalize", StringComparison.Ordinal)
				|| string.Equals(source, "authoritative", StringComparison.Ordinal);
		}

		internal static bool TryGetHumanVehicleScopePreviewNode(EntityID vehicleId, out Node node, out string source, string contextTag = null)
		{
			node = null;
			source = "none";
			if (!TryGetHumanVehicleScopePreviewNodeId(vehicleId, out NodeID nodeId, out source, contextTag))
			{
				return false;
			}

			node = nodeId.FindNode();
			return node != null;
		}

		internal static bool TryGetHumanVehicleCombatTargetNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "combat-target");

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.ExpectedNodeID.IsValid
				&& (IsHumanVehicleTravelActive(vehicleId) || pendingState.ResumeQueued || pendingState.GoalNodeID.IsValid))
			{
				nodeId = pendingState.ExpectedNodeID;
				source = pendingState.ResumeQueued ? "combat-queued-expected" : "combat-expected";
				LogVehicleAuthority(
					"combat-target-node",
					$"{vehicleId.id}:{nodeId}:{source}",
					$"combat-target-node vehicle={vehicleId.id} node={nodeId} source={source} finalGoal={pendingState.GoalNodeID}",
					dedupe: true);
				return true;
			}

			if (TryGetVehicleLiveAuthorityNodeId(vehicleId, out nodeId, out string liveSource) && nodeId.IsValid)
			{
				source = "combat-" + liveSource;
				LogVehicleAuthority(
					"combat-target-node",
					$"{vehicleId.id}:{nodeId}:{source}",
					$"combat-target-node vehicle={vehicleId.id} node={nodeId} source={source}",
					dedupe: true);
				return true;
			}

			return false;
		}

		private static bool ShouldPreferCommittedHumanVehicleInteractiveNode()
		{
			return IsPendingPresenceSelectionScopeActive() || IsPendingBuildingInteractionScopeActive();
		}

		private static bool TryGetSelectedHumanVehicleActiveSegmentNodeId(EntityID vehicleId, PendingVehicleTravelState pendingState, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid
				|| !pendingState.ExpectedNodeID.IsValid)
			{
				return false;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null
				|| !TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out string selectedSource)
				|| selectedVehicleId != vehicleId)
			{
				return false;
			}

			bool sameAsGoal = pendingState.GoalNodeID.IsValid && pendingState.GoalNodeID == pendingState.ExpectedNodeID;
			bool activeTravel = IsHumanVehicleTravelActive(vehicleId);
			bool queuedResume = pendingState.ResumeQueued && pendingState.GoalNodeID.IsValid;
			if (!activeTravel && !queuedResume)
			{
				return false;
			}

			nodeId = pendingState.ExpectedNodeID;
			source = sameAsGoal ? "selected-final-goal" : "selected-current-segment";
			LogVehicleAuthority(
				"selected-segment-authority-source",
				$"{vehicleId.id}:{nodeId}:{source}:{selectedSource}",
				$"selected-segment-authority-source vehicle={vehicleId.id} node={nodeId} source={source} selectedSource={selectedSource} finalGoal={pendingState.GoalNodeID}");
			return true;
		}

		internal static bool TryGetCommittedHumanVehicleInteractiveNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "committed-interactive");

			bool hasPendingState = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.ExpectedNodeID.IsValid;
			string startupSuppressionReason = "none";
			bool startupSuppressed = hasPendingState
				&& TryGetFreshStartupHumanVehicleAuthoritySuppression(vehicleId, pendingState, out startupSuppressionReason);

			if (TryGetQueuedArrivalCommittedNodeId(vehicleId, out nodeId) && nodeId.IsValid)
			{
				source = "queued-arrival";
				return true;
			}

			if (hasPendingState
				&& !startupSuppressed
				&& TryGetLiveArrivalBridgeNodeId(vehicleId, pendingState, out nodeId, out source))
			{
				return true;
			}

			if (hasPendingState
				&& !startupSuppressed
				&& IsHumanVehicleTravelActive(vehicleId)
				&& pendingState.GoalNodeID.IsValid)
			{
				nodeId = pendingState.ExpectedNodeID;
				source = pendingState.GoalNodeID == pendingState.ExpectedNodeID ? "current-final-goal" : "current-segment";
				LogVehicleAuthority(
					"active-route-authority-source",
					$"{vehicleId.id}:{nodeId}:{source}:{pendingState.GoalNodeID}",
					$"active-route-authority-source vehicle={vehicleId.id} node={nodeId} source={source} finalGoal={pendingState.GoalNodeID}",
					dedupe: false);
				return true;
			}

			if (hasPendingState
				&& !startupSuppressed
				&& TryGetSelectedHumanVehicleActiveSegmentNodeId(vehicleId, pendingState, out nodeId, out source))
			{
				return true;
			}

			if (TryGetFreshStartupStaleRecentFinalizeBypassNodeId(vehicleId, out nodeId, out source, out string bypassReason) && nodeId.IsValid)
			{
				LogVehicleAuthority("startup-authority-rebased", $"{vehicleId.id}:{nodeId}:{source}:{bypassReason}", $"startup-authority-rebased vehicle={vehicleId.id} node={nodeId} source={source} reason={bypassReason}", dedupe: false);
				return true;
			}

			if (TryGetSelectedHumanVehicleUiAuthorityNodeId(vehicleId, out nodeId, out source, out string uiReason, allowInterruptExpectedBridge: true) && nodeId.IsValid)
			{
				LogVehicleAuthority("selected-ui-authority-source", $"{vehicleId.id}:{nodeId}:{source}:{uiReason}", $"selected-ui-authority-source vehicle={vehicleId.id} node={nodeId} source={source} reason={uiReason}", dedupe: false);
				return true;
			}

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState committedPendingState)
				&& committedPendingState.GoalNodeID.IsValid
				&& committedPendingState.StartNodeID.IsValid
				&& !committedPendingState.ExpectedNodeID.IsValid)
			{
				if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId)
					&& recentFinalizedNodeId.IsValid
					&& recentFinalizedNodeId != committedPendingState.StartNodeID)
				{
					LogVehicleAuthority(
						"recent-finalize-authority-suppressed",
						$"{vehicleId.id}:{recentFinalizedNodeId}:{committedPendingState.StartNodeID}:{committedPendingState.GoalNodeID}",
						$"recent-finalize-authority-suppressed vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} committedNode={committedPendingState.StartNodeID} finalGoal={committedPendingState.GoalNodeID}",
						dedupe: false);
				}

				nodeId = committedPendingState.StartNodeID;
				source = "pending-start";
				return true;
			}

			if (TryGetAuthoritativeVehicleNodeId(vehicleId, out nodeId, out string authoritySource) && nodeId.IsValid)
			{
				if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID staleRecentNodeId)
					&& staleRecentNodeId.IsValid
					&& staleRecentNodeId != nodeId)
				{
					ClearRecentFinalizedNode(vehicleId, "committed-live-authority");
					LogVehicleAuthority(
						"committed-authority-bypassed",
						$"{vehicleId.id}:{staleRecentNodeId}:{nodeId}:{authoritySource}",
						$"committed-authority-bypassed vehicle={vehicleId.id} staleNode={staleRecentNodeId} authoritativeNode={nodeId} authoritativeSource={authoritySource}",
						dedupe: false);
				}

				source = string.IsNullOrWhiteSpace(authoritySource) ? "authoritative" : authoritySource;
				return true;
			}

			if (TryGetRecentFinalizedNodeId(vehicleId, out nodeId) && nodeId.IsValid)
			{
				source = "recent-finalize";
				return true;
			}

			return false;
		}

		private static bool TryGetSelectedHumanVehicleUiAuthorityNodeId(EntityID vehicleId, out NodeID nodeId, out string source, out string reason, bool allowInterruptExpectedBridge = false)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			reason = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "selected-ui");

			Entity vehicle = vehicleId.FindEntity();
			PlayerID pid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
			if (!pid.IsHumanPlayer)
			{
				return false;
			}

			PlayerCrew crew = pid.FindPlayer()?.crew;
			if (crew == null
				|| !TryGetSelectedHumanVehicleForMapScope(crew, out EntityID selectedVehicleId, out string selectedSource)
				|| selectedVehicleId != vehicleId)
			{
				return false;
			}

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& (pendingState.ExpectedNodeID.IsValid
					|| IsHumanVehicleTravelActive(vehicleId)
					|| (pendingState.ResumeQueued && pendingState.GoalNodeID.IsValid)))
			{
				return false;
			}

			bool hasRecentFinalized = TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId) && recentFinalizedNodeId.IsValid;
			bool hasRememberedFinalNode = GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(vehicleId, out NodeID rememberedFinalNodeId)
				&& rememberedFinalNodeId.IsValid;
			bool hasInterruptExpectedNode = TryGetInterruptExpectedStartNode(vehicleId, out NodeID interruptExpectedNodeId)
				&& interruptExpectedNodeId.IsValid;
			if (TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource) && liveNodeId.IsValid)
			{
				bool liveMobileSource = string.Equals(liveSource, "mobile", StringComparison.Ordinal);
				bool liveMobileTransient = liveMobileSource
					&& (IsHumanVehicleTravelActive(vehicleId) || HasQueuedHumanVehiclePendingResume(vehicleId));
				bool useInterruptExpectedBridge = allowInterruptExpectedBridge
					&& hasInterruptExpectedNode
					&& interruptExpectedNodeId != liveNodeId
					&& liveMobileTransient;
				if (useInterruptExpectedBridge)
				{
					nodeId = interruptExpectedNodeId;
					source = "interrupt-expected";
					reason = "post-stop-mobile-gap";
					LogVehicleAuthority("selected-ui-authority-interrupt-bridge", $"{vehicleId.id}:{interruptExpectedNodeId}:{liveNodeId}:{selectedSource}", $"selected-ui-authority-interrupt-bridge vehicle={vehicleId.id} interruptNode={interruptExpectedNodeId} liveNode={liveNodeId} liveSource={liveSource} selectedSource={selectedSource}", dedupe: false);
					return true;
				}

				if (hasInterruptExpectedNode && interruptExpectedNodeId == liveNodeId)
				{
					ClearInterruptExpectedStartNode(vehicleId, "selected-ui-live-caught-up");
					hasInterruptExpectedNode = false;
				}

				bool allowMemoryBridge = hasRememberedFinalNode
					&& hasRecentFinalized
					&& rememberedFinalNodeId != liveNodeId
					&& rememberedFinalNodeId == recentFinalizedNodeId
					&& !liveMobileSource;
				if (allowMemoryBridge)
				{
					nodeId = rememberedFinalNodeId;
					source = "selected-final-goal-memory";
					reason = "recent-finalize-preview-gap";
					LogVehicleAuthority("selected-ui-authority-memory-bridge", $"{vehicleId.id}:{recentFinalizedNodeId}:{liveNodeId}:{rememberedFinalNodeId}:{selectedSource}", $"selected-ui-authority-memory-bridge vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} liveNode={liveNodeId} rememberedFinal={rememberedFinalNodeId} selectedSource={selectedSource}", dedupe: false);
					return true;
				}

				bool allowPostFinalizeMobileBridge = hasRememberedFinalNode
					&& hasRecentFinalized
					&& rememberedFinalNodeId != liveNodeId
					&& rememberedFinalNodeId == recentFinalizedNodeId
					&& liveMobileTransient;
				if (allowPostFinalizeMobileBridge)
				{
					nodeId = rememberedFinalNodeId;
					source = "selected-final-goal-memory";
					reason = "post-finalize-mobile-gap";
					LogVehicleAuthority("selected-ui-authority-finalize-bridge", $"{vehicleId.id}:{recentFinalizedNodeId}:{liveNodeId}:{rememberedFinalNodeId}:{selectedSource}", $"selected-ui-authority-finalize-bridge vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} liveNode={liveNodeId} rememberedFinal={rememberedFinalNodeId} liveSource={liveSource} selectedSource={selectedSource}", dedupe: false);
					return true;
				}

				int stableBridgeFrameDelta = int.MaxValue;
				bool allowStableRecentFinalizeBridge = hasRecentFinalized
					&& recentFinalizedNodeId.IsValid
					&& recentFinalizedNodeId != liveNodeId
					&& liveMobileSource
					&& !IsHumanVehicleTravelActive(vehicleId)
					&& !HasQueuedHumanVehiclePendingResume(vehicleId)
					&& IsRecentFinalizedStableBridgeFresh(vehicleId, RecentFinalizeStableBridgeMaxFrames, out stableBridgeFrameDelta)
					&& AreNodesWithinStableFinalizeBridgeRange(recentFinalizedNodeId, liveNodeId);
				if (allowStableRecentFinalizeBridge)
				{
					nodeId = hasRememberedFinalNode && rememberedFinalNodeId == recentFinalizedNodeId
						? rememberedFinalNodeId
						: recentFinalizedNodeId;
					source = hasRememberedFinalNode && rememberedFinalNodeId == recentFinalizedNodeId
						? "selected-final-goal-memory"
						: "recent-finalize";
					reason = "post-finalize-mobile-stale";
					LogVehicleAuthority(
						"selected-ui-authority-stable-finalize-bridge",
						$"{vehicleId.id}:{recentFinalizedNodeId}:{liveNodeId}:{source}:{selectedSource}",
						$"selected-ui-authority-stable-finalize-bridge vehicle={vehicleId.id} finalizedNode={recentFinalizedNodeId} liveNode={liveNodeId} liveSource={liveSource} selectedSource={selectedSource} resolvedSource={source} frameDelta={stableBridgeFrameDelta}",
						dedupe: true);
					return true;
				}

				bool staleStableRecentFinalizeBridge = hasRecentFinalized
					&& recentFinalizedNodeId.IsValid
					&& recentFinalizedNodeId != liveNodeId
					&& liveMobileSource
					&& !IsHumanVehicleTravelActive(vehicleId)
					&& !HasQueuedHumanVehiclePendingResume(vehicleId);
				if (staleStableRecentFinalizeBridge)
				{
					if (hasRememberedFinalNode && rememberedFinalNodeId == recentFinalizedNodeId)
					{
						GameplayTweaksPlugin.ClearSelectedVehicleUiFinalNode(vehicleId, "selected-ui-stale-finalize-bridge");
						hasRememberedFinalNode = false;
					}

					ClearRecentFinalizedNode(vehicleId, "selected-ui-stale-finalize-bridge");
					hasRecentFinalized = false;
					LogVehicleAuthority(
						"selected-ui-authority-stale-finalize-cleared",
						$"{vehicleId.id}:{recentFinalizedNodeId}:{liveNodeId}:{selectedSource}",
						$"selected-ui-authority-stale-finalize-cleared vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} liveNode={liveNodeId} liveSource={liveSource} selectedSource={selectedSource}",
						dedupe: false);
				}

				bool preserveRememberedFinalNode = hasRecentFinalized
					|| HasQueuedHumanVehiclePendingResume(vehicleId);
				if (hasRememberedFinalNode && rememberedFinalNodeId != liveNodeId && !preserveRememberedFinalNode)
				{
					GameplayTweaksPlugin.ClearSelectedVehicleUiFinalNode(vehicleId, "selected-ui-live-authority");
					LogVehicleAuthority("selected-ui-authority-memory-cleared", $"{vehicleId.id}:{rememberedFinalNodeId}:{liveNodeId}:{liveSource}:{selectedSource}", $"selected-ui-authority-memory-cleared vehicle={vehicleId.id} rememberedFinal={rememberedFinalNodeId} liveNode={liveNodeId} liveSource={liveSource} selectedSource={selectedSource}", dedupe: false);
				}

				nodeId = liveNodeId;
				source = liveSource;
				reason = hasRecentFinalized && recentFinalizedNodeId != liveNodeId
					? "stale-recent-finalize"
					: "live-authority";

				if (hasRecentFinalized && recentFinalizedNodeId != liveNodeId)
				{
					ClearRecentFinalizedNode(vehicleId, "selected-ui-live-authority");
					LogVehicleAuthority("selected-ui-authority-bypassed", $"{vehicleId.id}:{recentFinalizedNodeId}:{liveNodeId}:{selectedSource}", $"selected-ui-authority-bypassed vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} authoritativeNode={liveNodeId} authoritativeSource={liveSource} selectedSource={selectedSource}", dedupe: false);

					Node staleNode = recentFinalizedNodeId.FindNode();
					Node liveNode = liveNodeId.FindNode();
					if (liveNode != null && (!hasRememberedFinalNode || rememberedFinalNodeId == liveNodeId))
					{
						GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(staleNode, liveNode, "selected-ui-authority-rebased", vehicleId);
						LogVehicleAuthority("selected-ui-building-picks-refreshed", $"{vehicleId.id}:{recentFinalizedNodeId}:{liveNodeId}", $"selected-ui-building-picks-refreshed vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} authoritativeNode={liveNodeId}", dedupe: false);
					}
				}

				return true;
			}

			if (hasInterruptExpectedNode)
			{
				nodeId = interruptExpectedNodeId;
				source = "interrupt-expected";
				reason = "post-stop-fallback";
				return true;
			}

			if (hasRecentFinalized)
			{
				nodeId = recentFinalizedNodeId;
				source = "recent-finalize";
				reason = "recent-finalize-fallback";
				return true;
			}

			return false;
		}

		private static bool AreNodesWithinStableFinalizeBridgeRange(NodeID priorNodeId, NodeID liveNodeId)
		{
			if (!priorNodeId.IsValid || !liveNodeId.IsValid)
			{
				return false;
			}

			if (priorNodeId == liveNodeId
				|| AreNodesDirectlyAdjacent(priorNodeId, liveNodeId))
			{
				return true;
			}

			Node priorNode = priorNodeId.FindNode();
			Node liveNode = liveNodeId.FindNode();
			if (priorNode == null || liveNode == null)
			{
				return false;
			}

			return (priorNode.pos - liveNode.pos).Magnitude <= 18f;
		}

		private static bool TryGetFreshStartupStaleRecentFinalizeBypassNodeId(EntityID vehicleId, out NodeID nodeId, out string source, out string reason)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			reason = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			Entity vehicle = vehicleId.FindEntity();
			PlayerID pid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
			if (!pid.IsHumanPlayer || !IsFreshHumanStartup(pid))
			{
				return false;
			}

			PlayerCrew crew = pid.FindPlayer()?.crew;
			if (crew == null
				|| !TryGetSelectedHumanVehicleForMapScope(crew, out EntityID selectedVehicleId, out string selectedSource)
				|| selectedVehicleId != vehicleId
				|| !string.Equals(selectedSource, "selection-mobile", StringComparison.Ordinal))
			{
				return false;
			}

			if (!TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId) || !recentFinalizedNodeId.IsValid)
			{
				return false;
			}

			if (GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(vehicleId, out NodeID rememberedFinalNodeId)
				&& rememberedFinalNodeId.IsValid
				&& rememberedFinalNodeId == recentFinalizedNodeId)
			{
				return false;
			}

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& (pendingState.ExpectedNodeID.IsValid
					|| (pendingState.ResumeQueued && pendingState.GoalNodeID.IsValid)))
			{
				return false;
			}

			if (!TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID authoritativeNodeId, out string authoritativeSource)
				|| !authoritativeNodeId.IsValid
				|| authoritativeNodeId == recentFinalizedNodeId)
			{
				return false;
			}

			reason = "stale-recent-finalize";
			ClearRecentFinalizedNode(vehicleId, "startup-stale-preview-authority");
			nodeId = authoritativeNodeId;
			source = string.IsNullOrWhiteSpace(authoritativeSource) ? "authoritative" : authoritativeSource;
			LogVehicleAuthority("startup-preview-authority-bypassed", $"{vehicleId.id}:{recentFinalizedNodeId}:{authoritativeNodeId}:{selectedSource}", $"startup-preview-authority-bypassed vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} authoritativeNode={authoritativeNodeId} authoritativeSource={source} selectedSource={selectedSource}", dedupe: false);

			Node staleNode = recentFinalizedNodeId.FindNode();
			Node authoritativeNode = authoritativeNodeId.FindNode();
			if (authoritativeNode != null)
			{
				GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(staleNode, authoritativeNode, "startup-authority-rebased", vehicleId);
				LogVehicleAuthority("startup-building-picks-refreshed", $"{vehicleId.id}:{recentFinalizedNodeId}:{authoritativeNodeId}", $"startup-building-picks-refreshed vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} authoritativeNode={authoritativeNodeId}", dedupe: false);
			}

			return true;
		}

		internal static bool TryGetCommittedHumanVehicleInteractiveNode(EntityID vehicleId, out Node node, out string source)
		{
			node = null;
			source = "none";
			if (!TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out NodeID nodeId, out source))
			{
				return false;
			}

			node = nodeId.FindNode();
			return node != null;
		}

		internal static bool TryGetPendingHumanVehicleInteractiveNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "pending-interactive");

			bool hasQueuedGoal = TryGetQueuedHumanVehicleGoal(vehicleId, out _, out NodeID goalNodeId) && goalNodeId.IsValid;
			bool hasSelectedPendingFinalGoal = TryGetSelectedHumanVehiclePendingFinalGoalNodeId(vehicleId, out NodeID selectedPendingFinalGoalNodeId, out _)
				&& selectedPendingFinalGoalNodeId.IsValid;
			bool preferCommittedNode = ShouldPreferCommittedHumanVehicleInteractiveNode();
			bool hasCommittedPendingStart = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState committedPendingState)
				&& committedPendingState.StartNodeID.IsValid
				&& committedPendingState.GoalNodeID.IsValid
				&& !committedPendingState.ExpectedNodeID.IsValid;
			bool allowQueuedFinalGoalPreview = IsPendingBuildingInteractionScopeActive()
				|| !TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState queuedPreviewPendingState)
				|| !queuedPreviewPendingState.ExpectedNodeID.IsValid
				|| !queuedPreviewPendingState.GoalNodeID.IsValid
				|| queuedPreviewPendingState.ExpectedNodeID == queuedPreviewPendingState.GoalNodeID;
			if (TryGetQueuedArrivalCommittedNodeId(vehicleId, out nodeId) && nodeId.IsValid)
			{
				source = "queued-arrival";
				LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source}");
				return true;
			}

			if (allowQueuedFinalGoalPreview && IsPendingBuildingInteractionScopeActive() && (hasQueuedGoal || hasSelectedPendingFinalGoal))
			{
				nodeId = hasSelectedPendingFinalGoal ? selectedPendingFinalGoalNodeId : goalNodeId;
				source = "queued-final-goal";
				LogVehicleAuthority("final-goal-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{goalNodeId}", $"final-goal-preview-source vehicle={vehicleId.id} node={nodeId} source={source} finalGoal={nodeId}", dedupe: false);
				LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source}");
				return true;
			}

			if (preferCommittedNode)
			{
				if (TryGetSelectedHumanVehicleUiAuthorityNodeId(vehicleId, out nodeId, out source, out string selectedUiReason, allowInterruptExpectedBridge: true) && nodeId.IsValid)
				{
					LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}:{selectedUiReason}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source} reason={selectedUiReason}");
					return true;
				}

				if (hasCommittedPendingStart)
				{
					if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId)
						&& recentFinalizedNodeId.IsValid
						&& recentFinalizedNodeId != committedPendingState.StartNodeID)
					{
						LogVehicleAuthority(
							"recent-finalize-authority-suppressed",
							$"{vehicleId.id}:{recentFinalizedNodeId}:{committedPendingState.StartNodeID}:{committedPendingState.GoalNodeID}:interactive",
							$"recent-finalize-authority-suppressed vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} committedNode={committedPendingState.StartNodeID} finalGoal={committedPendingState.GoalNodeID} phase=interactive-node",
							dedupe: false);
					}

					nodeId = committedPendingState.StartNodeID;
					source = "pending-start";
					LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source}");
					return true;
				}

				if (TryGetAuthoritativeVehicleNodeId(vehicleId, out nodeId, out string authoritySource) && nodeId.IsValid)
				{
					source = "authoritative";
					LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}:{authoritySource}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source} authoritySource={authoritySource}");
					return true;
				}
			}

			if (IsHumanVehicleTravelActive(vehicleId)
				&& TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.ExpectedNodeID.IsValid
				&& !TryGetFreshStartupHumanVehicleAuthoritySuppression(vehicleId, pendingState, out string startupSuppressionReason))
			{
				nodeId = pendingState.ExpectedNodeID;
				source = "current-segment";
				if (hasQueuedGoal)
				{
					LogVehicleAuthority("interactive-goal-suppressed", $"{vehicleId.id}:{goalNodeId}:{nodeId}:current-segment", $"interactive-goal-suppressed vehicle={vehicleId.id} queuedGoal={goalNodeId} liveNode={nodeId} source=current-segment");
				}
				LogVehicleAuthority("final-goal-preview-source", $"{vehicleId.id}:{nodeId}:{source}:{goalNodeId}", $"final-goal-preview-source vehicle={vehicleId.id} node={nodeId} source={source} finalGoal={goalNodeId}", dedupe: false);
				LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source}");
				return true;
			}
			else if (IsHumanVehicleTravelActive(vehicleId)
				&& TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState suppressedState)
				&& suppressedState.ExpectedNodeID.IsValid
				&& TryGetFreshStartupHumanVehicleAuthoritySuppression(vehicleId, suppressedState, out startupSuppressionReason))
			{
				LogVehicleAuthority("startup-authority-suppressed", $"{vehicleId.id}:{startupSuppressionReason}:{suppressedState.ExpectedNodeID}", $"startup-authority-suppressed vehicle={vehicleId.id} reason={startupSuppressionReason} expectedNode={suppressedState.ExpectedNodeID} goalNode={suppressedState.GoalNodeID} commandOwner={suppressedState.CommandOwnerKey}", dedupe: false);
			}

			if (hasQueuedGoal)
			{
				LogVehicleAuthority("interactive-goal-suppressed", $"{vehicleId.id}:{goalNodeId}:no-live-scope", $"interactive-goal-suppressed vehicle={vehicleId.id} queuedGoal={goalNodeId} liveNode={NodeID.INVALID} source=none");
			}

			if (TryGetSelectedHumanVehicleUiAuthorityNodeId(vehicleId, out nodeId, out source, out string finalUiReason, allowInterruptExpectedBridge: true) && nodeId.IsValid)
			{
				LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}:{finalUiReason}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source} reason={finalUiReason}");
				return true;
			}

			if (hasCommittedPendingStart)
			{
				if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId)
					&& recentFinalizedNodeId.IsValid
					&& recentFinalizedNodeId != committedPendingState.StartNodeID)
				{
					LogVehicleAuthority(
						"recent-finalize-authority-suppressed",
						$"{vehicleId.id}:{recentFinalizedNodeId}:{committedPendingState.StartNodeID}:{committedPendingState.GoalNodeID}:interactive-fallback",
						$"recent-finalize-authority-suppressed vehicle={vehicleId.id} staleNode={recentFinalizedNodeId} committedNode={committedPendingState.StartNodeID} finalGoal={committedPendingState.GoalNodeID} phase=interactive-fallback",
						dedupe: false);
				}

				nodeId = committedPendingState.StartNodeID;
				source = "pending-start";
				LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source}");
				return true;
			}

			if (TryGetAuthoritativeVehicleNodeId(vehicleId, out nodeId, out string liveAuthoritySource) && nodeId.IsValid)
			{
				source = "authoritative";
				LogVehicleAuthority("interactive-node", $"{vehicleId.id}:{nodeId}:{source}:{liveAuthoritySource}", $"interactive-node vehicle={vehicleId.id} node={nodeId} source={source} authoritySource={liveAuthoritySource}");
				return true;
			}

			return false;
		}

		private static bool TryGetLiveArrivalBridgeNodeId(EntityID vehicleId, PendingVehicleTravelState pendingState, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid
				|| !pendingState.ExpectedNodeID.IsValid
				|| !IsHumanVehicleTravelActive(vehicleId))
			{
				return false;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null
				|| !TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out _)
				|| selectedVehicleId != vehicleId)
			{
				return false;
			}

			if (TryResolveVehicleArrivalBridgeNodeId(vehicleId.FindEntity(), pendingState.ExpectedNodeID, out nodeId))
			{
				source = "arrival-bridge";
			}
			else
			{
				EntityID driverPeepId = GetDriverPeepId(humanCrew, vehicleId);
				Entity driverPeep = driverPeepId.IsValid ? driverPeepId.FindEntity() : null;
				if (!TryResolveVehicleArrivalBridgeNodeId(driverPeep, pendingState.ExpectedNodeID, out nodeId))
				{
					return false;
				}
				source = "live-arrival-bridge";
			}

			SetRecentFinalizedNode(vehicleId, nodeId);
			LogVehicleAuthority("arrival-bridge-source", $"{vehicleId.id}:{nodeId}:{source}", $"arrival-bridge-source vehicle={vehicleId.id} node={nodeId} source={source} expectedNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID}", dedupe: false);
			return true;
		}

		private static bool TryResolveVehicleArrivalBridgeNodeId(Entity entity, NodeID expectedNodeId, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			if (entity?.data?.board == null || !expectedNodeId.IsValid || global::Game.Game.ctx?.board?.nodes == null)
			{
				return false;
			}

			try
			{
				WorldPos worldPos = entity.data.board.worldpos;
				if (worldPos.IsZero)
				{
					return false;
				}

				Node snappedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(worldPos, 5f);
				if (snappedNode == null || snappedNode.id != expectedNodeId)
				{
					return false;
				}

				nodeId = snappedNode.id;
				return true;
			}
			catch
			{
				nodeId = NodeID.INVALID;
				return false;
			}
		}

		internal static bool IsHumanVehiclePhysicallyAtNode(EntityID vehicleId, NodeID nodeId)
		{
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}
			if (ShouldSkipForAfterProhibitionRoutesBehaviorOwner("vehicle-node-authority", "physical-check"))
			{
				return false;
			}

			LogAfterProhibitionRoutesVehicleNodeAuthorityDecision(vehicleId, nodeId, "physical-check");

			if (TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource)
				&& liveNodeId == nodeId)
			{
				if (string.Equals(liveSource, "mobile", StringComparison.Ordinal))
				{
					RecordObservedHumanVehicleReachedNode(vehicleId, nodeId, liveSource);
				}
				LogVehicleAuthority("vehicle-node-reached", $"{vehicleId.id}:{nodeId}:{liveSource}", $"vehicle-node-reached vehicle={vehicleId.id} node={nodeId} source={liveSource}", dedupe: true);
				return true;
			}

			return TryResolveVehicleArrivalBridgeNodeId(vehicleId.FindEntity(), nodeId, out NodeID snappedNodeId)
				&& snappedNodeId == nodeId;
		}

		private static bool TryGetHumanVehiclePhysicalNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid || global::Game.Game.ctx?.board?.nodes == null)
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			int frame = Time.frameCount;
			if (_vehiclePhysicalNodeCacheByVehicleId.TryGetValue(vehicleKey, out CachedVehicleNodeAuthority cached)
				&& cached.Frame == frame)
			{
				nodeId = cached.NodeId;
				source = cached.Source ?? "none";
				return cached.HasNode;
			}

			Entity vehicle = vehicleId.FindEntity();
			if (vehicle?.data?.board == null)
			{
				_vehiclePhysicalNodeCacheByVehicleId[vehicleKey] = new CachedVehicleNodeAuthority
				{
					Frame = frame,
					HasNode = false,
					NodeId = NodeID.INVALID,
					Source = "none"
				};
				return false;
			}

			try
			{
				WorldPos worldPos = vehicle.data.board.worldpos;
				if (worldPos.IsZero)
				{
					_vehiclePhysicalNodeCacheByVehicleId[vehicleKey] = new CachedVehicleNodeAuthority
					{
						Frame = frame,
						HasNode = false,
						NodeId = NodeID.INVALID,
						Source = "none"
					};
					return false;
				}

				Node snappedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(worldPos, 5f);
				if (snappedNode == null || !snappedNode.id.IsValid)
				{
					_vehiclePhysicalNodeCacheByVehicleId[vehicleKey] = new CachedVehicleNodeAuthority
					{
						Frame = frame,
						HasNode = false,
						NodeId = NodeID.INVALID,
						Source = "none"
					};
					return false;
				}

				nodeId = snappedNode.id;
				source = "physical";
				_vehiclePhysicalNodeCacheByVehicleId[vehicleKey] = new CachedVehicleNodeAuthority
				{
					Frame = frame,
					HasNode = true,
					NodeId = nodeId,
					Source = source
				};
				return true;
			}
			catch
			{
				nodeId = NodeID.INVALID;
				source = "none";
				_vehiclePhysicalNodeCacheByVehicleId[vehicleKey] = new CachedVehicleNodeAuthority
				{
					Frame = frame,
					HasNode = false,
					NodeId = NodeID.INVALID,
					Source = "none"
				};
				return false;
			}
		}

		internal static bool IsHumanVehicleStrictlyPhysicalAtNode(EntityID vehicleId, NodeID nodeId, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}
			if (ShouldSkipForAfterProhibitionRoutesBehaviorOwner("vehicle-node-authority", "strict-physical-check"))
			{
				return false;
			}

			LogAfterProhibitionRoutesVehicleNodeAuthorityDecision(vehicleId, nodeId, "strict-physical-check");

			if (!TryGetHumanVehiclePhysicalNodeId(vehicleId, out NodeID physicalNodeId, out string physicalSource)
				|| physicalNodeId != nodeId)
			{
				return false;
			}

			source = string.IsNullOrWhiteSpace(physicalSource) ? "physical" : physicalSource;
			LogVehicleAuthority(
				"vehicle-node-strict-physical",
				$"{vehicleId.id}:{nodeId}:{source}",
				$"vehicle-node-strict-physical vehicle={vehicleId.id} node={nodeId} source={source}",
				dedupe: true);
			return true;
		}

		internal static bool TryGetStrictPhysicalVehicleNode(EntityID vehicleId, out Node node, out string source)
		{
			node = null;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			if (!TryGetHumanVehiclePhysicalNodeId(vehicleId, out NodeID nodeId, out source) || !nodeId.IsValid)
			{
				return false;
			}

			node = nodeId.FindNode();
			if (node == null)
			{
				source = "none";
				return false;
			}

			LogVehicleAuthority(
				"vehicle-node-strict-physical",
				$"{vehicleId.id}:{nodeId}:{source}",
				$"vehicle-node-strict-physical vehicle={vehicleId.id} node={nodeId} source={source}",
				dedupe: true);
			return true;
		}

		internal static bool IsHumanVehicleStrictlyPhysicalAtNode(EntityID vehicleId, NodeID nodeId)
		{
			return IsHumanVehicleStrictlyPhysicalAtNode(vehicleId, nodeId, out _);
		}

		private static void RecordObservedHumanVehicleReachedNode(EntityID vehicleId, NodeID nodeId, string source)
		{
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			_observedHumanVehicleReachedNodeByVehicleId[vehicleKey] = nodeId;
			_observedHumanVehicleReachedFrameByVehicleId[vehicleKey] = Time.frameCount;
			_observedHumanVehicleReachedSourceByVehicleId[vehicleKey] = string.IsNullOrWhiteSpace(source) ? "mobile" : source;
		}

		internal static bool TryGetObservedHumanVehicleReachedNode(EntityID vehicleId, NodeID nodeId, int minimumFrame, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}

			LogAfterProhibitionRoutesVehicleNodeAuthorityDecision(vehicleId, nodeId, "observed-arrival-check");

			long vehicleKey = (long)vehicleId.id;
			if (!_observedHumanVehicleReachedNodeByVehicleId.TryGetValue(vehicleKey, out NodeID observedNodeId)
				|| observedNodeId != nodeId)
			{
				return false;
			}

			if (!_observedHumanVehicleReachedFrameByVehicleId.TryGetValue(vehicleKey, out int observedFrame)
				|| observedFrame < minimumFrame)
			{
				return false;
			}

			if (!_observedHumanVehicleReachedSourceByVehicleId.TryGetValue(vehicleKey, out source)
				|| string.IsNullOrWhiteSpace(source))
			{
				source = "mobile";
			}
			return true;
		}

		internal static bool IsHumanVehicleSettledAtNodeForAction(EntityID vehicleId, NodeID nodeId, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}

			if (IsHumanVehicleStrictlyPhysicalAtNode(vehicleId, nodeId, out string strictSource))
			{
				source = strictSource;
				return true;
			}

			bool hasRouteState = IsHumanVehicleTravelActive(vehicleId)
				|| HasQueuedHumanVehiclePendingResume(vehicleId)
				|| (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
					&& (pendingState.ExpectedNodeID.IsValid || pendingState.GoalNodeID.IsValid || pendingState.ResumeQueued));
			if (hasRouteState)
			{
				return false;
			}

			if (TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource)
				&& liveNodeId == nodeId)
			{
				source = "settled-" + (string.IsNullOrWhiteSpace(liveSource) ? "live" : liveSource);
				LogVehicleAuthority(
					"vehicle-node-settled-action",
					$"{vehicleId.id}:{nodeId}:{source}",
					$"vehicle-node-settled-action vehicle={vehicleId.id} node={nodeId} source={source}",
					dedupe: true);
				return true;
			}

			return false;
		}

		internal static bool IsHumanVehicleSettledAtNodeForAction(EntityID vehicleId, NodeID nodeId)
		{
			return IsHumanVehicleSettledAtNodeForAction(vehicleId, nodeId, out _);
		}

		internal static bool IsHumanVehicleAtOwnedBuildingAccessNode(EntityID vehicleId, NodeID nodeId)
		{
			return IsHumanVehicleAtOwnedBuildingAccessNode(vehicleId, nodeId, out _);
		}

		internal static bool IsHumanVehicleAtOwnedBuildingAccessNode(EntityID vehicleId, NodeID nodeId, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}

			if (IsHumanVehiclePhysicallyAtNode(vehicleId, nodeId))
			{
				source = "physical";
				return true;
			}

			bool hasPendingRouteState = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState);
			bool routeAuthorityInFlight = IsHumanVehicleTravelActive(vehicleId)
				|| HasQueuedHumanVehiclePendingResume(vehicleId)
				|| (hasPendingRouteState
					&& (pendingState.ExpectedNodeID.IsValid
						|| (pendingState.ResumeQueued && pendingState.GoalNodeID.IsValid)));
			if (routeAuthorityInFlight)
			{
				return false;
			}

			if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId)
				&& recentFinalizedNodeId == nodeId)
			{
				source = "recent-finalize";
				return true;
			}

			if (TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out NodeID committedNodeId, out string committedSource)
				&& committedNodeId == nodeId)
			{
				source = "committed-" + committedSource;
				return true;
			}

			if (TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource)
				&& liveNodeId == nodeId)
			{
				source = "live-" + liveSource;
				return true;
			}

			return false;
		}

		internal static bool IsHumanVehicleRouteSimAccessNode(EntityID vehicleId, NodeID nodeId, string actionTag, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}
			if (ShouldSkipForAfterProhibitionRoutesBehaviorOwner("route-sim-access", actionTag))
			{
				return false;
			}

			LogAfterProhibitionRoutesRouteSimAccessDecision(vehicleId, nodeId, actionTag);

			if (!(GameplayTweaksPlugin.EnableRouteSimulatedConvenienceActions?.Value ?? true))
			{
				LogVehicleAuthority(
					"route-sim-disabled",
					$"{vehicleId.id}:{nodeId}:{actionTag}",
					$"route-sim-disabled vehicle={vehicleId.id} node={nodeId} action={actionTag} reason=config-disabled",
					dedupe: true);
				return false;
			}

			if (!TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState))
			{
				return false;
			}

			bool routeActive = IsHumanVehicleTravelActive(vehicleId)
				|| pendingState.ResumeQueued
				|| HasQueuedHumanVehiclePendingResume(vehicleId);
			if (!routeActive)
			{
				return false;
			}

			if (pendingState.ExpectedNodeID == nodeId)
			{
				source = "route-sim-expected";
			}
			else if (pendingState.GoalNodeID == nodeId)
			{
				if (IsRouteSimExpectedOnlyAction(actionTag))
				{
					LogVehicleAuthority(
						"route-sim-goal-blocked",
						$"{vehicleId.id}:{nodeId}:{pendingState.ExpectedNodeID}:{actionTag}",
						$"route-sim-goal-blocked vehicle={vehicleId.id} node={nodeId} expectedNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID} action={actionTag} reason=expected-node-only",
						dedupe: true);
					return false;
				}
				source = "route-sim-goal";
			}
			else
			{
				return false;
			}

			if (IsRouteSimPhysicalOnlyAction(actionTag))
			{
				LogVehicleAuthority(
					"route-sim-physical-only-blocked",
					$"{vehicleId.id}:{nodeId}:{source}:{actionTag}",
					$"route-sim-physical-only-blocked vehicle={vehicleId.id} node={nodeId} expectedNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID} source={source} action={actionTag} reason=physical-only-action",
					dedupe: true);
				return false;
			}

			LogVehicleAuthority(
				"route-sim-access",
				$"{vehicleId.id}:{nodeId}:{source}:{actionTag}",
				$"route-sim-access vehicle={vehicleId.id} node={nodeId} expectedNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID} source={source} action={actionTag}",
				dedupe: true);
			return true;
		}

		private static bool IsRouteSimPhysicalOnlyAction(string actionTag)
		{
			if (string.IsNullOrWhiteSpace(actionTag))
			{
				return false;
			}

			string normalized = actionTag.Trim().ToLowerInvariant();
			return normalized.Contains("combat")
				|| normalized.Contains("hostile")
				|| normalized.Contains("attack")
				|| normalized.Contains("storage")
				|| normalized.Contains("module")
				|| normalized.Contains("shop-commit")
				|| normalized.Contains("buy-sell-commit")
				|| normalized.Contains("owned-building")
				|| normalized.Contains("safehouse")
				|| normalized.Contains("physical");
		}

		private static bool IsRouteSimExpectedOnlyAction(string actionTag)
		{
			if (string.IsNullOrWhiteSpace(actionTag))
			{
				return false;
			}

			string normalized = actionTag.Trim().ToLowerInvariant();
			return normalized.Contains("heal");
		}

		internal static bool IsHumanVehicleRouteSimExpectedAccessNode(EntityID vehicleId, NodeID nodeId, string actionTag, out string source)
		{
			source = "none";
			if (!IsHumanVehicleRouteSimAccessNode(vehicleId, nodeId, actionTag, out string routeSource))
			{
				return false;
			}

			if (!string.Equals(routeSource, "route-sim-expected", StringComparison.Ordinal))
			{
				return false;
			}

			source = routeSource;
			return true;
		}

		internal static bool IsHumanVehiclePreviewNodeKnownOrReached(EntityID vehicleId, NodeID nodeId)
		{
			if (!nodeId.IsValid)
			{
				return false;
			}

			if (IsHumanVehiclePhysicallyAtNode(vehicleId, nodeId))
			{
				return true;
			}

			try
			{
				Node node = nodeId.FindNode();
				return node?.known.Get(PlayerID.HumanPlayer) == true;
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsHumanVehicleFinalGoalScopePreviewAllowed(EntityID vehicleId, NodeID nodeId, string source)
		{
			if (!vehicleId.IsValid || !nodeId.IsValid || string.IsNullOrWhiteSpace(source))
			{
				return false;
			}

			bool finalGoalSource = source.IndexOf("final-goal", StringComparison.OrdinalIgnoreCase) >= 0
				|| string.Equals(source, "queued-resume-goal", StringComparison.Ordinal);
			if (!finalGoalSource)
			{
				return false;
			}

			return TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.GoalNodeID == nodeId
				&& (pendingState.ExpectedNodeID.IsValid || pendingState.ResumeQueued || IsHumanVehicleTravelActive(vehicleId));
		}

		internal static bool IsHumanVehicleFinalGoalPreviewOnlySource(string source)
		{
			if (string.IsNullOrWhiteSpace(source))
			{
				return false;
			}

			return source.IndexOf("final-goal", StringComparison.OrdinalIgnoreCase) >= 0
				|| string.Equals(source, "queued-resume-goal", StringComparison.Ordinal);
		}

		private static bool IsHumanVehicleScopePreviewStatusAuthorizedSource(string source)
		{
			if (string.IsNullOrWhiteSpace(source))
			{
				return false;
			}

			return source.StartsWith("selected-", StringComparison.Ordinal)
				|| source.StartsWith("selection-", StringComparison.Ordinal)
				|| source.StartsWith("current-segment", StringComparison.Ordinal)
				|| source.StartsWith("current-final-goal", StringComparison.Ordinal)
				|| source.StartsWith("route-final-goal", StringComparison.Ordinal)
				|| source.StartsWith("queued-final-goal", StringComparison.Ordinal)
				|| string.Equals(source, "queued-resume-goal", StringComparison.Ordinal);
		}

		internal static bool TryGetBuildingHumanVehicleInteractiveNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "building-interactive");

			bool hasPendingState = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.ExpectedNodeID.IsValid;
			bool hasQueuedResume = HasQueuedHumanVehiclePendingResume(vehicleId)
				|| (hasPendingState && pendingState.ResumeQueued && pendingState.GoalNodeID.IsValid);
			string startupSuppressionReason = "none";
			bool startupSuppressed = hasPendingState
				&& TryGetFreshStartupHumanVehicleAuthoritySuppression(vehicleId, pendingState, out startupSuppressionReason);
			bool routeAuthorityInFlight = hasPendingState || hasQueuedResume || IsHumanVehicleTravelActive(vehicleId);
			bool allowSelectedFinalGoalPreview = !routeAuthorityInFlight && !IsPendingBuildingInteractionScopeActive();
			PlayerCrew humanCrew = G.GetHumanCrew();
			string selectedSource = "none";
			NodeID selectedPendingFinalGoalNodeId = NodeID.INVALID;
			bool hasSelectedPendingFinalGoal = allowSelectedFinalGoalPreview
				&& !startupSuppressed
				&& TryGetSelectedHumanVehiclePendingFinalGoalNodeId(vehicleId, out selectedPendingFinalGoalNodeId, out selectedSource)
				&& selectedPendingFinalGoalNodeId.IsValid;
			bool hasSelectedVehicleFinalGoal = allowSelectedFinalGoalPreview
				&& hasPendingState
				&& pendingState.GoalNodeID.IsValid
				&& humanCrew != null
				&& TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out selectedSource)
				&& selectedVehicleId == vehicleId;
			NodeID selectedVehicleFinalGoalNodeId = hasSelectedVehicleFinalGoal ? pendingState.GoalNodeID : NodeID.INVALID;
			string selectedVehicleFinalGoalSource = hasSelectedVehicleFinalGoal ? "selected-final-goal-building" : "none";

			if (hasSelectedPendingFinalGoal)
			{
				nodeId = selectedPendingFinalGoalNodeId;
				source = "selected-final-goal-building";
				LogVehicleAuthority("building-node-selected-goal", $"{vehicleId.id}:{pendingState.ExpectedNodeID}:{nodeId}:selected-pending-goal", $"building-node-selected-goal vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={nodeId} finalGoal={nodeId} resolvedSource=selected-pending-goal selectedSource={selectedSource}", dedupe: true);
				return true;
			}

			if (TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out nodeId, out source) && nodeId.IsValid)
			{
				if (routeAuthorityInFlight
					&& hasPendingState
					&& pendingState.ExpectedNodeID == nodeId
					&& !IsHumanVehiclePhysicallyAtNode(vehicleId, nodeId)
					&& (string.Equals(source, "selected-final-goal", StringComparison.Ordinal)
						|| string.Equals(source, "selected-current-segment", StringComparison.Ordinal)
						|| string.Equals(source, "current-segment", StringComparison.Ordinal)
						|| string.Equals(source, "queued-resume-expected", StringComparison.Ordinal)
						|| string.Equals(source, "pending-start", StringComparison.Ordinal)
						|| source.IndexOf("final-goal", StringComparison.OrdinalIgnoreCase) >= 0))
				{
					LogVehicleAuthority(
						"building-node-preview-blocked",
						$"{vehicleId.id}:{pendingState.ExpectedNodeID}:{nodeId}:{source}",
						$"building-node-preview-blocked vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={nodeId} resolvedSource={source} finalGoal={pendingState.GoalNodeID} reason=vehicle-not-arrived",
						dedupe: true);
					nodeId = NodeID.INVALID;
					source = "none";
					return false;
				}

				if (hasPendingState && pendingState.ExpectedNodeID != nodeId)
				{
					if (hasSelectedVehicleFinalGoal && selectedVehicleFinalGoalNodeId != nodeId)
					{
						LogVehicleAuthority("building-node-selected-goal", $"{vehicleId.id}:{pendingState.ExpectedNodeID}:{selectedVehicleFinalGoalNodeId}:{source}", $"building-node-selected-goal vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={nodeId} finalGoal={selectedVehicleFinalGoalNodeId} resolvedSource={source} selectedSource={selectedSource}", dedupe: true);
						nodeId = selectedVehicleFinalGoalNodeId;
						source = selectedVehicleFinalGoalSource;
						return true;
					}

					string reason = hasQueuedResume
						? "queued-resume"
						: startupSuppressed
							? "startup-" + startupSuppressionReason
							: "predicted-mismatch";
					string category = hasQueuedResume
						? "queued-resume-ui-suppressed"
						: "building-node-suppressed";
					LogVehicleAuthority(category, $"{vehicleId.id}:{pendingState.ExpectedNodeID}:{nodeId}:{reason}", $"building-node-suppressed vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={nodeId} resolvedSource={source} finalGoal={pendingState.GoalNodeID} reason={reason}", dedupe: false);
					nodeId = NodeID.INVALID;
					source = "none";
					return false;
				}
				else if (hasPendingState && startupSuppressed)
				{
					if (hasSelectedVehicleFinalGoal && selectedVehicleFinalGoalNodeId != nodeId)
					{
						LogVehicleAuthority("building-node-selected-goal", $"{vehicleId.id}:{pendingState.ExpectedNodeID}:{selectedVehicleFinalGoalNodeId}:startup-{startupSuppressionReason}", $"building-node-selected-goal vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={nodeId} finalGoal={selectedVehicleFinalGoalNodeId} resolvedSource={source} selectedSource={selectedSource} reason=startup-{startupSuppressionReason}", dedupe: true);
						nodeId = selectedVehicleFinalGoalNodeId;
						source = selectedVehicleFinalGoalSource;
						return true;
					}

					LogVehicleAuthority("building-node-suppressed", $"{vehicleId.id}:{pendingState.ExpectedNodeID}:{nodeId}:startup-{startupSuppressionReason}", $"building-node-suppressed vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={nodeId} resolvedSource={source} finalGoal={pendingState.GoalNodeID} reason=startup-{startupSuppressionReason}", dedupe: false);
					nodeId = NodeID.INVALID;
					source = "none";
					return false;
				}

				LogVehicleAuthority("building-node-source", $"{vehicleId.id}:{nodeId}:{source}", $"building-node-source vehicle={vehicleId.id} node={nodeId} source={source}");
				return true;
			}

			if (hasPendingState)
			{
				if (hasSelectedVehicleFinalGoal)
				{
					nodeId = selectedVehicleFinalGoalNodeId;
					source = selectedVehicleFinalGoalSource;
					LogVehicleAuthority("building-node-selected-goal", $"{vehicleId.id}:{pendingState.ExpectedNodeID}:{selectedVehicleFinalGoalNodeId}:no-committed", $"building-node-selected-goal vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={NodeID.INVALID} finalGoal={selectedVehicleFinalGoalNodeId} resolvedSource=none selectedSource={selectedSource} reason=no-committed-authority", dedupe: true);
					return true;
				}

				string reason = hasQueuedResume
					? "queued-resume-no-committed"
					: startupSuppressed
						? "startup-" + startupSuppressionReason
						: "no-committed-authority";
				string category = hasQueuedResume
					? "queued-resume-ui-suppressed"
					: "building-node-suppressed";
				LogVehicleAuthority(category, $"{vehicleId.id}:{pendingState.ExpectedNodeID}:{reason}", $"building-node-suppressed vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={NodeID.INVALID} resolvedSource=none finalGoal={pendingState.GoalNodeID} reason={reason}", dedupe: false);
			}

			return false;
		}

		private static string NormalizeActualInteractionContextTag(string contextTag)
		{
			if (string.IsNullOrWhiteSpace(contextTag))
			{
				contextTag = GetPendingBuildingInteractionScopeSource();
			}
			if (string.IsNullOrWhiteSpace(contextTag))
			{
				return "actual";
			}
			if (contextTag.IndexOf("CrewInfoGen", StringComparison.OrdinalIgnoreCase) >= 0
				|| string.Equals(contextTag, "crewhud", StringComparison.OrdinalIgnoreCase))
			{
				return "crewhud";
			}
			if (contextTag.IndexOf("BizComponent", StringComparison.OrdinalIgnoreCase) >= 0
				|| contextTag.IndexOf("ResidenceComponent", StringComparison.OrdinalIgnoreCase) >= 0
				|| string.Equals(contextTag, "biz", StringComparison.OrdinalIgnoreCase))
			{
				return "biz";
			}
			if (contextTag.IndexOf("CivicComponent", StringComparison.OrdinalIgnoreCase) >= 0
				|| contextTag.IndexOf("Politics", StringComparison.OrdinalIgnoreCase) >= 0
				|| string.Equals(contextTag, "civic", StringComparison.OrdinalIgnoreCase))
			{
				return "civic";
			}
			return contextTag;
		}

		private static string GetActualInteractionSourceCategory(string contextTag)
		{
			switch (NormalizeActualInteractionContextTag(contextTag))
			{
				case "crewhud":
					return "crewhud-node-source";
				case "biz":
					return "biz-action-source";
				case "civic":
					return "civic-action-source";
				default:
					return "actual-action-source";
			}
		}

		private static void LogActualHumanInteractionPreviewBlocked(string contextTag, EntityID vehicleId, PendingVehicleTravelState pendingState, NodeID resolvedNodeId, string resolvedSource, string reason)
		{
			string normalizedContext = NormalizeActualInteractionContextTag(contextTag);
			LogVehicleAuthority(
				"actual-action-preview-blocked",
				$"{vehicleId.id}:{normalizedContext}:{pendingState.ExpectedNodeID}:{resolvedNodeId}:{reason}",
				$"actual-action-preview-blocked vehicle={vehicleId.id} context={normalizedContext} pendingNode={pendingState.ExpectedNodeID} resolvedNode={resolvedNodeId} resolvedSource={resolvedSource} finalGoal={pendingState.GoalNodeID} reason={reason}",
				dedupe: true);
			if (string.Equals(normalizedContext, "crewhud", StringComparison.Ordinal))
			{
				LogVehicleAuthority(
					"crewhud-node-suppressed",
					$"{vehicleId.id}:{pendingState.ExpectedNodeID}:{resolvedNodeId}:{reason}",
					$"crewhud-node-suppressed vehicle={vehicleId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={resolvedNodeId} resolvedSource={resolvedSource} finalGoal={pendingState.GoalNodeID} reason={reason}",
					dedupe: true);
			}
		}

		private static bool IsNonInterruptingHumanVehicleInteractionContext(string normalizedContext)
		{
			return string.Equals(normalizedContext, "crewhud", StringComparison.Ordinal)
				|| string.Equals(normalizedContext, "biz", StringComparison.Ordinal)
				|| string.Equals(normalizedContext, "civic", StringComparison.Ordinal);
		}

		private static bool IsRouteSimulatedHumanVehicleConversationContext(string normalizedContext)
		{
			return string.Equals(normalizedContext, "biz", StringComparison.Ordinal)
				|| string.Equals(normalizedContext, "civic", StringComparison.Ordinal);
		}

		private static void ArmPreviewStopRouteClearSuppression(EntityID peepId, EntityID vehicleId, PendingVehicleTravelState pendingState, NodeID resolvedNodeId, string resolvedSource, string normalizedContext, string reason)
		{
			if (!peepId.IsValid)
			{
				return;
			}

			long peepKey = (long)peepId.id;
			_suppressPreviewStopRouteClearFrameByPeepId[peepKey] = Time.frameCount;
			_suppressPreviewStopRouteClearReasonByPeepId[peepKey] = $"{normalizedContext}:{reason}";
			if (string.Equals(normalizedContext, "crewhud", StringComparison.Ordinal))
			{
				LogVehicleAuthority(
					"crewhud-preview-stop-blocked",
					$"{vehicleId.id}:{peepId.id}:{pendingState.ExpectedNodeID}:{resolvedNodeId}:{reason}",
					$"crewhud-preview-stop-blocked vehicle={vehicleId.id} peep={peepId.id} pendingNode={pendingState.ExpectedNodeID} resolvedNode={resolvedNodeId} resolvedSource={resolvedSource} finalGoal={pendingState.GoalNodeID} reason={reason}",
					dedupe: false);
				LogVehicleAuthority(
					"crewhud-preview-noninterrupting",
					$"{vehicleId.id}:{peepId.id}:{pendingState.ExpectedNodeID}:{reason}",
					$"crewhud-preview-noninterrupting vehicle={vehicleId.id} peep={peepId.id} pendingNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID} reason={reason}",
					dedupe: false);
			}
		}

		internal static bool TryConsumePreviewStopRouteClearSuppression(EntityID peepId, out string reason)
		{
			reason = string.Empty;
			if (!peepId.IsValid)
			{
				return false;
			}

			long peepKey = (long)peepId.id;
			if (!_suppressPreviewStopRouteClearFrameByPeepId.TryGetValue(peepKey, out int frame))
			{
				return false;
			}

			_suppressPreviewStopRouteClearFrameByPeepId.Remove(peepKey);
			if (_suppressPreviewStopRouteClearReasonByPeepId.TryGetValue(peepKey, out string storedReason))
			{
				reason = storedReason ?? string.Empty;
				_suppressPreviewStopRouteClearReasonByPeepId.Remove(peepKey);
			}

			return frame >= Time.frameCount - 1;
		}

		internal static bool ShouldFailClosedHumanVehicleInteraction(EntityID vehicleId, string contextTag, out string reason)
		{
			reason = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			string normalizedContext = NormalizeActualInteractionContextTag(contextTag);
			if (!IsNonInterruptingHumanVehicleInteractionContext(normalizedContext))
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "fail-closed-" + normalizedContext);

			if (!TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState) || !pendingState.ExpectedNodeID.IsValid)
			{
				return false;
			}

			string startupSuppressionReason = "none";
			bool startupSuppressed = TryGetFreshStartupHumanVehicleAuthoritySuppression(vehicleId, pendingState, out startupSuppressionReason);
			if (TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out NodeID committedNodeId, out _) && committedNodeId.IsValid)
			{
				if (pendingState.ExpectedNodeID == committedNodeId && !startupSuppressed)
				{
					return false;
				}

				reason = startupSuppressed ? "startup-" + startupSuppressionReason : "predicted-mismatch";
				return true;
			}

			reason = startupSuppressed ? "startup-" + startupSuppressionReason : "no-committed-authority";
			return true;
		}

		internal static bool TryGetActualHumanVehicleInteractionNodeId(EntityID vehicleId, out NodeID nodeId, out string source, string contextTag = null)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			string normalizedContext = NormalizeActualInteractionContextTag(contextTag);
			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "actual-interaction-" + normalizedContext);

			bool hasPendingState = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.ExpectedNodeID.IsValid;
			bool hasQueuedResume = HasQueuedHumanVehiclePendingResume(vehicleId)
				|| (hasPendingState && pendingState.ResumeQueued && pendingState.GoalNodeID.IsValid);
			string startupSuppressionReason = "none";
			bool startupSuppressed = hasPendingState
				&& TryGetFreshStartupHumanVehicleAuthoritySuppression(vehicleId, pendingState, out startupSuppressionReason);

			if (!hasPendingState && !hasQueuedResume)
			{
				if (TryGetSelectedHumanVehicleUiAuthorityNodeId(vehicleId, out nodeId, out source, out string selectedUiReason, allowInterruptExpectedBridge: true) && nodeId.IsValid)
				{
					string category = GetActualInteractionSourceCategory(normalizedContext);
					LogVehicleAuthority(
						category,
						$"{vehicleId.id}:{nodeId}:{source}:{normalizedContext}:{selectedUiReason}",
						$"{category} vehicle={vehicleId.id} node={nodeId} source={source} context={normalizedContext} reason={selectedUiReason}");
					return true;
				}

				if (TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource) && liveNodeId.IsValid)
				{
					if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId)
						&& recentFinalizedNodeId.IsValid
						&& recentFinalizedNodeId != liveNodeId)
					{
						ClearRecentFinalizedNode(vehicleId, "actual-interaction-live-authority");
						Node staleNode = recentFinalizedNodeId.FindNode();
						Node liveNode = liveNodeId.FindNode();
						if (liveNode != null)
						{
							GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(staleNode, liveNode, "actual-interaction-authority-rebased", vehicleId);
						}
					}

					nodeId = liveNodeId;
					source = liveSource;
					string category = GetActualInteractionSourceCategory(normalizedContext);
					LogVehicleAuthority(
						category,
						$"{vehicleId.id}:{nodeId}:{source}:{normalizedContext}:live",
						$"{category} vehicle={vehicleId.id} node={nodeId} source={source} context={normalizedContext} reason=live-authority");
					return true;
				}
			}

			if (TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out nodeId, out source) && nodeId.IsValid)
			{
				if (hasPendingState && (pendingState.ExpectedNodeID != nodeId || startupSuppressed))
				{
					string reason = hasQueuedResume
						? "queued-resume"
						: startupSuppressed
							? "startup-" + startupSuppressionReason
							: "predicted-mismatch";
					LogActualHumanInteractionPreviewBlocked(normalizedContext, vehicleId, pendingState, nodeId, source, reason);
					if (IsNonInterruptingHumanVehicleInteractionContext(normalizedContext))
					{
						ArmPreviewStopRouteClearSuppression(pendingState.PeepID, vehicleId, pendingState, nodeId, source, normalizedContext, reason);
						nodeId = NodeID.INVALID;
						source = "none";
						return false;
					}
				}
				else if (hasPendingState
					&& IsHumanVehicleTravelActive(vehicleId)
					&& pendingState.ExpectedNodeID == nodeId
					&& !IsHumanVehiclePhysicallyAtNode(vehicleId, nodeId))
				{
					if (IsRouteSimulatedHumanVehicleConversationContext(normalizedContext)
						&& IsHumanVehicleRouteSimAccessNode(vehicleId, nodeId, "convo-" + normalizedContext, out string routeSimSource))
					{
						string routeSimCategory = GetActualInteractionSourceCategory(normalizedContext);
						source = routeSimSource + "-" + source;
						LogVehicleAuthority(
							"route-sim-convo",
							$"{vehicleId.id}:{nodeId}:{normalizedContext}:{routeSimSource}",
							$"route-sim-convo vehicle={vehicleId.id} node={nodeId} source={source} context={normalizedContext}",
							dedupe: true);
						RouteShopStagingState.RememberRouteInConversation(vehicleId, nodeId, normalizedContext, routeSimSource);
						LogVehicleAuthority(
							routeSimCategory,
							$"{vehicleId.id}:{nodeId}:{source}:{normalizedContext}:route-sim",
							$"{routeSimCategory} vehicle={vehicleId.id} node={nodeId} source={source} context={normalizedContext} reason=route-sim");
						return true;
					}

					string reason = "vehicle-not-arrived";
					LogActualHumanInteractionPreviewBlocked(normalizedContext, vehicleId, pendingState, nodeId, source, reason);
					if (IsNonInterruptingHumanVehicleInteractionContext(normalizedContext))
					{
						ArmPreviewStopRouteClearSuppression(pendingState.PeepID, vehicleId, pendingState, nodeId, source, normalizedContext, reason);
					}
					nodeId = NodeID.INVALID;
					source = "none";
					return false;
				}

				string category = GetActualInteractionSourceCategory(normalizedContext);
				LogVehicleAuthority(
					category,
					$"{vehicleId.id}:{nodeId}:{source}:{normalizedContext}",
					$"{category} vehicle={vehicleId.id} node={nodeId} source={source} context={normalizedContext}");
				return true;
			}

			if (hasPendingState)
			{
				string reason = hasQueuedResume
					? "queued-resume-no-committed"
					: startupSuppressed
						? "startup-" + startupSuppressionReason
						: "no-committed-authority";
				LogActualHumanInteractionPreviewBlocked(normalizedContext, vehicleId, pendingState, NodeID.INVALID, "none", reason);
				if (IsNonInterruptingHumanVehicleInteractionContext(normalizedContext))
				{
					ArmPreviewStopRouteClearSuppression(pendingState.PeepID, vehicleId, pendingState, NodeID.INVALID, "none", normalizedContext, reason);
				}
			}

			return false;
		}

		internal static bool TryGetActualHumanVehicleInteractionNode(EntityID vehicleId, out Node node, out string source, string contextTag = null)
		{
			node = null;
			source = "none";
			if (!TryGetActualHumanVehicleInteractionNodeId(vehicleId, out NodeID nodeId, out source, contextTag))
			{
				return false;
			}

			node = nodeId.FindNode();
			return node != null;
		}

		internal static string GetHumanVehicleSourceTag(CrewAssignment assignment, CrewNodeResolutionMode mode)
		{
			if (assignment.IsValid
				&& assignment.IsInVehicle
				&& assignment.VehicleID.IsValid
				&& !string.IsNullOrWhiteSpace(GetCrewNodeResolutionSourceTag(mode)))
			{
				string source = "none";
				bool resolved = mode == CrewNodeResolutionMode.QueuedFinalGoalAllowed
					? TryGetPendingHumanVehicleInteractiveNodeId(assignment.VehicleID, out _, out source)
					: mode == CrewNodeResolutionMode.BuildingCommittedOnly
						? TryGetBuildingHumanVehicleInteractiveNodeId(assignment.VehicleID, out _, out source)
						: TryGetCommittedHumanVehicleInteractiveNodeId(assignment.VehicleID, out _, out source);
				if (resolved && !string.IsNullOrWhiteSpace(source))
				{
					return source;
				}
			}

			return GetCrewNodeResolutionSourceTag(mode);
		}

		internal static string GetHumanVehicleInteractiveSourceTag(CrewAssignment assignment)
		{
			return GetHumanVehicleSourceTag(assignment, GetInteractiveCrewNodeResolutionMode());
		}

		private static string GetHumanMapScopeSourceTag(List<CrewAssignment> matches, CrewNodeResolutionMode mode)
		{
			if (matches != null)
			{
				foreach (CrewAssignment assignment in matches)
				{
					if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
					{
						continue;
					}

					return GetHumanVehicleSourceTag(assignment, mode);
				}
			}

			return GetCrewNodeResolutionSourceTag(mode);
		}

		private static bool IsCrewJailedForVehicleQueries(EntityID peepId)
		{
			if (!peepId.IsValid)
			{
				return false;
			}
			try
			{
				return GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(peepId, out _);
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsCrewBlockedFromVehicleAssignment(EntityID peepId, out string failureReason)
		{
			return GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(peepId, out failureReason);
		}

		internal static bool IsActiveVehicleOccupant(PlayerCrew crew, CrewAssignment assignment)
		{
			if (crew == null || !assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid || !assignment.IsNotDead)
			{
				return false;
			}
			if (!crew.IsOnBoard(assignment.peepId))
			{
				return false;
			}
			return !IsCrewJailedForVehicleQueries(assignment.peepId) && CanUseCrewEntityForVehicleQueries(assignment);
		}

		internal static bool IsInspectableVehicleOccupant(PlayerCrew crew, CrewAssignment assignment)
		{
			if (crew == null || !assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid || !assignment.IsNotDead)
			{
				return false;
			}
			if (!crew.IsOnBoard(assignment.peepId))
			{
				return false;
			}
			return !IsCrewJailedForVehicleQueries(assignment.peepId) && CanUseCrewEntityForVehicleQueries(assignment);
		}

		private static bool CanUseCrewEntityForVehicleQueries(CrewAssignment assignment)
		{
			if (!assignment.IsValid || !assignment.peepId.IsValid)
			{
				return false;
			}

			Entity peep = assignment.GetPeep();
			if (peep == null)
			{
				return false;
			}
			if (peep.data?.person != null && !peep.data.person.IsAlive)
			{
				return false;
			}
			if (peep.components?.agent != null && !peep.components.agent.HasHealthPointsLeft)
			{
				return false;
			}
			return true;
		}

		internal static void PruneFreshStartupPendingVehicleTravel(PlayerInfo player)
		{
			if (player?.PID.IsHumanPlayer != true || player.crew == null || !IsFreshHumanStartup(player.PID))
			{
				return;
			}
			long playerKey = GetPlayerKey(player.PID);
			if (!_freshStartupPendingTravelPrunedByPlayerId.Add(playerKey))
			{
				return;
			}
			List<PendingVehicleTravelState> startupPending = _pendingVehicleTravelByVehicleId.Values
				.Where(state => state.VehicleID.IsValid && state.PeepID.IsValid && player.crew.GetCrewForPeep(state.PeepID).IsValid)
				.OrderBy(state => state.VehicleID.id)
				.ToList();
			foreach (PendingVehicleTravelState state in startupPending)
			{
				if (TryReconcileFreshStartupPendingVehicleTravelForUi(state.VehicleID, "startup-prune"))
				{
					continue;
				}
				if (ShouldSkipFreshStartupPendingTravelPrune(player.crew, state, out string skipReason))
				{
					LogVehicleAuthority("startup-prune-skip", $"{state.VehicleID.id}:{skipReason}", $"startup-prune-skip vehicle={state.VehicleID.id} reason={skipReason} startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} goalNode={state.GoalNodeID}", dedupe: false);
					continue;
				}
				LogVehicleAuthority("startup-prune-clear", $"{state.VehicleID.id}", $"startup-prune-clear-invalid vehicle={state.VehicleID.id} startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} goalNode={state.GoalNodeID}", dedupe: false);
				ClearPendingHumanVehicleTravel(state.VehicleID, GetPendingTravelClearReason(state, "startup-prune"));
			}
			if (startupPending.Count > 0)
			{
				LogVehicleAuthority("startup-prune", $"{player.PID.id}:{startupPending.Count}", $"startup-pending-travel-pruned pid={player.PID.id} count={startupPending.Count}", dedupe: false);
			}
		}

		private static bool ShouldSkipFreshStartupPendingTravelPrune(PlayerCrew crew, PendingVehicleTravelState state, out string reason)
		{
			reason = "none";
			if (crew == null || !state.VehicleID.IsValid || !state.PeepID.IsValid)
			{
				return false;
			}

			CrewAssignment assignment = crew.GetCrewForPeep(state.PeepID);
			if (!assignment.IsValid || !assignment.IsInVehicle || assignment.VehicleID != state.VehicleID)
			{
				return false;
			}

			Entity vehicle = state.VehicleID.FindEntity();
			Entity peep = state.PeepID.FindEntity();
			if (vehicle?.data?.mobile == null || peep?.data?.agent == null)
			{
				return false;
			}

			if (_activeHumanVehicleTravel.Contains((long)state.VehicleID.id)
				&& state.ExpectedNodeID.IsValid
				&& (HasInteractivePlayerIssuedHumanVehicleTravel(state) || HasActiveQueuedResumeHumanVehicleTravel(state)))
			{
				reason = state.CommandOwnerKey == QueuedRouteResumeCommandOwnerKey ? "queued-resume-active" : "active";
				return true;
			}

			if (state.ResumeQueued && state.GoalNodeID.IsValid && state.ExpectedNodeID.IsValid)
			{
				reason = "resume";
				return true;
			}

			return false;
		}

		private static bool HasInteractivePlayerIssuedHumanVehicleTravel(PendingVehicleTravelState state)
		{
			return state.VehicleID.IsValid
				&& _activeHumanVehicleTravel.Contains((long)state.VehicleID.id)
				&& state.CommandOwnerKey != 0
				&& state.CommandOwnerKey != QueuedRouteResumeCommandOwnerKey;
		}

		private static bool HasActiveQueuedResumeHumanVehicleTravel(PendingVehicleTravelState state)
		{
			return state.VehicleID.IsValid
				&& _activeHumanVehicleTravel.Contains((long)state.VehicleID.id)
				&& state.CommandOwnerKey == QueuedRouteResumeCommandOwnerKey
				&& state.ExpectedNodeID.IsValid
				&& state.GoalNodeID.IsValid;
		}

		private static bool ShouldUseLogicalHumanVehicleRouteArrivalAtTurnStart(PendingVehicleTravelState state)
		{
			return state.VehicleID.IsValid
				&& state.ExpectedNodeID.IsValid
				&& state.GoalNodeID.IsValid
				&& state.ExpectedNodeID != state.GoalNodeID
				&& (state.ResumeQueued || state.CommandOwnerKey == QueuedRouteResumeCommandOwnerKey);
		}

		private static bool TryGetFreshStartupStalePendingTravelAuthority(
			EntityID vehicleId,
			PendingVehicleTravelState state,
			out NodeID authoritativeNodeId,
			out string authoritativeSource,
			out NodeID committedNodeId,
			out string committedSource,
			out NodeID driverNodeId,
			out NodeID staleUiNodeId,
			out string reason)
		{
			authoritativeNodeId = NodeID.INVALID;
			authoritativeSource = "none";
			committedNodeId = NodeID.INVALID;
			committedSource = "none";
			driverNodeId = NodeID.INVALID;
			staleUiNodeId = NodeID.INVALID;
			reason = "none";
			if (!vehicleId.IsValid || state.VehicleID != vehicleId)
			{
				return false;
			}

			Entity vehicle = vehicleId.FindEntity();
			PlayerID pid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
			if (!pid.IsHumanPlayer || !IsFreshHumanStartup(pid))
			{
				return false;
			}

			if (HasInteractivePlayerIssuedHumanVehicleTravel(state))
			{
				return false;
			}

			if (state.ResumeQueued || HasActiveQueuedResumeHumanVehicleTravel(state))
			{
				return false;
			}

			if (!TryGetVehicleLiveAuthorityNodeId(vehicleId, out authoritativeNodeId, out authoritativeSource) || !authoritativeNodeId.IsValid)
			{
				return false;
			}

			PlayerCrew crew = pid.FindPlayer()?.crew;
			if (crew == null)
			{
				return false;
			}

			bool hasCommittedNode = TryGetFinalizedVehicleNodeId(vehicleId, out committedNodeId, out committedSource) && committedNodeId.IsValid;
			EntityID driverPeepId = GetDriverPeepId(crew, vehicleId);
			driverNodeId = driverPeepId.IsValid ? driverPeepId.FindEntity()?.data?.agent?.nid ?? NodeID.INVALID : NodeID.INVALID;
			if (!driverNodeId.IsValid
				|| driverNodeId != authoritativeNodeId)
			{
				return false;
			}

			if (hasCommittedNode && committedNodeId != authoritativeNodeId)
			{
				return false;
			}

			bool expectedMismatch = state.ExpectedNodeID.IsValid && state.ExpectedNodeID != authoritativeNodeId;
			bool goalMismatch = state.GoalNodeID.IsValid && state.GoalNodeID != authoritativeNodeId;
			if (!expectedMismatch && !goalMismatch)
			{
				return false;
			}

			staleUiNodeId = expectedMismatch
				? state.ExpectedNodeID
				: goalMismatch
					? state.GoalNodeID
					: NodeID.INVALID;
			if (!staleUiNodeId.IsValid && state.StartNodeID.IsValid && state.StartNodeID != authoritativeNodeId)
			{
				staleUiNodeId = state.StartNodeID;
			}

			if (!hasCommittedNode)
			{
				committedNodeId = authoritativeNodeId;
				committedSource = "live-authority";
			}

			reason = expectedMismatch && goalMismatch
				? "expected-goal-mismatch"
				: expectedMismatch
					? "expected-mismatch"
					: "goal-mismatch";
			return true;
		}

		private static bool TryReconcileFreshStartupPendingVehicleTravelForUi(EntityID vehicleId, string sourceTag)
		{
			if (!TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state))
			{
				return false;
			}

			if (!TryGetFreshStartupStalePendingTravelAuthority(
				vehicleId,
				state,
				out NodeID authoritativeNodeId,
				out string authoritativeSource,
				out NodeID committedNodeId,
				out string committedSource,
				out NodeID driverNodeId,
				out NodeID staleUiNodeId,
				out string reason))
			{
				return false;
			}

			if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId)
				&& recentFinalizedNodeId.IsValid
				&& recentFinalizedNodeId != authoritativeNodeId)
			{
				ClearRecentFinalizedNode(vehicleId, "startup-authority-reconcile");
			}

			ClearPendingHumanVehicleTravel(vehicleId, "startup-authority-reconcile");
			SetRecentFinalizedNode(vehicleId, authoritativeNodeId);

			Node staleNode = staleUiNodeId.IsValid ? staleUiNodeId.FindNode() : null;
			Node authoritativeNode = authoritativeNodeId.FindNode();
			if (authoritativeNode != null)
			{
				GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(staleNode, authoritativeNode, sourceTag, vehicleId);
			}

			LogVehicleAuthority(
				"startup-authority-reconciled",
				$"{vehicleId.id}:{staleUiNodeId}:{authoritativeNodeId}:{sourceTag}",
				$"startup-authority-reconciled vehicle={vehicleId.id} staleNode={staleUiNodeId} committedNode={committedNodeId} committedSource={committedSource} authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource} driverNode={driverNodeId} expectedNode={state.ExpectedNodeID} goalNode={state.GoalNodeID} reason={reason} source={sourceTag}",
				dedupe: false);
			return true;
		}

		private static bool TryGetFreshStartupHumanVehicleAuthoritySuppression(EntityID vehicleId, PendingVehicleTravelState state, out string reason)
		{
			reason = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			Entity vehicle = vehicleId.FindEntity();
			PlayerID pid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
			if (!pid.IsHumanPlayer || !IsFreshHumanStartup(pid))
			{
				return false;
			}

			if (!HasInteractivePlayerIssuedHumanVehicleTravel(state))
			{
				reason = "no-player-command";
				return true;
			}

			PlayerCrew crew = pid.FindPlayer()?.crew;
			if (crew == null)
			{
				reason = "no-crew";
				return true;
			}

			bool hasCommittedNode = TryGetRecentFinalizedNodeId(vehicleId, out NodeID committedNodeId) && committedNodeId.IsValid;
			bool hasAuthoritativeNode = TryGetAuthoritativeVehicleNodeId(vehicleId, out NodeID vehicleNodeId, out _)
				&& vehicleNodeId.IsValid;
			if (!hasCommittedNode && !hasAuthoritativeNode)
			{
				reason = "no-authority";
				return true;
			}

			EntityID driverPeepId = GetDriverPeepId(crew, vehicleId);
			NodeID driverNodeId = driverPeepId.IsValid ? driverPeepId.FindEntity()?.data?.agent?.nid ?? NodeID.INVALID : NodeID.INVALID;
			if (hasAuthoritativeNode && driverNodeId.IsValid && driverNodeId != vehicleNodeId)
			{
				reason = "node-mismatch";
				return true;
			}

			LogVehicleAuthority("startup-authority-stable", $"{vehicleId.id}:{committedNodeId}:{vehicleNodeId}", $"startup-authority-stable vehicle={vehicleId.id} committedNode={committedNodeId} authoritativeNode={vehicleNodeId} driverNode={driverNodeId}", dedupe: true);
			return false;
		}

		private static void TryNormalizeFreshStartupPendingVehicleTravelForUi(EntityID vehicleId, string sourceTag = "startup-ui")
		{
			if (TryReconcileFreshStartupPendingVehicleTravelForUi(vehicleId, sourceTag))
			{
				return;
			}

			if (!TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state))
			{
				return;
			}

			Entity vehicle = vehicleId.FindEntity();
			PlayerID pid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
			if (!pid.IsHumanPlayer || !IsFreshHumanStartup(pid))
			{
				return;
			}

			bool active = _activeHumanVehicleTravel.Contains((long)vehicleId.id);
			if (HasInteractivePlayerIssuedHumanVehicleTravel(state)
				|| HasActiveQueuedResumeHumanVehicleTravel(state)
				|| (state.ResumeQueued && state.GoalNodeID.IsValid))
			{
				return;
			}

			string reason = null;
			if (!state.StartNodeID.IsValid || !state.ExpectedNodeID.IsValid || !state.GoalNodeID.IsValid)
			{
				reason = "startup-leftover";
			}
			else if (active)
			{
				reason = "non-player-active";
			}

			if (string.IsNullOrWhiteSpace(reason))
			{
				return;
			}

			LogVehicleAuthority("startup-pending-ignored", $"{vehicleId.id}:{reason}", $"startup-pending-ignored vehicle={vehicleId.id} reason={reason} startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} goalNode={state.GoalNodeID} active={active}", dedupe: false);
			ClearPendingHumanVehicleTravel(vehicleId, "startup-ui-prune");
		}

		internal static void ResetFreshStartupPendingTravelPrune(PlayerID pid)
		{
			if (pid.IsNotValid)
			{
				return;
			}
			_freshStartupPendingTravelPrunedByPlayerId.Remove(GetPlayerKey(pid));
		}

		internal static void LogVehicleAuthority(string category, string key, string message, bool dedupe = true)
		{
			try
			{
				if (ShouldSuppressVehicleAuthorityLogCategory(category))
				{
					return;
				}

				int day = G.GetNow().days;
				string fullKey = $"{day}:{category}:{key}";
				if (!dedupe || _loggedVehicleAuthorityKeys.Add(fullKey))
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", message);
				}
			}
			catch
			{
			}
		}

		private static void FlushEnemyVehicleRepresentativeSwapSummary(string source)
		{
			if (_enemyVehicleRepresentativeSwapSummaryDay == int.MinValue || _enemyVehicleRepresentativeSwapSummaryTotal <= 0)
			{
				return;
			}

			string sourceCounts = string.Join(
				",",
				_enemyVehicleRepresentativeSwapSummaryBySource
					.OrderByDescending(pair => pair.Value)
					.ThenBy(pair => pair.Key, StringComparer.Ordinal)
					.Select(pair => pair.Key + ":" + pair.Value));
			GameplayTweaksPlugin.VerificationLog(
				"VehicleNodeAuthority",
				$"representative-swap-summary day={_enemyVehicleRepresentativeSwapSummaryDay} total={_enemyVehicleRepresentativeSwapSummaryTotal} sources={sourceCounts} source={source}");
			_enemyVehicleRepresentativeSwapSummaryDay = int.MinValue;
			_enemyVehicleRepresentativeSwapSummaryTotal = 0;
			_enemyVehicleRepresentativeSwapSummaryBySource.Clear();
		}

		private static void RecordEnemyVehicleRepresentativeSwap(string source)
		{
			int day = G.GetNow().days;
			if (_enemyVehicleRepresentativeSwapSummaryDay != int.MinValue && _enemyVehicleRepresentativeSwapSummaryDay != day)
			{
				FlushEnemyVehicleRepresentativeSwapSummary("day-change");
			}
			if (_enemyVehicleRepresentativeSwapSummaryDay == int.MinValue)
			{
				_enemyVehicleRepresentativeSwapSummaryDay = day;
			}

			string sourceKey = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
			_enemyVehicleRepresentativeSwapSummaryTotal++;
			_enemyVehicleRepresentativeSwapSummaryBySource.TryGetValue(sourceKey, out int sourceCount);
			_enemyVehicleRepresentativeSwapSummaryBySource[sourceKey] = sourceCount + 1;
			if ((_enemyVehicleRepresentativeSwapSummaryTotal % 40) == 0)
			{
				FlushEnemyVehicleRepresentativeSwapSummary("threshold");
			}
		}

		internal static bool IsHumanVehicleTravelActive(EntityID vehicleId)
		{
			return vehicleId.IsValid && _activeHumanVehicleTravel.Contains((long)vehicleId.id);
		}

		internal static bool TryGetHumanVehicleRouteModeLabel(EntityID vehicleId, out string label)
		{
			label = null;
			if (!vehicleId.IsValid)
			{
				return false;
			}

			bool active = IsHumanVehicleTravelActive(vehicleId);
			bool queuedResume = HasQueuedHumanVehiclePendingResume(vehicleId);
			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state)
				&& (active || queuedResume || state.ExpectedNodeID.IsValid || state.GoalNodeID.IsValid))
			{
				label = active
					&& state.ExpectedNodeID.IsValid
					&& state.GoalNodeID.IsValid
					&& state.ExpectedNodeID == state.GoalNodeID
					&& !state.ResumeQueued
						? "Arriving"
						: "In route";
				LogVehicleAuthority(
					"route-mode-label",
					$"{vehicleId.id}:{label}:{state.StartNodeID}:{state.ExpectedNodeID}:{state.GoalNodeID}:{active}:{queuedResume}:{state.ResumeQueued}",
					$"route-mode-label vehicle={vehicleId.id} label=\"{label}\" startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} active={active} queuedResume={queuedResume} resumeQueued={state.ResumeQueued}",
					dedupe: true);
				return true;
			}

			if (TryGetAuthoritativeVehicleNodeId(vehicleId, out NodeID nodeId, out _) && nodeId.IsValid)
			{
				label = "At corner";
				LogVehicleAuthority(
					"route-mode-label",
					$"{vehicleId.id}:{label}:{nodeId}",
					$"route-mode-label vehicle={vehicleId.id} label=\"{label}\" node={nodeId}",
					dedupe: true);
				return true;
			}

			return false;
		}

		internal static string AppendHumanVehicleRouteModeLabel(string text, EntityID vehicleId)
		{
			if (!TryGetHumanVehicleRouteModeLabel(vehicleId, out string label) || string.IsNullOrWhiteSpace(label))
			{
				return text;
			}

			string suffix = "[" + label + "]";
			if (!string.IsNullOrEmpty(text) && text.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return text;
			}

			return string.IsNullOrWhiteSpace(text) ? suffix : text + " " + suffix;
		}

		internal static bool TryGetRecentFinalizedNodeId(EntityID vehicleId, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			return vehicleId.IsValid && _recentFinalizedNodeByVehicleId.TryGetValue((long)vehicleId.id, out nodeId) && nodeId.IsValid;
		}

		internal static bool TryGetRecentFinalizedNode(EntityID vehicleId, out Node node)
		{
			node = null;
			if (!TryGetRecentFinalizedNodeId(vehicleId, out NodeID nodeId))
			{
				return false;
			}
			node = nodeId.FindNode();
			return node != null;
		}

		internal static void SetRecentFinalizedNode(EntityID vehicleId, NodeID nodeId)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}
			ClearVehicleNodeAuthorityCaches(vehicleId);
			long vehicleKey = (long)vehicleId.id;
			if (!nodeId.IsValid)
			{
				_recentFinalizedNodeByVehicleId.Remove(vehicleKey);
				_recentFinalizedFrameByVehicleId.Remove(vehicleKey);
				return;
			}
			_recentFinalizedNodeByVehicleId[vehicleKey] = nodeId;
			_recentFinalizedFrameByVehicleId[vehicleKey] = Time.frameCount;
		}

		internal static void ClearRecentFinalizedNode(EntityID vehicleId, string source = "clear")
		{
			if (!vehicleId.IsValid)
			{
				return;
			}
			ClearVehicleNodeAuthorityCaches(vehicleId);
			long vehicleKey = (long)vehicleId.id;
			if (_recentFinalizedNodeByVehicleId.Remove(vehicleKey))
			{
				LogVehicleAuthority("recent-finalize-clear", $"{vehicleId.id}:{source}", $"recent-finalize-cleared vehicle={vehicleId.id} reason={source}", dedupe: false);
			}
			_recentFinalizedFrameByVehicleId.Remove(vehicleKey);
		}

		private static bool IsRecentFinalizedStableBridgeFresh(EntityID vehicleId, int maxFrameDelta, out int frameDelta)
		{
			frameDelta = int.MaxValue;
			if (!vehicleId.IsValid
				|| !_recentFinalizedFrameByVehicleId.TryGetValue((long)vehicleId.id, out int finalizedFrame))
			{
				return false;
			}

			frameDelta = Math.Max(0, Time.frameCount - finalizedFrame);
			return frameDelta <= maxFrameDelta;
		}

		private static void ClearVehicleNodeAuthorityCaches(EntityID vehicleId)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			_vehicleLiveAuthorityNodeCacheByVehicleId.Remove(vehicleKey);
			_vehiclePhysicalNodeCacheByVehicleId.Remove(vehicleKey);
		}

		internal static bool TryGetInterruptExpectedStartNode(EntityID vehicleId, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			return vehicleId.IsValid && _interruptExpectedStartNodeByVehicleId.TryGetValue((long)vehicleId.id, out nodeId) && nodeId.IsValid;
		}

		internal static void SetInterruptExpectedStartNode(EntityID vehicleId, NodeID nodeId, string source)
		{
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return;
			}

			_interruptExpectedStartNodeByVehicleId[(long)vehicleId.id] = nodeId;
			LogVehicleAuthority("user-stop-commit", $"{vehicleId.id}:{nodeId}:{source}", $"user-stop-commit-expected vehicle={vehicleId.id} node={nodeId} source={source}", dedupe: false);
		}

		internal static void ClearInterruptExpectedStartNode(EntityID vehicleId, string source = "clear")
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			if (_interruptExpectedStartNodeByVehicleId.Remove((long)vehicleId.id))
			{
				LogVehicleAuthority("interrupt-expected-cleared", $"{vehicleId.id}:{source}", $"interrupt-expected-cleared vehicle={vehicleId.id} reason={source}", dedupe: false);
			}
		}

		private static bool ShouldSuppressVehicleAuthorityLogCategory(string category)
		{
			if (_verboseVehicleAuthorityPreviewLogs || string.IsNullOrWhiteSpace(category))
			{
				return false;
			}

			if (category.StartsWith("scope-preview-", StringComparison.Ordinal))
			{
				return true;
			}

			switch (category)
			{
				case "selected-ui-authority-source":
				case "active-route-authority-source":
				case "selected-scope-node-suppressed":
				case "recent-finalize-authority-suppressed":
				case "scope-pick-presence":
				case "scope-preview-blocked":
				case "scope-preview-source":
				case "scope-preview-edge-blocked":
				case "scope-preview-edge-normalized":
				case "scope-preview-corner-blocked":
				case "scope-preview-destination-blocked":
				case "scope-preview-corner-normalized":
				case "scope-preview-frontage-collapsed":
				case "scope-preview-destination-accepted":
				case "scope-preview-driver-only":
				case "scope-preview-vehicle-crew":
				case "selected-scope-preview-rebased":
				case "selected-scope-preview-exclusive":
				case "preview-scope-route-preserved":
				case "selected-scope-old-node-blocked":
				case "preview-scope-stale-start-blocked":
				case "selected-scope-node-source":
				case "route-resume-after-deferred-arrival-skipped":
				case "route-resume-business-preview-skipped":
				case "route-resume-source":
				case "route-resume-watchdog":
				case "turnstart-arrival-deferred":
				case "turnstart-arrival-finalize":
				case "turnstart-arrival-preserved":
				case "turnstart-sync-skip":
					return true;
				default:
					return false;
			}
		}

		private static bool WasTurnStartQueuedRouteFinalized(EntityID vehicleId)
		{
			return vehicleId.IsValid && _turnStartFinalizedQueuedRouteVehicleIds.Contains((long)vehicleId.id);
		}

		internal static bool TryGetRecentAiCommittedNodeId(EntityID vehicleId, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			return vehicleId.IsValid && _recentAiCommittedNodeByVehicleId.TryGetValue((long)vehicleId.id, out nodeId) && nodeId.IsValid;
		}

		internal static void SetRecentAiCommittedNode(EntityID vehicleId, NodeID nodeId, string source = "sync")
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			if (!nodeId.IsValid)
			{
				_recentAiCommittedNodeByVehicleId.Remove(vehicleKey);
				return;
			}

			_recentAiCommittedNodeByVehicleId[vehicleKey] = nodeId;
			LogVehicleAuthority("ai-committed-node", $"{vehicleId.id}:{nodeId}:{source}", $"ai-committed-node vehicle={vehicleId.id} node={nodeId} source={source}");
		}

		private static bool ShouldPreserveHumanCommittedAuthorityOnPendingTravelClear(string reason)
		{
			return string.Equals(reason, "segment-complete", StringComparison.Ordinal)
				|| string.Equals(reason, "arrived", StringComparison.Ordinal)
				|| string.Equals(reason, "stale-final-goal-cleared", StringComparison.Ordinal)
				|| string.Equals(reason, "invalid", StringComparison.Ordinal)
				|| string.Equals(reason, "user-stop", StringComparison.Ordinal)
				|| string.Equals(reason, "fresh-command-override", StringComparison.Ordinal);
		}

		private static bool ShouldClearSelectedFinalGoalOnPendingTravelClear(string reason)
		{
			if (string.IsNullOrWhiteSpace(reason))
			{
				return false;
			}

			return string.Equals(reason, "user-stop", StringComparison.Ordinal)
				|| string.Equals(reason, "fresh-command-override", StringComparison.Ordinal)
				|| string.Equals(reason, "segment-complete", StringComparison.Ordinal)
				|| string.Equals(reason, "arrived", StringComparison.Ordinal)
				|| string.Equals(reason, "invalid", StringComparison.Ordinal)
				|| string.Equals(reason, "requeue-suppressed", StringComparison.Ordinal)
				|| string.Equals(reason, "reverse-resume-blocked", StringComparison.Ordinal)
				|| string.Equals(reason, "resume-origin-invalid", StringComparison.Ordinal)
				|| string.Equals(reason, "turnstart-resume-blocked", StringComparison.Ordinal)
				|| reason.IndexOf("user-stop", StringComparison.Ordinal) >= 0
				|| reason.IndexOf("fresh-command", StringComparison.Ordinal) >= 0;
		}

		private static void PreserveHumanCommittedAuthorityOnPendingTravelClear(EntityID vehicleId, PendingVehicleTravelState state, string reason)
		{
			if (!vehicleId.IsValid || !ShouldPreserveHumanCommittedAuthorityOnPendingTravelClear(reason))
			{
				return;
			}

			if (string.Equals(reason, "user-stop", StringComparison.Ordinal)
				&& state.ExpectedNodeID.IsValid
				&& _activeHumanVehicleTravel.Contains((long)vehicleId.id))
			{
				SetInterruptExpectedStartNode(vehicleId, state.ExpectedNodeID, "pending-expected");
				return;
			}

			if (TryGetQueuedArrivalCommittedNodeId(vehicleId, out NodeID committedNodeId) && committedNodeId.IsValid)
			{
				SetRecentFinalizedNode(vehicleId, committedNodeId);
				LogVehicleAuthority("resume-authority-preserved", $"{vehicleId.id}:{committedNodeId}:{reason}", $"resume-authority-preserved vehicle={vehicleId.id} node={committedNodeId} reason={reason}", dedupe: false);
			}
			else if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentNodeId) && recentNodeId.IsValid)
			{
				LogVehicleAuthority("resume-authority-preserved", $"{vehicleId.id}:{recentNodeId}:{reason}:recent", $"resume-authority-preserved vehicle={vehicleId.id} node={recentNodeId} reason={reason} source=recent-finalize", dedupe: false);
			}
			else if (state.StartNodeID.IsValid)
			{
				SetRecentFinalizedNode(vehicleId, state.StartNodeID);
				LogVehicleAuthority("human-start-preserved", $"{vehicleId.id}:{state.StartNodeID}:{reason}", $"human-start-preserved vehicle={vehicleId.id} node={state.StartNodeID} reason={reason} source=pending-start", dedupe: false);
			}
		}

		internal static bool TryGetBestHumanVehicleStartNode(EntityID vehicleId, out Node node, out string source, bool logRebase = true)
		{
			node = null;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			Node authoritativeNode = null;
			string authoritativeSource = "none";
			TryGetAuthoritativeVehicleNode(vehicleId, out authoritativeNode, out authoritativeSource);
			NodeID authoritativeNodeId = authoritativeNode?.id ?? NodeID.INVALID;

			if (TryGetInterruptExpectedStartNode(vehicleId, out NodeID interruptNodeId) && interruptNodeId.IsValid)
			{
				NodeID committedNodeId = NodeID.INVALID;
				string committedSource = "none";
				bool hasCommittedNode = TryGetFinalizedVehicleNodeId(vehicleId, out committedNodeId, out committedSource)
					&& committedNodeId.IsValid;
				if (!hasCommittedNode
					&& authoritativeNodeId.IsValid
					&& !string.Equals(authoritativeSource, "mobile", StringComparison.Ordinal))
				{
					committedNodeId = authoritativeNodeId;
					committedSource = authoritativeSource;
					hasCommittedNode = true;
				}

				bool preserveInterruptExpected = hasCommittedNode
					&& committedNodeId != interruptNodeId
					&& string.Equals(committedSource, "recent-finalize", StringComparison.Ordinal);
				if (preserveInterruptExpected)
				{
					LogVehicleAuthority(
						WasTurnStartQueuedRouteFinalized(vehicleId) ? "turnstart-resume-preserved" : "interrupt-expected-preserved",
						$"{vehicleId.id}:{interruptNodeId}:{committedNodeId}:{committedSource}",
						$"{(WasTurnStartQueuedRouteFinalized(vehicleId) ? "turnstart-resume-preserved" : "interrupt-expected-preserved")} vehicle={vehicleId.id} pendingStart={interruptNodeId} staleCommittedNode={committedNodeId} staleCommittedSource={committedSource} reason=recent-finalize-suppressed",
						dedupe: false);
					hasCommittedNode = false;
					committedNodeId = NodeID.INVALID;
					committedSource = "none";
				}

				if (hasCommittedNode && committedNodeId != interruptNodeId)
				{
					LogVehicleAuthority(
						WasTurnStartQueuedRouteFinalized(vehicleId) ? "turnstart-resume-blocked" : "interrupt-expected-stale-blocked",
						$"{vehicleId.id}:{interruptNodeId}:{committedNodeId}:{committedSource}",
						$"{(WasTurnStartQueuedRouteFinalized(vehicleId) ? "turnstart-resume-blocked" : "interrupt-expected-stale-blocked")} vehicle={vehicleId.id} pendingStart={interruptNodeId} committedNode={committedNodeId} committedSource={committedSource} reason=interrupt-outranked",
						dedupe: false);
					if (logRebase)
					{
						LogVehicleAuthority(
							"user-stop-repath-rebased",
							$"{vehicleId.id}:{interruptNodeId}:{committedNodeId}:{committedSource}",
							$"user-stop-repath-rebased vehicle={vehicleId.id} staleNode={interruptNodeId} committedNode={committedNodeId} committedSource={committedSource}",
							dedupe: false);
					}
					ClearInterruptExpectedStartNode(vehicleId, WasTurnStartQueuedRouteFinalized(vehicleId) ? "turnstart-finalized" : "stale-committed-mismatch");
				}
				else
				{
					Node interruptNode = interruptNodeId.FindNode();
					if (interruptNode != null)
					{
						node = interruptNode;
						source = "interrupt-expected";
						if (logRebase && authoritativeNodeId.IsValid && authoritativeNodeId != interruptNodeId)
						{
							LogVehicleAuthority("human-start-rebased", $"{vehicleId.id}:{interruptNodeId}:{authoritativeNodeId}:interrupt-expected", $"human-start-rebased vehicle={vehicleId.id} startNode={interruptNodeId} source=interrupt-expected authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource}", dedupe: false);
						}
						return true;
					}
					ClearInterruptExpectedStartNode(vehicleId, "missing-node");
				}
			}

			if (TryGetQueuedArrivalCommittedNode(vehicleId, out node) && node != null)
			{
				source = "queued-arrival";
				if (logRebase && authoritativeNodeId.IsValid && authoritativeNodeId != node.id)
				{
					LogVehicleAuthority("human-start-rebased", $"{vehicleId.id}:{node.id}:{authoritativeNodeId}:queued-arrival", $"human-start-rebased vehicle={vehicleId.id} startNode={node.id} source=queued-arrival authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource}", dedupe: false);
				}
				return true;
			}

			if (TryGetRecentFinalizedNode(vehicleId, out node) && node != null)
			{
				bool routeAuthorityActive = IsHumanVehicleTravelActive(vehicleId)
					|| HasQueuedHumanVehiclePendingResume(vehicleId)
					|| TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingStartState)
						&& (pendingStartState.ExpectedNodeID.IsValid || pendingStartState.ResumeQueued || pendingStartState.GoalNodeID.IsValid);
				if (!routeAuthorityActive
					&& TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource)
					&& liveNodeId.IsValid
					&& liveNodeId != node.id
					&& !AreNodesWithinStableFinalizeBridgeRange(node.id, liveNodeId))
				{
					Node liveNode = liveNodeId.FindNode();
					if (liveNode != null)
					{
						NodeID staleNodeId = node.id;
						ClearRecentFinalizedNode(vehicleId, "fresh-command-live-authority");
						node = liveNode;
						source = string.IsNullOrWhiteSpace(liveSource) ? "live-authority" : liveSource;
						if (logRebase)
						{
							LogVehicleAuthority(
								"human-start-stale-finalize-cleared",
								$"{vehicleId.id}:{staleNodeId}:{liveNodeId}:fresh-command",
								$"human-start-stale-finalize-cleared vehicle={vehicleId.id} staleNode={staleNodeId} authoritativeNode={liveNodeId} authoritativeSource={source} reason=fresh-command-live-authority",
								dedupe: false);
						}
						return true;
					}
				}

				if (authoritativeNode != null
					&& authoritativeNodeId.IsValid
					&& authoritativeNodeId != node.id
					&& !AreNodesWithinStableFinalizeBridgeRange(node.id, authoritativeNodeId))
				{
					NodeID staleNodeId = node.id;
					ClearRecentFinalizedNode(vehicleId, "stale-start-node");
					node = authoritativeNode;
					source = authoritativeSource;
					if (logRebase)
					{
						LogVehicleAuthority("human-start-stale-finalize-cleared", $"{vehicleId.id}:{staleNodeId}:{authoritativeNodeId}", $"human-start-stale-finalize-cleared vehicle={vehicleId.id} staleNode={staleNodeId} authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource}", dedupe: false);
					}
					return true;
				}

				source = "recent-finalize";
				if (logRebase && authoritativeNodeId.IsValid && authoritativeNodeId != node.id)
				{
					LogVehicleAuthority("human-start-rebased", $"{vehicleId.id}:{node.id}:{authoritativeNodeId}:recent-finalize", $"human-start-rebased vehicle={vehicleId.id} startNode={node.id} source=recent-finalize authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource}", dedupe: false);
				}
				return true;
			}

			if (authoritativeNode != null)
			{
				node = authoritativeNode;
				source = authoritativeSource;
				return true;
			}

			return false;
		}

		private static bool TryGetLastAiIssuedGoalNodeId(EntityID vehicleId, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			if (!vehicleId.IsValid || !_recentAiVehicleMoveByVehicleId.TryGetValue((long)vehicleId.id, out AiRecentVehicleMoveState state))
			{
				return false;
			}

			nodeId = state.GoalNodeID;
			return nodeId.IsValid;
		}

		internal static bool TryGetBestAiVehicleStartNode(EntityID vehicleId, out Node node, out string source)
		{
			node = null;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			Node authoritativeNode = null;
			string authoritativeSource = "none";
			TryGetAuthoritativeVehicleNode(vehicleId, out authoritativeNode, out authoritativeSource);
			NodeID authoritativeNodeId = authoritativeNode?.id ?? NodeID.INVALID;

			if (TryGetRecentAiCommittedNodeId(vehicleId, out NodeID committedNodeId) && committedNodeId.IsValid)
			{
				Node committedNode = committedNodeId.FindNode();
				if (committedNode != null)
				{
					node = committedNode;
					source = "ai-committed";
					if (!authoritativeNodeId.IsValid || authoritativeNodeId != committedNodeId)
					{
						LogVehicleAuthority("ai-start-rebased", $"{vehicleId.id}:{committedNodeId}:{authoritativeNodeId}:committed", $"ai-start-rebased vehicle={vehicleId.id} startNode={committedNodeId} source=ai-committed authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource}", dedupe: false);
					}
					return true;
				}
			}

			if (TryGetLastAiIssuedGoalNodeId(vehicleId, out NodeID priorGoalNodeId) && priorGoalNodeId.IsValid)
			{
				Node priorGoalNode = priorGoalNodeId.FindNode();
				if (priorGoalNode != null && (!authoritativeNodeId.IsValid || authoritativeNodeId != priorGoalNodeId))
				{
					node = priorGoalNode;
					source = "ai-prior-goal";
					LogVehicleAuthority("ai-start-rebased", $"{vehicleId.id}:{priorGoalNodeId}:{authoritativeNodeId}:prior-goal", $"ai-start-rebased vehicle={vehicleId.id} startNode={priorGoalNodeId} source=ai-prior-goal authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource}", dedupe: false);
					return true;
				}
			}

			if (authoritativeNode != null)
			{
				node = authoritativeNode;
				source = authoritativeSource;
				return true;
			}

			return false;
		}

		internal static void RecordAiVehicleIssuedMove(EntityID vehicleId, NodeID startNodeId, NodeID goalNodeId)
		{
			if (!vehicleId.IsValid || !startNodeId.IsValid || !goalNodeId.IsValid || startNodeId == goalNodeId)
			{
				return;
			}
			long vehicleKey = (long)vehicleId.id;
			int day = G.GetNow().days;
			AiRecentVehicleMoveState state;
			if (!_recentAiVehicleMoveByVehicleId.TryGetValue(vehicleKey, out state))
			{
				state = default;
			}
			if (state.StartNodeID == startNodeId && state.GoalNodeID == goalNodeId && state.Day == day)
			{
				return;
			}
			state.StartNodeID = startNodeId;
			state.GoalNodeID = goalNodeId;
			state.Day = day;
			_recentAiVehicleMoveByVehicleId[vehicleKey] = state;
			SetRecentAiCommittedNode(vehicleId, goalNodeId, "ai-issued-goal");
			LogVehicleAuthority("ai-route-issued", $"{vehicleId.id}:{startNodeId}:{goalNodeId}", $"ai-route-issued vehicle={vehicleId.id} startNode={startNodeId} goalNode={goalNodeId}", dedupe: false);
		}

		internal static void RecordHumanVehicleFinalizedSegment(EntityID vehicleId, NodeID startNodeId, NodeID arrivalNodeId)
		{
			if (!vehicleId.IsValid || !startNodeId.IsValid || !arrivalNodeId.IsValid || startNodeId == arrivalNodeId)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			HumanVehicleRequeueState state;
			if (!_recentHumanVehicleRequeueByVehicleId.TryGetValue(vehicleKey, out state))
			{
				state = default;
			}

			state.LastFinalizedStartNodeID = startNodeId;
			state.LastFinalizedArrivalNodeID = arrivalNodeId;
			state.Day = G.GetNow().days;
			_recentHumanVehicleRequeueByVehicleId[vehicleKey] = state;
		}

		internal static bool ShouldBlockHumanImmediateReverse(EntityID vehicleId, NodeID startNodeId, NodeID goalNodeId)
		{
			if (!TryGetHumanImmediateReverseState(vehicleId, startNodeId, goalNodeId, out long vehicleKey, out HumanVehicleRequeueState state))
			{
				return false;
			}

			int day = G.GetNow().days;
			bool alreadyBlockedThisReverse = state.LastBlockedReverseStartNodeID == startNodeId
				&& state.LastBlockedReverseGoalNodeID == goalNodeId
				&& state.LastBlockedReverseDay == day;
			if (alreadyBlockedThisReverse)
			{
				return false;
			}

			state.LastBlockedReverseStartNodeID = startNodeId;
			state.LastBlockedReverseGoalNodeID = goalNodeId;
			state.LastBlockedReverseDay = day;
			_recentHumanVehicleRequeueByVehicleId[vehicleKey] = state;
			LogVehicleAuthority(
				"human-reverse-resume-blocked",
				$"{vehicleId.id}:{startNodeId}:{goalNodeId}",
				$"human-reverse-resume-blocked vehicle={vehicleId.id} startNode={startNodeId} goalNode={goalNodeId} previousStart={state.LastFinalizedStartNodeID} previousArrival={state.LastFinalizedArrivalNodeID}",
				dedupe: false);
			return true;
		}

		private static bool IsHumanImmediateReverseSegment(EntityID vehicleId, NodeID startNodeId, NodeID goalNodeId)
		{
			return TryGetHumanImmediateReverseState(vehicleId, startNodeId, goalNodeId, out _, out _);
		}

		private static bool TryGetHumanImmediateReverseState(EntityID vehicleId, NodeID startNodeId, NodeID goalNodeId, out long vehicleKey, out HumanVehicleRequeueState state)
		{
			vehicleKey = 0;
			state = default;
			if (!vehicleId.IsValid || !startNodeId.IsValid || !goalNodeId.IsValid || startNodeId == goalNodeId)
			{
				return false;
			}

			vehicleKey = (long)vehicleId.id;
			if (!_recentHumanVehicleRequeueByVehicleId.TryGetValue(vehicleKey, out state))
			{
				return false;
			}

			return state.LastFinalizedStartNodeID == goalNodeId
				&& state.LastFinalizedArrivalNodeID == startNodeId;
		}

		internal static bool ShouldBlockAiImmediateReverse(EntityID vehicleId, NodeID startNodeId, NodeID goalNodeId)
		{
			if (!vehicleId.IsValid || !startNodeId.IsValid || !goalNodeId.IsValid || startNodeId == goalNodeId)
			{
				return false;
			}
			long vehicleKey = (long)vehicleId.id;
			if (!_recentAiVehicleMoveByVehicleId.TryGetValue(vehicleKey, out AiRecentVehicleMoveState state))
			{
				return false;
			}
			int day = G.GetNow().days;
			bool isImmediateReverse = state.StartNodeID == goalNodeId && state.GoalNodeID == startNodeId;
			bool alreadyBlockedThisReverse = state.LastBlockedReverseStartNodeID == startNodeId
				&& state.LastBlockedReverseGoalNodeID == goalNodeId;
			if (!isImmediateReverse || alreadyBlockedThisReverse)
			{
				return false;
			}
			state.LastBlockedReverseDay = day;
			state.LastBlockedReverseStartNodeID = startNodeId;
			state.LastBlockedReverseGoalNodeID = goalNodeId;
			_recentAiVehicleMoveByVehicleId[vehicleKey] = state;
			LogVehicleAuthority("ai-reverse-block", $"{vehicleId.id}:{startNodeId}:{goalNodeId}", $"ai-reverse-block vehicle={vehicleId.id} startNode={startNodeId} goalNode={goalNodeId} previousStart={state.StartNodeID} previousGoal={state.GoalNodeID}", dedupe: false);
			return true;
		}

		internal static VehicleDeathSnapshot CaptureVehicleOccupantDeathSnapshot(Entity deadPeep, string source)
		{
			try
			{
				PlayerID pid = deadPeep?.data?.agent?.pid ?? PlayerID.INVALID;
				if (!pid.IsValid || pid.IsHumanPlayer)
				{
					return null;
				}
				PlayerCrew crew = pid.FindPlayer()?.crew;
				if (crew == null)
				{
					return null;
				}
				CrewAssignment deadAssignment = crew.GetCrewForPeep(deadPeep.Id);
				if (!deadAssignment.IsValid || !deadAssignment.IsInVehicle || !deadAssignment.VehicleID.IsValid)
				{
					return null;
				}
				EntityID vehicleId = deadAssignment.VehicleID;
				EntityID priorDriver = GetDriverPeepId(crew, vehicleId);
				var snapshot = new VehicleDeathSnapshot
				{
					DeadPeepId = deadPeep.Id,
					VehicleID = vehicleId,
					OwnerPid = pid,
					PriorDriverPeepId = priorDriver,
					WasDriver = priorDriver.IsValid && priorDriver == deadPeep.Id,
					Source = source ?? string.Empty
				};
				if (TryGetAuthoritativeVehicleNodeId(vehicleId, out NodeID liveNode, out _))
				{
					snapshot.LiveNode = liveNode;
				}
				foreach (CrewAssignment occupant in GetAllCrewInVehicle(crew, vehicleId)
					.Where(item => item.IsValid && item.peepId.IsValid && item.peepId != deadPeep.Id)
					.OrderBy(item => item.peepId.id))
				{
					Entity peep = occupant.GetPeep();
					if (peep != null && peep.components?.agent?.HasHealthPointsLeft == true && crew.IsOnBoard(occupant.peepId))
					{
						snapshot.SurvivorPeepIds.Add(occupant.peepId);
					}
				}
				return snapshot;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CaptureVehicleOccupantDeathSnapshot: " + ex.Message);
				return null;
			}
		}

		internal static void ApplyVehicleOccupantDeathSnapshot(object snapshotObj)
		{
			VehicleDeathSnapshot snapshot = snapshotObj as VehicleDeathSnapshot;
			if (snapshot == null || !snapshot.IsValid)
			{
				return;
			}
			try
			{
				PlayerCrew crew = snapshot.OwnerPid.FindPlayer()?.crew;
				if (crew == null)
				{
					return;
				}
				EntityID vehicleId = snapshot.VehicleID;
				long vehicleKey = (long)vehicleId.id;
				List<CrewAssignment> survivors = snapshot.SurvivorPeepIds
					.Select(id => crew.GetCrewForPeep(id))
					.Where(item => item.IsValid && item.IsInVehicle && item.VehicleID == vehicleId && item.IsNotDead && crew.IsOnBoard(item.peepId))
					.OrderBy(item => item.peepId.id)
					.ToList();
				if (survivors.Count <= 0)
				{
					_vehicleSearchBlockedBySurvivors.Remove(vehicleKey);
					DriverMap.Remove(vehicleKey);
					SetRecentEnemyVehicleDeathRepresentative(vehicleId, snapshot.DeadPeepId);
					RefreshEnemyVehicleRepresentative(crew, vehicleId, snapshot.Source);
					TryCleanupEmptyEnemyVehiclePresentation(crew, vehicleId, snapshot.Source + "-postdeath", out _);
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-driver-handoff-skip reason=no-occupants vehicle={vehicleId.id} deadPeep={snapshot.DeadPeepId.id} occupantsRemaining=0 source={snapshot.Source}");
					return;
				}

				SetRecentEnemyVehicleDeathRepresentative(vehicleId, EntityID.INVALID);
				_vehicleSearchBlockedBySurvivors.Add(vehicleKey);
				CrewAssignment newDriver = survivors[0];
				if (!snapshot.WasDriver)
				{
					TrySyncVehicleOccupantsToVehicleNode(crew, vehicleId, "death");
					RefreshEnemyVehicleRepresentative(crew, vehicleId, snapshot.Source);
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-driver-handoff-skip reason=not-driver vehicle={vehicleId.id} deadPeep={snapshot.DeadPeepId.id} occupantsRemaining={survivors.Count} source={snapshot.Source}");
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-vehicle-postdeath-sync vehicle={vehicleId.id} node={snapshot.LiveNode} newDriver={GetDriverPeepId(crew, vehicleId).id} source={snapshot.Source}");
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-vehicle-return-suppressed vehicle={vehicleId.id} reason=survivors-present source={snapshot.Source}");
					return;
				}

				SetDriverInternal(vehicleId, newDriver.peepId);
				TrySyncVehicleOccupantsToVehicleNode(crew, vehicleId, "death");
				RefreshEnemyVehicleRepresentative(crew, vehicleId, snapshot.Source);
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-driver-handoff vehicle={vehicleId.id} deadPeep={snapshot.DeadPeepId.id} newDriver={newDriver.peepId.id} occupantsRemaining={survivors.Count} source={snapshot.Source}");
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-vehicle-postdeath-sync vehicle={vehicleId.id} node={snapshot.LiveNode} newDriver={newDriver.peepId.id} source={snapshot.Source}");
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-vehicle-return-suppressed vehicle={vehicleId.id} reason=survivors-present source={snapshot.Source}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ApplyVehicleOccupantDeathSnapshot: " + ex.Message);
			}
		}

		internal static bool ShouldBlockVehicleSearch(EntityID vehicleId, out int survivorCount)
		{
			survivorCount = 0;
			if (!vehicleId.IsValid)
			{
				return false;
			}
			long vehicleKey = (long)vehicleId.id;
			if (!_vehicleSearchBlockedBySurvivors.Contains(vehicleKey))
			{
				return false;
			}
			Entity vehicle = vehicleId.FindEntity();
			PlayerID pid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
			PlayerCrew crew = pid.FindPlayer()?.crew;
			if (crew == null)
			{
				return false;
			}
			if (!TryGetEnemyVehicleDisplayState(crew, vehicleId, out EnemyVehicleDisplayState state))
			{
				return false;
			}
			survivorCount = state.LiveOccupantCount;
			if (!state.HasLiveOccupants)
			{
				_vehicleSearchBlockedBySurvivors.Remove(vehicleKey);
				TryCleanupEmptyEnemyVehiclePresentation(crew, vehicleId, "search-unblocked", out _);
				return false;
			}
			return true;
		}

		internal static bool HasPendingHumanVehicleTravel(EntityID vehicleId)
		{
			return vehicleId.IsValid && _pendingVehicleTravelByVehicleId.ContainsKey((long)vehicleId.id);
		}

		private static void PushFlushQueueRouteClearSuppression(EntityID peepId)
		{
			if (!peepId.IsValid)
			{
				return;
			}
			long peepKey = (long)peepId.id;
			if (_suppressFlushQueueRouteClearByPeepId.TryGetValue(peepKey, out int depth))
			{
				_suppressFlushQueueRouteClearByPeepId[peepKey] = depth + 1;
			}
			else
			{
				_suppressFlushQueueRouteClearByPeepId[peepKey] = 1;
			}
		}

		private static void PopFlushQueueRouteClearSuppression(EntityID peepId)
		{
			if (!peepId.IsValid)
			{
				return;
			}
			long peepKey = (long)peepId.id;
			if (!_suppressFlushQueueRouteClearByPeepId.TryGetValue(peepKey, out int depth))
			{
				return;
			}
			if (depth <= 1)
			{
				_suppressFlushQueueRouteClearByPeepId.Remove(peepKey);
			}
			else
			{
				_suppressFlushQueueRouteClearByPeepId[peepKey] = depth - 1;
			}
		}

		internal static bool IsFlushQueueRouteClearSuppressed(EntityID peepId)
		{
			return peepId.IsValid
				&& _suppressFlushQueueRouteClearByPeepId.TryGetValue((long)peepId.id, out int depth)
				&& depth > 0;
		}

		internal static bool TryGetPendingHumanVehicleTravelByPeep(EntityID peepId, out EntityID vehicleId, out NodeID goalNodeId)
		{
			vehicleId = EntityID.INVALID;
			goalNodeId = NodeID.INVALID;
			if (!peepId.IsValid)
			{
				return false;
			}
			foreach (PendingVehicleTravelState value in _pendingVehicleTravelByVehicleId.Values)
			{
				if (value.PeepID != peepId)
				{
					continue;
				}
				vehicleId = value.VehicleID;
				goalNodeId = value.GoalNodeID;
				return vehicleId.IsValid;
			}
			return false;
		}

		internal static bool TryGetOwnedQueuedHumanVehicleRoute(CommandGoto command, out EntityID vehicleId, out NodeID goalNodeId)
		{
			vehicleId = EntityID.INVALID;
			goalNodeId = NodeID.INVALID;
			if (command == null || !command.pid.IsHumanPlayer || !command.peepId.IsValid)
			{
				return false;
			}
			if (!TryGetPendingHumanVehicleTravelByPeep(command.peepId, out vehicleId, out goalNodeId))
			{
				return false;
			}
			if (!vehicleId.IsValid
				|| !_pendingVehicleTravelByVehicleId.TryGetValue((long)vehicleId.id, out PendingVehicleTravelState state)
				|| !state.GoalNodeID.IsValid)
			{
				return false;
			}
			if (IsSameHumanVehicleRouteCommand(state, command))
			{
				LogVehicleAuthority(
					"route-same-command-allowed",
					$"{vehicleId.id}:{command.peepId.id}:{state.ExpectedNodeID}:{state.GoalNodeID}:{command.goalID}",
					$"route-same-command-allowed vehicle={vehicleId.id} peep={command.peepId.id} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} commandGoal={command.goalID}",
					dedupe: false);
				return false;
			}
			if (command.goalID.IsValid
				&& command.goalID != state.ExpectedNodeID
				&& command.goalID != state.GoalNodeID)
			{
				LogVehicleAuthority(
					"route-fresh-command-allowed",
					$"{vehicleId.id}:{command.peepId.id}:{state.ExpectedNodeID}:{state.GoalNodeID}:{command.goalID}",
					$"route-fresh-command-allowed vehicle={vehicleId.id} peep={command.peepId.id} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} commandGoal={command.goalID} reason=replacement-destination",
					dedupe: false);
				return false;
			}
			return state.ResumeQueued
				|| state.CommandOwnerKey == QueuedRouteResumeCommandOwnerKey
				|| (_activeHumanVehicleTravel.Contains((long)vehicleId.id) && state.GoalNodeID != state.ExpectedNodeID);
		}

		private static bool IsAiVehicleAssignmentUsable(PlayerCrew crew, CrewAssignment assignment, HashSet<long> validVehicleIds)
		{
			if (crew == null || !assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
			{
				return false;
			}
			if (validVehicleIds == null || !validVehicleIds.Contains((long)assignment.VehicleID.id))
			{
				return false;
			}
			Entity vehicle = assignment.VehicleID.FindEntity();
			if (vehicle?.data?.mobile == null)
			{
				return false;
			}
			int slots = Math.Max(1, GetVehicleCrewSlots(vehicle));
			int crewCount = GetVehicleCrewCount(crew, assignment.VehicleID);
			return crewCount > 0 && crewCount <= slots;
		}

		private static bool TryAssignAiCrewToVehicleAtomic(PlayerInfo player, PlayerCrew crew, CrewAssignment currentAssignment, EntityID desiredVehicleId)
		{
			if (player == null || crew == null || !currentAssignment.IsValid || !currentAssignment.peepId.IsValid || !desiredVehicleId.IsValid)
			{
				return false;
			}
			if (!CanAiAssignCrewToVehicleAtSafehouse(player, currentAssignment.peepId, desiredVehicleId, out string assignmentReason))
			{
				GameplayTweaksPlugin.VerificationLog(
					"VehicleNodeAuthority",
					$"ai-vehicle-transfer-blocked gang={player.PID.id} peep={currentAssignment.peepId.id} from={currentAssignment.VehicleID.id} to={desiredVehicleId.id} reason={assignmentReason}");
				return false;
			}

			Entity peep = currentAssignment.GetPeep();
			bool aliveBefore = peep?.data?.person?.IsAlive ?? currentAssignment.IsNotDead;
			EntityID priorVehicleId = currentAssignment.IsInVehicle ? currentAssignment.VehicleID : EntityID.INVALID;
			try
			{
				crew.AssignCrewToVehicle(currentAssignment.peepId, desiredVehicleId);
				CrewAssignment updatedAssignment = crew.GetCrewForPeep(currentAssignment.peepId);
				if (!updatedAssignment.IsValid || !updatedAssignment.IsInVehicle || updatedAssignment.VehicleID != desiredVehicleId)
				{
					return false;
				}

				TrySyncCrewPeepToVehicleNode(desiredVehicleId, currentAssignment.peepId);
				TrySyncVehicleOccupantsToVehicleNode(crew, desiredVehicleId, "ai-transfer");
				RefreshEnemyVehicleRepresentative(crew, desiredVehicleId, "transfer");
				if (priorVehicleId.IsValid && priorVehicleId != desiredVehicleId)
				{
					TrySyncVehicleOccupantsToVehicleNode(crew, priorVehicleId, "ai-transfer");
					RefreshEnemyVehicleRepresentative(crew, priorVehicleId, "transfer");
				}

				Entity updatedPeep = updatedAssignment.GetPeep();
				bool aliveAfter = updatedPeep?.data?.person?.IsAlive ?? updatedAssignment.IsNotDead;
				if (aliveBefore && !aliveAfter)
				{
					GameplayTweaksPlugin.VerificationLog("DeathAttribution", $"source=vehicle-transfer peep={currentAssignment.peepId.id} from={priorVehicleId.id} to={desiredVehicleId.id}");
					GameplayTweaksPlugin.RecordDeathSource(currentAssignment.peepId, "vehicle-transfer");
				}

				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-vehicle-transfer gang={player.PID.id} peep={currentAssignment.peepId.id} from={priorVehicleId.id} to={desiredVehicleId.id} mode=atomic");
				return true;
			}
			catch (Exception ex)
			{
				Entity fallbackPeep = crew.GetCrewForPeep(currentAssignment.peepId).GetPeep();
				if (aliveBefore && fallbackPeep?.data?.person?.IsAlive == false)
				{
					GameplayTweaksPlugin.VerificationLog("DeathAttribution", $"source=vehicle-transfer peep={currentAssignment.peepId.id} from={priorVehicleId.id} to={desiredVehicleId.id}");
					GameplayTweaksPlugin.RecordDeathSource(currentAssignment.peepId, "vehicle-transfer");
				}
				Debug.LogWarning("[GameplayTweaks] TryAssignAiCrewToVehicleAtomic: " + ex.Message);
				return false;
			}
		}

		internal static bool CanAiAssignCrewToVehicleAtSafehouse(PlayerInfo player, EntityID peepId, EntityID desiredVehicleId, out string reason)
		{
			reason = "invalid";
			if (player == null || player.crew == null || player.territory == null || !peepId.IsValid || !desiredVehicleId.IsValid)
			{
				return false;
			}
			if (player.PID.IsHumanPlayer || player.IsJustCop || player.IsCopOrFed || global::Game.Game.ctx?.IsInteractive != true)
			{
				reason = "exempt";
				return true;
			}

			Node headquartersNode = player.territory.GetHeadquartersNode();
			NodeID headquartersNodeId = headquartersNode?.id ?? NodeID.INVALID;
			if (!headquartersNodeId.IsValid)
			{
				reason = "headquarters-node-unavailable";
				return false;
			}
			if (!TryGetAuthoritativeVehicleNodeId(desiredVehicleId, out NodeID desiredVehicleNodeId, out string desiredVehicleSource)
				|| desiredVehicleNodeId != headquartersNodeId)
			{
				reason = $"destination-not-at-safehouse:{desiredVehicleNodeId}:{desiredVehicleSource}";
				return false;
			}

			CrewAssignment currentAssignment = player.crew.GetCrewForPeep(peepId);
			if (!currentAssignment.IsValid)
			{
				reason = "crew-assignment-unavailable";
				return false;
			}
			if (currentAssignment.IsInVehicle && currentAssignment.VehicleID.IsValid)
			{
				if (currentAssignment.VehicleID == desiredVehicleId)
				{
					reason = "already-assigned";
					return true;
				}
				if (!TryGetAuthoritativeVehicleNodeId(currentAssignment.VehicleID, out NodeID currentVehicleNodeId, out string currentVehicleSource)
					|| currentVehicleNodeId != headquartersNodeId)
				{
					reason = $"current-vehicle-not-at-safehouse:{currentVehicleNodeId}:{currentVehicleSource}";
					return false;
				}
			}
			else
			{
				NodeID peepNodeId = currentAssignment.GetPeep()?.data?.agent?.nid ?? NodeID.INVALID;
				if (peepNodeId != headquartersNodeId)
				{
					reason = $"crew-not-at-safehouse:{peepNodeId}";
					return false;
				}
			}

			reason = $"safehouse:{headquartersNodeId}";
			return true;
		}

		internal static void PushPendingPresenceSelectionScope()
		{
			_pendingPresenceSelectionScopeDepth++;
		}

		internal static void PopPendingPresenceSelectionScope()
		{
			if (_pendingPresenceSelectionScopeDepth > 0)
			{
				_pendingPresenceSelectionScopeDepth--;
			}
		}

		internal static bool IsPendingPresenceSelectionScopeActive()
		{
			return _pendingPresenceSelectionScopeDepth > 0;
		}

		internal static void PushPendingBuildingInteractionScope(string source)
		{
			_pendingBuildingInteractionScopeDepth++;
			_pendingBuildingInteractionSourceStack.Push(source ?? string.Empty);
			LogVehicleAuthority("building-interaction-scope", source, $"building-interaction-scope push source={source}");
		}

		internal static void PopPendingBuildingInteractionScope()
		{
			if (_pendingBuildingInteractionScopeDepth > 0)
			{
				_pendingBuildingInteractionScopeDepth--;
			}
			if (_pendingBuildingInteractionSourceStack.Count > 0)
			{
				_pendingBuildingInteractionSourceStack.Pop();
			}
		}

		internal static bool IsPendingBuildingInteractionScopeActive()
		{
			return _pendingBuildingInteractionScopeDepth > 0;
		}

		internal static string GetPendingBuildingInteractionScopeSource()
		{
			return _pendingBuildingInteractionSourceStack.Count > 0
				? _pendingBuildingInteractionSourceStack.Peek()
				: string.Empty;
		}

		internal static void PushAiHireNormalizationScope()
		{
			_aiHireNormalizationScopeDepth++;
		}

		internal static void PopAiHireNormalizationScope()
		{
			if (_aiHireNormalizationScopeDepth > 0)
			{
				_aiHireNormalizationScopeDepth--;
			}
		}

		internal static bool IsAiHireNormalizationScopeActive()
		{
			return _aiHireNormalizationScopeDepth > 0;
		}

		internal static bool IsAiHireNormalizationActive()
		{
			return _aiHireNormalizationDepth > 0;
		}

		internal static bool IsAmbientTrafficVehicle(EntityID vehicleId)
		{
			return vehicleId.IsValid && _ambientTrafficVehicleIds.Contains((long)vehicleId.id);
		}

		internal static void MarkAmbientTrafficVehicle(Entity vehicle, Label template)
		{
			if (vehicle == null || !vehicle.Id.IsValid)
			{
				return;
			}

			_ambientTrafficVehicleIds.Add((long)vehicle.Id.id);
			_cachedAmbientLiveCount = -1;
			_cachedAmbientLiveCountFrame = -1;
			LogVehicleAuthority("ambient-spawn", $"{vehicle.Id.id}:{template}", $"ambient-spawn vehicle={vehicle.Id.id} template={template}", dedupe: false);
		}

		internal static void SetPendingAmbientSpawnReason(string reason)
		{
			_pendingAmbientSpawnReason = string.IsNullOrWhiteSpace(reason) ? "normal" : reason;
		}

		internal static int GetAmbientVisibleTrafficFloor()
		{
			int dayTarget = GetAmbientPopulationScaledDayTarget();
			if (IsAmbientNightTime())
			{
				return Math.Max(AmbientVisibleTrafficNightFloor, (int)Math.Ceiling(dayTarget * 0.4));
			}

			return dayTarget;
		}

		internal static int GetAmbientVisibleTrafficHardCap()
		{
			// Keep the hard cap tied to the city-size daytime target so existing daytime traffic
			// does not lock out future spawns when the reduced night floor temporarily drops.
			return GetAmbientPopulationScaledDayTarget() + AmbientVisibleTrafficHardCapBuffer;
		}

		internal static string ConsumePendingAmbientSpawnReason()
		{
			string reason = string.IsNullOrWhiteSpace(_pendingAmbientSpawnReason) ? "normal" : _pendingAmbientSpawnReason;
			_pendingAmbientSpawnReason = "normal";
			return reason;
		}

		internal static void UnmarkAmbientTrafficVehicle(EntityID vehicleId)
		{
			if (vehicleId.IsValid)
			{
				_ambientTrafficVehicleIds.Remove((long)vehicleId.id);
			}
			_cachedAmbientLiveCount = -1;
			_cachedAmbientLiveCountFrame = -1;
		}

		internal static int CountLiveAmbientTrafficVehicles()
		{
			int frame = Time.frameCount;
			if (_cachedAmbientLiveCountFrame == frame && _cachedAmbientLiveCount >= 0)
			{
				return _cachedAmbientLiveCount;
			}

			if (_ambientTrafficVehicleIds.Count == 0)
			{
				_cachedAmbientLiveCount = 0;
				_cachedAmbientLiveCountFrame = frame;
				return 0;
			}

			List<long> staleKeys = null;
			int count = 0;
			foreach (long vehicleKey in _ambientTrafficVehicleIds)
			{
				Entity entity = EntityID.FromID((ulong)vehicleKey).FindEntity();
				if (entity == null)
				{
					if (staleKeys == null)
					{
						staleKeys = new List<long>();
					}
					staleKeys.Add(vehicleKey);
					continue;
				}
				count++;
			}

			if (staleKeys != null)
			{
				foreach (long staleKey in staleKeys)
				{
					_ambientTrafficVehicleIds.Remove(staleKey);
				}
			}

			_cachedAmbientLiveCount = count;
			_cachedAmbientLiveCountFrame = frame;
			return count;
		}

		private static int GetAmbientBoardCacheKey()
		{
			return RuntimeHelpers.GetHashCode(global::Game.Game.ctx?.board);
		}

		private static void EnsureAmbientRoadNodeCache()
		{
			try
			{
				List<Node> allNodes = global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe();
				int boardKey = GetAmbientBoardCacheKey();
				int nodeCount = allNodes?.Count ?? 0;
				if (boardKey == _cachedAmbientRoadNodeBoardKey && nodeCount == _cachedAmbientRoadNodeSourceCount && _cachedAmbientRoadNodeIds.Count > 0)
				{
					return;
				}

				_cachedAmbientRoadNodeIds.Clear();
				_cachedAmbientVisitNodeIds.Clear();
				if (allNodes != null)
				{
					foreach (Node node in allNodes)
					{
						if (node == null || !node.HasRoad)
						{
							continue;
						}

						_cachedAmbientRoadNodeIds.Add(node.id);
						if (node.contained != null && node.contained.Count > 0)
						{
							_cachedAmbientVisitNodeIds.Add(node.id);
						}
					}
				}

				_cachedAmbientRoadNodeBoardKey = boardKey;
				_cachedAmbientRoadNodeSourceCount = nodeCount;
			}
			catch
			{
				_cachedAmbientRoadNodeIds.Clear();
				_cachedAmbientVisitNodeIds.Clear();
				_cachedAmbientRoadNodeBoardKey = -1;
				_cachedAmbientRoadNodeSourceCount = -1;
			}
		}

		private static Node PickAmbientRoadNode(List<NodeID> candidateNodeIds, Node excludeA = null, Node excludeB = null)
		{
			if (candidateNodeIds == null || candidateNodeIds.Count == 0)
			{
				return null;
			}

			int startIndex = GameplayTweaksPlugin.SharedRng.Next(candidateNodeIds.Count);
			int maxAttempts = Math.Min(candidateNodeIds.Count, 24);
			for (int offset = 0; offset < maxAttempts; offset++)
			{
				int index = (startIndex + offset) % candidateNodeIds.Count;
				Node node = candidateNodeIds[index].FindNode();
				if (node == null || !node.HasRoad || node.HasActiveRaid)
				{
					continue;
				}
				if ((excludeA != null && node.id == excludeA.id) || (excludeB != null && node.id == excludeB.id))
				{
					continue;
				}
				return node;
			}

			foreach (NodeID nodeId in candidateNodeIds)
			{
				Node node = nodeId.FindNode();
				if (node == null || !node.HasRoad || node.HasActiveRaid)
				{
					continue;
				}
				if ((excludeA != null && node.id == excludeA.id) || (excludeB != null && node.id == excludeB.id))
				{
					continue;
				}
				return node;
			}

			return null;
		}

		internal static bool TryChooseAmbientTrafficRouteNodes(out Node startNode, out Node midNode, out Node endNode)
		{
			startNode = null;
			midNode = null;
			endNode = null;
			EnsureAmbientRoadNodeCache();
			if (_cachedAmbientRoadNodeIds.Count <= 1)
			{
				return false;
			}

			List<NodeID> midPool = _cachedAmbientVisitNodeIds.Count > 0 ? _cachedAmbientVisitNodeIds : _cachedAmbientRoadNodeIds;
			midNode = PickAmbientRoadNode(midPool);
			if (midNode == null)
			{
				return false;
			}

			startNode = PickAmbientRoadNode(_cachedAmbientRoadNodeIds, midNode);
			if (startNode == null)
			{
				return false;
			}

			endNode = PickAmbientRoadNode(_cachedAmbientRoadNodeIds, midNode, startNode) ?? startNode;
			return endNode != null;
		}

		internal static Label ChooseAmbientVehicleTemplate(TransitManager transitManager)
		{
			_ = transitManager;
			// Ambient traffic is intentionally truck-only now.
			return TransitManager.AMBIENT_CAR;
		}

		private static bool IsAmbientNightTime()
		{
			float timeOfDay = global::Game.Game.ctx?.seasons?.FindDisplayedTimeOfDay() ?? 12f;
			return timeOfDay < 6f || timeOfDay >= 20f;
		}

		private static int GetAmbientPopulationScaledDayTarget()
		{
			int cityPopulation = GetAmbientCityPopulation();
			int scaledTarget = (int)Math.Ceiling(cityPopulation / 3000.0);
			if (scaledTarget < AmbientVisibleTrafficDayFloor)
			{
				return AmbientVisibleTrafficDayFloor;
			}

			return Math.Min(AmbientVisibleTrafficMaxTarget, scaledTarget);
		}

		private static int GetAmbientCityPopulation()
		{
			try
			{
				List<Node> allNodes = global::Game.Game.ctx?.board?.nodes?.GetAllNodesUnsafe();
				int boardKey = GetAmbientBoardCacheKey();
				if (allNodes == null || allNodes.Count <= 0)
				{
					_cachedAmbientCityPopulation = -1;
					_cachedAmbientCityPopulationNodeCount = -1;
					_cachedAmbientCityPopulationBoardKey = -1;
					return 0;
				}

				if (_cachedAmbientCityPopulation >= 0
					&& _cachedAmbientCityPopulationNodeCount == allNodes.Count
					&& _cachedAmbientCityPopulationBoardKey == boardKey)
				{
					return _cachedAmbientCityPopulation;
				}

				int totalPopulation = 0;
				foreach (Node node in allNodes)
				{
					if (node != null && node.population > 0)
					{
						totalPopulation += node.population;
					}
				}

				_cachedAmbientCityPopulation = Math.Max(0, totalPopulation);
				_cachedAmbientCityPopulationNodeCount = allNodes.Count;
				_cachedAmbientCityPopulationBoardKey = boardKey;
				return _cachedAmbientCityPopulation;
			}
			catch
			{
				return 0;
			}
		}

		internal static double GetAmbientTerritoryCap()
		{
			double minimumCap = GetAmbientVisibleTrafficHardCap();
			try
			{
				int boardKey = GetAmbientBoardCacheKey();
				int frame = Time.frameCount;
				if (_cachedAmbientTerritoryCapFrame == frame && _cachedAmbientTerritoryCapBoardKey == boardKey && _cachedAmbientTerritoryCap >= 0.0)
				{
					return _cachedAmbientTerritoryCap;
				}

				PlayerTerritory territory = global::Game.Game.ctx?.players?.Human?.territory;
				if (territory == null)
				{
					return minimumCap;
				}

				if (_ambientOwnedNodesMethod == null)
				{
					_ambientOwnedNodesMethod = territory.GetType().GetMethod("GetAllOwnedNodesUnsafe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}

				double cap = minimumCap;
				if (_ambientOwnedNodesMethod != null)
				{
					object ownedNodes = _ambientOwnedNodesMethod.Invoke(territory, null);
					if (ownedNodes is System.Collections.ICollection collection)
					{
						cap = Math.Max(minimumCap, Math.Ceiling((float)collection.Count / 2f));
					}
				}

				_cachedAmbientTerritoryCap = cap;
				_cachedAmbientTerritoryCapFrame = frame;
				_cachedAmbientTerritoryCapBoardKey = boardKey;
				return cap;
			}
			catch
			{
				return minimumCap;
			}
		}

		internal static float GetAmbientPauseDelay(Label template, bool betweenSegments)
		{
			_ = template;
			return betweenSegments ? 1.5f : 0.25f;
		}

		internal static int GetCommandOwnerKey(CommandGoto command)
		{
			return command != null ? RuntimeHelpers.GetHashCode(command) : 0;
		}

		private static bool TryGetActiveHumanVehicleTravelState(EntityID vehicleId, out PendingVehicleTravelState state)
		{
			state = default;
			return vehicleId.IsValid
				&& _activeHumanVehicleTravel.Contains((long)vehicleId.id)
				&& _pendingVehicleTravelByVehicleId.TryGetValue((long)vehicleId.id, out state);
		}

		internal static bool TryGetHumanVehicleTravelOwner(EntityID vehicleId, out int activeCommandKey)
		{
			activeCommandKey = 0;
			return TryGetActiveHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state) && (activeCommandKey = state.CommandOwnerKey) != 0;
		}

		internal static bool TryGetHumanVehicleTravelConflict(EntityID vehicleId, CommandGoto command, out int activeCommandKey, out int blockedCommandKey, out bool sameOwner)
		{
			activeCommandKey = 0;
			blockedCommandKey = GetCommandOwnerKey(command);
			sameOwner = false;
			if (!TryGetActiveHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state))
			{
				return false;
			}

			activeCommandKey = state.CommandOwnerKey;
			if (IsSameHumanVehicleRouteCommand(state, command))
			{
				LogVehicleAuthority(
					"travel-conflict-same-route-active",
					$"{vehicleId.id}:{activeCommandKey}:{blockedCommandKey}:{command.goalID}",
					$"travel-conflict-same-route-active vehicle={vehicleId.id} activeCommand={activeCommandKey} replacementCommand={blockedCommandKey} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} commandGoal={command.goalID}",
					dedupe: true);
				sameOwner = activeCommandKey == blockedCommandKey;
				return true;
			}
			if (activeCommandKey == QueuedRouteResumeCommandOwnerKey
				&& blockedCommandKey != 0
				&& blockedCommandKey != activeCommandKey)
			{
				LogVehicleAuthority(
					"route-resume-replacement-deferred",
					$"{vehicleId.id}:{blockedCommandKey}:{state.GoalNodeID}",
					$"route-resume-replacement-deferred vehicle={vehicleId.id} replacementCommand={blockedCommandKey} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID}",
					dedupe: true);
				sameOwner = false;
				return true;
			}

			if (activeCommandKey == 0)
			{
				return false;
			}

			sameOwner = activeCommandKey == blockedCommandKey;
			return true;
		}

		internal static bool ShouldDeferHumanVehicleRouteCommandUntilArrival(EntityID vehicleId, CommandGoto command, out NodeID expectedNodeId, out NodeID commandGoalId)
		{
			expectedNodeId = NodeID.INVALID;
			commandGoalId = command?.goalID ?? NodeID.INVALID;
			if (command == null
				|| !vehicleId.IsValid
				|| !command.pid.IsHumanPlayer
				|| !command.peepId.IsValid
				|| !commandGoalId.IsValid
				|| !TryGetActiveHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state)
				|| !state.ExpectedNodeID.IsValid)
			{
				return false;
			}
			if (state.CommandOwnerKey != QueuedRouteResumeCommandOwnerKey
				&& state.PeepID != command.peepId)
			{
				return false;
			}

			expectedNodeId = state.ExpectedNodeID;
			bool sameRouteCommand = commandGoalId == state.ExpectedNodeID || commandGoalId == state.GoalNodeID;
			if (sameRouteCommand)
			{
				LogVehicleAuthority(
					"route-same-command-deferred",
					$"{vehicleId.id}:{state.ExpectedNodeID}:{commandGoalId}",
					$"route-same-command-deferred vehicle={vehicleId.id} peep={command.peepId.id} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} commandGoal={commandGoalId} reason=await-active-arrival",
					dedupe: true);
				return true;
			}

			LogVehicleAuthority(
				"route-command-deferred",
				$"{vehicleId.id}:{state.ExpectedNodeID}:{commandGoalId}",
				$"route-command-deferred vehicle={vehicleId.id} peep={command.peepId.id} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} commandGoal={commandGoalId} reason=finish-active-segment-first",
				dedupe: false);
			NodeID previousGoalNodeId = state.GoalNodeID;
			state.GoalNodeID = commandGoalId;
			state.ResumeQueued = commandGoalId != state.ExpectedNodeID;
			_pendingVehicleTravelByVehicleId[(long)vehicleId.id] = state;
			RecordQueuedHumanVehicleDestinationPreview(vehicleId, state.ExpectedNodeID, commandGoalId, command.peepId, "route-command-queued-after-arrival");
			LogVehicleAuthority(
				"route-command-queued-after-arrival",
				$"{vehicleId.id}:{state.ExpectedNodeID}:{previousGoalNodeId}:{commandGoalId}",
				$"route-command-queued-after-arrival vehicle={vehicleId.id} peep={command.peepId.id} expectedNode={state.ExpectedNodeID} previousFinalGoal={previousGoalNodeId} queuedFinalGoal={commandGoalId} resumeQueued={state.ResumeQueued} reason=finish-active-segment-first",
				dedupe: false);
			return true;
		}

		private static void RecordQueuedHumanVehicleDestinationPreview(EntityID vehicleId, NodeID expectedNodeId, NodeID queuedGoalNodeId, EntityID peepId, string source)
		{
			if (!vehicleId.IsValid
				|| !expectedNodeId.IsValid
				|| !queuedGoalNodeId.IsValid
				|| queuedGoalNodeId == expectedNodeId)
			{
				return;
			}

			long vehicleKey = unchecked((long)vehicleId.id);
			_recentQueuedHumanVehicleDestinationPreviewByVehicleId[vehicleKey] = queuedGoalNodeId;
			_recentQueuedHumanVehicleDestinationPreviewExpectedByVehicleId[vehicleKey] = expectedNodeId;
			_recentQueuedHumanVehicleDestinationPreviewFrameByVehicleId[vehicleKey] = Time.frameCount;

			Node expectedNode = expectedNodeId.FindNode();
			Node queuedGoalNode = queuedGoalNodeId.FindNode();
			if (queuedGoalNode != null)
			{
				GameplayTweaksPlugin.QueueDeferredSelectedVehicleUiRefresh(expectedNode, queuedGoalNode, "route-queued-preview", vehicleId);
				GameplayTweaksPlugin.RefreshQueuedRouteSelectedVehicleNodeHighlight(queuedGoalNode, vehicleId, "route-queued-preview");
			}

			LogVehicleAuthority(
				"route-queued-destination-preview",
				$"{vehicleId.id}:{expectedNodeId}:{queuedGoalNodeId}:{source}",
				$"route-queued-destination-preview vehicle={vehicleId.id} peep={peepId.id} expectedNode={expectedNodeId} queuedGoal={queuedGoalNodeId} frame={Time.frameCount} source={source}",
				dedupe: false);
		}

		private static bool ShouldPreserveRecentQueuedHumanVehicleDestinationPreview(EntityID vehicleId, PendingVehicleTravelState state)
		{
			if (!vehicleId.IsValid
				|| !state.ExpectedNodeID.IsValid
				|| !state.GoalNodeID.IsValid
				|| state.GoalNodeID == state.ExpectedNodeID
				|| !state.ResumeQueued)
			{
				return false;
			}

			return TryGetRecentQueuedHumanVehicleDestinationPreview(vehicleId, state.ExpectedNodeID, out NodeID queuedGoalNodeId)
				&& queuedGoalNodeId == state.GoalNodeID;
		}

		internal static bool TryGetRecentQueuedHumanVehicleDestinationPreview(EntityID vehicleId, NodeID expectedNodeId, out NodeID queuedGoalNodeId)
		{
			queuedGoalNodeId = NodeID.INVALID;
			if (!vehicleId.IsValid || !expectedNodeId.IsValid)
			{
				return false;
			}

			long vehicleKey = unchecked((long)vehicleId.id);
			if (!_recentQueuedHumanVehicleDestinationPreviewByVehicleId.TryGetValue(vehicleKey, out queuedGoalNodeId)
				|| !queuedGoalNodeId.IsValid
				|| queuedGoalNodeId == expectedNodeId
				|| !_recentQueuedHumanVehicleDestinationPreviewExpectedByVehicleId.TryGetValue(vehicleKey, out NodeID previewExpectedNodeId)
				|| previewExpectedNodeId != expectedNodeId
				|| !_recentQueuedHumanVehicleDestinationPreviewFrameByVehicleId.TryGetValue(vehicleKey, out int queuedFrame))
			{
				return false;
			}

			return Time.frameCount - queuedFrame <= RecentQueuedHumanVehicleDestinationPreviewPreserveFrames;
		}

		internal static bool ShouldDiscardDuplicateHumanVehicleRouteCommand(EntityID vehicleId, CommandGoto command, out NodeID expectedNodeId, out NodeID goalNodeId, out NodeID commandGoalId)
		{
			expectedNodeId = NodeID.INVALID;
			goalNodeId = NodeID.INVALID;
			commandGoalId = command?.goalID ?? NodeID.INVALID;
			if (command == null
				|| !vehicleId.IsValid
				|| !command.pid.IsHumanPlayer
				|| !command.peepId.IsValid
				|| !commandGoalId.IsValid
				|| !TryGetActiveHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state)
				|| !state.ExpectedNodeID.IsValid)
			{
				return false;
			}
			if (state.CommandOwnerKey != QueuedRouteResumeCommandOwnerKey
				&& state.PeepID != command.peepId)
			{
				return false;
			}
			if (IsHumanVehiclePhysicallyAtNode(vehicleId, state.ExpectedNodeID))
			{
				return false;
			}

			expectedNodeId = state.ExpectedNodeID;
			goalNodeId = state.GoalNodeID;
			bool duplicate = commandGoalId == state.ExpectedNodeID || commandGoalId == state.GoalNodeID;
			if (!duplicate)
			{
				return false;
			}

			LogVehicleAuthority(
				"route-duplicate-command-discarded",
				$"{vehicleId.id}:{state.ExpectedNodeID}:{state.GoalNodeID}:{commandGoalId}",
				$"route-duplicate-command-discarded vehicle={vehicleId.id} peep={command.peepId.id} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} commandGoal={commandGoalId} reason=already-routed-await-arrival",
				dedupe: true);
			return true;
		}

		private static bool IsSameHumanVehicleRouteCommand(PendingVehicleTravelState state, CommandGoto command)
		{
			if (command == null || !command.goalID.IsValid)
			{
				return false;
			}

			return state.GoalNodeID.IsValid && command.goalID == state.GoalNodeID
				|| state.ExpectedNodeID.IsValid && command.goalID == state.ExpectedNodeID;
		}

		internal static bool TryInterruptActiveHumanVehicleTravelForImmediateMove(PlayerCrew crew, Entity vehicle, EntityID vehicleId, CommandGoto command, string sourceTag)
		{
			if (crew == null || vehicle == null || !vehicleId.IsValid)
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			if (!_activeHumanVehicleTravel.Contains(vehicleKey))
			{
				return false;
			}

			_pendingVehicleTravelByVehicleId.TryGetValue(vehicleKey, out PendingVehicleTravelState state);
			NodeID committedNodeId = NodeID.INVALID;
			string committedSource = "none";
			NodeID liveNodeId = NodeID.INVALID;
			string liveSource = "none";
			bool freshDestinationReplacement = state.ExpectedNodeID.IsValid
				&& command?.goalID.IsValid == true
				&& command.goalID != state.ExpectedNodeID
				&& command.goalID != state.GoalNodeID;
			if (freshDestinationReplacement)
			{
				committedNodeId = state.ExpectedNodeID;
				committedSource = "logical-pending-expected";
				LogVehicleAuthority(
					"travel-interrupt-logical-commit",
					$"{vehicleId.id}:{state.ExpectedNodeID}:{state.GoalNodeID}:{command.goalID}:{sourceTag}",
					$"travel-interrupt-logical-commit vehicle={vehicleId.id} expectedNode={state.ExpectedNodeID} priorGoal={state.GoalNodeID} replacementGoal={command.goalID} source={sourceTag} reason=fresh-destination-preferred-for-cost",
					dedupe: false);
			}
			if (!committedNodeId.IsValid
				&& TryGetVehicleLiveAuthorityNodeId(vehicle, out liveNodeId, out liveSource)
				&& liveNodeId.IsValid
				&& IsHumanVehiclePhysicallyAtNode(vehicleId, liveNodeId))
			{
				committedNodeId = liveNodeId;
				committedSource = "physical-" + liveSource;
			}
			if (!committedNodeId.IsValid
				&& TryGetQueuedArrivalCommittedNodeId(vehicleId, out NodeID queuedNodeId)
				&& queuedNodeId.IsValid
				&& IsHumanVehiclePhysicallyAtNode(vehicleId, queuedNodeId))
			{
				committedNodeId = queuedNodeId;
				committedSource = "physical-queued-arrival";
			}
			if (!committedNodeId.IsValid
				&& TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentNodeId)
				&& recentNodeId.IsValid
				&& IsHumanVehiclePhysicallyAtNode(vehicleId, recentNodeId))
			{
				committedNodeId = recentNodeId;
				committedSource = "physical-recent-finalize";
			}
			if (!committedNodeId.IsValid
				&& state.ExpectedNodeID.IsValid
				&& IsHumanVehiclePhysicallyAtNode(vehicleId, state.ExpectedNodeID))
			{
				committedNodeId = state.ExpectedNodeID;
				committedSource = "physical-pending-expected";
			}
			if (!committedNodeId.IsValid
				&& freshDestinationReplacement)
			{
				committedNodeId = state.ExpectedNodeID;
				committedSource = "logical-pending-expected";
				LogVehicleAuthority(
					"travel-interrupt-logical-commit",
					$"{vehicleId.id}:{state.ExpectedNodeID}:{state.GoalNodeID}:{command.goalID}:{sourceTag}",
					$"travel-interrupt-logical-commit vehicle={vehicleId.id} expectedNode={state.ExpectedNodeID} priorGoal={state.GoalNodeID} replacementGoal={command.goalID} source={sourceTag} reason=fresh-destination-before-physical-arrival",
					dedupe: false);
			}
			if (!committedNodeId.IsValid)
			{
				LogVehicleAuthority(
					"travel-interrupt-failed",
					$"{vehicleId.id}:{GetCommandOwnerKey(command)}:{sourceTag}",
					$"travel-interrupt-failed vehicle={vehicleId.id} replacementCommand={GetCommandOwnerKey(command)} source={sourceTag} reason=no-physical-committed-node expectedNode={state.ExpectedNodeID} liveNode={liveNodeId} liveSource={liveSource}",
					dedupe: false);
				return false;
			}

			int replacementCommandKey = GetCommandOwnerKey(command);
			int activeCommandKey = state.CommandOwnerKey;
			NodeID startNodeId = state.StartNodeID;
			NodeID goalNodeId = state.GoalNodeID;
			if (startNodeId.IsValid && startNodeId != committedNodeId)
			{
				RecordHumanVehicleFinalizedSegment(vehicleId, startNodeId, committedNodeId);
			}
			_queuedArrivalCommittedNodeByVehicleId[vehicleKey] = committedNodeId;
			SetRecentFinalizedNode(vehicleId, committedNodeId);

			try
			{
				vehicle.components?.script?.queue?.StopAllScripts(success: false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryInterruptActiveHumanVehicleTravelForImmediateMove: " + ex.Message);
			}

			bool syncVehicleToLogicalCommit = string.Equals(committedSource, "logical-pending-expected", StringComparison.Ordinal);
			TrySyncVehicleOccupantsToNode(crew, vehicleId, committedNodeId, "travel-interrupt-" + sourceTag, syncVehicle: syncVehicleToLogicalCommit);
			if (syncVehicleToLogicalCommit)
			{
				LogVehicleAuthority(
					"travel-interrupt-visual-sync",
					$"{vehicleId.id}:{committedNodeId}:{sourceTag}",
					$"travel-interrupt-visual-sync vehicle={vehicleId.id} node={committedNodeId} source={sourceTag} reason=logical-commit-before-replacement",
					dedupe: false);
			}
			ClearPendingHumanVehicleTravel(vehicleId, "fresh-command-override");
			ClearInterruptExpectedStartNode(vehicleId, "fresh-command-override");

			LogVehicleAuthority(
				"travel-interrupt-commit",
				$"{vehicleId.id}:{activeCommandKey}:{replacementCommandKey}:{committedNodeId}:{sourceTag}",
				$"travel-interrupt-commit vehicle={vehicleId.id} activeCommand={activeCommandKey} replacementCommand={replacementCommandKey} committedNode={committedNodeId} committedSource={committedSource} priorStart={startNodeId} priorGoal={goalNodeId} source={sourceTag}",
				dedupe: false);
			return true;
		}

		internal static void MarkHumanVehicleTravelActive(EntityID vehicleId, EntityID peepId, int commandOwnerKey, NodeID startNodeId, NodeID expectedNodeId, NodeID goalNodeId)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}
			long vehicleKey = (long)vehicleId.id;
			RouteShopStagingState.OnVehicleRouteChanged(vehicleId, expectedNodeId, goalNodeId, "segment-start");
			if (_activeHumanVehicleTravel.Contains(vehicleKey)
				&& _pendingVehicleTravelByVehicleId.TryGetValue(vehicleKey, out PendingVehicleTravelState activeState)
				&& activeState.CommandOwnerKey == QueuedRouteResumeCommandOwnerKey
				&& commandOwnerKey != 0
				&& commandOwnerKey != QueuedRouteResumeCommandOwnerKey)
			{
				LogVehicleAuthority(
					"route-resume-replacement-blocked",
					$"{vehicleId.id}:{commandOwnerKey}:{activeState.GoalNodeID}:segment-start",
					$"route-resume-replacement-blocked vehicle={vehicleId.id} replacementCommand={commandOwnerKey} expectedNode={activeState.ExpectedNodeID} finalGoal={activeState.GoalNodeID} phase=segment-start",
					dedupe: false);
				return;
			}
			ClearInterruptExpectedStartNode(vehicleId, "segment-start");
			_turnStartFinalizedQueuedRouteVehicleIds.Remove(vehicleKey);
			_queuedArrivalCommittedNodeByVehicleId.Remove(vehicleKey);
			if (commandOwnerKey != 0
				&& commandOwnerKey != QueuedRouteResumeCommandOwnerKey
				&& goalNodeId.IsValid)
			{
				if (TryGetRecentFinalizedNodeId(vehicleId, out NodeID staleRecentFinalizedNodeId)
					&& staleRecentFinalizedNodeId.IsValid
					&& (!startNodeId.IsValid || staleRecentFinalizedNodeId != startNodeId))
				{
					LogVehicleAuthority(
						"recent-finalize-authority-suppressed",
						$"{vehicleId.id}:{staleRecentFinalizedNodeId}:{startNodeId}:{goalNodeId}:travel-active",
						$"recent-finalize-authority-suppressed vehicle={vehicleId.id} staleNode={staleRecentFinalizedNodeId} committedNode={startNodeId} finalGoal={goalNodeId} phase=travel-active",
						dedupe: false);
				}

				ClearRecentFinalizedNode(vehicleId, "fresh-command-owner");
			}
			if (startNodeId.IsValid)
			{
				SetRecentFinalizedNode(vehicleId, startNodeId);
				LogVehicleAuthority("human-start-preserved", $"{vehicleId.id}:{startNodeId}:segment-start", $"human-start-preserved vehicle={vehicleId.id} node={startNodeId} reason=segment-start source=command-start", dedupe: false);
			}
			if (_pendingVehicleTravelByVehicleId.TryGetValue(vehicleKey, out PendingVehicleTravelState staleState) && !_activeHumanVehicleTravel.Contains(vehicleKey))
			{
				ClearPendingHumanVehicleTravel(vehicleId, GetPendingTravelClearReason(staleState, "replaced"));
			}
			if (_activeHumanVehicleTravel.Contains(vehicleKey))
			{
				if (_activeHumanVehicleTravel.Contains(vehicleKey))
				{
					if (_pendingVehicleTravelByVehicleId.TryGetValue(vehicleKey, out PendingVehicleTravelState existingState))
					{
						LogVehicleAuthority("travel-segment-keep", $"{vehicleId.id}:{existingState.ExpectedNodeID}:{expectedNodeId}", $"travel-segment-keep vehicle={vehicleId.id} existingExpectedNode={existingState.ExpectedNodeID} newExpectedNode={expectedNodeId} goalNode={goalNodeId}", dedupe: false);
					}
					return;
				}
			}
			_activeHumanVehicleTravel.Add(vehicleKey);
			_pendingVehicleTravelByVehicleId[vehicleKey] = new PendingVehicleTravelState
			{
				VehicleID = vehicleId,
				PeepID = peepId,
				CommandOwnerKey = commandOwnerKey,
				StartNodeID = startNodeId,
				ExpectedNodeID = expectedNodeId,
				GoalNodeID = goalNodeId,
				ResumeQueued = goalNodeId.IsValid && expectedNodeId.IsValid && goalNodeId != expectedNodeId
			};
			LogVehicleAuthority("travel-segment-start", $"{vehicleId.id}:{startNodeId}:{expectedNodeId}:{goalNodeId}", $"travel-segment-start vehicle={vehicleId.id} startNode={startNodeId} expectedNode={expectedNodeId} goalNode={goalNodeId}", dedupe: false);
			if (goalNodeId.IsValid && expectedNodeId.IsValid && goalNodeId != expectedNodeId)
			{
				LogVehicleAuthority("route-queued", $"{vehicleId.id}:{goalNodeId}", $"route-queued vehicle={vehicleId.id} finalGoal={goalNodeId}", dedupe: false);
			}
		}

		private static string GetPendingTravelClearReason(PendingVehicleTravelState state, string defaultReason)
		{
			if (!state.StartNodeID.IsValid || !state.GoalNodeID.IsValid)
			{
				return "setup-leftover";
			}
			if (!state.ExpectedNodeID.IsValid && !state.ResumeQueued)
			{
				return "setup-leftover";
			}
			return defaultReason;
		}

		internal static bool TryGetPendingHumanVehicleTravel(EntityID vehicleId, out EntityID peepId, out NodeID expectedNodeId, out NodeID goalNodeId)
		{
			peepId = EntityID.INVALID;
			expectedNodeId = NodeID.INVALID;
			goalNodeId = NodeID.INVALID;
			if (!vehicleId.IsValid || !_pendingVehicleTravelByVehicleId.TryGetValue((long)vehicleId.id, out PendingVehicleTravelState state))
			{
				return false;
			}
			peepId = state.PeepID;
			expectedNodeId = state.ExpectedNodeID;
			goalNodeId = state.GoalNodeID;
			return true;
		}

		internal static bool TryGetQueuedHumanVehicleGoal(EntityID vehicleId, out EntityID peepId, out NodeID goalNodeId)
		{
			peepId = EntityID.INVALID;
			goalNodeId = NodeID.INVALID;
			if (!TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state) || !state.ResumeQueued || !state.GoalNodeID.IsValid)
			{
				return false;
			}
			peepId = state.PeepID;
			goalNodeId = state.GoalNodeID;
			return true;
		}

		internal static bool TryGetHumanVehicleTravelDisplayFinalNodeId(EntityID vehicleId, NodeID fallbackNodeId, out NodeID displayNodeId, out string source)
		{
			displayNodeId = fallbackNodeId;
			source = fallbackNodeId.IsValid ? "segment-final" : "none";
			if (!vehicleId.IsValid)
			{
				return displayNodeId.IsValid;
			}

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.GoalNodeID.IsValid
				&& (pendingState.ExpectedNodeID.IsValid
					|| pendingState.ResumeQueued
					|| IsHumanVehicleTravelActive(vehicleId)))
			{
				if (pendingState.ExpectedNodeID.IsValid && IsHumanVehicleTravelActive(vehicleId))
				{
					displayNodeId = pendingState.ExpectedNodeID;
					source = "active-segment-target";
					return true;
				}
				bool knownOrReached = IsHumanVehiclePreviewNodeKnownOrReached(vehicleId, pendingState.GoalNodeID);
				displayNodeId = pendingState.GoalNodeID;
				source = pendingState.ResumeQueued && !pendingState.ExpectedNodeID.IsValid
					? "queued-final-goal"
					: "route-final-goal";
				if (!knownOrReached)
				{
					LogVehicleAuthority(
						"display-final-goal-preview",
						$"{vehicleId.id}:{pendingState.ExpectedNodeID}:{pendingState.GoalNodeID}",
						$"display-final-goal-preview vehicle={vehicleId.id} fallbackNode={fallbackNodeId} finalGoal={pendingState.GoalNodeID} reason=unknown-preview-only",
						dedupe: false);
				}
				return true;
			}

			if (TryGetQueuedHumanVehicleGoal(vehicleId, out _, out NodeID queuedGoalNodeId)
				&& queuedGoalNodeId.IsValid
				&& IsHumanVehiclePreviewNodeKnownOrReached(vehicleId, queuedGoalNodeId))
			{
				displayNodeId = queuedGoalNodeId;
				source = "queued-final-goal";
				return true;
			}

			return displayNodeId.IsValid;
		}

		internal static bool TryGetPendingHumanVehicleTravelStartNode(EntityID vehicleId, out NodeID startNodeId)
		{
			startNodeId = NodeID.INVALID;
			if (!TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state))
			{
				return false;
			}
			startNodeId = state.StartNodeID;
			return startNodeId.IsValid;
		}

		private static bool TryGetPendingHumanVehicleTravelState(EntityID vehicleId, out PendingVehicleTravelState state)
		{
			state = default;
			return vehicleId.IsValid && _pendingVehicleTravelByVehicleId.TryGetValue((long)vehicleId.id, out state);
		}

		private static bool TryRebindPendingHumanVehicleTravelToLiveDriver(PlayerCrew crew, PendingVehicleTravelState state, out PendingVehicleTravelState updatedState, out CrewAssignment assignment, out string reason)
		{
			updatedState = state;
			assignment = CrewAssignment.EMPTY;
			reason = "none";
			if (crew == null || !state.VehicleID.IsValid)
			{
				reason = "invalid-context";
				return false;
			}

			if (state.PeepID.IsValid)
			{
				CrewAssignment existing = crew.GetCrewForPeep(state.PeepID);
				if (existing.IsValid && existing.IsInVehicle && existing.VehicleID == state.VehicleID && IsActiveVehicleOccupant(crew, existing))
				{
					assignment = existing;
					reason = "stored-peep-valid";
					return true;
				}
			}

			EntityID driverPeepId = GetDriverPeepId(crew, state.VehicleID);
			if (driverPeepId.IsValid)
			{
				CrewAssignment driverAssignment = crew.GetCrewForPeep(driverPeepId);
				if (driverAssignment.IsValid && driverAssignment.IsInVehicle && driverAssignment.VehicleID == state.VehicleID && IsActiveVehicleOccupant(crew, driverAssignment))
				{
					updatedState.PeepID = driverPeepId;
					_pendingVehicleTravelByVehicleId[(long)state.VehicleID.id] = updatedState;
					assignment = driverAssignment;
					reason = state.PeepID.IsValid ? "dead-route-peep-driver-handoff" : "missing-route-peep-driver-handoff";
					return true;
				}
			}

			CrewAssignment survivor = GetAllCrewInVehicle(crew, state.VehicleID)
				.Where(item => item.IsValid && item.IsInVehicle && item.VehicleID == state.VehicleID && IsActiveVehicleOccupant(crew, item))
				.OrderBy(item => item.peepId.id)
				.FirstOrDefault();
			if (survivor.IsValid)
			{
				SetDriverInternal(state.VehicleID, survivor.peepId);
				updatedState.PeepID = survivor.peepId;
				_pendingVehicleTravelByVehicleId[(long)state.VehicleID.id] = updatedState;
				assignment = survivor;
				reason = "survivor-driver-handoff";
				return true;
			}

			reason = "no-live-occupants";
			return false;
		}

		private static bool TryReassignPendingHumanVehicleTravelDriver(EntityID vehicleId, EntityID driverPeepId, string sourceTag)
		{
			if (!vehicleId.IsValid || !driverPeepId.IsValid)
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			if (!_pendingVehicleTravelByVehicleId.TryGetValue(vehicleKey, out PendingVehicleTravelState state)
				|| (!state.ExpectedNodeID.IsValid && !state.GoalNodeID.IsValid && !state.ResumeQueued))
			{
				return false;
			}

			EntityID priorPeepId = state.PeepID;
			state.PeepID = driverPeepId;
			_pendingVehicleTravelByVehicleId[vehicleKey] = state;

			LogVehicleAuthority(
				"driver-switch-route-preserved",
				$"{vehicleId.id}:{priorPeepId.id}:{driverPeepId.id}:{state.StartNodeID}:{state.ExpectedNodeID}:{state.GoalNodeID}:{sourceTag}",
				$"driver-switch-route-preserved vehicle={vehicleId.id} priorPeep={priorPeepId.id} newDriver={driverPeepId.id} startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} goalNode={state.GoalNodeID} resumeQueued={state.ResumeQueued} active={_activeHumanVehicleTravel.Contains(vehicleKey)} source={sourceTag}",
				dedupe: false);
			return true;
		}

		internal static bool HasQueuedHumanVehiclePendingResume(EntityID vehicleId)
		{
			return TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState state)
				&& state.ResumeQueued
				&& state.GoalNodeID.IsValid
				&& !_activeHumanVehicleTravel.Contains((long)vehicleId.id);
		}

		internal static bool TryConsumeTurnStartDeferredHumanVehicleArrival(EntityID vehicleId, string sourceTag)
		{
			if (!vehicleId.IsValid)
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			bool consumed = _turnStartDeferredArrivalVehicleIds.Remove(vehicleKey);
			if (consumed)
			{
				LogVehicleAuthority(
					"turnstart-deferred-arrival-consumed",
					$"{vehicleId.id}:{sourceTag}",
					$"turnstart-deferred-arrival-consumed vehicle={vehicleId.id} source={sourceTag}",
					dedupe: false);
			}

			return consumed;
		}

		internal static bool ShouldRecordSelectedVehicleUiFinalNode(EntityID vehicleId, NodeID finalNodeId, string sourceTag)
		{
			if (!vehicleId.IsValid || !finalNodeId.IsValid)
			{
				return false;
			}

			if (!TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				|| !pendingState.GoalNodeID.IsValid)
			{
				return true;
			}

			bool hasActiveOrQueuedGoal = pendingState.ExpectedNodeID.IsValid
				|| pendingState.ResumeQueued
				|| IsHumanVehicleTravelActive(vehicleId);
			if (!hasActiveOrQueuedGoal || pendingState.GoalNodeID == finalNodeId)
			{
				return true;
			}

			LogVehicleAuthority("selected-final-goal-memory-preserved", $"{vehicleId.id}:{pendingState.GoalNodeID}:{finalNodeId}:{sourceTag}", $"selected-final-goal-memory-preserved vehicle={vehicleId.id} rememberedFinal={pendingState.GoalNodeID} skippedFinal={finalNodeId} source={sourceTag}", dedupe: false);
			return false;
		}

		internal static bool TryGetQueuedArrivalCommittedNodeId(EntityID vehicleId, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			return vehicleId.IsValid && _queuedArrivalCommittedNodeByVehicleId.TryGetValue((long)vehicleId.id, out nodeId) && nodeId.IsValid;
		}

		internal static bool TryGetQueuedArrivalCommittedNode(EntityID vehicleId, out Node node)
		{
			node = null;
			if (!TryGetQueuedArrivalCommittedNodeId(vehicleId, out NodeID nodeId))
			{
				return false;
			}
			node = nodeId.FindNode();
			return node != null;
		}

		internal static bool TryGetFinalizedVehicleNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}
			if (TryGetQueuedArrivalCommittedNodeId(vehicleId, out nodeId) && nodeId.IsValid)
			{
				source = "queued-arrival";
				return true;
			}
			if (TryGetRecentFinalizedNodeId(vehicleId, out nodeId) && nodeId.IsValid)
			{
				source = "recent-finalize";
				return true;
			}
			nodeId = NodeID.INVALID;
			source = "none";
			return false;
		}

		internal static void ClearPendingHumanVehicleTravel(EntityID vehicleId, string reason = "clear")
		{
			if (!vehicleId.IsValid)
			{
				return;
			}
			long vehicleKey = (long)vehicleId.id;
			bool hadPending = _pendingVehicleTravelByVehicleId.TryGetValue(vehicleKey, out PendingVehicleTravelState state);
			if (hadPending)
			{
				PreserveHumanCommittedAuthorityOnPendingTravelClear(vehicleId, state, reason);
			}
			_activeHumanVehicleTravel.Remove(vehicleKey);
			_turnStartFinalizedQueuedRouteVehicleIds.Remove(vehicleKey);
			_turnStartDeferredArrivalVehicleIds.Remove(vehicleKey);
			_pendingVehicleTravelByVehicleId.Remove(vehicleKey);
			_queuedArrivalCommittedNodeByVehicleId.Remove(vehicleKey);
			if (!ShouldPreserveHumanCommittedAuthorityOnPendingTravelClear(reason))
			{
				_recentFinalizedNodeByVehicleId.Remove(vehicleKey);
			}
			if (ShouldClearSelectedFinalGoalOnPendingTravelClear(reason))
			{
				GameplayTweaksPlugin.ClearSelectedVehicleUiFinalNode(vehicleId, "pending-clear-" + reason);
			}
			if (hadPending)
			{
				RouteShopStagingState.ClearForVehicle(vehicleId, reason, "pending-travel-clear");
				LogVehicleAuthority("pending-travel-clear", $"{vehicleId.id}:{state.ExpectedNodeID}:{reason}", $"pending-travel-cleared vehicle={vehicleId.id} startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} goalNode={state.GoalNodeID} reason={reason}", dedupe: false);
				if (state.ResumeQueued || state.GoalNodeID.IsValid)
				{
					LogVehicleAuthority("route-cleared", $"{vehicleId.id}:{state.GoalNodeID}:{reason}", $"route-cleared vehicle={vehicleId.id} reason={reason} finalGoal={state.GoalNodeID}", dedupe: false);
				}
			}
		}

		internal static void ClearInactivePendingHumanVehicleTravel(string reason = "stale-turn-start")
		{
			if (_pendingVehicleTravelByVehicleId.Count <= 0)
			{
				return;
			}

			List<long> staleVehicleKeys = null;
			foreach (KeyValuePair<long, PendingVehicleTravelState> pair in _pendingVehicleTravelByVehicleId)
			{
				if (_activeHumanVehicleTravel.Contains(pair.Key))
				{
					continue;
				}
				if (pair.Value.ResumeQueued && pair.Value.GoalNodeID.IsValid)
				{
					continue;
				}
				if (staleVehicleKeys == null)
				{
					staleVehicleKeys = new List<long>();
				}
				staleVehicleKeys.Add(pair.Key);
			}

			if (staleVehicleKeys == null || staleVehicleKeys.Count <= 0)
			{
				return;
			}

			foreach (long vehicleKey in staleVehicleKeys)
			{
				if (_pendingVehicleTravelByVehicleId.TryGetValue(vehicleKey, out PendingVehicleTravelState state))
				{
					ClearPendingHumanVehicleTravel(EntityID.FromID((ulong)vehicleKey), GetPendingTravelClearReason(state, reason));
				}
			}
		}

		internal static bool CompleteHumanVehicleTravelSegment(EntityID vehicleId, NodeID finalNodeId, out bool routeQueued)
		{
			routeQueued = false;
			if (!vehicleId.IsValid || !_pendingVehicleTravelByVehicleId.TryGetValue((long)vehicleId.id, out PendingVehicleTravelState state))
			{
				return false;
			}

			_activeHumanVehicleTravel.Remove((long)vehicleId.id);
			if (!finalNodeId.IsValid)
			{
				ClearPendingHumanVehicleTravel(vehicleId, "invalid");
				return false;
			}
			RouteShopStagingState.OnVehicleArrived(vehicleId, finalNodeId, "segment-arrived");

			if (!state.ResumeQueued || !state.GoalNodeID.IsValid)
			{
				RecordHumanVehicleFinalizedSegment(vehicleId, state.StartNodeID, finalNodeId);
				ClearPendingHumanVehicleTravel(vehicleId, "segment-complete");
				return false;
			}

			if (finalNodeId == state.GoalNodeID)
			{
				RecordHumanVehicleFinalizedSegment(vehicleId, state.StartNodeID, finalNodeId);
				ClearPendingHumanVehicleTravel(vehicleId, "arrived");
				return false;
			}

			if (state.StartNodeID.IsValid && finalNodeId == state.StartNodeID)
			{
				bool awaitingNextResume = state.GoalNodeID.IsValid && state.GoalNodeID != finalNodeId;
				string reason = awaitingNextResume ? "segment-committed-awaiting-resume" : "same-node-final-goal";
				LogVehicleAuthority(
					"route-resume-deferred",
					$"{vehicleId.id}:{finalNodeId}:{state.GoalNodeID}:{reason}",
					$"route-resume-deferred vehicle={vehicleId.id} reason={reason} currentNode={finalNodeId} startNode={state.StartNodeID} finalGoal={state.GoalNodeID} resumeQueued={awaitingNextResume}",
					dedupe: false);
				SetRecentFinalizedNode(vehicleId, finalNodeId);
				state.ExpectedNodeID = NodeID.INVALID;
				state.CommandOwnerKey = 0;
				state.ResumeQueued = awaitingNextResume;
				_pendingVehicleTravelByVehicleId[(long)vehicleId.id] = state;
				if (!state.ResumeQueued)
				{
					ClearPendingHumanVehicleTravel(vehicleId, "arrived");
				}
				return false;
			}

			RecordHumanVehicleFinalizedSegment(vehicleId, state.StartNodeID, finalNodeId);
			if (ShouldSuppressHumanVehicleRequeue(vehicleId, finalNodeId, state.GoalNodeID))
			{
				LogVehicleAuthority(
					"route-resume-skip",
					$"{vehicleId.id}:{finalNodeId}:{state.GoalNodeID}:requeue-suppressed",
					$"route-resume-skip vehicle={vehicleId.id} reason=requeue-suppressed currentNode={finalNodeId} finalGoal={state.GoalNodeID}",
					dedupe: false);
				ClearPendingHumanVehicleTravel(vehicleId, "requeue-suppressed");
				return false;
			}

			_queuedArrivalCommittedNodeByVehicleId[(long)vehicleId.id] = finalNodeId;
			SetRecentFinalizedNode(vehicleId, finalNodeId);
			state.StartNodeID = finalNodeId;
			state.ExpectedNodeID = NodeID.INVALID;
			state.CommandOwnerKey = 0;
			state.ResumeQueued = true;
			_pendingVehicleTravelByVehicleId[(long)vehicleId.id] = state;
			routeQueued = true;
			LogVehicleAuthority(
				"route-owned",
				$"{vehicleId.id}:{state.PeepID.id}:{state.GoalNodeID}",
				$"route-owned vehicle={vehicleId.id} peep={state.PeepID.id} finalGoal={state.GoalNodeID}",
				dedupe: false);
			return true;
		}

		internal static bool TryFinalizeHumanVehicleTravelStop(PlayerCrew crew, Entity vehicle, out NodeID finalNodeId, out bool routeQueued)
		{
			finalNodeId = NodeID.INVALID;
			routeQueued = false;
			if (crew == null || vehicle?.Id.IsValid != true)
			{
				return false;
			}

			bool hasPendingExpected = TryGetPendingHumanVehicleTravelState(vehicle.Id, out PendingVehicleTravelState pendingState)
				&& pendingState.ExpectedNodeID.IsValid;
			if (hasPendingExpected)
			{
				bool confirmedExpectedArrival;
				try
				{
					confirmedExpectedArrival = TryConfirmHumanVehicleExpectedArrival(crew, vehicle, pendingState, out _, out _);
				}
				catch (Exception ex)
				{
					LogVehicleAuthority(
						"travel-finalize-confirm-failed",
						$"{vehicle.Id.id}:{pendingState.StartNodeID}:{pendingState.ExpectedNodeID}:{pendingState.GoalNodeID}:{ex.GetType().Name}",
						$"travel-finalize-confirm-failed vehicle={vehicle.Id.id} startNode={pendingState.StartNodeID} expectedNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID} ex={ex.GetType().Name}: {ex.Message}",
						dedupe: false);
					return false;
				}
				if (!confirmedExpectedArrival)
				{
					LogHumanVehicleExpectedArrivalDeferred("travel-finalize-deferred", vehicle.Id, pendingState, "travel-end-await-physical");
					return false;
				}

				finalNodeId = pendingState.ExpectedNodeID;
			}

			try
			{
				TrySyncVehicleOccupantsToVehicleNode(crew, vehicle.Id, "travel-end");
			}
			catch (Exception ex)
			{
				LogVehicleAuthority(
					"travel-finalize-sync-failed",
					$"{vehicle.Id.id}:{finalNodeId}:{pendingState.ExpectedNodeID}:{ex.GetType().Name}",
					$"travel-finalize-sync-failed vehicle={vehicle.Id.id} finalNode={finalNodeId} expectedNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID} hasPendingExpected={hasPendingExpected} ex={ex.GetType().Name}: {ex.Message}",
					dedupe: false);
			}
			if (!finalNodeId.IsValid && TryGetRecentFinalizedNodeId(vehicle.Id, out NodeID recentFinalizedNodeId)
				&& recentFinalizedNodeId.IsValid)
			{
				finalNodeId = recentFinalizedNodeId;
			}
			else if (TryGetAuthoritativeVehicleNodeId(vehicle, out NodeID authoritativeNodeId, out _)
				&& authoritativeNodeId.IsValid)
			{
				finalNodeId = authoritativeNodeId;
			}

			if (finalNodeId.IsValid)
			{
				_queuedArrivalCommittedNodeByVehicleId[(long)vehicle.Id.id] = finalNodeId;
				SetRecentFinalizedNode(vehicle.Id, finalNodeId);
			}
			CompleteHumanVehicleTravelSegment(vehicle.Id, finalNodeId, out routeQueued);
			return finalNodeId.IsValid;
		}

		private static bool TryConfirmHumanVehicleExpectedArrival(PlayerCrew crew, Entity vehicle, PendingVehicleTravelState state, out NodeID confirmedNodeId, out string source)
		{
			confirmedNodeId = NodeID.INVALID;
			source = "none";
			if (crew == null || vehicle?.Id.IsValid != true || !state.ExpectedNodeID.IsValid)
			{
				return false;
			}

			NodeID mobileNodeId = vehicle.components?.mobile?.FindNodeNearThisMobile() ?? NodeID.INVALID;
			if (mobileNodeId == state.ExpectedNodeID)
			{
				confirmedNodeId = mobileNodeId;
				source = "mobile";
				RecordObservedHumanVehicleReachedNode(vehicle.Id, mobileNodeId, source);
				LogVehicleAuthority(
					"delivery-route-arrival-confirmed",
					$"{vehicle.Id.id}:{mobileNodeId}:{state.GoalNodeID}:{Time.frameCount}",
					$"delivery-route-arrival-confirmed vehicle={vehicle.Id.id} node={mobileNodeId} finalGoal={state.GoalNodeID} source={source} frame={Time.frameCount}",
					dedupe: false);
				return true;
			}
			if (mobileNodeId.IsValid)
			{
				return false;
			}

			if (IsHumanVehicleStrictlyPhysicalAtNode(vehicle.Id, state.ExpectedNodeID, out string physicalSource))
			{
				confirmedNodeId = state.ExpectedNodeID;
				source = physicalSource;
				return true;
			}

			return false;
		}

		private static void LogHumanVehicleExpectedArrivalDeferred(string category, EntityID vehicleId, PendingVehicleTravelState state, string reason)
		{
			NodeID liveNodeId = NodeID.INVALID;
			string liveSource = "none";
			_ = TryGetVehicleLiveAuthorityNodeId(vehicleId, out liveNodeId, out liveSource);
			LogVehicleAuthority(
				category,
				$"{vehicleId.id}:{state.StartNodeID}:{state.ExpectedNodeID}:{state.GoalNodeID}:{reason}",
				$"{category} vehicle={vehicleId.id} startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} liveNode={liveNodeId} liveSource={liveSource} reason={reason}",
				dedupe: false);
		}

		internal static bool TryGetRecoverableHumanVehicleTravelState(EntityID vehicleId, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			if (TryGetPendingHumanVehicleTravel(vehicleId, out EntityID peepId, out NodeID expectedNodeId, out NodeID goalNodeId))
			{
				if (peepId.IsValid && peepId.FindEntity()?.data?.agent != null && expectedNodeId.IsValid)
				{
					source = "pending-expected";
					return true;
				}
			}

			if (TryGetQueuedArrivalCommittedNodeId(vehicleId, out NodeID queuedArrivalNodeId) && queuedArrivalNodeId.IsValid)
			{
				source = "queued-arrival";
				return true;
			}

			return false;
		}

		internal static void ClearVanillaQueuedCommandsForOwnedHumanRoutes(CommandExecutor executor)
		{
			if (executor?.PID.IsHumanPlayer != true || _pendingVehicleTravelByVehicleId.Count <= 0)
			{
				return;
			}

			List<PendingVehicleTravelState> queuedRoutes = _pendingVehicleTravelByVehicleId.Values
				.Where(state => state.ResumeQueued && state.VehicleID.IsValid && state.PeepID.IsValid && !_activeHumanVehicleTravel.Contains((long)state.VehicleID.id))
				.OrderBy(state => state.VehicleID.id)
				.ToList();
			foreach (PendingVehicleTravelState state in queuedRoutes)
			{
				try
				{
					PushFlushQueueRouteClearSuppression(state.PeepID);
					executor.FlushQueue(state.PeepID, cancelActive: false);
					LogVehicleAuthority(
						"route-vanilla-clear",
						$"{state.VehicleID.id}:{state.PeepID.id}:{state.GoalNodeID}",
						$"route-vanilla-clear vehicle={state.VehicleID.id} peep={state.PeepID.id} reason=mod-owned-queue finalGoal={state.GoalNodeID}",
						dedupe: false);
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] ClearVanillaQueuedCommandsForOwnedHumanRoutes: " + ex.Message);
				}
				finally
				{
					PopFlushQueueRouteClearSuppression(state.PeepID);
				}
			}
		}

		private static void LogAfterProhibitionRoutesTravelContinuationDecision(EntityID vehicleId, string sourceTag)
		{
			string normalizedSource = string.IsNullOrWhiteSpace(sourceTag) ? "unknown" : sourceTag.Trim();
			string dedupeKey = vehicleId.id + ":" + normalizedSource;
			if (!_loggedAfterProhibitionRoutesTravelContinuationDecisionKeys.Add(dedupeKey))
			{
				return;
			}

			try
			{
				if (!TryGetAfterProhibitionRoutesPluginType(out Type routesType))
				{
					LogAfterProhibitionRoutesTravelContinuationFallback("bridge-missing", normalizedSource);
					return;
				}

				_afterProhibitionRoutesOwnsTravelContinuationDecisionMethod =
					_afterProhibitionRoutesOwnsTravelContinuationDecisionMethod ?? AccessTools.Method(routesType, "OwnsTravelContinuationDecision");
				_afterProhibitionRoutesLogTravelContinuationDecisionMethod =
					_afterProhibitionRoutesLogTravelContinuationDecisionMethod ?? AccessTools.Method(routesType, "LogTravelContinuationDecision", new[] { typeof(EntityID), typeof(string) });

				if (_afterProhibitionRoutesOwnsTravelContinuationDecisionMethod == null
					|| _afterProhibitionRoutesLogTravelContinuationDecisionMethod == null)
				{
					LogAfterProhibitionRoutesTravelContinuationFallback("bridge-method-missing", normalizedSource);
					return;
				}

				object ownsValue = _afterProhibitionRoutesOwnsTravelContinuationDecisionMethod.Invoke(null, null);
				if (!(ownsValue is bool ownsDecision) || !ownsDecision)
				{
					LogAfterProhibitionRoutesTravelContinuationFallback("bridge-disabled", normalizedSource);
					return;
				}

				if (!_verboseAfterProhibitionRoutesDecisionBridge)
				{
					if (!_loggedAfterProhibitionRoutesTravelContinuationDelegated)
					{
						_loggedAfterProhibitionRoutesTravelContinuationDelegated = true;
						Debug.Log("[GameplayTweaks] RouteContinuation delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True quietBridge=True source=" + normalizedSource);
					}
					return;
				}

				object summaryValue = _afterProhibitionRoutesLogTravelContinuationDecisionMethod.Invoke(null, new object[] { vehicleId, normalizedSource });
				if (!_loggedAfterProhibitionRoutesTravelContinuationDelegated)
				{
					_loggedAfterProhibitionRoutesTravelContinuationDelegated = true;
					Debug.Log("[GameplayTweaks] RouteContinuation delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True source=" + normalizedSource + " " + (summaryValue as string ?? string.Empty));
				}
			}
			catch (Exception ex)
			{
				LogAfterProhibitionRoutesTravelContinuationFallback("bridge-error-" + ex.GetType().Name, normalizedSource);
			}
		}

		private static void LogAfterProhibitionRoutesTravelContinuationFallback(string reason, string sourceTag)
		{
			if (_loggedAfterProhibitionRoutesTravelContinuationFallback)
			{
				return;
			}

			_loggedAfterProhibitionRoutesTravelContinuationFallback = true;
			Debug.Log("[GameplayTweaks] RouteContinuation fallback active reason=" + reason + " source=" + sourceTag);
		}

		private static void LogAfterProhibitionRoutesVehicleNodeAuthorityDecision(EntityID vehicleId, NodeID queryNodeId, string actionType)
		{
			string normalizedAction = string.IsNullOrWhiteSpace(actionType) ? "none" : actionType.Trim();
			string dedupeKey = vehicleId.id + ":" + queryNodeId + ":" + normalizedAction;
			if (!_loggedAfterProhibitionRoutesVehicleAuthorityDecisionKeys.Add(dedupeKey))
			{
				return;
			}

			try
			{
				if (!TryGetAfterProhibitionRoutesPluginType(out Type routesType))
				{
					LogAfterProhibitionRoutesVehicleNodeAuthorityFallback("bridge-missing", normalizedAction);
					return;
				}

				_afterProhibitionRoutesOwnsVehicleNodeAuthorityDecisionMethod =
					_afterProhibitionRoutesOwnsVehicleNodeAuthorityDecisionMethod ?? AccessTools.Method(routesType, "OwnsVehicleNodeAuthorityDecision");
				_afterProhibitionRoutesLogVehicleNodeAuthorityDecisionMethod =
					_afterProhibitionRoutesLogVehicleNodeAuthorityDecisionMethod ?? AccessTools.Method(routesType, "LogVehicleNodeAuthorityDecision", new[] { typeof(EntityID), typeof(NodeID), typeof(string) });

				if (_afterProhibitionRoutesOwnsVehicleNodeAuthorityDecisionMethod == null
					|| _afterProhibitionRoutesLogVehicleNodeAuthorityDecisionMethod == null)
				{
					LogAfterProhibitionRoutesVehicleNodeAuthorityFallback("bridge-method-missing", normalizedAction);
					return;
				}

				object ownsValue = _afterProhibitionRoutesOwnsVehicleNodeAuthorityDecisionMethod.Invoke(null, null);
				if (!(ownsValue is bool ownsDecision) || !ownsDecision)
				{
					LogAfterProhibitionRoutesVehicleNodeAuthorityFallback("bridge-disabled", normalizedAction);
					return;
				}

				if (!_verboseAfterProhibitionRoutesDecisionBridge)
				{
					if (!_loggedAfterProhibitionRoutesVehicleNodeAuthorityDelegated)
					{
						_loggedAfterProhibitionRoutesVehicleNodeAuthorityDelegated = true;
						Debug.Log("[GameplayTweaks] VehicleNodeAuthority delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True quietBridge=True source=" + normalizedAction);
					}
					return;
				}

				object summaryValue = _afterProhibitionRoutesLogVehicleNodeAuthorityDecisionMethod.Invoke(null, new object[] { vehicleId, queryNodeId, normalizedAction });
				if (!_loggedAfterProhibitionRoutesVehicleNodeAuthorityDelegated)
				{
					_loggedAfterProhibitionRoutesVehicleNodeAuthorityDelegated = true;
					Debug.Log("[GameplayTweaks] VehicleNodeAuthority delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True source=" + normalizedAction + " " + (summaryValue as string ?? string.Empty));
				}
			}
			catch (Exception ex)
			{
				LogAfterProhibitionRoutesVehicleNodeAuthorityFallback("bridge-error-" + ex.GetType().Name, normalizedAction);
			}
		}

		private static void LogAfterProhibitionRoutesVehicleNodeAuthorityFallback(string reason, string sourceTag)
		{
			if (_loggedAfterProhibitionRoutesVehicleNodeAuthorityFallback)
			{
				return;
			}

			_loggedAfterProhibitionRoutesVehicleNodeAuthorityFallback = true;
			Debug.Log("[GameplayTweaks] VehicleNodeAuthority fallback active reason=" + reason + " source=" + sourceTag);
		}

		internal static void LogAfterProhibitionRoutesDeliveryPumpDecision(EntityID vehicleId, NodeID finalNodeId, string sourceTag, int queuePumps, int automationPumps)
		{
			string normalizedSource = string.IsNullOrWhiteSpace(sourceTag) ? "unknown" : sourceTag.Trim();
			string dedupeKey = vehicleId.id + ":" + finalNodeId + ":" + normalizedSource + ":" + queuePumps + ":" + automationPumps;
			if (!_loggedAfterProhibitionRoutesDeliveryPumpDecisionKeys.Add(dedupeKey))
			{
				return;
			}

			try
			{
				if (!TryGetAfterProhibitionRoutesPluginType(out Type routesType))
				{
					LogAfterProhibitionRoutesDeliveryPumpFallback("bridge-missing", normalizedSource);
					return;
				}

				_afterProhibitionRoutesOwnsDeliveryPumpDecisionMethod =
					_afterProhibitionRoutesOwnsDeliveryPumpDecisionMethod ?? AccessTools.Method(routesType, "OwnsDeliveryPumpDecision");
				_afterProhibitionRoutesLogDeliveryPumpDecisionMethod =
					_afterProhibitionRoutesLogDeliveryPumpDecisionMethod ?? AccessTools.Method(routesType, "LogDeliveryPumpDecision", new[] { typeof(EntityID), typeof(NodeID), typeof(string), typeof(int), typeof(int) });

				if (_afterProhibitionRoutesOwnsDeliveryPumpDecisionMethod == null
					|| _afterProhibitionRoutesLogDeliveryPumpDecisionMethod == null)
				{
					LogAfterProhibitionRoutesDeliveryPumpFallback("bridge-method-missing", normalizedSource);
					return;
				}

				object ownsValue = _afterProhibitionRoutesOwnsDeliveryPumpDecisionMethod.Invoke(null, null);
				if (!(ownsValue is bool ownsDecision) || !ownsDecision)
				{
					LogAfterProhibitionRoutesDeliveryPumpFallback("bridge-disabled", normalizedSource);
					return;
				}

				if (!_verboseAfterProhibitionRoutesDecisionBridge)
				{
					if (!_loggedAfterProhibitionRoutesDeliveryPumpDelegated)
					{
						_loggedAfterProhibitionRoutesDeliveryPumpDelegated = true;
						Debug.Log("[GameplayTweaks] DeliveryRoutePump delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True quietBridge=True source=" + normalizedSource);
					}
					return;
				}

				object summaryValue = _afterProhibitionRoutesLogDeliveryPumpDecisionMethod.Invoke(null, new object[] { vehicleId, finalNodeId, normalizedSource, queuePumps, automationPumps });
				if (!_loggedAfterProhibitionRoutesDeliveryPumpDelegated)
				{
					_loggedAfterProhibitionRoutesDeliveryPumpDelegated = true;
					Debug.Log("[GameplayTweaks] DeliveryRoutePump delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True source=" + normalizedSource + " " + (summaryValue as string ?? string.Empty));
				}
			}
			catch (Exception ex)
			{
				LogAfterProhibitionRoutesDeliveryPumpFallback("bridge-error-" + ex.GetType().Name, normalizedSource);
			}
		}

		private static void LogAfterProhibitionRoutesDeliveryPumpFallback(string reason, string sourceTag)
		{
			if (_loggedAfterProhibitionRoutesDeliveryPumpFallback)
			{
				return;
			}

			_loggedAfterProhibitionRoutesDeliveryPumpFallback = true;
			Debug.Log("[GameplayTweaks] DeliveryRoutePump fallback active reason=" + reason + " source=" + sourceTag);
		}

		internal static void LogAfterProhibitionRoutesRouteSimAccessDecision(EntityID vehicleId, NodeID queryNodeId, string actionTag)
		{
			string normalizedAction = string.IsNullOrWhiteSpace(actionTag) ? "none" : actionTag.Trim();
			string dedupeKey = vehicleId.id + ":" + queryNodeId + ":" + normalizedAction;
			if (!_loggedAfterProhibitionRoutesRouteSimAccessDecisionKeys.Add(dedupeKey))
			{
				return;
			}

			try
			{
				if (!TryGetAfterProhibitionRoutesPluginType(out Type routesType))
				{
					LogAfterProhibitionRoutesRouteSimAccessFallback("bridge-missing", normalizedAction);
					return;
				}

				_afterProhibitionRoutesOwnsRouteSimAccessDecisionMethod =
					_afterProhibitionRoutesOwnsRouteSimAccessDecisionMethod ?? AccessTools.Method(routesType, "OwnsRouteSimAccessDecision");
				_afterProhibitionRoutesLogRouteSimAccessDecisionMethod =
					_afterProhibitionRoutesLogRouteSimAccessDecisionMethod ?? AccessTools.Method(routesType, "LogRouteSimAccessDecision", new[] { typeof(EntityID), typeof(NodeID), typeof(string) });

				if (_afterProhibitionRoutesOwnsRouteSimAccessDecisionMethod == null
					|| _afterProhibitionRoutesLogRouteSimAccessDecisionMethod == null)
				{
					LogAfterProhibitionRoutesRouteSimAccessFallback("bridge-method-missing", normalizedAction);
					return;
				}

				object ownsValue = _afterProhibitionRoutesOwnsRouteSimAccessDecisionMethod.Invoke(null, null);
				if (!(ownsValue is bool ownsDecision) || !ownsDecision)
				{
					LogAfterProhibitionRoutesRouteSimAccessFallback("bridge-disabled", normalizedAction);
					return;
				}

				if (!_verboseAfterProhibitionRoutesDecisionBridge)
				{
					if (!_loggedAfterProhibitionRoutesRouteSimAccessDelegated)
					{
						_loggedAfterProhibitionRoutesRouteSimAccessDelegated = true;
						Debug.Log("[GameplayTweaks] RouteSimAccess delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True quietBridge=True source=" + normalizedAction);
					}
					return;
				}

				object summaryValue = _afterProhibitionRoutesLogRouteSimAccessDecisionMethod.Invoke(null, new object[] { vehicleId, queryNodeId, normalizedAction });
				if (!_loggedAfterProhibitionRoutesRouteSimAccessDelegated)
				{
					_loggedAfterProhibitionRoutesRouteSimAccessDelegated = true;
					Debug.Log("[GameplayTweaks] RouteSimAccess delegated owner=AfterProhibitionRoutes mode=decision-only behaviorFallback=True source=" + normalizedAction + " " + (summaryValue as string ?? string.Empty));
				}
			}
			catch (Exception ex)
			{
				LogAfterProhibitionRoutesRouteSimAccessFallback("bridge-error-" + ex.GetType().Name, normalizedAction);
			}
		}

		private static void LogAfterProhibitionRoutesRouteSimAccessFallback(string reason, string sourceTag)
		{
			if (_loggedAfterProhibitionRoutesRouteSimAccessFallback)
			{
				return;
			}

			_loggedAfterProhibitionRoutesRouteSimAccessFallback = true;
			Debug.Log("[GameplayTweaks] RouteSimAccess fallback active reason=" + reason + " source=" + sourceTag);
		}

		internal static bool ShouldSkipForAfterProhibitionRoutesBehaviorOwner(string slice, string sourceTag)
		{
			string normalizedSlice = string.IsNullOrWhiteSpace(slice) ? "unknown" : slice.Trim();
			string normalizedSource = string.IsNullOrWhiteSpace(sourceTag) ? "unknown" : sourceTag.Trim();
			if (_afterProhibitionRoutesBehaviorOwnerBySlice.TryGetValue(normalizedSlice, out bool cachedOwnsBehavior))
			{
				return cachedOwnsBehavior;
			}

			try
			{
				if (!TryGetAfterProhibitionRoutesPluginType(out Type routesType))
				{
					LogAfterProhibitionRoutesBehaviorFallback(normalizedSlice, "bridge-missing", normalizedSource);
					_afterProhibitionRoutesBehaviorOwnerBySlice[normalizedSlice] = false;
					return false;
				}

				_afterProhibitionRoutesOwnsRouteBehaviorSliceMethod =
					_afterProhibitionRoutesOwnsRouteBehaviorSliceMethod ?? AccessTools.Method(routesType, "OwnsRouteBehaviorSlice", new[] { typeof(string) });
				if (_afterProhibitionRoutesOwnsRouteBehaviorSliceMethod == null)
				{
					LogAfterProhibitionRoutesBehaviorFallback(normalizedSlice, "bridge-method-missing", normalizedSource);
					_afterProhibitionRoutesBehaviorOwnerBySlice[normalizedSlice] = false;
					return false;
				}

				object ownsValue = _afterProhibitionRoutesOwnsRouteBehaviorSliceMethod.Invoke(null, new object[] { normalizedSlice });
				if (ownsValue is bool ownsBehavior && ownsBehavior)
				{
					_afterProhibitionRoutesBehaviorOwnerBySlice[normalizedSlice] = true;
					if (_loggedAfterProhibitionRoutesBehaviorDelegatedSlices.Add(normalizedSlice))
					{
						Debug.Log("[GameplayTweaks] Routes behavior delegated owner=AfterProhibitionRoutes slice=" + normalizedSlice + " source=" + normalizedSource);
					}
					return true;
				}

				LogAfterProhibitionRoutesBehaviorFallback(normalizedSlice, "behavior-owner-false", normalizedSource);
				_afterProhibitionRoutesBehaviorOwnerBySlice[normalizedSlice] = false;
				return false;
			}
			catch (Exception ex)
			{
				LogAfterProhibitionRoutesBehaviorFallback(normalizedSlice, "bridge-error-" + ex.GetType().Name, normalizedSource);
				return false;
			}
		}

		internal static void WarmAfterProhibitionRoutesBehaviorOwnerCache(string sourceTag)
		{
			if (_afterProhibitionRoutesBehaviorOwnerWarmComplete)
			{
				return;
			}

			int frame = Time.frameCount;
			if (_afterProhibitionRoutesBehaviorOwnerWarmAttempts > 0
				&& frame - _afterProhibitionRoutesBehaviorOwnerWarmLastAttemptFrame < 300)
			{
				return;
			}

			_afterProhibitionRoutesBehaviorOwnerWarmAttempts++;
			_afterProhibitionRoutesBehaviorOwnerWarmLastAttemptFrame = frame;

			long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			string normalizedSource = string.IsNullOrWhiteSpace(sourceTag) ? "warm" : sourceTag.Trim();
			if (!TryGetAfterProhibitionRoutesPluginType(out _))
			{
				if (_afterProhibitionRoutesBehaviorOwnerWarmAttempts >= 20)
				{
					_afterProhibitionRoutesBehaviorOwnerWarmComplete = true;
				}
				return;
			}

			int warmed = 0;
			for (int i = 0; i < AfterProhibitionRoutesBehaviorOwnerWarmSlices.Length; i++)
			{
				string warmSlice = AfterProhibitionRoutesBehaviorOwnerWarmSlices[i];
				if (_afterProhibitionRoutesBehaviorOwnerBySlice.ContainsKey(warmSlice))
				{
					continue;
				}

				ShouldSkipForAfterProhibitionRoutesBehaviorOwner(warmSlice, normalizedSource);
				warmed++;
			}

			bool complete = true;
			for (int i = 0; i < AfterProhibitionRoutesBehaviorOwnerWarmSlices.Length; i++)
			{
				if (!_afterProhibitionRoutesBehaviorOwnerBySlice.ContainsKey(AfterProhibitionRoutesBehaviorOwnerWarmSlices[i]))
				{
					complete = false;
					break;
				}
			}
			_afterProhibitionRoutesBehaviorOwnerWarmComplete = complete;

			long elapsedMs = GetElapsedMilliseconds(startTicks);
			if (elapsedMs >= 80L
				|| _verboseAfterProhibitionRoutesDecisionBridge
				|| (GameplayTweaksPlugin.EnablePerformanceDiagnostics?.Value ?? false && elapsedMs >= 10L))
			{
				Debug.Log("[PERF][RoutesBehaviorWarm] ms=" + elapsedMs
					+ " warmed=" + warmed
					+ " cached=" + _afterProhibitionRoutesBehaviorOwnerBySlice.Count
					+ " complete=" + _afterProhibitionRoutesBehaviorOwnerWarmComplete
					+ " attempts=" + _afterProhibitionRoutesBehaviorOwnerWarmAttempts
					+ " source=" + normalizedSource);
			}
		}

		private static void LogAfterProhibitionRoutesBehaviorFallback(string slice, string reason, string sourceTag)
		{
			string key = (string.IsNullOrWhiteSpace(slice) ? "unknown" : slice.Trim()) + ":" + reason;
			if (!_loggedAfterProhibitionRoutesBehaviorFallbackSlices.Add(key))
			{
				return;
			}

			Debug.Log("[GameplayTweaks] Routes behavior fallback active slice=" + slice + " reason=" + reason + " source=" + sourceTag);
		}

		private static bool TryGetAfterProhibitionRoutesPluginType(out Type routesType)
		{
			routesType = _afterProhibitionRoutesPluginType;
			if (routesType != null)
			{
				return true;
			}

			routesType = AccessTools.TypeByName("AfterProhibitionRoutes.AfterProhibitionRoutesPlugin");
			if (routesType != null)
			{
				_afterProhibitionRoutesPluginType = routesType;
				return true;
			}

			return false;
		}

		private static long GetElapsedMilliseconds(long startTicks)
		{
			return (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000L / System.Diagnostics.Stopwatch.Frequency;
		}

		internal static void FinalizeQueuedHumanVehicleArrivalsAtTurnStart(PlayerInfo player)
		{
			_turnStartFinalizedQueuedRouteVehicleIds.Clear();
			_turnStartDeferredArrivalVehicleIds.Clear();
			if (player?.PID.IsHumanPlayer != true || player.crew == null || _pendingVehicleTravelByVehicleId.Count <= 0)
			{
				return;
			}
			if (ShouldSkipForAfterProhibitionRoutesBehaviorOwner("travel-continuation", "turnstart-finalize"))
			{
				return;
			}

			System.Diagnostics.Stopwatch finalizeStopwatch = System.Diagnostics.Stopwatch.StartNew();
			int processedRoutes = 0;
			int deferredArrivals = 0;
			int finalizedRoutes = 0;
			int preservedRoutes = 0;
			int clearedRoutes = 0;
			long filterMs = 0L;
			long bridgeMs = 0L;
			long entityMs = 0L;
			long confirmMs = 0L;
			long commitMs = 0L;
			long sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			List<PendingVehicleTravelState> queuedRoutes = _pendingVehicleTravelByVehicleId.Values
				.Where((PendingVehicleTravelState state) => state.ResumeQueued && state.GoalNodeID.IsValid && state.VehicleID.IsValid)
				.OrderBy((PendingVehicleTravelState state) => state.VehicleID.id)
				.ToList();
			filterMs += GetElapsedMilliseconds(sectionTicks);
			foreach (PendingVehicleTravelState state in queuedRoutes)
			{
				processedRoutes++;
				PendingVehicleTravelState currentState = state;
				try
				{
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					LogAfterProhibitionRoutesTravelContinuationDecision(currentState.VehicleID, "turnstart-finalize");
					bridgeMs += GetElapsedMilliseconds(sectionTicks);
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					Entity vehicle = currentState.VehicleID.FindEntity();
					CrewAssignment assignment = player.crew.GetCrewForPeep(currentState.PeepID);
					entityMs += GetElapsedMilliseconds(sectionTicks);
					if (vehicle?.data?.mobile == null || !assignment.IsValid || !assignment.IsInVehicle || assignment.VehicleID != currentState.VehicleID)
					{
						if (vehicle?.data?.mobile != null
							&& TryRebindPendingHumanVehicleTravelToLiveDriver(player.crew, currentState, out PendingVehicleTravelState reboundState, out CrewAssignment reboundAssignment, out string reboundReason))
						{
							currentState = reboundState;
							assignment = reboundAssignment;
							LogVehicleAuthority(
								"turnstart-route-driver-rebound",
								$"{currentState.VehicleID.id}:{currentState.PeepID.id}:{currentState.StartNodeID}:{currentState.ExpectedNodeID}:{currentState.GoalNodeID}:{reboundReason}",
								$"turnstart-route-driver-rebound vehicle={currentState.VehicleID.id} peep={currentState.PeepID.id} startNode={currentState.StartNodeID} expectedNode={currentState.ExpectedNodeID} finalGoal={currentState.GoalNodeID} reason={reboundReason}",
								dedupe: false);
						}
						else
						{
							LogVehicleAuthority("turnstart-arrival-clear", $"{currentState.VehicleID.id}:invalid-assignment", $"turnstart-arrival-clear-invalid vehicle={currentState.VehicleID.id} reason=invalid-assignment finalGoal={currentState.GoalNodeID}", dedupe: false);
							ClearPendingHumanVehicleTravel(currentState.VehicleID, "invalid");
							clearedRoutes++;
							continue;
						}
					}

					long vehicleKey = (long)currentState.VehicleID.id;
					bool wasActiveTravel = _activeHumanVehicleTravel.Contains(vehicleKey);
					bool useLogicalRouteArrival = wasActiveTravel
						&& ShouldUseLogicalHumanVehicleRouteArrivalAtTurnStart(currentState);
					if (wasActiveTravel
						&& currentState.ExpectedNodeID.IsValid
						&& !useLogicalRouteArrival
						&& !TryConfirmHumanVehicleExpectedArrivalTimed(player.crew, vehicle, currentState, ref confirmMs))
					{
						_turnStartDeferredArrivalVehicleIds.Add(vehicleKey);
						LogHumanVehicleExpectedArrivalDeferred("turnstart-arrival-deferred", currentState.VehicleID, currentState, "turnstart-await-physical");
						deferredArrivals++;
						continue;
					}
					if (useLogicalRouteArrival)
					{
						LogVehicleAuthority(
							"turnstart-logical-route-arrival",
							$"{currentState.VehicleID.id}:{currentState.StartNodeID}:{currentState.ExpectedNodeID}:{currentState.GoalNodeID}",
							$"turnstart-logical-route-arrival vehicle={currentState.VehicleID.id} startNode={currentState.StartNodeID} expectedNode={currentState.ExpectedNodeID} finalGoal={currentState.GoalNodeID} reason=vanilla-route-continuation",
							dedupe: false);
					}

					_activeHumanVehicleTravel.Remove(vehicleKey);
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					NodeID committedNodeId = currentState.ExpectedNodeID;
					string committedSource = "expected";
					if (!committedNodeId.IsValid)
					{
						if (TryGetQueuedArrivalCommittedNodeId(currentState.VehicleID, out NodeID queuedArrivalNodeId) && queuedArrivalNodeId.IsValid)
						{
							committedNodeId = queuedArrivalNodeId;
							committedSource = "queued-arrival";
						}
						else if (TryGetRecentFinalizedNodeId(currentState.VehicleID, out NodeID recentFinalizedNodeId) && recentFinalizedNodeId.IsValid)
						{
							committedNodeId = recentFinalizedNodeId;
							committedSource = "recent-finalize";
						}
						else if (currentState.StartNodeID.IsValid)
						{
							committedNodeId = currentState.StartNodeID;
							committedSource = "pending-start";
						}
					}

					if (!committedNodeId.IsValid)
					{
						LogVehicleAuthority("turnstart-arrival-clear", $"{currentState.VehicleID.id}:invalid-expected", $"turnstart-arrival-clear-invalid vehicle={currentState.VehicleID.id} reason=invalid-expected finalGoal={currentState.GoalNodeID}", dedupe: false);
						ClearPendingHumanVehicleTravel(currentState.VehicleID, "invalid");
						clearedRoutes++;
						continue;
					}

					if (!currentState.ExpectedNodeID.IsValid)
					{
						if (committedNodeId == currentState.GoalNodeID)
						{
							LogVehicleAuthority(
								"turnstart-route-stale-cleared",
								$"{currentState.VehicleID.id}:{committedNodeId}:{currentState.GoalNodeID}:already-arrived",
								$"turnstart-route-stale-cleared vehicle={currentState.VehicleID.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={currentState.GoalNodeID} reason=already-arrived",
								dedupe: false);
							ClearInterruptExpectedStartNode(currentState.VehicleID, "turnstart-arrived");
							ClearPendingHumanVehicleTravel(currentState.VehicleID, "arrived");
							clearedRoutes++;
							continue;
						}
						// This state has already committed the previous segment and is only waiting
						// for the next turn's queued resume. Running the same-day requeue loop guard
						// here clears legitimate long-distance continuation before it can drive.
						RecordHumanVehicleFinalizedSegment(currentState.VehicleID, currentState.StartNodeID, committedNodeId);
						PendingVehicleTravelState preservedState = currentState;
						preservedState.StartNodeID = committedNodeId;
						preservedState.ExpectedNodeID = NodeID.INVALID;
						preservedState.CommandOwnerKey = 0;
						preservedState.ResumeQueued = preservedState.GoalNodeID.IsValid && preservedState.GoalNodeID != committedNodeId;
						_pendingVehicleTravelByVehicleId[vehicleKey] = preservedState;
						_queuedArrivalCommittedNodeByVehicleId[vehicleKey] = committedNodeId;
						SetRecentFinalizedNode(currentState.VehicleID, committedNodeId);
						ClearInterruptExpectedStartNode(currentState.VehicleID, "turnstart-preserved");
						LogVehicleAuthority(
							"turnstart-arrival-preserved",
							$"{currentState.VehicleID.id}:{committedNodeId}:{currentState.GoalNodeID}:{committedSource}",
							$"turnstart-arrival-already-committed vehicle={currentState.VehicleID.id} currentNode={committedNodeId} finalGoal={currentState.GoalNodeID} source={committedSource}",
							dedupe: false);
						LogVehicleAuthority(
							"route-resume-preserved",
							$"{currentState.VehicleID.id}:{committedNodeId}:{currentState.GoalNodeID}",
							$"route-resume-preserved vehicle={currentState.VehicleID.id} currentNode={committedNodeId} finalGoal={currentState.GoalNodeID}",
						dedupe: false);
					preservedRoutes++;
					commitMs += GetElapsedMilliseconds(sectionTicks);
					continue;
				}

					int mismatchCount = 0;
					if (TrySyncVehicleEntityToNode(vehicle, committedNodeId))
					{
						mismatchCount++;
					}
					foreach (CrewAssignment occupant in GetAllCrewInVehicle(player.crew, currentState.VehicleID))
					{
						if (!occupant.IsValid || !occupant.peepId.IsValid || !occupant.IsNotDead)
						{
							continue;
						}
						NodeID peepNodeId = occupant.GetPeep()?.data?.agent?.nid ?? NodeID.INVALID;
						if (peepNodeId != committedNodeId)
						{
							mismatchCount++;
						}
						global::Game.Game.ctx?.transit?.SetAgentAtNode(committedNodeId, occupant.peepId);
					}

					LogVehicleAuthority(
						"turnstart-arrival-finalize",
						$"{currentState.VehicleID.id}:{committedNodeId}:{currentState.GoalNodeID}",
						$"turnstart-arrival-finalize vehicle={currentState.VehicleID.id} finalNode={committedNodeId} reason=queued-segment-complete mismatches={mismatchCount}",
						dedupe: false);
					RecordHumanVehicleFinalizedSegment(currentState.VehicleID, currentState.StartNodeID, committedNodeId);
					if (committedNodeId == currentState.GoalNodeID)
					{
						LogVehicleAuthority(
							"turnstart-route-stale-cleared",
							$"{currentState.VehicleID.id}:{committedNodeId}:{currentState.GoalNodeID}:already-arrived",
							$"turnstart-route-stale-cleared vehicle={currentState.VehicleID.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={currentState.GoalNodeID} reason=already-arrived",
							dedupe: false);
						ClearInterruptExpectedStartNode(currentState.VehicleID, "turnstart-arrived");
						ClearPendingHumanVehicleTravel(currentState.VehicleID, "arrived");
						clearedRoutes++;
						continue;
					}
					if (ShouldSuppressHumanVehicleRequeue(currentState.VehicleID, committedNodeId, currentState.GoalNodeID))
					{
						LogVehicleAuthority(
							"turnstart-route-stale-cleared",
							$"{currentState.VehicleID.id}:{committedNodeId}:{currentState.GoalNodeID}:requeue-suppressed",
							$"turnstart-route-stale-cleared vehicle={currentState.VehicleID.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={currentState.GoalNodeID} reason=requeue-suppressed",
							dedupe: false);
						ClearInterruptExpectedStartNode(currentState.VehicleID, "turnstart-requeue-suppressed");
						ClearPendingHumanVehicleTravel(currentState.VehicleID, "requeue-suppressed");
						clearedRoutes++;
						continue;
					}
					_queuedArrivalCommittedNodeByVehicleId[vehicleKey] = committedNodeId;
					SetRecentFinalizedNode(currentState.VehicleID, committedNodeId);
					PendingVehicleTravelState resumedState = currentState;
					resumedState.StartNodeID = committedNodeId;
					resumedState.ExpectedNodeID = NodeID.INVALID;
					resumedState.CommandOwnerKey = 0;
					resumedState.ResumeQueued = resumedState.GoalNodeID.IsValid && resumedState.GoalNodeID != committedNodeId;
					_pendingVehicleTravelByVehicleId[vehicleKey] = resumedState;
					_turnStartFinalizedQueuedRouteVehicleIds.Add(vehicleKey);
					ClearInterruptExpectedStartNode(currentState.VehicleID, "turnstart-arrival-finalize");
					finalizedRoutes++;
					commitMs += GetElapsedMilliseconds(sectionTicks);
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] FinalizeQueuedHumanVehicleArrivalsAtTurnStart: " + ex.Message);
					ClearPendingHumanVehicleTravel(currentState.VehicleID, "invalid");
					clearedRoutes++;
				}
			}
			finalizeStopwatch.Stop();
			if (finalizeStopwatch.ElapsedMilliseconds >= 20)
			{
				Debug.Log($"[PERF][RouteTurnStartFinalize] ms={finalizeStopwatch.ElapsedMilliseconds} pending={_pendingVehicleTravelByVehicleId.Count} queued={queuedRoutes.Count} processed={processedRoutes} finalized={finalizedRoutes} preserved={preservedRoutes} deferred={deferredArrivals} cleared={clearedRoutes} filterMs={filterMs} bridgeMs={bridgeMs} entityMs={entityMs} confirmMs={confirmMs} commitMs={commitMs} frame={Time.frameCount}");
			}
		}

		private static bool TryConfirmHumanVehicleExpectedArrivalTimed(PlayerCrew crew, Entity vehicle, PendingVehicleTravelState state, ref long elapsedMs)
		{
			long ticks = System.Diagnostics.Stopwatch.GetTimestamp();
			bool confirmed = TryConfirmHumanVehicleExpectedArrival(crew, vehicle, state, out _, out _);
			elapsedMs += GetElapsedMilliseconds(ticks);
			return confirmed;
		}

		internal static void ClearQueuedHumanVehicleTravelForPeep(PlayerInfo player, EntityID peepId, string reason)
		{
			if (player?.PID.IsHumanPlayer != true || !peepId.IsValid)
			{
				return;
			}

			EntityID vehicleId = EntityID.INVALID;
			CrewAssignment assignment = player.crew?.GetCrewForPeep(peepId) ?? CrewAssignment.EMPTY;
			if (assignment.IsValid && assignment.IsInVehicle && assignment.VehicleID.IsValid)
			{
				vehicleId = assignment.VehicleID;
			}
			else
			{
				TryGetPendingHumanVehicleTravelByPeep(peepId, out vehicleId, out _);
			}

			if (vehicleId.IsValid && HasPendingHumanVehicleTravel(vehicleId))
			{
				if (string.Equals(reason, "user-stop", StringComparison.Ordinal)
					&& _activeHumanVehicleTravel.Contains((long)vehicleId.id)
					&& _pendingVehicleTravelByVehicleId.TryGetValue((long)vehicleId.id, out PendingVehicleTravelState activeState)
					&& activeState.ExpectedNodeID.IsValid)
				{
					NodeID abandonedGoalNodeId = activeState.GoalNodeID;
					if (ShouldPreserveRecentQueuedHumanVehicleDestinationPreview(vehicleId, activeState))
					{
						Node queuedStartNode = activeState.ExpectedNodeID.FindNode();
						Node queuedGoalNode = activeState.GoalNodeID.FindNode();
						if (queuedGoalNode != null)
						{
							GameplayTweaksPlugin.QueueDeferredSelectedVehicleUiRefresh(queuedStartNode, queuedGoalNode, "route-queued-preview-user-stop-preserved", vehicleId);
							GameplayTweaksPlugin.RefreshQueuedRouteSelectedVehicleNodeHighlight(queuedGoalNode, vehicleId, "route-queued-preview-user-stop-preserved");
						}
						Entity queuedPeep = peepId.FindEntity();
						LogVehicleAuthority(
							"user-stop-route-queued-preserved",
							$"{vehicleId.id}:{activeState.ExpectedNodeID}:{activeState.GoalNodeID}:{peepId.id}",
							$"user-stop-route-queued-preserved vehicle={vehicleId.id} peep={peepId.id} expectedNode={activeState.ExpectedNodeID} queuedGoal={activeState.GoalNodeID} moves={queuedPeep?.components?.agent?.MovesRemaining ?? -1} actions={queuedPeep?.components?.agent?.ActionsRemaining ?? -1} reason=recent-queued-destination-preview",
							dedupe: false);
						return;
					}
					activeState.GoalNodeID = activeState.ExpectedNodeID;
					activeState.ResumeQueued = false;
					_pendingVehicleTravelByVehicleId[(long)vehicleId.id] = activeState;
					bool stopSynced = TrySyncVehicleOccupantsToNode(player.crew, vehicleId, activeState.ExpectedNodeID, "user-stop-route-preserved", syncVehicle: true);
					_queuedArrivalCommittedNodeByVehicleId[(long)vehicleId.id] = activeState.ExpectedNodeID;
					SetRecentFinalizedNode(vehicleId, activeState.ExpectedNodeID);
					RecordObservedHumanVehicleReachedNode(vehicleId, activeState.ExpectedNodeID, "user-stop-route-preserved");
					GameplayTweaksPlugin.ClearSelectedVehicleUiFinalNode(vehicleId, "user-stop-route-preserved");
					Node stopStartNode = activeState.StartNodeID.FindNode();
					Node stopExpectedNode = activeState.ExpectedNodeID.FindNode();
					if (stopExpectedNode != null)
					{
						GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(stopStartNode, stopExpectedNode, "user-stop-route-preserved", vehicleId);
						if (abandonedGoalNodeId.IsValid && abandonedGoalNodeId != activeState.ExpectedNodeID)
						{
							Node abandonedGoalNode = abandonedGoalNodeId.FindNode();
							if (abandonedGoalNode != null)
							{
								GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(abandonedGoalNode, stopExpectedNode, "user-stop-route-preserved-old-goal", vehicleId);
							}
						}
					}
					Entity peep = peepId.FindEntity();
					int movesRemaining = peep?.components?.agent?.MovesRemaining ?? -1;
					int actionsRemaining = peep?.components?.agent?.ActionsRemaining ?? -1;
					LogVehicleAuthority(
						"user-stop-route-preserved",
						$"{vehicleId.id}:{activeState.ExpectedNodeID}:{peepId.id}",
						$"user-stop-route-preserved vehicle={vehicleId.id} peep={peepId.id} expectedNode={activeState.ExpectedNodeID} abandonedGoal={abandonedGoalNodeId} moves={movesRemaining} actions={actionsRemaining} synced={stopSynced} reason=active-travel-finalize-pending",
						dedupe: false);
					return;
				}

				ClearPendingHumanVehicleTravel(vehicleId, reason);
			}
		}

		private static bool TryBuildQueuedHumanVehiclePath(PlayerInfo player, Entity peep, CrewAssignment assignment, Node goalNode, bool freeDrive, out PathData path, out Node startNode, out string source, out QueuedHumanVehiclePathBuildResult buildResult)
		{
			path = null;
			startNode = null;
			source = "none";
			buildResult = QueuedHumanVehiclePathBuildResult.Invalid;
			if (player == null || peep?.data?.agent == null || !assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid || goalNode == null)
			{
				return false;
			}
			NodeID peepNodeId = peep.data?.agent?.nid ?? NodeID.INVALID;
			if (!TryGetResolvedHumanVehiclePathStartNode(assignment.VehicleID, peepNodeId, out startNode, out source, allowQueuedResumeRepair: true) || startNode == null)
			{
				return false;
			}
			int movesRemaining = freeDrive ? int.MaxValue : peep.components.agent.MovesRemaining;
			if (movesRemaining <= 0)
			{
				buildResult = QueuedHumanVehiclePathBuildResult.NoMovesYet;
				return true;
			}
			SomaSim.Util.Fixnum maxCost = freeDrive ? SomaSim.Util.Fixnum.MAX_VALUE : new SomaSim.Util.Fixnum(movesRemaining);
			WorldPos startPos = startNode.pos;
			PathData builtPath = null;
			bool routeExistsBeyondCurrentMoves = false;
			global::Game.Game.ctx.transit.FindDrivingPath(player.PID, peep, goalNode.pos, delegate (Pathfinding.Result result)
			{
				if (result.status == Pathfinding.Status.Success)
				{
					builtPath = new PathData();
					result.PopulatePath(builtPath, startPos, maxCost);
					if (!freeDrive && (builtPath.world == null || builtPath.world.Count <= 1))
					{
						PathData fullPath = new PathData();
						result.PopulatePath(fullPath, startPos, SomaSim.Util.Fixnum.MAX_VALUE);
						routeExistsBeyondCurrentMoves = fullPath.world != null && fullPath.world.Count > 1;
					}
				}
			});
			path = builtPath;
			buildResult = builtPath != null && builtPath.world != null && builtPath.world.Count > 1
				? QueuedHumanVehiclePathBuildResult.BuiltValid
				: routeExistsBeyondCurrentMoves
					? QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment
				: QueuedHumanVehiclePathBuildResult.BuiltEmpty;
			return true;
		}

		private static bool TryBuildQueuedHumanVehiclePathFromCommittedNode(PlayerInfo player, Entity peep, CrewAssignment assignment, Node goalNode, bool freeDrive, out PathData path, out Node startNode, out QueuedHumanVehiclePathBuildResult buildResult)
		{
			path = null;
			startNode = null;
			buildResult = QueuedHumanVehiclePathBuildResult.Invalid;
			if (player == null || peep?.data?.agent == null || !assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid || goalNode == null)
			{
				return false;
			}
			if (!TryGetQueuedArrivalCommittedNode(assignment.VehicleID, out startNode) || startNode == null)
			{
				return false;
			}
			int movesRemaining = freeDrive ? int.MaxValue : peep.components.agent.MovesRemaining;
			if (movesRemaining <= 0)
			{
				buildResult = QueuedHumanVehiclePathBuildResult.NoMovesYet;
				return true;
			}
			SomaSim.Util.Fixnum maxCost = freeDrive ? SomaSim.Util.Fixnum.MAX_VALUE : new SomaSim.Util.Fixnum(movesRemaining);
			WorldPos startPos = startNode.pos;
			PathData builtPath = null;
			bool routeExistsBeyondCurrentMoves = false;
			RoadPathContext ctx = new RoadPathContext(global::Game.Game.ctx.board);
			global::Game.Game.ctx.board.GetGridPath(player.PID, peep.Id, startPos, goalNode.pos, ctx, delegate (Pathfinding.Result result)
			{
				if (result.status == Pathfinding.Status.Success)
				{
					builtPath = new PathData();
					result.PopulatePath(builtPath, startPos, maxCost);
					if (!freeDrive && (builtPath.world == null || builtPath.world.Count <= 1))
					{
						PathData fullPath = new PathData();
						result.PopulatePath(fullPath, startPos, SomaSim.Util.Fixnum.MAX_VALUE);
						routeExistsBeyondCurrentMoves = fullPath.world != null && fullPath.world.Count > 1;
					}
				}
			});
			path = builtPath;
			buildResult = builtPath != null && builtPath.world != null && builtPath.world.Count > 1
				? QueuedHumanVehiclePathBuildResult.BuiltValid
				: routeExistsBeyondCurrentMoves
					? QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment
				: QueuedHumanVehiclePathBuildResult.BuiltEmpty;
			return true;
		}

		internal static void TryResumeQueuedHumanVehicleRoutes(PlayerInfo player)
		{
			TryResumeQueuedHumanVehicleRoutes(player, EntityID.INVALID, "turn-start");
		}

		internal static void QueueDeferredQueuedHumanVehicleRouteResume(PlayerInfo player, string sourceTag)
		{
			if (player?.PID.IsHumanPlayer != true || player.crew == null || _pendingVehicleTravelByVehicleId.Count <= 0)
			{
				return;
			}
			if (!_pendingVehicleTravelByVehicleId.Values.Any(state => state.ResumeQueued && state.GoalNodeID.IsValid && state.VehicleID.IsValid && state.PeepID.IsValid && !_activeHumanVehicleTravel.Contains((long)state.VehicleID.id)))
			{
				return;
			}

			_deferredQueuedHumanVehicleRouteResumePending = true;
			_deferredQueuedHumanVehicleRouteResumeEarliestFrame = Time.frameCount + 2;
			_deferredQueuedHumanVehicleRouteResumeSource = string.IsNullOrWhiteSpace(sourceTag) ? "turn-start" : sourceTag;
			LogVehicleAuthority(
				"route-resume-deferred-turnstart",
				$"{_deferredQueuedHumanVehicleRouteResumeSource}:{_deferredQueuedHumanVehicleRouteResumeEarliestFrame}:{_pendingVehicleTravelByVehicleId.Count}",
				$"route-resume-deferred-turnstart source={_deferredQueuedHumanVehicleRouteResumeSource} pending={_pendingVehicleTravelByVehicleId.Count} earliestFrame={_deferredQueuedHumanVehicleRouteResumeEarliestFrame}",
				dedupe: false);
		}

		internal static void FlushDeferredQueuedHumanVehicleRouteResume(string sourceTag)
		{
			if (!_deferredQueuedHumanVehicleRouteResumePending)
			{
				return;
			}
			if (Time.frameCount < _deferredQueuedHumanVehicleRouteResumeEarliestFrame)
			{
				return;
			}

			_deferredQueuedHumanVehicleRouteResumePending = false;
			string queuedSource = string.IsNullOrWhiteSpace(_deferredQueuedHumanVehicleRouteResumeSource)
				? "deferred-turn-start"
				: _deferredQueuedHumanVehicleRouteResumeSource;
			_deferredQueuedHumanVehicleRouteResumeSource = string.Empty;
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer?.PID.IsHumanPlayer != true || humanPlayer.crew == null)
			{
				return;
			}

			System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
			TryResumeQueuedHumanVehicleRoutes(humanPlayer, EntityID.INVALID, "deferred-" + queuedSource);
			stopwatch.Stop();
			if (stopwatch.ElapsedMilliseconds >= 20)
			{
				Debug.Log($"[PERF][RouteResume] source={sourceTag} queuedSource={queuedSource} ms={stopwatch.ElapsedMilliseconds} frame={Time.frameCount}");
			}
		}

		internal static void TryResumeQueuedHumanVehicleRoutes(PlayerInfo player, EntityID onlyVehicleId, string sourceTag)
		{
			if (player?.PID.IsHumanPlayer != true || player.crew == null)
			{
				return;
			}
			if (ShouldSkipForAfterProhibitionRoutesBehaviorOwner("travel-continuation", sourceTag))
			{
				return;
			}

			bool singleVehicle = onlyVehicleId.IsValid;
			List<PendingVehicleTravelState> queuedRoutes = _pendingVehicleTravelByVehicleId.Values
				.Where(state => state.ResumeQueued && state.GoalNodeID.IsValid && !_activeHumanVehicleTravel.Contains((long)state.VehicleID.id))
				.Where(state => !singleVehicle || state.VehicleID == onlyVehicleId)
				.Where(state => state.VehicleID.IsValid && state.PeepID.IsValid)
				.OrderBy(state => state.VehicleID.id)
				.ToList();
			foreach (PendingVehicleTravelState state in queuedRoutes)
			{
				try
				{
					System.Diagnostics.Stopwatch routeDetailStopwatch = System.Diagnostics.Stopwatch.StartNew();
					long pathBuildMs = 0L;
					long markKnownMs = 0L;
					long hudRefreshMs = 0L;
					long syncMs = 0L;
					long driveMs = 0L;
					long bridgeMs = 0L;
					long authorityMs = 0L;
					long entityMs = 0L;
					long rebaseMs = 0L;
					long reachedNodeMs = 0L;
					long stateMs = 0L;
					long displayMs = 0L;
					long sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					LogAfterProhibitionRoutesTravelContinuationDecision(state.VehicleID, sourceTag);
					bridgeMs += GetElapsedMilliseconds(sectionTicks);
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					bool turnStartFinalized = WasTurnStartQueuedRouteFinalized(state.VehicleID);
					PendingVehicleTravelState resumeState = state;
					NodeID committedNodeId = NodeID.INVALID;
					string committedSource = "none";
					if (!TryGetFinalizedVehicleNodeId(state.VehicleID, out committedNodeId, out committedSource))
					{
						TryGetAuthoritativeVehicleNodeId(state.VehicleID, out committedNodeId, out committedSource);
					}
					LogVehicleAuthority(
						"route-resume-watchdog",
						$"{state.VehicleID.id}:{state.StartNodeID}:{state.ExpectedNodeID}:{state.GoalNodeID}:{committedNodeId}:{committedSource}:{sourceTag}",
						$"route-resume-watchdog vehicle={state.VehicleID.id} peep={state.PeepID.id} startNode={state.StartNodeID} expectedNode={state.ExpectedNodeID} finalGoal={state.GoalNodeID} committedNode={committedNodeId} committedSource={committedSource} turnStartFinalized={turnStartFinalized} active={_activeHumanVehicleTravel.Contains((long)state.VehicleID.id)} source={sourceTag}",
						dedupe: false);
					authorityMs += GetElapsedMilliseconds(sectionTicks);
					if (string.Equals(committedSource, "recent-finalize", StringComparison.Ordinal)
						&& TryGetAuthoritativeVehicleNodeId(state.VehicleID, out NodeID authoritativeCommittedNodeId, out string authoritativeCommittedSource)
						&& authoritativeCommittedNodeId.IsValid
						&& authoritativeCommittedNodeId != committedNodeId
						&& !AreNodesWithinStableFinalizeBridgeRange(committedNodeId, authoritativeCommittedNodeId))
					{
						NodeID staleCommittedNodeId = committedNodeId;
						ClearRecentFinalizedNode(state.VehicleID, "resume-stale-finalize");
						committedNodeId = authoritativeCommittedNodeId;
						committedSource = authoritativeCommittedSource;
						LogVehicleAuthority("turnstart-resume-stale-finalize-cleared", $"{state.VehicleID.id}:{staleCommittedNodeId}:{authoritativeCommittedNodeId}", $"turnstart-resume-stale-finalize-cleared vehicle={state.VehicleID.id} staleCommittedNode={staleCommittedNodeId} authoritativeNode={authoritativeCommittedNodeId} authoritativeSource={authoritativeCommittedSource} finalGoal={state.GoalNodeID}", dedupe: false);
					}
					if (turnStartFinalized && !committedNodeId.IsValid)
					{
						LogVehicleAuthority(
							"turnstart-resume-blocked",
							$"{state.VehicleID.id}:{state.GoalNodeID}:no-committed",
							$"turnstart-resume-blocked vehicle={state.VehicleID.id} committedNode={NodeID.INVALID} committedSource=none finalGoal={state.GoalNodeID} reason=no-committed",
							dedupe: false);
						LogVehicleAuthority(
							"turnstart-route-stale-cleared",
							$"{state.VehicleID.id}:{state.GoalNodeID}:no-committed",
							$"turnstart-route-stale-cleared vehicle={state.VehicleID.id} committedNode={NodeID.INVALID} committedSource=none finalGoal={state.GoalNodeID} reason=no-committed",
							dedupe: false);
						ClearPendingHumanVehicleTravel(state.VehicleID, "turnstart-resume-blocked");
						continue;
					}
					if (committedNodeId.IsValid && resumeState.StartNodeID.IsValid && resumeState.StartNodeID != committedNodeId)
					{
						long vehicleKey = (long)resumeState.VehicleID.id;
						resumeState.StartNodeID = committedNodeId;
						resumeState.ExpectedNodeID = NodeID.INVALID;
						resumeState.CommandOwnerKey = 0;
						resumeState.ResumeQueued = resumeState.GoalNodeID.IsValid && resumeState.GoalNodeID != committedNodeId;
						_pendingVehicleTravelByVehicleId[vehicleKey] = resumeState;
						_queuedArrivalCommittedNodeByVehicleId[vehicleKey] = committedNodeId;
						SetRecentFinalizedNode(resumeState.VehicleID, committedNodeId);
						ClearInterruptExpectedStartNode(resumeState.VehicleID, "turnstart-resume-rebased");
						LogVehicleAuthority("turnstart-resume-rebased-before-refresh", $"{resumeState.VehicleID.id}:{state.StartNodeID}:{committedNodeId}:{committedSource}", $"turnstart-resume-rebased-before-refresh vehicle={resumeState.VehicleID.id} staleStart={state.StartNodeID} committedNode={committedNodeId} committedSource={committedSource} finalGoal={resumeState.GoalNodeID}", dedupe: false);
						if (!resumeState.ResumeQueued)
						{
							ClearPendingHumanVehicleTravel(resumeState.VehicleID, "arrived");
							continue;
						}
					}
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					Entity vehicle = state.VehicleID.FindEntity();
					Entity peep = state.PeepID.FindEntity();
					CrewAssignment assignment = player.crew.GetCrewForPeep(state.PeepID);
					Node goalNode = state.GoalNodeID.FindNode();
					entityMs += GetElapsedMilliseconds(sectionTicks);
					if (vehicle?.data?.mobile == null || peep?.data?.agent == null || !assignment.IsValid || !assignment.IsInVehicle || assignment.VehicleID != state.VehicleID || goalNode == null)
					{
						LogVehicleAuthority("route-resume-skip", $"{state.VehicleID.id}:invalid", $"route-resume-skip vehicle={state.VehicleID.id} reason=invalid finalGoal={state.GoalNodeID}", dedupe: false);
						if (vehicle?.data?.mobile != null && peep?.data?.agent != null && goalNode != null)
						{
							LogVehicleAuthority("route-resume-deferred", $"{state.VehicleID.id}:invalid-state", $"route-resume-deferred vehicle={state.VehicleID.id} reason=transient-invalid finalGoal={state.GoalNodeID}", dedupe: false);
							continue;
						}
						ClearPendingHumanVehicleTravel(state.VehicleID, "invalid");
						continue;
					}
					System.Diagnostics.Stopwatch pathBuildStopwatch = System.Diagnostics.Stopwatch.StartNew();
					bool builtQueuedPath = TryBuildQueuedHumanVehiclePath(player, peep, assignment, goalNode, freeDrive: false, out PathData path, out Node startNode, out string source, out QueuedHumanVehiclePathBuildResult buildResult);
					pathBuildStopwatch.Stop();
					pathBuildMs += pathBuildStopwatch.ElapsedMilliseconds;
					if (!builtQueuedPath)
					{
						routeDetailStopwatch.Stop();
						if (routeDetailStopwatch.ElapsedMilliseconds >= 20 || pathBuildMs >= 20)
						{
							Debug.Log($"[PERF][RouteResumeDetail] outcome=build-failed ms={routeDetailStopwatch.ElapsedMilliseconds} pathBuildMs={pathBuildMs} vehicle={state.VehicleID.id} peep={state.PeepID.id} finalGoal={state.GoalNodeID} sourceTag={sourceTag} frame={Time.frameCount}");
						}
						LogVehicleAuthority("route-resume-skip", $"{state.VehicleID.id}:invalid-build", $"route-resume-skip vehicle={state.VehicleID.id} reason=invalid finalGoal={state.GoalNodeID}", dedupe: false);
						LogVehicleAuthority("route-resume-deferred", $"{state.VehicleID.id}:build-failed", $"route-resume-deferred vehicle={state.VehicleID.id} reason=build-failed finalGoal={state.GoalNodeID}", dedupe: false);
						continue;
					}
					LogVehicleAuthority("route-resume-source", $"{state.VehicleID.id}:{source}", $"route-resume-source vehicle={state.VehicleID.id} source={source}", dedupe: false);
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					bool rebasedBeforeRefresh = false;
					if (startNode?.id.IsValid == true
						&& resumeState.StartNodeID.IsValid
						&& startNode.id != resumeState.StartNodeID
						&& (string.Equals(source, "peep-live", StringComparison.Ordinal)
							|| string.Equals(source, "recent-finalize", StringComparison.Ordinal)
							|| string.Equals(source, "queued-arrival", StringComparison.Ordinal)))
					{
						long vehicleKey = (long)resumeState.VehicleID.id;
						resumeState.StartNodeID = startNode.id;
						resumeState.ExpectedNodeID = NodeID.INVALID;
						resumeState.CommandOwnerKey = 0;
						resumeState.ResumeQueued = resumeState.GoalNodeID.IsValid && resumeState.GoalNodeID != startNode.id;
						_pendingVehicleTravelByVehicleId[vehicleKey] = resumeState;
						_queuedArrivalCommittedNodeByVehicleId[vehicleKey] = startNode.id;
						SetRecentFinalizedNode(resumeState.VehicleID, startNode.id);
						ClearInterruptExpectedStartNode(resumeState.VehicleID, "resume-start-rebased");
						rebasedBeforeRefresh = true;
						LogVehicleAuthority("turnstart-resume-rebased-before-refresh", $"{resumeState.VehicleID.id}:{state.StartNodeID}:{startNode.id}:{source}", $"turnstart-resume-rebased-before-refresh vehicle={resumeState.VehicleID.id} staleStart={state.StartNodeID} committedNode={startNode.id} committedSource={source} finalGoal={resumeState.GoalNodeID}", dedupe: false);
						if (!resumeState.ResumeQueued)
						{
							ClearPendingHumanVehicleTravel(resumeState.VehicleID, "arrived");
							continue;
						}
					}
					rebaseMs += GetElapsedMilliseconds(sectionTicks);
					bool invalidResumeOrigin = string.Equals(source, "interrupt-expected", StringComparison.Ordinal)
						|| (turnStartFinalized
							&& !string.Equals(source, "queued-arrival", StringComparison.Ordinal)
							&& !string.Equals(source, "recent-finalize", StringComparison.Ordinal)
							&& !string.Equals(source, "peep-live", StringComparison.Ordinal));
					if (invalidResumeOrigin)
					{
						LogVehicleAuthority(
							"resume-origin-invalid",
							$"{state.VehicleID.id}:{source}:{committedNodeId}:{committedSource}",
							$"resume-origin-invalid vehicle={state.VehicleID.id} source={source} committedNode={committedNodeId} committedSource={committedSource} finalGoal={state.GoalNodeID}",
							dedupe: false);
						if (turnStartFinalized)
						{
							LogVehicleAuthority(
								"turnstart-resume-blocked",
								$"{state.VehicleID.id}:{source}:{committedNodeId}:invalid-origin",
								$"turnstart-resume-blocked vehicle={state.VehicleID.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={state.GoalNodeID} reason=invalid-origin source={source}",
								dedupe: false);
							LogVehicleAuthority(
								"turnstart-route-stale-cleared",
								$"{state.VehicleID.id}:{committedNodeId}:{state.GoalNodeID}:invalid-origin",
								$"turnstart-route-stale-cleared vehicle={state.VehicleID.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={state.GoalNodeID} reason=invalid-origin source={source}",
								dedupe: false);
						}
						ClearInterruptExpectedStartNode(state.VehicleID, "resume-origin-invalid");
						ClearPendingHumanVehicleTravel(state.VehicleID, turnStartFinalized ? "turnstart-resume-blocked" : "resume-origin-invalid");
						continue;
					}
					if (committedNodeId.IsValid && startNode?.id.IsValid == true && startNode.id != committedNodeId)
					{
						LogVehicleAuthority(
							"resume-origin-invalid",
							$"{state.VehicleID.id}:{startNode.id}:{committedNodeId}:{source}",
							$"resume-origin-invalid vehicle={state.VehicleID.id} source={source} startNode={startNode.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={state.GoalNodeID} reason=committed-mismatch",
							dedupe: false);
						if (turnStartFinalized)
						{
							LogVehicleAuthority(
								"turnstart-resume-blocked",
								$"{state.VehicleID.id}:{startNode.id}:{committedNodeId}:committed-mismatch",
								$"turnstart-resume-blocked vehicle={state.VehicleID.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={state.GoalNodeID} reason=committed-mismatch source={source}",
								dedupe: false);
							LogVehicleAuthority(
								"turnstart-route-stale-cleared",
								$"{state.VehicleID.id}:{committedNodeId}:{state.GoalNodeID}:committed-mismatch",
								$"turnstart-route-stale-cleared vehicle={state.VehicleID.id} committedNode={committedNodeId} committedSource={committedSource} finalGoal={state.GoalNodeID} reason=committed-mismatch source={source}",
								dedupe: false);
						}
						ClearInterruptExpectedStartNode(state.VehicleID, "committed-mismatch");
						ClearPendingHumanVehicleTravel(state.VehicleID, turnStartFinalized ? "turnstart-resume-blocked" : "resume-origin-invalid");
						continue;
					}
					if (path == null || path.world.Count <= 1)
					{
						NodeID currentNodeId = NodeID.INVALID;
						if (!TryGetFinalizedVehicleNodeId(state.VehicleID, out currentNodeId, out _))
						{
							TryGetAuthoritativeVehicleNodeId(state.VehicleID, out currentNodeId, out _);
						}
						if (currentNodeId.IsValid && currentNodeId == state.GoalNodeID)
						{
							LogVehicleAuthority("route-resume-skip", $"{state.VehicleID.id}:arrived", $"route-resume-skip vehicle={state.VehicleID.id} reason=already-arrived finalGoal={state.GoalNodeID}", dedupe: false);
							ClearPendingHumanVehicleTravel(state.VehicleID, "arrived");
						}
						else if (buildResult == QueuedHumanVehiclePathBuildResult.NoMovesYet
							|| buildResult == QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment)
						{
							string deferredReason = buildResult == QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment
								? "insufficient-moves-for-next-segment"
								: "no-moves";
							LogVehicleAuthority(
								"route-resume-deferred",
								$"{state.VehicleID.id}:{currentNodeId}:{state.GoalNodeID}:{deferredReason}",
								$"route-resume-deferred vehicle={state.VehicleID.id} reason={deferredReason} currentNode={currentNodeId} finalGoal={state.GoalNodeID}",
								dedupe: false);
							continue;
						}
						else
						{
							if (source == "queued-arrival" && TryBuildQueuedHumanVehiclePathFromCommittedNode(player, peep, assignment, goalNode, freeDrive: false, out PathData retryPath, out Node retryStartNode, out QueuedHumanVehiclePathBuildResult retryBuildResult))
							{
								LogVehicleAuthority("route-resume-retry", $"{state.VehicleID.id}:queued-arrival", $"route-resume-retry vehicle={state.VehicleID.id} source=queued-arrival", dedupe: false);
								path = retryPath;
								startNode = retryStartNode;
								buildResult = retryBuildResult;
								if (buildResult == QueuedHumanVehiclePathBuildResult.NoMovesYet
									|| buildResult == QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment)
								{
									string deferredReason = buildResult == QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment
										? "insufficient-moves-for-next-segment"
										: "no-moves";
									LogVehicleAuthority(
										"route-resume-deferred",
										$"{state.VehicleID.id}:{currentNodeId}:{state.GoalNodeID}:{deferredReason}-retry",
										$"route-resume-deferred vehicle={state.VehicleID.id} reason={deferredReason} currentNode={currentNodeId} finalGoal={state.GoalNodeID} source=queued-arrival-retry",
										dedupe: false);
									continue;
								}
								if (path != null && path.world.Count > 1)
								{
									// Retry succeeded using the committed queued-arrival node; continue with the rebuilt path.
								}
								else
								{
									LogVehicleAuthority("route-resume-invalid-path", $"{state.VehicleID.id}:queued-arrival", $"route-resume-invalid-path vehicle={state.VehicleID.id} currentNode={currentNodeId} finalGoal={state.GoalNodeID} source=queued-arrival-retry", dedupe: false);
									ClearPendingHumanVehicleTravel(state.VehicleID, "invalid");
									continue;
								}
							}
							else
							{
								LogVehicleAuthority("route-resume-invalid-path", $"{state.VehicleID.id}:build-empty", $"route-resume-invalid-path vehicle={state.VehicleID.id} currentNode={currentNodeId} finalGoal={state.GoalNodeID} source={source}", dedupe: false);
								ClearPendingHumanVehicleTravel(state.VehicleID, "invalid");
								continue;
							}
						}
					}

					WorldPos reachedPos = path.world[path.world.Count - 1];
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					Node reachedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(reachedPos, 5f);
					reachedNodeMs += GetElapsedMilliseconds(sectionTicks);
					if (reachedNode == null)
					{
						LogVehicleAuthority("route-resume-skip", $"{state.VehicleID.id}:invalid-reached", $"route-resume-skip vehicle={state.VehicleID.id} reason=invalid finalGoal={state.GoalNodeID}", dedupe: false);
						ClearPendingHumanVehicleTravel(state.VehicleID, "invalid");
						continue;
					}
					if (startNode?.id.IsValid == true && ShouldBlockHumanImmediateReverse(state.VehicleID, startNode.id, reachedNode.id))
					{
						NodeID rebasedNodeId = committedNodeId;
						string rebasedSource = committedSource;
						if (!rebasedNodeId.IsValid && TryGetFinalizedVehicleNodeId(state.VehicleID, out NodeID finalizedNodeId, out string finalizedSource))
						{
							rebasedNodeId = finalizedNodeId;
							rebasedSource = finalizedSource;
						}
						if (!rebasedNodeId.IsValid && TryGetAuthoritativeVehicleNodeId(state.VehicleID, out NodeID authoritativeNodeId, out string authoritativeSource))
						{
							rebasedNodeId = authoritativeNodeId;
							rebasedSource = authoritativeSource;
						}
						if (!rebasedNodeId.IsValid)
						{
							LogVehicleAuthority("turnstart-resume-blocked", $"{state.VehicleID.id}:{startNode.id}:{reachedNode.id}:reverse-no-commit", $"turnstart-resume-blocked vehicle={state.VehicleID.id} committedNode={NodeID.INVALID} committedSource=none finalGoal={state.GoalNodeID} reason=reverse-no-commit", dedupe: false);
							LogVehicleAuthority("turnstart-reverse-resume-cleared", $"{state.VehicleID.id}:{startNode.id}:{reachedNode.id}:reverse-no-commit", $"turnstart-reverse-resume-cleared vehicle={state.VehicleID.id} staleStart={startNode.id} staleGoal={reachedNode.id} finalGoal={state.GoalNodeID} reason=reverse-no-commit", dedupe: false);
							ClearPendingHumanVehicleTravel(state.VehicleID, "reverse-resume-blocked");
							continue;
						}

						long vehicleKey = (long)state.VehicleID.id;
						PendingVehicleTravelState rebasedState = state;
						rebasedState.StartNodeID = rebasedNodeId;
						rebasedState.ExpectedNodeID = NodeID.INVALID;
						rebasedState.CommandOwnerKey = 0;
						rebasedState.ResumeQueued = rebasedState.GoalNodeID.IsValid && rebasedState.GoalNodeID != rebasedNodeId;
						_pendingVehicleTravelByVehicleId[vehicleKey] = rebasedState;
						_queuedArrivalCommittedNodeByVehicleId[vehicleKey] = rebasedNodeId;
						SetRecentFinalizedNode(state.VehicleID, rebasedNodeId);
						ClearInterruptExpectedStartNode(state.VehicleID, "reverse-resume-rebase");
						LogVehicleAuthority("turnstart-reverse-resume-cleared", $"{state.VehicleID.id}:{startNode.id}:{reachedNode.id}:{rebasedNodeId}", $"turnstart-reverse-resume-cleared vehicle={state.VehicleID.id} staleStart={startNode.id} staleGoal={reachedNode.id} rebasedNode={rebasedNodeId} rebasedSource={rebasedSource} finalGoal={state.GoalNodeID}", dedupe: false);
						LogVehicleAuthority("queued-resume-refresh-suppressed", $"{state.VehicleID.id}:{startNode.id}:{reachedNode.id}:{rebasedNodeId}", $"queued-resume-refresh-suppressed vehicle={state.VehicleID.id} staleStart={startNode.id} staleGoal={reachedNode.id} rebasedNode={rebasedNodeId} rebasedSource={rebasedSource} finalGoal={state.GoalNodeID}", dedupe: false);

						if (!rebasedState.ResumeQueued)
						{
							ClearPendingHumanVehicleTravel(state.VehicleID, "arrived");
							continue;
						}

						pathBuildStopwatch.Restart();
						bool builtRebasedPath = TryBuildQueuedHumanVehiclePathFromCommittedNode(player, peep, assignment, goalNode, freeDrive: false, out PathData rebasedPath, out Node rebasedStartNode, out QueuedHumanVehiclePathBuildResult rebasedBuildResult);
						pathBuildStopwatch.Stop();
						pathBuildMs += pathBuildStopwatch.ElapsedMilliseconds;
						if (!builtRebasedPath)
						{
							LogVehicleAuthority("resume-origin-invalid", $"{state.VehicleID.id}:{rebasedNodeId}:reverse-rebuild-failed", $"resume-origin-invalid vehicle={state.VehicleID.id} source=reverse-rebuild committedNode={rebasedNodeId} committedSource={rebasedSource} finalGoal={state.GoalNodeID} reason=rebuild-failed", dedupe: false);
							continue;
						}
						if (rebasedBuildResult == QueuedHumanVehiclePathBuildResult.NoMovesYet
							|| rebasedBuildResult == QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment)
						{
							string deferredReason = rebasedBuildResult == QueuedHumanVehiclePathBuildResult.InsufficientMovesForSegment
								? "insufficient-moves-for-next-segment"
								: "no-moves";
							LogVehicleAuthority(
								"route-resume-deferred",
								$"{state.VehicleID.id}:{rebasedNodeId}:{state.GoalNodeID}:reverse-{deferredReason}",
								$"route-resume-deferred vehicle={state.VehicleID.id} reason={deferredReason} currentNode={rebasedNodeId} finalGoal={state.GoalNodeID} source=reverse-rebuild",
								dedupe: false);
							continue;
						}
						if (rebasedPath == null || rebasedPath.world == null || rebasedPath.world.Count <= 1)
						{
							LogVehicleAuthority("route-resume-invalid-path", $"{state.VehicleID.id}:reverse-rebuild-empty", $"route-resume-invalid-path vehicle={state.VehicleID.id} currentNode={rebasedNodeId} finalGoal={state.GoalNodeID} source=reverse-rebuild", dedupe: false);
							continue;
						}

						WorldPos rebasedReachedPos = rebasedPath.world[rebasedPath.world.Count - 1];
						Node rebasedReachedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(rebasedReachedPos, 5f);
						if (rebasedStartNode == null || rebasedReachedNode == null)
						{
							LogVehicleAuthority("turnstart-mobile-start-blocked", $"{state.VehicleID.id}:{rebasedNodeId}:{state.GoalNodeID}:reverse-invalid", $"turnstart-mobile-start-blocked vehicle={state.VehicleID.id} committedNode={rebasedNodeId} committedSource={rebasedSource} finalGoal={state.GoalNodeID} reason=reverse-rebuild-invalid", dedupe: false);
							continue;
						}
						if (IsHumanImmediateReverseSegment(state.VehicleID, rebasedStartNode.id, rebasedReachedNode.id))
						{
							pathBuildStopwatch.Restart();
							bool builtBridgePath = TryBuildQueuedHumanVehiclePathFromCommittedNode(player, peep, assignment, goalNode, freeDrive: true, out PathData bridgePath, out Node bridgeStartNode, out QueuedHumanVehiclePathBuildResult bridgeBuildResult);
							pathBuildStopwatch.Stop();
							pathBuildMs += pathBuildStopwatch.ElapsedMilliseconds;
							if (builtBridgePath
								&& bridgePath?.world != null
								&& bridgePath.world.Count > 1)
							{
								WorldPos bridgeReachedPos = bridgePath.world[bridgePath.world.Count - 1];
								Node bridgeReachedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(bridgeReachedPos, 5f);
								if (bridgeStartNode != null && bridgeReachedNode != null && bridgeReachedNode.id == state.GoalNodeID)
								{
									rebasedPath = bridgePath;
									rebasedStartNode = bridgeStartNode;
									rebasedReachedNode = bridgeReachedNode;
									LogVehicleAuthority(
										"route-resume-freebridge",
										$"{state.VehicleID.id}:{rebasedNodeId}:{state.GoalNodeID}:{bridgeBuildResult}",
										$"route-resume-freebridge vehicle={state.VehicleID.id} committedNode={rebasedNodeId} committedSource={rebasedSource} finalGoal={state.GoalNodeID} reason=avoid-immediate-reverse",
										dedupe: false);
								}
								else
								{
									LogVehicleAuthority("turnstart-mobile-start-blocked", $"{state.VehicleID.id}:{rebasedNodeId}:{state.GoalNodeID}:reverse-bridge-miss", $"turnstart-mobile-start-blocked vehicle={state.VehicleID.id} committedNode={rebasedNodeId} committedSource={rebasedSource} finalGoal={state.GoalNodeID} reason=reverse-bridge-miss bridgeNode={bridgeReachedNode?.id ?? NodeID.INVALID}", dedupe: false);
									continue;
								}
							}
							else
							{
								LogVehicleAuthority("turnstart-mobile-start-blocked", $"{state.VehicleID.id}:{rebasedNodeId}:{state.GoalNodeID}:reverse", $"turnstart-mobile-start-blocked vehicle={state.VehicleID.id} committedNode={rebasedNodeId} committedSource={rebasedSource} finalGoal={state.GoalNodeID} reason=reverse-rebuild", dedupe: false);
								continue;
							}
						}

						path = rebasedPath;
						startNode = rebasedStartNode;
						reachedNode = rebasedReachedNode;
						source = "queued-arrival-rebased";
						LogVehicleAuthority("resume-segment-rebased", $"{state.VehicleID.id}:{startNode.id}:{reachedNode.id}:{state.GoalNodeID}", $"resume-segment-rebased vehicle={state.VehicleID.id} startNode={startNode.id} nextGoal={reachedNode.id} finalGoal={state.GoalNodeID} source={rebasedSource}", dedupe: false);
					}
					bool instant = !player.IsHuman;
					System.Diagnostics.Stopwatch markKnownStopwatch = System.Diagnostics.Stopwatch.StartNew();
					foreach (PathNode pathNode in path.nodes)
					{
						if (player.IsHuman && !pathNode.node.known.Get(PlayerID.HumanPlayer))
						{
							peep.components.agent.IncrementStat(CrewStats.NodeScouted, 1);
						}
						player.meetings.MarkNodeAsKnown(pathNode.node, expectedSeen: true, instant);
					}
					markKnownStopwatch.Stop();
					markKnownMs += markKnownStopwatch.ElapsedMilliseconds;

					HumanVehicleTravelEndSyncPatch.ClearDeliveryPumpSegmentGuard(state.VehicleID, "queued-route-resume");
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					MarkHumanVehicleTravelActive(state.VehicleID, state.PeepID, QueuedRouteResumeCommandOwnerKey, startNode?.id ?? NodeID.INVALID, reachedNode.id, state.GoalNodeID);
					stateMs += GetElapsedMilliseconds(sectionTicks);
					NodeID previewFinalNodeId = reachedNode.id;
					sectionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
					TryGetHumanVehicleTravelDisplayFinalNodeId(state.VehicleID, reachedNode.id, out previewFinalNodeId, out string previewFinalSource);
					displayMs += GetElapsedMilliseconds(sectionTicks);
					Node refreshStartNode = rebasedBeforeRefresh ? null : startNode;
					if (rebasedBeforeRefresh)
					{
						LogVehicleAuthority("queued-resume-refresh-suppressed", $"{state.VehicleID.id}:{state.StartNodeID}:{startNode?.id ?? NodeID.INVALID}:{reachedNode.id}", $"queued-resume-refresh-suppressed vehicle={state.VehicleID.id} staleStart={state.StartNodeID} rebasedStart={startNode?.id ?? NodeID.INVALID} finalGoal={state.GoalNodeID}", dedupe: false);
					}
					System.Diagnostics.Stopwatch hudRefreshStopwatch = System.Diagnostics.Stopwatch.StartNew();
					TryRefreshCrewHudStateAfterTravel(state.VehicleID, previewFinalNodeId);
					hudRefreshStopwatch.Stop();
					hudRefreshMs += hudRefreshStopwatch.ElapsedMilliseconds;
					if (refreshStartNode?.id.IsValid == true && previewFinalNodeId.IsValid && refreshStartNode.id != previewFinalNodeId)
					{
						HideCornerInfoIfShowingNode(refreshStartNode.id);
					}
					LogVehicleAuthority("route-resume-business-preview-skipped", $"{state.VehicleID.id}:{startNode?.id ?? NodeID.INVALID}:{reachedNode.id}:{state.GoalNodeID}", $"route-resume-business-preview-skipped vehicle={state.VehicleID.id} startNode={startNode?.id ?? NodeID.INVALID} nextNode={reachedNode.id} finalGoal={state.GoalNodeID} reason=route-node-authority", dedupe: false);
					LogVehicleAuthority("route-resume", $"{state.VehicleID.id}:{startNode?.id ?? NodeID.INVALID}:{reachedNode.id}:{state.GoalNodeID}", $"route-resume vehicle={state.VehicleID.id} startNode={startNode?.id ?? NodeID.INVALID} nextGoal={reachedNode.id} finalGoal={state.GoalNodeID} previewNode={previewFinalNodeId} previewSource={previewFinalSource}", dedupe: false);
					if (startNode?.id.IsValid == true)
					{
						System.Diagnostics.Stopwatch syncStopwatch = System.Diagnostics.Stopwatch.StartNew();
						TrySyncVehicleOccupantsToNode(player.crew, state.VehicleID, startNode.id, "resume-travel-start", syncVehicle: false);
						syncStopwatch.Stop();
						syncMs += syncStopwatch.ElapsedMilliseconds;
					}
					System.Diagnostics.Stopwatch driveStopwatch = System.Diagnostics.Stopwatch.StartNew();
					global::Game.Game.ctx.transit.DriveOnPath(player.PID, vehicle, path);
					driveStopwatch.Stop();
					driveMs += driveStopwatch.ElapsedMilliseconds;
					peep.components.agent.AddXP(XPSource.FromDriving);
					vehicle.components.mobile.UpdateHealthFrom(assignment, VehicleHealthSource.FromDriving);
					routeDetailStopwatch.Stop();
					if (routeDetailStopwatch.ElapsedMilliseconds >= 20)
					{
						Debug.Log($"[PERF][RouteResumeDetail] outcome=started ms={routeDetailStopwatch.ElapsedMilliseconds} bridgeMs={bridgeMs} authorityMs={authorityMs} entityMs={entityMs} pathBuildMs={pathBuildMs} rebaseMs={rebaseMs} reachedNodeMs={reachedNodeMs} markKnownMs={markKnownMs} stateMs={stateMs} displayMs={displayMs} hudMs={hudRefreshMs} syncMs={syncMs} driveMs={driveMs} vehicle={state.VehicleID.id} peep={state.PeepID.id} nodes={path?.nodes?.Count ?? 0} world={path?.world?.Count ?? 0} startNode={startNode?.id ?? NodeID.INVALID} nextNode={reachedNode.id} finalGoal={state.GoalNodeID} source={source} sourceTag={sourceTag} frame={Time.frameCount}");
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] TryResumeQueuedHumanVehicleRoutes: " + ex.Message);
					ClearPendingHumanVehicleTravel(state.VehicleID, "invalid");
				}
			}
			if (singleVehicle)
			{
				_turnStartFinalizedQueuedRouteVehicleIds.Remove((long)onlyVehicleId.id);
			}
			else
			{
				_turnStartFinalizedQueuedRouteVehicleIds.Clear();
			}
		}

		internal static void LogSelectionHookConfiguration(MethodInfo mobileActivation, MethodInfo crewPickOnClick, MethodInfo crewPickMouseover)
		{
			try
			{
				GameplayTweaksPlugin.VerificationLog(
					"HostileMobileSelect",
					$"crew-pick hooks active onClick={(crewPickOnClick != null)} mouseover={(crewPickMouseover != null)}");
				if (mobileActivation == null)
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "activation hook check skipped methodMissing=True");
					return;
				}

				bool foundUnexpectedSelectionHook = false;
				Patches patchInfo = Harmony.GetPatchInfo(mobileActivation);
				if (patchInfo?.Prefixes != null)
				{
					foreach (Patch patch in patchInfo.Prefixes)
					{
						MethodInfo patchMethod = patch?.PatchMethod;
						if (patchMethod == null || patchMethod.DeclaringType == null || patchMethod.DeclaringType.Assembly != typeof(MultiCrewVehiclePatches).Assembly)
						{
							continue;
						}
						if (patchMethod.DeclaringType == typeof(AmbientTrafficActivationPatch))
						{
							continue;
						}
						foundUnexpectedSelectionHook = true;
						Debug.LogWarning("[GameplayTweaks] Unexpected activation-based selection hook still registered: " + patchMethod.DeclaringType.FullName + "." + patchMethod.Name);
					}
				}

				if (!foundUnexpectedSelectionHook)
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "activation selection hook disabled");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] LogSelectionHookConfiguration: " + ex.Message);
			}
		}

		internal static bool TryGetAuthoritativeVehicleNodeId(Entity vehicle, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (vehicle == null)
			{
				return false;
			}
			try
			{
				if (vehicle.Id.IsValid && TryGetFinalizedVehicleNodeId(vehicle.Id, out nodeId, out source) && nodeId.IsValid)
				{
					return true;
				}
				if (TryGetVehicleLiveAuthorityNodeId(vehicle, out nodeId, out source) && nodeId.IsValid)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryGetAuthoritativeVehicleNodeId: " + ex.Message);
			}
			nodeId = NodeID.INVALID;
			source = "none";
			return false;
		}

		internal static bool TryGetVehicleLiveAuthorityNodeId(Entity vehicle, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (vehicle == null)
			{
				return false;
			}

			NodeID mobileNodeId = vehicle.components?.mobile?.FindNodeNearThisMobile() ?? NodeID.INVALID;
			NodeID boardNodeId = vehicle.data?.board?.bead.nodeId ?? NodeID.INVALID;
			NodeID agentNodeId = vehicle.data?.agent?.nid ?? NodeID.INVALID;

			if (vehicle.Id.IsValid && TryGetRecentFinalizedNodeId(vehicle.Id, out NodeID recentFinalizedNodeId) && recentFinalizedNodeId.IsValid)
			{
				if (boardNodeId.IsValid && boardNodeId == recentFinalizedNodeId)
				{
					nodeId = boardNodeId;
					source = "board-recent-finalize";
					return true;
				}

				if (agentNodeId.IsValid && agentNodeId == recentFinalizedNodeId)
				{
					nodeId = agentNodeId;
					source = "agent-recent-finalize";
					return true;
				}
			}

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

			nodeId = NodeID.INVALID;
			source = "none";
			return false;
		}

		internal static bool TryGetVehicleLiveAuthorityNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			int frame = Time.frameCount;
			if (_vehicleLiveAuthorityNodeCacheByVehicleId.TryGetValue(vehicleKey, out CachedVehicleNodeAuthority cached)
				&& cached.Frame == frame)
			{
				nodeId = cached.NodeId;
				source = cached.Source ?? "none";
				return cached.HasNode;
			}

			bool hasNode = TryGetVehicleLiveAuthorityNodeId(vehicleId.FindEntity(), out nodeId, out source);
			_vehicleLiveAuthorityNodeCacheByVehicleId[vehicleKey] = new CachedVehicleNodeAuthority
			{
				Frame = frame,
				HasNode = hasNode,
				NodeId = hasNode ? nodeId : NodeID.INVALID,
				Source = hasNode ? source : "none"
			};
			return hasNode;
		}

		internal static bool TryGetAuthoritativeVehicleNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			return vehicleId.IsValid && TryGetAuthoritativeVehicleNodeId(vehicleId.FindEntity(), out nodeId, out source);
		}

		internal static bool TryGetAuthoritativeVehicleNode(Entity vehicle, out Node node, out string source)
		{
			node = null;
			if (!TryGetAuthoritativeVehicleNodeId(vehicle, out NodeID nodeId, out source))
			{
				return false;
			}
			node = nodeId.FindNode();
			return node != null;
		}

		internal static bool TryGetAuthoritativeVehicleNode(EntityID vehicleId, out Node node, out string source)
		{
			node = null;
			source = "none";
			return vehicleId.IsValid && TryGetAuthoritativeVehicleNode(vehicleId.FindEntity(), out node, out source);
		}

		public static int GetVehicleCrewSlots(EntityID vehicleId)
		{
			Entity v = vehicleId.FindEntity();
			return GetVehicleCrewSlots(v);
		}

		public static int GetVehicleCrewSlots(Entity vehicle)
		{
			if (vehicle?.config == null)
				return DefaultCrewSlots;
			return ResolveVehicleCrewSlots(vehicle.config.Template.String);
		}

		private static int ResolveVehicleCrewSlots(string template)
		{
			if (string.IsNullOrEmpty(template))
				return DefaultCrewSlots;
			if (VehicleCrewSlots.TryGetValue(template, out int slots))
				return slots;
			const string preorderSuffix = "-preorder";
			if (template.EndsWith(preorderSuffix, StringComparison.OrdinalIgnoreCase))
			{
				string baseTemplate = template.Substring(0, template.Length - preorderSuffix.Length);
				if (!string.IsNullOrEmpty(baseTemplate) && VehicleCrewSlots.TryGetValue(baseTemplate, out slots))
					return slots;
			}
			return DefaultCrewSlots;
		}

		public static int GetVehicleCrewCount(PlayerCrew crew, EntityID vehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
				return 0;
			int count = 0;
			foreach (CrewAssignment c in crew.AllCrew)
			{
				if (c.IsInVehicle && c.VehicleID == vehicleId)
					count++;
			}
			return count;
		}

		public static int GetLiveVehicleCrewCount(PlayerCrew crew, EntityID vehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
				return 0;
			int count = 0;
			foreach (CrewAssignment c in crew.AllCrew)
			{
				if (c.IsInVehicle && c.VehicleID == vehicleId && c.IsNotDead && crew.IsOnBoard(c.peepId))
					count++;
			}
			return count;
		}

		public static List<CrewAssignment> GetAllCrewInVehicle(PlayerCrew crew, EntityID vehicleId)
		{
			var list = new List<CrewAssignment>();
			if (crew == null || !vehicleId.IsValid)
				return list;
			foreach (CrewAssignment c in crew.AllCrew)
			{
				if (c.IsInVehicle && c.VehicleID == vehicleId)
					list.Add(c);
			}
			return list;
		}

		private static EntityID TryGetEnemyVehicleIdForEntity(PlayerCrew crew, Entity entity)
		{
			if (crew == null || entity == null || !entity.Id.IsValid)
			{
				return EntityID.INVALID;
			}
			if (entity.components?.mobile != null)
			{
				return entity.Id;
			}

			CrewAssignment assignment = crew.GetCrewForPeep(entity.Id);
			if (assignment.IsValid && assignment.VehicleID.IsValid)
			{
				return assignment.VehicleID;
			}

			EntityID assignedVehicleId = crew.FindVehicleAssignedToPeep(entity.Id);
			return assignedVehicleId.IsValid ? assignedVehicleId : EntityID.INVALID;
		}

		private static EntityID ChooseVehicleRepresentativePeepId(PlayerCrew crew, EntityID vehicleId, List<CrewAssignment> activeOccupants)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return EntityID.INVALID;
			}

			EntityID driverPeepId = GetDriverPeepId(crew, vehicleId);
			if (activeOccupants != null && activeOccupants.Any(item => item.peepId == driverPeepId))
			{
				return driverPeepId;
			}

			if (activeOccupants != null)
			{
				CrewAssignment activeRepresentative = activeOccupants
					.Where(item => item.IsValid && item.peepId.IsValid)
					.OrderBy(item => item.peepId.id)
					.FirstOrDefault();
				if (activeRepresentative.IsValid)
				{
					return activeRepresentative.peepId;
				}
			}
			return EntityID.INVALID;
		}

		private static List<CrewAssignment> GetInspectableEnemyVehicleOccupants(PlayerCrew crew, EntityID vehicleId)
		{
			return GetAllCrewInVehicle(crew, vehicleId)
				.Where(item => item.IsValid && item.peepId.IsValid && IsInspectableVehicleOccupant(crew, item))
				.OrderBy(item => item.peepId.id)
				.ToList();
		}

		private static void SetRecentEnemyVehicleDeathRepresentative(EntityID vehicleId, EntityID peepId)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			if (peepId.IsValid)
			{
				_recentEnemyVehicleDeathPeepByVehicleId[vehicleKey] = (long)peepId.id;
			}
			else
			{
				_recentEnemyVehicleDeathPeepByVehicleId.Remove(vehicleKey);
			}
		}

		private static bool TryGetRecentEnemyVehicleDeathRepresentative(EntityID vehicleId, out EntityID peepId)
		{
			peepId = EntityID.INVALID;
			if (!vehicleId.IsValid || !_recentEnemyVehicleDeathPeepByVehicleId.TryGetValue((long)vehicleId.id, out long rawPeepId))
			{
				return false;
			}

			peepId = EntityID.FromID((ulong)rawPeepId);
			return peepId.IsValid;
		}

		private static bool EnemyVehicleHasSearchableRewards(Entity vehicle)
		{
			if (vehicle == null)
			{
				return false;
			}

			try
			{
				if (GameplayTweaksPlugin.ReadInventoryAmount(vehicle, "cash") > 0
					|| GameplayTweaksPlugin.ReadInventoryAmount(vehicle, ModConstants.DIRTY_CASH_LABEL) > 0)
				{
					return true;
				}

				InventoryModule inventory = ModulesUtil.GetInventory(vehicle);
				if (((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory)?.data?.contents == null)
				{
					return false;
				}

				return inventory.data.contents.Any(content => content.qty.IntFloor() > 0);
			}
			catch
			{
				return false;
			}
		}

		private static bool ClearEnemyVehicleTransientPresentation(EntityID vehicleId, string source, string reason)
		{
			if (!vehicleId.IsValid)
			{
				return false;
			}

			bool changed = false;
			long vehicleKey = (long)vehicleId.id;
			if (_recentEnemyVehicleDeathPeepByVehicleId.Remove(vehicleKey))
			{
				changed = true;
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"dead-representative-cleared vehicle={vehicleId.id} source={source} reason={reason}");
			}
			if (_lastEnemyVehicleRepresentativeByVehicleId.Remove(vehicleKey))
			{
				changed = true;
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"representative-cleared vehicle={vehicleId.id} source={source} reason={reason}");
			}
			_vehicleSearchBlockedBySurvivors.Remove(vehicleKey);
			DriverMap.Remove(vehicleKey);
			return changed;
		}

		internal static bool TryCleanupEmptyEnemyVehiclePresentation(PlayerCrew crew, EntityID vehicleId, string source, out bool rewardAvailable)
		{
			rewardAvailable = false;
			if (crew == null || !vehicleId.IsValid)
			{
				return false;
			}

			try
			{
				bool hasLiveOccupants = crew.AllCrew.Any(assignment =>
					assignment.IsValid
					&& assignment.IsInVehicle
					&& assignment.VehicleID == vehicleId
					&& assignment.IsNotDead
					&& crew.IsOnBoard(assignment.peepId));
				if (hasLiveOccupants)
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"empty-vehicle-cleanup-blocked vehicle={vehicleId.id} source={source} reason=live-occupants");
					return false;
				}

				Entity vehicle = vehicleId.FindEntity();
				rewardAvailable = EnemyVehicleHasSearchableRewards(vehicle);
				if (rewardAvailable)
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"empty-vehicle-cleanup-deferred vehicle={vehicleId.id} source={source} reason=loot-available");
					return false;
				}

				TryGetRecentEnemyVehicleDeathRepresentative(vehicleId, out EntityID deadRepresentativePeepId);
				bool cleanedPresentation = ClearEnemyVehicleTransientPresentation(vehicleId, source, "no-loot");
				bool removedFromScavengeable = GameplayTweaksPlugin.RemoveScavengeableCarRecord(crew, vehicleId);
				bool allowImmediateDeathCleanup = !string.IsNullOrEmpty(source)
					&& source.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0;
				bool despawned = false;
				if (allowImmediateDeathCleanup && !rewardAvailable && vehicle != null)
				{
					try
					{
						global::Game.Game.ctx?.transit?.DespawnCar(vehicleId, false);
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] TryCleanupEmptyEnemyVehiclePresentation despawn failed: " + ex.Message);
					}

					despawned = vehicleId.FindEntity() == null;
				}

				if (cleanedPresentation || removedFromScavengeable || despawned)
				{
					GameplayTweaksPlugin.ClearCrewPicksForTargets("empty-vehicle-cleanup:" + source, vehicleId, deadRepresentativePeepId);
				}
				if (!string.Equals(source, "vehicle-search", StringComparison.Ordinal)
					&& !string.Equals(source, "search-unblocked", StringComparison.Ordinal)
					&& !allowImmediateDeathCleanup)
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"empty-vehicle-cleanup-deferred vehicle={vehicleId.id} source={source} reason=awaiting-search cleanedPresentation={cleanedPresentation} removedFromScavengeable={removedFromScavengeable}");
					return cleanedPresentation || removedFromScavengeable;
				}
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"empty-vehicle-cleanup vehicle={vehicleId.id} source={source} removedFromScavengeable={removedFromScavengeable} persisted={(vehicleId.FindEntity() != null)} cleanedPresentation={cleanedPresentation} despawned={despawned}");
				return cleanedPresentation || removedFromScavengeable || despawned;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryCleanupEmptyEnemyVehiclePresentation: " + ex.Message);
				return false;
			}
		}

		internal static void RefreshEnemyVehicleRepresentative(PlayerCrew crew, EntityID vehicleId, string source)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return;
			}

			try
			{
				Entity vehicle = vehicleId.FindEntity();
				PlayerID ownerPid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
				if (!ownerPid.IsValid || ownerPid.IsHumanPlayer)
				{
					return;
				}

				List<CrewAssignment> inspectableOccupants = GetInspectableEnemyVehicleOccupants(crew, vehicleId);
				List<CrewAssignment> activeOccupants = inspectableOccupants
					.Where(item => crew.IsOnBoard(item.peepId))
					.ToList();
				if (activeOccupants.Count <= 0 && inspectableOccupants.Count > 0)
				{
					activeOccupants = inspectableOccupants;
				}
				EntityID representativePeepId = ChooseVehicleRepresentativePeepId(crew, vehicleId, activeOccupants);
				long vehicleKey = (long)vehicleId.id;
				EntityID priorRepresentativePeepId = _lastEnemyVehicleRepresentativeByVehicleId.TryGetValue(vehicleKey, out long rawRepresentativePeepId)
					? EntityID.FromID((ulong)rawRepresentativePeepId)
					: EntityID.INVALID;

				if (representativePeepId.IsValid)
				{
					SetRecentEnemyVehicleDeathRepresentative(vehicleId, EntityID.INVALID);
					_lastEnemyVehicleRepresentativeByVehicleId[vehicleKey] = (long)representativePeepId.id;
					if (priorRepresentativePeepId != representativePeepId)
					{
						RecordEnemyVehicleRepresentativeSwap(source);
					}
					return;
				}

				if (_lastEnemyVehicleRepresentativeByVehicleId.Remove(vehicleKey) && priorRepresentativePeepId.IsValid)
				{
					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"representative-cleared vehicle={vehicleId.id} from={priorRepresentativePeepId.id} source={source}:no-live-occupants");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] RefreshEnemyVehicleRepresentative: " + ex.Message);
			}
		}

		internal static bool TryGetEnemyVehicleDisplayState(Entity entity, out EnemyVehicleDisplayState state)
		{
			state = null;
			PlayerID pid = entity?.data?.mobile?.pid ?? entity?.data?.agent?.pid ?? PlayerID.INVALID;
			if (!pid.IsValid || pid.IsHumanPlayer)
			{
				return false;
			}

			PlayerCrew crew = pid.FindPlayer()?.crew;
			EntityID vehicleId = TryGetEnemyVehicleIdForEntity(crew, entity);
			return TryGetEnemyVehicleDisplayState(crew, vehicleId, entity, out state);
		}

		internal static bool TryGetEnemyVehicleDisplayState(PlayerCrew crew, EntityID vehicleId, out EnemyVehicleDisplayState state)
		{
			return TryGetEnemyVehicleDisplayState(crew, vehicleId, null, out state);
		}

		private static bool TryGetEnemyVehicleDisplayState(PlayerCrew crew, EntityID vehicleId, Entity preferredEntity, out EnemyVehicleDisplayState state)
		{
			state = null;
			if (crew == null || !vehicleId.IsValid)
			{
				return false;
			}

			Entity vehicle = vehicleId.FindEntity();
			PlayerID ownerPid = vehicle?.data?.mobile?.pid ?? preferredEntity?.data?.agent?.pid ?? PlayerID.INVALID;
			if (!ownerPid.IsValid || ownerPid.IsHumanPlayer)
			{
				return false;
			}

			List<CrewAssignment> inspectableOccupants = GetInspectableEnemyVehicleOccupants(crew, vehicleId);
			List<CrewAssignment> activeOccupants = inspectableOccupants
				.Where(item => crew.IsOnBoard(item.peepId))
				.ToList();
			EntityID driverPeepId = GetDriverPeepId(crew, vehicleId);
			EntityID representativePeepId = ChooseVehicleRepresentativePeepId(crew, vehicleId, activeOccupants);
			EntityID deadRepresentativePeepId = EntityID.INVALID;
			if (!representativePeepId.IsValid)
			{
				bool suppressDeadRepresentative = ownerPid.FindPlayer()?.IsJustCop == true;
				if (!suppressDeadRepresentative)
				{
					TryGetRecentEnemyVehicleDeathRepresentative(vehicleId, out deadRepresentativePeepId);
				}
				if (deadRepresentativePeepId.IsValid && vehicle == null)
				{
					TryCleanupEmptyEnemyVehiclePresentation(crew, vehicleId, "display-state-prune", out _);
					deadRepresentativePeepId = EntityID.INVALID;
				}
			}
			// Prefer live occupants, but keep the most recent dead representative only while
			// the vehicle still has a searchable followup so the player can empty/search it.
			EntityID inspectablePeepId = representativePeepId.IsValid
				? representativePeepId
				: (deadRepresentativePeepId.IsValid
					? deadRepresentativePeepId
					: ChooseVehicleRepresentativePeepId(crew, vehicleId, inspectableOccupants));
			int liveOccupantCount = activeOccupants.Count;
			int passengerCount = Math.Max(0, liveOccupantCount - (driverPeepId.IsValid && activeOccupants.Any(item => item.peepId == driverPeepId) ? 1 : 0));
			int inspectableOccupantCount = inspectableOccupants.Count > 0
				? inspectableOccupants.Count
				: (deadRepresentativePeepId.IsValid ? 1 : 0);
			int inspectablePassengerCount = Math.Max(0, inspectableOccupantCount - (driverPeepId.IsValid && inspectableOccupants.Any(item => item.peepId == driverPeepId) ? 1 : 0));

			state = new EnemyVehicleDisplayState
			{
				VehicleID = vehicleId,
				OwnerPid = ownerPid,
				DriverPeepId = driverPeepId,
				RepresentativePeepId = representativePeepId,
				InspectablePeepId = inspectablePeepId,
				LiveOccupantCount = liveOccupantCount,
				PassengerCount = passengerCount,
				InspectableOccupantCount = inspectableOccupantCount,
				InspectablePassengerCount = inspectablePassengerCount,
				CrewSlots = Math.Max(1, GetVehicleCrewSlots(vehicleId)),
				Presentation = liveOccupantCount > 0
					? EnemyVehiclePresentationMode.ActiveCrew
					: (deadRepresentativePeepId.IsValid ? EnemyVehiclePresentationMode.DeadCrew : EnemyVehiclePresentationMode.Empty)
			};
			return true;
		}

		internal static bool TryGetEnemyVehicleCrewIndicator(Entity entity, out string indicator, out EntityID vehicleId)
		{
			indicator = null;
			vehicleId = EntityID.INVALID;
			if (!TryGetEnemyVehicleDisplayState(entity, out EnemyVehicleDisplayState state))
			{
				return false;
			}
			vehicleId = state.VehicleID;
			if (!state.HasInspectableOccupants)
			{
				return false;
			}
			int passengerCount = state.HasLiveOccupants ? state.PassengerCount : state.InspectablePassengerCount;
			int crewCount = state.HasLiveOccupants ? state.LiveOccupantCount : state.InspectableOccupantCount;
			indicator = $"Passengers: {passengerCount} | Crew {crewCount}/{state.CrewSlots}";
			return true;
		}

		internal static bool TryResolveEnemyVehicleInspectTarget(Entity entity, out Entity resolvedPeep, out EntityID vehicleId, out EnemyVehicleDisplayState state, out string reason)
		{
			resolvedPeep = entity;
			vehicleId = EntityID.INVALID;
			state = null;
			reason = "not-vehicle";
			if (entity == null)
			{
				return false;
			}
			if (!TryGetEnemyVehicleDisplayState(entity, out state))
			{
				return false;
			}

			vehicleId = state.VehicleID;
			Entity inspectablePeep = state.InspectablePeep;
			if (!state.HasInspectableOccupants || inspectablePeep == null)
			{
				resolvedPeep = null;
				reason = state.HasLiveOccupants ? "representative-missing" : "no-inspectable-occupants";
				return true;
			}

			resolvedPeep = inspectablePeep;
			if (state.HasLiveOccupants)
			{
				reason = resolvedPeep.Id == entity.Id ? "live-current" : "normalized";
			}
			else
			{
				reason = resolvedPeep.Id == entity.Id ? "assigned-current" : "assigned-fallback";
			}
			return true;
		}

		internal static Entity ResolveVehicleActionPeep(PlayerCrew crew, EntityID vehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return null;
			}
			if (TryGetEnemyVehicleDisplayState(crew, vehicleId, out EnemyVehicleDisplayState state) && state.HasInspectableOccupants)
			{
				Entity representative = state.InspectablePeep;
				if (representative != null)
				{
					return representative;
				}
			}
			return null;
		}

		private static bool ShouldSuppressHumanVehicleRequeue(EntityID vehicleId, NodeID startNodeId, NodeID goalNodeId)
		{
			if (!vehicleId.IsValid || !startNodeId.IsValid || !goalNodeId.IsValid || startNodeId == goalNodeId)
			{
				return false;
			}
			if (IsHumanVehicleDeliveryAutomationActive(vehicleId))
			{
				LogVehicleAuthority(
					"route-requeue-allowed",
					$"{vehicleId.id}:{startNodeId}:{goalNodeId}:delivery",
					$"route-requeue-allowed vehicle={vehicleId.id} currentNode={startNodeId} finalGoal={goalNodeId} reason=delivery-automation",
					dedupe: true);
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			int day = G.GetNow().days;
			if (_recentHumanVehicleRequeueByVehicleId.TryGetValue(vehicleKey, out HumanVehicleRequeueState priorState)
				&& priorState.Day == day
				&& priorState.StartNodeID == startNodeId
				&& priorState.GoalNodeID == goalNodeId)
			{
				return true;
			}

			if (!_recentHumanVehicleRequeueByVehicleId.TryGetValue(vehicleKey, out HumanVehicleRequeueState state))
			{
				state = default;
			}
			state.StartNodeID = startNodeId;
			state.GoalNodeID = goalNodeId;
			state.Day = day;
			_recentHumanVehicleRequeueByVehicleId[vehicleKey] = state;
			return false;
		}

		internal static bool IsHumanVehicleDeliveryAutomationActive(EntityID vehicleId)
		{
			try
			{
				if (!vehicleId.IsValid)
				{
					return false;
				}
				AutomationSequence sequence = G.GetHumanPlayer()?.automation?.GetAutoOrNull(vehicleId);
				return sequence != null && sequence.IsAutoActive && sequence.steps != null && sequence.steps.Count > 0;
			}
			catch
			{
				return false;
			}
		}

		internal static bool TryStartCrewVisitWithPendingSelectionScope(Entity targetPeep, CrewAssignment matchingCrew)
		{
			if (targetPeep == null || !targetPeep.Id.IsValid || !matchingCrew.IsValid)
			{
				return false;
			}
			var controller = global::Game.Game.ctx?.hud?.convoDialog?.Controller;
			if (controller == null)
			{
				return false;
			}
			PushPendingPresenceSelectionScope();
			try
			{
				controller.StartCrewVisit(targetPeep, matchingCrew);
				return true;
			}
			finally
			{
				PopPendingPresenceSelectionScope();
			}
		}

		internal static List<Entity> GetHumanCombatPopupCrewAtNode(NodeID targetNodeId, CrewNodeResolutionMode mode)
		{
			var result = new List<Entity>();
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer?.crew == null || humanPlayer.crew.IsCrewDefeated || !targetNodeId.IsValid)
			{
				return result;
			}
			foreach (CrewAssignment crew in GetHumanCrewPresentAtNode(humanPlayer.crew, targetNodeId, mode))
			{
				if (!crew.IsValid || !crew.peepId.IsValid)
				{
					continue;
				}
				Entity peep = crew.GetPeep();
				if (peep != null)
				{
					result.Add(peep);
				}
			}
			return result;
		}

		internal static bool TryFindHumanCrewAtNode(NodeID targetNodeId, CrewNodeResolutionMode mode, out CrewAssignment matchingCrew, out int matchingCount)
		{
			matchingCrew = CrewAssignment.EMPTY;
			matchingCount = 0;
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer?.crew == null || humanPlayer.crew.IsCrewDefeated || !targetNodeId.IsValid)
			{
				return false;
			}
			List<CrewAssignment> matches = GetHumanCrewPresentAtNode(humanPlayer.crew, targetNodeId, mode);
			matchingCount = matches.Count;
			if (matchingCount <= 0)
			{
				return false;
			}
			CrewAssignment bossCrew = humanPlayer.crew.GetCrewForPlayerPeep();
			CrewAssignment bossMatch = matches.FirstOrDefault(item => item.IsValid && bossCrew.IsValid && item.peepId == bossCrew.peepId);
			if (bossMatch.IsValid)
			{
				matchingCrew = bossMatch;
				return true;
			}
			matchingCrew = matches.FirstOrDefault(item => item.IsValid);
			return matchingCrew.IsValid;
		}

		internal static string GetCrewNodeResolutionSourceTag(CrewNodeResolutionMode mode)
		{
			if (mode == CrewNodeResolutionMode.QueuedFinalGoalAllowed)
			{
				return "interactive-live";
			}
			if (mode == CrewNodeResolutionMode.BuildingCommittedOnly)
			{
				return "building-committed";
			}
			return "live-authoritative";
		}

		internal static bool TryFindInteractiveHumanCrewAtNode(NodeID targetNodeId, out CrewAssignment matchingCrew, out int matchingCount, out string sourceTag)
		{
			CrewNodeResolutionMode mode = GetInteractiveCrewNodeResolutionMode();
			sourceTag = GetCrewNodeResolutionSourceTag(mode);
			if (TryFindHumanCrewAtNode(targetNodeId, mode, out matchingCrew, out matchingCount))
			{
				sourceTag = matchingCrew.IsValid ? GetHumanVehicleInteractiveSourceTag(matchingCrew) : GetCrewNodeResolutionSourceTag(mode);
				return true;
			}
			if (mode != CrewNodeResolutionMode.LiveAuthoritative
				&& TryFindHumanCrewAtNode(targetNodeId, CrewNodeResolutionMode.LiveAuthoritative, out matchingCrew, out matchingCount))
			{
				sourceTag = "live-authoritative-fallback";
				return true;
			}
			return false;
		}

		internal static bool TryFindActualHumanCrewAtNode(NodeID targetNodeId, out CrewAssignment matchingCrew, out int matchingCount, out string sourceTag)
		{
			CrewNodeResolutionMode mode = GetActualHumanInteractionCrewNodeResolutionMode();
			sourceTag = GetCrewNodeResolutionSourceTag(mode);
			if (TryFindHumanCrewAtNode(targetNodeId, mode, out matchingCrew, out matchingCount))
			{
				sourceTag = matchingCrew.IsValid ? GetHumanVehicleSourceTag(matchingCrew, mode) : GetCrewNodeResolutionSourceTag(mode);
				return true;
			}
			if (mode != CrewNodeResolutionMode.LiveAuthoritative
				&& TryFindHumanCrewAtNode(targetNodeId, CrewNodeResolutionMode.LiveAuthoritative, out matchingCrew, out matchingCount))
			{
				sourceTag = "live-authoritative-fallback";
				return true;
			}
			return false;
		}

		internal static bool TryGetResolvedHumanVehiclePathStartNode(EntityID vehicleId, NodeID peepNodeId, out Node node, out string source, bool allowQueuedResumeRepair = false)
		{
			node = null;
			source = "none";

			Node priorNode = null;
			string priorSource = "none";
			bool foundBestNode = TryGetBestHumanVehicleStartNode(vehicleId, out priorNode, out priorSource);
			node = priorNode;
			source = priorSource;

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.GoalNodeID.IsValid
				&& pendingState.StartNodeID.IsValid
				&& !pendingState.ExpectedNodeID.IsValid
				&& string.Equals(priorSource, "recent-finalize", StringComparison.Ordinal)
				&& priorNode?.id.IsValid == true
				&& priorNode.id != pendingState.StartNodeID)
			{
				Node pendingStartNode = pendingState.StartNodeID.FindNode();
				if (pendingStartNode != null)
				{
					LogVehicleAuthority(
						"recent-finalize-authority-suppressed",
						$"{vehicleId.id}:{priorNode.id}:{pendingState.StartNodeID}:{pendingState.GoalNodeID}:path-start",
						$"recent-finalize-authority-suppressed vehicle={vehicleId.id} staleNode={priorNode.id} committedNode={pendingState.StartNodeID} finalGoal={pendingState.GoalNodeID} phase=path-start",
						dedupe: false);
					node = pendingStartNode;
					source = "pending-start";
					foundBestNode = true;
				}
			}

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out pendingState)
				&& pendingState.ExpectedNodeID.IsValid
				&& peepNodeId.IsValid
				&& peepNodeId == pendingState.ExpectedNodeID
				&& string.Equals(priorSource, "recent-finalize", StringComparison.Ordinal)
				&& priorNode?.id.IsValid == true
				&& priorNode.id != peepNodeId)
			{
				Node peepNode = peepNodeId.FindNode();
				if (peepNode != null)
				{
					LogVehicleAuthority(
						"path-redirect-resolved",
						$"{vehicleId.id}:{priorNode.id}:{peepNodeId}:pending-expected",
						$"path-redirect-resolved vehicle={vehicleId.id} priorNode={priorNode.id} resolvedNode={peepNodeId} priorSource=recent-finalize resolvedSource=pending-expected finalGoal={pendingState.GoalNodeID}",
						dedupe: false);
					node = peepNode;
					source = "pending-expected";
					SetRecentFinalizedNode(vehicleId, peepNodeId);
					return true;
				}
			}

			bool resumeOrTurnStartState = HasQueuedHumanVehiclePendingResume(vehicleId) || WasTurnStartQueuedRouteFinalized(vehicleId);
			bool allowStaleCommittedRepair = allowQueuedResumeRepair
				&& resumeOrTurnStartState
				&& priorNode?.id.IsValid == true
				&& peepNodeId.IsValid
				&& priorNode.id != peepNodeId;
			bool canRepairFromPeepNode = vehicleId.IsValid
				&& peepNodeId.IsValid
				&& !IsHumanVehicleTravelActive(vehicleId)
				&& (!HasQueuedHumanVehiclePendingResume(vehicleId) || allowQueuedResumeRepair)
				&& (string.Equals(priorSource, "mobile", StringComparison.Ordinal)
					|| string.Equals(priorSource, "board", StringComparison.Ordinal)
					|| string.Equals(priorSource, "agent", StringComparison.Ordinal)
					|| !foundBestNode
					|| priorNode == null
					|| allowStaleCommittedRepair);
			if (canRepairFromPeepNode
				&& string.Equals(priorSource, "mobile", StringComparison.Ordinal)
				&& priorNode?.id.IsValid == true
				&& peepNodeId.IsValid
				&& priorNode.id != peepNodeId
				&& TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveStartNodeId, out string liveStartSource)
				&& liveStartNodeId.IsValid
				&& liveStartNodeId == priorNode.id)
			{
				LogVehicleAuthority(
					"path-peep-live-suppressed",
					$"{vehicleId.id}:{priorNode.id}:{peepNodeId}:{liveStartSource}",
					$"path-peep-live-suppressed vehicle={vehicleId.id} vehicleNode={priorNode.id} peepNode={peepNodeId} vehicleSource={liveStartSource} priorSource={priorSource} reason=prefer-mobile-authority",
					dedupe: true);
				return true;
			}
			if (canRepairFromPeepNode)
			{
				Node peepNode = peepNodeId.FindNode();
				if (peepNode != null)
				{
					NodeID priorNodeId = priorNode?.id ?? NodeID.INVALID;
					if (allowQueuedResumeRepair && resumeOrTurnStartState && priorNodeId.IsValid && priorNodeId != peepNodeId)
					{
						LogVehicleAuthority("turnstart-mobile-start-blocked", $"{vehicleId.id}:{priorNodeId}:{peepNodeId}:{priorSource}", $"turnstart-mobile-start-blocked vehicle={vehicleId.id} staleNode={priorNodeId} resolvedNode={peepNodeId} staleSource={priorSource}", dedupe: false);
					}
					node = peepNode;
					source = "peep-live";
					SetRecentFinalizedNode(vehicleId, peepNodeId);
					LogVehicleAuthority("path-redirect-resolved", $"{vehicleId.id}:{priorNodeId}:{peepNodeId}:{priorSource}", $"path-redirect-resolved vehicle={vehicleId.id} priorNode={priorNodeId} resolvedNode={peepNodeId} priorSource={priorSource} resolvedSource=peep-live", dedupe: false);
					if (allowQueuedResumeRepair && resumeOrTurnStartState)
					{
						LogVehicleAuthority("resume-start-rebased", $"{vehicleId.id}:{priorNodeId}:{peepNodeId}:{priorSource}", $"resume-start-rebased vehicle={vehicleId.id} staleNode={priorNodeId} resolvedNode={peepNodeId} staleSource={priorSource} resolvedSource=peep-live", dedupe: false);
					}
					return true;
				}
			}

			return node != null;
		}

		internal static bool TryFindLiveHumanCrewAtNode(NodeID targetNodeId, out CrewAssignment matchingCrew)
		{
			return TryFindHumanCrewAtNode(targetNodeId, CrewNodeResolutionMode.LiveAuthoritative, out matchingCrew, out _);
		}

		public static EntityID GetDriverPeepId(PlayerCrew crew, EntityID vehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
				return EntityID.INVALID;

			long vkey = (long)vehicleId.id;
			if (DriverMap.TryGetValue(vkey, out long rawPeep))
			{
				EntityID mappedPeep = EntityID.FromID((ulong)rawPeep);
				foreach (CrewAssignment c in crew.AllCrew)
				{
					if (c.peepId == mappedPeep && c.VehicleID == vehicleId && IsActiveVehicleOccupant(crew, c))
						return mappedPeep;
				}
				DriverMap.Remove(vkey);
			}

			// Fallback when no active mapped driver exists: first active crew in vehicle becomes driver.
			foreach (CrewAssignment c in crew.AllCrew)
			{
				if (c.VehicleID == vehicleId && IsActiveVehicleOccupant(crew, c))
				{
					SetDriverInternal(vehicleId, c.peepId);
					return c.peepId;
				}
			}

			if (DriverMap.ContainsKey(vkey))
				DriverMap.Remove(vkey);
			return EntityID.INVALID;
		}

		internal static void HandleVehicleOccupantDeath(Entity deadPeep, string source)
		{
			ApplyVehicleOccupantDeathSnapshot(CaptureVehicleOccupantDeathSnapshot(deadPeep, source));
		}

		public static bool IsDriver(PlayerCrew crew, CrewAssignment assignment)
		{
			if (crew == null || !assignment.IsInVehicle)
				return false;
			EntityID driverId = GetDriverPeepId(crew, assignment.VehicleID);
			return driverId.IsValid && driverId == assignment.peepId;
		}

		public static List<CrewAssignment> GetPassengers(PlayerCrew crew, EntityID vehicleId)
		{
			var passengers = new List<CrewAssignment>();
			if (crew == null || !vehicleId.IsValid)
				return passengers;

			EntityID driverId = GetDriverPeepId(crew, vehicleId);
			foreach (CrewAssignment c in crew.AllCrew)
			{
				if (c.VehicleID == vehicleId && c.peepId != driverId && IsActiveVehicleOccupant(crew, c))
					passengers.Add(c);
			}
			return passengers;
		}

		private static bool HasTraitLocal(Entity peep, string traitId)
		{
			if (peep?.data?.person?.traitIds == null || string.IsNullOrEmpty(traitId))
			{
				return false;
			}
			try
			{
				foreach (Label trait in peep.data.person.traitIds)
				{
					if (string.Equals(trait.ToString(), traitId, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		internal static bool ShouldRunAiHireNormalization(PlayerInfo player, Entity peep, bool isBoss)
		{
			if (player == null || player.PID.IsHumanPlayer || player.IsJustCop || player.IsCopOrFed || player.crew == null || player.crew.IsCrewDefeated || isBoss)
			{
				return false;
			}
			if (peep == null || !peep.Id.IsValid)
			{
				return false;
			}
			return global::Game.Game.ctx?.IsInteractive == true;
		}

		private static bool HasAvailableSeatInAiVehicleFleet(PlayerInfo player, PlayerCrew crew)
		{
			if (player == null || crew == null)
			{
				return false;
			}

			EntityID bossPeepId = GetAiBossPeepId(player);
			EntityID bossVehicleId = bossPeepId.IsValid
				? crew.GetCrewForPeep(bossPeepId).VehicleID
				: EntityID.INVALID;

			List<Entity> vehicles = crew.AllVehicles
				.Where(item => item.IsValid)
				.Select(item => item.FindEntity())
				.Where(vehicle => IsUsableAiVehicle(vehicle, player.PID))
				.ToList();
			int seatingCrewCount = GetAiVehicleSeatingCrewCount(crew);
			Dictionary<long, int> targetOccupancyByVehicleId = BuildAiVehicleTargetOccupancyByVehicleId(crew, vehicles, bossVehicleId, seatingCrewCount);
			return vehicles.Any(vehicle => HasVehicleTargetSeatAvailable(crew, vehicle.Id, targetOccupancyByVehicleId));
		}

		private static bool TryAssignAiHireToPreferredAvailableSeat(PlayerInfo player, PlayerCrew crew, CrewAssignment currentAssignment)
		{
			if (player == null || crew == null || !currentAssignment.IsValid || !currentAssignment.peepId.IsValid)
			{
				return false;
			}

			EntityID bossPeepId = GetAiBossPeepId(player);
			EntityID bossVehicleId = bossPeepId.IsValid
				? crew.GetCrewForPeep(bossPeepId).VehicleID
				: EntityID.INVALID;

			List<Entity> vehicles = crew.AllVehicles
				.Where(item => item.IsValid)
				.Select(item => item.FindEntity())
				.Where(vehicle => IsUsableAiVehicle(vehicle, player.PID))
				.OrderBy(vehicle => vehicle.Id == bossVehicleId ? 0 : 1)
				.ThenByDescending(vehicle => GetVehicleCrewCount(crew, vehicle.Id) > 0 ? 1 : 0)
				.ThenByDescending(IsDeliveryTradeVehicle)
				.ThenBy(vehicle => vehicle.Id.id)
				.ToList();
			int seatingCrewCount = GetAiVehicleSeatingCrewCount(crew);
			Dictionary<long, int> targetOccupancyByVehicleId = BuildAiVehicleTargetOccupancyByVehicleId(crew, vehicles, bossVehicleId, seatingCrewCount);

			foreach (Entity vehicle in vehicles)
			{
				if (!HasVehicleTargetSeatAvailable(crew, vehicle.Id, targetOccupancyByVehicleId))
				{
					continue;
				}
				if (TryAssignAiCrewToVehicleAtomic(player, crew, currentAssignment, vehicle.Id))
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-hire-seat-reuse gang={player.PID.id} peep={currentAssignment.peepId.id} vehicle={vehicle.Id.id}");
					return true;
				}
			}

			return false;
		}

		internal static bool TryStabilizeAiVehicleState(PlayerInfo player, EntityID vehicleId, string source)
		{
			if (player == null || player.PID.IsHumanPlayer || player.crew == null || !vehicleId.IsValid)
			{
				return false;
			}

			PlayerCrew crew = player.crew;
			List<CrewAssignment> occupants = GetAllCrewInVehicle(crew, vehicleId)
				.Where(item => item.IsValid && item.peepId.IsValid && item.IsNotDead && crew.IsOnBoard(item.peepId))
				.OrderBy(item => item.peepId.id)
				.ToList();
			if (occupants.Count <= 0)
			{
				DriverMap.Remove((long)vehicleId.id);
				RefreshEnemyVehicleRepresentative(crew, vehicleId, source);
				return false;
			}

			EntityID currentDriverId = DriverMap.TryGetValue((long)vehicleId.id, out long rawDriverId)
				? EntityID.FromID((ulong)rawDriverId)
				: EntityID.INVALID;
			if (!currentDriverId.IsValid || occupants.All(item => item.peepId != currentDriverId))
			{
				CrewAssignment newDriver = occupants
					.OrderBy(item => item.peepId == GetAiBossPeepId(player) ? 0 : 1)
					.ThenByDescending(GetAiCrewStrengthScore)
					.ThenBy(item => item.peepId.id)
					.FirstOrDefault();
				if (newDriver.IsValid)
				{
					SetDriverInternal(vehicleId, newDriver.peepId);
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-driver-stabilized gang={player.PID.id} vehicle={vehicleId.id} driver={newDriver.peepId.id} source={source}");
				}
			}

			bool synced = TrySyncVehicleOccupantsToVehicleNode(crew, vehicleId, source);
			RefreshEnemyVehicleRepresentative(crew, vehicleId, source);
			return synced;
		}

		internal static bool TryNormalizeAiCrewAfterHire(PlayerInfo player, EntityID hiredPeepId, Node fallbackNode, bool allowVehicleCreateFallback, bool preserveCurrentVehicle, string source)
		{
			if (player == null || player.PID.IsHumanPlayer || player.crew == null || !hiredPeepId.IsValid || IsAiHireNormalizationActive())
			{
				return false;
			}

			Entity hiredPeep = hiredPeepId.FindEntity();
			if (!ShouldRunAiHireNormalization(player, hiredPeep, isBoss: false))
			{
				return false;
			}

			PlayerCrew crew = player.crew;
			try
			{
				_aiHireNormalizationDepth++;

				CrewAssignment currentAssignment = crew.GetCrewForPeep(hiredPeepId);
				if (!currentAssignment.IsValid || !currentAssignment.peepId.IsValid || !currentAssignment.IsNotDead)
				{
					return false;
				}

				if (preserveCurrentVehicle && currentAssignment.IsInVehicle && currentAssignment.VehicleID.IsValid)
				{
					return TryStabilizeAiVehicleState(player, currentAssignment.VehicleID, source);
				}

				if (!currentAssignment.IsInVehicle && HasAvailableSeatInAiVehicleFleet(player, crew))
				{
					TryApplyAiVehicleFillPolicy(player);
					currentAssignment = crew.GetCrewForPeep(hiredPeepId);
				}

				if ((!currentAssignment.IsInVehicle || !currentAssignment.VehicleID.IsValid)
					&& currentAssignment.IsValid
					&& currentAssignment.peepId.IsValid
					&& TryAssignAiHireToPreferredAvailableSeat(player, crew, currentAssignment))
				{
					currentAssignment = crew.GetCrewForPeep(hiredPeepId);
				}

				if (currentAssignment.IsInVehicle && currentAssignment.VehicleID.IsValid)
				{
					TryStabilizeAiVehicleState(player, currentAssignment.VehicleID, source);
					return true;
				}

				if (!allowVehicleCreateFallback)
				{
					return false;
				}

				Node spawnNode = fallbackNode ?? player.territory?.GetHeadquartersNode();
				if (spawnNode == null || hiredPeep == null)
				{
					return false;
				}

				Entity createdVehicle = crew.CreateVehicleAndAssignCrew(spawnNode, hiredPeep, isBoss: false);
				if (createdVehicle?.Id.IsValid != true)
				{
					return false;
				}

				TryStabilizeAiVehicleState(player, createdVehicle.Id, source + "-fallback");
				return true;
			}
			finally
			{
				_aiHireNormalizationDepth--;
			}
		}

		internal static bool TryHandleAiVehicleHireIntercept(PlayerCrew crew, Node node, Entity peep, Entity introducer, bool isBoss)
		{
			PlayerInfo player = crew != null ? crew.PID.FindPlayer() : null;
			if (!ShouldRunAiHireNormalization(player, peep, isBoss) || !HasAvailableSeatInAiVehicleFleet(player, crew))
			{
				return false;
			}

			crew.AddToCrewUnassigned(peep, introducer, isBoss: false);
			bool assigned = TryNormalizeAiCrewAfterHire(player, peep.Id, node, allowVehicleCreateFallback: true, preserveCurrentVehicle: false, source: "ai-hire-intercept");
			CrewAssignment finalAssignment = crew.GetCrewForPeep(peep.Id);
			if (finalAssignment.IsValid && finalAssignment.IsInVehicle && finalAssignment.VehicleID.IsValid)
			{
				TryStabilizeAiVehicleState(player, finalAssignment.VehicleID, "ai-hire-intercept-final");
				assigned = true;
			}

			if (assigned)
			{
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-hire-intercept gang={player.PID.id} peep={peep.Id.id} node={node?.id ?? NodeID.INVALID}");
			}
			else
			{
				Debug.LogWarning($"[GameplayTweaks] AI hire intercept could not place peep={peep.Id.id} gang={player.PID.id}; leaving crew unassigned.");
			}

			return true;
		}

		private static EntityID GetAiBossPeepId(PlayerInfo player)
		{
			if (player?.crew == null || player.crew.IsCrewDefeated)
			{
				return EntityID.INVALID;
			}
			try
			{
				if (player.social != null && !player.social.PlayerPeepId.IsNotValid)
				{
					return player.social.PlayerPeepId;
				}
			}
			catch
			{
			}
			try
			{
				CrewAssignment boss = player.crew.GetCrewForIndex(0);
				if (boss.IsValid && boss.peepId.IsValid)
				{
					return boss.peepId;
				}
			}
			catch
			{
			}
			return EntityID.INVALID;
		}

		private static bool IsDeliveryTradeVehicle(Entity vehicle)
		{
			string template = vehicle?.config?.Template.String ?? string.Empty;
			return template.IndexOf("delivery", StringComparison.OrdinalIgnoreCase) >= 0
				|| template.IndexOf("truck", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsUsableAiVehicle(Entity vehicle, PlayerID ownerPid)
		{
			if (vehicle == null || !vehicle.Id.IsValid || vehicle.components?.mobile == null || vehicle.data?.mobile == null)
			{
				return false;
			}
			if (vehicle.data.mobile.pid != ownerPid)
			{
				return false;
			}
			return !IsAmbientTrafficVehicle(vehicle.Id);
		}

		private static int GetAiVehicleSeatingCrewCount(PlayerCrew crew)
		{
			if (crew == null)
			{
				return 0;
			}

			return GetAiCrewInHireOrder(crew).Count;
		}

		private static int GetAiVehicleTargetOccupancy(Entity vehicle, bool isBossVehicle, int seatingCrewCount)
		{
			int slots = Math.Max(1, GetVehicleCrewSlots(vehicle));
			int defaultOccupancy = Math.Min(slots, seatingCrewCount >= 12 ? 3 : 2);
			if (!isBossVehicle)
			{
				return defaultOccupancy;
			}

			int bossOccupancy = seatingCrewCount >= 12
				? 4
				: (seatingCrewCount >= 8 ? 3 : 2);
			return Math.Min(slots, bossOccupancy);
		}

		private static Dictionary<long, int> BuildAiVehicleTargetOccupancyByVehicleId(PlayerCrew crew, List<Entity> vehicles, EntityID bossVehicleId, int seatingCrewCount)
		{
			Dictionary<long, int> result = new Dictionary<long, int>();
			if (crew == null || vehicles == null)
			{
				return result;
			}

			foreach (Entity vehicle in vehicles)
			{
				if (vehicle == null || !vehicle.Id.IsValid)
				{
					continue;
				}
				result[(long)vehicle.Id.id] = GetAiVehicleTargetOccupancy(vehicle, vehicle.Id == bossVehicleId, seatingCrewCount);
			}
			return result;
		}

		private static int GetVehicleTargetOccupancy(EntityID vehicleId, Dictionary<long, int> targetOccupancyByVehicleId)
		{
			if (vehicleId.IsValid && targetOccupancyByVehicleId != null && targetOccupancyByVehicleId.TryGetValue((long)vehicleId.id, out int targetOccupancy))
			{
				return Math.Max(1, targetOccupancy);
			}
			return Math.Max(1, GetVehicleCrewSlots(vehicleId));
		}

		private static bool HasVehicleTargetSeatAvailable(PlayerCrew crew, EntityID vehicleId, Dictionary<long, int> targetOccupancyByVehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return false;
			}

			return GetVehicleCrewCount(crew, vehicleId) < GetVehicleTargetOccupancy(vehicleId, targetOccupancyByVehicleId);
		}

		private static EntityID ChooseAiReserveVehicle(PlayerCrew crew, List<Entity> vehicles, EntityID bossVehicleId)
		{
			if (crew == null || vehicles == null || vehicles.Count < 2)
			{
				return EntityID.INVALID;
			}
			return vehicles
				.Where(vehicle => vehicle != null && vehicle.Id.IsValid && vehicle.Id != bossVehicleId)
				.OrderByDescending(IsDeliveryTradeVehicle)
				.ThenBy(vehicle => GetVehicleCrewCount(crew, vehicle.Id))
				.ThenBy(vehicle => vehicle.Id.id)
				.Select(vehicle => vehicle.Id)
				.FirstOrDefault();
		}

		private static List<CrewAssignment> GetAiCrewInHireOrder(PlayerCrew crew)
		{
			var ordered = new List<CrewAssignment>();
			if (crew == null)
			{
				return ordered;
			}
			foreach (CrewAssignment item in crew.AllCrew)
			{
				if (!item.IsValid || !item.peepId.IsValid || !item.IsNotDead || !crew.IsOnBoard(item.peepId) || GameplayTweaksPlugin.IsCrewCurrentlyJailed(item.peepId))
				{
					continue;
				}
				ordered.Add(item);
			}
			return ordered;
		}

		private static float GetAiCrewStrengthScore(CrewAssignment assignment)
		{
			if (!assignment.IsValid || !assignment.peepId.IsValid)
			{
				return float.MinValue;
			}

			float score = 0f;
			try
			{
				CrewModState state = GameplayTweaksPlugin.GetCrewStateOrNull(assignment.peepId);
				if (state != null)
				{
					score += Math.Max(0, state.StreetCreditLevel) * 1000f;
					score += Mathf.Clamp01(state.StreetCreditProgress) * 100f;
				}
			}
			catch
			{
			}

			try
			{
				Entity peep = assignment.GetPeep();
				if (peep?.data?.agent?.crewHistoryStats != null)
				{
					if (peep.data.agent.crewHistoryStats.TryGetValue(CrewStats.PeepsKilled, out int kills))
					{
						score += Math.Max(0, kills) * 25f;
					}
					if (peep.data.agent.crewHistoryStats.TryGetValue(CrewStats.TimesFought, out int fights))
					{
						score += Math.Max(0, fights) * 5f;
					}
				}
			}
			catch
			{
			}

			return score;
		}

		private static List<CrewAssignment> GetAiCrewByPassengerPriority(PlayerInfo player, PlayerCrew crew, List<CrewAssignment> orderedCrew, EntityID bossPeepId)
		{
			if (crew == null || orderedCrew == null || orderedCrew.Count <= 0)
			{
				return new List<CrewAssignment>();
			}

			Dictionary<long, int> stableOrder = orderedCrew
				.Where(item => item.IsValid && item.peepId.IsValid)
				.Select((item, index) => new { item, index })
				.ToDictionary(item => (long)item.item.peepId.id, item => item.index);

			return orderedCrew
				.Where(item => item.IsValid && item.peepId.IsValid)
				.OrderBy(item => item.peepId == bossPeepId ? 0 : 1)
				.ThenByDescending(GetAiCrewStrengthScore)
				.ThenBy(item => stableOrder.TryGetValue((long)item.peepId.id, out int index) ? index : int.MaxValue)
				.ThenBy(item => item.peepId.id)
				.ToList();
		}

		private static bool HasVehicleSeatAvailable(PlayerCrew crew, EntityID vehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return false;
			}

			return GetVehicleCrewCount(crew, vehicleId) < Math.Max(1, GetVehicleCrewSlots(vehicleId));
		}

		private static bool TryRelocateAiVehicleBlocker(PlayerInfo player, PlayerCrew crew, List<Entity> vehicles, EntityID blockedVehicleId, Dictionary<EntityID, EntityID> desiredAssignments, Dictionary<long, int> stableOrder, Dictionary<long, int> targetOccupancyByVehicleId)
		{
			if (player == null || crew == null || vehicles == null || !blockedVehicleId.IsValid)
			{
				return false;
			}

			List<CrewAssignment> blockers = GetAllCrewInVehicle(crew, blockedVehicleId)
				.Where(item => item.IsValid && item.peepId.IsValid && item.IsNotDead && crew.IsOnBoard(item.peepId))
				.Where(item => !desiredAssignments.TryGetValue(item.peepId, out EntityID desiredVehicleId) || desiredVehicleId != blockedVehicleId)
				.OrderBy(item => item.peepId == GetAiBossPeepId(player) ? 0 : 1)
				.ThenBy(GetAiCrewStrengthScore)
				.ThenBy(item => stableOrder.TryGetValue((long)item.peepId.id, out int index) ? index : int.MaxValue)
				.ThenBy(item => item.peepId.id)
				.ToList();
			foreach (CrewAssignment blocker in blockers)
			{
				EntityID preferredVehicleId = desiredAssignments.TryGetValue(blocker.peepId, out EntityID desiredVehicleId) ? desiredVehicleId : EntityID.INVALID;
				if (preferredVehicleId.IsValid
					&& preferredVehicleId != blockedVehicleId
					&& HasVehicleTargetSeatAvailable(crew, preferredVehicleId, targetOccupancyByVehicleId)
					&& TryAssignAiCrewToVehicleAtomic(player, crew, crew.GetCrewForPeep(blocker.peepId), preferredVehicleId))
				{
					return true;
				}

				foreach (Entity vehicle in vehicles)
				{
					if (vehicle == null || !vehicle.Id.IsValid || vehicle.Id == blockedVehicleId || vehicle.Id == preferredVehicleId)
					{
						continue;
					}
					if (!HasVehicleTargetSeatAvailable(crew, vehicle.Id, targetOccupancyByVehicleId))
					{
						continue;
					}
					if (TryAssignAiCrewToVehicleAtomic(player, crew, crew.GetCrewForPeep(blocker.peepId), vehicle.Id))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static void FinalizeAiVehicleDrivers(PlayerInfo player, PlayerCrew crew, List<Entity> vehicles, Dictionary<long, int> stableOrder)
		{
			if (player == null || crew == null || vehicles == null)
			{
				return;
			}

			foreach (Entity vehicle in vehicles)
			{
				if (vehicle == null || !vehicle.Id.IsValid)
				{
					continue;
				}

				List<CrewAssignment> occupants = GetAllCrewInVehicle(crew, vehicle.Id)
					.Where(item => item.IsValid && item.peepId.IsValid && item.IsNotDead && crew.IsOnBoard(item.peepId))
					.ToList();
				if (occupants.Count <= 0)
				{
					RefreshEnemyVehicleRepresentative(crew, vehicle.Id, "stabilize");
					continue;
				}

				EntityID currentDriverId = GetDriverPeepId(crew, vehicle.Id);
				if (currentDriverId.IsValid && occupants.Any(item => item.peepId == currentDriverId))
				{
					continue;
				}

				CrewAssignment newDriver = occupants
					.OrderByDescending(GetAiCrewStrengthScore)
					.ThenBy(item => stableOrder.TryGetValue((long)item.peepId.id, out int index) ? index : int.MaxValue)
					.ThenBy(item => item.peepId.id)
					.FirstOrDefault();
				if (newDriver.IsValid)
				{
					SetDriverInternal(vehicle.Id, newDriver.peepId);
					RefreshEnemyVehicleRepresentative(crew, vehicle.Id, "stabilize");
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-driver-stabilized gang={player.PID.id} vehicle={vehicle.Id.id} driver={newDriver.peepId.id} occupants={occupants.Count}");
				}
			}
		}

		internal static void TryApplyAiVehicleFillPolicy(PlayerInfo player)
		{
			try
			{
				if (player == null || player.PID.IsHumanPlayer || player.IsJustCop || player.IsCopOrFed || player.crew == null || player.crew.IsCrewDefeated)
				{
					return;
				}

				PlayerCrew crew = player.crew;
				List<CrewAssignment> orderedCrew = GetAiCrewInHireOrder(crew);
				List<Entity> vehicles = crew.AllVehicles
					.Where(item => item.IsValid)
					.Select(item => item.FindEntity())
					.Where(item => IsUsableAiVehicle(item, player.PID))
					.GroupBy(item => (long)item.Id.id)
					.Select(group => group.First())
					.OrderBy(vehicle => vehicle.Id.id)
					.ToList();
				if (orderedCrew.Count <= 0 || vehicles.Count <= 0)
				{
					return;
				}

				EntityID bossPeepId = GetAiBossPeepId(player);
				Dictionary<long, int> stableOrder = orderedCrew
					.Where(item => item.IsValid && item.peepId.IsValid)
					.Select((item, index) => new { item, index })
					.ToDictionary(item => (long)item.item.peepId.id, item => item.index);
				List<CrewAssignment> prioritizedCrew = GetAiCrewByPassengerPriority(player, crew, orderedCrew, bossPeepId);
				CrewAssignment bossAssignment = bossPeepId.IsValid ? crew.GetCrewForPeep(bossPeepId) : CrewAssignment.EMPTY;
				EntityID bossVehicleId = bossAssignment.IsValid && bossAssignment.IsInVehicle && bossAssignment.VehicleID.IsValid
					? bossAssignment.VehicleID
					: vehicles.OrderByDescending(GetVehicleCrewSlots).ThenBy(vehicle => vehicle.Id.id).Select(vehicle => vehicle.Id).FirstOrDefault();
				Entity bossVehicle = vehicles.FirstOrDefault(vehicle => vehicle.Id == bossVehicleId);
				if (bossVehicle == null)
				{
					bossVehicle = vehicles.OrderByDescending(GetVehicleCrewSlots).ThenBy(vehicle => vehicle.Id.id).FirstOrDefault();
					bossVehicleId = bossVehicle?.Id ?? EntityID.INVALID;
				}
				if (bossVehicle == null || !bossVehicleId.IsValid)
				{
					return;
				}
				int seatingCrewCount = prioritizedCrew.Count;
				Dictionary<long, int> targetOccupancyByVehicleId = BuildAiVehicleTargetOccupancyByVehicleId(crew, vehicles, bossVehicleId, seatingCrewCount);
				int bossVehicleSlots = GetVehicleTargetOccupancy(bossVehicleId, targetOccupancyByVehicleId);
				var desiredAssignments = new Dictionary<EntityID, EntityID>();
				var desiredRoles = new Dictionary<EntityID, string>();
				var orderedNonBoss = prioritizedCrew.Where(item => item.peepId != bossPeepId).ToList();

				if (bossPeepId.IsValid)
				{
					desiredAssignments[bossPeepId] = bossVehicleId;
					desiredRoles[bossPeepId] = "boss";
				}

				int nextIndex = 0;
				int bossAssigned = desiredAssignments.Values.Count(item => item == bossVehicleId);
				while (nextIndex < orderedNonBoss.Count && bossAssigned < bossVehicleSlots)
				{
					CrewAssignment item = orderedNonBoss[nextIndex++];
					desiredAssignments[item.peepId] = bossVehicleId;
					desiredRoles[item.peepId] = "bosscar";
					bossAssigned++;
				}

				List<Entity> secondaryVehicles = vehicles
					.Where(vehicle => vehicle.Id != bossVehicleId)
					.OrderByDescending(vehicle => GetVehicleCrewCount(crew, vehicle.Id) > 0 ? 1 : 0)
					.ThenByDescending(IsDeliveryTradeVehicle)
					.ThenBy(vehicle => vehicle.Id.id)
					.ToList();
				foreach (Entity vehicle in secondaryVehicles)
				{
					int targetSlots = GetVehicleTargetOccupancy(vehicle.Id, targetOccupancyByVehicleId);
					int assignedToVehicle = desiredAssignments.Values.Count(item => item == vehicle.Id);
					while (nextIndex < orderedNonBoss.Count && assignedToVehicle < targetSlots)
					{
						CrewAssignment item2 = orderedNonBoss[nextIndex++];
						desiredAssignments[item2.peepId] = vehicle.Id;
						desiredRoles[item2.peepId] = "secondary";
						assignedToVehicle++;
					}
				}

				EntityID nextVehicleId = secondaryVehicles.Count > 0 ? secondaryVehicles[0].Id : EntityID.INVALID;
				int secondaryTarget = secondaryVehicles.Count > 0 ? GetVehicleTargetOccupancy(nextVehicleId, targetOccupancyByVehicleId) : 0;
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-vehicle-policy gang={player.PID.id} mode=crew-threshold seatingCrewCount={seatingCrewCount} bossVehicle={bossVehicleId.id} bossTarget={bossVehicleSlots} secondaryTarget={secondaryTarget} nextVehicle={nextVehicleId.id} vehicles={vehicles.Count}");

				HashSet<long> validVehicleIds = new HashSet<long>(vehicles.Where(vehicle => vehicle != null && vehicle.Id.IsValid).Select(vehicle => (long)vehicle.Id.id));
				bool progress = true;
				int pass = 0;
				while (progress && pass < Math.Max(4, prioritizedCrew.Count * 2))
				{
					progress = false;
					pass++;
					foreach (CrewAssignment assignment in prioritizedCrew)
					{
						EntityID desiredVehicleId = desiredAssignments.TryGetValue(assignment.peepId, out EntityID mappedVehicleId) ? mappedVehicleId : EntityID.INVALID;
						if (!desiredVehicleId.IsValid)
						{
							continue;
						}

						CrewAssignment currentAssignment = crew.GetCrewForPeep(assignment.peepId);
						if (!currentAssignment.IsValid || !currentAssignment.peepId.IsValid)
						{
							continue;
						}
						if (currentAssignment.IsInVehicle && currentAssignment.VehicleID == desiredVehicleId && IsAiVehicleAssignmentUsable(crew, currentAssignment, validVehicleIds))
						{
							continue;
						}

						int guard = Math.Max(2, prioritizedCrew.Count);
						while (!HasVehicleTargetSeatAvailable(crew, desiredVehicleId, targetOccupancyByVehicleId) && guard-- > 0)
						{
							if (!TryRelocateAiVehicleBlocker(player, crew, vehicles, desiredVehicleId, desiredAssignments, stableOrder, targetOccupancyByVehicleId))
							{
								break;
							}
							progress = true;
						}

						if (!HasVehicleTargetSeatAvailable(crew, desiredVehicleId, targetOccupancyByVehicleId))
						{
							GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-vehicle-fill-blocked gang={player.PID.id} peep={assignment.peepId.id} desired={desiredVehicleId.id} current={currentAssignment.VehicleID.id}");
							continue;
						}
						if (!TryAssignAiCrewToVehicleAtomic(player, crew, currentAssignment, desiredVehicleId))
						{
							continue;
						}
						string role = desiredRoles.TryGetValue(assignment.peepId, out string mappedRole) ? mappedRole : "secondary";
						GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"ai-hire-assign gang={player.PID.id} peep={assignment.peepId.id} role={role} vehicle={desiredVehicleId.id} priority={GetAiCrewStrengthScore(assignment):0.0}");
						progress = true;
					}
				}

				FinalizeAiVehicleDrivers(player, crew, vehicles, stableOrder);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryApplyAiVehicleFillPolicy: " + ex.Message);
			}
		}

		internal static void SetDriverForVehicle(PlayerCrew crew, EntityID vehicleId, EntityID peepId)
		{
			if (crew == null || !crew.PID.IsHumanPlayer || !vehicleId.IsValid || !peepId.IsValid)
				return;
			EntityID priorDriverPeepId = GetDriverPeepId(crew, vehicleId);
			SetDriverInternal(vehicleId, peepId);
			TransferHumanDriverMovePointsAfterSwitch(crew, priorDriverPeepId, peepId);
			RefreshHumanDriverSelectionState(crew, vehicleId, peepId, "set-driver", priorDriverPeepId);
		}

		internal static void BeginStartupFounderVehicleAssignment(PlayerCrew crew, EntityID peepId)
		{
			if (crew == null || !peepId.IsValid || !IsFreshHumanStartup(crew.PID))
				return;
			StartupFounderAssignmentByPlayerId[GetPlayerKey(crew.PID)] = (long)peepId.id;
		}

		internal static void EndStartupFounderVehicleAssignment(PlayerCrew crew)
		{
			if (crew == null)
				return;
			StartupFounderAssignmentByPlayerId.Remove(GetPlayerKey(crew.PID));
		}

		internal static bool IsStartupFounderVehicleAssignment(PlayerCrew crew, EntityID peepId)
		{
			if (crew == null || !peepId.IsValid)
				return false;
			return StartupFounderAssignmentByPlayerId.TryGetValue(GetPlayerKey(crew.PID), out long mappedPeepId)
				&& mappedPeepId == (long)peepId.id;
		}

		internal static void RecordStartupCanonicalVehicle(PlayerCrew crew, Entity peep, Entity vehicle)
		{
			if (crew == null || peep == null || vehicle == null || !IsFreshHumanStartup(crew.PID))
				return;
			if (!peep.Id.IsValid || !vehicle.Id.IsValid)
				return;

			StartupCanonicalVehicleByPlayerId[GetPlayerKey(crew.PID)] = (long)vehicle.Id.id;
			SetDriverForVehicle(crew, vehicle.Id, peep.Id);
			if (!_loggedStartupCanonicalVehicleOnce)
			{
				_loggedStartupCanonicalVehicleOnce = true;
				Debug.Log($"[GameplayTweaks] Startup canonical vehicle captured pid={crew.PID.id} vehicle={vehicle.Id.id} template={vehicle.config?.Template}");
			}
		}

		internal static EntityID GetStartupCanonicalVehicleId(PlayerID pid)
		{
			long key = GetPlayerKey(pid);
			if (!StartupCanonicalVehicleByPlayerId.TryGetValue(key, out long rawVehicleId))
				return EntityID.INVALID;

			EntityID vehicleId = EntityID.FromID((ulong)rawVehicleId);
			if (vehicleId.IsValid && vehicleId.FindEntity() != null)
				return vehicleId;

			StartupCanonicalVehicleByPlayerId.Remove(key);
			return EntityID.INVALID;
		}

		internal static Entity GetStartupCanonicalVehicle(PlayerID pid)
		{
			EntityID vehicleId = GetStartupCanonicalVehicleId(pid);
			return vehicleId.IsValid ? vehicleId.FindEntity() : null;
		}

		internal static Entity GetFounderCurrentVehicle(PlayerInfo player)
		{
			if (player?.crew == null)
				return null;
			try
			{
				CrewAssignment founder = player.crew.GetCrewForPlayerPeep();
				return founder.IsValid ? founder.GetVehicle() : null;
			}
			catch
			{
				return null;
			}
		}

		internal static Entity ResolveStartupGrantVehicle(PlayerInfo player)
		{
			if (player == null)
				return null;

			Entity canonicalVehicle = GetStartupCanonicalVehicle(player.PID);
			if (canonicalVehicle != null)
				return canonicalVehicle;

			return GetFounderCurrentVehicle(player);
		}

		internal static bool EnsureFounderStartsInCanonicalVehicle(PlayerInfo player)
		{
			if (player?.crew == null || !IsFreshHumanStartup(player.PID))
				return false;

			try
			{
				CrewAssignment founder = player.crew.GetCrewForPlayerPeep();
				if (!founder.IsValid)
					return false;

				Entity founderPeep = founder.GetPeep();
				if (founderPeep == null)
					return false;

				Entity currentVehicle = founder.GetVehicle();
				if (currentVehicle != null)
				{
					RecordStartupCanonicalVehicle(player.crew, founderPeep, currentVehicle);
					SetDriverForVehicle(player.crew, currentVehicle.Id, founder.peepId);
					TrySyncCrewPeepToVehicleNode(currentVehicle.Id, founder.peepId);
					return true;
				}

				Entity canonicalVehicle = GetStartupCanonicalVehicle(player.PID);
				if (canonicalVehicle == null)
				{
					Node founderNode = null;
					if (!TryGetCrewCommandNode(founder, out founderNode) || founderNode == null)
					{
						NodeID founderNodeId = founderPeep.data?.agent?.nid ?? NodeID.INVALID;
						if (founderNodeId.IsValid)
							founderNode = founderNodeId.FindNode();
					}
					if (founderNode == null)
						founderNode = player.territory?.GetHeadquartersNode();
					if (founderNode == null)
						return false;

					BeginStartupFounderVehicleAssignment(player.crew, founder.peepId);
					try
					{
						canonicalVehicle = player.crew.CreateVehicleAndAssignCrew(founderNode, founderPeep, isBoss: true);
					}
					finally
					{
						EndStartupFounderVehicleAssignment(player.crew);
					}
				}
				else
				{
					BeginStartupFounderVehicleAssignment(player.crew, founder.peepId);
					try
					{
						player.crew.AssignCrewToVehicle(founder.peepId, canonicalVehicle.Id);
					}
					finally
					{
						EndStartupFounderVehicleAssignment(player.crew);
					}
				}

				Entity repairedVehicle = canonicalVehicle ?? GetFounderCurrentVehicle(player);
				if (repairedVehicle == null)
					return false;

				RecordStartupCanonicalVehicle(player.crew, founderPeep, repairedVehicle);
				SetDriverForVehicle(player.crew, repairedVehicle.Id, founder.peepId);
				TrySyncCrewPeepToVehicleNode(repairedVehicle.Id, founder.peepId);
				if (!_loggedStartupFounderRepairOnce)
				{
					_loggedStartupFounderRepairOnce = true;
					Debug.Log($"[GameplayTweaks] Startup founder vehicle repaired pid={player.PID.id} vehicle={repairedVehicle.Id.id} peep={founder.peepId.id}");
				}
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnsureFounderStartsInCanonicalVehicle: " + ex.Message);
				return false;
			}
		}

		private static void SetDriverInternal(EntityID vehicleId, EntityID peepId)
		{
			long vkey = (long)vehicleId.id;
			long pkey = (long)peepId.id;
			DriverMap[vkey] = pkey;
		}

		private static bool TryGetCrewData(PlayerCrew crew, out PlayerCrewData crewData)
		{
			crewData = null;
			if (crew == null || _playerCrewDataField == null)
			{
				return false;
			}

			try
			{
				crewData = _playerCrewDataField.GetValue(crew) as PlayerCrewData;
				return crewData != null;
			}
			catch
			{
				crewData = null;
				return false;
			}
		}

		internal static int RepairHumanVehicleAssignmentsFromSnapshot(PlayerCrew crew, EntityID vehicleId, IEnumerable<EntityID> occupantIds, EntityID arrestedPeepId, string source)
		{
			if (crew == null || !crew.PID.IsHumanPlayer || !vehicleId.IsValid || occupantIds == null || !TryGetCrewData(crew, out PlayerCrewData crewData))
			{
				return 0;
			}

			int repairedCount = 0;
			HashSet<ulong> seen = new HashSet<ulong>();
			foreach (EntityID occupantId in occupantIds)
			{
				if (!occupantId.IsValid || occupantId == arrestedPeepId || !seen.Add(occupantId.id))
				{
					continue;
				}
				if (!crew.IsOnBoard(occupantId) || IsCrewJailedForVehicleQueries(occupantId))
				{
					continue;
				}

				int index = crewData.FindPeepIndex(occupantId);
				if (index < 0)
				{
					continue;
				}

				CrewAssignment assignment = crewData.Get(index);
				if (!assignment.IsValid || !assignment.IsNotDead || assignment.VehicleID == vehicleId && assignment.IsInVehicle)
				{
					continue;
				}

				crewData.Set(index, assignment.SetVehicle(vehicleId));
				repairedCount++;
			}

			if (repairedCount > 0)
			{
				LogVehicleAuthority(
					"federal-arrest-inline-assignment-repaired",
					$"{vehicleId.id}:{arrestedPeepId.id}:{repairedCount}",
					$"federal-arrest-inline-assignment-repaired vehicle={vehicleId.id} arrestedPeep={arrestedPeepId.id} repairedAssignments={repairedCount} source={source}",
					dedupe: false);
			}

			return repairedCount;
		}

		internal static bool TryUnassignHumanVehicleCrewMetadataOnly(PlayerCrew crew, EntityID peepId, out EntityID vehicleId)
		{
			vehicleId = EntityID.INVALID;
			if (crew == null || !crew.PID.IsHumanPlayer || peepId.IsNotValid || !TryGetCrewData(crew, out PlayerCrewData crewData))
			{
				return false;
			}

			try
			{
				int index = crewData.FindPeepIndex(peepId);
				if (index < 0)
				{
					return false;
				}

				CrewAssignment assignment = crewData.Get(index);
				if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					return false;
				}

				PlayerInfo player = crew.PID.FindPlayer();
				player?.automation?.TryClearAutomationCrew(assignment);
				vehicleId = assignment.VehicleID;
				crewData.Set(index, assignment.SetNotAssigned());
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryUnassignHumanVehicleCrewMetadataOnly failed: " + ex.Message);
				vehicleId = EntityID.INVALID;
				return false;
			}
		}

		internal static void RepairHumanVehicleOccupantsAfterArrest(PlayerCrew crew, EntityID vehicleId, EntityID arrestedPeepId, string source, bool syncOccupantsToNode = true, bool federalArrestLane = false)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return;
			}
			long vehicleKey = (long)vehicleId.id;
			EntityID priorDriverId = DriverMap.TryGetValue(vehicleKey, out long priorRawDriverId)
				? EntityID.FromID((ulong)priorRawDriverId)
				: EntityID.INVALID;
			if (arrestedPeepId.IsValid && DriverMap.TryGetValue(vehicleKey, out long rawDriverId) && rawDriverId == (long)arrestedPeepId.id)
			{
				DriverMap.Remove(vehicleKey);
			}
			EntityID activeDriverId = GetDriverPeepId(crew, vehicleId);
			int liveOccupants = GetLiveVehicleCrewCount(crew, vehicleId);
			if (activeDriverId.IsValid && activeDriverId != priorDriverId && activeDriverId != arrestedPeepId)
			{
				LogVehicleAuthority("vehicle-arrest-driver-reassigned", $"{vehicleId.id}:{arrestedPeepId.id}:{priorDriverId.id}:{activeDriverId.id}", $"vehicle-arrest-driver-reassigned vehicle={vehicleId.id} arrestedPeep={arrestedPeepId.id} priorDriver={priorDriverId.id} newDriver={activeDriverId.id} source={source}", dedupe: false);
			}
			LogVehicleAuthority("arrest-repair", $"{vehicleId.id}:{arrestedPeepId.id}:{activeDriverId.id}:{liveOccupants}", $"arrest-repair vehicle={vehicleId.id} arrestedPeep={arrestedPeepId.id} activeDriver={activeDriverId.id} liveOccupants={liveOccupants} source={source}", dedupe: false);
			if (federalArrestLane && liveOccupants > 0 && activeDriverId.IsValid)
			{
				LogVehicleAuthority("federal-arrest-driver-preserved", $"{vehicleId.id}:{arrestedPeepId.id}:{activeDriverId.id}:{liveOccupants}", $"federal-arrest-driver-preserved vehicle={vehicleId.id} arrestedPeep={arrestedPeepId.id} activeDriver={activeDriverId.id} liveOccupants={liveOccupants} source={source}", dedupe: false);
			}
			if (!syncOccupantsToNode)
			{
				if (federalArrestLane)
				{
					LogVehicleAuthority("federal-arrest-inline-sync-skipped", $"{vehicleId.id}:{arrestedPeepId.id}:{activeDriverId.id}:{liveOccupants}", $"federal-arrest-inline-sync-skipped vehicle={vehicleId.id} arrestedPeep={arrestedPeepId.id} activeDriver={activeDriverId.id} liveOccupants={liveOccupants} source={source}", dedupe: false);
				}
				return;
			}
			TrySyncVehicleOccupantsToVehicleNode(crew, vehicleId, source);
		}

		internal static void ResetHumanVehicleAuthorityAfterArrest(PlayerCrew crew, EntityID vehicleId, EntityID arrestedPeepId, string source, bool deferNodeSync = false, bool federalArrestLane = false)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return;
			}

			ClearInterruptExpectedStartNode(vehicleId, source);
			ClearPendingHumanVehicleTravel(vehicleId, source);
			GameplayTweaksPlugin.ClearSelectedVehicleUiFinalNode(vehicleId, source);
			ClearRecentFinalizedNode(vehicleId, source + "-arrest-authority-reset");
			int liveOccupants = GetLiveVehicleCrewCount(crew, vehicleId);
			if (liveOccupants > 0)
			{
				LogVehicleAuthority("vehicle-arrest-survivors-preserved", $"{vehicleId.id}:{arrestedPeepId.id}:{liveOccupants}", $"vehicle-arrest-survivors-preserved vehicle={vehicleId.id} arrestedPeep={arrestedPeepId.id} liveOccupants={liveOccupants} source={source}", dedupe: false);
			}
			RepairHumanVehicleOccupantsAfterArrest(crew, vehicleId, arrestedPeepId, source, !deferNodeSync, federalArrestLane);
		}

		internal static void HandleCrewDepartureFromGang(PlayerInfo player, EntityID peepId, EntityID priorVehicleId, string source)
		{
			if (player?.crew == null || !peepId.IsValid)
			{
				return;
			}

			try
			{
				PlayerCrew crew = player.crew;
				if (priorVehicleId.IsValid)
				{
					try
					{
						player.automation?.TryClearAutomationCrew(new CrewAssignment(peepId).SetVehicle(priorVehicleId));
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] HandleCrewDepartureFromGang automation cleanup: " + ex.Message);
					}
				}
				else if (player.PID.IsHumanPlayer)
				{
					GameplayTweaksPlugin.RefreshCrewHudUi(source + "-crew-departure", rebuildCards: true);
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"crew-departure gang={player.PID.id} peep={peepId.id} vehicle=0 liveOccupants=0 activeDriver=0 source={source}");
					return;
				}

				long vehicleKey = (long)priorVehicleId.id;
				if (DriverMap.TryGetValue(vehicleKey, out long rawDriverId) && rawDriverId == (long)peepId.id)
				{
					DriverMap.Remove(vehicleKey);
				}

				int liveOccupants = GetLiveVehicleCrewCount(crew, priorVehicleId);
				if (liveOccupants <= 0)
				{
					_vehicleSearchBlockedBySurvivors.Remove(vehicleKey);
					SetRecentEnemyVehicleDeathRepresentative(priorVehicleId, EntityID.INVALID);
					if (player.PID.IsHumanPlayer)
					{
						ClearInterruptExpectedStartNode(priorVehicleId, source);
						ClearPendingHumanVehicleTravel(priorVehicleId, source);
						ClearRecentFinalizedNode(priorVehicleId, source);
					}
				}
				else
				{
					TrySyncVehicleOccupantsToVehicleNode(crew, priorVehicleId, source);
				}

				EntityID activeDriverId = GetDriverPeepId(crew, priorVehicleId);
				RefreshEnemyVehicleRepresentative(crew, priorVehicleId, source);
				if (player.PID.IsHumanPlayer)
				{
					TryRefreshCrewHudCardsForVehicle(priorVehicleId);
					GameplayTweaksPlugin.RefreshCrewHudUi(source + "-crew-departure", rebuildCards: true);
				}
				else
				{
					TryApplyAiVehicleFillPolicy(player);
				}

				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"crew-departure gang={player.PID.id} peep={peepId.id} vehicle={priorVehicleId.id} liveOccupants={liveOccupants} activeDriver={activeDriverId.id} source={source}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HandleCrewDepartureFromGang: " + ex.Message);
			}
		}

		internal static bool IsVehicleAtOwnedBuilding(Entity vehicle)
		{
			try
			{
				if (vehicle == null || vehicle.components.mobile == null)
					return false;

				PlayerInfo human = G.GetHumanPlayer();
				if (human == null || human.territory == null)
					return false;

				if (!TryGetAuthoritativeVehicleNodeId(vehicle, out NodeID vehicleNode, out _))
					return false;

				object territory = human.territory;
				if (territory == null)
					return false;

				// Safehouse is not in GetAllControlledBuildingsUnsafe; treat it as owned for crew assignment.
				try
				{
					var safehouseProp = territory.GetType().GetProperty("Safehouse", BindingFlags.Public | BindingFlags.Instance);
					if (safehouseProp != null)
					{
						EntityID safehouseId = (EntityID)safehouseProp.GetValue(territory);
						if (safehouseId.IsValid)
						{
							Entity sh = safehouseId.FindEntity();
							if (sh?.components?.board != null)
							{
								if (_getNodeIdMethod == null)
									_getNodeIdMethod = sh.components.board.GetType().GetMethod("GetNodeID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								if (_getNodeIdMethod != null)
								{
									NodeID safehouseNode = (NodeID)_getNodeIdMethod.Invoke(sh.components.board, null);
									if (safehouseNode == vehicleNode)
										return true;
								}
							}
						}
					}
				}
				catch { /* ignore */ }

				if (_getControlledBuildingsMethod == null)
				{
					_getControlledBuildingsMethod = territory.GetType().GetMethod("GetAllControlledBuildingsUnsafe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				if (_getControlledBuildingsMethod == null)
					return false;

				object controlledObj = _getControlledBuildingsMethod.Invoke(territory, null);
				List<EntityID> controlled = controlledObj as List<EntityID>;
				if (controlled == null)
					return false;

				foreach (EntityID buildingId in controlled)
				{
					Entity building = buildingId.FindEntity();
					if (building?.components.board == null || building.components.building == null)
						continue;

					object board = building.components.board;
					if (board == null)
						continue;

					if (_getNodeIdMethod == null)
					{
						_getNodeIdMethod = board.GetType().GetMethod("GetNodeID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					}
					if (_getNodeIdMethod == null)
						continue;

					NodeID bnode = (NodeID)_getNodeIdMethod.Invoke(board, null);
					if (bnode == vehicleNode)
						return true;
				}
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] IsVehicleAtOwnedBuilding failed: {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}

		internal static bool TryGetEntityBoardNodeId(Entity entity, out NodeID nodeId)
		{
			nodeId = entity?.data?.board?.bead.nodeId ?? NodeID.INVALID;
			if (nodeId.IsValid)
			{
				return true;
			}

			object board = entity?.components?.board;
			if (board == null)
			{
				return false;
			}

			try
			{
				MethodInfo getNodeId = board.GetType().GetMethod("GetNodeID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (getNodeId == null)
				{
					return false;
				}

				nodeId = (NodeID)getNodeId.Invoke(board, null);
				return nodeId.IsValid;
			}
			catch
			{
				nodeId = NodeID.INVALID;
				return false;
			}
		}

		internal static IEnumerable<EntityID> GetControlledBuildingIds(PlayerInfo player)
		{
			object territory = player?.territory;
			if (territory == null)
			{
				yield break;
			}

			MethodInfo getControlledBuildings = territory.GetType().GetMethod("GetAllControlledBuildingsUnsafe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object controlledObj = null;
			try
			{
				controlledObj = getControlledBuildings?.Invoke(territory, null);
			}
			catch
			{
				yield break;
			}

			if (controlledObj is IEnumerable<EntityID> controlledIds)
			{
				foreach (EntityID buildingId in controlledIds)
				{
					if (buildingId.IsValid)
					{
						yield return buildingId;
					}
				}
			}
		}

		internal static bool TrySyncVehicleEntityToNode(Entity vehicle, NodeID nodeId)
		{
			if (vehicle == null || !nodeId.IsValid || vehicle.components?.agent == null)
			{
				return false;
			}
			if (vehicle.Id.IsValid)
			{
				ClearVehicleNodeAuthorityCaches(vehicle.Id);
			}
			bool changed = false;
			NodeID currentNodeId = vehicle.data?.agent?.nid ?? NodeID.INVALID;
			if (currentNodeId != nodeId)
			{
				vehicle.components.agent.SetNodeID(nodeId);
				changed = true;
			}
			try
			{
				Node targetNode = nodeId.FindNode();
				BoardComponent board = vehicle.components?.board;
				if (board != null && targetNode != null)
				{
					NodeID liveBoardNodeId = vehicle.data?.board?.bead.nodeId ?? NodeID.INVALID;
					if (liveBoardNodeId != nodeId)
					{
						board.Move(targetNode.pos, vehicle.data?.mobile?.deg ?? 0f);
						changed = true;
					}
				}
				MobileComponent mobile = vehicle.components?.mobile;
				if (mobile != null && targetNode != null)
				{
					NodeID liveMobileNodeId = mobile.FindNodeNearThisMobile();
					if (liveMobileNodeId != nodeId)
					{
						mobile.Move(targetNode.pos, vehicle.data?.mobile?.deg ?? 0f);
						changed = true;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TrySyncVehicleEntityToNode mobile sync failed: " + ex.Message);
			}
			return changed;
		}

		internal static void ApplyPassengerMoveCap(PlayerCrew crew, CrewAssignment assignment)
		{
			try
			{
				if (crew == null || !crew.PID.IsHumanPlayer || !assignment.IsInVehicle)
					return;

				if (IsDriver(crew, assignment))
				{
					_humanPassengerUncappedMovesByPeepId.Remove((long)assignment.peepId.id);
					return;
				}

				Entity peep = assignment.GetPeep();
				if (peep == null || peep.data?.agent == null)
					return;

				int current = peep.data.agent.movesLeft;
				if (current > PassengerMoveCap)
				{
					long peepKey = (long)assignment.peepId.id;
					if (!_humanPassengerUncappedMovesByPeepId.TryGetValue(peepKey, out int remembered) || current > remembered)
					{
						_humanPassengerUncappedMovesByPeepId[peepKey] = current;
					}
					peep.data.agent.movesLeft = PassengerMoveCap;
					NotifyCrewActionsChanged(assignment.peepId);
				}
			}
			catch (TypeLoadException) { }
			catch (ReflectionTypeLoadException) { }
		}

		private static void TransferHumanDriverMovePointsAfterSwitch(PlayerCrew crew, EntityID priorDriverPeepId, EntityID newDriverPeepId)
		{
			try
			{
				if (crew == null || !crew.PID.IsHumanPlayer || !priorDriverPeepId.IsValid || !newDriverPeepId.IsValid || priorDriverPeepId == newDriverPeepId)
				{
					return;
				}

				Entity priorDriverPeep = priorDriverPeepId.FindEntity();
				Entity newDriverPeep = newDriverPeepId.FindEntity();
				if (priorDriverPeep?.data?.agent == null || newDriverPeep?.data?.agent == null)
				{
					return;
				}

				int restoredMovesLeft = Math.Max(0, newDriverPeep.data.agent.movesLeft);
				long newDriverKey = (long)newDriverPeepId.id;
				if (_humanPassengerUncappedMovesByPeepId.TryGetValue(newDriverKey, out int rememberedUncappedMoves))
				{
					restoredMovesLeft = Math.Max(restoredMovesLeft, rememberedUncappedMoves);
					_humanPassengerUncappedMovesByPeepId.Remove(newDriverKey);
				}
				else if (restoredMovesLeft <= PassengerMoveCap && newDriverPeep.components?.agent != null)
				{
					try
					{
						ValueTuple<int, int> perTurn = newDriverPeep.components.agent.GetMovesAndActionsPerTurn();
						restoredMovesLeft = Math.Max(restoredMovesLeft, perTurn.Item1);
					}
					catch
					{
					}
				}
				newDriverPeep.data.agent.movesLeft = restoredMovesLeft;

				if (priorDriverPeep.data.agent.movesLeft > PassengerMoveCap)
				{
					long priorDriverKey = (long)priorDriverPeepId.id;
					_humanPassengerUncappedMovesByPeepId[priorDriverKey] = priorDriverPeep.data.agent.movesLeft;
					priorDriverPeep.data.agent.movesLeft = PassengerMoveCap;
				}
				NotifyCrewActionsChanged(priorDriverPeepId);
				NotifyCrewActionsChanged(newDriverPeepId);
				LogVehicleAuthority(
					"driver-switch-mp",
					$"{priorDriverPeepId.id}:{newDriverPeepId.id}:{restoredMovesLeft}:{priorDriverPeep.data.agent.movesLeft}",
					$"driver-switch-mp priorDriver={priorDriverPeepId.id} priorMoves={priorDriverPeep.data.agent.movesLeft} newDriver={newDriverPeepId.id} newMoves={restoredMovesLeft}",
					dedupe: false);
			}
			catch (TypeLoadException) { }
			catch (ReflectionTypeLoadException) { }
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TransferHumanDriverMovePointsAfterSwitch: " + ex.Message);
			}
		}

		private static void NotifyCrewActionsChanged(EntityID peepId)
		{
			if (!peepId.IsValid)
			{
				return;
			}
			try
			{
				global::Game.Game.ctx?.events?.EnqueueOnce(new SessionEvent(SessionEventType.CrewActionsChanged, peepId, PlayerID.HumanPlayer, null));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] NotifyCrewActionsChanged: " + ex.Message);
			}
		}

		internal static bool TryBecomeDriverFromUi(EntityID peepId, EntityID vehicleId)
		{
			if (!peepId.IsValid || !vehicleId.IsValid)
				return false;

			PlayerCrew humanCrew;
			try
			{
				humanCrew = G.GetHumanCrew();
			}
			catch (TypeLoadException ex)
			{
				return TrySetDriverFailOpen(vehicleId, peepId, ex);
			}
			catch (ReflectionTypeLoadException ex)
			{
				return TrySetDriverFailOpen(vehicleId, peepId, ex);
			}
			catch (Exception ex)
			{
				if (!_loggedSetDriverUnexpectedFailureOnce)
				{
					_loggedSetDriverUnexpectedFailureOnce = true;
					Debug.LogWarning($"[GameplayTweaks] TryBecomeDriverFromUi failed while reading crew: {ex.GetType().Name}: {ex.Message}");
				}
				return false;
			}

			if (humanCrew == null)
				return false;

			EntityID priorDriverPeepId = GetDriverPeepId(humanCrew, vehicleId);

			try
			{
				CrewAssignment assignment = humanCrew.GetCrewForPeep(peepId);
				if (!assignment.IsInVehicle || assignment.VehicleID != vehicleId)
					return false;
			}
			catch (TypeLoadException ex)
			{
				return TrySetDriverFailOpen(vehicleId, peepId, ex);
			}
			catch (ReflectionTypeLoadException ex)
			{
				return TrySetDriverFailOpen(vehicleId, peepId, ex);
			}
			catch (Exception ex)
			{
				if (!_loggedSetDriverUnexpectedFailureOnce)
				{
					_loggedSetDriverUnexpectedFailureOnce = true;
					Debug.LogWarning($"[GameplayTweaks] TryBecomeDriverFromUi failed while validating assignment: {ex.GetType().Name}: {ex.Message}");
				}
				return false;
			}

			SetDriverInternal(vehicleId, peepId);
			TransferHumanDriverMovePointsAfterSwitch(humanCrew, priorDriverPeepId, peepId);
			RefreshHumanDriverSelectionState(humanCrew, vehicleId, peepId, "ui", priorDriverPeepId);
			return true;
		}

		private static bool TrySetDriverFailOpen(EntityID vehicleId, EntityID peepId, Exception ex)
		{
			if (!_loggedSetDriverFailOpenOnce)
			{
				_loggedSetDriverFailOpenOnce = true;
				Debug.LogWarning($"[GameplayTweaks] TryBecomeDriverFromUi type-load fallback active; setting driver without full validation. {ex.GetType().Name}: {ex.Message}");
			}
			if (!vehicleId.IsValid || !peepId.IsValid)
				return false;
			try
			{
				PlayerCrew humanCrew = G.GetHumanCrew();
				EntityID priorDriverPeepId = GetDriverPeepId(humanCrew, vehicleId);
				SetDriverInternal(vehicleId, peepId);
				TransferHumanDriverMovePointsAfterSwitch(humanCrew, priorDriverPeepId, peepId);
				RefreshHumanDriverSelectionState(humanCrew, vehicleId, peepId, "ui-failopen", priorDriverPeepId);
				return true;
			}
			catch (Exception inner)
			{
				if (!_loggedSetDriverUnexpectedFailureOnce)
				{
					_loggedSetDriverUnexpectedFailureOnce = true;
					Debug.LogWarning($"[GameplayTweaks] TryBecomeDriverFromUi fallback failed: {inner.GetType().Name}: {inner.Message}");
				}
				return false;
			}
		}

		internal static void ShowHudMessage(string message)
		{
			if (string.IsNullOrEmpty(message))
				return;
			try
			{
				object tickers = global::Game.Game.ctx?.hud?.tickers;
				if (tickers == null)
				{
					Debug.Log("[GameplayTweaks] " + message);
					return;
				}

				MethodInfo[] methods = tickers.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (MethodInfo method in methods)
				{
					if (!string.Equals(method.Name, "AddTextTicker", StringComparison.Ordinal))
						continue;
					ParameterInfo[] parameters = method.GetParameters();
					if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
					{
						method.Invoke(tickers, new object[] { message });
						return;
					}
					if (parameters.Length == 3 && parameters[2].ParameterType == typeof(string))
					{
						object icon = ResolveEnumValue(parameters[0].ParameterType, "VEHICLE_NEW");
						object title = ResolveEnumValue(parameters[1].ParameterType, "DEFAULT");
						method.Invoke(tickers, new[] { icon, title, message });
						return;
					}
				}
				Debug.Log("[GameplayTweaks] " + message);
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] ShowHudMessage type load fallback: " + ex.Message);
				Debug.Log("[GameplayTweaks] " + message);
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] ShowHudMessage reflection type load fallback: " + ex.Message);
				Debug.Log("[GameplayTweaks] " + message);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ShowHudMessage fallback: " + ex.Message);
				Debug.Log("[GameplayTweaks] " + message);
			}
		}

		private static object ResolveEnumValue(Type enumType, string preferredName)
		{
			if (enumType != null && enumType.IsEnum)
			{
				try
				{
					if (!string.IsNullOrEmpty(preferredName) && Enum.IsDefined(enumType, preferredName))
						return Enum.Parse(enumType, preferredName);
					Array values = Enum.GetValues(enumType);
					if (values != null && values.Length > 0)
						return values.GetValue(0);
				}
				catch
				{
				}
			}
			return null;
		}

		/// <summary>Calls game's TweenCameraToEntity; used from click handlers to avoid TypeLoadException in closures.</summary>
		internal static void TweenCameraToEntitySafe(Entity entity)
		{
			if (entity == null)
				return;
			try
			{
				PersonInfoUtil.TweenCameraToEntity(entity);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TweenCameraToEntity: " + ex.Message);
			}
		}

		internal static bool TryBecomeDriverFromCardContext(CrewCardContext ctx)
		{
			if (ctx?.data == null || ctx.data.type != CrewCardType.CrewMuscle)
				return false;

			CrewAssignment assignment = ctx.data.crew;
			return TryBecomeDriver(assignment, "card-context");
		}

		internal static bool TryBecomeDriver(CrewAssignment assignment, string sourceTag)
		{
			if (!assignment.IsInVehicle || !assignment.peepId.IsValid || !assignment.VehicleID.IsValid)
				return false;

			if (string.IsNullOrWhiteSpace(sourceTag))
				sourceTag = "direct";

			PlayerCrew humanCrew = null;
			EntityID priorDriverPeepId = EntityID.INVALID;
			EntityID founderPeepIdBefore = EntityID.INVALID;
			try
			{
				humanCrew = G.GetHumanCrew();
				if (humanCrew != null)
				{
					CrewAssignment latestAssignment = humanCrew.GetCrewForPeep(assignment.peepId);
					if (!latestAssignment.IsValid
						|| !latestAssignment.IsInVehicle
						|| latestAssignment.VehicleID != assignment.VehicleID
						|| !IsActiveVehicleOccupant(humanCrew, latestAssignment))
					{
						LogVehicleAuthority(
							"driver-switch-stale-assignment",
							$"{sourceTag}:{assignment.VehicleID.id}:{assignment.peepId.id}",
							$"driver-switch-stale-assignment source={sourceTag} vehicle={assignment.VehicleID.id} peep={assignment.peepId.id}",
							dedupe: false);
						return false;
					}

					assignment = latestAssignment;
					priorDriverPeepId = GetDriverPeepId(humanCrew, assignment.VehicleID);
					founderPeepIdBefore = humanCrew.GetCrewForPlayerPeep().peepId;
				}
			}
			catch
			{
			}

			SetDriverInternal(assignment.VehicleID, assignment.peepId);
			TransferHumanDriverMovePointsAfterSwitch(humanCrew, priorDriverPeepId, assignment.peepId);
			RefreshHumanDriverSelectionState(humanCrew, assignment.VehicleID, assignment.peepId, sourceTag, priorDriverPeepId);

			try
			{
				if (!TrySyncCrewPeepToVehicleNode(assignment.VehicleID, assignment.peepId))
					TrySyncCrewPeepToDriverNode(priorDriverPeepId, assignment.peepId);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryBecomeDriverFromCardContext sync: " + ex.Message);
			}
			TryGetAuthoritativeVehicleNodeId(assignment.VehicleID, out NodeID vehicleNodeId, out string vehicleNodeSource);
			NodeID priorDriverNodeId = priorDriverPeepId.FindEntity()?.data?.agent?.nid ?? NodeID.INVALID;
			NodeID newDriverNodeId = assignment.peepId.FindEntity()?.data?.agent?.nid ?? NodeID.INVALID;
			LogVehicleAuthority(
				"driver-switch-context",
				$"{sourceTag}:{assignment.VehicleID.id}:{priorDriverPeepId.id}:{assignment.peepId.id}:{vehicleNodeId}",
				$"driver-switch-context source={sourceTag} vehicle={assignment.VehicleID.id} priorDriver={priorDriverPeepId.id} newDriver={assignment.peepId.id} priorDriverNode={priorDriverNodeId} newDriverNode={newDriverNodeId} vehicleNode={vehicleNodeId} vehicleNodeSource={vehicleNodeSource}",
				dedupe: false);
			AssertFounderIdentityStable(humanCrew, founderPeepIdBefore, "TryBecomeDriver");
			if (!_loggedSetDriverCardSuccessOnce)
			{
				_loggedSetDriverCardSuccessOnce = true;
				Debug.Log($"[GameplayTweaks] Set driver card path success vehicle={assignment.VehicleID.id} peep={assignment.peepId.id}");
			}
			return true;
		}

		internal static bool TrySyncCrewPeepToDriverNode(EntityID sourcePeepId, EntityID targetPeepId)
		{
			if (!sourcePeepId.IsValid || !targetPeepId.IsValid)
				return false;
			try
			{
				Entity sourcePeep = sourcePeepId.FindEntity();
				NodeID nodeId = sourcePeep?.data?.agent?.nid ?? NodeID.INVALID;
				if (!nodeId.IsValid)
					return false;
				global::Game.Game.ctx?.transit?.SetAgentAtNode(nodeId, targetPeepId);
				if (!_loggedSetDriverNodeSyncOnce)
				{
					_loggedSetDriverNodeSyncOnce = true;
					Debug.Log($"[GameplayTweaks] Set driver node sync source-peep={sourcePeepId.id} target-peep={targetPeepId.id} node={nodeId}");
				}
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TrySyncCrewPeepToDriverNode: " + ex.Message);
				return false;
			}
		}

		internal static bool TrySyncCrewPeepToVehicleNode(EntityID vehicleId, EntityID peepId)
		{
			if (!vehicleId.IsValid || !peepId.IsValid)
				return false;
			try
			{
				if (!TryGetAuthoritativeVehicleNodeId(vehicleId, out NodeID nodeId, out string source))
					return false;
				global::Game.Game.ctx?.transit?.SetAgentAtNode(nodeId, peepId);
				if (!_loggedSetDriverNodeSyncOnce)
				{
					_loggedSetDriverNodeSyncOnce = true;
					Debug.Log($"[GameplayTweaks] Set driver node sync vehicle={vehicleId.id} peep={peepId.id} node={nodeId} source={source}");
				}
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TrySyncCrewPeepToVehicleNode: " + ex.Message);
				return false;
			}
		}

		internal static bool TrySyncVehicleOccupantsToVehicleNode(PlayerCrew crew, EntityID vehicleId)
		{
			return TrySyncVehicleOccupantsToVehicleNode(crew, vehicleId, "fallback");
		}

		internal static bool TrySyncVehicleOccupantsToNode(PlayerCrew crew, EntityID vehicleId, NodeID nodeId, string trigger, bool syncVehicle = true)
		{
			if (crew == null || !vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}

			Entity vehicle = vehicleId.FindEntity();
			int mismatchCount = 0;
			int occupantCount = 0;
			bool synced = false;
			if (syncVehicle && TrySyncVehicleEntityToNode(vehicle, nodeId))
			{
				mismatchCount++;
				synced = true;
			}

			foreach (CrewAssignment assignment in GetAllCrewInVehicle(crew, vehicleId))
			{
				if (!IsActiveVehicleOccupant(crew, assignment))
				{
					continue;
				}

				occupantCount++;
				Entity peep = assignment.GetPeep();
				NodeID peepNodeId = peep?.data?.agent?.nid ?? NodeID.INVALID;
				if (peepNodeId != nodeId)
				{
					mismatchCount++;
				}
				global::Game.Game.ctx?.transit?.SetAgentAtNode(nodeId, assignment.peepId);
				synced = true;
			}

			if (mismatchCount > 0)
			{
				LogVehicleAuthority("repair", $"{vehicleId.id}:{nodeId}:{trigger}", $"repair trigger={trigger} vehicle={vehicleId.id} node={nodeId} mismatches={mismatchCount} occupants={occupantCount} syncVehicle={syncVehicle}", dedupe: false);
			}

			return synced;
		}

		internal static bool TrySyncVehicleOccupantsToVehicleNode(PlayerCrew crew, EntityID vehicleId, string trigger)
		{
			if (crew == null || !vehicleId.IsValid)
				return false;
			if (string.Equals(trigger, "fallback", StringComparison.Ordinal)
				&& TryGetRecentFinalizedNodeId(vehicleId, out NodeID recentFinalizedNodeId)
				&& recentFinalizedNodeId.IsValid)
			{
				LogVehicleAuthority("fallback-skip", $"{vehicleId.id}:{recentFinalizedNodeId}", $"fallback-sync-skip vehicle={vehicleId.id} reason=recent-finalize node={recentFinalizedNodeId}");
				return false;
			}
			if (string.Equals(trigger, "fallback", StringComparison.Ordinal) && IsHumanVehicleTravelActive(vehicleId))
			{
				LogVehicleAuthority("fallback-suppress", $"{vehicleId.id}:{trigger}", $"fallback-suppressed-travel vehicle={vehicleId.id}");
				return false;
			}
			Entity vehicle = vehicleId.FindEntity();
			if (!TryGetAuthoritativeVehicleNodeId(vehicle, out NodeID nodeId, out string source))
			{
				nodeId = NodeID.INVALID;
				source = "none";
			}
			NodeID expectedNodeId = NodeID.INVALID;
			NodeID goalNodeId = NodeID.INVALID;
			NodeID startNodeId = NodeID.INVALID;
			if (string.Equals(trigger, "travel-end", StringComparison.Ordinal)
				&& TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.ExpectedNodeID.IsValid)
			{
				expectedNodeId = pendingState.ExpectedNodeID;
				goalNodeId = pendingState.GoalNodeID;
				startNodeId = pendingState.StartNodeID;
				bool hasObservedExpectedArrival = TryGetObservedHumanVehicleReachedNode(vehicleId, pendingState.ExpectedNodeID, Time.frameCount, out string observedExpectedSource);
				string expectedArrivalSource = hasObservedExpectedArrival
					? "observed-" + observedExpectedSource
					: "expected";
				if (!nodeId.IsValid)
				{
					nodeId = pendingState.ExpectedNodeID;
					source = expectedArrivalSource;
				}
				else if (nodeId != pendingState.ExpectedNodeID)
				{
					string overrideReason = hasObservedExpectedArrival ? "observed-arrival" : "expected-authority";
					LogVehicleAuthority(
						"travel-finalize-override",
						$"{vehicleId.id}:{pendingState.ExpectedNodeID}:{nodeId}",
						$"travel-finalize-override vehicle={vehicleId.id} expectedNode={pendingState.ExpectedNodeID} staleNode={nodeId} source={expectedArrivalSource} reason={overrideReason}");
					nodeId = pendingState.ExpectedNodeID;
					source = expectedArrivalSource;
				}
				else if (hasObservedExpectedArrival)
				{
					source = expectedArrivalSource;
				}
			}
			if (!nodeId.IsValid)
			{
				return false;
			}
			if (string.Equals(trigger, "travel-end", StringComparison.Ordinal))
			{
				LogVehicleAuthority("travel-finalize-source", $"{vehicleId.id}:{nodeId}:{source}", $"travel-finalize-source vehicle={vehicleId.id} finalNode={nodeId} source={source}", dedupe: false);
			}
			int mismatchCount = CountVehicleNodeMismatches(crew, vehicleId, vehicle, nodeId, out int occupantCount);
			bool synced = TrySyncVehicleOccupantsToNode(crew, vehicleId, nodeId, trigger, syncVehicle: true);
			if (string.Equals(trigger, "travel-end", StringComparison.Ordinal))
			{
				bool mismatch = expectedNodeId.IsValid && expectedNodeId != nodeId;
				if (expectedNodeId.IsValid && nodeId == expectedNodeId)
				{
					SetRecentFinalizedNode(vehicleId, expectedNodeId);
				}
				SetRecentFinalizedNode(vehicleId, nodeId);
				LogVehicleAuthority("travel-finalize", $"{vehicleId.id}:{nodeId}:{expectedNodeId}:{goalNodeId}", $"travel-segment-finalize vehicle={vehicleId.id} startNode={startNodeId} finalNode={nodeId} expectedNode={expectedNodeId} goalNode={goalNodeId} mismatch={mismatch} source={source}", dedupe: false);
				LogVehicleAuthority("travel-end", $"{vehicleId.id}:{nodeId}:{occupantCount}", $"travel-end-sync vehicle={vehicleId.id} node={nodeId} source={source} occupants={occupantCount} mismatches={mismatchCount}", dedupe: false);
				if (crew?.PID.IsHumanPlayer == true)
				{
					GameplayTweaksPlugin.TurnUpdatePatch.TryRunAiHumanRobberyContactScanNow("human-travel-finalize");
				}
			}
			else if (crew?.PID.IsAIPlayer == true && nodeId.IsValid)
			{
				SetRecentAiCommittedNode(vehicleId, nodeId, trigger);
			}
			return synced;
		}

		private static int CountVehicleNodeMismatches(PlayerCrew crew, EntityID vehicleId, Entity vehicle, NodeID nodeId, out int occupantCount)
		{
			occupantCount = 0;
			if (crew == null || !vehicleId.IsValid || !nodeId.IsValid)
			{
				return 0;
			}

			int mismatchCount = 0;
			NodeID vehicleNodeId = vehicle?.data?.agent?.nid ?? NodeID.INVALID;
			if (vehicleNodeId.IsValid && vehicleNodeId != nodeId)
			{
				mismatchCount++;
			}

			foreach (CrewAssignment assignment in GetAllCrewInVehicle(crew, vehicleId))
			{
				if (!IsActiveVehicleOccupant(crew, assignment))
				{
					continue;
				}

				occupantCount++;
				NodeID peepNodeId = assignment.GetPeep()?.data?.agent?.nid ?? NodeID.INVALID;
				if (peepNodeId != nodeId)
				{
					mismatchCount++;
				}
			}

			return mismatchCount;
		}

		internal static bool TryNormalizeHumanVehicleCommandDriver(CommandGoto command, PlayerCrew crew, out Entity peep, out CrewAssignment assignment, out EntityID originalPeepId)
		{
			peep = null;
			assignment = default;
			originalPeepId = command?.peepId ?? EntityID.INVALID;
			if (command == null || crew == null || !command.peepId.IsValid)
			{
				return false;
			}

			assignment = crew.GetCrewForPeep(command.peepId);
			peep = command.peepId.FindEntity();
			if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
			{
				return peep?.data?.agent != null;
			}

			if (!IsActiveVehicleOccupant(crew, assignment))
			{
				return false;
			}

			EntityID driverPeepId = GetDriverPeepId(crew, assignment.VehicleID);
			if (!driverPeepId.IsValid || driverPeepId == assignment.peepId)
			{
				return peep?.data?.agent != null;
			}

			CrewAssignment driverAssignment = crew.GetCrewForPeep(driverPeepId);
			Entity driverPeep = driverPeepId.FindEntity();
			if (!driverAssignment.IsValid
				|| !driverAssignment.IsInVehicle
				|| driverAssignment.VehicleID != assignment.VehicleID
				|| !IsActiveVehicleOccupant(crew, driverAssignment)
				|| driverPeep?.data?.agent == null)
			{
				return peep?.data?.agent != null;
			}

			command.peepId = driverPeepId;
			peep = driverPeep;
			assignment = driverAssignment;
			LogVehicleAuthority(
				"command-driver-remap",
				$"{assignment.VehicleID.id}:{originalPeepId.id}:{driverPeepId.id}",
				$"command-driver-remap vehicle={assignment.VehicleID.id} fromPeep={originalPeepId.id} toPeep={driverPeepId.id}",
				dedupe: false);
			return true;
		}

		internal static bool TryGetHumanVehicleCommandContext(CommandGoto command, out PlayerInfo player, out PlayerCrew crew, out Entity peep, out CrewAssignment assignment, out Entity vehicle)
		{
			player = null;
			crew = null;
			peep = null;
			assignment = default;
			vehicle = null;
			if (command == null || !command.pid.IsHumanPlayer)
			{
				return false;
			}
			player = command.pid.FindPlayer();
			crew = player?.crew;
			if (crew == null)
			{
				return false;
			}
			if (!TryNormalizeHumanVehicleCommandDriver(command, crew, out peep, out assignment, out _))
			{
				return false;
			}
			if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid || !IsActiveVehicleOccupant(crew, assignment))
			{
				return false;
			}
			vehicle = assignment.VehicleID.FindEntity();
			return vehicle?.data?.mobile != null;
		}

		internal static bool TryGetAiVehicleCommandContext(CommandGoto command, out PlayerInfo player, out PlayerCrew crew, out Entity peep, out CrewAssignment assignment, out Entity vehicle)
		{
			player = null;
			crew = null;
			peep = null;
			assignment = default;
			vehicle = null;
			if (command == null || !command.pid.IsAIPlayer)
			{
				return false;
			}
			player = command.pid.FindPlayer();
			crew = player?.crew;
			if (crew == null)
			{
				return false;
			}
			peep = command.peepId.FindEntity();
			if (peep?.data?.agent == null)
			{
				return false;
			}
			assignment = crew.GetCrewForPeep(command.peepId);
			if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
			{
				return false;
			}
			vehicle = assignment.VehicleID.FindEntity();
			return vehicle?.data?.mobile != null && vehicle.data.mobile.pid.IsAIPlayer;
		}

		internal static bool TryShouldSkipAiQueuedVehicleCommand(CommandGoto command, out EntityID vehicleId, out string reason)
		{
			vehicleId = EntityID.INVALID;
			reason = "none";
			if (command == null || !command.pid.IsAIPlayer)
			{
				return false;
			}

			PlayerCrew crew = command.pid.FindPlayer()?.crew;
			if (crew == null)
			{
				return false;
			}

			CrewAssignment assignment = crew.GetCrewForPeep(command.peepId);
			if ((!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid) && command.peepId.IsValid)
			{
				EntityID assignedVehicleId = crew.FindVehicleAssignedToPeep(command.peepId);
				if (assignedVehicleId.IsValid)
				{
					vehicleId = assignedVehicleId;
					reason = "assignment-missing";
					return true;
				}
				return false;
			}

			vehicleId = assignment.VehicleID;
			Entity peep = command.peepId.FindEntity();
			if (peep?.data?.agent == null)
			{
				reason = "peep-missing";
				return true;
			}
			try
			{
				if (peep.components?.agent == null || peep.components.agent.GetNode() == null)
				{
					reason = "peep-node-missing";
					return true;
				}
			}
			catch
			{
				reason = "peep-node-missing";
				return true;
			}
			if (!IsActiveVehicleOccupant(crew, assignment))
			{
				reason = "occupant-inactive";
				return true;
			}
			if (!TryGetEnemyVehicleDisplayState(crew, vehicleId, out EnemyVehicleDisplayState state)
				|| !state.HasLiveOccupants
				|| !state.RepresentativePeepId.IsValid)
			{
				reason = "no-live-representative";
				return true;
			}
			if (!state.DriverPeepId.IsValid || state.DriverPeepId != assignment.peepId)
			{
				reason = state.DriverPeepId.IsValid ? "non-driver-command" : "driver-missing";
				return true;
			}
			return false;
		}

		internal static bool TryBuildHumanVehicleCommandPath(CommandGoto command, out PathData path, out Node startNode, out string source)
		{
			long totalTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			long segmentTicks = totalTicks;
			long contextMs = 0L;
			long goalMs = 0L;
			long startResolveMs = 0L;
			long authorityMs = 0L;
			long pathfindMs = 0L;
			path = null;
			startNode = null;
			source = "none";
			if (!TryGetHumanVehicleCommandContext(command, out _, out _, out Entity peep, out CrewAssignment assignment, out _))
			{
				return false;
			}
			contextMs = GetElapsedMs(segmentTicks);
			segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			Node goalNode = command.goalID.FindNode();
			NodeID peepNodeId = peep.data?.agent?.nid ?? NodeID.INVALID;
			goalMs = GetElapsedMs(segmentTicks);
			segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			if (goalNode == null || !TryGetResolvedHumanVehiclePathStartNode(assignment.VehicleID, peepNodeId, out startNode, out source, allowQueuedResumeRepair: true) || startNode == null)
			{
				return false;
			}
			startResolveMs = GetElapsedMs(segmentTicks);
			segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			if (TryGetAuthoritativeVehicleNodeId(assignment.VehicleID, out NodeID authoritativeNodeId, out string authoritativeSource)
				&& authoritativeNodeId.IsValid
				&& authoritativeNodeId != startNode.id
				&& (string.Equals(source, "recent-finalize", StringComparison.Ordinal)
					|| string.Equals(source, "queued-arrival", StringComparison.Ordinal)))
			{
				LogVehicleAuthority("human-start-rebased", $"{assignment.VehicleID.id}:{startNode.id}:{authoritativeNodeId}:{source}:command-build", $"human-start-rebased vehicle={assignment.VehicleID.id} startNode={startNode.id} source={source} authoritativeNode={authoritativeNodeId} authoritativeSource={authoritativeSource} phase=command-build", dedupe: false);
			}
			authorityMs = GetElapsedMs(segmentTicks);
			segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			int movesRemaining = command.freeDrive ? int.MaxValue : peep.components.agent.MovesRemaining;
			if (movesRemaining <= 0)
			{
				return true;
			}
			SomaSim.Util.Fixnum maxCost = command.freeDrive ? SomaSim.Util.Fixnum.MAX_VALUE : new SomaSim.Util.Fixnum(movesRemaining);
			WorldPos startPos = startNode.pos;
			PathData builtPath = null;
			global::Game.Game.ctx.transit.FindDrivingPath(command.pid, peep, goalNode.pos, delegate (Pathfinding.Result result)
			{
				if (result.status == Pathfinding.Status.Success)
				{
					builtPath = new PathData();
					result.PopulatePath(builtPath, startPos, maxCost);
				}
			});
			pathfindMs = GetElapsedMs(segmentTicks);
			path = builtPath;
			long totalMs = GetElapsedMs(totalTicks);
			if (totalMs >= 12L || pathfindMs >= 8L || startResolveMs >= 8L)
			{
				Debug.Log($"[PERF][HumanVehicleCommandPath] ms={totalMs} contextMs={contextMs} goalMs={goalMs} startResolveMs={startResolveMs} authorityMs={authorityMs} pathfindMs={pathfindMs} vehicle={assignment.VehicleID.id} peep={assignment.peepId.id} startNode={startNode?.id ?? NodeID.INVALID} goalNode={command.goalID} source={source} moves={movesRemaining} pathNodes={builtPath?.nodes?.Count ?? 0} pathWorld={builtPath?.world?.Count ?? 0} frame={Time.frameCount}");
			}
			return true;
		}

		private static long GetElapsedMs(long startTicks)
		{
			return (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000L / System.Diagnostics.Stopwatch.Frequency;
		}

		internal static bool TryGetCrewCommandNode(CrewAssignment crew, out Node node)
		{
			return TryGetCrewCommandNode(crew, out node, CrewNodeResolutionMode.LiveAuthoritative);
		}

		internal static bool TryGetCrewCommandNode(CrewAssignment crew, out Node node, CrewNodeResolutionMode mode)
		{
			node = null;
			try
			{
				if (!crew.IsValid)
					return false;
				if (crew.IsInVehicle)
				{
					NodeID interactiveNodeId = NodeID.INVALID;
					string interactiveSource = "none";
					bool hasInteractivePendingNode = false;
					if (mode == CrewNodeResolutionMode.QueuedFinalGoalAllowed)
					{
						hasInteractivePendingNode = TryGetPendingHumanVehicleInteractiveNodeId(crew.VehicleID, out interactiveNodeId, out interactiveSource)
							&& interactiveNodeId.IsValid;
					}
					else if (mode == CrewNodeResolutionMode.BuildingCommittedOnly)
					{
						hasInteractivePendingNode = TryGetBuildingHumanVehicleInteractiveNodeId(crew.VehicleID, out interactiveNodeId, out interactiveSource)
							&& interactiveNodeId.IsValid;
					}
					if (hasInteractivePendingNode)
					{
						node = interactiveNodeId.FindNode();
						if (node != null)
						{
							LogVehicleAuthority("arrival-node", $"{crew.VehicleID.id}:{interactiveNodeId}:{interactiveSource}", $"arrival-node vehicle={crew.VehicleID.id} node={interactiveNodeId} source={interactiveSource}");
							return true;
						}
					}
					if (TryGetQueuedArrivalCommittedNode(crew.VehicleID, out node) && node != null)
					{
						LogVehicleAuthority("arrival-node", $"{crew.VehicleID.id}:{node.id}:queued-arrival", $"arrival-node vehicle={crew.VehicleID.id} node={node.id} source=queued-arrival");
						return true;
					}
					if (TryGetAuthoritativeVehicleNode(crew.VehicleID, out node, out string authoritySource) && node != null)
					{
						LogVehicleAuthority("arrival-node", $"{crew.VehicleID.id}:{node.id}:{authoritySource}", $"arrival-node vehicle={crew.VehicleID.id} node={node.id} source={authoritySource}");
						return true;
					}
					if (TryGetRecentFinalizedNode(crew.VehicleID, out node) && node != null)
					{
						LogVehicleAuthority("arrival-node", $"{crew.VehicleID.id}:{node.id}:recent-finalize", $"arrival-node vehicle={crew.VehicleID.id} node={node.id} source=recent-finalize");
						return true;
					}
				if (mode == CrewNodeResolutionMode.PendingCurrentSegmentAllowed
					&& IsHumanVehicleTravelActive(crew.VehicleID)
						&& TryGetPendingHumanVehicleTravel(crew.VehicleID, out _, out NodeID expectedNodeId, out _)
						&& expectedNodeId.IsValid)
					{
						node = expectedNodeId.FindNode();
						if (node != null)
						{
							string pendingSource = mode == CrewNodeResolutionMode.PendingCurrentSegmentAllowed ? "pending-internal-segment" : "pending-expected";
							LogVehicleAuthority("arrival-node", $"{crew.VehicleID.id}:{expectedNodeId}:{pendingSource}", $"arrival-node vehicle={crew.VehicleID.id} node={expectedNodeId} source={pendingSource}");
							return true;
						}
					}
					return false;
				}

				Entity peep = crew.GetPeep();
				node = peep?.components?.agent?.GetNode();
				return node != null;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryGetCrewCommandNode: " + ex.Message);
				node = null;
				return false;
			}
		}

		internal static List<CrewAssignment> GetHumanCrewPresentAtNodeLive(PlayerCrew crew, NodeID nodeId)
		{
			return GetHumanCrewPresentAtNode(crew, nodeId, CrewNodeResolutionMode.LiveAuthoritative);
		}

		private static List<CrewAssignment> GetHumanCrewPresentAtNode(PlayerCrew crew, NodeID nodeId, CrewNodeResolutionMode mode)
		{
			List<CrewAssignment> result = new List<CrewAssignment>();
			if (crew == null || !crew.PID.IsHumanPlayer || !nodeId.IsValid)
			{
				return result;
			}

			List<CrewAssignment> living = crew.GetLiving().ToList();
			Dictionary<long, List<CrewAssignment>> vehicleAssignments = new Dictionary<long, List<CrewAssignment>>();
			HashSet<long> matchedVehicleIds = new HashSet<long>();
			HashSet<long> addedPeepIds = new HashSet<long>();

			foreach (CrewAssignment assignment in living)
			{
				if (!IsCrewAvailableForLiveNodeQueries(crew, assignment))
				{
					continue;
				}

				if (!TryGetCrewCommandNode(assignment, out Node crewNode, mode) || crewNode == null || crewNode.id != nodeId)
				{
					continue;
				}

				if (!assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					if (addedPeepIds.Add((long)assignment.peepId.id))
					{
						result.Add(assignment);
					}
					continue;
				}

				long vehicleKey = (long)assignment.VehicleID.id;
				if (!vehicleAssignments.TryGetValue(vehicleKey, out List<CrewAssignment> grouped))
				{
					grouped = new List<CrewAssignment>();
					vehicleAssignments[vehicleKey] = grouped;
				}
				grouped.Add(assignment);
				matchedVehicleIds.Add(vehicleKey);
			}

			foreach (CrewAssignment assignment in living)
			{
				if (!assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					continue;
				}

				long vehicleKey = (long)assignment.VehicleID.id;
				if (!matchedVehicleIds.Remove(vehicleKey))
				{
					continue;
				}

				if (!vehicleAssignments.TryGetValue(vehicleKey, out List<CrewAssignment> grouped) || grouped == null || grouped.Count == 0)
				{
					continue;
				}

				EntityID driverPeepId = GetDriverPeepId(crew, assignment.VehicleID);
				if (driverPeepId.IsValid)
				{
					CrewAssignment driverAssignment = grouped.FirstOrDefault(item => item.peepId == driverPeepId);
					if (driverAssignment.IsValid && addedPeepIds.Add((long)driverAssignment.peepId.id))
					{
						result.Add(driverAssignment);
					}
				}

				foreach (CrewAssignment groupedAssignment in grouped)
				{
					if (!groupedAssignment.IsValid || !groupedAssignment.peepId.IsValid || !addedPeepIds.Add((long)groupedAssignment.peepId.id))
					{
						continue;
					}
					result.Add(groupedAssignment);
				}
			}

			return result;
		}

		internal static List<CrewAssignment> GetHumanCrewUsableForMapScopeAtNode(PlayerCrew crew, NodeID nodeId)
		{
			_ = TryGetHumanCrewUsableForMapScopeAtNode(crew, nodeId, out List<CrewAssignment> matches, out _);
			return matches;
		}

		internal static bool TryGetSelectedHumanVehicleForMapScope(PlayerCrew crew, out EntityID vehicleId, out string sourceTag)
		{
			vehicleId = EntityID.INVALID;
			sourceTag = "none";
			if (crew == null || !crew.PID.IsHumanPlayer)
			{
				return false;
			}

			Entity currentActive = global::Game.Game.ctx?.selection?.CurrentActive;
			if (currentActive != null)
			{
				if (TryResolveSelectedHumanVehicleFromEntity(crew, currentActive, "selection", out vehicleId, out sourceTag))
				{
					RememberSelectedHumanVehicle(vehicleId);
					return true;
				}
			}

			Entity previousActive = global::Game.Game.ctx?.selection?.PreviousActive;
			if (previousActive != null)
			{
				if (TryResolveSelectedHumanVehicleFromEntity(crew, previousActive, "previous-selection", out vehicleId, out sourceTag))
				{
					RememberSelectedHumanVehicle(vehicleId);
					return true;
				}
			}

			if (TryGetRememberedSelectedHumanVehicle(crew, out vehicleId, out sourceTag))
			{
				return true;
			}

			List<PendingVehicleTravelState> activeVehicles = _pendingVehicleTravelByVehicleId.Values
				.Where(state => state.VehicleID.IsValid
					&& state.PeepID.IsValid
					&& _activeHumanVehicleTravel.Contains((long)state.VehicleID.id)
					&& crew.GetCrewForPeep(state.PeepID).IsValid)
				.GroupBy(state => state.VehicleID.id)
				.Select(group => group.First())
				.ToList();
			if (activeVehicles.Count == 1)
			{
				vehicleId = activeVehicles[0].VehicleID;
				sourceTag = "single-active-travel";
				RememberSelectedHumanVehicle(vehicleId);
				return true;
			}

			return false;
		}

		private static void RememberSelectedHumanVehicle(EntityID vehicleId)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			_lastSelectedHumanVehicleId = unchecked((long)vehicleId.id);
		}

		private static bool TryGetRememberedSelectedHumanVehicle(PlayerCrew crew, out EntityID vehicleId, out string sourceTag)
		{
			vehicleId = EntityID.INVALID;
			sourceTag = "none";
			if (crew == null || !crew.PID.IsHumanPlayer || _lastSelectedHumanVehicleId <= 0L)
			{
				return false;
			}

			vehicleId = EntityID.FromID(unchecked((ulong)_lastSelectedHumanVehicleId));
			if (!vehicleId.IsValid)
			{
				return false;
			}

			Entity vehicle = vehicleId.FindEntity();
			if (vehicle?.data?.mobile?.pid != crew.PID)
			{
				vehicleId = EntityID.INVALID;
				return false;
			}

			EntityID rememberedVehicleId = vehicleId;
			bool hasOccupant = crew.GetLiving().Any(assignment => assignment.IsValid
				&& assignment.IsInVehicle
				&& assignment.VehicleID == rememberedVehicleId
				&& crew.IsOnBoard(assignment.peepId));
			if (!hasOccupant)
			{
				vehicleId = EntityID.INVALID;
				return false;
			}

			bool hasRememberedFinalNode = GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(vehicleId, out NodeID rememberedFinalNodeId)
				&& rememberedFinalNodeId.IsValid;
			bool hasPendingTravelState = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& (pendingState.ExpectedNodeID.IsValid || pendingState.GoalNodeID.IsValid);
			if (!hasRememberedFinalNode
				&& !_activeHumanVehicleTravel.Contains(unchecked((long)vehicleId.id))
				&& !hasPendingTravelState)
			{
				vehicleId = EntityID.INVALID;
				return false;
			}

			sourceTag = "remembered-selection";
			return true;
		}

		private static void RefreshHumanDriverSelectionState(PlayerCrew crew, EntityID vehicleId, EntityID driverPeepId, string source, EntityID priorDriverPeepId = default)
		{
			if (crew == null || !crew.PID.IsHumanPlayer || !vehicleId.IsValid)
			{
				return;
			}

			try
			{
				string driverSwitchSource = "driver-switch-" + (source ?? "unknown");
				bool preservedPendingRoute = TryReassignPendingHumanVehicleTravelDriver(vehicleId, driverPeepId, driverSwitchSource);
				if (!preservedPendingRoute)
				{
					ClearPendingHumanVehicleTravel(vehicleId, driverSwitchSource);
					ClearInterruptExpectedStartNode(vehicleId, driverSwitchSource);
				}
				RememberSelectedHumanVehicle(vehicleId);
				TryRefreshCrewHudCardsForVehicle(vehicleId);
				GameplayTweaksPlugin.RefreshCrewHudUi(driverSwitchSource, rebuildCards: true);
				if (priorDriverPeepId.IsValid && priorDriverPeepId != driverPeepId)
				{
					GameplayTweaksPlugin.ClearCrewPicksForTargets(driverSwitchSource, priorDriverPeepId, driverPeepId, vehicleId);
				}
				else
				{
					GameplayTweaksPlugin.ClearCrewPicksForTargets(driverSwitchSource, driverPeepId, vehicleId);
				}
				GameplayTweaksPlugin.MarkCrewPickAggroDirty(crew.PID, driverSwitchSource);
				GameplayTweaksPlugin.FlushCrewPickAggroRefreshes(driverSwitchSource);
				GameplayTweaksPlugin.PactColorUiPatch.RequestFullCrewPickRefresh("vehicle-selection", 0);

				SelectionManager selection = global::Game.Game.ctx?.selection;
				Entity currentActive = selection?.CurrentActive;
				bool selectionMatchesVehicle = currentActive != null
					&& (currentActive.Id == vehicleId
						|| currentActive.Id == driverPeepId
						|| (crew.GetCrewForPeep(currentActive.Id).IsValid
							&& crew.GetCrewForPeep(currentActive.Id).IsInVehicle
							&& crew.GetCrewForPeep(currentActive.Id).VehicleID == vehicleId));
				Entity vehicleEntity = vehicleId.FindEntity();
				Entity driverEntity = driverPeepId.FindEntity();
				if (selection != null)
				{
					selection.HandleDeselect();
					if (driverEntity != null)
					{
						selection.SetActive(driverEntity);
						selection.SetFocus(driverEntity);
					}
					else if (vehicleEntity != null)
					{
						selection.SetActive(vehicleEntity);
						selection.SetFocus(vehicleEntity);
					}
				}

				NodeID activeNodeId = currentActive?.data?.agent?.nid ?? NodeID.INVALID;
				NodeID driverNodeId = driverPeepId.FindEntity()?.data?.agent?.nid ?? NodeID.INVALID;
				TryGetAuthoritativeVehicleNodeId(vehicleId, out NodeID vehicleNodeId, out string vehicleNodeSource);
				EntityID mappedDriverPeepId = GetDriverPeepId(crew, vehicleId);
				Node vehicleNode = vehicleNodeId.FindNode();
				if (vehicleNode != null)
				{
					Node finalNode = vehicleNode;
					if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
						&& pendingState.GoalNodeID.IsValid)
					{
						finalNode = pendingState.GoalNodeID.FindNode() ?? vehicleNode;
					}
					GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(vehicleNode, finalNode, driverSwitchSource, vehicleId);
				}
				GameplayTweaksPlugin.RefreshShownOwnedBuildingDialogsAfterVehicleTravel(vehicleId, driverSwitchSource);

				LogVehicleAuthority(
					"driver-switch-refresh",
					$"{vehicleId.id}:{driverPeepId.id}:{source}",
					$"driver-switch-refresh vehicle={vehicleId.id} driver={driverPeepId.id} mappedDriver={mappedDriverPeepId.id} driverNode={driverNodeId} vehicleNode={vehicleNodeId} vehicleNodeSource={vehicleNodeSource} active={currentActive?.Id.id ?? 0UL} activeNode={activeNodeId} selectionMatchesVehicle={selectionMatchesVehicle} source={source}",
					dedupe: false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] RefreshHumanDriverSelectionState: " + ex.Message);
			}
		}

		private static bool TryResolveSelectedHumanVehicleFromEntity(PlayerCrew crew, Entity activeEntity, string sourcePrefix, out EntityID vehicleId, out string sourceTag)
		{
			vehicleId = EntityID.INVALID;
			sourceTag = "none";
			if (crew == null || activeEntity == null)
			{
				return false;
			}

			if (activeEntity.components?.mobile != null && activeEntity.data?.mobile?.pid == crew.PID && activeEntity.Id.IsValid)
			{
				vehicleId = activeEntity.Id;
				sourceTag = string.IsNullOrWhiteSpace(sourcePrefix) ? "selection-mobile" : sourcePrefix + "-mobile";
				return true;
			}

			CrewAssignment selectedCrew = crew.GetCrewForPeep(activeEntity.Id);
			if (selectedCrew.IsValid && selectedCrew.IsInVehicle && selectedCrew.VehicleID.IsValid)
			{
				vehicleId = selectedCrew.VehicleID;
				sourceTag = string.IsNullOrWhiteSpace(sourcePrefix) ? "selection-crew" : sourcePrefix + "-crew";
				return true;
			}

			return false;
		}

		internal static bool TryGetHumanCrewUsableForScopePreviewAtNode(PlayerCrew crew, NodeID nodeId, out List<CrewAssignment> matches, out string sourceTag)
		{
			return TryGetHumanCrewUsableForScopePreviewAtNodes(crew, new[] { nodeId }, nodeId, EntityID.INVALID, out matches, out sourceTag);
		}

		internal static void MarkQueuedFinalGoalKnownForScopePreview(PlayerInfo player, Entity peep, EntityID vehicleId, Node startNode, Node reachedNode, NodeID finalGoalNodeId, string sourceTag)
		{
			if (player == null || !player.PID.IsHumanPlayer || !vehicleId.IsValid || !finalGoalNodeId.IsValid)
			{
				return;
			}

			Node finalNode = finalGoalNodeId.FindNode();
			if (finalNode == null)
			{
				return;
			}

			if (string.Equals(sourceTag, "travel-start-preview-goal-known", StringComparison.Ordinal)
				|| string.Equals(sourceTag, "route-resume-goal-known", StringComparison.Ordinal))
			{
				LogVehicleAuthority(
					"scope-preview-final-goal-skipped",
					$"{vehicleId.id}:{finalNode.id}:{sourceTag}",
					$"scope-preview-final-goal-skipped vehicle={vehicleId.id} node={finalNode.id} reachedNode={reachedNode?.id ?? NodeID.INVALID} source={sourceTag} reason=route-node-authority",
					dedupe: false);
				return;
			}

			bool wasKnown = true;
			try
			{
				wasKnown = finalNode.known.Get(PlayerID.HumanPlayer);
				if (!wasKnown && !IsHumanVehiclePhysicallyAtNode(vehicleId, finalNode.id))
				{
					LogVehicleAuthority(
						"scope-preview-final-goal-known-blocked",
						$"{vehicleId.id}:{finalNode.id}:{sourceTag}",
						$"scope-preview-final-goal-known-blocked vehicle={vehicleId.id} node={finalNode.id} reachedNode={reachedNode?.id ?? NodeID.INVALID} source={sourceTag} reason=unknown-not-arrived",
						dedupe: false);
					return;
				}
				if (!wasKnown && peep?.components?.agent != null)
				{
					peep.components.agent.IncrementStat(CrewStats.NodeScouted, 1);
				}
				if (!wasKnown)
				{
					player.meetings.MarkNodeAsKnown(finalNode, expectedSeen: true, instant: true);
				}
			}
			catch (Exception ex)
			{
				LogVehicleAuthority("scope-preview-final-goal-known-error", $"{vehicleId.id}:{finalGoalNodeId}:{sourceTag}", $"scope-preview-final-goal-known-error vehicle={vehicleId.id} node={finalGoalNodeId} source={sourceTag} error={ex.Message}", dedupe: false);
				return;
			}

			if (startNode != null && startNode.id.IsValid && finalNode.id.IsValid && startNode.id != finalNode.id)
			{
				try
				{
					GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(startNode, finalNode, sourceTag, vehicleId);
				}
				catch (Exception ex)
				{
					LogVehicleAuthority("scope-preview-final-goal-refresh-error", $"{vehicleId.id}:{finalNode.id}:{sourceTag}", $"scope-preview-final-goal-refresh-error vehicle={vehicleId.id} node={finalNode.id} source={sourceTag} error={ex.Message}", dedupe: false);
				}
			}

			NodeID reachedNodeId = reachedNode?.id ?? NodeID.INVALID;
			LogVehicleAuthority(
				"scope-preview-final-goal-known",
				$"{vehicleId.id}:{finalNode.id}:{sourceTag}:{wasKnown}",
				$"scope-preview-final-goal-known vehicle={vehicleId.id} node={finalNode.id} wasKnown={wasKnown} reachedNode={reachedNodeId} source={sourceTag}",
				dedupe: false);
		}

		internal static bool TryGetHumanCrewUsableForScopePreviewAtBuilding(PlayerCrew crew, Entity building, out List<CrewAssignment> matches, out string sourceTag)
		{
			matches = new List<CrewAssignment>();
			sourceTag = "none";
			if (!GameplayTweaks.CommandButtonScopeOutPatch.TryGetScopeComparisonNodeIds(building, out _, out List<NodeID> comparisonNodeIds, out NodeID primaryNodeId) || comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			return TryGetHumanCrewUsableForScopePreviewAtNodes(crew, comparisonNodeIds, primaryNodeId, building?.Id ?? EntityID.INVALID, out matches, out sourceTag);
		}

		private static bool TryGetHumanCrewUsableForScopePreviewAtNodes(PlayerCrew crew, IEnumerable<NodeID> comparisonNodeIds, NodeID requestedNodeId, EntityID buildingId, out List<CrewAssignment> matches, out string sourceTag)
		{
			matches = new List<CrewAssignment>();
			sourceTag = "none";
			if (crew == null || !crew.PID.IsHumanPlayer)
			{
				return false;
			}

			List<NodeID> normalizedNodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			if (normalizedNodeIds.Count <= 0)
			{
				return false;
			}
			if (!requestedNodeId.IsValid)
			{
				requestedNodeId = normalizedNodeIds[0];
			}

			if (!TryGetSelectedHumanVehicleForMapScope(crew, out EntityID selectedVehicleId, out string selectedSource) || !selectedVehicleId.IsValid)
			{
				return false;
			}

			EntityID driverPeepId = GetDriverPeepId(crew, selectedVehicleId);
			CrewAssignment driverAssignment = driverPeepId.IsValid ? crew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
			List<CrewAssignment> activeOccupants = GetAllCrewInVehicle(crew, selectedVehicleId)
				.Where(assignment => assignment.IsValid && IsActiveVehicleOccupant(crew, assignment))
				.ToList();
			if (activeOccupants.Count <= 0)
			{
				LogVehicleAuthority("scope-preview-blocked", $"{selectedVehicleId.id}:{requestedNodeId}:no-active-occupants", $"scope-preview-blocked vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} building={buildingId.id} reason=no-active-occupants selectedSource={selectedSource}", dedupe: false);
				return false;
			}

			if (!TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out NodeID previewNodeId, out string previewSource, "scope-building") || !previewNodeId.IsValid)
			{
				return false;
			}

			bool matched = normalizedNodeIds.Contains(previewNodeId);
			Entity building = buildingId.IsValid ? buildingId.FindEntity() : null;
			WorldPos buildingWorldPos = building?.data?.board?.worldpos ?? WorldPos.Zero;
			NodeID clusterMatchNodeId = NodeID.INVALID;
			List<NodeID> clusteredNodeIds = null;
			if (!matched
				&& TryResolveScopeFrontageClusterMatch(previewNodeId, normalizedNodeIds, buildingWorldPos, out clusterMatchNodeId, out clusteredNodeIds))
			{
				matched = true;
			}
			if (!matched
				&& TryGetPendingHumanVehicleTravelState(selectedVehicleId, out PendingVehicleTravelState pendingScopeState)
				&& pendingScopeState.GoalNodeID.IsValid
				&& normalizedNodeIds.Contains(pendingScopeState.GoalNodeID)
				&& (pendingScopeState.ExpectedNodeID.IsValid || pendingScopeState.ResumeQueued || IsHumanVehicleTravelActive(selectedVehicleId)))
			{
				previewNodeId = pendingScopeState.GoalNodeID;
				previewSource = pendingScopeState.ResumeQueued && !pendingScopeState.ExpectedNodeID.IsValid
					? "queued-final-goal"
					: "route-final-goal";
				matched = true;
				LogVehicleAuthority("scope-preview-final-goal-accepted", $"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{buildingId.id}", $"scope-preview-final-goal-accepted vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} matchedNode={previewNodeId} normalizedNodes={string.Join(",", normalizedNodeIds.Select(nodeId => nodeId.ToString()))} building={buildingId.id} source={previewSource} selectedSource={selectedSource}", dedupe: false);
			}

			if (!matched)
			{
				string normalizedNodeText = string.Join(",", normalizedNodeIds.Select(nodeId => nodeId.ToString()));
				string logTag = normalizedNodeIds.Count > 1 ? "scope-preview-corner-blocked" : "scope-preview-blocked";
				LogVehicleAuthority(logTag, $"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:node-mismatch", $"{logTag} vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} resolvedNode={previewNodeId} normalizedNodes={normalizedNodeText} building={buildingId.id} resolvedSource={previewSource} reason=node-mismatch selectedSource={selectedSource}", dedupe: false);
				LogVehicleAuthority("scope-preview-destination-blocked", $"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{buildingId.id}", $"scope-preview-destination-blocked vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} resolvedNode={previewNodeId} normalizedNodes={normalizedNodeText} building={buildingId.id} resolvedSource={previewSource} reason=node-mismatch selectedSource={selectedSource}", dedupe: false);
				return false;
			}

			bool sourceIsPreviewAuthority = IsHumanVehicleScopePreviewStatusAuthorizedSource(previewSource);
			bool physicallyAtPreviewNode = IsHumanVehiclePhysicallyAtNode(selectedVehicleId, previewNodeId);
			bool routeExpectedScopeAccess = IsHumanVehicleRouteSimExpectedAccessNode(selectedVehicleId, previewNodeId, "scope-preview-building", out string routeExpectedSource);
			if (buildingId.IsValid
				&& IsHumanVehicleFinalGoalPreviewOnlySource(previewSource)
				&& !physicallyAtPreviewNode
				&& !routeExpectedScopeAccess)
			{
				LogVehicleAuthority(
					"scope-preview-physical-presence-required",
					$"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{previewSource}:{buildingId.id}",
					$"scope-preview-physical-presence-required vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} resolvedNode={previewNodeId} building={buildingId.id} resolvedSource={previewSource} reason=final-goal-not-arrived selectedSource={selectedSource}",
					dedupe: false);
				return false;
			}
			if (routeExpectedScopeAccess)
			{
				LogVehicleAuthority(
					"scope-preview-route-access-allowed",
					$"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{previewSource}:{buildingId.id}",
					$"scope-preview-route-access-allowed vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} resolvedNode={previewNodeId} building={buildingId.id} resolvedSource={previewSource} routeSource={routeExpectedSource} reason=active-route-expected selectedSource={selectedSource}",
					dedupe: false);
			}
			if (!sourceIsPreviewAuthority && !physicallyAtPreviewNode && !routeExpectedScopeAccess)
			{
				LogVehicleAuthority(
					"scope-preview-source-rejected",
					$"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{previewSource}:{buildingId.id}",
					$"scope-preview-source-rejected vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} resolvedNode={previewNodeId} building={buildingId.id} resolvedSource={previewSource} reason=not-preview-or-physical selectedSource={selectedSource}",
					dedupe: false);
				return false;
			}

			if (!IsHumanVehiclePreviewNodeKnownOrReached(selectedVehicleId, previewNodeId)
				&& !IsHumanVehicleFinalGoalScopePreviewAllowed(selectedVehicleId, previewNodeId, previewSource))
			{
				LogVehicleAuthority(
					"scope-preview-unknown-blocked",
					$"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{buildingId.id}",
					$"scope-preview-unknown-blocked vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} resolvedNode={previewNodeId} building={buildingId.id} resolvedSource={previewSource} reason=unknown-not-arrived selectedSource={selectedSource}",
					dedupe: false);
				return false;
			}

			if (clusterMatchNodeId.IsValid)
			{
				string clusteredNodeText = string.Join(",", (clusteredNodeIds ?? normalizedNodeIds).Select(nodeId => nodeId.ToString()));
				LogVehicleAuthority("scope-preview-frontage-collapsed", $"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{clusterMatchNodeId}:{buildingId.id}", $"scope-preview-frontage-collapsed vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} previewNode={previewNodeId} matchedNode={clusterMatchNodeId} normalizedNodes={clusteredNodeText} building={buildingId.id} source={previewSource} selectedSource={selectedSource}", dedupe: false);
			}
			else if (normalizedNodeIds.Count > 1 && previewNodeId != requestedNodeId)
			{
				string normalizedNodeText = string.Join(",", normalizedNodeIds.Select(nodeId => nodeId.ToString()));
				LogVehicleAuthority("scope-preview-corner-normalized", $"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{buildingId.id}", $"scope-preview-corner-normalized vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} matchedNode={previewNodeId} normalizedNodes={normalizedNodeText} building={buildingId.id} source={previewSource} selectedSource={selectedSource}", dedupe: false);
			}
			LogVehicleAuthority("scope-preview-destination-accepted", $"{selectedVehicleId.id}:{requestedNodeId}:{previewNodeId}:{buildingId.id}", $"scope-preview-destination-accepted vehicle={selectedVehicleId.id} requestedNode={requestedNodeId} matchedNode={previewNodeId} normalizedNodes={string.Join(",", normalizedNodeIds.Select(nodeId => nodeId.ToString()))} building={buildingId.id} source={previewSource} selectedSource={selectedSource}", dedupe: false);

			matches = GetHumanVehicleScopeCandidates(crew, selectedVehicleId, activeOccupants);
			if (matches.Count <= 0)
			{
				if (driverAssignment.IsValid)
				{
					matches.Add(driverAssignment);
				}
				else
				{
					matches.AddRange(activeOccupants);
				}
			}
			sourceTag = previewSource;
			LogVehicleAuthority("scope-preview-vehicle-crew", $"{selectedVehicleId.id}:{matches.Count}:{previewNodeId}:{previewSource}", $"scope-preview-vehicle-crew vehicle={selectedVehicleId.id} crew={string.Join(",", matches.Where(match => match.IsValid && match.peepId.IsValid).Select(match => match.peepId.id.ToString()))} node={previewNodeId} requestedNode={requestedNodeId} building={buildingId.id} source={previewSource} selectedSource={selectedSource}", dedupe: false);
			return true;
		}

		internal static bool AreNodesDirectlyAdjacent(NodeID a, NodeID b)
		{
			if (!a.IsValid || !b.IsValid || a == b)
			{
				return false;
			}

			Node node = a.FindNode();
			if (node?.edges == null)
			{
				return false;
			}

			foreach (NodeEdgeID edgeId in node.edges)
			{
				NodeEdge edge = edgeId.IsValid ? edgeId.FindEdge() : null;
				if (edge != null && edge.GetOtherNodeID(a) == b)
				{
					return true;
				}
			}

			return false;
		}

		internal static bool TryCollapseTinyScopeFrontageCluster(IEnumerable<NodeID> comparisonNodeIds, WorldPos anchorPos, out List<NodeID> collapsedNodeIds, out NodeID representativeNodeId)
		{
			collapsedNodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			representativeNodeId = NodeID.INVALID;
			if (collapsedNodeIds.Count <= 1 || collapsedNodeIds.Count > 3)
			{
				return false;
			}

			List<Node> nodes = collapsedNodeIds
				.Select(nodeId => nodeId.FindNode())
				.Where(node => node != null)
				.ToList();
			if (nodes.Count != collapsedNodeIds.Count)
			{
				return false;
			}

			Node anchorNode = anchorPos.IsZero
				? nodes[0]
				: nodes
					.OrderBy(node => (node.pos - anchorPos).MagnitudeSquared)
					.FirstOrDefault();
			if (anchorNode == null)
			{
				return false;
			}

			foreach (Node node in nodes)
			{
				float anchorDistance = (node.pos - anchorNode.pos).Magnitude;
				bool adjacentToAnchor = AreNodesDirectlyAdjacent(anchorNode.id, node.id);
				if (node.id != anchorNode.id && anchorDistance > 1.85f && !adjacentToAnchor)
				{
					return false;
				}

				if (!anchorPos.IsZero && (node.pos - anchorPos).Magnitude > 3.5f)
				{
					return false;
				}
			}

			collapsedNodeIds = new List<NodeID> { anchorNode.id };
			representativeNodeId = anchorNode.id;
			return true;
		}

		internal static bool TryResolveScopeFrontageClusterMatch(NodeID previewNodeId, IEnumerable<NodeID> comparisonNodeIds, WorldPos anchorPos, out NodeID matchedNodeId, out List<NodeID> clusteredNodeIds)
		{
			matchedNodeId = NodeID.INVALID;
			clusteredNodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			if (!previewNodeId.IsValid || clusteredNodeIds.Count <= 1)
			{
				return false;
			}

			if (!TryCollapseTinyScopeFrontageCluster(clusteredNodeIds, anchorPos, out List<NodeID> collapsedNodeIds, out NodeID representativeNodeId)
				|| !representativeNodeId.IsValid
				|| collapsedNodeIds.Count != 1)
			{
				return false;
			}

			if (!AreNodesDirectlyAdjacent(previewNodeId, representativeNodeId))
			{
				Node previewNode = previewNodeId.FindNode();
				Node representativeNode = representativeNodeId.FindNode();
				if (previewNode == null || representativeNode == null || (previewNode.pos - representativeNode.pos).Magnitude > 1.85f)
				{
					return false;
				}
			}

			matchedNodeId = representativeNodeId;
			return true;
		}

		private static List<CrewAssignment> FilterHumanCrewMatchesToVehicle(List<CrewAssignment> matches, EntityID vehicleId)
		{
			if (matches == null || matches.Count <= 0 || !vehicleId.IsValid)
			{
				return new List<CrewAssignment>();
			}

			return matches
				.Where(item => item.IsValid && item.IsInVehicle && item.VehicleID == vehicleId)
				.ToList();
		}

		private static List<CrewAssignment> GetHumanVehicleScopeCandidates(PlayerCrew crew, EntityID vehicleId, IEnumerable<CrewAssignment> matches)
		{
			List<CrewAssignment> normalizedMatches = matches?
				.Where(item => item.IsValid && item.peepId.IsValid)
				.GroupBy(item => item.peepId.id)
				.Select(group => group.First())
				.ToList() ?? new List<CrewAssignment>();
			if (normalizedMatches.Count <= 0)
			{
				return new List<CrewAssignment>();
			}

			if (crew == null || !vehicleId.IsValid)
			{
				return normalizedMatches;
			}

			List<CrewAssignment> vehicleMatches = normalizedMatches
				.Where(item => item.IsInVehicle && item.VehicleID == vehicleId && crew.IsOnBoard(item.peepId) && !GameplayTweaksPlugin.IsCrewCurrentlyJailed(item.peepId))
				.ToList();
			if (vehicleMatches.Count <= 0)
			{
				return normalizedMatches;
			}

			List<CrewAssignment> orderedMatches = new List<CrewAssignment>();
			EntityID driverPeepId = GetDriverPeepId(crew, vehicleId);
			if (driverPeepId.IsValid)
			{
				CrewAssignment driverMatch = vehicleMatches.FirstOrDefault(item => item.peepId == driverPeepId);
				if (driverMatch.IsValid)
				{
					return new List<CrewAssignment> { driverMatch };
				}
			}

			return vehicleMatches.Count > 0
				? new List<CrewAssignment> { vehicleMatches[0] }
				: normalizedMatches;
		}

		internal static bool HasBlockedCrewAssignmentsInVehicle(PlayerCrew crew, EntityID vehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
			{
				return false;
			}

			foreach (CrewAssignment assignment in crew.AllCrew)
			{
				if (!assignment.IsValid
					|| !assignment.IsInVehicle
					|| assignment.VehicleID != vehicleId
					|| !assignment.peepId.IsValid)
				{
					continue;
				}

				if (GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(assignment.peepId, out _))
				{
					return true;
				}
			}

			return false;
		}

		private static bool ShouldPreferSelectedVehiclePreviewForMapScope(EntityID vehicleId, NodeID committedNodeId, NodeID previewNodeId, string previewSource, string committedSource)
		{
			if (!vehicleId.IsValid
				|| !previewNodeId.IsValid
				|| previewNodeId == committedNodeId
				|| string.IsNullOrWhiteSpace(previewSource)
				|| !previewSource.StartsWith("selected-", StringComparison.Ordinal))
			{
				return false;
			}

			if (HasBlockedCrewAssignmentsInVehicle(G.GetHumanCrew(), vehicleId))
			{
				LogVehicleAuthority("selected-scope-preview-suppressed", $"{vehicleId.id}:{committedNodeId}:{previewNodeId}:custody-blocked", $"selected-scope-preview-suppressed vehicle={vehicleId.id} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} committedSource={committedSource} reason=custody-blocked");
				return false;
			}

			if (IsHumanVehicleTravelActive(vehicleId) || HasQueuedHumanVehiclePendingResume(vehicleId))
			{
				return true;
			}

			if (TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState)
				&& pendingState.GoalNodeID.IsValid
				&& pendingState.GoalNodeID == previewNodeId)
			{
				return true;
			}

			return string.Equals(previewSource, "selected-final-goal-memory", StringComparison.Ordinal)
				&& string.Equals(committedSource, "mobile", StringComparison.Ordinal)
				&& GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(vehicleId, out NodeID rememberedFinalNodeId)
				&& rememberedFinalNodeId == previewNodeId;
		}

		internal static bool TryGetHumanCrewUsableForMapScopeAtNode(PlayerCrew crew, NodeID nodeId, out List<CrewAssignment> matches, out string sourceTag)
		{
			CrewNodeResolutionMode mode = GetMapScopeCrewNodeResolutionMode();
			if (TryGetSelectedHumanVehicleForMapScope(crew, out EntityID selectedVehicleId, out string selectedSource))
			{
				matches = new List<CrewAssignment>();
				sourceTag = "selected-none";
				string committedSource = "none";
				NodeID committedNodeId = NodeID.INVALID;
				NodeID previewNodeId = NodeID.INVALID;
				string previewSource = "none";
				bool hasPreviewNode = TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out previewNodeId, out previewSource, "selected-scope")
					&& previewNodeId.IsValid;
				if (TryGetBuildingHumanVehicleInteractiveNodeId(selectedVehicleId, out committedNodeId, out committedSource) && committedNodeId.IsValid)
				{
					if (nodeId == committedNodeId
						&& hasPreviewNode
						&& previewNodeId != committedNodeId
						&& IsHumanVehicleTravelActive(selectedVehicleId)
						&& !IsHumanVehiclePhysicallyAtNode(selectedVehicleId, committedNodeId))
					{
						LogVehicleAuthority("selected-scope-old-node-blocked", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}:active-unreached", $"selected-scope-old-node-blocked vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource} reason=active-route-not-physical", dedupe: false);
						LogVehicleAuthority("preview-scope-stale-start-blocked", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}:active-unreached", $"preview-scope-stale-start-blocked vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource} reason=active-route-not-physical", dedupe: false);
						if (TryGetOtherHumanCrewUsableForSelectedVehicleFallback(crew, selectedVehicleId, nodeId, out matches, out sourceTag))
						{
							LogVehicleAuthority("selected-scope-node-fallback", $"{selectedVehicleId.id}:{nodeId}:{selectedSource}:active-unreached", $"selected-scope-node-fallback vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource} reason=active-route-not-physical", dedupe: false);
							return true;
						}
						sourceTag = "selected-active-route-unreached";
						return false;
					}

					bool physicallyAtCommittedNode = IsHumanVehiclePhysicallyAtNode(selectedVehicleId, committedNodeId);
					if (nodeId == committedNodeId && physicallyAtCommittedNode)
					{
						matches = GetHumanVehicleScopeCandidates(crew, selectedVehicleId, FilterHumanCrewMatchesToVehicle(GetHumanCrewPresentAtNode(crew, nodeId, mode), selectedVehicleId));
						sourceTag = matches.Count > 0
							? GetHumanMapScopeSourceTag(matches, mode) + "-physical-vehicle-crew"
							: "selected-none";
						if (matches.Count > 0)
						{
							LogVehicleAuthority("selected-scope-physical-node-source", $"{selectedVehicleId.id}:{nodeId}:{selectedSource}:{committedSource}", $"selected-scope-physical-node-source vehicle={selectedVehicleId.id} node={nodeId} selectedSource={selectedSource} committedSource={committedSource} previewNode={previewNodeId} previewSource={previewSource}", dedupe: false);
						}
						return matches.Count > 0;
					}

					bool preferPreviewNode = hasPreviewNode
						&& ShouldPreferSelectedVehiclePreviewForMapScope(selectedVehicleId, committedNodeId, previewNodeId, previewSource, committedSource);
					if (preferPreviewNode)
					{
						if (nodeId != previewNodeId)
						{
							LogVehicleAuthority("selected-scope-preview-preferred", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}", $"selected-scope-preview-preferred vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
							LogVehicleAuthority("selected-scope-old-node-blocked", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}", $"selected-scope-old-node-blocked vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
							LogVehicleAuthority("preview-scope-stale-start-blocked", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}", $"preview-scope-stale-start-blocked vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
							if (TryGetOtherHumanCrewUsableForSelectedVehicleFallback(crew, selectedVehicleId, nodeId, out matches, out sourceTag))
							{
								LogVehicleAuthority("selected-scope-node-fallback", $"{selectedVehicleId.id}:{nodeId}:{selectedSource}:{previewSource}", $"selected-scope-node-fallback vehicle={selectedVehicleId.id} requestedNode={nodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
								return true;
							}
							return false;
						}

						if (TryGetHumanCrewUsableForScopePreviewAtNode(crew, nodeId, out matches, out sourceTag) && matches.Count > 0)
						{
							LogVehicleAuthority("selected-scope-preview-source", $"{selectedVehicleId.id}:{nodeId}:{selectedSource}:{previewSource}:{committedSource}", $"selected-scope-preview-source vehicle={selectedVehicleId.id} node={nodeId} selectedSource={selectedSource} previewSource={previewSource} committedSource={committedSource}", dedupe: false);
							return true;
						}

						LogVehicleAuthority("selected-scope-preview-missing", $"{selectedVehicleId.id}:{nodeId}:{previewNodeId}:{selectedSource}", $"selected-scope-preview-missing vehicle={selectedVehicleId.id} requestedNode={nodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
						if (TryGetOtherHumanCrewUsableForSelectedVehicleFallback(crew, selectedVehicleId, nodeId, out matches, out sourceTag))
						{
							LogVehicleAuthority("selected-scope-node-fallback", $"{selectedVehicleId.id}:{nodeId}:{selectedSource}:{previewSource}:preview-missing", $"selected-scope-node-fallback vehicle={selectedVehicleId.id} requestedNode={nodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource} reason=preview-missing", dedupe: false);
							return true;
						}
						return false;
					}

					if (nodeId != committedNodeId)
					{
						if (hasPreviewNode && previewNodeId != committedNodeId && nodeId == previewNodeId)
						{
							LogVehicleAuthority("selected-scope-preview-blocked", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}", $"selected-scope-preview-blocked vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
						}
						LogVehicleAuthority("selected-scope-old-node-blocked", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}", $"selected-scope-old-node-blocked vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
						LogVehicleAuthority("preview-scope-stale-start-blocked", $"{selectedVehicleId.id}:{nodeId}:{committedNodeId}:{previewNodeId}:{selectedSource}", $"preview-scope-stale-start-blocked vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
						if (TryGetOtherHumanCrewUsableForSelectedVehicleFallback(crew, selectedVehicleId, nodeId, out matches, out sourceTag))
						{
							LogVehicleAuthority("selected-scope-node-fallback", $"{selectedVehicleId.id}:{nodeId}:{selectedSource}:{committedSource}", $"selected-scope-node-fallback vehicle={selectedVehicleId.id} requestedNode={nodeId} committedNode={committedNodeId} previewNode={previewNodeId} previewSource={previewSource} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
							return true;
						}
						return false;
					}

					matches = GetHumanVehicleScopeCandidates(crew, selectedVehicleId, FilterHumanCrewMatchesToVehicle(GetHumanCrewPresentAtNode(crew, nodeId, mode), selectedVehicleId));
					sourceTag = matches.Count > 0
						? GetHumanMapScopeSourceTag(matches, mode) + "-vehicle-crew"
						: "selected-none";
					if (matches.Count > 0)
					{
						LogVehicleAuthority("selected-scope-node-source", $"{selectedVehicleId.id}:{nodeId}:{selectedSource}:{committedSource}", $"selected-scope-node-source vehicle={selectedVehicleId.id} node={nodeId} selectedSource={selectedSource} committedSource={committedSource}", dedupe: false);
					}
				}
				return matches.Count > 0;
			}

			return TryGetFallbackHumanCrewUsableForMapScopeAtNode(crew, nodeId, mode, null, out matches, out sourceTag);
		}

		private static bool TryGetFallbackHumanCrewUsableForMapScopeAtNode(PlayerCrew crew, NodeID nodeId, CrewNodeResolutionMode mode, string sourceSuffix, out List<CrewAssignment> matches, out string sourceTag)
		{
			matches = GetHumanCrewPresentAtNode(crew, nodeId, mode);
			sourceTag = GetHumanMapScopeSourceTag(matches, mode);
			if (matches.Count <= 0 && mode != CrewNodeResolutionMode.LiveAuthoritative)
			{
				matches = GetHumanCrewPresentAtNode(crew, nodeId, CrewNodeResolutionMode.LiveAuthoritative);
				sourceTag = matches.Count > 0
					? GetHumanMapScopeSourceTag(matches, CrewNodeResolutionMode.LiveAuthoritative) + "-fallback"
					: GetCrewNodeResolutionSourceTag(CrewNodeResolutionMode.LiveAuthoritative);
			}

			if (matches.Count > 0 && !string.IsNullOrEmpty(sourceSuffix))
			{
				sourceTag = string.IsNullOrEmpty(sourceTag) ? sourceSuffix : (sourceTag + "-" + sourceSuffix);
			}

			return matches.Count > 0;
		}

		private static bool TryGetOtherHumanCrewUsableForSelectedVehicleFallback(PlayerCrew crew, EntityID selectedVehicleId, NodeID nodeId, out List<CrewAssignment> matches, out string sourceTag)
		{
			matches = GetHumanCrewPresentAtNode(crew, nodeId, CrewNodeResolutionMode.LiveAuthoritative)
				.Where(assignment => !assignment.IsInVehicle || !assignment.VehicleID.IsValid || assignment.VehicleID != selectedVehicleId)
				.ToList();
			sourceTag = matches.Count > 0
				? GetHumanMapScopeSourceTag(matches, CrewNodeResolutionMode.LiveAuthoritative) + "-selected-fallback"
				: GetCrewNodeResolutionSourceTag(CrewNodeResolutionMode.LiveAuthoritative);
			return matches.Count > 0;
		}

		private static bool IsCrewAvailableForLiveNodeQueries(PlayerCrew crew, CrewAssignment assignment)
		{
			if (crew == null || !assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsNotDead)
			{
				return false;
			}

			if (!crew.IsOnBoard(assignment.peepId))
			{
				return false;
			}

			try
			{
				if (GameplayTweaksPlugin.IsCrewCurrentlyJailed(assignment.peepId))
				{
					return false;
				}
			}
			catch
			{
			}

			return true;
		}

		internal static bool TryFindPendingHumanVehiclePresence(PlayerCrew crew, NodeID nodeId, bool onlyInVehicles, out CrewAssignment match)
		{
			match = CrewAssignment.EMPTY;
			if (crew == null || !crew.PID.IsHumanPlayer || !nodeId.IsValid)
			{
				return false;
			}

			CrewAssignment firstMatch = CrewAssignment.EMPTY;
			EntityID preferredDriverId = EntityID.INVALID;
			foreach (CrewAssignment assignment in crew.GetLiving())
			{
				if (!assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					continue;
				}
				if (onlyInVehicles && !assignment.IsInVehicle)
				{
					continue;
				}
				if (!crew.IsOnBoard(assignment.peepId) || IsCrewJailedForVehicleQueries(assignment.peepId))
				{
					continue;
				}
				NodeID interactiveNodeId = NodeID.INVALID;
				bool resolved = TryGetHumanVehiclePresenceNodeId(assignment.VehicleID, out interactiveNodeId, out _);
				if (!resolved || interactiveNodeId != nodeId)
				{
					continue;
				}
				if (!firstMatch.IsValid)
				{
					firstMatch = assignment;
					preferredDriverId = GetDriverPeepId(crew, assignment.VehicleID);
				}
				if (preferredDriverId.IsValid && assignment.peepId == preferredDriverId)
				{
					match = assignment;
					break;
				}
			}

			if (!match.IsValid && firstMatch.IsValid)
			{
				match = firstMatch;
			}
			return match.IsValid;
		}

		private static bool TryGetHumanVehiclePresenceNodeId(EntityID vehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}

			TryNormalizeFreshStartupPendingVehicleTravelForUi(vehicleId, "presence-node");

			bool hasPendingRouteState = TryGetPendingHumanVehicleTravelState(vehicleId, out PendingVehicleTravelState pendingState);
			bool routeAuthorityInFlight = IsHumanVehicleTravelActive(vehicleId)
				|| HasQueuedHumanVehiclePendingResume(vehicleId)
				|| (hasPendingRouteState
					&& (pendingState.ExpectedNodeID.IsValid
						|| (pendingState.ResumeQueued && pendingState.GoalNodeID.IsValid)));
			if (routeAuthorityInFlight)
			{
				if (pendingState.ExpectedNodeID.IsValid
					&& IsHumanVehiclePhysicallyAtNode(vehicleId, pendingState.ExpectedNodeID))
				{
					nodeId = pendingState.ExpectedNodeID;
					source = "physical-expected";
					return true;
				}

				if (TryGetHumanVehiclePhysicalNodeId(vehicleId, out nodeId, out source) && nodeId.IsValid)
				{
					source = "route-" + source;
					return true;
				}

				LogVehicleAuthority(
					"presence-preview-blocked",
					$"{vehicleId.id}:{pendingState.ExpectedNodeID}:{pendingState.GoalNodeID}",
					$"presence-preview-blocked vehicle={vehicleId.id} expectedNode={pendingState.ExpectedNodeID} finalGoal={pendingState.GoalNodeID} reason=route-preview-not-presence",
					dedupe: true);
				return false;
			}

			if (TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out nodeId, out source) && nodeId.IsValid)
			{
				if (source.IndexOf("final-goal", StringComparison.OrdinalIgnoreCase) >= 0
					|| source.IndexOf("current-segment", StringComparison.OrdinalIgnoreCase) >= 0
					|| source.IndexOf("pending", StringComparison.OrdinalIgnoreCase) >= 0
					|| source.IndexOf("queued", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					LogVehicleAuthority(
						"presence-preview-blocked",
						$"{vehicleId.id}:{nodeId}:{source}",
						$"presence-preview-blocked vehicle={vehicleId.id} node={nodeId} source={source} reason=preview-source",
						dedupe: true);
					nodeId = NodeID.INVALID;
					source = "none";
					return false;
				}
				return true;
			}

			if (TryGetVehicleLiveAuthorityNodeId(vehicleId, out nodeId, out source) && nodeId.IsValid)
			{
				return true;
			}

			return false;
		}

		internal static List<CrewAssignment> GetPendingHumanVehicleDriversAtNode(PlayerCrew crew, NodeID nodeId)
		{
			List<CrewAssignment> matches = new List<CrewAssignment>();
			if (crew == null || !crew.PID.IsHumanPlayer || !nodeId.IsValid)
			{
				return matches;
			}

			HashSet<long> seenVehicleIds = new HashSet<long>();
			foreach (CrewAssignment assignment in crew.GetLiving())
			{
				if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid || !assignment.peepId.IsValid)
				{
					continue;
				}
				if (!crew.IsOnBoard(assignment.peepId) || IsCrewJailedForVehicleQueries(assignment.peepId))
				{
					continue;
				}
				NodeID interactiveNodeId = NodeID.INVALID;
				bool resolved = TryGetHumanVehiclePresenceNodeId(assignment.VehicleID, out interactiveNodeId, out _);
				if (!resolved || interactiveNodeId != nodeId || !seenVehicleIds.Add((long)assignment.VehicleID.id))
				{
					continue;
				}
				EntityID driverId = GetDriverPeepId(crew, assignment.VehicleID);
				CrewAssignment driverAssignment = driverId.IsValid ? crew.GetCrewForPeep(driverId) : CrewAssignment.EMPTY;
				if (driverAssignment.IsValid && driverAssignment.VehicleID == assignment.VehicleID && IsActiveVehicleOccupant(crew, driverAssignment))
				{
					matches.Add(driverAssignment);
				}
				else
				{
					matches.Add(assignment);
				}
			}

			return matches;
		}

		internal static void LogArrivalPresenceSuppressed(string caller, CrewAssignment match, NodeID nodeId)
		{
			if (!match.IsValid || !nodeId.IsValid)
			{
				return;
			}
			LogVehicleAuthority("arrival-presence-suppressed", $"{caller}:{match.VehicleID.id}:{nodeId}", $"arrival-presence-suppressed caller={caller} crew={match.peepId.id} vehicle={match.VehicleID.id} node={nodeId} reason=pending-not-real");
		}

		internal static void TryRefreshCrewHudCardsForVehicle(EntityID vehicleId)
		{
			if (!vehicleId.IsValid)
				return;
			try
			{
				CrewDialog crewHud = global::Game.Game.ctx?.hud?.crew;
				if (crewHud == null)
					return;

				List<CrewCardContext> allCards = CrewDialogAllCardsField?.GetValue(crewHud) as List<CrewCardContext>;
				if (allCards == null)
					return;

				foreach (CrewCardContext card in allCards)
				{
					if (card?.data == null)
						continue;
					if (card.data.type != CrewCardType.CrewUnassigned
						&& !(card.data.type == CrewCardType.CrewMuscle && card.data.crew.IsInVehicle && card.data.crew.VehicleID == vehicleId))
						continue;

					try
					{
						card.RefreshCard();
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] TryRefreshCrewHudCardsForVehicle card refresh: " + ex.Message);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryRefreshCrewHudCardsForVehicle: " + ex.Message);
			}
		}

		private sealed class DeferredCrewHudVehicleRefresh
		{
			internal int Day;
			internal int QueuedFrame;
			internal int EarliestFrame;
			internal EntityID VehicleId = EntityID.INVALID;
			internal NodeID FinalNodeId = NodeID.INVALID;
			internal string Source = string.Empty;
		}

		private static readonly Dictionary<long, DeferredCrewHudVehicleRefresh> DeferredCrewHudRefreshByVehicle = new Dictionary<long, DeferredCrewHudVehicleRefresh>();
		private static readonly bool EnableRoutineCrewDisplayRefreshLog =
			string.Equals(Environment.GetEnvironmentVariable("COG_VERBOSE_VEHICLE_AUTHORITY"), "1", StringComparison.Ordinal);

		internal static void QueueDeferredCrewHudStateAfterTravel(EntityID vehicleId, NodeID finalNodeId, string sourceTag, int delayFrames = 8)
		{
			TryRefreshCrewHudStateAfterTravelInternal(vehicleId, finalNodeId, deferCardRefresh: true, sourceTag, delayFrames);
		}

		internal static void FlushDeferredCrewHudVehicleRefreshes(string sourceTag)
		{
			if (DeferredCrewHudRefreshByVehicle.Count == 0)
			{
				return;
			}

			int today = G.GetNow().days;
			long readyKey = 0L;
			int readyQueuedFrame = int.MaxValue;
			foreach (KeyValuePair<long, DeferredCrewHudVehicleRefresh> pair in DeferredCrewHudRefreshByVehicle)
			{
				DeferredCrewHudVehicleRefresh candidate = pair.Value;
				if (candidate == null || candidate.Day != today)
				{
					readyKey = pair.Key;
					break;
				}
				if (Time.frameCount < Math.Max(candidate.QueuedFrame + 1, candidate.EarliestFrame))
				{
					continue;
				}
				if (candidate.QueuedFrame < readyQueuedFrame)
				{
					readyKey = pair.Key;
					readyQueuedFrame = candidate.QueuedFrame;
				}
			}

			if (readyKey == 0L)
			{
				return;
			}

			if (DeferredCrewHudRefreshByVehicle.TryGetValue(readyKey, out DeferredCrewHudVehicleRefresh refresh) && refresh != null)
			{
				long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				try
				{
					TryRefreshCrewHudCardsForVehicle(refresh.VehicleId);
				}
				finally
				{
					DeferredCrewHudRefreshByVehicle.Remove(readyKey);
					long elapsedMs = GetElapsedMs(startTicks);
					if (elapsedMs >= 4L)
					{
						Debug.Log($"[PERF][DeferredCrewHudVehicleRefresh] ms={elapsedMs} vehicle={refresh.VehicleId.id} finalNode={refresh.FinalNodeId} source={refresh.Source} trigger={sourceTag} frame={Time.frameCount}");
					}
					LogVehicleAuthority(
						"crew-display-refresh-flushed",
						$"{refresh.VehicleId.id}:{refresh.FinalNodeId}:{refresh.Source}",
						$"crew-display-refresh-flushed vehicle={refresh.VehicleId.id} finalNode={refresh.FinalNodeId} source={refresh.Source} trigger={sourceTag}",
						dedupe: false);
				}
			}
			else
			{
				DeferredCrewHudRefreshByVehicle.Remove(readyKey);
			}
		}

		internal static void TryRefreshCrewHudStateAfterTravel(EntityID vehicleId, NodeID finalNodeId)
		{
			TryRefreshCrewHudStateAfterTravelInternal(vehicleId, finalNodeId, deferCardRefresh: false, "immediate", 0);
		}

		private static void TryRefreshCrewHudStateAfterTravelInternal(EntityID vehicleId, NodeID finalNodeId, bool deferCardRefresh, string sourceTag, int delayFrames)
		{
			if (!vehicleId.IsValid || !finalNodeId.IsValid)
			{
				return;
			}

			bool selected = false;
			try
			{
				SelectionManager selection = global::Game.Game.ctx?.selection;
				Entity currentActive = selection?.CurrentActive;
				if (currentActive != null)
				{
					if (currentActive.Id == vehicleId)
					{
						selected = true;
					}
					else
					{
						PlayerCrew humanCrew = G.GetHumanCrew();
						CrewAssignment selectedCrew = humanCrew != null ? humanCrew.GetCrewForPeep(currentActive.Id) : CrewAssignment.EMPTY;
						selected = selectedCrew.IsValid && selectedCrew.IsInVehicle && selectedCrew.VehicleID == vehicleId;
					}
				}
				if (!selected
					&& GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(vehicleId, out NodeID rememberedFinalNodeId)
					&& rememberedFinalNodeId.IsValid
					&& rememberedFinalNodeId == finalNodeId)
				{
					selected = true;
				}
				if (selected)
				{
					RememberSelectedHumanVehicle(vehicleId);
				}

				if (deferCardRefresh)
				{
					QueueDeferredCrewHudCardsForVehicle(vehicleId, finalNodeId, sourceTag, delayFrames);
				}
				else
				{
					TryRefreshCrewHudCardsForVehicle(vehicleId);
				}
				GameplayTweaksPlugin.PactColorUiPatch.RequestFullCrewPickRefresh("vehicle-travel", 18);
				if (!deferCardRefresh || EnableRoutineCrewDisplayRefreshLog)
				{
					LogVehicleAuthority(
						deferCardRefresh ? "crew-display-refresh-deferred" : "crew-display-refresh",
						$"{vehicleId.id}:{finalNodeId}:{selected}:{sourceTag}",
						$"crew-display-refresh{(deferCardRefresh ? "-deferred" : string.Empty)} vehicle={vehicleId.id} selected={selected} finalNode={finalNodeId} source={sourceTag}",
						dedupe: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryRefreshCrewHudStateAfterTravel: " + ex.Message);
			}
		}

		private static void QueueDeferredCrewHudCardsForVehicle(EntityID vehicleId, NodeID finalNodeId, string sourceTag, int delayFrames)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			int earliestFrame = Time.frameCount + Math.Max(1, delayFrames);
			if (DeferredCrewHudRefreshByVehicle.TryGetValue(vehicleKey, out DeferredCrewHudVehicleRefresh existing)
				&& existing != null
				&& existing.Day == G.GetNow().days
				&& existing.FinalNodeId == finalNodeId)
			{
				existing.EarliestFrame = Math.Min(existing.EarliestFrame, earliestFrame);
				return;
			}

			DeferredCrewHudRefreshByVehicle[vehicleKey] = new DeferredCrewHudVehicleRefresh
			{
				Day = G.GetNow().days,
				QueuedFrame = Time.frameCount,
				EarliestFrame = earliestFrame,
				VehicleId = vehicleId,
				FinalNodeId = finalNodeId,
				Source = string.IsNullOrWhiteSpace(sourceTag) ? "unknown" : sourceTag
			};
		}

		internal static void HideCornerInfoIfShowingNode(NodeID nodeId)
		{
			if (!nodeId.IsValid)
			{
				return;
			}
			try
			{
				CornerInfoDialog cornerInfo = global::Game.Game.ctx?.hud?.cornerInfo;
				if (cornerInfo == null || !cornerInfo.IsShowing)
				{
					return;
				}
				FieldInfo nodeField = typeof(CornerInfoDialog).GetField("_node", BindingFlags.Instance | BindingFlags.NonPublic);
				Node dialogNode = nodeField?.GetValue(cornerInfo) as Node;
				if (dialogNode != null && dialogNode.id == nodeId)
				{
					cornerInfo.Hide();
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HideCornerInfoIfShowingNode: " + ex.Message);
			}
		}

		internal static void LogPickupDirectAssign(EntityID vehicleId, EntityID peepId)
		{
			if (_loggedPickupDirectAssignOnce)
				return;
			_loggedPickupDirectAssignOnce = true;
			Debug.Log($"[GameplayTweaks] Pick up direct assign vehicle={vehicleId.id} peep={peepId.id}");
		}

		internal static void LogPickupSelector(EntityID peepId, int vehicleCount)
		{
			if (_loggedPickupSelectorOnce)
				return;
			_loggedPickupSelectorOnce = true;
			Debug.Log($"[GameplayTweaks] Pick up selector path peep={peepId.id} vehicles={vehicleCount}");
		}

		internal static void LogPickupAssignStickFailure(EntityID vehicleId, EntityID peepId)
		{
			if (_loggedPickupAssignStickFailureOnce)
				return;
			_loggedPickupAssignStickFailureOnce = true;
			Debug.LogWarning($"[GameplayTweaks] Pick up assignment did not stick vehicle={vehicleId.id} peep={peepId.id}");
		}

		internal static bool TryBeginSetDriverUi()
		{
			if (_setDriverUiInProgress)
				return false;
			_setDriverUiInProgress = true;
			return true;
		}

		internal static void EndSetDriverUi()
		{
			_setDriverUiInProgress = false;
		}

		internal static void AssertFounderIdentityStable(PlayerCrew crew, EntityID expectedFounderPeepId, string source)
		{
			if (_loggedFounderIdentityDriftOnce || crew == null || !expectedFounderPeepId.IsValid)
				return;
			try
			{
				EntityID currentFounderPeepId = crew.GetCrewForPlayerPeep().peepId;
				if (currentFounderPeepId.IsValid && currentFounderPeepId != expectedFounderPeepId)
				{
					_loggedFounderIdentityDriftOnce = true;
					Debug.LogWarning($"[GameplayTweaks] Founder identity drift detected after {source}: expected={expectedFounderPeepId.id} actual={currentFounderPeepId.id}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] AssertFounderIdentityStable failed after {source}: {ex.Message}");
			}
		}

		/// <summary>Create a button that mirrors the HUD "Crew Command Button" (start driving / scope out style). Returns null if template unavailable.</summary>
		internal static GameObject CreateButtonFromCommandTemplate(Transform parent, string goName, string label)
		{
			try
			{
				object crewHud = global::Game.Game.ctx?.hud?.crew;
				if (crewHud == null)
					return null;
				PropertyInfo prop = crewHud.GetType().GetProperty("CommandButtonTemplate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				GameObject tmpl = prop?.GetValue(crewHud) as GameObject;
				if (tmpl == null)
					return null;
				GameObject clone = (GameObject)UnityEngine.Object.Instantiate(tmpl);
				clone.name = goName;
				clone.transform.SetParent(parent, false);
				Button cloneButton = clone.GetComponent<Button>();
				if (cloneButton != null)
				{
					// Prevent inherited template listeners from firing on custom mod buttons.
					cloneButton.onClick = new Button.ButtonClickedEvent();
				}
				Transform textTr = clone.transform.Find("Text");
				if (textTr != null)
				{
					Text t = textTr.GetComponent<Text>();
					if (t != null)
						t.text = label;
					TMPro.TMP_Text tmp = textTr.GetComponent<TMPro.TMP_Text>();
					if (tmp != null)
						tmp.text = label;
				}
				return clone;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CreateButtonFromCommandTemplate: " + ex.Message);
				return null;
			}
		}

		/// <summary>Vehicles that have fewer than their crew slot limit (for assignment UI).</summary>
		public static IEnumerable<EntityID> GetVehiclesWithSpace(PlayerCrew crew)
		{
			if (crew == null)
				yield break;
			foreach (EntityID eid in crew.AllVehicles)
			{
				int slots = GetVehicleCrewSlots(eid);
				if (GetVehicleCrewCount(crew, eid) < slots)
					yield return eid;
			}
		}

		/// <summary>Vehicles with space that are parked at an owned building (for assign-at-building UI).</summary>
		public static IEnumerable<EntityID> GetVehiclesWithSpaceAtOwnedBuilding(PlayerCrew crew)
		{
			if (crew == null)
				yield break;
			foreach (EntityID eid in GetVehiclesWithSpace(crew))
			{
				if (!TryIsVehicleAtOwnedBuilding(eid, out bool atOwned))
					continue;
				if (atOwned)
					yield return eid;
			}
		}

		internal static bool TryIsVehicleAtOwnedBuilding(EntityID vehicleId, out bool atOwned)
		{
			atOwned = false;
			try
			{
				Entity vehicle = vehicleId.FindEntity();
				atOwned = IsVehicleAtOwnedBuilding(vehicle);
				return true;
			}
			catch (Exception ex)
			{
				if (!_loggedOwnedBuildingProbeFallbackOnce)
				{
					_loggedOwnedBuildingProbeFallbackOnce = true;
					Debug.LogWarning("[GameplayTweaks] TryIsVehicleAtOwnedBuilding fallback: " + ex.Message);
				}
				return false;
			}
		}

		internal static bool TryGetScoutableUnknownNodes(EntityID peepId, out List<Node> result, out string failureReason)
		{
			result = new List<Node>();
			failureReason = "Scout failed.";
			try
			{
				PlayerCrew crew = G.GetHumanCrew();
				if (crew == null)
				{
					failureReason = "Crew is no longer available.";
					return false;
				}
				CrewAssignment assignment = crew.GetCrewForPeep(peepId);
				if (!assignment.IsValid || !assignment.peepId.IsValid)
				{
					failureReason = "Crew is no longer available.";
					return false;
				}
				if (!assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					failureReason = "Crew must be in a vehicle to scout.";
					return false;
				}
				if (IsDriver(crew, assignment))
				{
					failureReason = "Only passengers can scout.";
					return false;
				}
				if (!TryGetCommittedHumanVehicleInteractiveNode(assignment.VehicleID, out Node vehicleNode, out _))
				{
					failureReason = "Vehicle location is updating. Try again.";
					return false;
				}
				var neighbors = new List<Node>();
				vehicleNode.FindAllNeighbors(neighbors);
				foreach (Node node in neighbors)
				{
					if (node != null && !node.known.Get(PlayerID.HumanPlayer))
					{
						result.Add(node);
					}
				}
				if (result.Count <= 0)
				{
					failureReason = "No unknown corners nearby.";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] TryGetScoutableUnknownNodes failed: {ex.Message}");
				failureReason = "Scout failed.";
				result.Clear();
				return false;
			}
		}

		/// <summary>Scout one adjacent node (passenger spends 7 MP to reveal it). Returns true if successful.</summary>
		internal static bool TryScoutOneNode(EntityID peepId, Node targetNode)
		{
			return TryScoutOneNode(peepId, targetNode, out _);
		}

		/// <summary>Scout one adjacent node (passenger spends 7 MP to reveal it). Returns true if successful.</summary>
		internal static bool TryScoutOneNode(EntityID peepId, Node targetNode, out string failureReason)
		{
			failureReason = "Scout failed.";
			try
			{
				if (targetNode == null)
				{
					failureReason = "No unknown corners nearby.";
					return false;
				}
				PlayerCrew crew = G.GetHumanCrew();
				if (crew == null)
				{
					failureReason = "Crew is no longer available.";
					return false;
				}
				CrewAssignment assignment = crew.GetCrewForPeep(peepId);
				if (!assignment.IsValid || !assignment.peepId.IsValid)
				{
					failureReason = "Crew is no longer available.";
					return false;
				}
				if (!assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					failureReason = "Crew must be in a vehicle to scout.";
					return false;
				}
				if (IsDriver(crew, assignment))
				{
					failureReason = "Only passengers can scout.";
					return false;
				}
				if (!TryGetCommittedHumanVehicleInteractiveNode(assignment.VehicleID, out Node vehicleNode, out _))
				{
					failureReason = "Vehicle location is updating. Try again.";
					return false;
				}
				if (!TryGetScoutableUnknownNodes(peepId, out List<Node> unknownNodes, out failureReason))
				{
					return false;
				}
				if (!unknownNodes.Contains(targetNode))
				{
					failureReason = "That corner can no longer be scouted.";
					return false;
				}
				PlayerInfo human = G.GetHumanPlayer();
				if (human?.meetings == null)
				{
					failureReason = "Scout failed.";
					return false;
				}
				if (targetNode.known.Get(PlayerID.HumanPlayer))
				{
					failureReason = "That corner is already known.";
					return false;
				}
				Entity peep = peepId.FindEntity();
				const int ScoutMoveCost = 7;
				if (peep?.data?.agent == null)
				{
					failureReason = "Crew is no longer available.";
					return false;
				}
				if (peep.data.agent.movesLeft < ScoutMoveCost)
				{
					failureReason = $"Need {ScoutMoveCost} MP to scout.";
					return false;
				}
				peep.data.agent.movesLeft -= ScoutMoveCost;
				if (_humanPassengerUncappedMovesByPeepId.TryGetValue((long)peepId.id, out int rememberedMoves))
				{
					_humanPassengerUncappedMovesByPeepId[(long)peepId.id] = Math.Max(0, rememberedMoves - ScoutMoveCost);
				}
				NotifyCrewActionsChanged(peepId);
				human.meetings.MarkNodeAsKnown(targetNode, expectedSeen: true, instant: true);
				failureReason = string.Empty;
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] TryScoutOneNode failed: {ex.Message}");
				failureReason = "Scout failed.";
				return false;
			}
		}

		/// <summary>True when assigning crew via "Pick up stranded vehicle" flow; allows assign to unoccupied vehicle not at owned building.</summary>
		internal static bool PickUpStrandedVehicleInProgress { get; set; }

		/// <summary>Vehicles with 0 crew that are not at an owned building (stranded); can be "picked up" by crew not in a vehicle.</summary>
		public static IEnumerable<EntityID> GetStrandedVehicles(PlayerCrew crew)
		{
			if (crew == null)
				yield break;
			foreach (EntityID eid in crew.AllVehicles)
			{
				if (GetVehicleCrewCount(crew, eid) != 0)
					continue;
				if (!TryIsVehicleAtOwnedBuilding(eid, out bool atOwned))
				{
					// If ownership-location probe fails, treat as stranded to keep pickup flow usable.
					yield return eid;
					continue;
				}
				if (atOwned)
					continue;
				yield return eid;
			}
		}

		/// <summary>Adjacent nodes to the passenger's vehicle that are not yet known to the human player.</summary>
		internal static List<Node> GetAdjacentUnknownNodes(EntityID peepId)
		{
			return TryGetScoutableUnknownNodes(peepId, out List<Node> result, out _)
				? result
				: new List<Node>();
		}
	}

	internal static class HumanCrewPickDriverPortraitPatch
	{
		private static readonly FieldInfo CrewField = AccessTools.Field(typeof(CrewPick), "_crew");
		private static readonly FieldInfo PeepField = AccessTools.Field(typeof(CrewPick), "_peep");
		private static readonly FieldInfo PeepSpriteField = AccessTools.Field(typeof(CrewPick), "_peepSprite");

		[HarmonyPostfix]
		internal static void SetTargetPostfix(CrewPick __instance)
		{
			NormalizeHumanVehicleDriverPortrait(__instance, "set-target");
		}

		[HarmonyPrefix]
		internal static void RefreshContentsPrefix(CrewPick __instance)
		{
			NormalizeHumanVehicleDriverPortrait(__instance, "refresh");
		}

		private static void NormalizeHumanVehicleDriverPortrait(CrewPick pick, string source)
		{
			try
			{
				if (pick == null)
				{
					return;
				}

				Entity vehicle = pick.Target.FindEntity();
				if (vehicle?.data?.mobile == null || !vehicle.data.mobile.pid.IsHumanPlayer)
				{
					return;
				}

				PlayerCrew humanCrew = vehicle.data.mobile.pid.FindPlayer()?.crew ?? G.GetHumanCrew();
				if (humanCrew == null)
				{
					return;
				}

				EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, vehicle.Id);
				CrewAssignment driverAssignment = driverPeepId.IsValid ? humanCrew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
				if (!driverAssignment.IsValid
					|| !driverAssignment.IsInVehicle
					|| driverAssignment.VehicleID != vehicle.Id
					|| !MultiCrewVehicleHelper.IsActiveVehicleOccupant(humanCrew, driverAssignment))
				{
					return;
				}

				Entity driverPeep = driverPeepId.FindEntity();
				if (driverPeep == null)
				{
					return;
				}

				if (PeepField?.GetValue(pick) is Entity currentPeep && currentPeep.Id == driverPeepId)
				{
					return;
				}

				CrewField?.SetValue(pick, driverAssignment);
				PeepField?.SetValue(pick, driverPeep);
				PeepSpriteField?.SetValue(pick, HUDUtil.GetCrewSprite(driverPeep));
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"crew-pick-driver-portrait vehicle={vehicle.Id.id} driver={driverPeepId.id} source={source}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanCrewPickDriverPortraitPatch: " + ex.Message);
			}
		}
	}

	internal static class MultiCrewVehiclePatches
	{
		public static void ApplyPatches(Harmony harmony)
		{
			var crewPickSetTarget = typeof(CrewPick).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PickTarget) }, null);
			if (crewPickSetTarget != null)
			{
				harmony.Patch(crewPickSetTarget, postfix: new HarmonyMethod(typeof(HumanCrewPickDriverPortraitPatch), nameof(HumanCrewPickDriverPortraitPatch.SetTargetPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewPick driver portrait SetTarget patch applied");
			}
			var crewPickRefresh = typeof(CrewPick).GetMethod("RefreshContents", BindingFlags.Instance | BindingFlags.Public);
			if (crewPickRefresh != null)
			{
				harmony.Patch(crewPickRefresh, prefix: new HarmonyMethod(typeof(HumanCrewPickDriverPortraitPatch), nameof(HumanCrewPickDriverPortraitPatch.RefreshContentsPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewPick driver portrait refresh patch applied");
			}
			MethodInfo selectionSetActive = typeof(SelectionManager).GetMethod("SetActive", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Entity) }, null);
			if (selectionSetActive != null)
			{
				harmony.Patch(selectionSetActive, finalizer: new HarmonyMethod(typeof(SelectionManagerStaleEntityPatch), nameof(SelectionManagerStaleEntityPatch.SetActiveFinalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: SelectionManager.SetActive stale entity guard applied");
			}

			// AllUnassignedVehicles: show vehicles with < 4 crew so player can add more
			var getter = typeof(PlayerCrew).GetProperty("AllUnassignedVehicles")?.GetGetMethod();
			if (getter != null)
			{
				harmony.Patch(getter, prefix: new HarmonyMethod(typeof(AllUnassignedVehiclesPatch), nameof(AllUnassignedVehiclesPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AllUnassignedVehicles patch applied");
			}

			// DestroyAndUntrackVehicle: only destroy when no crew left in vehicle
			var destroyMethod = typeof(PlayerCrew).GetMethod("DestroyAndUntrackVehicle",
				HarmonyLib.AccessTools.all,
				null,
				new[] { typeof(EntityID), typeof(bool) },
				null);
			if (destroyMethod != null)
			{
				harmony.Patch(destroyMethod, prefix: new HarmonyMethod(typeof(DestroyVehiclePatch), nameof(DestroyVehiclePatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: DestroyAndUntrackVehicle patch applied");
			}

			// AssignCrewToVehicle: allow only when vehicle has space, and (for human) only at owned buildings
			var assignMethod = typeof(PlayerCrew).GetMethod("AssignCrewToVehicle",
				HarmonyLib.AccessTools.all,
				null,
				new[] { typeof(EntityID), typeof(EntityID) },
				null);
			if (assignMethod != null)
			{
				harmony.Patch(assignMethod, prefix: new HarmonyMethod(typeof(AssignCrewToVehiclePatch), nameof(AssignCrewToVehiclePatch.Prefix)), postfix: new HarmonyMethod(typeof(AssignCrewToVehiclePatch), nameof(AssignCrewToVehiclePatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AssignCrewToVehicle guard applied");
			}
			var assignBuildingMethod = typeof(PlayerCrew).GetMethod("AssignCrewToBuilding",
				HarmonyLib.AccessTools.all,
				null,
				new[] { typeof(EntityID), typeof(EntityID) },
				null);
			if (assignBuildingMethod != null)
			{
				harmony.Patch(assignBuildingMethod, prefix: new HarmonyMethod(typeof(AssignCrewToBuildingPatch), nameof(AssignCrewToBuildingPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AssignCrewToBuilding guard applied");
			}

			var hireNewCrewInVehicle = typeof(PlayerCrew).GetMethod("HireNewCrewInVehicle",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(Node), typeof(Entity), typeof(Entity), typeof(bool) },
				null);
			if (hireNewCrewInVehicle != null)
			{
				harmony.Patch(
					hireNewCrewInVehicle,
					prefix: new HarmonyMethod(typeof(AiHireNewCrewInVehiclePatch), nameof(AiHireNewCrewInVehiclePatch.Prefix)),
					postfix: new HarmonyMethod(typeof(AiHireNewCrewInVehiclePatch), nameof(AiHireNewCrewInVehiclePatch.Postfix)),
					finalizer: new HarmonyMethod(typeof(AiHireNewCrewInVehiclePatch), nameof(AiHireNewCrewInVehiclePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AI HireNewCrewInVehicle normalization applied");
			}

			var hireNewCrewMemberUnassigned = typeof(PlayerCrew).GetMethod("HireNewCrewMemberUnassigned",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(Entity), typeof(Entity) },
				null);
			if (hireNewCrewMemberUnassigned != null)
			{
				harmony.Patch(
					hireNewCrewMemberUnassigned,
					prefix: new HarmonyMethod(typeof(AiHireNewCrewMemberUnassignedPatch), nameof(AiHireNewCrewMemberUnassignedPatch.Prefix)),
					postfix: new HarmonyMethod(typeof(AiHireNewCrewMemberUnassignedPatch), nameof(AiHireNewCrewMemberUnassignedPatch.Postfix)),
					finalizer: new HarmonyMethod(typeof(AiHireNewCrewMemberUnassignedPatch), nameof(AiHireNewCrewMemberUnassignedPatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AI HireNewCrewMemberUnassigned normalization applied");
			}

			var hireNewCrewInSpecificVehicle = typeof(PlayerCrew).GetMethod("HireNewCrewInSpecificVehicle",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(Node), typeof(Entity), typeof(Entity), typeof(Label) },
				null);
			if (hireNewCrewInSpecificVehicle != null)
			{
				harmony.Patch(
					hireNewCrewInSpecificVehicle,
					prefix: new HarmonyMethod(typeof(AiHireNewCrewInSpecificVehiclePatch), nameof(AiHireNewCrewInSpecificVehiclePatch.Prefix)),
					postfix: new HarmonyMethod(typeof(AiHireNewCrewInSpecificVehiclePatch), nameof(AiHireNewCrewInSpecificVehiclePatch.Postfix)),
					finalizer: new HarmonyMethod(typeof(AiHireNewCrewInSpecificVehiclePatch), nameof(AiHireNewCrewInSpecificVehiclePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AI HireNewCrewInSpecificVehicle stabilization applied");
			}
			var isEligibleCrewMember = typeof(PlayerSocial).GetMethod("IsEligibleCrewMember",
				BindingFlags.Static | BindingFlags.Public,
				null,
				new[] { typeof(SimTime), typeof(Entity) },
				null);
			if (isEligibleCrewMember != null)
			{
				harmony.Patch(isEligibleCrewMember, postfix: new HarmonyMethod(typeof(CrewHireEligibilityPatch), nameof(CrewHireEligibilityPatch.IsEligibleCrewMemberPostfix)));
				Debug.Log("[GameplayTweaks] Crew hire: strict neutral candidate eligibility applied");
			}
			var getBestCrewCandidateFrom = typeof(PlayerCrewGrowth).GetMethod("GetBestCrewCandidateFrom", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(EntityID) }, null);
			if (getBestCrewCandidateFrom != null)
			{
				harmony.Patch(getBestCrewCandidateFrom, postfix: new HarmonyMethod(typeof(CrewHireEligibilityPatch), nameof(CrewHireEligibilityPatch.GetBestCrewCandidateFromPostfix)));
				Debug.Log("[GameplayTweaks] Crew hire: strict candidate replacement applied");
			}
			var crewHireEnabled = typeof(ConvoDataCrewHire).GetMethod("IsConvoStepEnabled", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(VisitState), typeof(ConvoButtonState) }, null);
			if (crewHireEnabled != null)
			{
				harmony.Patch(crewHireEnabled, postfix: new HarmonyMethod(typeof(CrewHireEligibilityPatch), nameof(CrewHireEligibilityPatch.ConvoDataCrewHireEnabledPostfix)));
				Debug.Log("[GameplayTweaks] Crew hire: convo enable guard applied");
			}
			var showCrewHirePopup = typeof(ConvoCallbacks).GetMethod("ShowCrewHirePopup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(ConvoButton) }, null);
			if (showCrewHirePopup != null)
			{
				harmony.Patch(showCrewHirePopup, prefix: new HarmonyMethod(typeof(CrewHireEligibilityPatch), nameof(CrewHireEligibilityPatch.ShowCrewHirePopupPrefix)));
				Debug.Log("[GameplayTweaks] Crew hire: popup guard applied");
			}
			var executeCrewHire = typeof(ConvoCallbacks).GetMethod("ExecuteCrewHire", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(ConvoButton) }, null);
			if (executeCrewHire != null)
			{
				harmony.Patch(executeCrewHire, prefix: new HarmonyMethod(typeof(CrewHireEligibilityPatch), nameof(CrewHireEligibilityPatch.ExecuteCrewHirePrefix)));
				Debug.Log("[GameplayTweaks] Crew hire: execution guard applied");
			}

			var addToCrewUnassigned = typeof(PlayerCrew).GetMethod("AddToCrewUnassigned",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(Entity), typeof(Entity), typeof(bool) },
				null);
			if (addToCrewUnassigned != null)
			{
				harmony.Patch(addToCrewUnassigned, postfix: new HarmonyMethod(typeof(AiAddToCrewUnassignedPatch), nameof(AiAddToCrewUnassignedPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AI AddToCrewUnassigned normalization applied");
			}

			var createVehicleAndAssignCrew = typeof(PlayerCrew).GetMethod("CreateVehicleAndAssignCrew",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(Node), typeof(Entity), typeof(bool) },
				null);
			if (createVehicleAndAssignCrew != null)
			{
				harmony.Patch(
					createVehicleAndAssignCrew,
					prefix: new HarmonyMethod(typeof(StartupFounderVehicleCreatePatch), nameof(StartupFounderVehicleCreatePatch.Prefix)),
					postfix: new HarmonyMethod(typeof(StartupFounderVehicleCreatePatch), nameof(StartupFounderVehicleCreatePatch.Postfix)),
					finalizer: new HarmonyMethod(typeof(StartupFounderVehicleCreatePatch), nameof(StartupFounderVehicleCreatePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: startup founder vehicle capture applied");
			}

			// PlayerCrew.FindPeepAssignedToVehicle: for human vehicles, prefer explicit mapped driver.
			var findPeepAssignedToVehicle = typeof(PlayerCrew).GetMethod("FindPeepAssignedToVehicle",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(EntityID) },
				null);
			if (findPeepAssignedToVehicle != null)
			{
				harmony.Patch(findPeepAssignedToVehicle, prefix: new HarmonyMethod(typeof(DriverMappedCrewLookupPatch), nameof(DriverMappedCrewLookupPatch.FindPeepAssignedToVehiclePrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: FindPeepAssignedToVehicle mapped-driver override applied");
			}
			var findFirstCrewAtLocation = typeof(PlayerCrew).GetMethod("FindFirstCrewAtLocation", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(NodeID), typeof(bool) }, null);
			if (findFirstCrewAtLocation != null)
			{
				harmony.Patch(findFirstCrewAtLocation, postfix: new HarmonyMethod(typeof(HumanFindFirstCrewAtLocationPatch), nameof(HumanFindFirstCrewAtLocationPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: FindFirstCrewAtLocation pending-arrival suppression patch applied");
			}
			var findAllDriversAtNode = typeof(PlayerCrew).GetMethod("FindAllDriversAtNode", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(NodeID) }, null);
			if (findAllDriversAtNode != null)
			{
				harmony.Patch(findAllDriversAtNode, postfix: new HarmonyMethod(typeof(HumanFindAllDriversAtNodePatch), nameof(HumanFindAllDriversAtNodePatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: FindAllDriversAtNode pending-arrival suppression patch applied");
			}
			var showCrewSelector = typeof(EntitySelectionPopup).GetMethod("ShowCrewSelector", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(NodeID), typeof(string), typeof(Action<EntityID>), typeof(Action<EntityID>) }, null);
			if (showCrewSelector != null)
			{
				harmony.Patch(showCrewSelector, prefix: new HarmonyMethod(typeof(HumanCrewSelectorNodePatch), nameof(HumanCrewSelectorNodePatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: EntitySelectionPopup.ShowCrewSelector node fallback applied");
			}
			var isAnyCrewAtThisNode = typeof(BuildingComponent).GetMethod("IsAnyCrewAtThisNode", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PlayerID) }, null);
			if (isAnyCrewAtThisNode != null)
			{
				harmony.Patch(isAnyCrewAtThisNode, prefix: new HarmonyMethod(typeof(HumanBuildingCrewPresencePatch), nameof(HumanBuildingCrewPresencePatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingComponent.IsAnyCrewAtThisNode live presence patch applied");
			}

			// PlayerCrew.GetCrewForTarget: for human vehicle targets, prefer explicit mapped driver assignment.
			var getCrewForTarget = typeof(PlayerCrew).GetMethod("GetCrewForTarget",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(EntityID) },
				null);
			if (getCrewForTarget != null)
			{
				harmony.Patch(getCrewForTarget, prefix: new HarmonyMethod(typeof(DriverMappedCrewLookupPatch), nameof(DriverMappedCrewLookupPatch.GetCrewForTargetPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: GetCrewForTarget mapped-driver override applied");
			}

			// CommandExecutor.OnPlayerTurnStarted: after vanilla refill, cap passenger movement
			var cmdTurnMethod = typeof(CommandExecutor).GetMethod("OnPlayerTurnStarted", BindingFlags.Instance | BindingFlags.Public);
			if (cmdTurnMethod != null)
			{
				harmony.Patch(
					cmdTurnMethod,
					prefix: new HarmonyMethod(typeof(PassengerMovementCapPatch), nameof(PassengerMovementCapPatch.Prefix)),
					postfix: new HarmonyMethod(typeof(PassengerMovementCapPatch), nameof(PassengerMovementCapPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: passenger movement cap applied");
			}
			var refillCrewActionsMethod = typeof(CommandExecutor).GetMethod("RefillPlayerCrewActions", BindingFlags.Instance | BindingFlags.NonPublic);
			if (refillCrewActionsMethod != null)
			{
				harmony.Patch(refillCrewActionsMethod, postfix: new HarmonyMethod(typeof(HumanVehicleTurnStartResumeAfterRefillPatch), nameof(HumanVehicleTurnStartResumeAfterRefillPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: queued human route resume after refill applied");
			}
			var automationTurnMethod = typeof(AutomationExecutor).GetMethod("OnPlayerTurnStarted", BindingFlags.Instance | BindingFlags.Public);
			if (automationTurnMethod != null)
			{
				harmony.Patch(automationTurnMethod, prefix: new HarmonyMethod(typeof(HumanVehicleAutomationDriverPatch), nameof(HumanVehicleAutomationDriverPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: human vehicle automation driver routing applied");
			}
			var handleAddingCommandMethod = typeof(CommandExecutor).GetMethod("HandleAddingCommand", BindingFlags.Instance | BindingFlags.NonPublic);
			if (handleAddingCommandMethod != null)
			{
				harmony.Patch(handleAddingCommandMethod, prefix: new HarmonyMethod(typeof(HumanVehicleCommandQueueDriverPatch), nameof(HumanVehicleCommandQueueDriverPatch.HandleAddingCommandPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: human vehicle command queue driver routing applied");
			}
			var flushQueueMethod = typeof(CommandExecutor).GetMethod("FlushQueue", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(EntityID), typeof(bool) }, null);
			if (flushQueueMethod != null)
			{
				harmony.Patch(flushQueueMethod, postfix: new HarmonyMethod(typeof(HumanVehicleFlushQueuePatch), nameof(HumanVehicleFlushQueuePatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: queued route clear on FlushQueue applied");
			}

			// CommandGoto.CanStart: only the driver can move the car on the map
			var gotoCanStart = typeof(CommandGoto).GetMethod("CanStart", BindingFlags.Instance | BindingFlags.NonPublic);
			if (gotoCanStart != null)
			{
				harmony.Patch(gotoCanStart, prefix: new HarmonyMethod(typeof(CommandGotoDriverGuardPatch), nameof(CommandGotoDriverGuardPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandGoto driver guard applied");
			}
			var setAndValidatePath = typeof(CommandGoto).GetMethod("SetAndValidatePath", BindingFlags.Instance | BindingFlags.NonPublic);
			if (setAndValidatePath != null)
			{
				harmony.Patch(setAndValidatePath, prefix: new HarmonyMethod(typeof(HumanVehicleCommandGotoPathPatch), nameof(HumanVehicleCommandGotoPathPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandGoto path override applied");
			}
			ConstructorInfo carInputModeCtor = typeof(CarInputMode).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Entity), typeof(bool) }, null);
			if (carInputModeCtor != null)
			{
				harmony.Patch(carInputModeCtor, postfix: new HarmonyMethod(typeof(CarInputModeDriverPeepPatch), nameof(CarInputModeDriverPeepPatch.ConstructorPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CarInputMode constructor driver authority applied");
			}
			MethodInfo carInputUpdateLine = typeof(CarInputMode).GetMethod("UpdateLine", BindingFlags.Instance | BindingFlags.NonPublic);
			if (carInputUpdateLine != null)
			{
				harmony.Patch(
					carInputUpdateLine,
					prefix: new HarmonyMethod(typeof(CarInputModeDriverPeepPatch), nameof(CarInputModeDriverPeepPatch.UpdateLinePrefix)),
					finalizer: new HarmonyMethod(typeof(CarInputModeDriverPeepPatch), nameof(CarInputModeDriverPeepPatch.UpdateLineFinalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CarInputMode.UpdateLine driver authority applied");
			}
			// CommandGoto.StartDrivingCar: block vehicle movement when passenger (safety net if CanStart was bypassed)
			var startDrivingCar = typeof(CommandGoto).GetMethod("StartDrivingCar", BindingFlags.Instance | BindingFlags.NonPublic);
			if (startDrivingCar != null)
			{
				harmony.Patch(startDrivingCar, prefix: new HarmonyMethod(typeof(CommandGotoDriverGuardPatch), nameof(CommandGotoDriverGuardPatch.StartDrivingCarPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: StartDrivingCar human vehicle override applied");
			}
			var commandGotoOnTurnStarted = typeof(CommandGoto).GetMethod("OnTurnStarted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (commandGotoOnTurnStarted != null)
			{
				harmony.Patch(commandGotoOnTurnStarted, prefix: new HarmonyMethod(typeof(HumanVehicleQueuedTurnStartPatch), nameof(HumanVehicleQueuedTurnStartPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandGoto.OnTurnStarted human vehicle resume ownership applied");
			}
			var canActivateAfterDequeue = typeof(CommandGoto).GetMethod("CanActivateAfterDequeue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (canActivateAfterDequeue != null)
			{
				harmony.Patch(canActivateAfterDequeue, prefix: new HarmonyMethod(typeof(HumanVehicleQueuedCanActivatePatch), nameof(HumanVehicleQueuedCanActivatePatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandGoto.CanActivateAfterDequeue human vehicle stale queue skip applied");
			}
			int commandGotoExecuteTimingMethods = HumanVehicleCommandGotoExecuteTimingPatch.Patch(harmony);
			if (commandGotoExecuteTimingMethods > 0)
			{
				Debug.Log($"[GameplayTweaks] Multi-crew vehicle: CommandGoto execute segment timing applied methods={commandGotoExecuteTimingMethods} owners=human,ai");
			}
			var findDrivingPath = typeof(TransitManager).GetMethod("FindDrivingPath", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PlayerID), typeof(Entity), typeof(WorldPos), typeof(Action<Pathfinding.Result>) }, null);
			if (findDrivingPath != null)
			{
				harmony.Patch(findDrivingPath, prefix: new HarmonyMethod(typeof(HumanVehiclePathSourcePatch), nameof(HumanVehiclePathSourcePatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: TransitManager.FindDrivingPath human vehicle source patch applied");
			}
			var actionNavigateStop = typeof(ActionNavigate).GetMethod("OnStop", BindingFlags.Instance | BindingFlags.NonPublic);
			if (actionNavigateStop != null)
			{
				harmony.Patch(actionNavigateStop, postfix: new HarmonyMethod(typeof(HumanVehicleTravelEndSyncPatch), nameof(HumanVehicleTravelEndSyncPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ActionNavigate.OnStop travel-end sync applied");
			}

			// CrewInfoGen: when crew is in vehicle, Goto and Corner use vehicle position (not peep)
			var getGotoTarget = typeof(CrewInfoGen).GetMethod("GetGotoTarget", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (getGotoTarget != null)
			{
				harmony.Patch(getGotoTarget, prefix: new HarmonyMethod(typeof(CrewInfoGenGotoCornerPatch), nameof(CrewInfoGenGotoCornerPatch.GetGotoTargetPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewInfoGen.GetGotoTarget (vehicle position) applied");
			}
			var getCornerNode = typeof(CrewInfoGen).GetMethod("GetCornerNode", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (getCornerNode != null)
			{
				harmony.Patch(getCornerNode, prefix: new HarmonyMethod(typeof(CrewInfoGenGotoCornerPatch), nameof(CrewInfoGenGotoCornerPatch.GetCornerNodePrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewInfoGen.GetCornerNode (vehicle position) applied");
			}
			var muscleTextLineBottom = typeof(CrewInfoGenMuscle).GetMethod("GetTextLineBottom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (muscleTextLineBottom != null)
			{
				harmony.Patch(muscleTextLineBottom, postfix: new HarmonyMethod(typeof(CrewCardRouteModeLabelPatch), nameof(CrewCardRouteModeLabelPatch.MuscleTextLineBottomPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: crew card route-mode label applied");
			}
			var jobTextLineBottom = typeof(CrewInfoGenJob).GetMethod("GetTextLineBottom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (jobTextLineBottom != null)
			{
				harmony.Patch(jobTextLineBottom, postfix: new HarmonyMethod(typeof(CrewCardRouteModeLabelPatch), nameof(CrewCardRouteModeLabelPatch.JobTextLineBottomPostfix)));
			}
			var emptyVehicleTextLineBottom = typeof(CrewInfoGenJustVehicle).GetMethod("GetTextLineBottom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (emptyVehicleTextLineBottom != null)
			{
				harmony.Patch(emptyVehicleTextLineBottom, postfix: new HarmonyMethod(typeof(CrewCardRouteModeLabelPatch), nameof(CrewCardRouteModeLabelPatch.EmptyVehicleTextLineBottomPostfix)));
			}
			var getCrewName = typeof(CrewInfoGen).GetMethod("GetCrewName", BindingFlags.Instance | BindingFlags.NonPublic);
			if (getCrewName != null)
			{
				harmony.Patch(getCrewName, finalizer: new HarmonyMethod(typeof(CrewInfoGenCrewNameStabilityPatch), nameof(CrewInfoGenCrewNameStabilityPatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewInfoGen.GetCrewName stale assignment guard applied");
			}
			var getEmptyVehicleName = typeof(CrewInfoGen).GetMethod("GetEmptyVehicleName", BindingFlags.Instance | BindingFlags.NonPublic);
			if (getEmptyVehicleName != null)
			{
				harmony.Patch(getEmptyVehicleName, finalizer: new HarmonyMethod(typeof(CrewInfoGenEmptyVehicleNameStabilityPatch), nameof(CrewInfoGenEmptyVehicleNameStabilityPatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewInfoGen.GetEmptyVehicleName stale vehicle guard applied");
			}

			// Crew card: show other crew in same vehicle below main portrait
			var refreshPanel = typeof(CrewCardContext).GetMethod("RefreshPanel", BindingFlags.NonPublic | BindingFlags.Instance);
			if (refreshPanel != null)
			{
				harmony.Patch(refreshPanel, postfix: new HarmonyMethod(typeof(CrewCardExtraCrewPatch), nameof(CrewCardExtraCrewPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewCardContext.RefreshPanel (extra crew row) applied");
			}
			var setCardState = typeof(CrewDialog).GetMethod("SetCardState", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(CrewCardContext), typeof(bool) }, null);
			if (setCardState != null)
			{
				harmony.Patch(
					setCardState,
					prefix: new HarmonyMethod(typeof(CrewDialogCardToggleLayoutPatch), nameof(CrewDialogCardToggleLayoutPatch.Prefix)),
					postfix: new HarmonyMethod(typeof(CrewDialogCardToggleLayoutPatch), nameof(CrewDialogCardToggleLayoutPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewDialog.SetCardState layout refresh applied");
			}
			var generateCardDataFor = typeof(CrewDialog).GetMethod("GenerateCardDataFor", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(CrewCardType) }, null);
			if (generateCardDataFor != null)
			{
				harmony.Patch(generateCardDataFor, postfix: new HarmonyMethod(typeof(DriverOnlyVehicleMuscleCardPatch), nameof(DriverOnlyVehicleMuscleCardPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: passenger muscle card visibility applied");
			}
			var refreshCards = typeof(CrewDialog).GetMethod("RefreshCards", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
			if (refreshCards != null)
			{
				harmony.Patch(refreshCards, postfix: new HarmonyMethod(typeof(CrewDialogRefreshLayoutPatch), nameof(CrewDialogRefreshLayoutPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewDialog.RefreshCards layout refresh applied");
			}
			var recreateAllCards = typeof(CrewDialog).GetMethod("RecreateAllCards", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
			if (recreateAllCards != null)
			{
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewDialog.RecreateAllCards layout refresh skipped; vanilla rebuild already forces layout");
			}
			var toggleSectionEditMode = typeof(CrewDialog).GetMethod("ToggleSectionEditMode", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(CrewCardType), typeof(GameObject) }, null);
			if (toggleSectionEditMode != null)
			{
				harmony.Patch(toggleSectionEditMode, postfix: new HarmonyMethod(typeof(CrewDialogRefreshLayoutPatch), nameof(CrewDialogRefreshLayoutPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewDialog.ToggleSectionEditMode layout refresh applied");
			}
			var clearEditMode = typeof(CrewDialog).GetMethod("ClearEditMode", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
			if (clearEditMode != null)
			{
				harmony.Patch(clearEditMode, postfix: new HarmonyMethod(typeof(CrewDialogRefreshLayoutPatch), nameof(CrewDialogRefreshLayoutPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewDialog.ClearEditMode layout refresh applied");
			}

			// Crew management popup: show crew count N/4 when a vehicle is selected
			var refreshInfoPanel = typeof(CrewManagementPopup).GetMethod("RefreshInfoPanel", BindingFlags.NonPublic | BindingFlags.Instance);
			if (refreshInfoPanel != null)
			{
				harmony.Patch(refreshInfoPanel, postfix: new HarmonyMethod(typeof(CrewMgmtCrewCountPatch), nameof(CrewMgmtCrewCountPatch.Postfix)));
				harmony.Patch(refreshInfoPanel, postfix: new HarmonyMethod(typeof(CrewMgmtPickUpStrandedPatch), nameof(CrewMgmtPickUpStrandedPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewManagementPopup crew count display applied");
			}

			// Crew management popup: disable "Add to vehicle" when no vehicle at owned building
			var buttonStatusType = typeof(CrewManagementPopup).GetNestedType("ButtonStatus", BindingFlags.NonPublic | BindingFlags.Public);
			var buttonStatusUpdate = buttonStatusType?.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
			if (buttonStatusUpdate != null)
			{
				harmony.Patch(buttonStatusUpdate, postfix: new HarmonyMethod(typeof(CrewMgmtButtonStatusPatch), nameof(CrewMgmtButtonStatusPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewManagementPopup ButtonStatus.Update (assign-at-building) applied");
			}
			var entryType = typeof(CrewManagementPopup).GetNestedType("Entry", BindingFlags.NonPublic | BindingFlags.Public);
			var isArrested = typeof(CrewManagementPopup).GetMethod("IsArrested", BindingFlags.NonPublic | BindingFlags.Instance);
			if (isArrested != null)
			{
				harmony.Patch(isArrested, postfix: new HarmonyMethod(typeof(CrewMgmtPopupJailStatePatch), nameof(CrewMgmtPopupJailStatePatch.IsArrestedPostfix)));
			}
			var isUnavailable = typeof(CrewManagementPopup).GetMethod("IsUnavailable", BindingFlags.NonPublic | BindingFlags.Instance);
			if (isUnavailable != null)
			{
				harmony.Patch(isUnavailable, postfix: new HarmonyMethod(typeof(CrewMgmtPopupJailStatePatch), nameof(CrewMgmtPopupJailStatePatch.IsUnavailablePostfix)));
			}
			var entryIsPeepUnassigned = entryType?.GetProperty("IsPeepUnassigned", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetGetMethod(true);
			if (entryIsPeepUnassigned != null)
			{
				harmony.Patch(entryIsPeepUnassigned, postfix: new HarmonyMethod(typeof(CrewMgmtPopupJailStatePatch), nameof(CrewMgmtPopupJailStatePatch.IsPeepUnassignedPostfix)));
			}
			bool afterUiCrewManagementJailVisuals = GameplayTweaksPlugin.IsAfterProhibitionUiCrewManagementJailVisualsAvailable();
			var setCardDesc = typeof(CrewManagementPopup).GetMethod("SetCardDesc", BindingFlags.NonPublic | BindingFlags.Instance);
			if (setCardDesc != null && !afterUiCrewManagementJailVisuals)
			{
				harmony.Patch(setCardDesc, postfix: new HarmonyMethod(typeof(CrewMgmtPopupJailStatePatch), nameof(CrewMgmtPopupJailStatePatch.SetCardDescPostfix)));
			}
			var setCardButtons = typeof(CrewManagementPopup).GetMethod("SetCardButtons", BindingFlags.NonPublic | BindingFlags.Instance);
			if (setCardButtons != null)
			{
				harmony.Patch(setCardButtons, postfix: new HarmonyMethod(typeof(CrewMgmtPopupJailStatePatch), nameof(CrewMgmtPopupJailStatePatch.SetCardButtonsPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewManagementPopup jail card state patches applied");
			}
			var getArrestedDesc = typeof(CrewManagementPopup).GetMethod("GetArrestedDesc", BindingFlags.NonPublic | BindingFlags.Static);
			if (getArrestedDesc != null && !afterUiCrewManagementJailVisuals)
			{
				harmony.Patch(getArrestedDesc, prefix: new HarmonyMethod(typeof(CrewMgmtPopupJailStatePatch), nameof(CrewMgmtPopupJailStatePatch.GetArrestedDescPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewManagementPopup jail description override applied");
			}
			var describeCrewPeep = typeof(CrewInfoGen).GetMethod("DescribeCrewPeep", BindingFlags.NonPublic | BindingFlags.Instance);
			if (describeCrewPeep != null && !afterUiCrewManagementJailVisuals)
			{
				harmony.Patch(describeCrewPeep, postfix: new HarmonyMethod(typeof(CrewMgmtPopupJailStatePatch), nameof(CrewMgmtPopupJailStatePatch.DescribeCrewPeepPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewInfoGen jail description override applied");
			}
			if (afterUiCrewManagementJailVisuals)
			{
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: crew management jail text visuals skipped; owner=AfterProhibitionUI");
			}

			// Crew management popup: only show vehicles at owned building in assign-to-vehicle selector
			var assignPeepToVehicle = typeof(CrewManagementPopup).GetMethod("AssignPeepToSomeVehicle", BindingFlags.NonPublic | BindingFlags.Instance);
			if (assignPeepToVehicle != null)
			{
				harmony.Patch(assignPeepToVehicle, prefix: new HarmonyMethod(typeof(AssignPeepToSomeVehiclePrefixPatch), nameof(AssignPeepToSomeVehiclePrefixPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AssignPeepToSomeVehicle (filter at owned building) applied");
			}
			var assignPeepToBuilding = typeof(CrewManagementPopup).GetMethod("AssignPeepToSomeBuilding", BindingFlags.NonPublic | BindingFlags.Instance);
			if (assignPeepToBuilding != null)
			{
				harmony.Patch(assignPeepToBuilding, prefix: new HarmonyMethod(typeof(AssignPeepToSomeBuildingPrefixPatch), nameof(AssignPeepToSomeBuildingPrefixPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: AssignPeepToSomeBuilding custody guard applied");
			}

			// VisitGrantList.DoApplyAll: keep VisitVehicle current across startup grants so starter vehicle cash/items resolve correctly.
			var doApplyAll = typeof(VisitGrantList).GetMethod("DoApplyAll", BindingFlags.NonPublic | BindingFlags.Instance);
			if (doApplyAll != null)
			{
				harmony.Patch(doApplyAll, prefix: new HarmonyMethod(typeof(VisitGrantListStartupVehiclePatch), nameof(VisitGrantListStartupVehiclePatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: VisitGrantList.DoApplyAll startup vehicle patch applied");
			}

			var createPlayersType = HarmonyLib.AccessTools.TypeByName("Game.Session.Setup.CreatePlayers");
			var grantStarterPacks = createPlayersType?.GetMethod("GrantStarterPacks", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(PlayerInfo), typeof(Entity) }, null);
			if (grantStarterPacks != null)
			{
				harmony.Patch(grantStarterPacks, prefix: new HarmonyMethod(typeof(GrantStarterPacksFounderVehiclePatch), nameof(GrantStarterPacksFounderVehiclePatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: GrantStarterPacks founder vehicle repair applied");
			}

			// GrantVehicle.Apply: avoid NRE when ctx.visit is null (e.g. during setup / starter skill with vehicle selector)
			var grantVehicleApply = typeof(GrantVehicle).GetMethod("Apply", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(GrantContext) }, null);
			if (grantVehicleApply != null)
			{
				harmony.Patch(grantVehicleApply, prefix: new HarmonyMethod(typeof(GrantVehicleApplyGuardPatch), nameof(GrantVehicleApplyGuardPatch.Prefix)));
				Debug.Log("[GameplayTweaks] GrantVehicle.Apply guard (null visit) applied");
			}

			// CommandButtonScopeOut: use authoritative scope checks for both on-foot and in-vehicle crews.
			var scopeOutValidate = typeof(CommandButtonScopeOut).GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PlayerID), typeof(CrewAssignment) }, null);
			if (scopeOutValidate != null)
			{
				harmony.Patch(scopeOutValidate, prefix: new HarmonyMethod(typeof(CommandButtonScopeOutPatch), nameof(CommandButtonScopeOutPatch.ValidatePrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandButtonScopeOut.Validate human scope patch applied");
			}
			var scopeOutPostCommand = typeof(CommandButtonScopeOut).GetMethod("PostCommandAsHuman", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(CrewAssignment) }, null);
			if (scopeOutPostCommand != null)
			{
				harmony.Patch(scopeOutPostCommand, prefix: new HarmonyMethod(typeof(CommandButtonScopeOutPatch), nameof(CommandButtonScopeOutPatch.PostCommandAsHumanPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandButtonScopeOut.PostCommandAsHuman human scope patch applied");
			}
			var scopeOutClick = typeof(CommandButtonScopeOut).GetMethod("OnHumanButtonClick", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(CrewAssignment), typeof(Entity) }, null);
			if (scopeOutClick != null)
			{
				harmony.Patch(scopeOutClick, prefix: new HarmonyMethod(typeof(CommandButtonScopeOutPatch), nameof(CommandButtonScopeOutPatch.OnHumanButtonClickPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandButtonScopeOut.OnHumanButtonClick human scope patch applied");
			}
			var buildingPickType = AccessTools.TypeByName("Game.UI.Session.Picks.BuildingPick");
			var basePickType = AccessTools.TypeByName("Game.UI.Session.Picks.BasePick");
			var pickContainerType = AccessTools.TypeByName("Game.UI.Session.Picks.PickContainer");
			var basePickSetPositionAndVisibility = basePickType?.GetMethod("SetPositionAndVisibility", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector2), typeof(bool) }, null);
			if (basePickSetPositionAndVisibility != null)
			{
				harmony.Patch(
					basePickSetPositionAndVisibility,
					prefix: new HarmonyMethod(typeof(BuildingPickPositionVisibilityPatch), nameof(BuildingPickPositionVisibilityPatch.SetPositionAndVisibilityPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPick.SetPositionAndVisibility travel visibility grace applied");
			}
			var pickContainerRefreshAll = pickContainerType != null ? AccessTools.Method(pickContainerType, "RefreshAll") : null;
			if (pickContainerRefreshAll != null)
			{
				harmony.Patch(
					pickContainerRefreshAll,
					prefix: new HarmonyMethod(typeof(BuildingPickLifecycleDiagnosticsPatch), nameof(BuildingPickLifecycleDiagnosticsPatch.RefreshAllPrefix)),
					postfix: new HarmonyMethod(typeof(BuildingPickLifecycleDiagnosticsPatch), nameof(BuildingPickLifecycleDiagnosticsPatch.RefreshAllPostfix)),
					finalizer: new HarmonyMethod(typeof(BuildingPickLifecycleDiagnosticsPatch), nameof(BuildingPickLifecycleDiagnosticsPatch.RefreshAllFinalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPick container refresh lifecycle diagnostics applied");
			}
			var pickContainerRemove = pickContainerType?.GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(PickTarget) }, null);
			if (pickContainerRemove != null)
			{
				harmony.Patch(
					pickContainerRemove,
					prefix: new HarmonyMethod(typeof(BuildingPickLifecycleDiagnosticsPatch), nameof(BuildingPickLifecycleDiagnosticsPatch.RemovePrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPick container remove diagnostics applied");
			}
			var basePickReset = basePickType?.GetMethod("Reset", BindingFlags.Instance | BindingFlags.Public);
			if (basePickReset != null)
			{
				harmony.Patch(
					basePickReset,
					prefix: new HarmonyMethod(typeof(BuildingPickLifecycleDiagnosticsPatch), nameof(BuildingPickLifecycleDiagnosticsPatch.ResetPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPick reset diagnostics applied");
			}
			var buildingPickRefreshContents = buildingPickType?.GetMethod("RefreshContents", BindingFlags.Instance | BindingFlags.Public);
			if (buildingPickRefreshContents != null)
			{
				harmony.Patch(
					buildingPickRefreshContents,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingInteractionScopePatch), nameof(HumanVehicleBuildingInteractionScopePatch.RefreshContentsPrefix)),
					postfix: new HarmonyMethod(typeof(HumanVehicleBuildingInteractionScopePatch), nameof(HumanVehicleBuildingInteractionScopePatch.RefreshContentsPostfix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingInteractionScopePatch), nameof(HumanVehicleBuildingInteractionScopePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPick.RefreshContents building interaction scope applied");
			}
			var buildingPickOnClick = buildingPickType?.GetMethod("OnClick", BindingFlags.Instance | BindingFlags.Public);
			if (buildingPickOnClick != null)
			{
				harmony.Patch(
					buildingPickOnClick,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingInteractionScopePatch), nameof(HumanVehicleBuildingInteractionScopePatch.OnClickPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingInteractionScopePatch), nameof(HumanVehicleBuildingInteractionScopePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPick.OnClick building interaction scope applied");
			}
			var buildingPickTryScopeOut = buildingPickType?.GetMethod("TryScopeOut", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Entity) }, null);
			if (buildingPickTryScopeOut != null)
			{
				harmony.Patch(buildingPickTryScopeOut, prefix: new HarmonyMethod(typeof(BuildingPickScopeOutPatch), nameof(BuildingPickScopeOutPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPick.TryScopeOut target-aware scope patch applied");
			}
			var convoShowBuySellPopup = typeof(ConvoCallbacks).GetMethod("ShowBuySellPopup", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(ConvoButton) }, null);
			if (convoShowBuySellPopup != null)
			{
				harmony.Patch(
					convoShowBuySellPopup,
					prefix: new HarmonyMethod(typeof(HumanBuySellVehiclePhysicalGatePatch), nameof(HumanBuySellVehiclePhysicalGatePatch.ShowBuySellPopupPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ConvoCallbacks.ShowBuySellPopup physical vehicle gate applied");
			}
			var buySellExecuteVisit = typeof(BuySellUtils).GetMethod("ExecuteHumanBuySell", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(PlayerInfo), typeof(VisitState), typeof(ConvoDataBuySell), typeof(QtyAndDir), typeof(bool) }, null);
			if (buySellExecuteVisit != null)
			{
				harmony.Patch(
					buySellExecuteVisit,
					prefix: new HarmonyMethod(typeof(HumanBuySellVehiclePhysicalGatePatch), nameof(HumanBuySellVehiclePhysicalGatePatch.ExecuteVisitPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuySellUtils.ExecuteHumanBuySell visit physical vehicle gate applied");
			}
			var buySellExecuteCrew = typeof(BuySellUtils).GetMethod("ExecuteHumanBuySell", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(PlayerInfo), typeof(CrewAssignment), typeof(Entity), typeof(Resource), typeof(QtyAndDir), typeof(bool) }, null);
			if (buySellExecuteCrew != null)
			{
				harmony.Patch(
					buySellExecuteCrew,
					prefix: new HarmonyMethod(typeof(HumanBuySellVehiclePhysicalGatePatch), nameof(HumanBuySellVehiclePhysicalGatePatch.ExecuteCrewPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuySellUtils.ExecuteHumanBuySell crew physical vehicle gate applied");
			}
			var ownedBizControllerType = AccessTools.TypeByName("Game.UI.Session.OwnedBiz.OwnedBizController");
			var ownedBizSetModel = ownedBizControllerType?.GetMethod("SetModel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(VisitState) }, null);
			if (ownedBizSetModel != null)
			{
				harmony.Patch(
					ownedBizSetModel,
					prefix: new HarmonyMethod(typeof(OwnedBizVehicleVisitStatePatch), nameof(OwnedBizVehicleVisitStatePatch.SetModelPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: OwnedBizController.SetModel vehicle visit normalization applied");
			}
			var ownedBizSetInventoryLoadingMode = ownedBizControllerType?.GetMethod("SetInventoryLoadingMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (ownedBizSetInventoryLoadingMode != null)
			{
				harmony.Patch(
					ownedBizSetInventoryLoadingMode,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.ControllerInventoryPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: OwnedBizController.SetInventoryLoadingMode vehicle visit normalization applied");
			}
			var ownedBizCanLoadAtLeastOne = ownedBizControllerType?.GetMethod("CanLoadAtLeastOne", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (ownedBizCanLoadAtLeastOne != null)
			{
				harmony.Patch(
					ownedBizCanLoadAtLeastOne,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.ControllerInventoryPrefix)));
				harmony.Patch(
					ownedBizCanLoadAtLeastOne,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.CanLoadAtLeastOnePrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: OwnedBizController.CanLoadAtLeastOne vehicle visit normalization applied");
			}
			var ownedBizDoPerformLoading = ownedBizControllerType?.GetMethod("DoPerformLoading", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (ownedBizDoPerformLoading != null)
			{
				harmony.Patch(
					ownedBizDoPerformLoading,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.ControllerInventoryPrefix)));
				harmony.Patch(
					ownedBizDoPerformLoading,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.DoPerformLoadingPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: OwnedBizController.DoPerformLoading vehicle visit normalization applied");
			}
			var ownedBizModuleToggleContextType = AccessTools.TypeByName("Game.UI.Session.OwnedBiz.ModuleToggleContext");
			var ownedBizOnModuleButtonClick = ownedBizModuleToggleContextType == null
				? null
				: ownedBizControllerType?.GetMethod("OnModuleButtonClick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { ownedBizModuleToggleContextType }, null);
			if (ownedBizOnModuleButtonClick != null)
			{
				harmony.Patch(
					ownedBizOnModuleButtonClick,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.ModuleButtonClickPrefix)),
					postfix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.ModuleButtonClickPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: OwnedBizController.OnModuleButtonClick inventory physical gate applied");
			}
			var ownedBizModuleToggleSetOwner = ownedBizModuleToggleContextType?.GetMethod("SetOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(VisitState) }, null);
			if (ownedBizModuleToggleSetOwner != null)
			{
				harmony.Patch(
					ownedBizModuleToggleSetOwner,
					prefix: new HarmonyMethod(typeof(OwnedBizModuleToggleOwnerGuardPatch), nameof(OwnedBizModuleToggleOwnerGuardPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ModuleToggleContext.SetOwner null-owner guard applied");
			}
			var ownedBizDialogType = AccessTools.TypeByName("Game.UI.Session.OwnedBiz.OwnedBizDialog");
			var ownedBizRefreshHeader = ownedBizDialogType?.GetMethod("RefreshHeader", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (ownedBizRefreshHeader != null)
			{
				harmony.Patch(
					ownedBizRefreshHeader,
					prefix: new HarmonyMethod(typeof(OwnedBizVehicleVisitStatePatch), nameof(OwnedBizVehicleVisitStatePatch.RefreshHeaderPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: OwnedBizDialog.RefreshHeader owner normalization applied");
			}
			var ownedBizRefreshFooter = ownedBizDialogType?.GetMethod("RefreshFooter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (ownedBizRefreshFooter != null)
			{
				harmony.Patch(
					ownedBizRefreshFooter,
					prefix: new HarmonyMethod(typeof(OwnedBizVehicleVisitStatePatch), nameof(OwnedBizVehicleVisitStatePatch.RefreshFooterPrefix)),
					finalizer: new HarmonyMethod(typeof(OwnedBizVehicleVisitStatePatch), nameof(OwnedBizVehicleVisitStatePatch.RefreshFooterFinalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: OwnedBizDialog.RefreshFooter destination crew scope applied");
			}
			var ownedBizViewInventoryType = AccessTools.TypeByName("Game.UI.Session.OwnedBiz.ViewInventory");
			var ownedBizViewInventoryOnActivated = ownedBizViewInventoryType?.GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (ownedBizViewInventoryOnActivated != null)
			{
				harmony.Patch(
					ownedBizViewInventoryOnActivated,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.OnActivatedPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ViewInventory.OnActivated vehicle visit normalization applied");
			}
			var ownedBizViewInventoryRefreshAllPanels = ownedBizViewInventoryType?.GetMethod("RefreshAllPanels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (ownedBizViewInventoryRefreshAllPanels != null)
			{
				harmony.Patch(
					ownedBizViewInventoryRefreshAllPanels,
					prefix: new HarmonyMethod(typeof(OwnedBizInventoryVehicleVisitPatch), nameof(OwnedBizInventoryVehicleVisitPatch.RefreshAllPanelsPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ViewInventory.RefreshAllPanels vehicle visit normalization applied");
			}
			var bizOnActivated = typeof(BizComponent).GetMethod("OnBizActivated", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Entity) }, null);
			if (bizOnActivated != null)
			{
				harmony.Patch(
					bizOnActivated,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.BizActivatedPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BizComponent.OnBizActivated building interaction scope applied");
			}
			var bizStartConversation = typeof(BizComponent).GetMethod("StartConversation", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity), typeof(bool) }, null);
			if (bizStartConversation != null)
			{
				harmony.Patch(
					bizStartConversation,
					prefix: new HarmonyMethod(typeof(OwnedBuildingInteractionSelectionPatch), nameof(OwnedBuildingInteractionSelectionPatch.StartBizConversationPrefix)));
				harmony.Patch(
					bizStartConversation,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.BizConversationPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BizComponent.StartConversation building interaction scope applied");
			}
			var bizStartVisitConversation = typeof(BizComponent).GetMethod("StartConversation", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(VisitState), typeof(bool) }, null);
			if (bizStartVisitConversation != null)
			{
				harmony.Patch(
					bizStartVisitConversation,
					prefix: new HarmonyMethod(typeof(OwnedBuildingInteractionSelectionPatch), nameof(OwnedBuildingInteractionSelectionPatch.StartBizVisitConversationPrefix)));
				harmony.Patch(
					bizStartVisitConversation,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.BizConversationPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BizComponent.StartConversation visit building interaction scope applied");
			}
			var residenceType = typeof(ResidenceComponent);
			var residenceStartConversation = residenceType.GetMethod("StartCasinoConversation", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
			if (residenceStartConversation != null)
			{
				harmony.Patch(
					residenceStartConversation,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.ResidenceConversationPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				harmony.Patch(
					residenceStartConversation,
					prefix: new HarmonyMethod(typeof(OwnedBuildingInteractionSelectionPatch), nameof(OwnedBuildingInteractionSelectionPatch.StartCasinoConversationPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ResidenceComponent.StartCasinoConversation building interaction scope applied");
			}
			var residenceVisitConversation = residenceType.GetMethod("StartCasinoConversation", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(VisitState), typeof(bool) }, null);
			if (residenceVisitConversation != null)
			{
				harmony.Patch(
					residenceVisitConversation,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.ResidenceConversationPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				harmony.Patch(
					residenceVisitConversation,
					prefix: new HarmonyMethod(typeof(OwnedBuildingInteractionSelectionPatch), nameof(OwnedBuildingInteractionSelectionPatch.StartCasinoVisitConversationPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ResidenceComponent.StartCasinoConversation visit building interaction scope applied");
			}
			int residenceStartConversationPatched = 0;
			foreach (MethodInfo residenceConversationMethod in residenceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
				.Where(method => string.Equals(method.Name, "StartConversation", StringComparison.Ordinal)))
			{
				harmony.Patch(
					residenceConversationMethod,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.ResidenceConversationPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				residenceStartConversationPatched++;
			}
			if (residenceStartConversationPatched > 0)
			{
				Debug.Log($"[GameplayTweaks] Multi-crew vehicle: ResidenceComponent.StartConversation building interaction scope applied count={residenceStartConversationPatched}");
			}
			var civicType = typeof(CivicComponent);
			var civicStartConversation = civicType.GetMethod("StartConversation", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
			if (civicStartConversation != null)
			{
				harmony.Patch(
					civicStartConversation,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.CivicConversationPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CivicComponent.StartConversation building interaction scope applied");
			}
			var civicShowPoliticsDialog = civicType.GetMethod("ShowPoliticsDialog", BindingFlags.Public | BindingFlags.Instance);
			if (civicShowPoliticsDialog != null)
			{
				harmony.Patch(
					civicShowPoliticsDialog,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.CivicPoliticsPrefix)),
					finalizer: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CivicComponent.ShowPoliticsDialog building interaction scope applied");
			}
			var buildingPickUtilType = AccessTools.TypeByName("Game.UI.Session.Picks.BuildingPickUtil");
			var buildingPickMakeMouseover = buildingPickUtilType?.GetMethod("MakeMouseover", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity), AccessTools.TypeByName("Game.UI.Session.Picks.BuildingPickData") }, null);
			if (buildingPickMakeMouseover != null)
			{
				harmony.Patch(
					buildingPickMakeMouseover,
					prefix: new HarmonyMethod(typeof(HumanVehicleBuildingActivationScopePatch), nameof(HumanVehicleBuildingActivationScopePatch.BuildingPickMouseoverPrefix)),
					finalizer: new HarmonyMethod(typeof(BuildingPickMouseoverGuardPatch), nameof(BuildingPickMouseoverGuardPatch.Finalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: BuildingPickUtil.MakeMouseover building interaction scope applied");
			}
			var mobilePickUtilType = AccessTools.TypeByName("Game.UI.Session.Picks.MobilePickUtil");
			MethodInfo mobilePickMakeMouseover = mobilePickUtilType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault(method => method.Name == "MakeMouseover"
					&& method.ReturnType == typeof(string)
					&& method.GetParameters().Length >= 1
					&& method.GetParameters()[0].ParameterType == typeof(Entity));
			if (mobilePickMakeMouseover != null)
			{
				harmony.Patch(
					mobilePickMakeMouseover,
					postfix: new HarmonyMethod(typeof(EnemyVehicleCrewIndicatorPatch), nameof(EnemyVehicleCrewIndicatorPatch.MobileMouseoverPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: MobilePickUtil.MakeMouseover enemy vehicle crew indicator applied");
			}
			Type crewPickType = AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick");
			MethodInfo crewPickOnClick = crewPickType?.GetMethod("OnClick", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (crewPickOnClick != null)
			{
				harmony.Patch(crewPickOnClick, prefix: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.OnClickPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewPick.OnClick enemy vehicle convo normalization applied");
			}
			else
			{
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "CrewPick.OnClick enemy vehicle convo normalization unavailable");
			}
			MethodInfo crewPickMouseover = crewPickType?.GetMethod("MakeMouseoverMessage", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (crewPickMouseover != null)
			{
				harmony.Patch(
					crewPickMouseover,
					prefix: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.MakeMouseoverPrefix)),
					postfix: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.MakeMouseoverPostfix)),
					finalizer: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.MakeMouseoverFinalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewPick.MakeMouseoverMessage enemy vehicle crew indicator applied");
			}
			MethodInfo crewPickSceneVector = crewPickType?.GetMethod("MakeSceneVector", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			if (crewPickSceneVector != null)
			{
				harmony.Patch(
					crewPickSceneVector,
					prefix: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.MakeSceneVectorPrefix)),
					finalizer: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.MakeSceneVectorFinalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewPick.MakeSceneVector enemy vehicle UI stabilization applied");
			}
			MethodInfo crewPickScreenVector = crewPickType?.GetMethod("MakeScreenVector", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			if (crewPickScreenVector != null)
			{
				harmony.Patch(
					crewPickScreenVector,
					prefix: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.MakeScreenVectorPrefix)),
					finalizer: new HarmonyMethod(typeof(EnemyVehicleCrewPickPatch), nameof(EnemyVehicleCrewPickPatch.MakeScreenVectorFinalizer)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CrewPick.MakeScreenVector enemy vehicle UI stabilization applied");
			}
			var crewInteract = typeof(PlayerScheme).GetMethod("CrewInteract", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Entity) }, null);
			if (crewInteract != null)
			{
				harmony.Patch(crewInteract, prefix: new HarmonyMethod(typeof(EnemyVehicleCrewInteractPatch), nameof(EnemyVehicleCrewInteractPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: PlayerScheme.CrewInteract enemy vehicle inspect normalization applied");
			}
			var commandScopeOutOnStarted = typeof(CommandScopeOut).GetMethod("OnStarted", BindingFlags.Instance | BindingFlags.NonPublic);
			if (commandScopeOutOnStarted != null)
			{
				harmony.Patch(commandScopeOutOnStarted, postfix: new HarmonyMethod(typeof(CommandScopeOutGuardPatch), nameof(CommandScopeOutGuardPatch.OnStartedPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandScopeOut.OnStarted stale scope guard applied");
			}
			var scopeOutFeedback = typeof(PlayerTerritory).GetMethod("ScopeOutBuildingWithFeedback", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Entity), typeof(EntityID) }, null);
			if (scopeOutFeedback != null)
			{
				harmony.Patch(
					scopeOutFeedback,
					prefix: new HarmonyMethod(typeof(ScopeOutFeedbackSelectionPatch), nameof(ScopeOutFeedbackSelectionPatch.Prefix)),
					postfix: new HarmonyMethod(typeof(ScopeOutFeedbackSelectionPatch), nameof(ScopeOutFeedbackSelectionPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ScopeOutBuildingWithFeedback deferred ticker/selection refresh patch applied");
			}
			var performTakeover = typeof(PlayerTerritory).GetMethod("PerformTakeover", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(CrewAssignment), typeof(PlayerTerritory.TakeoverData) }, null);
			if (performTakeover != null)
			{
				harmony.Patch(performTakeover, postfix: new HarmonyMethod(typeof(OwnedBizTakeoverOwnerSocialPatch), nameof(OwnedBizTakeoverOwnerSocialPatch.PerformTakeoverPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: owned business takeover owner social repair patch applied");
			}
			var executeCombat = typeof(ConvoCallbacks).GetMethod("ExecuteCombat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(ConvoButton) }, null);
			if (executeCombat != null)
			{
				harmony.Patch(executeCombat, prefix: new HarmonyMethod(typeof(HumanVehicleConvoAttackPopupPatch), nameof(HumanVehicleConvoAttackPopupPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ConvoCallbacks.ExecuteCombat human vehicle node patch applied");
			}
			var mobileActivation = typeof(MobileComponent).GetMethod("OnEntityActivationChanged", BindingFlags.Instance | BindingFlags.NonPublic);
			if (mobileActivation != null)
			{
				harmony.Patch(mobileActivation, prefix: new HarmonyMethod(typeof(AmbientTrafficActivationPatch), nameof(AmbientTrafficActivationPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ambient traffic selection suppression applied");
			}
			MultiCrewVehicleHelper.LogSelectionHookConfiguration(mobileActivation, crewPickOnClick, crewPickMouseover);
			var performHealing = typeof(CombatManager).GetMethod("PerformHealing", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PlayerID), typeof(CrewAssignment) }, null);
			if (performHealing != null)
			{
				harmony.Patch(performHealing, prefix: new HarmonyMethod(typeof(CombatManagerHealingGuardPatch), nameof(CombatManagerHealingGuardPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CombatManager.PerformHealing stale crew guard applied");
			}
			MethodInfo getAvailableHumanCommands = typeof(HumanCommandValidator).GetMethod("GetAvailableCommands", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(CrewAssignment) }, null);
			if (getAvailableHumanCommands != null)
			{
				harmony.Patch(getAvailableHumanCommands, postfix: new HarmonyMethod(typeof(PassengerCommandAuthorityPatch), nameof(PassengerCommandAuthorityPatch.GetAvailableCommandsPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: passenger command authority split applied");
			}
			MethodInfo commandHealValidate = typeof(CommandHealValidator).GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PlayerID), typeof(CrewAssignment) }, null);
			if (commandHealValidate != null)
			{
				harmony.Patch(
					commandHealValidate,
					prefix: new HarmonyMethod(typeof(CommandButtonHealDriverPatch), nameof(CommandButtonHealDriverPatch.ValidatePrefix)),
					postfix: new HarmonyMethod(typeof(CommandButtonHealDriverPatch), nameof(CommandButtonHealDriverPatch.ValidatePostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandHealValidator.Validate stale crew guard and driver-only/group-heal patch applied");
			}
			MethodInfo commandHealPostHuman = typeof(CommandHealValidator).GetMethod("PostCommandAsHuman", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(CrewAssignment) }, null);
			if (commandHealPostHuman != null)
			{
				harmony.Patch(commandHealPostHuman, prefix: new HarmonyMethod(typeof(CommandButtonHealDriverPatch), nameof(CommandButtonHealDriverPatch.PostCommandAsHumanPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandHealValidator.PostCommandAsHuman driver-only patch applied");
			}
			MethodInfo commandHealCanStart = typeof(CommandHeal).GetMethod("CanStart", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			if (commandHealCanStart != null)
			{
				harmony.Patch(commandHealCanStart, prefix: new HarmonyMethod(typeof(CommandButtonHealDriverPatch), nameof(CommandButtonHealDriverPatch.CanStartPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CommandHeal.CanStart vehicle group-heal patch applied");
			}
			var canSpawnAmbientCar = typeof(TransitManager).GetMethod("CanSpawnAmbientCar", BindingFlags.Instance | BindingFlags.NonPublic);
			if (canSpawnAmbientCar != null)
			{
				harmony.Patch(canSpawnAmbientCar, prefix: new HarmonyMethod(typeof(AmbientTrafficCanSpawnPatch), nameof(AmbientTrafficCanSpawnPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: TransitManager.CanSpawnAmbientCar ambient traffic patch applied");
			}
			var spawnAmbientCar = typeof(TransitManager).GetMethod("SpawnAmbientCar", BindingFlags.Instance | BindingFlags.NonPublic);
			if (spawnAmbientCar != null)
			{
				harmony.Patch(spawnAmbientCar, prefix: new HarmonyMethod(typeof(AmbientTrafficSpawnPatch), nameof(AmbientTrafficSpawnPatch.Prefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: TransitManager.SpawnAmbientCar ambient traffic patch applied");
			}
			var despawnCar = typeof(TransitManager).GetMethod("DespawnCar", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(EntityID), typeof(bool) }, null);
			if (despawnCar != null)
			{
				harmony.Patch(despawnCar, postfix: new HarmonyMethod(typeof(AmbientTrafficDespawnPatch), nameof(AmbientTrafficDespawnPatch.Postfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: TransitManager.DespawnCar ambient traffic cleanup applied");
			}
			var visitStateGetCrewNode = typeof(VisitState).GetMethod("GetCrewNode", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (visitStateGetCrewNode != null)
			{
				harmony.Patch(visitStateGetCrewNode, prefix: new HarmonyMethod(typeof(HumanVehicleVisitStateNodePatch), nameof(HumanVehicleVisitStateNodePatch.GetCrewNodePrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: VisitState.GetCrewNode human vehicle override applied");
			}
			var visitStateGetCrewNodeId = typeof(VisitState).GetMethod("GetCrewNodeID", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (visitStateGetCrewNodeId != null)
			{
				harmony.Patch(visitStateGetCrewNodeId, prefix: new HarmonyMethod(typeof(HumanVehicleVisitStateNodePatch), nameof(HumanVehicleVisitStateNodePatch.GetCrewNodeIdPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: VisitState.GetCrewNodeID human vehicle override applied");
			}
			Type checkNpcHasLootType = AccessTools.TypeByName("Game.Session.Data.CheckNpcHasLoot");
			MethodInfo checkNpcHasLoot = checkNpcHasLootType?.GetMethod("DoesPass", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(VisitState) }, null);
			if (checkNpcHasLoot != null)
			{
				harmony.Patch(checkNpcHasLoot, prefix: new HarmonyMethod(typeof(VehicleSearchBlockPatch), nameof(VehicleSearchBlockPatch.CheckNpcHasLootPrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CheckNpcHasLoot search block applied");
			}
			Type checkHasCashInVehicleType = AccessTools.TypeByName("Game.Session.Data.CheckHasCashInVehicle");
			MethodInfo checkHasCashInVehicle = checkHasCashInVehicleType?.GetMethod("DoesPass", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(VisitState) }, null);
			if (checkHasCashInVehicle != null)
			{
				harmony.Patch(checkHasCashInVehicle, prefix: new HarmonyMethod(typeof(VehicleSearchBlockPatch), nameof(VehicleSearchBlockPatch.CheckHasCashInVehiclePrefix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: CheckHasCashInVehicle search block applied");
			}
			Type convoDataLootDropType = AccessTools.TypeByName("Game.UI.Session.Convo.ConvoDataLootDrop");
			MethodInfo convoDataLootDropMakeReplacements = convoDataLootDropType?.GetMethod("MakeReplacements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(VisitState), typeof(int) }, null);
			if (convoDataLootDropMakeReplacements != null)
			{
				harmony.Patch(convoDataLootDropMakeReplacements, postfix: new HarmonyMethod(typeof(VehicleSearchBlockPatch), nameof(VehicleSearchBlockPatch.LootDropMakeReplacementsPostfix)));
				Debug.Log("[GameplayTweaks] Multi-crew vehicle: ConvoDataLootDrop death source patch applied");
			}
		}
	}

}

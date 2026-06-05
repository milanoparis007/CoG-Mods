using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Heatmaps;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Crew;
using Game.UI.Session.HUD;
using Game.UI.Session.Picks;
using Game.UI.Util;
using HarmonyLib;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace GameplayTweaks
{
	internal static class TurnPerformanceDiagnosticsPatch
	{
		private const long SlowStageThresholdMs = 40;
		private const long CityGenSlowStageThresholdMs = 250;
		private const long PlayerTurnSlowStageThresholdMs = 10;
		private const long AdvisorTurnSlowStageThresholdMs = 8;
		private const long TerritoryAdvisorDetailThresholdMs = 4;
		private const long TakeoverDetailThresholdMs = 4;
		private const long PlayerAIStartDetailThresholdMs = 4;
		private const long PlayerAIAssignRequestsDetailThresholdMs = 4;
		private const long PlayerAIAssignRequestsAggregateThresholdMs = 25;
		private const long PlayerCrewTurnStartDetailThresholdMs = 4;
		private const long PoliticsDetailThresholdMs = 4;
		private const long SimulationSubmanagerDetailThresholdMs = 5;
		private const int InteractiveTurnMaxAdvancesPerFrame = 4;
		private const int InteractiveSystemTurnMaxHandlersPerFrame = 3;
		private const long InteractiveTurnFrameBudgetMs = 18;
		private const long InteractiveCheapContinuationBudgetMs = 5;
		private const long InteractiveBusinessPhaseCheapContinuationBudgetMs = 8;
		private const long InteractivePostBusinessSimulationTailBudgetMs = 45;
		private const long LargeSaveBusinessEntrySimulationBudgetMs = 24;
		private const long LargeSavePostBusinessSimulationTailBudgetMs = 55;
		private const long InteractiveTurnFrameLogThresholdMs = 25;
		private const long TurnHandlerDetailThresholdMs = 8;
		private const long CompactInteractiveTurnFrameLogThresholdMs = 120;
		private const long CompactTurnHandlerDetailThresholdMs = 80;
		private const long CompactSlowStageThresholdMs = 80;
		private const long CompactCacheMissLogThresholdMs = 40;
		private const long CompactSystemMaintenanceTotalLogThresholdMs = 750;
		private const long CommandQueueDetailThresholdMs = 5;
		private const long CommandQueueStepDetailThresholdMs = 8;
		private const long CommandExecutorDetailThresholdMs = 25;
		private const int AiCommandExecutorMaxTuplesPerTurn = 10;
		private const long AiCommandExecutorBudgetMs = 24;
		private const int AiCommandQueueMaxCompletedCommandsPerTurn = 1;
		private const long AiCommandQueueBudgetMs = 8;
		private const long HudInputDetailThresholdMs = 6;
		private const int ResidenceImmigrationBatchTurns = 1;
		private const bool EnableResidenceCacheSplitDiagnostics = false;
		private const int InteractiveHeatmapEntriesPerFrame = 1;
		private const long InteractiveHeatmapUpdateBudgetMs = 4;
		private const int InteractiveSimulationSubmanagersPerFrame = 2;
		private const int InteractiveBusinessUpdatePhasesPerFrame = 1;
		private const int InteractiveBusinessModuleUpdatesPerFrame = 8192;
		private const int LargeSaveBusinessModuleUpdatesPerFrame = 4096;
		private const int BusinessTrackerRelationshipRespectPhaseIndex = 3;
		private const int BusinessTrackerPostRelationshipTailStartPhaseIndex = 4;
		private const int BusinessTrackerFinalTailStartPhaseIndex = 10;
		private const long InteractiveBusinessModuleUpdateBudgetMs = 20;
		private const long LargeSaveBusinessModuleUpdateBudgetMs = 24;
		private const int InteractiveNonHumanBusinessModuleUpdateCohorts = 8;
		private const long SlowBusinessModuleIterationThresholdMs = 20;
		private const long SlowBusinessModuleUpdateThresholdMs = 24;
		private const long ManufactureModuleDetailThresholdMs = 20;
		private const long HumanCrewCandidateRecomputeDetailThresholdMs = 8;
		private const int NonHumanManufactureMaxCatchupIntervals = 1;
		private const long BusinessModuleInitDetailThresholdMs = 8;
		private static readonly bool EnableBusinessModuleFamilyProfileDiagnostics = false;
		private const int BusinessModuleUpdateFamilyCount = 7;
		private const int InteractiveRelationshipRespectPlayersPerFrame = 80;
		private const int LargeSaveRelationshipRespectPlayersPerFrame = 96;
		private const long InteractiveRelationshipRespectBudgetMs = 22;
		private const long LargeSaveRelationshipRespectBudgetMs = 28;
		private const long InteractiveBusinessTrackerFrameBudgetMs = 22;
		private const long LargeSaveBusinessTrackerFrameBudgetMs = 38;
		private const long InteractiveBusinessTrackerCheapContinuationBudgetMs = 20;
		private const long LargeSaveBusinessTrackerCheapContinuationBudgetMs = 38;
		private const long LargeSaveBusinessTrackerFinalTailBudgetMs = 55;
		private const long LargeSaveBusinessTrackerPostRelationshipTailBudgetMs = 55;
		private const int DeferredSocialInferenceDelayFrames = 180;
		private const int DeferredSocialInferenceQuietFramesAfterCommandQueue = 120;
		private const int DeferredSocialInferenceMaxExtraDelayFrames = 480;
		private const int MaxDeferredSocialInferenceQueue = 64;
		private const int DeferredHumanCrewCandidateDelayFrames = 180;
		private const int ExistingPickRefreshesPerFrame = 6;
		private const int DeferredExistingPickRefreshDelayFrames = 1;
		private const int DeferredExistingPickRefreshesPerFrame = 4;
		private const int MaxDeferredExistingPickRefreshes = 64;
		private const int DeferredTakeoverPickRefreshDelayFrames = 2;
		private const int MaxDeferredTakeoverPickRefreshes = 16;
		private const int DeferredCrewDialogRefreshDelayFrames = 2;
		private const int MaxDeferredCrewDialogRefreshes = 8;
		private const long CrewDialogRefreshDeferredLogThresholdMs = 12;
		private const int DeferredCrewDialogSelectionDelayFrames = 2;
		private const int MaxDeferredCrewDialogSelectionChanges = 8;
		private const long CrewDialogSelectionDeferredLogThresholdMs = 12;
		private const int DistinctSkillChoiceCacheFrames = 240;
		private const long SkillRequirementDetailLogThresholdMs = 40;
		private const long SkillRequirementSingleLogThresholdMs = 8;
		private const int ModuleInTerritoryCacheFrames = 240;
		private const int TerritoryModulePresenceCacheFrames = 120;
		private const int CanBuySellAvailabilityCacheFrames = 30;
		private const int CanBuySellLockedLogEvery = 2048;
		private const int CanBuySellForcedClosedLogIntervalDays = 28;
		private const int CanBuySellLockedKeyLimit = 512;
		private const int LargeSaveTotalPlayersThreshold = 120;
		private const int LargeSaveBusinessCountThreshold = 10000;
		private const int LargeSaveNonHumanBusinessModuleUpdateCohorts = 24;
		private static readonly Label TagPlayerLegalBiz = (Label)"tag-player-legal-biz";
		private static readonly Label TagLegalLiquor = (Label)"tag-legal-liquor";
		private const BindingFlags InstanceMethodFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool IsPerformanceDiagnosticsEnabled()
		{
			try
			{
				return GameplayTweaksPlugin.EnablePerformanceDiagnostics?.Value ?? false;
			}
			catch
			{
				return false;
			}
		}

		private static long GetInteractiveTurnFrameLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? InteractiveTurnFrameLogThresholdMs
				: CompactInteractiveTurnFrameLogThresholdMs;
		}

		private static long GetTurnHandlerDetailLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? TurnHandlerDetailThresholdMs
				: CompactTurnHandlerDetailThresholdMs;
		}

		private static long GetEffectiveStageLogThresholdMs(long thresholdMs, bool isCityGen)
		{
			if (IsPerformanceDiagnosticsEnabled() || isCityGen)
			{
				return thresholdMs;
			}

			return Math.Max(thresholdMs, CompactSlowStageThresholdMs);
		}

		private static long GetCacheMissLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? PlayerAIStartDetailThresholdMs
				: CompactCacheMissLogThresholdMs;
		}

		private static long GetCommandQueueStepLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? CommandQueueStepDetailThresholdMs
				: CompactTurnHandlerDetailThresholdMs;
		}

		private static long GetCommandQueueLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? CommandQueueDetailThresholdMs
				: CompactTurnHandlerDetailThresholdMs;
		}

		private static long GetCommandExecutorDetailLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? CommandExecutorDetailThresholdMs
				: CompactTurnHandlerDetailThresholdMs;
		}

		private static long GetAiDetailLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? PlayerAIAssignRequestsDetailThresholdMs
				: CompactTurnHandlerDetailThresholdMs;
		}

		private static long GetAiAggregateLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? PlayerAIAssignRequestsAggregateThresholdMs
				: CompactTurnHandlerDetailThresholdMs;
		}

		private static long GetHudInputLogThresholdMs()
		{
			return IsPerformanceDiagnosticsEnabled()
				? HudInputDetailThresholdMs
				: CompactTurnHandlerDetailThresholdMs;
		}

		private static bool ShouldLogSystemMaintenanceTotal(long elapsedMs)
		{
			return elapsedMs >= (IsPerformanceDiagnosticsEnabled()
				? InteractiveTurnFrameLogThresholdMs
				: CompactSystemMaintenanceTotalLogThresholdMs);
		}

		private static bool ShouldLogDirectPerformanceDetail(long elapsedMs)
		{
			return elapsedMs >= CompactTurnHandlerDetailThresholdMs || IsPerformanceDiagnosticsEnabled();
		}

		private static bool ShouldLogPerformanceSample()
		{
			return IsPerformanceDiagnosticsEnabled();
		}

		private static bool ShouldLogPerformanceSlice(bool completed, long elapsedMs)
		{
			return elapsedMs >= GetInteractiveTurnFrameLogThresholdMs()
				|| (!completed && IsPerformanceDiagnosticsEnabled());
		}

		private static readonly Dictionary<MethodBase, long> ThresholdsByMethod = new Dictionary<MethodBase, long>();
		private static readonly HashSet<MethodBase> StartupStageMethods = new HashSet<MethodBase>();
		private static readonly HashSet<string> FailedTimedPatchTargets = new HashSet<string>(StringComparer.Ordinal);
		private static readonly HashSet<string> LoggedSlowBusinessModuleUpdates = new HashSet<string>(StringComparer.Ordinal);
		private static readonly HashSet<string> LoggedSlowBusinessModuleIterations = new HashSet<string>(StringComparer.Ordinal);
		private static readonly HashSet<string> LoggedAiManufactureCatchupClamps = new HashSet<string>(StringComparer.Ordinal);
		private static readonly HashSet<string> LoggedAiManufactureInventoryTransferOptimizations = new HashSet<string>(StringComparer.Ordinal);
		private static int _uniqueAiManufactureCatchupClamps;
		private static int _uniqueAiManufactureInventoryTransferOptimizations;
		private static string _lastSuppressedSetupPresenceKey;
		private static int _suppressedSetupPresenceUpdates;
		private static int _skippedNonHumanSocialQuestFlushes;
		private static bool _flushingDeferredSocialInferences;
		private static int _lastInteractiveCommandQueueFrame = int.MinValue;
		private static bool _loggedFastNextTurnSelectionClearSkipped;
		private static int _largeSaveBudgetCacheDay = int.MinValue;
		private static int _largeSaveBudgetCacheTurn = int.MinValue;
		private static bool _largeSaveBudgetCacheResult;
		private static int _distinctSkillChoiceCacheLastPruneFrame;
		private static int _distinctSkillChoiceCacheHits;
		private static int _moduleInTerritoryCacheLastPruneFrame;
		private static int _moduleInTerritoryCacheHits;
		private static int _territoryModulePresenceHits;
		private static int _territoryModulePresenceBuilds;
		private static int _canBuySellAvailabilityCacheLastPruneFrame;
		private static int _canBuySellAvailabilityCacheHits;
		private static int _canBuySellAvailabilityCacheMisses;
		private static int _canBuySellAvailabilityLockedBlocks;
		private static readonly Dictionary<string, int> CanBuySellLockedInvalidationTurnByKey = new Dictionary<string, int>();
		private static readonly Dictionary<string, int> CanBuySellLockedBlockLogDayByKey = new Dictionary<string, int>();
		private static readonly Dictionary<ResidenceTracker, float> PendingResidenceImmigrationDays = new Dictionary<ResidenceTracker, float>();
		private static readonly Dictionary<HeatmapManager, PendingHeatmapUpdateWork> PendingHeatmapUpdates = new Dictionary<HeatmapManager, PendingHeatmapUpdateWork>();
		private static readonly Queue<DeferredSocialInferenceWork> PendingSocialInferences = new Queue<DeferredSocialInferenceWork>();
		private static readonly Queue<DeferredExistingPickRefreshWork> PendingExistingPickRefreshes = new Queue<DeferredExistingPickRefreshWork>();
		private static readonly HashSet<ulong> PendingExistingPickRefreshIds = new HashSet<ulong>();
		private static readonly Queue<DeferredTakeoverPickRefreshWork> PendingTakeoverPickRefreshes = new Queue<DeferredTakeoverPickRefreshWork>();
		private static readonly Queue<DeferredCrewDialogRefreshWork> PendingCrewDialogRefreshes = new Queue<DeferredCrewDialogRefreshWork>();
		private static readonly Queue<DeferredCrewDialogSelectionWork> PendingCrewDialogSelectionChanges = new Queue<DeferredCrewDialogSelectionWork>();
		private static readonly Dictionary<string, CachedDistinctSkillChoice> DistinctSkillChoiceCache = new Dictionary<string, CachedDistinctSkillChoice>();
		private static readonly Dictionary<string, CachedModuleInTerritoryCheck> ModuleInTerritoryCache = new Dictionary<string, CachedModuleInTerritoryCheck>();
		private static readonly Dictionary<string, CachedTerritoryModulePresence> TerritoryModulePresenceCache = new Dictionary<string, CachedTerritoryModulePresence>();
		private static readonly Dictionary<string, CachedCanBuySellAvailability> CanBuySellAvailabilityCache = new Dictionary<string, CachedCanBuySellAvailability>();
		private static List<IModuleConfig> _quietPlayerLegalModules;
		private static MethodInfo _dirtyCashIsFrontRoomModuleMethod;
		private static MethodInfo _dirtyCashShouldShowModuleMethod;
		[ThreadStatic]
		private static CommandExecutorDetailState _activeCommandExecutorDetail;
		[ThreadStatic]
		private static CommandQueueDetailState _activeCommandQueueDetail;
		[ThreadStatic]
		private static PlayerAIAssignRequestsDetailState _activePlayerAIAssignRequestsDetail;
		[ThreadStatic]
		private static PlayerAIDispatchDetailState _activePlayerAIDispatchDetail;
		[ThreadStatic]
		private static ScriptDispatcherDetailState _activeScriptDispatcherDetail;
		[ThreadStatic]
		private static HudUpdateAnimationDetailState _activeHudUpdateAnimationDetail;
		private static readonly FieldInfo SessionPlayerTurnHandlerField = AccessTools.Field(typeof(SessionContext), "_playerTurnHandler");
		private static readonly FieldInfo SessionTurnSourceField = AccessTools.Field(typeof(SessionContext), "_turnSource");
		private static readonly FieldInfo SessionTurnAdvancedHandlersField = AccessTools.Field(typeof(SessionContext), "_turnAdvancedHandlers");
		private static readonly FieldInfo SessionSystemTurnHandlersField = AccessTools.Field(typeof(SessionContext), "_systemTurnHandlers");
		private static readonly FieldInfo SimulationTurnUpdatesField = AccessTools.Field(typeof(SimulationManager), "_turnUpdates");
		private static readonly FieldInfo BaseHudGoField = AccessTools.Field(typeof(BaseHUDDialog), "_go");
		private static readonly MethodInfo AllPlayersManagerFinishActivePlayerTurnMethod = AccessTools.Method(typeof(AllPlayersManager), "FinishActivePlayerTurn");
		private static readonly FieldInfo HudLastShownPlayerField = AccessTools.Field(typeof(HUDBar), "_lastShownPlayer");
		private static readonly FieldInfo HudLastShownPlayerTimeField = AccessTools.Field(typeof(HUDBar), "_lastShownPlayerTime");
		private static readonly FieldInfo HudCornersTextField = AccessTools.Field(typeof(HUDBar), "_corners");
		private static readonly FieldInfo PlayerSubmanagerPidField = AccessTools.Field(typeof(PlayerSubmanager), "_pid");
		private static readonly MethodInfo CommandExecutorProcessCommandQueueMethod = AccessTools.Method(typeof(CommandExecutor), "ProcessCommandQueue");
		private static readonly MethodInfo CommandExecutorRemoveDeprecatedTuplesMethod = AccessTools.Method(typeof(CommandExecutor), "RemoveDeprecatedTuples");
		private static readonly MethodInfo SocialHistoryReprocessInferencesMethod = FindInstanceMethodNoWarn(typeof(SocialHistoryData), "ReprocessInferences");
		private static readonly ReprocessSocialInferencesDelegate SocialHistoryReprocessInferences = CreateReprocessSocialInferencesDelegate();
		private static readonly MethodInfo PickManagerAddOrRefreshPickMethod = AccessTools.Method(typeof(PickManager), "AddOrRefreshPick", new[] { typeof(PickType), typeof(PickTarget), typeof(bool) });
		private static readonly MethodInfo PickManagerRefreshVisibleSummaryPicksMethod = AccessTools.Method(typeof(PickManager), "RefreshVisibleSummaryPicks");
		private static readonly MethodInfo CrewDialogRecreateAllCardsMethod = AccessTools.Method(typeof(CrewDialog), "RecreateAllCards");
		private static readonly MethodInfo CrewDialogOnCurrentActiveChangedMethod = AccessTools.Method(typeof(CrewDialog), "OnCurrentActiveChanged");
		private static readonly FieldInfo CrewDialogAllCardsField = AccessTools.Field(typeof(CrewDialog), "_allCards");
		private static readonly MethodInfo PlayerTerritoryGetAllControlledBuildingsUnsafeMethod = AccessTools.Method(typeof(PlayerTerritory), "GetAllControlledBuildingsUnsafe");
		private static readonly MethodInfo PlayerTerritoryGetAllOwnedNodesUnsafeMethod = AccessTools.Method(typeof(PlayerTerritory), "GetAllOwnedNodesUnsafe");
		private static readonly FieldInfo BusinessTrackerBizCacheAllField = AccessTools.Field(typeof(BusinessTracker), "_bizCacheAll");
		private static readonly FieldInfo BusinessTrackerPotentialOwnersCacheField = AccessTools.Field(typeof(BusinessTracker), "_potentialOwnersCache");
		private static readonly FieldInfo BusinessTrackerBizWithoutOwnersField = AccessTools.Field(typeof(BusinessTracker), "_bizWithoutOwners");
		private static readonly FieldInfo HeatmapManagerMapsField = AccessTools.Field(typeof(HeatmapManager), "maps");
		private static readonly FieldInfo HeatmapManagerTempField = AccessTools.Field(typeof(HeatmapManager), "_temp");
		private static readonly MethodInfo BusinessTrackerTryAssignOwnersAsNeededMethod = AccessTools.Method(typeof(BusinessTracker), "TryAssignOwnersAsNeeded");
		private static readonly MethodInfo ResidenceDailyImmigrationMethod = AccessTools.Method(typeof(ResidenceTracker), "DailyImmigration");
		private static readonly MethodInfo ResidenceDailyImmigrationHelperMethod = AccessTools.Method(typeof(ResidenceTracker), "DailyImmigrationHelper");
		private static readonly FieldInfo ResidenceMoveinsField = AccessTools.Field(typeof(ResidenceTracker), "_moveins");
		private static readonly FieldInfo ResidenceMoveinsEthnicitiesField = ResidenceMoveinsField?.FieldType == null ? null : AccessTools.Field(ResidenceMoveinsField.FieldType, "_ethnicities");
		private static readonly MethodInfo ResidenceMoveinsCacheResidenceDataMethod = ResidenceMoveinsField?.FieldType == null ? null : AccessTools.Method(ResidenceMoveinsField.FieldType, "CacheResidenceData");
		private static readonly MethodInfo ResidenceMoveinsCacheLotsAndHousesMethod = ResidenceMoveinsField?.FieldType == null ? null : AccessTools.Method(ResidenceMoveinsField.FieldType, "CacheLotsAndHouses");
		private static readonly MethodInfo ResidenceMoveinsCachePotentialResidencesForEthMethod = ResidenceMoveinsField?.FieldType == null ? null : AccessTools.Method(ResidenceMoveinsField.FieldType, "CachePotentialResidencesForEth");
		private static readonly MethodInfo ResidenceMoveinsResetCachesMethod = ResidenceMoveinsField?.FieldType == null ? null : AccessTools.Method(ResidenceMoveinsField.FieldType, "ResetCaches");
		private static ResidenceDailyImmigrationDelegate _residenceDailyImmigrationDelegate;
		private static bool _residenceDailyImmigrationDelegateAttempted;
		private static bool _loggedResidenceDailyImmigrationDelegateFallback;
		private static readonly MethodInfo DirtyCashUpdateBusinessModulesPrefixMethod = AccessTools.Method(typeof(GameplayTweaksPlugin.DirtyCashEconomyCompatibilityPatch), "UpdateBusinessModulesPrefix");
		private static readonly MethodInfo DirtyCashUpdateBusinessModulesPostfixMethod = AccessTools.Method(typeof(GameplayTweaksPlugin.DirtyCashEconomyCompatibilityPatch), "UpdateBusinessModulesPostfix");
		private static readonly MethodInfo DirtyCashUpdateBusinessModulesFinalizerMethod = AccessTools.Method(typeof(GameplayTweaksPlugin.DirtyCashEconomyCompatibilityPatch), "UpdateBusinessModulesFinalizer");
		private static MethodInfo _afterProhibitionEconomyOwnsDirtyCashRuntimeSweepMutationMethod;
		private static MethodInfo _afterProhibitionEconomyBeginDirtyCashRuntimeSweepBusinessUpdateMethod;
		private static MethodInfo _afterProhibitionEconomyCompleteDirtyCashRuntimeSweepBusinessUpdateMethod;
		private static MethodInfo _afterProhibitionEconomyEndDirtyCashRuntimeSweepBusinessUpdateMethod;
		private static readonly MethodInfo ModulesComponentDoUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new[] { typeof(SimTime), typeof(bool) });
		private static ModulesComponentDoUpdateDelegate _modulesComponentDoUpdateDelegate;
		private static bool _modulesComponentDoUpdateDelegateAttempted;
		private static bool _loggedModulesComponentDoUpdateDelegateFallback;
		private static SessionContext _businessModuleCacheContext;
		private static readonly Dictionary<EntityID, CachedBusinessModules> BusinessModuleComponentCache = new Dictionary<EntityID, CachedBusinessModules>();
		private static readonly MethodInfo AfterProhibitionPurchaseStockRunMethod = AccessTools.Method(AccessTools.TypeByName("AfterProhibitionEconomy.PurchaseStockRefreshPatch"), "Run");
		private static readonly MethodInfo AfterProhibitionEmptyBusinessRepairRunMethod = AccessTools.Method(AccessTools.TypeByName("AfterProhibitionEconomy.EmptyBusinessModuleRepairPatch"), "Run");
		private static readonly MethodInfo PlayerCrewGrowthRecomputeHumanCrewCandidatesMethod = AccessTools.Method(typeof(PlayerCrewGrowth), "RecomputeHumanCrewCandidates", new[] { typeof(PlayerCrew) });
		private static readonly ConditionalWeakTable<CommandExecutor, CommandExecutorLimiterState> CommandExecutorLimiterStates = new ConditionalWeakTable<CommandExecutor, CommandExecutorLimiterState>();
		private static readonly FieldInfo PoliticsManagerPersistedDataField = AccessTools.Field(typeof(PoliticsManager), "_pdata");
		private static readonly HashSet<ulong> RuntimeCheatGeneratedPeeps = new HashSet<ulong>();
		private static readonly HashSet<Label> RuntimeCheatGeneratedEthnicities = new HashSet<Label>();
		private static readonly HashSet<ulong> RuntimeCheatPoliticianIds = new HashSet<ulong>();
		private static int _runtimeCheatGeneratedDay = int.MinValue;
		private static int _runtimeCheatGeneratedTurn = int.MinValue;
		private static PlayerCrewGrowth _pendingHumanCrewCandidateGrowth;
		private static PlayerCrew _pendingHumanCrewCandidateCrew;
		private static int _pendingHumanCrewCandidateDay;
		private static int _pendingHumanCrewCandidateTurn;
		private static int _pendingHumanCrewCandidateFrame;
		private static bool _pendingHumanCrewCandidateRecompute;
		private static bool _pendingHumanCrewCandidateServedStale;
		private static bool _forceHumanCrewCandidateRecompute;
		private static int _pickRefreshLimiterFrame = int.MinValue;
		private static int _pickRefreshLimiterExistingRefreshes;
		private static int _pickRefreshLimiterSkipped;
		private static int _pickRefreshLimiterBypassUntilFrame = int.MinValue;
		private static string _pickRefreshLimiterBypassReason = string.Empty;
		private static bool _flushingDeferredCrewDialogSelectionChange;
		private static int _playerCrewGrowthTurnStartDepth;
		private static int _lastDeferredHumanCrewCandidateLogDay = int.MinValue;
		private static int _lastDeferredHumanCrewCandidateIdlePreserveLogDay = int.MinValue;
		private static readonly BusinessUpdatePhase[] SlicedBusinessUpdatePhases =
		{
			new BusinessUpdatePhase("ClearAOEEffectsOnNodes", AccessTools.Method(typeof(BusinessUpdate), "ClearAOEEffectsOnNodes"), false),
			new BusinessUpdatePhase("UpdateBusinessModules", AccessTools.Method(typeof(BusinessUpdate), "UpdateBusinessModules"), true),
			new BusinessUpdatePhase("UpdateGamblingModules", AccessTools.Method(typeof(BusinessUpdate), "UpdateGamblingModules"), true),
			new BusinessUpdatePhase("UpdateRespectFromRelationships", AccessTools.Method(typeof(BusinessUpdate), "UpdateRespectFromRelationships"), false),
			new BusinessUpdatePhase("UpdateRespectFromTerritory", AccessTools.Method(typeof(BusinessUpdate), "UpdateRespectFromTerritory"), false),
			new BusinessUpdatePhase("UpdateRespectFromEthnicity", AccessTools.Method(typeof(BusinessUpdate), "UpdateRespectFromEthnicity"), false),
			new BusinessUpdatePhase("UpdateRespectFromSafehouses", AccessTools.Method(typeof(BusinessUpdate), "UpdateRespectFromSafehouses"), false),
			new BusinessUpdatePhase("UpdateHeatFromRelationships", AccessTools.Method(typeof(BusinessUpdate), "UpdateHeatFromRelationships"), false),
			new BusinessUpdatePhase("UpdateHeatFromNeighbors", AccessTools.Method(typeof(BusinessUpdate), "UpdateHeatFromNeighbors"), false),
			new BusinessUpdatePhase("UpdateHeatFromBusinesses", AccessTools.Method(typeof(BusinessUpdate), "UpdateHeatFromBusinesses"), false),
			new BusinessUpdatePhase("RecalculateHeatAndRespectForNodes", AccessTools.Method(typeof(BusinessUpdate), "RecalculateHeatAndRespectForNodes"), true)
		};
		private static bool _interactiveTurnSmoothingDisabled;
		private static PendingSystemTurnWork _pendingSystemTurnWork;
		private static int _cachedHudCornerCount = int.MinValue;
		private static int _cachedHudCornerFrame = -100000;

		private delegate void ModulesComponentDoUpdateDelegate(ModulesComponent instance, SimTime time, bool initial);
		private delegate int ResidenceDailyImmigrationDelegate(ResidenceTracker instance, float days);

		internal static bool IsFlushingDeferredCrewDialogSelectionChange => _flushingDeferredCrewDialogSelectionChange;

		private sealed class CachedBusinessModules
		{
			public EntityID BuildingId;
			public Entity Building;
			public ModulesComponent Modules;
		}

		private enum BusinessModuleUpdateFamily
		{
			Noop = 0,
			Manufacture = 1,
			Consumer = 2,
			Vehicle = 3,
			Gambling = 4,
			Mixed = 5,
			Other = 6
		}

		private static readonly string[] BusinessModuleUpdateFamilyNames =
		{
			"noop",
			"manufacture",
			"consumer",
			"vehicle",
			"gambling",
			"mixed",
			"other"
		};

		private readonly struct BusinessUpdatePhase
		{
			public readonly string Name;
			public readonly MethodInfo Method;
			public readonly bool TakesInitial;

			public BusinessUpdatePhase(string name, MethodInfo method, bool takesInitial)
			{
				Name = name;
				Method = method;
				TakesInitial = takesInitial;
			}
		}

		private sealed class PendingSystemTurnWork
		{
			public SessionContext Context;
			public List<ISystemTurnHandler> Handlers;
			public int NextIndex;
			public long StartedTicks;
			public int Day;
			public int Turn;
			public SimulationManager ActiveSimulationManager;
			public List<ISystemTurnSubManager<SimulationManager>> SimulationSubmanagers;
			public int NextSimulationIndex;
			public long SimulationStartedTicks;
			public long SimulationSliceCpuMs;
			public long SimulationMaxResumeGapMs;
			public long LastSimulationSliceEndTicks;
			public List<SimulationSubmanagerProfile> SimulationProfiles;
			public PendingBusinessTrackerTurnWork BusinessTrackerWork;
			public bool UiMaintenanceHoldLogged;
		}

		private sealed class SimulationSubmanagerProfile
		{
			public string Name;
			public long CpuMs;
			public long MaxMs;
			public int Calls;
		}

		private sealed class PendingBusinessTrackerTurnWork
		{
			public BusinessTracker Tracker;
			public bool OwnerStepCompleted;
			public int NextPhaseIndex;
			public long StartedTicks;
			public bool BusinessUpdateScopeStarted;
			public PendingBusinessModulesUpdateWork BusinessModulesUpdateWork;
			public PendingRelationshipRespectUpdateWork RelationshipRespectUpdateWork;
			public long BusinessModulesWallMs;
			public long BusinessModulesSliceCpuMs;
			public int BusinessModulesSlices;
			public long RelationshipRespectWallMs;
			public long RelationshipRespectSliceCpuMs;
			public int RelationshipRespectSlices;
		}

		private sealed class PendingBusinessModulesUpdateWork
		{
			public SessionContext Context;
			public List<Entity> Businesses;
			public HashSet<EntityID> HumanControlledBuildings;
			public int NextIndex;
			public SimTime Now;
			public bool DirtyCashPrefixApplied;
			public int NoopModuleUpdatesSkipped;
			public int ActiveModuleUpdates;
			public int CohortModuleUpdatesDeferred;
			public int[] ModuleFamilyCounts = new int[BusinessModuleUpdateFamilyCount];
			public long[] ModuleFamilyTicks = new long[BusinessModuleUpdateFamilyCount];
			public long StartedTicks;
			public long SliceCpuMs;
			public int SliceCount;
		}

		private sealed class PendingRelationshipRespectUpdateWork
		{
			public SessionContext Context;
			public List<PlayerInfo> Players;
			public int NextIndex;
			public List<(NodeID nid, Fixnum val)> AverageRelsPerNode = new List<(NodeID nid, Fixnum val)>();
			public long StartedTicks;
			public long SliceCpuMs;
			public int SliceCount;
		}

		private sealed class PendingHeatmapUpdateWork
		{
			public List<List<HeatmapManager.HeatmapEntry>> CurrentMaps;
			public List<List<HeatmapManager.HeatmapEntry>> TempMaps;
			public List<HeatmapUpdateEntry> Entries;
			public int NextIndex;
			public long StartedTicks;
			public long SliceCpuMs;
			public int SliceCount;
		}

		private readonly struct DeferredSocialInferenceWork
		{
			public readonly SocialHistoryData History;
			public readonly Relationship Relationship;
			public readonly EntityID Context;
			public readonly Label SocialAction;
			public readonly int QueuedFrame;
			public readonly int EarliestFrame;
			public readonly int QueuedDay;
			public readonly int QueuedTurn;

			public DeferredSocialInferenceWork(SocialHistoryData history, Relationship relationship, EntityID context, Label socialAction, int queuedFrame, int earliestFrame, int queuedDay, int queuedTurn)
			{
				History = history;
				Relationship = relationship;
				Context = context;
				SocialAction = socialAction;
				QueuedFrame = queuedFrame;
				EarliestFrame = earliestFrame;
				QueuedDay = queuedDay;
				QueuedTurn = queuedTurn;
			}
		}

		private readonly struct DeferredExistingPickRefreshWork
		{
			public readonly PickContainer Container;
			public readonly PickTarget Target;
			public readonly int QueuedFrame;
			public readonly int EarliestFrame;

			public DeferredExistingPickRefreshWork(PickContainer container, PickTarget target, int queuedFrame, int earliestFrame)
			{
				Container = container;
				Target = target;
				QueuedFrame = queuedFrame;
				EarliestFrame = earliestFrame;
			}
		}

		private struct DeferredTakeoverPickRefreshWork
		{
			public PickManager Manager;
			public EntityID BuildingId;
			public int QueuedFrame;
			public int EarliestFrame;
			public bool BuildingPickRefreshed;

			public DeferredTakeoverPickRefreshWork(PickManager manager, EntityID buildingId, int queuedFrame, int earliestFrame)
			{
				Manager = manager;
				BuildingId = buildingId;
				QueuedFrame = queuedFrame;
				EarliestFrame = earliestFrame;
				BuildingPickRefreshed = false;
			}
		}

		private readonly struct DeferredCrewDialogRefreshWork
		{
			public readonly CrewDialog Dialog;
			public readonly SessionEventType EventType;
			public readonly EntityID EntityId;
			public readonly int QueuedFrame;
			public readonly int EarliestFrame;

			public DeferredCrewDialogRefreshWork(CrewDialog dialog, SessionEventType eventType, EntityID entityId, int queuedFrame, int earliestFrame)
			{
				Dialog = dialog;
				EventType = eventType;
				EntityId = entityId;
				QueuedFrame = queuedFrame;
				EarliestFrame = earliestFrame;
			}
		}

		private readonly struct DeferredCrewDialogSelectionWork
		{
			public readonly CrewDialog Dialog;
			public readonly SessionEvent Event;
			public readonly EntityID ActiveId;
			public readonly EntityID PreviousId;
			public readonly int QueuedFrame;
			public readonly int EarliestFrame;

			public DeferredCrewDialogSelectionWork(CrewDialog dialog, SessionEvent ev, int queuedFrame, int earliestFrame)
			{
				Dialog = dialog;
				Event = ev;
				ActiveId = ev.eid;
				PreviousId = ev.ctx is Entity previous ? previous.Id : EntityID.INVALID;
				QueuedFrame = queuedFrame;
				EarliestFrame = earliestFrame;
			}
		}

		private delegate void ReprocessSocialInferencesDelegate(SocialHistoryData history, Relationship rel, EntityID ctx, Label socialaction);

		private sealed class CachedDistinctSkillChoice
		{
			public int Frame;
			public int Day;
			public int Turn;
			public int CurrentSkillCount;
			public List<SkillDef> Skills;
		}

		private sealed class CachedModuleInTerritoryCheck
		{
			public int Frame;
			public bool Result;
		}

		private sealed class CachedTerritoryModulePresence
		{
			public int Frame;
			public HashSet<Label> Modules;
		}

		private sealed class CachedCanBuySellAvailability
		{
			public int Frame;
			public bool CanBuy;
			public bool CanSell;
		}

		private sealed class SkillRequirementScanStats
		{
			public int SkillsChecked;
			public int RequirementsChecked;
			public int RequirementsFailed;
			public long TotalRequirementTicks;
			public readonly Dictionary<string, long> TicksByType = new Dictionary<string, long>(StringComparer.Ordinal);
			public readonly Dictionary<string, int> CallsByType = new Dictionary<string, int>(StringComparer.Ordinal);
		}

		private struct DistinctSkillChoiceCacheState
		{
			public bool CacheHit;
			public bool OptimizedScan;
			public int TaggedSkillsSkipped;
			public string Key;
			public long StartTicks;
		}

		private struct ModuleInTerritoryCacheState
		{
			public bool CacheHit;
			public string Key;
			public long StartTicks;
		}

		private readonly struct HeatmapUpdateEntry
		{
			public readonly Heatmap Source;
			public readonly Heatmap Target;

			public HeatmapUpdateEntry(Heatmap source, Heatmap target)
			{
				Source = source;
				Target = target;
			}
		}

		private static readonly string[] SystemTurnTypeNames =
		{
			"Game.Session.Sim.BusinessTracker",
			"Game.Session.Sim.SimulationManager",
			"Game.Session.Sim.PeopleTracker",
			"Game.Session.Sim.RelationshipTracker",
			"Game.Session.Sim.ResidenceTracker",
			"Game.Session.Sim.PoliticsManager",
			"Game.Session.Sim.CopTracker",
			"Game.Session.Sim.DemandsTracker",
			"Game.Session.Sim.ResEventManager",
			"Game.Session.Sim.CombatManager",
			"Game.Session.Heatmaps.HeatmapManager",
			"Game.Session.Setup.SetupOrchestrator"
		};

		private static readonly HashSet<string> SimulationSubmanagerTypeNames = new HashSet<string>
		{
			"Game.Session.Sim.BusinessTracker",
			"Game.Session.Sim.PeopleTracker",
			"Game.Session.Sim.RelationshipTracker",
			"Game.Session.Sim.ResidenceTracker",
			"Game.Session.Sim.PoliticsManager",
			"Game.Session.Sim.CopTracker",
			"Game.Session.Sim.DemandsTracker",
			"Game.Session.Sim.ResEventManager",
			"Game.Session.Sim.CombatManager"
		};

		private static readonly string[] BusinessUpdatePhaseNames =
		{
			"Tick",
			"ClearAOEEffectsOnNodes",
			"UpdateBusinessModules",
			"UpdateGamblingModules",
			"UpdateRespectFromRelationships",
			"UpdateRespectFromTerritory",
			"UpdateRespectFromEthnicity",
			"UpdateRespectFromSafehouses",
			"UpdateHeatFromRelationships",
			"UpdateHeatFromNeighbors",
			"UpdateHeatFromBusinesses",
			"RecalculateHeatAndRespectForNodes"
		};

		private static readonly string[] PeopleTrackerPhaseNames =
		{
			"ProcessBirths",
			"ProcessMarriages",
			"ProcessDeaths"
		};

		private static readonly string[] PlayerTurnTypeNames =
		{
			"Game.Session.Player.AllPlayersManager",
			"Game.Session.Player.PlayerInfo",
			"Game.Session.Player.AutomationExecutor",
			"Game.Session.Player.CommandExecutor",
			"Game.Session.Player.PlayerCrew",
			"Game.Session.Player.PlayerCrewGrowth",
			"Game.Session.Player.PlayerFinances",
			"Game.Session.Player.PlayerGambling",
			"Game.Session.Player.PlayerOutposts",
			"Game.Session.Player.PlayerScheme",
			"Game.Session.Player.PlayerSkills",
			"Game.Session.Player.PlayerTerritory",
			"Game.Session.Player.PlayerThrone",
			"Game.Session.Player.AI.PlayerAI",
			"Game.Session.Player.KB.PlayerKB"
		};

		private static readonly string[] PlayerTurnMethodNames =
		{
			"OnPlayerTurnStarted",
			"OnPlayerTurnEnded",
			"GetPlayerTurnStatus",
			"IsPlayerTurnDone",
			"RefillPlayerCrewActions",
			"ConsumeMovementForBusyCrew"
		};

		private static readonly string[] AdvisorTurnTypeNames =
		{
			"Game.Session.Player.AI.UnitsAdvisor",
			"Game.Session.Player.AI.SocialAdvisor",
			"Game.Session.Player.AI.SafehouseAdvisor",
			"Game.Session.Player.AI.TerritoryAdvisor",
			"Game.Session.Player.AI.BusinessAdvisor",
			"Game.Session.Player.AI.PrecinctAdvisor",
			"Game.Session.Player.AI.FedsAdvisor",
			"Game.Session.Player.AI.GoonAdvisor",
			"Game.Session.Player.AI.AttackAdvisor",
			"Game.Session.Player.AI.CombatAdvisor"
		};

		private static readonly string[] AdvisorTurnMethodNames =
		{
			"OnTurnUpdate",
			"ProduceRequests"
		};

		private static readonly string[] PlayerAIStartDetailMethodNames =
		{
			"ClearAdvisorRequests",
			"OnTurnUpdate",
			"ProduceAdvisorRequests",
			"MaybeAssignFallbackTasks"
		};

		private static readonly string[] TerritoryAdvisorDetailMethodNames =
		{
			"FindOutpostToStart",
			"FindOutpostToStartHelper",
			"FindOutpostToVisit",
			"FindOutpostToVisitHelper",
			"UpdateNextNodeToExplore",
			"UpdateNextBuildingToScopeOut",
			"FindOutpostToSteal",
			"FindOutpostToStealHelper",
			"FindEverybodysOutposts",
			"FilterByDistanceToOurs",
			"FilterByAgreements",
			"ProduceSingleRequestRivals",
			"ProduceSingleRequestUpkeep",
			"UpdateExpiredTreaties"
		};

		private static readonly string[] TerritoryPotentialDetailMethodNames =
		{
			"PickBestNode",
			"Recalculate"
		};

		private static readonly string[] BusinessAdvisorTakeoverDetailMethodNames =
		{
			"TradeCallbackAtBusiness",
			"TryTakeOver",
			"TryTakeOverCasino"
		};

		private static readonly string[] PlayerTerritoryTakeoverDetailMethodNames =
		{
			"FindTakeoverDataForAI",
			"PerformTakeover",
			"SetScoped",
			"SetControlled",
			"LearnAboutResourcesFromTakeover"
		};

		private static readonly (string TypeName, string MethodName)[] StartupStageMethodNames =
		{
			("Game.Session.Sim.SimulationManager", "OnPreInteractive"),
			("Game.Session.Sim.PeopleTracker", "RunNewGameFamilyPlacement"),
			("Game.Session.Sim.BusinessTracker", "RunNewGameBusinessAssignments"),
			("Game.Session.Sim.PoliticsManager", "RunNewGamePoliticianSetup"),
			("Game.Session.Setup.SetupOrchestrator", "OnPreInteractiveAIGen"),
			("Game.Session.Setup.CreatePlayersHuman", "RunBlocking"),
			("Game.Session.Setup.CreatePlayersGang", "RunBlocking"),
			("Game.Session.Setup.CreatePlayersCops", "RunBlocking"),
			("Game.Session.Setup.CreatePlayersFeds", "RunBlocking"),
			("Game.Session.Setup.AfterCreatePlayers", "RunBlocking")
		};

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				int handlersPatched = 0;
				int businessPhasesPatched = 0;
				int playerTurnMethodsPatched = 0;
				int commandQueueDetailMethodsPatched = PatchCommandQueueDetailMethods(harmony);
				int cityGenFrameMethodsPatched = PatchCityGenFrameMethods(harmony);
				int interactiveFrameMethodsPatched = PatchInteractiveTurnFrameMethods(harmony);
				int startupStageMethodsPatched = PatchStartupStageMethods(harmony);
				int playerCrewPromptGuardsPatched = PatchPlayerCrewPromptGuards(harmony);
				int playerCrewGrowthDeferralMethodsPatched = PatchPlayerCrewGrowthCandidateDeferral(harmony);
				int playerCrewTurnStartDetailMethodsPatched = PatchPlayerCrewTurnStartDetailMethods(harmony);
				int residenceImmigrationMethodsPatched = PatchResidenceImmigrationBatching(harmony);
				int heatmapUpdateMethodsPatched = PatchHeatmapUpdateBatching(harmony);
				int dirtyCashQuietUpgradeMethodsPatched = PatchDirtyCashQuietLegalUpgradeList(harmony);
				int setupPresenceMethodsPatched = PatchSetupPresenceTurnUpdates(harmony);
				int playerAIStartDetailMethodsPatched = PatchPlayerAIStartDetailMethods(harmony);
				int playerAIAssignRequestDetailMethodsPatched = PatchPlayerAIAssignRequestDetailMethods(harmony);
				int territoryAdvisorDetailMethodsPatched = PatchTerritoryAdvisorDetailMethods(harmony);
				int takeoverDetailMethodsPatched = PatchTakeoverDetailMethods(harmony);
				int manufactureModuleDetailMethodsPatched = PatchManufactureModuleDetailMethods(harmony);
				int politicsDetailMethodsPatched = PatchPoliticsDetailMethods(harmony);
				int socialActionDetailMethodsPatched = PatchSocialActionDetailMethods(harmony);
				int actionCompletionDetailMethodsPatched = PatchActionCompletionDetailMethods(harmony);
				foreach (string typeName in SystemTurnTypeNames)
				{
					MethodInfo method = AccessTools.Method(AccessTools.TypeByName(typeName), "OnSystemTurn");
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(
						harmony,
						method,
						SimulationSubmanagerTypeNames.Contains(typeName) ? SimulationSubmanagerDetailThresholdMs : SlowStageThresholdMs);
					handlersPatched++;
				}

				Type businessUpdateType = AccessTools.TypeByName("Game.Session.Sim.BusinessUpdate");
				foreach (string methodName in BusinessUpdatePhaseNames)
				{
					MethodInfo method = AccessTools.Method(businessUpdateType, methodName);
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(harmony, method);
					businessPhasesPatched++;
				}

				int peoplePhasesPatched = 0;
				foreach (string methodName in PeopleTrackerPhaseNames)
				{
					MethodInfo method = AccessTools.Method(typeof(PeopleTracker), methodName);
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(harmony, method);
					peoplePhasesPatched++;
				}

				foreach (string typeName in PlayerTurnTypeNames)
				{
					Type type = AccessTools.TypeByName(typeName);
					if (type == null)
					{
						continue;
					}

					foreach (string methodName in PlayerTurnMethodNames)
					{
						MethodInfo method = FindInstanceMethodNoWarn(type, methodName);
						if (method == null)
						{
							continue;
						}

						if (type == typeof(PlayerCrewGrowth) && string.Equals(methodName, "OnPlayerTurnStarted", StringComparison.Ordinal))
						{
							continue;
						}

						PatchTimedMethod(harmony, method, PlayerTurnSlowStageThresholdMs);
						playerTurnMethodsPatched++;
					}
				}

				int advisorTurnMethodsPatched = PatchAdvisorTurnMethods(harmony);
				int moduleMethodsPatched = PatchModuleLookupMethods(harmony);
				int externalDirtyCashMethodsPatched = PatchExternalDirtyCashMethods(harmony);
				bool modulesDoUpdateDelegateWarmed = WarmTurnPerformanceDelegates();
				bool economyBridgeMethodsWarmed = WarmAfterProhibitionEconomyBridgeMethods();

				Debug.Log("[PERF][TurnStage] diagnostics-applied handlers=" + handlersPatched + " businessPhases=" + businessPhasesPatched + " peoplePhases=" + peoplePhasesPatched + " playerTurnMethods=" + playerTurnMethodsPatched + " advisorTurnMethods=" + advisorTurnMethodsPatched + " playerAIStartDetails=" + playerAIStartDetailMethodsPatched + " playerAIAssignRequestDetails=" + playerAIAssignRequestDetailMethodsPatched + " territoryAdvisorDetails=" + territoryAdvisorDetailMethodsPatched + " takeoverDetails=" + takeoverDetailMethodsPatched + " manufactureModuleDetails=" + manufactureModuleDetailMethodsPatched + " politicsDetails=" + politicsDetailMethodsPatched + " socialActionDetails=" + socialActionDetailMethodsPatched + " actionCompletionDetails=" + actionCompletionDetailMethodsPatched + " cityGenFrames=" + cityGenFrameMethodsPatched + " interactiveFrames=" + interactiveFrameMethodsPatched + " startupStages=" + startupStageMethodsPatched + " playerCrewPromptGuards=" + playerCrewPromptGuardsPatched + " playerCrewGrowthDeferral=" + playerCrewGrowthDeferralMethodsPatched + " playerCrewTurnStartDetails=" + playerCrewTurnStartDetailMethodsPatched + " residenceImmigrationBatch=" + residenceImmigrationMethodsPatched + " heatmapUpdateSlice=" + heatmapUpdateMethodsPatched + " dirtyCashQuietUpgrade=" + dirtyCashQuietUpgradeMethodsPatched + " setupPresenceSkip=" + setupPresenceMethodsPatched + " commandQueueDetails=" + commandQueueDetailMethodsPatched + " moduleMethods=" + moduleMethodsPatched + " dirtyCashMethods=" + externalDirtyCashMethodsPatched + " modulesDoUpdateDelegateWarmed=" + modulesDoUpdateDelegateWarmed + " economyBridgeMethodsWarmed=" + economyBridgeMethodsWarmed + " thresholdMs=" + SlowStageThresholdMs + " cityGenThresholdMs=" + CityGenSlowStageThresholdMs + " playerThresholdMs=" + PlayerTurnSlowStageThresholdMs + " advisorThresholdMs=" + AdvisorTurnSlowStageThresholdMs + " territoryAdvisorDetailThresholdMs=" + TerritoryAdvisorDetailThresholdMs + " takeoverDetailThresholdMs=" + TakeoverDetailThresholdMs + " playerAIStartDetailThresholdMs=" + PlayerAIStartDetailThresholdMs + " playerAIAssignRequestsDetailThresholdMs=" + PlayerAIAssignRequestsDetailThresholdMs + " playerAIAssignRequestsAggregateThresholdMs=" + PlayerAIAssignRequestsAggregateThresholdMs + " playerCrewTurnStartDetailThresholdMs=" + PlayerCrewTurnStartDetailThresholdMs + " manufactureModuleDetailThresholdMs=" + ManufactureModuleDetailThresholdMs + " humanCrewCandidateRecomputeThresholdMs=" + HumanCrewCandidateRecomputeDetailThresholdMs + " politicsDetailThresholdMs=" + PoliticsDetailThresholdMs + " simulationSubmanagerThresholdMs=" + SimulationSubmanagerDetailThresholdMs + " hudInputThresholdMs=" + HudInputDetailThresholdMs + " residenceImmigrationBatchTurns=" + ResidenceImmigrationBatchTurns + " interactiveHeatmapEntriesPerFrame=" + InteractiveHeatmapEntriesPerFrame + " interactiveHeatmapBudgetMs=" + InteractiveHeatmapUpdateBudgetMs + " interactiveBusinessModuleUpdatesPerFrame=" + InteractiveBusinessModuleUpdatesPerFrame + " interactiveBusinessModuleBudgetMs=" + InteractiveBusinessModuleUpdateBudgetMs + " largeSaveBusinessModuleBudgetMs=" + LargeSaveBusinessModuleUpdateBudgetMs + " interactiveBusinessTrackerFrameBudgetMs=" + InteractiveBusinessTrackerFrameBudgetMs + " largeSaveBusinessTrackerFrameBudgetMs=" + LargeSaveBusinessTrackerFrameBudgetMs + " interactiveBusinessTrackerCheapContinuationBudgetMs=" + InteractiveBusinessTrackerCheapContinuationBudgetMs + " largeSaveBusinessTrackerCheapContinuationBudgetMs=" + LargeSaveBusinessTrackerCheapContinuationBudgetMs + " largeSaveBusinessTrackerFinalTailBudgetMs=" + LargeSaveBusinessTrackerFinalTailBudgetMs + " largeSaveBusinessTrackerPostRelationshipTailBudgetMs=" + LargeSaveBusinessTrackerPostRelationshipTailBudgetMs + " interactiveNonHumanBusinessModuleUpdateCohorts=" + InteractiveNonHumanBusinessModuleUpdateCohorts + " largeSaveNonHumanBusinessModuleUpdateCohorts=" + LargeSaveNonHumanBusinessModuleUpdateCohorts + " interactiveRelationshipRespectPlayersPerFrame=" + InteractiveRelationshipRespectPlayersPerFrame + " interactiveRelationshipRespectBudgetMs=" + InteractiveRelationshipRespectBudgetMs + " largeSaveRelationshipRespectPlayersPerFrame=" + LargeSaveRelationshipRespectPlayersPerFrame + " largeSaveRelationshipRespectBudgetMs=" + LargeSaveRelationshipRespectBudgetMs + " businessModuleFamilyProfileDiagnostics=" + EnableBusinessModuleFamilyProfileDiagnostics + " aiCommandExecutorMaxTuplesPerTurn=" + AiCommandExecutorMaxTuplesPerTurn + " aiCommandExecutorBudgetMs=" + AiCommandExecutorBudgetMs + " aiCommandQueueMaxCompletedPerTurn=" + AiCommandQueueMaxCompletedCommandsPerTurn + " aiCommandQueueBudgetMs=" + AiCommandQueueBudgetMs + " interactiveMaxAdvances=" + InteractiveTurnMaxAdvancesPerFrame + " interactiveSystemHandlersPerFrame=" + InteractiveSystemTurnMaxHandlersPerFrame + " interactiveSimulationSubmanagersPerFrame=" + InteractiveSimulationSubmanagersPerFrame + " interactiveBudgetMs=" + InteractiveTurnFrameBudgetMs + " interactiveCheapContinuationBudgetMs=" + InteractiveCheapContinuationBudgetMs + " interactiveBusinessPhaseCheapContinuationBudgetMs=" + InteractiveBusinessPhaseCheapContinuationBudgetMs + " interactivePostBusinessSimulationTailBudgetMs=" + InteractivePostBusinessSimulationTailBudgetMs + " largeSaveBusinessEntrySimulationBudgetMs=" + LargeSaveBusinessEntrySimulationBudgetMs + " largeSavePostBusinessSimulationTailBudgetMs=" + LargeSavePostBusinessSimulationTailBudgetMs);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Turn performance diagnostics patch setup failed: " + ex.Message);
			}
		}

		private static bool WarmTurnPerformanceDelegates()
		{
			long startTicks = Stopwatch.GetTimestamp();
			bool modulesDelegateReady = GetModulesComponentDoUpdateDelegate() != null;
			bool residenceDelegateReady = GetResidenceDailyImmigrationDelegate() != null;
			long elapsedMs = GetElapsedMilliseconds(startTicks);
			if (elapsedMs >= 5)
			{
				Debug.Log("[PERF][TurnStage] delegate-warmup ms=" + elapsedMs + " modulesDoUpdate=" + modulesDelegateReady + " residenceImmigration=" + residenceDelegateReady);
			}
			return modulesDelegateReady;
		}

		private static bool WarmAfterProhibitionEconomyBridgeMethods()
		{
			long startTicks = Stopwatch.GetTimestamp();
			try
			{
				bool ownsMutation = DoesAfterProhibitionEconomyOwnDirtyCashRuntimeSweepMutation();
				bool ownsMethodReady = _afterProhibitionEconomyOwnsDirtyCashRuntimeSweepMutationMethod != null;
				bool beginReady = GetAfterProhibitionEconomyMethod(ref _afterProhibitionEconomyBeginDirtyCashRuntimeSweepBusinessUpdateMethod, "BeginDirtyCashRuntimeSweepBusinessUpdate") != null;
				bool completeReady = GetAfterProhibitionEconomyMethod(ref _afterProhibitionEconomyCompleteDirtyCashRuntimeSweepBusinessUpdateMethod, "CompleteDirtyCashRuntimeSweepBusinessUpdate") != null;
				bool endReady = GetAfterProhibitionEconomyMethod(ref _afterProhibitionEconomyEndDirtyCashRuntimeSweepBusinessUpdateMethod, "EndDirtyCashRuntimeSweepBusinessUpdate") != null;
				bool purchaseReady = AfterProhibitionPurchaseStockRunMethod != null;
				bool repairReady = AfterProhibitionEmptyBusinessRepairRunMethod != null;
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs >= 5)
				{
					Debug.Log("[PERF][TurnStage] economy-bridge-warmup ms=" + elapsedMs + " ownsMutation=" + ownsMutation + " ownsMethod=" + ownsMethodReady + " begin=" + beginReady + " complete=" + completeReady + " end=" + endReady + " purchase=" + purchaseReady + " repair=" + repairReady);
				}

				return ownsMethodReady && beginReady && completeReady && endReady;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] After Prohibition economy bridge warmup failed: " + ex.GetType().Name + ":" + ex.Message);
				return false;
			}
		}

		private static int PatchSetupPresenceTurnUpdates(Harmony harmony)
		{
			if (harmony == null)
			{
				return 0;
			}

			MethodInfo method = AccessTools.Method(AccessTools.TypeByName("Game.Session.Setup.SetupOrchestrator"), "OnSystemTurn");
			if (method == null)
			{
				return 0;
			}

			try
			{
				harmony.Patch(
					method,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(SetupOrchestratorOnSystemTurnPrefix))
					{
						priority = Priority.First
					});
				return 1;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetupOrchestrator presence throttle patch skipped: " + ex.GetType().Name + ":" + ex.Message);
				return 0;
			}
		}

		private static int PatchCityGenFrameMethods(Harmony harmony)
		{
			Type sessionContextType = AccessTools.TypeByName("Game.Session.SessionContext");
			MethodInfo frameUpdate = AccessTools.Method(sessionContextType, "FrameUpdateDuringCityGen");
			if (frameUpdate == null)
			{
				return 0;
			}

			PatchTimedMethod(harmony, frameUpdate);
			return 1;
		}

		private static int PatchInteractiveTurnFrameMethods(Harmony harmony)
		{
			int patched = 0;
			MethodInfo frameUpdate = AccessTools.Method(typeof(SessionContext), "FrameUpdateDuringInteractive");
			if (frameUpdate != null)
			{
				harmony.Patch(
					frameUpdate,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(FrameUpdateDuringInteractivePrefix)));
				patched++;
			}

			MethodInfo refreshTurn = AccessTools.Method(typeof(HUDBar), "TryRefreshTurn");
			if (refreshTurn != null)
			{
				harmony.Patch(
					refreshTurn,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HUDBarTryRefreshTurnPrefix)));
				patched++;
			}

			MethodInfo nextTurnClick = AccessTools.Method(typeof(HUDBar), "OnNextTurnClick");
			if (nextTurnClick != null)
			{
				harmony.Patch(
					nextTurnClick,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HUDBarOnNextTurnClickPrefix)),
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HUDBarOnNextTurnClickPostfix)));
				patched++;
			}

			MethodInfo updateAnimations = FindInstanceMethodNoWarn(typeof(HUDBar), "UpdateAnimations");
			if (updateAnimations != null)
			{
				harmony.Patch(
					updateAnimations,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HudUpdateAnimationsDetailPrefix)),
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HudUpdateAnimationsDetailPostfix)));
				patched++;
			}

			foreach (string methodName in new[] { "TryRefreshTurn", "UpdateNextTurnButton", "TryRefreshMoney", "TryRefreshCrew", "TryRefreshCorners", "ProcessHyperlink" })
			{
				MethodInfo method = FindInstanceMethodNoWarn(typeof(HUDBar), methodName);
				if (method == null)
				{
					continue;
				}

				if (string.Equals(methodName, "TryRefreshCorners", StringComparison.Ordinal))
				{
					harmony.Patch(
						method,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HUDBarTryRefreshCornersPrefix)));
				}
				harmony.Patch(
					method,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HudUpdateStepDetailPrefix)),
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HudUpdateStepDetailPostfix)));
				patched++;
			}

			return patched;
		}

		private static int PatchPlayerCrewPromptGuards(Harmony harmony)
		{
			MethodInfo method = AccessTools.Method(typeof(PlayerCrew), "HandlePromotionPrompting");
			if (method == null)
			{
				return 0;
			}

			harmony.Patch(
				method,
				prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(SkipAiPromotionPromptingPrefix)));
			return 1;
		}

		private static int PatchPlayerCrewGrowthCandidateDeferral(Harmony harmony)
		{
			if (harmony == null || PlayerCrewGrowthRecomputeHumanCrewCandidatesMethod == null)
			{
				return 0;
			}

			int patched = 0;
			try
			{
				MethodInfo turnStarted = AccessTools.Method(typeof(PlayerCrewGrowth), "OnPlayerTurnStarted", new[] { typeof(PlayerCrew) });
				if (turnStarted != null)
				{
					harmony.Patch(
						turnStarted,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(PlayerCrewGrowthOnPlayerTurnStartedPrefix))
						{
							priority = Priority.First
						},
						finalizer: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(PlayerCrewGrowthOnPlayerTurnStartedFinalizer))
						{
							priority = Priority.Last
						});
					patched++;
				}

				harmony.Patch(
					PlayerCrewGrowthRecomputeHumanCrewCandidatesMethod,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(PlayerCrewGrowthRecomputeHumanCrewCandidatesPrefix))
					{
						priority = Priority.First
					},
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(PlayerCrewGrowthRecomputeHumanCrewCandidatesPostfix))
					{
						priority = Priority.Last
					});
				patched++;

				foreach (string methodName in new[] { "HasAnyCrewCandidatesFrom", "GetAllCrewCandidateFrom", "GetBestCrewCandidateFrom" })
				{
					MethodInfo accessMethod = FindInstanceMethodNoWarn(typeof(PlayerCrewGrowth), methodName);
					if (accessMethod == null)
					{
						continue;
					}

					harmony.Patch(
						accessMethod,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(PlayerCrewGrowthCandidateAccessPrefix))
						{
							priority = Priority.First
						});
					patched++;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Human crew candidate turn-start deferral patch skipped: " + ex.GetType().Name + ":" + ex.Message);
				return patched;
			}

			return patched;
		}

		private static int PatchPlayerCrewTurnStartDetailMethods(Harmony harmony)
		{
			if (harmony == null)
			{
				return 0;
			}

			int patched = 0;
			foreach (string methodName in new[] { "HandleLevelups", "HandleJunkCars", "HandleBossDeath", "HandlePromotionPrompting", "HandleQueuedReturns" })
			{
				MethodInfo method = FindInstanceMethodNoWarn(typeof(PlayerCrew), methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, PlayerCrewTurnStartDetailThresholdMs);
				patched++;
			}

			return patched;
		}

		private static int PatchResidenceImmigrationBatching(Harmony harmony)
		{
			MethodInfo method = AccessTools.Method(typeof(ResidenceTracker), "OnSystemTurn");
			if (method == null || ResidenceDailyImmigrationMethod == null)
			{
				return 0;
			}

			harmony.Patch(
				method,
				prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ResidenceTrackerOnSystemTurnPrefix)));
			return 1;
		}

		private static int PatchHeatmapUpdateBatching(Harmony harmony)
		{
			MethodInfo method = AccessTools.Method(typeof(HeatmapManager), "OnSystemTurn");
			if (method == null || HeatmapManagerMapsField == null || HeatmapManagerTempField == null)
			{
				return 0;
			}

			harmony.Patch(
				method,
				prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(HeatmapManagerOnSystemTurnPrefix)));
			return 1;
		}

		private static int PatchDirtyCashQuietLegalUpgradeList(Harmony harmony)
		{
			MethodInfo method = AccessTools.Method(typeof(ModulesUtil), "FindUpgradesOrNull");
			if (method == null || AccessTools.TypeByName("DirtyCashEconomy.FindUpgradesOrNullPatch") == null)
			{
				return 0;
			}

			HarmonyMethod prefix = new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(QuietDirtyCashLegalFindUpgradesPrefix))
			{
				priority = Priority.First
			};
			harmony.Patch(method, prefix: prefix);
			int patched = 1;
			MethodInfo getPlayerLegalModules = AccessTools.Method(AccessTools.TypeByName("DirtyCashEconomy.LegalBusinessPatches"), "GetPlayerLegalModules");
			if (getPlayerLegalModules != null)
			{
				harmony.Patch(
					getPlayerLegalModules,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(QuietDirtyCashGetPlayerLegalModulesPrefix))
					{
						priority = Priority.First
					});
				patched++;
			}
			return patched;
		}

		private static int PatchCommandQueueDetailMethods(Harmony harmony)
		{
			int patched = 0;
			MethodInfo executeCommands = FindInstanceMethodNoWarn(typeof(CommandExecutor), "ExecuteCommandsInQueue");
			if (executeCommands != null)
			{
				HarmonyMethod executorLimiterPrefix = new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(LimitAiCommandExecutorPrefix))
				{
					priority = Priority.Last
				};
				harmony.Patch(
					executeCommands,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CommandExecutorDetailPrefix)),
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CommandExecutorDetailPostfix)));
				harmony.Patch(executeCommands, prefix: executorLimiterPrefix);
				patched++;
			}

			MethodInfo processCommandQueue = FindInstanceMethodNoWarn(typeof(CommandExecutor), "ProcessCommandQueue");
			if (processCommandQueue != null)
			{
				HarmonyMethod limiterPrefix = new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(LimitAiCommandQueuePrefix))
				{
					priority = Priority.Last
				};
				harmony.Patch(
					processCommandQueue,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CommandQueueDetailPrefix)),
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CommandQueueDetailPostfix)));
				harmony.Patch(processCommandQueue, prefix: limiterPrefix);
				patched++;
			}

			return patched;
		}

		private sealed class CommandExecutorDetailState
		{
			public CommandExecutor Executor;
			public long StartTicks;
			public int PlayerId;
			public int TupleCountBefore;
			public int MarkedBefore;
			public int ActiveBefore;
			public int QueuedBefore;
			public int ProcessedTuples;
			public long TotalTupleMs;
			public long MaxTupleMs;
			public ulong MaxTuplePeepId;
			public string MaxTupleActiveBefore;
			public string MaxTupleNextBefore;
			public int MaxTupleQueueBefore;
		}

		private sealed class CommandExecutorLimiterState
		{
			public int NextTupleIndex;
		}

		private sealed class CommandQueueDetailState
		{
			public long StartTicks;
			public int PlayerId;
			public ulong PeepId;
			public string ActiveBefore;
			public string NextBefore;
			public int QueueCountBefore;
			public bool MarkedBefore;
			public bool LimitedByGameplayTweaks;
			public bool BudgetDeferred;
			public int CompletedCommands;
			public long LimiterElapsedMs;
			public string LastLimiterOutcome;
			public int StepCount;
			public long StepTotalMs;
			public long MaxStepMs;
			public string MaxStepName;
			public string MaxStepCommand;
		}

		private sealed class PlayerAIAssignRequestsDetailState
		{
			public long StartTicks;
			public int PlayerId;
			public int DispatchAttempts;
			public int AssignedAttempts;
			public int AvailableAttempts;
			public int Dispatched;
			public int SlowDispatches;
			public long TotalDispatchMs;
			public long MaxDispatchMs;
			public string MaxDispatchPath;
			public string MaxDispatchAdvisor;
			public string MaxDispatchScript;
			public string MaxDispatchPriority;
			public string MaxDispatchAssignedTo;
			public int CompletionCalls;
			public int SlowCompletions;
			public long TotalCompletionMs;
			public long MaxCompletionMs;
			public string MaxCompletionMethod;
			public string MaxCompletionAdvisor;
			public string MaxCompletionScript;
			public string MaxCompletionPriority;
			public string MaxCompletionAssignedTo;
			public int LogRequestCalls;
			public int SlowLogRequests;
			public long TotalLogRequestMs;
			public long MaxLogRequestMs;
			public string MaxLogRequestAdvisor;
			public string MaxLogRequestScript;
			public string MaxLogRequestPriority;
			public string MaxLogRequestAssignedTo;
		}

		private sealed class PlayerAIDispatchDetailState
		{
			public long StartTicks;
			public string Advisor;
			public string Script;
			public string Priority;
			public string AssignedTo;
			public int ScriptDispatchCalls;
			public long ScriptDispatchMs;
			public long MaxScriptDispatchMs;
			public string MaxScriptDispatchScript;
			public string MaxScriptDispatchPeep;
		}

		private readonly struct PlayerAIRequestStepTimingState
		{
			public readonly long StartTicks;
			public readonly string Advisor;
			public readonly string Script;
			public readonly string Priority;
			public readonly string AssignedTo;

			public PlayerAIRequestStepTimingState(long startTicks, AdvisorRequest request)
			{
				StartTicks = startTicks;
				Advisor = FormatAdvisorForLog(request);
				Script = request != null ? FormatLabelForLog(request.script) : "none";
				Priority = request?.priority.ToString() ?? "none";
				AssignedTo = request != null ? FormatEntityForLog(request.assignedTo) : "none";
			}
		}

		private sealed class ScriptDispatcherDetailState
		{
			public long StartTicks;
			public string Script;
			public string Peep;
			public int ValidateCalls;
			public long ValidateMs;
			public long MaxValidateMs;
			public int StepToCommandCalls;
			public long StepToCommandMs;
			public long MaxStepToCommandMs;
			public string MaxStepToCommandType;
			public int AddCommandCalls;
			public long AddCommandMs;
			public long MaxAddCommandMs;
			public string MaxAddCommandType;
			public int QueueProcessCalls;
			public long QueueProcessMs;
			public long MaxQueueProcessMs;
			public string MaxQueueProcessCommand;
		}

		private readonly struct ScriptStepTimingState
		{
			public readonly long StartTicks;
			public readonly string StepType;

			public ScriptStepTimingState(long startTicks, string stepType)
			{
				StartTicks = startTicks;
				StepType = stepType;
			}
		}

		private readonly struct ScriptCommandTimingState
		{
			public readonly long StartTicks;
			public readonly string Command;

			public ScriptCommandTimingState(long startTicks, string command)
			{
				StartTicks = startTicks;
				Command = command;
			}
		}

		private sealed class HudUpdateAnimationDetailState
		{
			public long StartTicks;
			public HudUpdateAnimationDetailState Previous;
			public long RefreshTurnMs;
			public long RefreshMoneyMs;
			public long RefreshCrewMs;
			public long RefreshCornersMs;
			public long NextTurnButtonMs;
			public long HyperlinkMs;
			public long OtherStepMs;
			public int RefreshTurnCalls;
			public int RefreshMoneyCalls;
			public int RefreshCrewCalls;
			public int RefreshCornersCalls;
			public int NextTurnButtonCalls;
			public int HyperlinkCalls;
			public int OtherStepCalls;
			public long MaxStepMs;
			public string MaxStepName;
		}

		private readonly struct HudUpdateStepTimingState
		{
			public readonly long StartTicks;
			public readonly string MethodName;

			public HudUpdateStepTimingState(long startTicks, string methodName)
			{
				StartTicks = startTicks;
				MethodName = methodName;
			}
		}

		private sealed class ManufactureModuleDetailState
		{
			public long StartTicks;
			public string ModuleId;
			public int RecipeIndex;
			public int ProduceCount;
			public int ConsumeCount;
			public int SelloffCount;
			public int RefillCount;
			public int LastUpdateDay;
			public int OriginalLastUpdateDay;
			public int CatchupClampDays;
			public bool OwnerIsHuman;
		}

		private sealed class IndexedInventoryMutator
		{
			private readonly List<ResourceAndQty> _contents;
			private readonly Dictionary<Label, int> _indexById = new Dictionary<Label, int>();

			public IndexedInventoryMutator(InventoryModuleData data)
			{
				_contents = data?.contents;
				RebuildIndex();
			}

			public Fixnum GetQty(Label id)
			{
				if (_contents == null)
				{
					return Fixnum.ZERO;
				}

				if (_indexById.TryGetValue(id, out int index) && index >= 0 && index < _contents.Count)
				{
					return _contents[index].qty;
				}

				return Fixnum.ZERO;
			}

			public bool Increment(Label id, Fixnum delta)
			{
				if (_contents == null)
				{
					return false;
				}

				if (_indexById.TryGetValue(id, out int index) && index >= 0 && index < _contents.Count)
				{
					ResourceAndQty current = _contents[index];
					if (current.qty + delta < 0)
					{
						return false;
					}

					ResourceAndQty updated = current.IncrementQuantity(delta);
					if (updated.qty.IsZero)
					{
						_contents.RemoveAt(index);
						RebuildIndex();
					}
					else
					{
						_contents[index] = updated;
					}

					return true;
				}

				if (delta > 0)
				{
					_indexById[id] = _contents.Count;
					_contents.Add(new ResourceAndQty(id, delta));
					return true;
				}

				return false;
			}

			private void RebuildIndex()
			{
				_indexById.Clear();
				if (_contents == null)
				{
					return;
				}

				for (int i = 0; i < _contents.Count; i++)
				{
					Label id = _contents[i].id;
					if (!_indexById.ContainsKey(id))
					{
						_indexById[id] = i;
					}
				}
			}
		}

		private static bool LimitAiCommandExecutorPrefix(CommandExecutor __instance)
		{
			try
			{
				if (__instance == null
					|| __instance.PID.IsHumanPlayer
					|| !(global::Game.Game.ctx?.IsInteractive ?? false)
					|| CommandExecutorProcessCommandQueueMethod == null
					|| CommandExecutorRemoveDeprecatedTuplesMethod == null)
				{
					return true;
				}

				List<CommandExecutor.CommandTuple> tuples = __instance.tuples;
				int tupleCount = tuples?.Count ?? 0;
				if (tupleCount == 0)
				{
					return false;
				}

				CommandExecutorLimiterState limiterState = CommandExecutorLimiterStates.GetOrCreateValue(__instance);
				int cursor = limiterState.NextTupleIndex;
				if (cursor < 0 || cursor >= tupleCount)
				{
					cursor = 0;
				}

				long startTicks = Stopwatch.GetTimestamp();
				int scanned = 0;
				int runnable = 0;
				int processed = 0;
				bool budgetDeferred = false;
				while (scanned < tupleCount)
				{
					if (tuples.Count == 0)
					{
						cursor = 0;
						break;
					}

					if (cursor >= tuples.Count)
					{
						cursor = 0;
					}

					CommandExecutor.CommandTuple tuple = tuples[cursor];
					cursor++;
					scanned++;
					if (tuple == null || tuple.markedForRemoval || !tuple.HasAnyCommands)
					{
						continue;
					}

					runnable++;
					CommandExecutorProcessCommandQueueMethod.Invoke(__instance, new object[] { tuple });
					processed++;
					if (processed >= AiCommandExecutorMaxTuplesPerTurn)
					{
						budgetDeferred = scanned < tupleCount;
						break;
					}

					if (GetElapsedMilliseconds(startTicks) >= AiCommandExecutorBudgetMs)
					{
						budgetDeferred = scanned < tupleCount;
						break;
					}
				}

				limiterState.NextTupleIndex = tuples.Count == 0 ? 0 : cursor % tuples.Count;
				CommandExecutorRemoveDeprecatedTuplesMethod.Invoke(__instance, null);

				long elapsedMs = GetElapsedMilliseconds(startTicks);
				long limiterLogThresholdMs = IsPerformanceDiagnosticsEnabled()
					? CommandExecutorDetailThresholdMs
					: CompactTurnHandlerDetailThresholdMs;
				if ((budgetDeferred && IsPerformanceDiagnosticsEnabled()) || elapsedMs >= limiterLogThresholdMs)
				{
					Debug.Log(
						"[PERF][CommandExecutorLimiter] ms=" + elapsedMs +
						" pid=" + TryGetPlayerSubmanagerPid(__instance) +
						" currentPid=" + GetPlayerForLog() +
						" tuples=" + tupleCount +
						" scanned=" + scanned +
						" runnable=" + runnable +
						" processed=" + processed +
						" nextCursor=" + limiterState.NextTupleIndex +
						" budgetDeferred=" + budgetDeferred +
						" maxTuples=" + AiCommandExecutorMaxTuplesPerTurn +
						" budgetMs=" + AiCommandExecutorBudgetMs +
						" day=" + GetDayForLog() +
						" year=" + GetYearForLog() +
						" turn=" + GetTurnForLog() +
						" totalPlayers=" + GetTotalPlayersForLog());
				}

				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] AI command executor limiter fell back to vanilla: " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static bool LimitAiCommandQueuePrefix(CommandExecutor __instance, CommandExecutor.CommandTuple tuple)
		{
			long limiterStartTicks = Stopwatch.GetTimestamp();
			CommandQueueDetailState queueState = null;
			int completedCommands = 0;
			try
			{
				if (tuple == null
					|| tuple.markedForRemoval
					|| __instance == null
					|| !(global::Game.Game.ctx?.IsInteractive ?? false))
				{
					return true;
				}

				bool isHumanQueue = __instance.PID.IsHumanPlayer;
				queueState = _activeCommandQueueDetail;
				Command lastCompletedCommand = null;
				while (tuple.HasAnyCommands)
				{
					if (tuple.active == null)
					{
						Command next = tuple.NextInQueue;
						if (next == null
							|| !TimeCommandQueueStep(__instance, tuple, queueState, "can-activate", next, () => next.CanActivateAfterDequeue()))
						{
							MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: false, "blocked");
							break;
						}

						if (!isHumanQueue && ShouldDeferCommandQueueForBudget(limiterStartTicks, completedCommands))
						{
							if (!ShouldAllowImmediateAiFollowupAfterCompletedCommand(lastCompletedCommand, tuple))
							{
								MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: true, "budget-before-activate");
								return false;
							}
						}

						TimeCommandQueueStep(__instance, tuple, queueState, "activate", next, () => tuple.ActivateFirst());
						Command active = tuple.active;
						Command.StartStatus startStatus = TimeCommandQueueStep(__instance, tuple, queueState, "start", active, () => active.Start());
						switch (startStatus)
						{
							case Command.StartStatus.Failed:
								TimeCommandQueueStep(__instance, tuple, queueState, "deactivate-failed", active, () => tuple.Deactivate());
								completedCommands++;
								lastCompletedCommand = active;
								if (!isHumanQueue
									&& completedCommands >= AiCommandQueueMaxCompletedCommandsPerTurn
									&& !ShouldAllowImmediateAiFollowupAfterCompletedCommand(lastCompletedCommand, tuple))
								{
									MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: false, "completed-limit-after-failed-start");
									return false;
								}
								continue;
							case Command.StartStatus.SkipThisTurn:
								MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: false, "skip-this-turn");
								return false;
						}

						if (!isHumanQueue && ShouldDeferCommandQueueForBudget(limiterStartTicks, completedCommands))
						{
							if (!ShouldAllowImmediateAiFollowupAfterCompletedCommand(lastCompletedCommand, tuple))
							{
								MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: true, "budget-after-start");
								return false;
							}
						}
					}

					Command commandToExecute = tuple.active;
					if (!TimeCommandQueueStep(__instance, tuple, queueState, "execute", commandToExecute, () => commandToExecute.ExecuteSingleTurn()))
					{
						TimeCommandQueueStep(__instance, tuple, queueState, "finish", commandToExecute, () => commandToExecute.Finish(success: true));
						TimeCommandQueueStep(__instance, tuple, queueState, "deactivate-complete", commandToExecute, () => tuple.Deactivate());
						completedCommands++;
						lastCompletedCommand = commandToExecute;
						if (!isHumanQueue
							&& completedCommands >= AiCommandQueueMaxCompletedCommandsPerTurn
							&& !ShouldAllowImmediateAiFollowupAfterCompletedCommand(lastCompletedCommand, tuple))
						{
							MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: false, "completed-limit");
							return false;
						}

						if (!isHumanQueue && ShouldDeferCommandQueueForBudget(limiterStartTicks, completedCommands))
						{
							if (!ShouldAllowImmediateAiFollowupAfterCompletedCommand(lastCompletedCommand, tuple))
							{
								MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: true, "budget-after-complete");
								return false;
							}
						}

						continue;
					}

					MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: false, "continues");
					break;
				}

				MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: false, "drained-or-blocked");
				return false;
			}
			catch (Exception ex)
			{
				if (tuple != null && !tuple.HasAnyCommands)
				{
					MarkCommandQueueLimiterState(queueState, completedCommands, limiterStartTicks, budgetDeferred: false, "cleared-after-exception");
					Debug.LogWarning("[GameplayTweaks] Command queue profiler suppressed cleared tuple exception: " + ex.GetType().Name + ":" + ex.Message);
					return false;
				}
				Debug.LogWarning("[GameplayTweaks] Command queue profiler fell back to vanilla: " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static bool ShouldDeferCommandQueueForBudget(long limiterStartTicks, int completedCommands)
		{
			return completedCommands > 0 && GetElapsedMilliseconds(limiterStartTicks) >= AiCommandQueueBudgetMs;
		}

		private static bool ShouldAllowImmediateAiFollowupAfterCompletedCommand(Command completedCommand, CommandExecutor.CommandTuple tuple)
		{
			if (completedCommand == null || tuple?.active != null)
			{
				return false;
			}

			Command next = tuple.NextInQueue;
			if (next == null)
			{
				return false;
			}

			return IsCommandType(completedCommand, "CommandGoto", CommandType.GoTo)
				&& (IsCommandType(next, "CommandAttack", CommandType.Attack)
					|| IsCommandType(next, "AICommandAtOutpost", CommandType.AtOutpost)
					|| IsCommandType(next, "AICommandAttackBuilding", CommandType.AttackBuilding));
		}

		private static bool IsCommandType(Command command, string typeName, CommandType commandType)
		{
			if (command == null)
			{
				return false;
			}

			if (command.type == commandType)
			{
				return true;
			}

			return string.Equals(command.GetType().Name, typeName, StringComparison.Ordinal);
		}

		private static T TimeCommandQueueStep<T>(CommandExecutor executor, CommandExecutor.CommandTuple tuple, CommandQueueDetailState state, string stepName, Command command, Func<T> action)
		{
			long startTicks = Stopwatch.GetTimestamp();
			try
			{
				return action();
			}
			finally
			{
				RecordCommandQueueStep(executor, tuple, state, stepName, command, startTicks);
			}
		}

		private static void TimeCommandQueueStep(CommandExecutor executor, CommandExecutor.CommandTuple tuple, CommandQueueDetailState state, string stepName, Command command, Action action)
		{
			long startTicks = Stopwatch.GetTimestamp();
			try
			{
				action();
			}
			finally
			{
				RecordCommandQueueStep(executor, tuple, state, stepName, command, startTicks);
			}
		}

		private static void RecordCommandQueueStep(CommandExecutor executor, CommandExecutor.CommandTuple tuple, CommandQueueDetailState state, string stepName, Command command, long startTicks)
		{
			long elapsedMs = GetElapsedMilliseconds(startTicks);
			if (state != null)
			{
				state.StepCount++;
				state.StepTotalMs += elapsedMs;
				if (elapsedMs > state.MaxStepMs)
				{
					state.MaxStepMs = elapsedMs;
					state.MaxStepName = stepName;
					state.MaxStepCommand = FormatCommandForLog(command);
				}
			}

			if (elapsedMs < GetCommandQueueStepLogThresholdMs())
			{
				return;
			}

			Debug.Log(
				"[PERF][CommandQueueStep] ms=" + elapsedMs +
				" step=" + stepName +
				" pid=" + TryGetPlayerSubmanagerPid(executor) +
				" currentPid=" + GetPlayerForLog() +
				" peep=" + (tuple?.peepId.id ?? 0UL) +
				" command=" + FormatCommandForLog(command) +
				" active=" + FormatCommandForLog(tuple?.active) +
				" next=" + FormatCommandForLog(tuple?.NextInQueue) +
				" queue=" + (tuple?.queue?.Count ?? -1) +
				" day=" + GetDayForLog() +
				" year=" + GetYearForLog() +
				" turn=" + GetTurnForLog() +
				" totalPlayers=" + GetTotalPlayersForLog());
		}

		private static void MarkCommandQueueLimiterState(CommandQueueDetailState state, int completedCommands, long limiterStartTicks, bool budgetDeferred, string outcome)
		{
			if (state == null)
			{
				return;
			}

			state.LimitedByGameplayTweaks = true;
			state.CompletedCommands = completedCommands;
			state.LimiterElapsedMs = GetElapsedMilliseconds(limiterStartTicks);
			state.BudgetDeferred = budgetDeferred;
			state.LastLimiterOutcome = outcome;
		}

		private static void CommandExecutorDetailPrefix(CommandExecutor __instance, out CommandExecutorDetailState __state)
		{
			__state = new CommandExecutorDetailState
			{
				Executor = __instance,
				StartTicks = Stopwatch.GetTimestamp(),
				PlayerId = TryGetPlayerSubmanagerPid(__instance)
			};

			CountCommandTuples(__instance, out __state.TupleCountBefore, out __state.MarkedBefore, out __state.ActiveBefore, out __state.QueuedBefore);
			_activeCommandExecutorDetail = __state;
		}

		private static void CommandExecutorDetailPostfix(CommandExecutor __instance, CommandExecutorDetailState __state)
		{
			try
			{
				if (_activeCommandExecutorDetail == __state)
				{
					_activeCommandExecutorDetail = null;
				}

				if (__state == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				if (elapsedMs < GetCommandExecutorDetailLogThresholdMs())
				{
					return;
				}

				int tupleCountAfter;
				int markedAfter;
				int activeAfter;
				int queuedAfter;
				CountCommandTuples(__instance, out tupleCountAfter, out markedAfter, out activeAfter, out queuedAfter);
				Debug.Log(
					"[PERF][CommandExecutorDetail] ms=" + elapsedMs +
					" pid=" + __state.PlayerId +
					" currentPid=" + GetPlayerForLog() +
					" tuplesBefore=" + __state.TupleCountBefore +
					" markedBefore=" + __state.MarkedBefore +
					" activeBefore=" + __state.ActiveBefore +
					" queuedBefore=" + __state.QueuedBefore +
					" processedTuples=" + __state.ProcessedTuples +
					" totalTupleMs=" + __state.TotalTupleMs +
					" maxTupleMs=" + __state.MaxTupleMs +
					" maxTuplePeep=" + __state.MaxTuplePeepId +
					" maxTupleActiveBefore=" + (__state.MaxTupleActiveBefore ?? "none") +
					" maxTupleNextBefore=" + (__state.MaxTupleNextBefore ?? "none") +
					" maxTupleQueueBefore=" + __state.MaxTupleQueueBefore +
					" tuplesAfter=" + tupleCountAfter +
					" markedAfter=" + markedAfter +
					" activeAfter=" + activeAfter +
					" queuedAfter=" + queuedAfter +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
				if (_activeCommandExecutorDetail == __state)
				{
					_activeCommandExecutorDetail = null;
				}
			}
		}

		private static void CommandQueueDetailPrefix(CommandExecutor __instance, CommandExecutor.CommandTuple tuple, out CommandQueueDetailState __state)
		{
			__state = new CommandQueueDetailState
			{
				StartTicks = Stopwatch.GetTimestamp(),
				PlayerId = TryGetPlayerSubmanagerPid(__instance),
				PeepId = tuple?.peepId.id ?? 0UL,
				ActiveBefore = FormatCommandForLog(tuple?.active),
				NextBefore = FormatCommandForLog(tuple?.NextInQueue),
				QueueCountBefore = tuple?.queue?.Count ?? -1,
				MarkedBefore = tuple?.markedForRemoval ?? false
			};
			_activeCommandQueueDetail = __state;
		}

		private static void CommandQueueDetailPostfix(CommandExecutor __instance, CommandExecutor.CommandTuple tuple, CommandQueueDetailState __state)
		{
			try
			{
				if (_activeCommandQueueDetail == __state)
				{
					_activeCommandQueueDetail = null;
				}

				if (__state == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				CommandExecutorDetailState aggregate = _activeCommandExecutorDetail;
				if (aggregate != null && ReferenceEquals(aggregate.Executor, __instance))
				{
					aggregate.ProcessedTuples++;
					aggregate.TotalTupleMs += elapsedMs;
					if (elapsedMs > aggregate.MaxTupleMs)
					{
						aggregate.MaxTupleMs = elapsedMs;
						aggregate.MaxTuplePeepId = __state.PeepId;
						aggregate.MaxTupleActiveBefore = __state.ActiveBefore;
						aggregate.MaxTupleNextBefore = __state.NextBefore;
						aggregate.MaxTupleQueueBefore = __state.QueueCountBefore;
					}
				}

				ScriptDispatcherDetailState script = _activeScriptDispatcherDetail;
				if (script != null)
				{
					script.QueueProcessCalls++;
					script.QueueProcessMs += elapsedMs;
					if (elapsedMs > script.MaxQueueProcessMs)
					{
						script.MaxQueueProcessMs = elapsedMs;
						script.MaxQueueProcessCommand = __state.NextBefore ?? __state.ActiveBefore ?? "none";
					}
				}

				if (__state.PlayerId == PlayerID.HumanPlayer.id
					&& (__state.CompletedCommands > 0 || elapsedMs >= CommandQueueStepDetailThresholdMs || __state.MaxStepMs >= CommandQueueStepDetailThresholdMs))
				{
					_lastInteractiveCommandQueueFrame = Time.frameCount;
					if (elapsedMs >= 16L || __state.MaxStepMs >= 12L)
					{
						GameplayTweaksPlugin.HoldUiMaintenanceDuringTurnProcessing("human-command-queue", 12, false);
					}
				}

				if (elapsedMs < GetCommandQueueLogThresholdMs()
					&& __state.MaxStepMs < GetCommandQueueStepLogThresholdMs())
				{
					return;
				}

				Debug.Log(
					"[PERF][CommandQueue] ms=" + elapsedMs +
					" pid=" + __state.PlayerId +
					" currentPid=" + GetPlayerForLog() +
					" peep=" + __state.PeepId +
					" activeBefore=" + __state.ActiveBefore +
					" nextBefore=" + __state.NextBefore +
					" queueBefore=" + __state.QueueCountBefore +
					" markedBefore=" + __state.MarkedBefore +
					" activeAfter=" + FormatCommandForLog(tuple?.active) +
					" nextAfter=" + FormatCommandForLog(tuple?.NextInQueue) +
					" queueAfter=" + (tuple?.queue?.Count ?? -1) +
					" markedAfter=" + (tuple?.markedForRemoval ?? false) +
					" limited=" + __state.LimitedByGameplayTweaks +
					" budgetDeferred=" + __state.BudgetDeferred +
					" limiterOutcome=" + (__state.LastLimiterOutcome ?? "none") +
					" completedCommands=" + __state.CompletedCommands +
					" limiterMs=" + __state.LimiterElapsedMs +
					" stepCount=" + __state.StepCount +
					" stepTotalMs=" + __state.StepTotalMs +
					" maxStepMs=" + __state.MaxStepMs +
					" maxStep=" + (__state.MaxStepName ?? "none") +
					" maxStepCommand=" + (__state.MaxStepCommand ?? "none") +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
				if (_activeCommandQueueDetail == __state)
				{
					_activeCommandQueueDetail = null;
				}
			}
		}

		private static void PlayerAIAssignRequestsDetailPrefix(PlayerAI __instance, out PlayerAIAssignRequestsDetailState __state)
		{
			__state = new PlayerAIAssignRequestsDetailState
			{
				StartTicks = Stopwatch.GetTimestamp(),
				PlayerId = TryGetPlayerSubmanagerPid(__instance)
			};
			_activePlayerAIAssignRequestsDetail = __state;
		}

		private static void PlayerAIAssignRequestsDetailPostfix(PlayerAIAssignRequestsDetailState __state)
		{
			try
			{
				if (_activePlayerAIAssignRequestsDetail == __state)
				{
					_activePlayerAIAssignRequestsDetail = null;
				}

				if (__state == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				long aggregateThresholdMs = GetAiAggregateLogThresholdMs();
				long detailThresholdMs = GetAiDetailLogThresholdMs();
				if (elapsedMs < aggregateThresholdMs
					&& __state.MaxDispatchMs < detailThresholdMs
					&& __state.MaxCompletionMs < detailThresholdMs
					&& __state.MaxLogRequestMs < detailThresholdMs)
				{
					return;
				}

				Debug.Log(
					"[PERF][PlayerAIAssignRequests] ms=" + elapsedMs +
					" pid=" + __state.PlayerId +
					" currentPid=" + GetPlayerForLog() +
					" attempts=" + __state.DispatchAttempts +
					" assignedAttempts=" + __state.AssignedAttempts +
					" availableAttempts=" + __state.AvailableAttempts +
					" dispatched=" + __state.Dispatched +
					" slowDispatches=" + __state.SlowDispatches +
					" totalDispatchMs=" + __state.TotalDispatchMs +
					" maxDispatchMs=" + __state.MaxDispatchMs +
					" maxDispatchPath=" + (__state.MaxDispatchPath ?? "none") +
					" maxDispatchAdvisor=" + (__state.MaxDispatchAdvisor ?? "none") +
					" maxDispatchScript=" + (__state.MaxDispatchScript ?? "none") +
					" maxDispatchPriority=" + (__state.MaxDispatchPriority ?? "none") +
					" maxDispatchAssignedTo=" + (__state.MaxDispatchAssignedTo ?? "none") +
					" completionCalls=" + __state.CompletionCalls +
					" slowCompletions=" + __state.SlowCompletions +
					" totalCompletionMs=" + __state.TotalCompletionMs +
					" maxCompletionMs=" + __state.MaxCompletionMs +
					" maxCompletionMethod=" + (__state.MaxCompletionMethod ?? "none") +
					" maxCompletionAdvisor=" + (__state.MaxCompletionAdvisor ?? "none") +
					" maxCompletionScript=" + (__state.MaxCompletionScript ?? "none") +
					" maxCompletionPriority=" + (__state.MaxCompletionPriority ?? "none") +
					" maxCompletionAssignedTo=" + (__state.MaxCompletionAssignedTo ?? "none") +
					" logRequestCalls=" + __state.LogRequestCalls +
					" slowLogRequests=" + __state.SlowLogRequests +
					" totalLogRequestMs=" + __state.TotalLogRequestMs +
					" maxLogRequestMs=" + __state.MaxLogRequestMs +
					" maxLogRequestAdvisor=" + (__state.MaxLogRequestAdvisor ?? "none") +
					" maxLogRequestScript=" + (__state.MaxLogRequestScript ?? "none") +
					" maxLogRequestPriority=" + (__state.MaxLogRequestPriority ?? "none") +
					" maxLogRequestAssignedTo=" + (__state.MaxLogRequestAssignedTo ?? "none") +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
				if (_activePlayerAIAssignRequestsDetail == __state)
				{
					_activePlayerAIAssignRequestsDetail = null;
				}
			}
		}

		private static void PlayerAIDispatchDetailPrefix(AdvisorRequest request, out PlayerAIDispatchDetailState __state)
		{
			__state = new PlayerAIDispatchDetailState
			{
				StartTicks = Stopwatch.GetTimestamp(),
				Advisor = FormatAdvisorForLog(request),
				Script = request != null ? FormatLabelForLog(request.script) : "none",
				Priority = request?.priority.ToString() ?? "none",
				AssignedTo = request != null ? FormatEntityForLog(request.assignedTo) : "none"
			};
			_activePlayerAIDispatchDetail = __state;
		}

		private static void PlayerAIDispatchDetailPostfix(MethodBase __originalMethod, AdvisorRequest request, bool __result, PlayerAIDispatchDetailState __state)
		{
			try
			{
				if (_activePlayerAIDispatchDetail == __state)
				{
					_activePlayerAIDispatchDetail = null;
				}
				if (__state == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				string path = __originalMethod?.Name ?? "unknown";
				PlayerAIAssignRequestsDetailState aggregate = _activePlayerAIAssignRequestsDetail;
				if (aggregate != null)
				{
					aggregate.DispatchAttempts++;
					if (string.Equals(path, "TryDispatchToAssigned", StringComparison.Ordinal))
					{
						aggregate.AssignedAttempts++;
					}
					else if (string.Equals(path, "TryDispatchToAvailable", StringComparison.Ordinal))
					{
						aggregate.AvailableAttempts++;
					}

					if (__result)
					{
						aggregate.Dispatched++;
					}

					aggregate.TotalDispatchMs += elapsedMs;
					if (elapsedMs >= PlayerAIAssignRequestsDetailThresholdMs)
					{
						aggregate.SlowDispatches++;
					}

					if (elapsedMs > aggregate.MaxDispatchMs)
					{
						aggregate.MaxDispatchMs = elapsedMs;
						aggregate.MaxDispatchPath = path;
						aggregate.MaxDispatchAdvisor = __state.Advisor;
						aggregate.MaxDispatchScript = __state.Script;
						aggregate.MaxDispatchPriority = __state.Priority;
						aggregate.MaxDispatchAssignedTo = __state.AssignedTo;
					}
				}

				if (elapsedMs < GetAiDetailLogThresholdMs())
				{
					return;
				}

				Debug.Log(
					"[PERF][PlayerAIDispatch] method=" + path +
					" ms=" + elapsedMs +
					" pid=" + (request != null ? request.pid.id : -1) +
					" currentPid=" + GetPlayerForLog() +
					" result=" + __result +
					" advisor=" + __state.Advisor +
					" script=" + __state.Script +
					" priority=" + __state.Priority +
					" assignedTo=" + __state.AssignedTo +
					" scriptDispatchCalls=" + __state.ScriptDispatchCalls +
					" scriptDispatchMs=" + __state.ScriptDispatchMs +
					" dispatchRemainderMs=" + Math.Max(0L, elapsedMs - __state.ScriptDispatchMs) +
					" maxScriptDispatchMs=" + __state.MaxScriptDispatchMs +
					" maxScriptDispatchScript=" + (__state.MaxScriptDispatchScript ?? "none") +
					" maxScriptDispatchPeep=" + (__state.MaxScriptDispatchPeep ?? "none") +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
				if (_activePlayerAIDispatchDetail == __state)
				{
					_activePlayerAIDispatchDetail = null;
				}
			}
		}

		private static void PlayerAIRequestCompletionDetailPrefix(AdvisorRequest req, out PlayerAIRequestStepTimingState __state)
		{
			__state = _activePlayerAIAssignRequestsDetail != null
				? new PlayerAIRequestStepTimingState(Stopwatch.GetTimestamp(), req)
				: default;
		}

		private static void PlayerAIRequestCompletionDetailPostfix(MethodBase __originalMethod, AdvisorRequest req, bool dispatched, PlayerAIRequestStepTimingState __state)
		{
			try
			{
				if (__state.StartTicks == 0L)
				{
					return;
				}

				PlayerAIAssignRequestsDetailState aggregate = _activePlayerAIAssignRequestsDetail;
				if (aggregate == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				string method = FormatMethodForLog(__originalMethod);
				aggregate.CompletionCalls++;
				aggregate.TotalCompletionMs += elapsedMs;
				if (elapsedMs >= PlayerAIAssignRequestsDetailThresholdMs)
				{
					aggregate.SlowCompletions++;
				}

				if (elapsedMs > aggregate.MaxCompletionMs)
				{
					aggregate.MaxCompletionMs = elapsedMs;
					aggregate.MaxCompletionMethod = method;
					aggregate.MaxCompletionAdvisor = __state.Advisor;
					aggregate.MaxCompletionScript = __state.Script;
					aggregate.MaxCompletionPriority = __state.Priority;
					aggregate.MaxCompletionAssignedTo = __state.AssignedTo;
				}

				if (elapsedMs < GetAiDetailLogThresholdMs())
				{
					return;
				}

				Debug.Log(
					"[PERF][PlayerAIRequestCompletion] method=" + method +
					" ms=" + elapsedMs +
					" pid=" + (req != null ? req.pid.id : -1) +
					" currentPid=" + GetPlayerForLog() +
					" dispatched=" + dispatched +
					" advisor=" + __state.Advisor +
					" script=" + __state.Script +
					" priority=" + __state.Priority +
					" assignedTo=" + __state.AssignedTo +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
			}
		}

		private static void PlayerAILogRequestDetailPrefix(AdvisorRequest req, out PlayerAIRequestStepTimingState __state)
		{
			__state = _activePlayerAIAssignRequestsDetail != null
				? new PlayerAIRequestStepTimingState(Stopwatch.GetTimestamp(), req)
				: default;
		}

		private static void PlayerAILogRequestDetailPostfix(AdvisorRequest req, PlayerAIRequestStepTimingState __state)
		{
			try
			{
				if (__state.StartTicks == 0L)
				{
					return;
				}

				PlayerAIAssignRequestsDetailState aggregate = _activePlayerAIAssignRequestsDetail;
				if (aggregate == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				aggregate.LogRequestCalls++;
				aggregate.TotalLogRequestMs += elapsedMs;
				if (elapsedMs >= PlayerAIAssignRequestsDetailThresholdMs)
				{
					aggregate.SlowLogRequests++;
				}

				if (elapsedMs > aggregate.MaxLogRequestMs)
				{
					aggregate.MaxLogRequestMs = elapsedMs;
					aggregate.MaxLogRequestAdvisor = __state.Advisor;
					aggregate.MaxLogRequestScript = __state.Script;
					aggregate.MaxLogRequestPriority = __state.Priority;
					aggregate.MaxLogRequestAssignedTo = __state.AssignedTo;
				}

				if (elapsedMs < GetAiDetailLogThresholdMs())
				{
					return;
				}

				Debug.Log(
					"[PERF][PlayerAILogRequest] ms=" + elapsedMs +
					" pid=" + (req != null ? req.pid.id : -1) +
					" currentPid=" + GetPlayerForLog() +
					" advisor=" + __state.Advisor +
					" script=" + __state.Script +
					" priority=" + __state.Priority +
					" assignedTo=" + __state.AssignedTo +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
			}
		}

		private static void ScriptDispatcherDetailPrefix(Label scriptLabel, Entity peep, out ScriptDispatcherDetailState __state)
		{
			__state = new ScriptDispatcherDetailState
			{
				StartTicks = Stopwatch.GetTimestamp(),
				Script = FormatLabelForLog(scriptLabel),
				Peep = peep != null ? FormatEntityForLog(peep.Id) : "none"
			};
			_activeScriptDispatcherDetail = __state;
		}

		private static void ScriptDispatcherDetailPostfix(ScriptDispatcherDetailState __state)
		{
			try
			{
				if (_activeScriptDispatcherDetail == __state)
				{
					_activeScriptDispatcherDetail = null;
				}

				if (__state == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				PlayerAIDispatchDetailState dispatch = _activePlayerAIDispatchDetail;
				if (dispatch != null)
				{
					dispatch.ScriptDispatchCalls++;
					dispatch.ScriptDispatchMs += elapsedMs;
					if (elapsedMs > dispatch.MaxScriptDispatchMs)
					{
						dispatch.MaxScriptDispatchMs = elapsedMs;
						dispatch.MaxScriptDispatchScript = __state.Script;
						dispatch.MaxScriptDispatchPeep = __state.Peep;
					}
				}

				if (elapsedMs < GetAiDetailLogThresholdMs())
				{
					return;
				}

				Debug.Log(
					"[PERF][ScriptDispatcher] ms=" + elapsedMs +
					" script=" + __state.Script +
					" peep=" + __state.Peep +
					" currentPid=" + GetPlayerForLog() +
					" validateCalls=" + __state.ValidateCalls +
					" validateMs=" + __state.ValidateMs +
					" maxValidateMs=" + __state.MaxValidateMs +
					" stepCalls=" + __state.StepToCommandCalls +
					" stepMs=" + __state.StepToCommandMs +
					" maxStepMs=" + __state.MaxStepToCommandMs +
					" maxStepType=" + (__state.MaxStepToCommandType ?? "none") +
					" addCommandCalls=" + __state.AddCommandCalls +
					" addCommandMs=" + __state.AddCommandMs +
					" maxAddCommandMs=" + __state.MaxAddCommandMs +
					" maxAddCommand=" + (__state.MaxAddCommandType ?? "none") +
					" queueProcessCalls=" + __state.QueueProcessCalls +
					" queueProcessMs=" + __state.QueueProcessMs +
					" maxQueueProcessMs=" + __state.MaxQueueProcessMs +
					" maxQueueProcessCommand=" + (__state.MaxQueueProcessCommand ?? "none") +
					" scriptRemainderMs=" + Math.Max(0L, elapsedMs - __state.ValidateMs - __state.StepToCommandMs - __state.AddCommandMs) +
					" addCommandRemainderMs=" + Math.Max(0L, __state.AddCommandMs - __state.QueueProcessMs) +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog());
			}
			catch
			{
				if (_activeScriptDispatcherDetail == __state)
				{
					_activeScriptDispatcherDetail = null;
				}
			}
		}

		private static void ScriptValidateDetailPrefix(out long __state)
		{
			__state = _activeScriptDispatcherDetail != null ? Stopwatch.GetTimestamp() : 0L;
		}

		private static void ScriptValidateDetailPostfix(long __state)
		{
			try
			{
				if (__state == 0L)
				{
					return;
				}

				ScriptDispatcherDetailState script = _activeScriptDispatcherDetail;
				if (script == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state);
				script.ValidateCalls++;
				script.ValidateMs += elapsedMs;
				if (elapsedMs > script.MaxValidateMs)
				{
					script.MaxValidateMs = elapsedMs;
				}
			}
			catch
			{
			}
		}

		private static void ScriptStepToCommandDetailPrefix(ScriptStep step, out ScriptStepTimingState __state)
		{
			__state = _activeScriptDispatcherDetail != null
				? new ScriptStepTimingState(Stopwatch.GetTimestamp(), step.type.ToString())
				: default;
		}

		private static void ScriptStepToCommandDetailPostfix(ScriptStepTimingState __state)
		{
			try
			{
				if (__state.StartTicks == 0L)
				{
					return;
				}

				ScriptDispatcherDetailState script = _activeScriptDispatcherDetail;
				if (script == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				script.StepToCommandCalls++;
				script.StepToCommandMs += elapsedMs;
				if (elapsedMs > script.MaxStepToCommandMs)
				{
					script.MaxStepToCommandMs = elapsedMs;
					script.MaxStepToCommandType = __state.StepType;
				}
			}
			catch
			{
			}
		}

		private static void ScriptAddCommandDetailPrefix(Command cmd, out ScriptCommandTimingState __state)
		{
			__state = _activeScriptDispatcherDetail != null
				? new ScriptCommandTimingState(Stopwatch.GetTimestamp(), FormatCommandForLog(cmd))
				: default;
		}

		private static void ScriptAddCommandDetailPostfix(ScriptCommandTimingState __state)
		{
			try
			{
				if (__state.StartTicks == 0L)
				{
					return;
				}

				ScriptDispatcherDetailState script = _activeScriptDispatcherDetail;
				if (script == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				script.AddCommandCalls++;
				script.AddCommandMs += elapsedMs;
				if (elapsedMs > script.MaxAddCommandMs)
				{
					script.MaxAddCommandMs = elapsedMs;
					script.MaxAddCommandType = __state.Command;
				}
			}
			catch
			{
			}
		}

		private static void ManufactureModuleDetailPrefix(ManufactureModule __instance, ModuleQuery q, out ManufactureModuleDetailState __state)
		{
			int originalLastUpdateDay = __instance?.data?.lastUpdate.days ?? -1;
			int catchupClampDays = ClampNonHumanManufactureCatchup(__instance, q);
			__state = new ManufactureModuleDetailState
			{
				StartTicks = Stopwatch.GetTimestamp(),
				ModuleId = FormatLabelForLog(__instance?.config?.id ?? Label.NULL),
				RecipeIndex = __instance?.data?.recipeIndex ?? -1,
				LastUpdateDay = __instance?.data?.lastUpdate.days ?? -1,
				OriginalLastUpdateDay = originalLastUpdateDay,
				CatchupClampDays = catchupClampDays,
				OwnerIsHuman = q.OwnerIsHumanPlayer
			};

			try
			{
				if (__instance?.config?.recipes != null
					&& __state.RecipeIndex >= 0
					&& __state.RecipeIndex < __instance.config.recipes.Count)
				{
					Recipe recipe = __instance.config.recipes[__state.RecipeIndex];
					__state.ProduceCount = recipe?.produce?.Count ?? 0;
					__state.ConsumeCount = recipe?.consume?.Count ?? 0;
					__state.SelloffCount = recipe?.selloff?.Count ?? 0;
					__state.RefillCount = recipe?.refill?.Count ?? 0;
				}
			}
			catch
			{
			}
		}

		private static void ManufactureModuleDetailPostfix(ManufactureModule __instance, ModuleQuery q, SimTime time, bool initial, bool enabled, ModuleResult __result, ManufactureModuleDetailState __state)
		{
			try
			{
				if (__state == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				if (elapsedMs < ManufactureModuleDetailThresholdMs)
				{
					return;
				}

				Debug.Log(
					"[PERF][ManufactureModuleUpdate] ms=" + elapsedMs +
					" module=" + (__state.ModuleId ?? "none") +
					" result=" + __result +
					" initial=" + initial +
					" enabled=" + enabled +
					" ownerHuman=" + __state.OwnerIsHuman +
					" recipeIndex=" + __state.RecipeIndex +
					" produce=" + __state.ProduceCount +
					" consume=" + __state.ConsumeCount +
					" selloff=" + __state.SelloffCount +
					" refill=" + __state.RefillCount +
					" lastUpdateDayBefore=" + __state.LastUpdateDay +
					" originalLastUpdateDayBefore=" + __state.OriginalLastUpdateDay +
					" catchupClampDays=" + __state.CatchupClampDays +
					" lastUpdateDayAfter=" + (__instance?.data?.lastUpdate.days ?? -1) +
					" currentDay=" + time.days +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" pid=" + GetPlayerForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
			}
		}

		private static int ClampNonHumanManufactureCatchup(ManufactureModule module, ModuleQuery q)
		{
			try
			{
				if (module?.data == null
					|| module.config?.recipes == null
					|| q.OwnerIsHumanPlayer
					|| q.container == null
					|| !(global::Game.Game.ctx?.IsInteractive ?? false))
				{
					return 0;
				}

				int recipeIndex = module.data.recipeIndex;
				if (recipeIndex < 0 || recipeIndex >= module.config.recipes.Count)
				{
					return 0;
				}

				SimTime now = global::Game.Game.ctx.clock.Now;
				int originalLastUpdateDay = module.data.lastUpdate.days;
				if (originalLastUpdateDay <= 0 || originalLastUpdateDay >= now.days)
				{
					return 0;
				}

				Recipe recipe = module.config.recipes[recipeIndex];
				int periodDays = recipe?.ModProduceConsumeDays(q, module) ?? 0;
				if (periodDays <= 0)
				{
					return 0;
				}

				int maxCatchupDays = Math.Max(1, periodDays * NonHumanManufactureMaxCatchupIntervals);
				int earliestLastUpdateDay = now.days - maxCatchupDays;
				if (originalLastUpdateDay >= earliestLastUpdateDay)
				{
					return 0;
				}

				module.data.lastUpdate = now.IncrementDays(-maxCatchupDays);
				string moduleId = FormatLabelForLog(module.config.id);
				string key = moduleId + "|" + q.container.Id.id;
				if (LoggedAiManufactureCatchupClamps.Add(key))
				{
					RecordAiManufactureCatchupClamp(moduleId, q.container.Id, originalLastUpdateDay, module.data.lastUpdate.days, now.days, periodDays);
				}

				return maxCatchupDays;
			}
			catch
			{
				return 0;
			}
		}

		private static void RecordAiManufactureCatchupClamp(string moduleId, EntityID buildingId, int originalLastUpdateDay, int adjustedLastUpdateDay, int currentDay, int periodDays)
		{
			_uniqueAiManufactureCatchupClamps++;
			if (_uniqueAiManufactureCatchupClamps != 1 && _uniqueAiManufactureCatchupClamps % 500 != 0)
			{
				return;
			}

			Debug.Log("[PERF][ManufactureCatchupClampSummary] unique=" + _uniqueAiManufactureCatchupClamps
				+ " lastModule=" + moduleId
				+ " lastBuilding=" + buildingId
				+ " originalLastUpdateDay=" + originalLastUpdateDay
				+ " adjustedLastUpdateDay=" + adjustedLastUpdateDay
				+ " currentDay=" + currentDay
				+ " periodDays=" + periodDays
				+ " maxIntervals=" + NonHumanManufactureMaxCatchupIntervals
				+ " day=" + GetDayForLog()
				+ " turn=" + GetTurnForLog());
		}

		private static bool ManufactureDoSellOffInventoryPrefix(ManufactureModule __instance, ModuleQuery q, InventoryModule inventory)
		{
			try
			{
				if (!TryGetInteractiveNonHumanManufactureRecipe(__instance, q, inventory, out Recipe recipe))
				{
					return true;
				}

				ApplyOptimizedAiSelloff(__instance, q, inventory, recipe);
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool ManufactureDoRefillInventoryPrefix(ManufactureModule __instance, ModuleQuery q, InventoryModule inventory)
		{
			try
			{
				if (!TryGetInteractiveNonHumanManufactureRecipe(__instance, q, inventory, out Recipe recipe))
				{
					return true;
				}

				ApplyOptimizedAiRefill(__instance, q, inventory, recipe);
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool TryGetInteractiveNonHumanManufactureRecipe(ManufactureModule module, ModuleQuery q, InventoryModule inventory, out Recipe recipe)
		{
			recipe = null;
			if (module?.data == null
				|| module.config?.recipes == null
				|| inventory?.data?.contents == null
				|| q.OwnerIsHumanPlayer
				|| !(global::Game.Game.ctx?.IsInteractive ?? false))
			{
				return false;
			}

			int recipeIndex = module.data.recipeIndex;
			if (recipeIndex < 0 || recipeIndex >= module.config.recipes.Count)
			{
				return false;
			}

			recipe = module.config.recipes[recipeIndex];
			return recipe != null;
		}

		private static void ApplyOptimizedAiSelloff(ManufactureModule module, ModuleQuery q, InventoryModule inventory, Recipe recipe)
		{
			if (recipe?.selloff == null || recipe.selloff.Count == 0)
			{
				return;
			}

			IndexedInventoryMutator mutator = new IndexedInventoryMutator(inventory.data);
			foreach (SellOffElement item in recipe.selloff)
			{
				if (item.sell > 0)
				{
					Debug.LogWarning($"Refill recipe should not be negative in {item.id}, {item.sell}");
				}

				if (mutator.GetQty(item.id) <= item.above)
				{
					continue;
				}

				Fixnum toBuilding = QtyAndDir.FromSignedQuantity(item.sell).ToBuilding;
				if (toBuilding.IsNotZero)
				{
					mutator.Increment(item.id, toBuilding);
				}
			}

			LogAiManufactureInventoryTransferOptimization(module, q, recipe);
		}

		private static void ApplyOptimizedAiRefill(ManufactureModule module, ModuleQuery q, InventoryModule inventory, Recipe recipe)
		{
			if (recipe?.refill == null || recipe.refill.Count == 0)
			{
				return;
			}

			IndexedInventoryMutator mutator = new IndexedInventoryMutator(inventory.data);
			foreach (RefillElement item in recipe.refill)
			{
				if (item.get < 0)
				{
					Debug.LogWarning($"Refill recipe should not be negative in {item.id}, {item.get}");
				}

				if (mutator.GetQty(item.id) >= item.below)
				{
					continue;
				}

				Fixnum toBuilding = QtyAndDir.FromSignedQuantity(item.get).ToBuilding;
				if (toBuilding.IsNotZero)
				{
					mutator.Increment(item.id, toBuilding);
				}
			}

			LogAiManufactureInventoryTransferOptimization(module, q, recipe);
		}

		private static void LogAiManufactureInventoryTransferOptimization(ManufactureModule module, ModuleQuery q, Recipe recipe)
		{
			try
			{
				int selloffCount = recipe?.selloff?.Count ?? 0;
				int refillCount = recipe?.refill?.Count ?? 0;
				if (selloffCount + refillCount < 12 || q.container == null)
				{
					return;
				}

				string moduleId = FormatLabelForLog(module?.config?.id ?? Label.NULL);
				string key = moduleId + "|" + q.container.Id.id;
				if (LoggedAiManufactureInventoryTransferOptimizations.Add(key))
				{
					RecordAiManufactureInventoryTransferOptimization(moduleId, q.container.Id, selloffCount, refillCount);
				}
			}
			catch
			{
			}
		}

		private static void RecordAiManufactureInventoryTransferOptimization(string moduleId, EntityID buildingId, int selloffCount, int refillCount)
		{
			_uniqueAiManufactureInventoryTransferOptimizations++;
			if (_uniqueAiManufactureInventoryTransferOptimizations != 1 && _uniqueAiManufactureInventoryTransferOptimizations % 500 != 0)
			{
				return;
			}

			Debug.Log("[PERF][ManufactureInventoryTransferOptimizedSummary] unique=" + _uniqueAiManufactureInventoryTransferOptimizations
				+ " lastModule=" + moduleId
				+ " lastBuilding=" + buildingId
				+ " selloff=" + selloffCount
				+ " refill=" + refillCount
				+ " day=" + GetDayForLog()
				+ " turn=" + GetTurnForLog());
		}

		private static void CountCommandTuples(CommandExecutor executor, out int tupleCount, out int markedCount, out int activeCount, out int queuedCount)
		{
			tupleCount = 0;
			markedCount = 0;
			activeCount = 0;
			queuedCount = 0;
			try
			{
				List<CommandExecutor.CommandTuple> tuples = executor?.tuples;
				if (tuples == null)
				{
					return;
				}

				tupleCount = tuples.Count;
				for (int i = 0; i < tuples.Count; i++)
				{
					CommandExecutor.CommandTuple tuple = tuples[i];
					if (tuple == null)
					{
						continue;
					}

					if (tuple.markedForRemoval)
					{
						markedCount++;
					}

					if (tuple.active != null)
					{
						activeCount++;
					}

					if (tuple.queue != null)
					{
						queuedCount += tuple.queue.Count;
					}
				}
			}
			catch
			{
			}
		}

		private static int TryGetPlayerSubmanagerPid(object submanager)
		{
			try
			{
				if (submanager != null && PlayerSubmanagerPidField?.GetValue(submanager) is PlayerID pid)
				{
					return pid.id;
				}
			}
			catch
			{
			}

			return -1;
		}

		private static string FormatCommandForLog(Command command)
		{
			if (command == null)
			{
				return "none";
			}

			return command.GetType().Name + ":" + command.type;
		}

		private static string FormatAdvisorForLog(AdvisorRequest request)
		{
			return request?.advisor?.GetType().Name ?? "none";
		}

		private static string FormatLabelForLog(Label label)
		{
			if (!label.IsSet)
			{
				return "none";
			}

			return string.IsNullOrEmpty(label.String) ? label.Index.ToString() : label.String;
		}

		private static string FormatEntityForLog(EntityID entityId)
		{
			return entityId.IsValid ? entityId.id.ToString() : "none";
		}

		private static bool SkipAiPromotionPromptingPrefix(PlayerCrew __instance)
		{
			try
			{
				if (__instance == null || PlayerSubmanagerPidField == null)
				{
					return true;
				}

				PlayerID pid = (PlayerID)PlayerSubmanagerPidField.GetValue(__instance);
				if (pid.IsValid && !pid.IsHumanPlayer)
				{
					return false;
				}
			}
			catch
			{
			}

			return true;
		}

		private static void PlayerCrewGrowthOnPlayerTurnStartedPrefix(PlayerCrew crew, out bool __state)
		{
			__state = false;
			try
			{
				if (IsInteractiveHumanCrewGrowthCandidateContext(crew))
				{
					_playerCrewGrowthTurnStartDepth++;
					__state = true;
				}
			}
			catch
			{
			}
		}

		private static Exception PlayerCrewGrowthOnPlayerTurnStartedFinalizer(bool __state, Exception __exception)
		{
			try
			{
				if (__state && _playerCrewGrowthTurnStartDepth > 0)
				{
					_playerCrewGrowthTurnStartDepth--;
				}
			}
			catch
			{
			}

			return __exception;
		}

		private static bool PlayerCrewGrowthRecomputeHumanCrewCandidatesPrefix(PlayerCrewGrowth __instance, PlayerCrew crew, out bool __state)
		{
			__state = true;
			try
			{
				if (_forceHumanCrewCandidateRecompute || _playerCrewGrowthTurnStartDepth <= 0)
				{
					return true;
				}

				if (__instance == null || !IsInteractiveHumanCrewGrowthCandidateContext(crew))
				{
					return true;
				}

				if (!crew.CanAddCrew())
				{
					__instance.humanCrewCandidates = __instance.humanCrewCandidates ?? new List<PlayerCrewGrowth.CandidateData>();
					__instance.humanCrewCandidates.Clear();
					ClearPendingHumanCrewCandidateRecompute(__instance);
					__state = false;
					return false;
				}

				_pendingHumanCrewCandidateGrowth = __instance;
				_pendingHumanCrewCandidateCrew = crew;
				_pendingHumanCrewCandidateDay = GetDayForLog();
				_pendingHumanCrewCandidateTurn = GetTurnForLog();
				_pendingHumanCrewCandidateFrame = Time.frameCount;
				_pendingHumanCrewCandidateRecompute = true;
				_pendingHumanCrewCandidateServedStale = false;
				if (_lastDeferredHumanCrewCandidateLogDay != _pendingHumanCrewCandidateDay)
				{
					_lastDeferredHumanCrewCandidateLogDay = _pendingHumanCrewCandidateDay;
					Debug.Log("[PERF][HumanTurnPhase] phase=human-crew-candidates-deferred day=" + _pendingHumanCrewCandidateDay + " turn=" + _pendingHumanCrewCandidateTurn + " pid=" + GetPlayerForLog() + " source=PlayerCrewGrowth.OnPlayerTurnStarted mode=lazy-preserve-existing");
				}

				__state = false;
				return false;
			}
			catch
			{
				__state = true;
				return true;
			}
		}

		private static void PlayerCrewGrowthRecomputeHumanCrewCandidatesPostfix(PlayerCrewGrowth __instance, bool __state)
		{
			try
			{
				if (__state)
				{
					ClearPendingHumanCrewCandidateRecompute(__instance);
				}
			}
			catch
			{
			}
		}

		private static void PlayerCrewGrowthCandidateAccessPrefix(PlayerCrewGrowth __instance)
		{
			EnsurePendingHumanCrewCandidatesReady(__instance, "candidate-access");
		}

		private static void EnsurePendingHumanCrewCandidatesReady(PlayerCrewGrowth growth, string trigger)
		{
			if (!_pendingHumanCrewCandidateRecompute || growth == null || !ReferenceEquals(growth, _pendingHumanCrewCandidateGrowth))
			{
				return;
			}

			int ageFrames = Time.frameCount - _pendingHumanCrewCandidateFrame;
			int currentCount = growth.humanCrewCandidates?.Count ?? 0;
			if (currentCount > 0 && ageFrames < DeferredHumanCrewCandidateDelayFrames)
			{
				if (!_pendingHumanCrewCandidateServedStale)
				{
					_pendingHumanCrewCandidateServedStale = true;
					Debug.Log("[PERF][HumanTurnPhase] phase=human-crew-candidates-stale-served candidates=" + currentCount + " trigger=" + (trigger ?? "unknown") + " ageFrames=" + ageFrames + " delayFrames=" + DeferredHumanCrewCandidateDelayFrames + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return;
			}

			RunPendingHumanCrewCandidateRecompute(growth, trigger);
		}

		internal static void FlushDeferredHumanCrewCandidates(string source)
		{
			try
			{
				if (!_pendingHumanCrewCandidateRecompute || _pendingHumanCrewCandidateGrowth == null)
				{
					return;
				}

				int ageFrames = Time.frameCount - _pendingHumanCrewCandidateFrame;
				if (ageFrames < DeferredHumanCrewCandidateDelayFrames)
				{
					return;
				}

				int lastCommandFrame = _lastInteractiveCommandQueueFrame;
				if (lastCommandFrame > 0 && Time.frameCount - lastCommandFrame < DeferredSocialInferenceQuietFramesAfterCommandQueue)
				{
					return;
				}

				int existingCount = _pendingHumanCrewCandidateGrowth.humanCrewCandidates?.Count ?? 0;
				if (existingCount > 0 && string.Equals(source, "update-idle", StringComparison.OrdinalIgnoreCase))
				{
					int currentDay = GetDayForLog();
					if (_lastDeferredHumanCrewCandidateIdlePreserveLogDay != currentDay)
					{
						_lastDeferredHumanCrewCandidateIdlePreserveLogDay = currentDay;
						Debug.Log("[PERF][HumanTurnPhase] phase=human-crew-candidates-idle-preserved candidates=" + existingCount + " ageFrames=" + ageFrames + " day=" + currentDay + " turn=" + GetTurnForLog() + " source=" + (source ?? "update"));
					}
					return;
				}

				RunPendingHumanCrewCandidateRecompute(_pendingHumanCrewCandidateGrowth, source ?? "update");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Deferred human crew candidate update flush failed: " + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void RunPendingHumanCrewCandidateRecompute(PlayerCrewGrowth growth, string trigger)
		{
			if (!_pendingHumanCrewCandidateRecompute || growth == null || !ReferenceEquals(growth, _pendingHumanCrewCandidateGrowth))
			{
				return;
			}

			PlayerCrew crew = _pendingHumanCrewCandidateCrew;
			if (crew == null || PlayerCrewGrowthRecomputeHumanCrewCandidatesMethod == null)
			{
				ClearPendingHumanCrewCandidateRecompute(growth);
				return;
			}

			int deferredFrame = _pendingHumanCrewCandidateFrame;
			int deferredDay = _pendingHumanCrewCandidateDay;
			int deferredTurn = _pendingHumanCrewCandidateTurn;
			long startTicks = Stopwatch.GetTimestamp();
			try
			{
				_forceHumanCrewCandidateRecompute = true;
				PlayerCrewGrowthRecomputeHumanCrewCandidatesMethod.Invoke(growth, new object[] { crew });
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Deferred human crew candidate recompute failed: " + ex.GetType().Name + ":" + ex.Message);
			}
			finally
			{
				_forceHumanCrewCandidateRecompute = false;
				ClearPendingHumanCrewCandidateRecompute(growth);
			}

			long elapsedMs = GetElapsedMilliseconds(startTicks);
			if (elapsedMs >= HumanCrewCandidateRecomputeDetailThresholdMs)
			{
				int count = growth.humanCrewCandidates?.Count ?? 0;
				Debug.Log("[PERF][HumanTurnPhase] phase=human-crew-candidates-lazy-recompute ms=" + elapsedMs + " candidates=" + count + " trigger=" + (trigger ?? "unknown") + " ageFrames=" + (Time.frameCount - deferredFrame) + " deferredDay=" + deferredDay + " deferredTurn=" + deferredTurn + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " pid=" + GetPlayerForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
		}

		private static bool IsInteractiveHumanCrewGrowthCandidateContext(PlayerCrew crew)
		{
			try
			{
				return crew != null
					&& crew.PID.IsHumanPlayer
					&& (global::Game.Game.ctx?.IsInteractive ?? false)
					&& !(global::Game.Game.ctx?.IsCityGen ?? false);
			}
			catch
			{
				return false;
			}
		}

		private static void ClearPendingHumanCrewCandidateRecompute(PlayerCrewGrowth growth)
		{
			if (growth != null && !ReferenceEquals(growth, _pendingHumanCrewCandidateGrowth))
			{
				return;
			}

			_pendingHumanCrewCandidateGrowth = null;
			_pendingHumanCrewCandidateCrew = null;
			_pendingHumanCrewCandidateRecompute = false;
			_pendingHumanCrewCandidateServedStale = false;
			_pendingHumanCrewCandidateFrame = 0;
		}

		private static bool SetupOrchestratorOnSystemTurnPrefix()
		{
			try
			{
				SessionContext context = global::Game.Game.ctx;
				if (context?.IsInteractive != true)
				{
					return true;
				}

				string gang = context.players?.Human?.social?.PlayerGroupName ?? string.Empty;
				string date = context.clock != null ? Loc.FormatNumber(context.clock.Now.YearsInt) : string.Empty;
				string city = context.session?.mapconfig.CityName ?? string.Empty;
				string presenceKey = gang + "|" + date + "|" + city;
				string previousKey = _lastSuppressedSetupPresenceKey;
				_lastSuppressedSetupPresenceKey = presenceKey;
				_suppressedSetupPresenceUpdates++;

				if (IsPerformanceDiagnosticsEnabled()
					&& (_suppressedSetupPresenceUpdates == 1
						|| !string.Equals(previousKey, presenceKey, StringComparison.Ordinal)
						|| _suppressedSetupPresenceUpdates % 25 == 0))
				{
					Debug.Log("[PERF][SetupPresenceSkip] suppressed=" + _suppressedSetupPresenceUpdates + " changed=" + !string.Equals(previousKey, presenceKey, StringComparison.Ordinal) + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}

				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool ResidenceTrackerOnSystemTurnPrefix(ResidenceTracker __instance)
		{
			if (__instance == null || ResidenceDailyImmigrationMethod == null)
			{
				return true;
			}

			try
			{
				if (!(global::Game.Game.ctx?.IsInteractive ?? false) || __instance.data == null)
				{
					PendingResidenceImmigrationDays.Remove(__instance);
					return true;
				}

				float daysPerTurn = global::Game.Game.ctx.clock.State.daysPerTurn;
				if (daysPerTurn <= 0f)
				{
					return true;
				}

				PendingResidenceImmigrationDays.TryGetValue(__instance, out float pendingDays);
				pendingDays += daysPerTurn;
				float targetDays = daysPerTurn * ResidenceImmigrationBatchTurns;
				if (pendingDays + 0.001f < targetDays)
				{
					PendingResidenceImmigrationDays[__instance] = pendingDays;
					if (IsPerformanceDiagnosticsEnabled())
					{
						Debug.Log("[PERF][ResidenceImmigration] deferred pendingDays=" + pendingDays.ToString("0.###") + " targetDays=" + targetDays.ToString("0.###") + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
					}
					return false;
				}

				long startTicks = Stopwatch.GetTimestamp();
				int immigrants = InvokeResidenceDailyImmigration(
					__instance,
					pendingDays,
					out int targetImmigrants,
					out long cacheMs,
					out long cacheResetMs,
					out long cacheLotsMs,
					out long cacheEthMs,
					out int cacheEthCount,
					out long cacheMaxEthMs,
					out long countMs,
					out long helperMs,
					out long resetMs);
				__instance.data.population += immigrants;
				PendingResidenceImmigrationDays[__instance] = 0f;
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (ShouldLogDirectPerformanceDetail(elapsedMs)
					|| cacheMs >= CompactTurnHandlerDetailThresholdMs
					|| helperMs >= CompactTurnHandlerDetailThresholdMs
					|| resetMs >= CompactTurnHandlerDetailThresholdMs)
				{
					Debug.Log("[PERF][ResidenceImmigration] batched ms=" + elapsedMs + " pendingDays=" + pendingDays.ToString("0.###") + " targetImmigrants=" + targetImmigrants + " immigrants=" + immigrants + " cacheMs=" + cacheMs + " cacheResetMs=" + cacheResetMs + " cacheLotsMs=" + cacheLotsMs + " cacheEthMs=" + cacheEthMs + " cacheEthCount=" + cacheEthCount + " cacheMaxEthMs=" + cacheMaxEthMs + " countMs=" + countMs + " helperMs=" + helperMs + " resetMs=" + resetMs + " population=" + __instance.data.population + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch (Exception ex)
			{
				PendingResidenceImmigrationDays.Remove(__instance);
				Debug.LogWarning("[GameplayTweaks] Residence immigration batching disabled for this turn: " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static int InvokeResidenceDailyImmigration(
			ResidenceTracker tracker,
			float pendingDays,
			out int targetImmigrants,
			out long cacheMs,
			out long cacheResetMs,
			out long cacheLotsMs,
			out long cacheEthMs,
			out int cacheEthCount,
			out long cacheMaxEthMs,
			out long countMs,
			out long helperMs,
			out long resetMs)
		{
			targetImmigrants = -1;
			cacheMs = -1L;
			cacheResetMs = -1L;
			cacheLotsMs = -1L;
			cacheEthMs = -1L;
			cacheEthCount = -1;
			cacheMaxEthMs = -1L;
			countMs = -1L;
			helperMs = -1L;
			resetMs = -1L;
			if (tracker == null)
			{
				return 0;
			}

			if (EnableResidenceCacheSplitDiagnostics && TryInvokeResidenceDailyImmigrationWithSplitTimings(
				tracker,
				pendingDays,
				out int immigrants,
				out targetImmigrants,
				out cacheMs,
				out cacheResetMs,
				out cacheLotsMs,
				out cacheEthMs,
				out cacheEthCount,
				out cacheMaxEthMs,
				out countMs,
				out helperMs,
				out resetMs))
			{
				return immigrants;
			}

			ResidenceDailyImmigrationDelegate immigrationDelegate = GetResidenceDailyImmigrationDelegate();
			if (immigrationDelegate != null)
			{
				return immigrationDelegate(tracker, pendingDays);
			}

			if (!_loggedResidenceDailyImmigrationDelegateFallback)
			{
				_loggedResidenceDailyImmigrationDelegateFallback = true;
				Debug.LogWarning("[GameplayTweaks] ResidenceTracker.DailyImmigration delegate unavailable; falling back to reflection invoke in residence immigration batching.");
			}

			object result = ResidenceDailyImmigrationMethod?.Invoke(tracker, new object[] { pendingDays });
			return result is int count ? count : 0;
		}

		private static bool TryInvokeResidenceDailyImmigrationWithSplitTimings(
			ResidenceTracker tracker,
			float pendingDays,
			out int immigrants,
			out int targetImmigrants,
			out long cacheMs,
			out long cacheResetMs,
			out long cacheLotsMs,
			out long cacheEthMs,
			out int cacheEthCount,
			out long cacheMaxEthMs,
			out long countMs,
			out long helperMs,
			out long resetMs)
		{
			immigrants = 0;
			targetImmigrants = -1;
			cacheMs = -1L;
			cacheResetMs = -1L;
			cacheLotsMs = -1L;
			cacheEthMs = -1L;
			cacheEthCount = -1;
			cacheMaxEthMs = -1L;
			countMs = -1L;
			helperMs = -1L;
			resetMs = -1L;
			if (tracker == null
				|| tracker.data == null
				|| ResidenceMoveinsField == null
				|| ResidenceMoveinsCacheResidenceDataMethod == null
				|| ResidenceMoveinsCacheLotsAndHousesMethod == null
				|| ResidenceMoveinsCachePotentialResidencesForEthMethod == null
				|| ResidenceMoveinsEthnicitiesField == null
				|| ResidenceMoveinsResetCachesMethod == null
				|| ResidenceDailyImmigrationHelperMethod == null)
			{
				return false;
			}

			object moveins = ResidenceMoveinsField.GetValue(tracker);
			if (moveins == null)
			{
				return false;
			}

			bool cacheStarted = false;
			try
			{
				long phaseTicks = Stopwatch.GetTimestamp();
				ResidenceMoveinsResetCachesMethod.Invoke(moveins, null);
				cacheStarted = true;
				cacheResetMs = GetElapsedMilliseconds(phaseTicks);

				phaseTicks = Stopwatch.GetTimestamp();
				ResidenceMoveinsCacheLotsAndHousesMethod.Invoke(moveins, new object[] { tracker.data.rng });
				cacheLotsMs = GetElapsedMilliseconds(phaseTicks);

				cacheEthMs = 0L;
				cacheEthCount = 0;
				cacheMaxEthMs = 0L;
				if (!(ResidenceMoveinsEthnicitiesField.GetValue(moveins) is List<Label> ethnicities))
				{
					return false;
				}

				foreach (Label ethnicity in ethnicities)
				{
					phaseTicks = Stopwatch.GetTimestamp();
					ResidenceMoveinsCachePotentialResidencesForEthMethod.Invoke(moveins, new object[] { tracker.data.rng, ethnicity });
					long ethnicityMs = GetElapsedMilliseconds(phaseTicks);
					cacheEthMs += ethnicityMs;
					cacheMaxEthMs = Math.Max(cacheMaxEthMs, ethnicityMs);
					cacheEthCount++;
				}
				cacheMs = Math.Max(0L, cacheResetMs) + Math.Max(0L, cacheLotsMs) + Math.Max(0L, cacheEthMs);

				phaseTicks = Stopwatch.GetTimestamp();
				float years = pendingDays / 365f;
				IntRange procGenYears = global::Game.Game.serv.globals.settings.general.generator.procGenYears;
				int procGenYearSpan = Math.Max(1, procGenYears.to - procGenYears.from);
				float yearlyInflux = (float)global::Game.Game.ctx.session.mapconfig.generator.totalInflux / (float)procGenYearSpan;
				targetImmigrants = MathUtil.RoundProbabilistic(years * yearlyInflux, tracker.data.rng);
				countMs = GetElapsedMilliseconds(phaseTicks);

				phaseTicks = Stopwatch.GetTimestamp();
				object result = ResidenceDailyImmigrationHelperMethod.Invoke(tracker, new object[] { targetImmigrants });
				helperMs = GetElapsedMilliseconds(phaseTicks);
				immigrants = result is int count ? count : 0;
				return true;
			}
			finally
			{
				if (cacheStarted)
				{
					long resetTicks = Stopwatch.GetTimestamp();
					ResidenceMoveinsResetCachesMethod.Invoke(moveins, null);
					resetMs = GetElapsedMilliseconds(resetTicks);
				}
			}
		}

		private static ResidenceDailyImmigrationDelegate GetResidenceDailyImmigrationDelegate()
		{
			if (_residenceDailyImmigrationDelegateAttempted)
			{
				return _residenceDailyImmigrationDelegate;
			}

			_residenceDailyImmigrationDelegateAttempted = true;
			if (ResidenceDailyImmigrationMethod == null)
			{
				return null;
			}

			try
			{
				_residenceDailyImmigrationDelegate = (ResidenceDailyImmigrationDelegate)Delegate.CreateDelegate(
					typeof(ResidenceDailyImmigrationDelegate),
					null,
					ResidenceDailyImmigrationMethod);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResidenceTracker.DailyImmigration delegate creation failed: " + ex.GetType().Name + ":" + ex.Message);
			}

			return _residenceDailyImmigrationDelegate;
		}

		private static bool HeatmapManagerOnSystemTurnPrefix(HeatmapManager __instance)
		{
			if (__instance == null || HeatmapManagerMapsField == null || HeatmapManagerTempField == null)
			{
				return true;
			}

			try
			{
				if (!(global::Game.Game.ctx?.IsInteractive ?? false))
				{
					PendingHeatmapUpdates.Remove(__instance);
					return true;
				}

				if (!PendingHeatmapUpdates.TryGetValue(__instance, out PendingHeatmapUpdateWork work) || work == null)
				{
					work = CreateHeatmapUpdateWork(__instance);
					if (work == null || work.Entries.Count == 0)
					{
						return true;
					}

					PendingHeatmapUpdates[__instance] = work;
				}

				long sliceTicks = Stopwatch.GetTimestamp();
				int entriesUpdated = 0;
				int maxEntries = Math.Max(1, InteractiveHeatmapEntriesPerFrame);
				long budgetMs = Math.Max(1L, InteractiveHeatmapUpdateBudgetMs);
				while (work.NextIndex < work.Entries.Count && entriesUpdated < maxEntries)
				{
					HeatmapUpdateEntry entry = work.Entries[work.NextIndex++];
					CopyHeatmapData(entry.Source, entry.Target);
					entry.Target?.UpdateInteractive();
					entriesUpdated++;
					if (GetElapsedMilliseconds(sliceTicks) >= budgetMs)
					{
						break;
					}
				}

				long elapsedMs = GetElapsedMilliseconds(sliceTicks);
				work.SliceCpuMs += elapsedMs;
				work.SliceCount++;
				bool completed = work.NextIndex >= work.Entries.Count;
				if (completed)
				{
					HeatmapManagerMapsField.SetValue(__instance, work.TempMaps);
					HeatmapManagerTempField.SetValue(__instance, work.CurrentMaps);
					PendingHeatmapUpdates.Remove(__instance);
					long wallMs = GetElapsedMilliseconds(work.StartedTicks);
					if (ShouldLogDirectPerformanceDetail(work.SliceCpuMs)
						|| wallMs >= CompactTurnHandlerDetailThresholdMs)
					{
						Debug.Log("[PERF][HeatmapUpdateSlice] phase=total ms=" + work.SliceCpuMs + " wallMs=" + wallMs + " slices=" + work.SliceCount + " entries=" + work.Entries.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
					}
				}

				if (ShouldLogDirectPerformanceDetail(elapsedMs))
				{
					Debug.Log("[PERF][HeatmapUpdateSlice] ms=" + elapsedMs + " entriesUpdated=" + entriesUpdated + " nextEntry=" + work.NextIndex + " totalEntries=" + work.Entries.Count + " completed=" + completed + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch (Exception ex)
			{
				PendingHeatmapUpdates.Remove(__instance);
				Debug.LogWarning("[GameplayTweaks] Heatmap update slicing disabled for this turn: " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static PendingHeatmapUpdateWork CreateHeatmapUpdateWork(HeatmapManager manager)
		{
			List<List<HeatmapManager.HeatmapEntry>> currentMaps = HeatmapManagerMapsField.GetValue(manager) as List<List<HeatmapManager.HeatmapEntry>>;
			List<List<HeatmapManager.HeatmapEntry>> tempMaps = HeatmapManagerTempField.GetValue(manager) as List<List<HeatmapManager.HeatmapEntry>>;
			if (currentMaps == null || tempMaps == null)
			{
				return null;
			}

			List<HeatmapUpdateEntry> entries = new List<HeatmapUpdateEntry>();
			for (int i = 0; i < tempMaps.Count; i++)
			{
				List<HeatmapManager.HeatmapEntry> tempList = tempMaps[i];
				if (tempList == null)
				{
					continue;
				}

				for (int j = 0; j < tempList.Count; j++)
				{
					HeatmapManager.HeatmapEntry tempEntry = tempList[j];
					Heatmap source = FindHeatmap(currentMaps, tempEntry.type, tempEntry.tag);
					if (source != null && tempEntry.map != null)
					{
						entries.Add(new HeatmapUpdateEntry(source, tempEntry.map));
					}
				}
			}

			return new PendingHeatmapUpdateWork
			{
				CurrentMaps = currentMaps,
				TempMaps = tempMaps,
				Entries = entries,
				NextIndex = 0,
				StartedTicks = Stopwatch.GetTimestamp()
			};
		}

		private static Heatmap FindHeatmap(List<List<HeatmapManager.HeatmapEntry>> maps, HeatmapType type, Label tag)
		{
			int typeIndex = (int)type;
			if (maps == null || typeIndex < 0 || typeIndex >= maps.Count)
			{
				return null;
			}

			List<HeatmapManager.HeatmapEntry> entries = maps[typeIndex];
			if (entries == null)
			{
				return null;
			}

			for (int i = 0; i < entries.Count; i++)
			{
				HeatmapManager.HeatmapEntry entry = entries[i];
				if (entry.tag == tag)
				{
					return entry.map;
				}
			}

			return null;
		}

		private static void CopyHeatmapData(Heatmap source, Heatmap destination)
		{
			if (source?.data == null || destination?.data == null)
			{
				return;
			}

			HeatmapSize size = source.heatmapSize;
			if (destination.heatmapSize.width < size.width)
			{
				size.width = destination.heatmapSize.width;
			}

			if (destination.heatmapSize.height < size.height)
			{
				size.height = destination.heatmapSize.height;
			}

			for (int i = 1, width = size.width - 1; i < width; i++)
			{
				for (int j = 1, height = size.height - 1; j < height; j++)
				{
					destination.data[i, j] = source.data[i, j];
				}
			}
		}

		private static bool QuietDirtyCashLegalFindUpgradesPrefix(IModule module, VisitState visit, SimTime now, ref List<AddModuleDef> __result)
		{
			_ = visit;
			_ = now;
			try
			{
				IModuleConfig currentConfig = module?.ModuleConfig;
				if (!IsDirtyCashFrontRoomModule(currentConfig))
				{
					return true;
				}

				List<IModuleConfig> legalModules = GetQuietPlayerLegalModules();
				if (legalModules == null || legalModules.Count == 0)
				{
					return true;
				}

				Label currentId = currentConfig.Id;
				List<AddModuleDef> upgrades = new List<AddModuleDef>(legalModules.Count);
				HashSet<Label> added = new HashSet<Label>();
				for (int i = 0; i < legalModules.Count; i++)
				{
					IModuleConfig candidate = legalModules[i];
					if (candidate == null || candidate.Id == currentId || !ShouldShowDirtyCashLegalModule(candidate) || !added.Add(candidate.Id))
					{
						continue;
					}

					upgrades.Add(new AddModuleDef
					{
						config = candidate,
						passesReqs = true,
						passesVisreqs = true
					});
				}

				__result = upgrades;
				SetDirtyCashIsUpgradingFrontRoom();
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Quiet DirtyCash legal upgrade list failed; falling back to DirtyCashEconomy patch. " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static List<IModuleConfig> GetQuietPlayerLegalModules()
		{
			if (_quietPlayerLegalModules != null)
			{
				return _quietPlayerLegalModules;
			}

			List<IModuleConfig> modules = new List<IModuleConfig>();
			try
			{
				IEnumerable<IModuleConfig> allModules = ModulesUtil.FindAllModuleDefsExpensive();
				if (allModules != null)
				{
					foreach (IModuleConfig config in allModules)
					{
						if (IsDirtyCashPlayerLegalModule(config) && !config.Id.ToString().EndsWith("-base", StringComparison.Ordinal))
						{
							modules.Add(config);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to build quiet DirtyCash legal module cache: " + ex.Message);
			}

			_quietPlayerLegalModules = modules;
			Debug.Log("[PERF][DirtyCashLegalUI] quiet-cache modules=" + modules.Count);
			return _quietPlayerLegalModules;
		}

		private static bool QuietDirtyCashGetPlayerLegalModulesPrefix(ref List<IModuleConfig> __result)
		{
			List<IModuleConfig> modules = GetQuietPlayerLegalModules();
			if (modules == null || modules.Count == 0)
			{
				return true;
			}

			__result = modules;
			return false;
		}

		private static bool IsDirtyCashFrontRoomModule(IModuleConfig config)
		{
			try
			{
				if (_dirtyCashIsFrontRoomModuleMethod == null)
				{
					_dirtyCashIsFrontRoomModuleMethod = AccessTools.Method(AccessTools.TypeByName("DirtyCashEconomy.LegalBusinessPatches"), "IsFrontRoomModule");
				}
				if (_dirtyCashIsFrontRoomModuleMethod != null)
				{
					return (bool)_dirtyCashIsFrontRoomModuleMethod.Invoke(null, new object[] { config });
				}
			}
			catch
			{
			}

			TagList tags = config?.Common?.tags;
			if (tags == null)
			{
				return false;
			}

			foreach (Label tag in tags)
			{
				if (tag == TagConstants.TAG_SAFEHOUSE_FRONTROOMS)
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsDirtyCashPlayerLegalModule(IModuleConfig config)
		{
			TagList tags = config?.Common?.tags;
			if (tags == null)
			{
				return false;
			}

			foreach (Label tag in tags)
			{
				if (tag == TagPlayerLegalBiz)
				{
					return true;
				}
			}

			return false;
		}

		private static bool ShouldShowDirtyCashLegalModule(IModuleConfig config)
		{
			try
			{
				if (_dirtyCashShouldShowModuleMethod == null)
				{
					_dirtyCashShouldShowModuleMethod = AccessTools.Method(AccessTools.TypeByName("DirtyCashEconomy.LegalBusinessPatches"), "ShouldShowModule");
				}
				if (_dirtyCashShouldShowModuleMethod != null)
				{
					return (bool)_dirtyCashShouldShowModuleMethod.Invoke(null, new object[] { config });
				}
			}
			catch
			{
			}

			bool prohibitionActive = false;
			try
			{
				MethodInfo isProhibitionActive = AccessTools.Method(AccessTools.TypeByName("DirtyCashEconomy.LegalBusinessPatches"), "IsProhibitionActive");
				if (isProhibitionActive != null)
				{
					prohibitionActive = (bool)isProhibitionActive.Invoke(null, Array.Empty<object>());
				}
			}
			catch
			{
			}

			if (IsDirtyCashLegalLiquorModule(config))
			{
				return !prohibitionActive;
			}

			return !IsDirtyCashIllegalLiquorModule(config) || prohibitionActive;
		}

		private static bool IsDirtyCashLegalLiquorModule(IModuleConfig config)
		{
			TagList tags = config?.Common?.tags;
			if (tags == null)
			{
				return false;
			}

			foreach (Label tag in tags)
			{
				if (tag == TagLegalLiquor)
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsDirtyCashIllegalLiquorModule(IModuleConfig config)
		{
			return config != null && config.Id.ToString().StartsWith("bootlegger-", StringComparison.Ordinal);
		}

		private static void SetDirtyCashIsUpgradingFrontRoom()
		{
			try
			{
				PropertyInfo prop = AccessTools.Property(AccessTools.TypeByName("DirtyCashEconomy.LegalBusinessPatches"), "IsUpgradingFrontRoom");
				prop?.SetValue(null, true, null);
			}
			catch
			{
			}
		}

		private static bool FrameUpdateDuringInteractivePrefix(SessionContext __instance)
		{
			if (_interactiveTurnSmoothingDisabled)
			{
				return true;
			}

			try
			{
				if (!TryGetInteractiveTurnFields(
					__instance,
					out IPlayerTurnHandler playerTurnHandler,
					out ISingletonTurnSource turnSource,
					out List<IGlobalTurnSetHandler> turnAdvancedHandlers,
					out List<ISystemTurnHandler> systemTurnHandlers))
				{
					return true;
				}

				if (HasPendingSystemTurnWork(__instance))
				{
					long resumeTicks = Stopwatch.GetTimestamp();
					bool completed = ContinuePendingSystemTurn(
						__instance,
						resumeTicks,
						out int handlersRun,
						out int nextSystemHandler,
						out int totalSystemHandlers);
					long pendingElapsedMs = GetElapsedMilliseconds(resumeTicks);
					if (completed)
					{
						__instance.events.EnqueueOnce(SessionEventType.SystemTurnStarted);
					}
					if (ShouldLogPerformanceSlice(completed, pendingElapsedMs))
					{
						Debug.Log("[PERF][TurnFrame] phase=system-turn-slice ms=" + pendingElapsedMs + " handlersRun=" + handlersRun + " nextHandler=" + nextSystemHandler + " totalHandlers=" + totalSystemHandlers + " completed=" + completed + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
					}
					return false;
				}

				if (IsPlayerTurnStillGoing(__instance, playerTurnHandler))
				{
					return false;
				}

				long startTicks = Stopwatch.GetTimestamp();
				PlayerID startPlayer = __instance.clock.State.pid;
				int advances = 0;
				bool ranSystemTurn = false;
				for (int i = 0; i < InteractiveTurnMaxAdvancesPerFrame; i++)
				{
					if (advances > 0 && IsLastRealPlayer(__instance))
					{
						break;
					}

					bool advancedToSystem = AdvanceToNextPlayerOrSystem(
						__instance,
						playerTurnHandler,
						turnSource,
						turnAdvancedHandlers,
						systemTurnHandlers,
						startTicks);
					advances++;
					ranSystemTurn |= advancedToSystem;

					if (HasPendingSystemTurnWork(__instance))
					{
						break;
					}

					if (IsPlayerTurnStillGoing(__instance, playerTurnHandler))
					{
						break;
					}

					if (advancedToSystem)
					{
						if (GetElapsedMilliseconds(startTicks) < InteractiveTurnFrameBudgetMs)
						{
							continue;
						}

						break;
					}

					if (IsLastRealPlayer(__instance) || GetElapsedMilliseconds(startTicks) >= InteractiveTurnFrameBudgetMs)
					{
						break;
					}
				}

				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs >= GetInteractiveTurnFrameLogThresholdMs())
				{
					PlayerID endPlayer = __instance.clock.State.pid;
					Debug.Log("[PERF][TurnFrame] ms=" + elapsedMs + " advances=" + advances + " systemTurn=" + ranSystemTurn + " startPid=" + startPlayer.id + " endPid=" + endPlayer.id + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}

				return false;
			}
			catch (Exception ex)
			{
				_interactiveTurnSmoothingDisabled = true;
				Debug.LogWarning("[GameplayTweaks] Interactive turn smoothing disabled; falling back to vanilla. " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static bool HUDBarTryRefreshTurnPrefix(HUDBar __instance)
		{
			try
			{
				PlayerID currentPlayer = global::Game.Game.ctx?.clock?.CurrentPlayer ?? PlayerID.INVALID;
				if (!currentPlayer.IsSystem)
				{
					return true;
				}

				GameObject go = BaseHudGoField?.GetValue(__instance) as GameObject;
				if (go == null)
				{
					return true;
				}

				SimTime now = global::Game.Game.ctx.clock.Now;
				float progress = 0.99f;
				go.SetText("Clock/Date", Loc.Get("ui.hud.player", "percent", Loc.Percentage(progress)));
				go.GetChild("Clock/Progress Bar").SetActive(true);
				RectTransform slider = go.GetChild("Clock/Progress Bar/Slider").transform as RectTransform;
				if (slider != null)
				{
					slider.sizeDelta = slider.sizeDelta.SetX(250f * progress);
				}

				HudLastShownPlayerField?.SetValue(__instance, currentPlayer);
				HudLastShownPlayerTimeField?.SetValue(__instance, now);
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool HUDBarOnNextTurnClickPrefix(HUDBar __instance, out long __state)
		{
			__state = Stopwatch.GetTimestamp();
			GameplayTweaksPlugin.DeferUiMaintenanceForNextTurnClick("hud-next-turn-click", 12);
			try
			{
				SessionContext context = global::Game.Game.ctx;
				AllPlayersManager players = context?.players;
				if (players == null || context?.clock?.CurrentPlayer.IsHumanPlayer != true)
				{
					return true;
				}

				long audioTicks = Stopwatch.GetTimestamp();
				try
				{
					global::Game.Game.serv?.audio?.PlayUISFX(global::Game.Session.Assets.SFXType.ClickButtonNextTurn);
				}
				catch
				{
				}
				long audioMs = GetElapsedMilliseconds(audioTicks);

				long markTicks = Stopwatch.GetTimestamp();
				GameplayTweaksPlugin.CancelDeferredAlliancePactGangOpsForNextTurnInput("hud-next-turn-click-fast");
				players.Human?.ai?.MarkPlayerTurnAsDone();
				long markMs = GetElapsedMilliseconds(markTicks);

				if (!_loggedFastNextTurnSelectionClearSkipped
					&& context.selection?.HasActive == true
					&& IsPerformanceDiagnosticsEnabled())
				{
					_loggedFastNextTurnSelectionClearSkipped = true;
					Debug.Log("[PERF][HudInputDetail] method=HUDBar.OnNextTurnClickFast selectionClear=skipped reason=avoid-click-refresh-cascade day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " pid=" + GetPlayerForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}

				long buttonTicks = Stopwatch.GetTimestamp();
				TryDisableNextTurnButtonCheap(__instance);
				long buttonMs = GetElapsedMilliseconds(buttonTicks);
				long elapsedMs = GetElapsedMilliseconds(__state);
				if (elapsedMs >= GetHudInputLogThresholdMs())
				{
					Debug.Log("[PERF][HudInputDetail] method=HUDBar.OnNextTurnClickFast ms=" + elapsedMs + " audioMs=" + audioMs + " markTurnDoneMs=" + markMs + " buttonMs=" + buttonMs + " selectionClear=skipped frame=" + Time.frameCount + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " pid=" + GetPlayerForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static void HUDBarOnNextTurnClickPostfix(long __state)
		{
			try
			{
				long elapsedMs = GetElapsedMilliseconds(__state);
				if (elapsedMs < GetHudInputLogThresholdMs())
				{
					return;
				}

				Debug.Log("[PERF][HudInput] method=HUDBar.OnNextTurnClick ms=" + elapsedMs + " frame=" + Time.frameCount + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " pid=" + GetPlayerForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
			}
		}

		private static void TryDisableNextTurnButtonCheap(HUDBar hudBar)
		{
			try
			{
				GameObject go = BaseHudGoField?.GetValue(hudBar) as GameObject;
				if (go == null)
				{
					return;
				}

				go.GetButton("Clock/Next Turn Button").interactable = false;
				go.SetText("Clock/Next Turn Button/Text", TextUtil.ColorEnabledIf(false, Loc.Get("ui.hud.nextturn")));
			}
			catch
			{
			}
		}

		private static bool HUDBarTryRefreshCornersPrefix(HUDBar __instance)
		{
			long startTicks = Stopwatch.GetTimestamp();
			try
			{
				PlayerTerritory territory = global::Game.Game.ctx?.players?.Human?.territory;
				if (territory == null)
				{
					return true;
				}

				int frame = Time.frameCount;
				if (GameplayTweaksPlugin.IsUiMaintenanceDeferredForInput() && _cachedHudCornerCount != int.MinValue)
				{
					return false;
				}

				int cornerCount = territory.OwnedNodeCount;
				if (_cachedHudCornerCount == cornerCount && frame - _cachedHudCornerFrame < 300)
				{
					return false;
				}

				TextMeshProUGUI cornersText = HudCornersTextField?.GetValue(__instance) as TextMeshProUGUI;
				if (cornersText == null)
				{
					return true;
				}

				cornersText.SetText(Loc.Get("ui.hud.corners", "num", cornerCount));
				_cachedHudCornerCount = cornerCount;
				_cachedHudCornerFrame = frame;

				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs >= GetHudInputLogThresholdMs())
				{
					Debug.Log("[PERF][HudCornersCached] ms=" + elapsedMs + " corners=" + cornerCount + " frame=" + frame + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " pid=" + GetPlayerForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static void HudUpdateAnimationsDetailPrefix(out HudUpdateAnimationDetailState __state)
		{
			__state = new HudUpdateAnimationDetailState
			{
				StartTicks = Stopwatch.GetTimestamp(),
				Previous = _activeHudUpdateAnimationDetail
			};
			_activeHudUpdateAnimationDetail = __state;
		}

		private static void HudUpdateAnimationsDetailPostfix(HudUpdateAnimationDetailState __state)
		{
			try
			{
				if (_activeHudUpdateAnimationDetail == __state)
				{
					_activeHudUpdateAnimationDetail = __state?.Previous;
				}

				if (__state == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				if (elapsedMs < GetHudInputLogThresholdMs())
				{
					return;
				}

				long measuredMs =
					__state.RefreshTurnMs +
					__state.RefreshMoneyMs +
					__state.RefreshCrewMs +
					__state.RefreshCornersMs +
					__state.NextTurnButtonMs +
					__state.HyperlinkMs +
					__state.OtherStepMs;

				Debug.Log(
					"[PERF][HudUpdateAnimations] ms=" + elapsedMs +
					" refreshTurnMs=" + __state.RefreshTurnMs +
					" refreshTurnCalls=" + __state.RefreshTurnCalls +
					" moneyMs=" + __state.RefreshMoneyMs +
					" moneyCalls=" + __state.RefreshMoneyCalls +
					" crewMs=" + __state.RefreshCrewMs +
					" crewCalls=" + __state.RefreshCrewCalls +
					" cornersMs=" + __state.RefreshCornersMs +
					" cornersCalls=" + __state.RefreshCornersCalls +
					" nextTurnButtonMs=" + __state.NextTurnButtonMs +
					" nextTurnButtonCalls=" + __state.NextTurnButtonCalls +
					" hyperlinkMs=" + __state.HyperlinkMs +
					" hyperlinkCalls=" + __state.HyperlinkCalls +
					" otherStepMs=" + __state.OtherStepMs +
					" otherStepCalls=" + __state.OtherStepCalls +
					" unmeasuredMs=" + Math.Max(0L, elapsedMs - measuredMs) +
					" maxStepMs=" + __state.MaxStepMs +
					" maxStep=" + (__state.MaxStepName ?? "none") +
					" currentPid=" + GetPlayerForLog() +
					" day=" + GetDayForLog() +
					" year=" + GetYearForLog() +
					" turn=" + GetTurnForLog() +
					" totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
				if (_activeHudUpdateAnimationDetail == __state)
				{
					_activeHudUpdateAnimationDetail = __state?.Previous;
				}
			}
		}

		private static void HudUpdateStepDetailPrefix(MethodBase __originalMethod, out HudUpdateStepTimingState __state)
		{
			__state = _activeHudUpdateAnimationDetail != null
				? new HudUpdateStepTimingState(Stopwatch.GetTimestamp(), __originalMethod?.Name ?? "unknown")
				: default;
		}

		private static void HudUpdateStepDetailPostfix(HudUpdateStepTimingState __state)
		{
			try
			{
				if (__state.StartTicks == 0L)
				{
					return;
				}

				HudUpdateAnimationDetailState detail = _activeHudUpdateAnimationDetail;
				if (detail == null)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				switch (__state.MethodName)
				{
					case "TryRefreshTurn":
						detail.RefreshTurnCalls++;
						detail.RefreshTurnMs += elapsedMs;
						break;
					case "TryRefreshMoney":
						detail.RefreshMoneyCalls++;
						detail.RefreshMoneyMs += elapsedMs;
						break;
					case "TryRefreshCrew":
						detail.RefreshCrewCalls++;
						detail.RefreshCrewMs += elapsedMs;
						break;
					case "TryRefreshCorners":
						detail.RefreshCornersCalls++;
						detail.RefreshCornersMs += elapsedMs;
						break;
					case "UpdateNextTurnButton":
						detail.NextTurnButtonCalls++;
						detail.NextTurnButtonMs += elapsedMs;
						break;
					case "ProcessHyperlink":
						detail.HyperlinkCalls++;
						detail.HyperlinkMs += elapsedMs;
						break;
					default:
						detail.OtherStepCalls++;
						detail.OtherStepMs += elapsedMs;
						break;
				}

				if (elapsedMs > detail.MaxStepMs)
				{
					detail.MaxStepMs = elapsedMs;
					detail.MaxStepName = __state.MethodName;
				}
			}
			catch
			{
			}
		}

		private static bool IsLastRealPlayer(SessionContext context)
		{
			try
			{
				if (context?.clock == null || context.players?.all == null)
				{
					return false;
				}

				PlayerID currentPlayer = context.clock.CurrentPlayer;
				return currentPlayer.IsAnyPlayer && !currentPlayer.IsHumanPlayer && currentPlayer.id >= context.players.all.Count - 1;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryGetInteractiveTurnFields(
			SessionContext context,
			out IPlayerTurnHandler playerTurnHandler,
			out ISingletonTurnSource turnSource,
			out List<IGlobalTurnSetHandler> turnAdvancedHandlers,
			out List<ISystemTurnHandler> systemTurnHandlers)
		{
			playerTurnHandler = SessionPlayerTurnHandlerField?.GetValue(context) as IPlayerTurnHandler;
			turnSource = SessionTurnSourceField?.GetValue(context) as ISingletonTurnSource;
			turnAdvancedHandlers = SessionTurnAdvancedHandlersField?.GetValue(context) as List<IGlobalTurnSetHandler>;
			systemTurnHandlers = SessionSystemTurnHandlersField?.GetValue(context) as List<ISystemTurnHandler>;
			return context?.clock != null
				&& context.events != null
				&& playerTurnHandler != null
				&& turnSource != null
				&& turnAdvancedHandlers != null
				&& systemTurnHandlers != null;
		}

		private static bool IsPlayerTurnStillGoing(SessionContext context, IPlayerTurnHandler playerTurnHandler)
		{
			if (context == null || context.clock == null)
			{
				return false;
			}

			if (context.clock.State.pid.IsAnyPlayer)
			{
				return !playerTurnHandler.IsPlayerTurnDone();
			}

			return false;
		}

		private static bool AdvanceToNextPlayerOrSystem(
			SessionContext context,
			IPlayerTurnHandler playerTurnHandler,
			ISingletonTurnSource turnSource,
			List<IGlobalTurnSetHandler> turnAdvancedHandlers,
			List<ISystemTurnHandler> systemTurnHandlers,
			long frameStartTicks)
		{
			if (context.clock.State.pid.IsAnyPlayer)
			{
				bool isHumanPlayer = context.clock.State.pid.IsHumanPlayer;
				long playerEndTicks = Stopwatch.GetTimestamp();
				playerTurnHandler.OnPlayerTurnEnded();
				LogTurnHandlerDetail("player-turn-ended", playerTurnHandler, playerEndTicks);
				if (isHumanPlayer)
				{
					context.events.SendImmediate(SessionEventType.HumanPlayerTurnEnded);
				}
			}

			long advanceTicks = Stopwatch.GetTimestamp();
			bool advancedToSystem = turnSource.AdvancePlayerOrSystem();
			LogTurnHandlerDetail("advance-player-or-system", turnSource, advanceTicks);
			if (advancedToSystem)
			{
				long globalTurnTicks = Stopwatch.GetTimestamp();
				turnSource.AdvanceGlobalGameTurn();
				LogTurnHandlerDetail("advance-global-game-turn", turnSource, globalTurnTicks);
				foreach (IGlobalTurnSetHandler turnAdvancedHandler in turnAdvancedHandlers)
				{
					long handlerTicks = Stopwatch.GetTimestamp();
					try
					{
						turnAdvancedHandler.OnGlobalTurnSetAdvanced();
					}
					finally
					{
						LogTurnHandlerDetail("global-turn-set-advanced", turnAdvancedHandler, handlerTicks);
					}
				}
			}

			if (context.clock.State.pid.IsAnyPlayer)
			{
				long playerStartTicks = Stopwatch.GetTimestamp();
				playerTurnHandler.OnPlayerTurnStarted();
				LogTurnHandlerDetail("player-turn-started", playerTurnHandler, playerStartTicks);
			}
			else
			{
				BeginPendingSystemTurn(context, systemTurnHandlers);
				if (GetElapsedMilliseconds(frameStartTicks) >= InteractiveTurnFrameBudgetMs)
				{
					return true;
				}

				bool completed = ContinuePendingSystemTurn(
					context,
					frameStartTicks,
					out int handlersRun,
					out int nextSystemHandler,
					out int totalSystemHandlers);
				long elapsedMs = GetElapsedMilliseconds(frameStartTicks);
				if (ShouldLogPerformanceSlice(completed, elapsedMs))
				{
					Debug.Log("[PERF][TurnFrame] phase=system-turn-slice ms=" + elapsedMs + " handlersRun=" + handlersRun + " nextHandler=" + nextSystemHandler + " totalHandlers=" + totalSystemHandlers + " completed=" + completed + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}
				if (!completed)
				{
					return true;
				}
			}

			SessionEventType type = context.clock.State.pid.IsHumanPlayer
				? SessionEventType.HumanPlayerTurnStarted
				: (context.clock.State.pid.IsAnyPlayer ? SessionEventType.OtherPlayerTurnStarted : SessionEventType.SystemTurnStarted);
			context.events.EnqueueOnce(type);
			return advancedToSystem;
		}

		private static void BeginPendingSystemTurn(SessionContext context, List<ISystemTurnHandler> systemTurnHandlers)
		{
			_pendingSystemTurnWork = new PendingSystemTurnWork
			{
				Context = context,
				Handlers = systemTurnHandlers,
				NextIndex = 0,
				StartedTicks = Stopwatch.GetTimestamp(),
				Day = GetDayForLog(),
				Turn = GetTurnForLog()
			};
		}

		private static bool HasPendingSystemTurnWork(SessionContext context)
		{
			if (_pendingSystemTurnWork == null)
			{
				return false;
			}

			if (!ReferenceEquals(_pendingSystemTurnWork.Context, context))
			{
				ClearPendingSystemTurnWork();
				return false;
			}

			return true;
		}

		private static bool ContinuePendingSystemTurn(
			SessionContext context,
			long frameStartTicks,
			out int handlersRun,
			out int nextSystemHandler,
			out int totalSystemHandlers)
		{
			handlersRun = 0;
			nextSystemHandler = -1;
			totalSystemHandlers = 0;
			PendingSystemTurnWork work = _pendingSystemTurnWork;
			if (work == null || !ReferenceEquals(work.Context, context))
			{
				ClearPendingSystemTurnWork();
				return true;
			}

			HoldUiMaintenanceForPendingSystemTurn(work, "system-turn-slice");

			List<ISystemTurnHandler> handlers = work.Handlers;
			totalSystemHandlers = handlers?.Count ?? 0;
			if (handlers == null || handlers.Count == 0)
			{
				ClearPendingSystemTurnWork();
				return true;
			}

			while (work.NextIndex < handlers.Count)
			{
				ISystemTurnHandler systemTurnHandler = handlers[work.NextIndex];
				if (systemTurnHandler is SimulationManager simulationManager && TryGetSimulationSubmanagers(simulationManager, out List<ISystemTurnSubManager<SimulationManager>> simulationSubmanagers))
				{
					long simulationSliceTicks = Stopwatch.GetTimestamp();
					bool simulationCompleted = ContinueSimulationManagerTurn(
						work,
						simulationManager,
						simulationSubmanagers,
						simulationSliceTicks,
						out int simulationHandlersRun,
						out int nextSimulationHandler,
						out int totalSimulationHandlers);
					long simulationSliceMs = GetElapsedMilliseconds(simulationSliceTicks);
					if (ShouldLogPerformanceSlice(simulationCompleted, simulationSliceMs))
					{
						Debug.Log("[PERF][SimulationManagerSlice] ms=" + simulationSliceMs + " submanagersRun=" + simulationHandlersRun + " nextSubmanager=" + nextSimulationHandler + " totalSubmanagers=" + totalSimulationHandlers + " completed=" + simulationCompleted + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
					}

					handlersRun++;
					if (!simulationCompleted)
					{
						nextSystemHandler = work.NextIndex;
						return false;
					}

					work.NextIndex++;
					if (handlersRun >= InteractiveSystemTurnMaxHandlersPerFrame
						&& work.NextIndex < handlers.Count)
					{
						nextSystemHandler = work.NextIndex;
						return false;
					}

					if (work.NextIndex < handlers.Count && GetElapsedMilliseconds(frameStartTicks) >= InteractiveTurnFrameBudgetMs)
					{
						nextSystemHandler = work.NextIndex;
						return false;
					}

					continue;
				}

				long handlerTicks = Stopwatch.GetTimestamp();
				try
				{
					systemTurnHandler?.OnSystemTurn();
				}
				catch
				{
				}
				finally
				{
					LogTurnHandlerDetail("system-turn", systemTurnHandler, handlerTicks);
				}

				work.NextIndex++;
				handlersRun++;
				if (handlersRun >= InteractiveSystemTurnMaxHandlersPerFrame
					&& work.NextIndex < handlers.Count)
				{
					nextSystemHandler = work.NextIndex;
					return false;
				}

				if (work.NextIndex < handlers.Count && GetElapsedMilliseconds(frameStartTicks) >= InteractiveTurnFrameBudgetMs)
				{
					nextSystemHandler = work.NextIndex;
					return false;
				}
			}

			long totalMs = GetElapsedMilliseconds(work.StartedTicks);
			if (ShouldLogSystemMaintenanceTotal(totalMs))
			{
				Debug.Log("[PERF][TurnFrame] phase=system-turn-total ms=" + totalMs + " day=" + work.Day + " turn=" + work.Turn + " handlers=" + handlers.Count);
			}
			nextSystemHandler = handlers.Count;
			ClearPendingSystemTurnWork();
			return true;
		}

		private static void ClearPendingSystemTurnWork()
		{
			EndBusinessUpdateScopeIfNeeded(_pendingSystemTurnWork?.BusinessTrackerWork);
			_pendingSystemTurnWork = null;
		}

		private static void HoldUiMaintenanceForPendingSystemTurn(PendingSystemTurnWork work, string sourceTag)
		{
			try
			{
				if (work == null)
				{
					return;
				}

				bool log = !work.UiMaintenanceHoldLogged;
				GameplayTweaksPlugin.HoldUiMaintenanceDuringTurnProcessing(sourceTag, 4, log);
				if (log)
				{
					work.UiMaintenanceHoldLogged = true;
				}
			}
			catch
			{
			}
		}

		private static bool TryGetSimulationSubmanagers(SimulationManager simulationManager, out List<ISystemTurnSubManager<SimulationManager>> submanagers)
		{
			submanagers = null;
			try
			{
				if (!(global::Game.Game.ctx?.IsInteractive ?? false) || SimulationTurnUpdatesField == null)
				{
					return false;
				}

				submanagers = SimulationTurnUpdatesField.GetValue(simulationManager) as List<ISystemTurnSubManager<SimulationManager>>;
				return submanagers != null && submanagers.Count > 0;
			}
			catch
			{
				submanagers = null;
				return false;
			}
		}

		private static bool ContinueSimulationManagerTurn(
			PendingSystemTurnWork work,
			SimulationManager simulationManager,
			List<ISystemTurnSubManager<SimulationManager>> submanagers,
			long frameStartTicks,
			out int submanagersRun,
			out int nextSubmanager,
			out int totalSubmanagers)
		{
			submanagersRun = 0;
			nextSubmanager = -1;
			totalSubmanagers = submanagers?.Count ?? 0;
			if (work == null || simulationManager == null || submanagers == null || submanagers.Count == 0)
			{
				return true;
			}

			if (!ReferenceEquals(work.ActiveSimulationManager, simulationManager))
			{
				work.ActiveSimulationManager = simulationManager;
				work.SimulationSubmanagers = submanagers;
				work.NextSimulationIndex = 0;
				work.SimulationStartedTicks = Stopwatch.GetTimestamp();
				ResetSimulationProfiling(work, submanagers);
			}

			if (work.LastSimulationSliceEndTicks != 0L)
			{
				long resumeGapMs = GetElapsedMilliseconds(work.LastSimulationSliceEndTicks);
				if (resumeGapMs > work.SimulationMaxResumeGapMs)
				{
					work.SimulationMaxResumeGapMs = resumeGapMs;
				}
			}

			while (work.NextSimulationIndex < submanagers.Count)
			{
				int profileIndex = work.NextSimulationIndex;
				ISystemTurnSubManager<SimulationManager> submanager = submanagers[work.NextSimulationIndex];
				if (submanager is BusinessTracker businessTracker && global::Game.Game.ctx?.IsInteractive == true)
				{
					long businessTrackerTicks = Stopwatch.GetTimestamp();
					bool businessTrackerCompleted = ContinueBusinessTrackerTurn(
						work,
						businessTracker,
						businessTrackerTicks,
						out string businessPhase,
						out int nextBusinessPhase,
						out int totalBusinessPhases);
					long businessTrackerMs = GetElapsedMilliseconds(businessTrackerTicks);
					if (ShouldLogPerformanceSlice(businessTrackerCompleted, businessTrackerMs))
					{
						Debug.Log("[PERF][BusinessTrackerSlice] ms=" + businessTrackerMs + " phase=" + businessPhase + " nextPhase=" + nextBusinessPhase + " totalPhases=" + totalBusinessPhases + " completed=" + businessTrackerCompleted + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
					}

					RecordSimulationSubmanagerProfile(work, submanager, profileIndex, businessTrackerMs);
					submanagersRun++;
					if (!businessTrackerCompleted)
					{
						work.LastSimulationSliceEndTicks = Stopwatch.GetTimestamp();
						nextSubmanager = work.NextSimulationIndex;
						return false;
					}

					work.NextSimulationIndex++;
					if (submanagersRun >= InteractiveSimulationSubmanagersPerFrame
						&& work.NextSimulationIndex < submanagers.Count
						&& !CanDrainCheapSimulationTail(work, frameStartTicks))
					{
						work.LastSimulationSliceEndTicks = Stopwatch.GetTimestamp();
						nextSubmanager = work.NextSimulationIndex;
						return false;
					}

					if (work.NextSimulationIndex < submanagers.Count
						&& GetElapsedMilliseconds(frameStartTicks) >= InteractiveTurnFrameBudgetMs
						&& !CanDrainCheapSimulationTail(work, frameStartTicks))
					{
						work.LastSimulationSliceEndTicks = Stopwatch.GetTimestamp();
						nextSubmanager = work.NextSimulationIndex;
						return false;
					}

					continue;
				}

				long submanagerTicks = Stopwatch.GetTimestamp();
				try
				{
					submanager?.OnSystemTurn();
				}
				catch
				{
				}
				finally
				{
					LogTurnHandlerDetail("simulation-submanager", submanager, submanagerTicks);
				}
				long submanagerMs = GetElapsedMilliseconds(submanagerTicks);
				RecordSimulationSubmanagerProfile(work, submanager, profileIndex, submanagerMs);

				work.NextSimulationIndex++;
				submanagersRun++;
				if (submanagersRun >= InteractiveSimulationSubmanagersPerFrame
					&& work.NextSimulationIndex < submanagers.Count
					&& !CanDrainCheapSimulationTail(work, frameStartTicks))
				{
					work.LastSimulationSliceEndTicks = Stopwatch.GetTimestamp();
					nextSubmanager = work.NextSimulationIndex;
					return false;
				}

				if (work.NextSimulationIndex < submanagers.Count
					&& GetElapsedMilliseconds(frameStartTicks) >= InteractiveTurnFrameBudgetMs
					&& !CanDrainCheapSimulationTail(work, frameStartTicks))
				{
					work.LastSimulationSliceEndTicks = Stopwatch.GetTimestamp();
					nextSubmanager = work.NextSimulationIndex;
					return false;
				}
			}

			long totalMs = GetElapsedMilliseconds(work.SimulationStartedTicks);
			if (ShouldLogSystemMaintenanceTotal(totalMs))
			{
				Debug.Log("[PERF][SimulationManagerSlice] phase=total ms=" + totalMs + " sliceCpuMs=" + work.SimulationSliceCpuMs + " maxResumeGapMs=" + work.SimulationMaxResumeGapMs + " submanagers=" + submanagers.Count + " profile=" + FormatSimulationSubmanagerProfile(work) + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}

			nextSubmanager = submanagers.Count;
			work.ActiveSimulationManager = null;
			work.SimulationSubmanagers = null;
			work.NextSimulationIndex = 0;
			work.SimulationStartedTicks = 0L;
			work.SimulationSliceCpuMs = 0L;
			work.SimulationMaxResumeGapMs = 0L;
			work.LastSimulationSliceEndTicks = 0L;
			work.SimulationProfiles = null;
			return true;
		}

		private static void ResetSimulationProfiling(PendingSystemTurnWork work, List<ISystemTurnSubManager<SimulationManager>> submanagers)
		{
			work.SimulationSliceCpuMs = 0L;
			work.SimulationMaxResumeGapMs = 0L;
			work.LastSimulationSliceEndTicks = 0L;
			int count = submanagers?.Count ?? 0;
			work.SimulationProfiles = new List<SimulationSubmanagerProfile>(count);
			for (int i = 0; i < count; i++)
			{
				work.SimulationProfiles.Add(new SimulationSubmanagerProfile
				{
					Name = FormatProfileName(submanagers[i])
				});
			}
		}

		private static void RecordSimulationSubmanagerProfile(PendingSystemTurnWork work, ISystemTurnSubManager<SimulationManager> submanager, int index, long elapsedMs)
		{
			if (work == null)
			{
				return;
			}

			work.SimulationSliceCpuMs += elapsedMs;
			List<SimulationSubmanagerProfile> profiles = work.SimulationProfiles;
			if (profiles == null || index < 0 || index >= profiles.Count)
			{
				return;
			}

			SimulationSubmanagerProfile profile = profiles[index];
			if (string.IsNullOrEmpty(profile.Name))
			{
				profile.Name = FormatProfileName(submanager);
			}

			profile.CpuMs += elapsedMs;
			profile.Calls++;
			if (elapsedMs > profile.MaxMs)
			{
				profile.MaxMs = elapsedMs;
			}
		}

		private static string FormatSimulationSubmanagerProfile(PendingSystemTurnWork work)
		{
			List<SimulationSubmanagerProfile> profiles = work?.SimulationProfiles;
			if (profiles == null || profiles.Count == 0)
			{
				return "none";
			}

			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < profiles.Count; i++)
			{
				SimulationSubmanagerProfile profile = profiles[i];
				if (profile == null || profile.Calls <= 0)
				{
					continue;
				}

				if (builder.Length > 0)
				{
					builder.Append(',');
				}

				builder.Append(string.IsNullOrEmpty(profile.Name) ? "unknown" : profile.Name);
				builder.Append(':');
				builder.Append(profile.CpuMs);
				builder.Append('/');
				builder.Append(profile.Calls);
				builder.Append('/');
				builder.Append(profile.MaxMs);
			}

			return builder.Length == 0 ? "none" : builder.ToString();
		}

		private static string FormatProfileName(object instance)
		{
			return instance?.GetType().Name ?? "unknown";
		}

		private static bool CanDrainCheapSimulationTail(PendingSystemTurnWork work, long frameStartTicks)
		{
			try
			{
				if (work?.SimulationSubmanagers == null)
				{
					return false;
				}

				if (work.NextSimulationIndex >= work.SimulationSubmanagers.Count)
				{
					return false;
				}

				long elapsedMs = GetElapsedMilliseconds(frameStartTicks);
				if (IsLargeInteractiveSave() && work.NextSimulationIndex == 2)
				{
					return elapsedMs < LargeSaveBusinessEntrySimulationBudgetMs;
				}

				long postBusinessTailBudgetMs = IsLargeInteractiveSave()
					? LargeSavePostBusinessSimulationTailBudgetMs
					: InteractivePostBusinessSimulationTailBudgetMs;
				if (work.NextSimulationIndex >= 4)
				{
					return elapsedMs < postBusinessTailBudgetMs;
				}

				// Once BusinessTracker is complete, do not leave the small Politics/tail pass pending if
				// the slice is still bounded. Pending resumes can be delayed by the game loop at 100%.
				return work.NextSimulationIndex == 3
					&& elapsedMs < postBusinessTailBudgetMs;
			}
			catch
			{
				return false;
			}
		}

		private static bool ContinueBusinessTrackerTurn(
			PendingSystemTurnWork work,
			BusinessTracker tracker,
			long frameStartTicks,
			out string phase,
			out int nextPhase,
			out int totalPhases)
		{
			phase = "unknown";
			nextPhase = -1;
			totalPhases = SlicedBusinessUpdatePhases.Length + 1;
			if (work == null || tracker == null)
			{
				return true;
			}

			long trackerFrameBudgetMs = GetInteractiveBusinessTrackerFrameBudgetMs();
			long trackerCheapContinuationBudgetMs = GetInteractiveBusinessTrackerCheapContinuationBudgetMs();
			PendingBusinessTrackerTurnWork businessWork = work.BusinessTrackerWork;
			if (businessWork == null || !ReferenceEquals(businessWork.Tracker, tracker))
			{
				EndBusinessUpdateScopeIfNeeded(businessWork);
				businessWork = new PendingBusinessTrackerTurnWork
				{
					Tracker = tracker,
					OwnerStepCompleted = false,
					NextPhaseIndex = 0,
					StartedTicks = Stopwatch.GetTimestamp()
				};
				work.BusinessTrackerWork = businessWork;
			}

			int phasesRun = 0;
			if (!businessWork.OwnerStepCompleted)
			{
				phase = "owners";
				long ownerTicks = Stopwatch.GetTimestamp();
				RunBusinessTrackerOwnerStep(tracker);
				LogBusinessTrackerPhase("owners", ownerTicks);
				businessWork.OwnerStepCompleted = true;
				nextPhase = businessWork.NextPhaseIndex;
				if (SlicedBusinessUpdatePhases.Length > 0 && GetElapsedMilliseconds(frameStartTicks) >= trackerFrameBudgetMs)
				{
					return false;
				}
			}

			if (!businessWork.BusinessUpdateScopeStarted)
			{
				TurnPerformanceOptimizationsPatch.BeginBusinessUpdateTickScope();
				businessWork.BusinessUpdateScopeStarted = true;
			}

			while (businessWork.NextPhaseIndex < SlicedBusinessUpdatePhases.Length)
			{
				BusinessUpdatePhase updatePhase = SlicedBusinessUpdatePhases[businessWork.NextPhaseIndex];
				phase = updatePhase.Name;
				long phaseTicks = Stopwatch.GetTimestamp();
				if (string.Equals(updatePhase.Name, "UpdateBusinessModules", StringComparison.Ordinal))
				{
					bool businessModulesCompleted = ContinueSlicedUpdateBusinessModules(
						businessWork,
						frameStartTicks,
						trackerFrameBudgetMs,
						initial: false,
						out int modulesUpdated,
						out int nextBusiness,
						out int totalBusinesses,
						out string businessStopReason);
					if (!businessModulesCompleted)
					{
						nextPhase = businessWork.NextPhaseIndex;
						long elapsedMs = GetElapsedMilliseconds(phaseTicks);
						if (ShouldLogPerformanceSlice(false, elapsedMs))
						{
							Debug.Log("[PERF][BusinessUpdateModulesSlice] ms=" + elapsedMs + " updated=" + modulesUpdated + " nextBusiness=" + nextBusiness + " totalBusinesses=" + totalBusinesses + " stopReason=" + businessStopReason + " completed=False day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
						}
						return false;
					}
				}
				else if (string.Equals(updatePhase.Name, "UpdateRespectFromRelationships", StringComparison.Ordinal))
				{
					bool relationshipRespectCompleted = ContinueSlicedUpdateRespectFromRelationships(
						businessWork,
						frameStartTicks,
						trackerFrameBudgetMs,
						out int playersUpdated,
						out int nextPlayer,
						out int totalPlayers,
						out string relationshipStopReason);
					if (!relationshipRespectCompleted)
					{
						nextPhase = businessWork.NextPhaseIndex;
						long elapsedMs = GetElapsedMilliseconds(phaseTicks);
						if (ShouldLogPerformanceSlice(false, elapsedMs))
						{
							Debug.Log("[PERF][RelationshipRespectSlice] ms=" + elapsedMs + " playersUpdated=" + playersUpdated + " nextPlayer=" + nextPlayer + " totalPlayers=" + totalPlayers + " stopReason=" + relationshipStopReason + " completed=False day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog());
						}
						return false;
					}
				}
				else
				{
					InvokeBusinessUpdatePhase(updatePhase, initial: false);
				}
				LogBusinessTrackerPhase(updatePhase.Name, phaseTicks);
				businessWork.NextPhaseIndex++;
				phasesRun++;
				nextPhase = businessWork.NextPhaseIndex;
				bool canDrainBusinessTail =
					CanDrainLargeSaveBusinessTrackerPostRelationshipTail(businessWork, frameStartTicks)
					|| CanDrainLargeSaveBusinessTrackerFinalTail(businessWork, frameStartTicks);
				if (businessWork.NextPhaseIndex < SlicedBusinessUpdatePhases.Length
					&& phasesRun >= InteractiveBusinessUpdatePhasesPerFrame
					&& GetElapsedMilliseconds(frameStartTicks) >= trackerCheapContinuationBudgetMs
					&& !canDrainBusinessTail)
				{
					return false;
				}

				if (businessWork.NextPhaseIndex < SlicedBusinessUpdatePhases.Length
					&& GetElapsedMilliseconds(frameStartTicks) >= trackerFrameBudgetMs
					&& !canDrainBusinessTail)
				{
					return false;
				}
			}

			EndBusinessUpdateScopeIfNeeded(businessWork);
			long totalMs = GetElapsedMilliseconds(businessWork.StartedTicks);
			if (ShouldLogSystemMaintenanceTotal(totalMs))
			{
				Debug.Log("[PERF][BusinessTrackerSlice] phase=total ms=" + totalMs + " phases=" + totalPhases + " businessModulesWallMs=" + businessWork.BusinessModulesWallMs + " businessModulesSliceCpuMs=" + businessWork.BusinessModulesSliceCpuMs + " businessModulesSlices=" + businessWork.BusinessModulesSlices + " relationshipRespectWallMs=" + businessWork.RelationshipRespectWallMs + " relationshipRespectSliceCpuMs=" + businessWork.RelationshipRespectSliceCpuMs + " relationshipRespectSlices=" + businessWork.RelationshipRespectSlices + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}

			work.BusinessTrackerWork = null;
			phase = "complete";
			nextPhase = totalPhases;
			return true;
		}

		private static void RunBusinessTrackerOwnerStep(BusinessTracker tracker)
		{
			try
			{
				object potentialOwnersCache = BusinessTrackerPotentialOwnersCacheField?.GetValue(tracker);
				MethodInfo clearMethod = potentialOwnersCache?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				clearMethod?.Invoke(potentialOwnersCache, null);

				HashSet<Entity> bizWithoutOwners = BusinessTrackerBizWithoutOwnersField?.GetValue(tracker) as HashSet<Entity>;
				if ((bizWithoutOwners?.Count ?? 0) > 0 && global::Game.Game.ctx?.IsInteractive == true)
				{
					BusinessTrackerTryAssignOwnersAsNeededMethod?.Invoke(tracker, null);
				}
			}
			catch (Exception ex)
			{
				LogBusinessUpdateException(ex);
			}
		}

		private static bool ContinueSlicedUpdateBusinessModules(
			PendingBusinessTrackerTurnWork businessWork,
			long frameStartTicks,
			long frameBudgetMs,
			bool initial,
			out int modulesUpdated,
			out int nextBusiness,
			out int totalBusinesses,
			out string stopReason)
		{
			modulesUpdated = 0;
			nextBusiness = -1;
			totalBusinesses = 0;
			stopReason = "complete";
			if (businessWork == null)
			{
				return true;
			}

			SessionContext context = global::Game.Game.ctx;
			PendingBusinessModulesUpdateWork updateWork = businessWork.BusinessModulesUpdateWork;
			if (updateWork != null && !ReferenceEquals(updateWork.Context, context))
			{
				EndSlicedUpdateBusinessModulesIfNeeded(updateWork);
				updateWork = null;
				businessWork.BusinessModulesUpdateWork = null;
			}

			if (updateWork == null)
			{
				long initTicks = Stopwatch.GetTimestamp();
				long listTicks = Stopwatch.GetTimestamp();
				string businessListSource;
				int allBusinessCount;
				List<Entity> businessList = BuildOwnedBusinessModuleUpdateList(context, out businessListSource, out allBusinessCount);
				long listMs = GetElapsedMilliseconds(listTicks);
				if (businessList == null)
				{
					return true;
				}

				long cacheTicks = Stopwatch.GetTimestamp();
				EnsureBusinessModuleComponentCacheContext(context);
				long cacheMs = GetElapsedMilliseconds(cacheTicks);
				updateWork = new PendingBusinessModulesUpdateWork
				{
					Context = context,
					Businesses = businessList,
					HumanControlledBuildings = BuildHumanControlledBuildingSet(context),
					NextIndex = 0,
					Now = context.clock.Now,
					StartedTicks = initTicks
				};
				businessWork.BusinessModulesUpdateWork = updateWork;
				long dirtyCashTicks = Stopwatch.GetTimestamp();
				InvokeDirtyCashUpdateBusinessModulesPrefix(updateWork);
				long dirtyCashMs = GetElapsedMilliseconds(dirtyCashTicks);
				long initMs = GetElapsedMilliseconds(initTicks);
				if (initMs >= BusinessModuleInitDetailThresholdMs)
				{
					Debug.Log("[PERF][BusinessUpdateModulesInit] ms=" + initMs + " listMs=" + listMs + " cacheMs=" + cacheMs + " dirtyCashPrefixMs=" + dirtyCashMs + " businesses=" + businessList.Count + " allBusinesses=" + allBusinessCount + " listSource=" + businessListSource + " economyBridgeMethodsWarmed=" + (_afterProhibitionEconomyOwnsDirtyCashRuntimeSweepMutationMethod != null && _afterProhibitionEconomyBeginDirtyCashRuntimeSweepBusinessUpdateMethod != null) + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}
			}

			List<Entity> businessesToUpdate = updateWork.Businesses;
			totalBusinesses = businessesToUpdate?.Count ?? 0;
			if (businessesToUpdate == null || businessesToUpdate.Count == 0)
			{
				CompleteSlicedUpdateBusinessModules(businessWork, updateWork, initial);
				nextBusiness = totalBusinesses;
				return true;
			}

			long sliceTicks = Stopwatch.GetTimestamp();
			int maxUpdates = Math.Max(1, GetInteractiveBusinessModuleUpdatesPerFrame(totalBusinesses));
			long budgetMs = Math.Max(1L, GetInteractiveBusinessModuleUpdateBudgetMs(totalBusinesses));
			while (updateWork.NextIndex < businessesToUpdate.Count && modulesUpdated < maxUpdates)
			{
				Entity business = businessesToUpdate[updateWork.NextIndex++];
				modulesUpdated++;
				long iterationStartTicks = Stopwatch.GetTimestamp();
				long lookupMs = 0L;
				long deferCheckMs = 0L;
				long skipCheckMs = 0L;
				long updateMs = 0L;
				string iterationOutcome = "no-modules";
				CachedBusinessModules cachedBusiness = null;
				ModulesComponent modules = null;
				try
				{
					long substepTicks = Stopwatch.GetTimestamp();
					bool deferredForCohort = ShouldDeferNonHumanBusinessModuleUpdateForCohort(business, initial, updateWork, out cachedBusiness);
					deferCheckMs = GetElapsedMilliseconds(substepTicks);
					if (deferredForCohort)
					{
						updateWork.CohortModuleUpdatesDeferred++;
						iterationOutcome = "cohort-deferred";
						continue;
					}

					substepTicks = Stopwatch.GetTimestamp();
					modules = GetCachedBusinessModules(business, out cachedBusiness);
					lookupMs = GetElapsedMilliseconds(substepTicks);
					if (modules != null)
					{
						substepTicks = Stopwatch.GetTimestamp();
						bool skippedNoop = ShouldSkipBusinessModuleUpdate(modules);
						skipCheckMs = GetElapsedMilliseconds(substepTicks);
						if (skippedNoop)
						{
							updateWork.NoopModuleUpdatesSkipped++;
							iterationOutcome = "noop-skipped";
							continue;
						}

						updateWork.ActiveModuleUpdates++;
						long moduleStartTicks = Stopwatch.GetTimestamp();
						InvokeModulesComponentDoUpdate(modules, updateWork.Now, initial);
						updateMs = GetElapsedMilliseconds(moduleStartTicks);
						if (EnableBusinessModuleFamilyProfileDiagnostics)
						{
							RecordBusinessModuleFamilyProfile(updateWork, ClassifyBusinessModuleUpdateFamily(modules), Stopwatch.GetTimestamp() - moduleStartTicks);
						}
						iterationOutcome = "updated";
						if (updateMs >= SlowBusinessModuleUpdateThresholdMs)
						{
							LogSlowBusinessModuleUpdate(business, cachedBusiness?.Building, modules, updateMs, initial);
						}
					}
				}
				catch (Exception ex)
				{
					iterationOutcome = "exception";
					LogBusinessUpdateException(ex);
				}
				finally
				{
					long iterationElapsedMs = GetElapsedMilliseconds(iterationStartTicks);
					if (iterationElapsedMs >= SlowBusinessModuleIterationThresholdMs)
					{
						LogSlowBusinessModuleIteration(business, cachedBusiness?.Building, modules, iterationElapsedMs, lookupMs, deferCheckMs, skipCheckMs, updateMs, iterationOutcome, initial);
					}
				}

				if (updateWork.NextIndex < businessesToUpdate.Count
					&& modulesUpdated > 0
					&& (GetElapsedMilliseconds(sliceTicks) >= budgetMs || GetElapsedMilliseconds(frameStartTicks) >= frameBudgetMs))
				{
					RecordBusinessModuleSlice(updateWork, sliceTicks);
					nextBusiness = updateWork.NextIndex;
					stopReason = FormatSliceStopReason(sliceTicks, budgetMs, frameStartTicks, frameBudgetMs);
					return false;
				}
			}

			if (updateWork.NextIndex < businessesToUpdate.Count)
			{
				RecordBusinessModuleSlice(updateWork, sliceTicks);
				nextBusiness = updateWork.NextIndex;
				stopReason = "max-updates";
				return false;
			}

			RecordBusinessModuleSlice(updateWork, sliceTicks);
			CompleteSlicedUpdateBusinessModules(businessWork, updateWork, initial);
			nextBusiness = totalBusinesses;
			return true;
		}

		private static void RecordBusinessModuleSlice(PendingBusinessModulesUpdateWork updateWork, long sliceStartTicks)
		{
			if (updateWork == null)
			{
				return;
			}

			updateWork.SliceCount++;
			updateWork.SliceCpuMs += GetElapsedMilliseconds(sliceStartTicks);
		}

		private static List<Entity> BuildOwnedBusinessModuleUpdateList(SessionContext context, out string source, out int allBusinessCount)
		{
			source = "none";
			allBusinessCount = 0;
			try
			{
				BusinessTracker tracker = context?.simman?.businesses;
				if (tracker == null)
				{
					return null;
				}

				if (BusinessTrackerBizCacheAllField?.GetValue(tracker) is HashSet<Entity> allBusinesses)
				{
					allBusinessCount = allBusinesses.Count;
					List<Entity> result = new List<Entity>(allBusinesses.Count);
					foreach (Entity business in allBusinesses)
					{
						if (business?.data?.biz?.owner.IsOwnerSet == true)
						{
							result.Add(business);
						}
					}

					source = "biz-cache-direct";
					return result;
				}

				IEnumerable<Entity> businesses = tracker.GetAllBizWithOwnersUnsafe();
				if (businesses == null)
				{
					return null;
				}

				List<Entity> fallback = businesses.ToList();
				allBusinessCount = fallback.Count;
				source = "enumerable-fallback";
				return fallback;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Business module update list fallback failed: " + ex.GetType().Name + ":" + ex.Message);
				return null;
			}
		}

		private static bool ShouldSkipBusinessModuleUpdate(ModulesComponent modules)
		{
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				return true;
			}

			for (int i = 0; i < slots.Count; i++)
			{
				IModule slot = slots[i];
				if (slot != null && !(slot is InventoryModule) && !(slot is ExplanationModule))
				{
					return false;
				}
			}

			return true;
		}

		private static BusinessModuleUpdateFamily ClassifyBusinessModuleUpdateFamily(ModulesComponent modules)
		{
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				return BusinessModuleUpdateFamily.Noop;
			}

			BusinessModuleUpdateFamily family = BusinessModuleUpdateFamily.Noop;
			for (int i = 0; i < slots.Count; i++)
			{
				IModule slot = slots[i];
				if (slot == null || slot is InventoryModule || slot is ExplanationModule)
				{
					continue;
				}

				BusinessModuleUpdateFamily slotFamily;
				if (slot is ManufactureModule)
				{
					slotFamily = BusinessModuleUpdateFamily.Manufacture;
				}
				else if (slot is ConsumerModule)
				{
					slotFamily = BusinessModuleUpdateFamily.Consumer;
				}
				else if (slot is VehicleModule)
				{
					slotFamily = BusinessModuleUpdateFamily.Vehicle;
				}
				else if (slot is GamblingModule)
				{
					slotFamily = BusinessModuleUpdateFamily.Gambling;
				}
				else
				{
					slotFamily = BusinessModuleUpdateFamily.Other;
				}

				if (family == BusinessModuleUpdateFamily.Noop)
				{
					family = slotFamily;
				}
				else if (family != slotFamily)
				{
					return BusinessModuleUpdateFamily.Mixed;
				}
			}

			return family;
		}

		private static void EnsureBusinessModuleComponentCacheContext(SessionContext context)
		{
			if (ReferenceEquals(_businessModuleCacheContext, context))
			{
				return;
			}

			_businessModuleCacheContext = context;
			BusinessModuleComponentCache.Clear();
		}

		private static ModulesComponent GetCachedBusinessModules(Entity business)
		{
			CachedBusinessModules _;
			return GetCachedBusinessModules(business, out _);
		}

		private static ModulesComponent GetCachedBusinessModules(Entity business, out CachedBusinessModules cachedInfo)
		{
			cachedInfo = null;
			try
			{
				if (business?.data?.biz == null)
				{
					return null;
				}

				EntityID businessId = business.Id;
				EntityID buildingId = business.data.biz.building;
				if (!businessId.IsValid || !buildingId.IsValid)
				{
					return null;
				}

				if (BusinessModuleComponentCache.TryGetValue(businessId, out CachedBusinessModules cached)
					&& cached != null
					&& cached.BuildingId == buildingId)
				{
					cachedInfo = cached;
					return cached.Modules;
				}

				Entity building = buildingId.FindEntity();
				ModulesComponent modules = building?.components?.modules;
				if (building != null)
				{
					cachedInfo = new CachedBusinessModules
					{
						BuildingId = buildingId,
						Building = building,
						Modules = modules
					};
					BusinessModuleComponentCache[businessId] = cachedInfo;
				}
				else
				{
					BusinessModuleComponentCache.Remove(businessId);
				}

				return modules;
			}
			catch
			{
				Entity building = BuildingUtil.FindBuildingForBiz(business);
				ModulesComponent modules = building?.components?.modules;
				if (modules != null)
				{
					cachedInfo = new CachedBusinessModules
					{
						BuildingId = business?.data?.biz?.building ?? EntityID.INVALID,
						Building = building,
						Modules = modules
					};
				}

				return modules;
			}
		}

		private static Entity GetCachedBusinessBuilding(Entity business, out CachedBusinessModules cachedInfo)
		{
			cachedInfo = null;
			try
			{
				if (business?.data?.biz == null)
				{
					return null;
				}

				EntityID businessId = business.Id;
				EntityID buildingId = business.data.biz.building;
				if (!businessId.IsValid || !buildingId.IsValid)
				{
					return null;
				}

				if (BusinessModuleComponentCache.TryGetValue(businessId, out CachedBusinessModules cached)
					&& cached != null
					&& cached.BuildingId == buildingId
					&& cached.Building != null)
				{
					cachedInfo = cached;
					return cached.Building;
				}

				Entity building = buildingId.FindEntity();
				if (building == null)
				{
					BusinessModuleComponentCache.Remove(businessId);
					return null;
				}

				cachedInfo = new CachedBusinessModules
				{
					BuildingId = buildingId,
					Building = building,
					Modules = building.components?.modules
				};
				BusinessModuleComponentCache[businessId] = cachedInfo;
				return building;
			}
			catch
			{
				return null;
			}
		}

		private static HashSet<EntityID> BuildHumanControlledBuildingSet(SessionContext context)
		{
			try
			{
				List<EntityID> controlledBuildings = PlayerTerritoryGetAllControlledBuildingsUnsafeMethod?.Invoke(context?.players?.Human?.territory, null) as List<EntityID>;
				if (controlledBuildings == null || controlledBuildings.Count == 0)
				{
					return null;
				}

				return new HashSet<EntityID>(controlledBuildings);
			}
			catch
			{
				return null;
			}
		}

		private static bool ShouldDeferNonHumanBusinessModuleUpdateForCohort(Entity business, bool initial, PendingBusinessModulesUpdateWork updateWork, out CachedBusinessModules cachedBusiness)
		{
			cachedBusiness = null;
			int cohorts = GetInteractiveNonHumanBusinessModuleUpdateCohorts();
			if (initial || cohorts <= 1 || business == null)
			{
				return false;
			}

			try
			{
				EntityID buildingId = business.data?.biz?.building ?? EntityID.INVALID;
				HashSet<EntityID> humanControlledBuildings = updateWork?.HumanControlledBuildings;
				if (buildingId.IsValid && humanControlledBuildings != null)
				{
					if (humanControlledBuildings.Contains(buildingId))
					{
						return false;
					}
				}
				else
				{
					Entity building = GetCachedBusinessBuilding(business, out cachedBusiness);
					PlayerID controllingPlayer = building?.data?.building?.controlled?.Get() ?? PlayerID.INVALID;
					if (controllingPlayer.IsHumanPlayer)
					{
						return false;
					}
				}

				int currentTurn = GetTurnForLog();
				if (currentTurn < 0)
				{
					return false;
				}

				int activeCohort = PositiveModulo(currentTurn, cohorts);
				int businessCohort = GetBusinessModuleUpdateCohort(business, cohorts);
				return businessCohort != activeCohort;
			}
			catch
			{
				return false;
			}
		}

		private static int GetBusinessModuleUpdateCohort(Entity business, int cohorts)
		{
			if (business == null || cohorts <= 1)
			{
				return 0;
			}

			ulong id = business.Id.id;
			int hash = (int)(id ^ (id >> 32));
			return PositiveModulo(hash, cohorts);
		}

		private static int PositiveModulo(int value, int divisor)
		{
			if (divisor <= 1)
			{
				return 0;
			}

			int result = value % divisor;
			return result < 0 ? result + divisor : result;
		}

		private static string FormatSliceStopReason(long sliceStartTicks, long sliceBudgetMs, long frameStartTicks, long frameBudgetMs)
		{
			long sliceElapsedMs = GetElapsedMilliseconds(sliceStartTicks);
			long frameElapsedMs = GetElapsedMilliseconds(frameStartTicks);
			bool hitSliceBudget = sliceElapsedMs >= sliceBudgetMs;
			bool hitFrameBudget = frameElapsedMs >= frameBudgetMs;
			if (hitSliceBudget && hitFrameBudget)
			{
				return "slice-and-frame-budget";
			}

			if (hitFrameBudget)
			{
				return "frame-budget";
			}

			if (hitSliceBudget)
			{
				return "slice-budget";
			}

			return "budget";
		}

		private static long GetInteractiveBusinessTrackerFrameBudgetMs()
		{
			return IsLargeInteractiveSave() ? LargeSaveBusinessTrackerFrameBudgetMs : InteractiveBusinessTrackerFrameBudgetMs;
		}

		private static long GetInteractiveBusinessTrackerCheapContinuationBudgetMs()
		{
			return IsLargeInteractiveSave() ? LargeSaveBusinessTrackerCheapContinuationBudgetMs : InteractiveBusinessTrackerCheapContinuationBudgetMs;
		}

		private static int GetInteractiveNonHumanBusinessModuleUpdateCohorts()
		{
			return IsLargeInteractiveSave() ? LargeSaveNonHumanBusinessModuleUpdateCohorts : InteractiveNonHumanBusinessModuleUpdateCohorts;
		}

		private static bool CanDrainLargeSaveBusinessTrackerFinalTail(PendingBusinessTrackerTurnWork businessWork, long frameStartTicks)
		{
			try
			{
				if (businessWork == null || !IsLargeInteractiveSave())
				{
					return false;
				}

				if (businessWork.NextPhaseIndex < BusinessTrackerFinalTailStartPhaseIndex || businessWork.NextPhaseIndex >= SlicedBusinessUpdatePhases.Length)
				{
					return false;
				}

				return GetElapsedMilliseconds(frameStartTicks) < LargeSaveBusinessTrackerFinalTailBudgetMs;
			}
			catch
			{
				return false;
			}
		}

		private static bool CanDrainLargeSaveBusinessTrackerPostRelationshipTail(PendingBusinessTrackerTurnWork businessWork, long frameStartTicks)
		{
			try
			{
				if (businessWork == null || !IsLargeInteractiveSave())
				{
					return false;
				}

				if (businessWork.NextPhaseIndex < BusinessTrackerPostRelationshipTailStartPhaseIndex
					|| businessWork.NextPhaseIndex >= BusinessTrackerFinalTailStartPhaseIndex)
				{
					return false;
				}

				return GetElapsedMilliseconds(frameStartTicks) < LargeSaveBusinessTrackerPostRelationshipTailBudgetMs;
			}
			catch
			{
				return false;
			}
		}

		private static long GetInteractiveBusinessModuleUpdateBudgetMs(int totalBusinesses)
		{
			return totalBusinesses >= LargeSaveBusinessCountThreshold ? LargeSaveBusinessModuleUpdateBudgetMs : InteractiveBusinessModuleUpdateBudgetMs;
		}

		private static int GetInteractiveBusinessModuleUpdatesPerFrame(int totalBusinesses)
		{
			return totalBusinesses >= LargeSaveBusinessCountThreshold ? LargeSaveBusinessModuleUpdatesPerFrame : InteractiveBusinessModuleUpdatesPerFrame;
		}

		private static int GetInteractiveRelationshipRespectPlayersPerFrame(int totalPlayers)
		{
			return ShouldUseLargeSaveRelationshipRespectBudget(totalPlayers) ? LargeSaveRelationshipRespectPlayersPerFrame : InteractiveRelationshipRespectPlayersPerFrame;
		}

		private static long GetInteractiveRelationshipRespectBudgetMs(int totalPlayers)
		{
			return ShouldUseLargeSaveRelationshipRespectBudget(totalPlayers) ? LargeSaveRelationshipRespectBudgetMs : InteractiveRelationshipRespectBudgetMs;
		}

		private static bool ShouldUseLargeSaveRelationshipRespectBudget(int totalPlayers)
		{
			return totalPlayers >= LargeSaveTotalPlayersThreshold || IsLargeInteractiveSave();
		}

		private static bool IsLargeInteractiveSave()
		{
			SessionContext context = global::Game.Game.ctx;
			if (context?.IsInteractive != true)
			{
				return false;
			}

			int day = GetDayForLog();
			int turn = GetTurnForLog();
			if (_largeSaveBudgetCacheDay == day && _largeSaveBudgetCacheTurn == turn)
			{
				return _largeSaveBudgetCacheResult;
			}

			bool result = GetTotalPlayersForLog() >= LargeSaveTotalPlayersThreshold
				|| HasLargeOwnedBusinessCount(context);
			_largeSaveBudgetCacheDay = day;
			_largeSaveBudgetCacheTurn = turn;
			_largeSaveBudgetCacheResult = result;
			return result;
		}

		private static bool HasLargeOwnedBusinessCount(SessionContext context)
		{
			try
			{
				BusinessTracker tracker = context?.simman?.businesses;
				if (tracker == null)
				{
					return false;
				}

				if (BusinessTrackerBizCacheAllField?.GetValue(tracker) is HashSet<Entity> allBusinesses)
				{
					int ownedCount = 0;
					foreach (Entity business in allBusinesses)
					{
						if (business?.data?.biz?.owner.IsOwnerSet != true)
						{
							continue;
						}

						ownedCount++;
						if (ownedCount >= LargeSaveBusinessCountThreshold)
						{
							return true;
						}
					}

					return false;
				}

				IEnumerable<Entity> businesses = tracker.GetAllBizWithOwnersUnsafe();
				if (businesses == null)
				{
					return false;
				}

				int count = 0;
				foreach (Entity _ in businesses)
				{
					count++;
					if (count >= LargeSaveBusinessCountThreshold)
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

		private static void RecordBusinessModuleFamilyProfile(PendingBusinessModulesUpdateWork updateWork, BusinessModuleUpdateFamily family, long elapsedTicks)
		{
			if (updateWork?.ModuleFamilyCounts == null || updateWork.ModuleFamilyTicks == null)
			{
				return;
			}

			int index = (int)family;
			if (index <= (int)BusinessModuleUpdateFamily.Noop || index >= BusinessModuleUpdateFamilyCount)
			{
				index = (int)BusinessModuleUpdateFamily.Other;
			}

			updateWork.ModuleFamilyCounts[index]++;
			updateWork.ModuleFamilyTicks[index] += elapsedTicks;
		}

		private static void InvokeModulesComponentDoUpdate(ModulesComponent modules, SimTime now, bool initial)
		{
			if (modules == null)
			{
				return;
			}

			ModulesComponentDoUpdateDelegate updateDelegate = GetModulesComponentDoUpdateDelegate();
			if (updateDelegate != null)
			{
				updateDelegate(modules, now, initial);
				return;
			}

			if (!_loggedModulesComponentDoUpdateDelegateFallback)
			{
				_loggedModulesComponentDoUpdateDelegateFallback = true;
				Debug.LogWarning("[GameplayTweaks] ModulesComponent.DoUpdate delegate unavailable; falling back to reflection invoke in business update slicer.");
			}

			ModulesComponentDoUpdateMethod?.Invoke(modules, new object[] { now, initial });
		}

		private static ModulesComponentDoUpdateDelegate GetModulesComponentDoUpdateDelegate()
		{
			if (_modulesComponentDoUpdateDelegateAttempted)
			{
				return _modulesComponentDoUpdateDelegate;
			}

			_modulesComponentDoUpdateDelegateAttempted = true;
			if (ModulesComponentDoUpdateMethod == null)
			{
				return null;
			}

			try
			{
				_modulesComponentDoUpdateDelegate = (ModulesComponentDoUpdateDelegate)Delegate.CreateDelegate(
					typeof(ModulesComponentDoUpdateDelegate),
					null,
					ModulesComponentDoUpdateMethod);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ModulesComponent.DoUpdate delegate creation failed: " + ex.GetType().Name + ":" + ex.Message);
			}

			return _modulesComponentDoUpdateDelegate;
		}

		private static void LogSlowBusinessModuleUpdate(Entity business, Entity building, ModulesComponent modules, long elapsedMs, bool initial)
		{
			try
			{
				int day = GetDayForLog();
				string key = day + "|" + (business?.Id.ToString() ?? "null") + "|" + (building?.Id.ToString() ?? "null") + "|" + initial;
				if (!LoggedSlowBusinessModuleUpdates.Add(key))
				{
					return;
				}

				List<IModule> slots = modules?.GetAllSlotsUnsafe();
				string moduleSample = slots == null
					? "none"
					: string.Join(",", slots
						.Where(slot => slot != null)
						.Take(8)
						.Select(slot => slot.ModuleConfig?.Id.String ?? "unknown")
						.ToArray());
				int slotCount = slots?.Count ?? 0;
				int installedCount = slots?.Count(slot => slot != null) ?? 0;
				int bizModuleCount = modules?.bizmodules?.Count ?? 0;
				string buildingTemplate = building?.config?.Template.String ?? "unknown";
				string bizTemplate = business?.config?.Template.String ?? "unknown";

				Debug.Log("[PERF][BusinessModuleSlowUpdate] ms=" +
					elapsedMs +
					" day=" +
					day +
					" initial=" +
					initial +
					" building=" +
					(building?.Id.ToString() ?? "null") +
					" business=" +
					(business?.Id.ToString() ?? "null") +
					" buildingTemplate=" +
					buildingTemplate +
					" bizTemplate=" +
					bizTemplate +
					" slots=" +
					slotCount +
					" installed=" +
					installedCount +
					" bizModules=" +
					bizModuleCount +
					" modules=" +
					moduleSample);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to log slow business module update: " + ex.Message);
			}
		}

		private static void LogSlowBusinessModuleIteration(
			Entity business,
			Entity building,
			ModulesComponent modules,
			long elapsedMs,
			long lookupMs,
			long deferCheckMs,
			long skipCheckMs,
			long updateMs,
			string outcome,
			bool initial)
		{
			try
			{
				int day = GetDayForLog();
				string key = day + "|" + (business?.Id.ToString() ?? "null") + "|" + (building?.Id.ToString() ?? "null") + "|" + initial + "|" + outcome;
				if (!LoggedSlowBusinessModuleIterations.Add(key))
				{
					return;
				}

				List<IModule> slots = modules?.GetAllSlotsUnsafe();
				string moduleSample = slots == null
					? "none"
					: string.Join(",", slots
						.Where(slot => slot != null)
						.Take(8)
						.Select(slot => slot.ModuleConfig?.Id.String ?? "unknown")
						.ToArray());
				string buildingTemplate = building?.config?.Template.String ?? "unknown";
				string bizTemplate = business?.config?.Template.String ?? "unknown";

				Debug.Log("[PERF][BusinessModuleSlowIteration] ms=" +
					elapsedMs +
					" lookupMs=" +
					lookupMs +
					" deferCheckMs=" +
					deferCheckMs +
					" skipCheckMs=" +
					skipCheckMs +
					" updateMs=" +
					updateMs +
					" outcome=" +
					(string.IsNullOrWhiteSpace(outcome) ? "unknown" : outcome) +
					" day=" +
					day +
					" initial=" +
					initial +
					" building=" +
					(building?.Id.ToString() ?? "null") +
					" business=" +
					(business?.Id.ToString() ?? "null") +
					" buildingTemplate=" +
					buildingTemplate +
					" bizTemplate=" +
					bizTemplate +
					" slots=" +
					(slots?.Count ?? 0) +
					" installed=" +
					(slots?.Count(slot => slot != null) ?? 0) +
					" bizModules=" +
					(modules?.bizmodules?.Count ?? 0) +
					" modules=" +
					moduleSample);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to log slow business module iteration: " + ex.Message);
			}
		}

		private static void InvokeDirtyCashUpdateBusinessModulesPrefix(PendingBusinessModulesUpdateWork updateWork)
		{
			if (updateWork == null)
			{
				return;
			}

			try
			{
				if (DoesAfterProhibitionEconomyOwnDirtyCashRuntimeSweepMutation())
				{
					GetAfterProhibitionEconomyMethod(ref _afterProhibitionEconomyBeginDirtyCashRuntimeSweepBusinessUpdateMethod, "BeginDirtyCashRuntimeSweepBusinessUpdate")?.Invoke(null, null);
				}
				else
				{
					DirtyCashUpdateBusinessModulesPrefixMethod?.Invoke(null, null);
				}

				updateWork.DirtyCashPrefixApplied = true;
			}
			catch (Exception ex)
			{
				LogBusinessUpdateException(ex);
			}
		}

		private static bool DoesAfterProhibitionEconomyOwnDirtyCashRuntimeSweepMutation()
		{
			try
			{
				object result = GetAfterProhibitionEconomyMethod(ref _afterProhibitionEconomyOwnsDirtyCashRuntimeSweepMutationMethod, "OwnsDirtyCashRuntimeSweepMutation")?.Invoke(null, null);
				return result is bool ownsMutation && ownsMutation;
			}
			catch
			{
				return false;
			}
		}

		private static MethodInfo GetAfterProhibitionEconomyMethod(ref MethodInfo cachedMethod, string methodName)
		{
			if (cachedMethod != null)
			{
				return cachedMethod;
			}

			Type economyType = AccessTools.TypeByName("AfterProhibitionEconomy.AfterProhibitionEconomyPlugin");
			if (economyType == null)
			{
				return null;
			}

			cachedMethod = AccessTools.Method(economyType, methodName);
			return cachedMethod;
		}

		private static void CompleteSlicedUpdateBusinessModules(
			PendingBusinessTrackerTurnWork businessWork,
			PendingBusinessModulesUpdateWork updateWork,
			bool initial)
		{
			try
			{
				if (DoesAfterProhibitionEconomyOwnDirtyCashRuntimeSweepMutation())
				{
					GetAfterProhibitionEconomyMethod(ref _afterProhibitionEconomyCompleteDirtyCashRuntimeSweepBusinessUpdateMethod, "CompleteDirtyCashRuntimeSweepBusinessUpdate")?.Invoke(null, new object[] { initial });
				}
				else if (DirtyCashUpdateBusinessModulesPostfixMethod != null)
				{
					DirtyCashUpdateBusinessModulesPostfixMethod.Invoke(null, new object[] { initial });
				}

				InvokeAfterProhibitionEconomyBusinessUpdatePostfixes(initial);
			}
			catch (Exception ex)
			{
				LogBusinessUpdateException(ex);
			}
			finally
			{
				EndSlicedUpdateBusinessModulesIfNeeded(updateWork);
				if (businessWork != null)
				{
					businessWork.BusinessModulesUpdateWork = null;
				}
			}

			long totalMs = GetElapsedMilliseconds(updateWork.StartedTicks);
			if (businessWork != null)
			{
				businessWork.BusinessModulesWallMs = totalMs;
				businessWork.BusinessModulesSliceCpuMs = updateWork.SliceCpuMs;
				businessWork.BusinessModulesSlices = updateWork.SliceCount;
			}

			if (ShouldLogSystemMaintenanceTotal(totalMs))
			{
				Debug.Log("[PERF][BusinessUpdateModulesSlice] phase=total ms=" + totalMs + " sliceCpuMs=" + updateWork.SliceCpuMs + " slices=" + updateWork.SliceCount + " businesses=" + (updateWork.Businesses?.Count ?? 0) + " active=" + updateWork.ActiveModuleUpdates + " deferredCohort=" + updateWork.CohortModuleUpdatesDeferred + " skippedNoop=" + updateWork.NoopModuleUpdatesSkipped + " profile=" + FormatBusinessModuleFamilyProfile(updateWork) + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
		}

		private static string FormatBusinessModuleFamilyProfile(PendingBusinessModulesUpdateWork updateWork)
		{
			if (updateWork == null || updateWork.ModuleFamilyCounts == null || updateWork.ModuleFamilyTicks == null)
			{
				return "disabled";
			}

			List<string> parts = new List<string>();
			for (int i = 1; i < BusinessModuleUpdateFamilyCount && i < BusinessModuleUpdateFamilyNames.Length; i++)
			{
				int count = updateWork.ModuleFamilyCounts[i];
				if (count <= 0)
				{
					continue;
				}

				long ms = updateWork.ModuleFamilyTicks[i] * 1000L / Stopwatch.Frequency;
				parts.Add(BusinessModuleUpdateFamilyNames[i] + ":" + count + "/" + ms + "ms");
			}

			if (parts.Count == 0)
			{
				parts.Add("active:" + updateWork.ActiveModuleUpdates);
			}

			parts.Add("cohortDeferred:" + updateWork.CohortModuleUpdatesDeferred);
			parts.Add("noop:" + updateWork.NoopModuleUpdatesSkipped);
			return string.Join(",", parts.ToArray());
		}

		private static bool ContinueSlicedUpdateRespectFromRelationships(
			PendingBusinessTrackerTurnWork businessWork,
			long frameStartTicks,
			long frameBudgetMs,
			out int playersUpdated,
			out int nextPlayer,
			out int totalPlayers,
			out string stopReason)
		{
			playersUpdated = 0;
			nextPlayer = -1;
			totalPlayers = 0;
			stopReason = "complete";
			if (businessWork == null)
			{
				return true;
			}

			SessionContext context = global::Game.Game.ctx;
			PendingRelationshipRespectUpdateWork updateWork = businessWork.RelationshipRespectUpdateWork;
			if (updateWork != null && !ReferenceEquals(updateWork.Context, context))
			{
				updateWork = null;
				businessWork.RelationshipRespectUpdateWork = null;
			}

			if (updateWork == null)
			{
				IEnumerable<PlayerInfo> players = context?.players?.all;
				if (players == null)
				{
					return true;
				}

				updateWork = new PendingRelationshipRespectUpdateWork
				{
					Context = context,
					Players = players.ToList(),
					NextIndex = 0,
					StartedTicks = Stopwatch.GetTimestamp()
				};
				businessWork.RelationshipRespectUpdateWork = updateWork;
			}

			List<PlayerInfo> playersToUpdate = updateWork.Players;
			totalPlayers = playersToUpdate?.Count ?? 0;
			if (playersToUpdate == null || playersToUpdate.Count == 0)
			{
				CompleteSlicedRelationshipRespect(businessWork, updateWork);
				nextPlayer = totalPlayers;
				return true;
			}

			long sliceTicks = Stopwatch.GetTimestamp();
			int maxPlayers = Math.Max(1, GetInteractiveRelationshipRespectPlayersPerFrame(totalPlayers));
			long budgetMs = Math.Max(1L, GetInteractiveRelationshipRespectBudgetMs(totalPlayers));
			while (updateWork.NextIndex < playersToUpdate.Count && playersUpdated < maxPlayers)
			{
				PlayerInfo player = playersToUpdate[updateWork.NextIndex++];
				playersUpdated++;
				try
				{
					ApplyRelationshipRespectForPlayer(player, updateWork.AverageRelsPerNode);
				}
				catch (Exception ex)
				{
					LogBusinessUpdateException(ex);
				}
				finally
				{
					updateWork.AverageRelsPerNode.Clear();
				}

				if (updateWork.NextIndex < playersToUpdate.Count
					&& playersUpdated > 0
					&& (GetElapsedMilliseconds(sliceTicks) >= budgetMs
						|| (GetElapsedMilliseconds(frameStartTicks) >= frameBudgetMs
							&& !CanDrainLargeSaveRelationshipRespectSlice(businessWork, totalPlayers, frameStartTicks))))
				{
					RecordRelationshipRespectSlice(updateWork, sliceTicks);
					nextPlayer = updateWork.NextIndex;
					stopReason = FormatSliceStopReason(sliceTicks, budgetMs, frameStartTicks, frameBudgetMs);
					return false;
				}
			}

			if (updateWork.NextIndex < playersToUpdate.Count)
			{
				RecordRelationshipRespectSlice(updateWork, sliceTicks);
				nextPlayer = updateWork.NextIndex;
				stopReason = "max-players";
				return false;
			}

			RecordRelationshipRespectSlice(updateWork, sliceTicks);
			CompleteSlicedRelationshipRespect(businessWork, updateWork);
			nextPlayer = totalPlayers;
			return true;
		}

		private static bool CanDrainLargeSaveRelationshipRespectSlice(
			PendingBusinessTrackerTurnWork businessWork,
			int totalPlayers,
			long frameStartTicks)
		{
			try
			{
				if (businessWork == null || !IsLargeInteractiveSave())
				{
					return false;
				}

				if (businessWork.NextPhaseIndex != BusinessTrackerRelationshipRespectPhaseIndex)
				{
					return false;
				}

				if (totalPlayers > InteractiveRelationshipRespectPlayersPerFrame)
				{
					return false;
				}

				return GetElapsedMilliseconds(frameStartTicks) < LargeSaveBusinessTrackerPostRelationshipTailBudgetMs;
			}
			catch
			{
				return false;
			}
		}

		private static void RecordRelationshipRespectSlice(PendingRelationshipRespectUpdateWork updateWork, long sliceStartTicks)
		{
			if (updateWork == null)
			{
				return;
			}

			updateWork.SliceCount++;
			updateWork.SliceCpuMs += GetElapsedMilliseconds(sliceStartTicks);
		}

		private static void ApplyRelationshipRespectForPlayer(PlayerInfo player, List<(NodeID nid, Fixnum val)> averageRelsPerNode)
		{
			if (player == null || averageRelsPerNode == null)
			{
				return;
			}

			player.social.ProduceAverageRelPerNode(averageRelsPerNode);
			if (averageRelsPerNode.Count == 0)
			{
				return;
			}

			Fixnum multiplier = global::Game.Game.serv.globals.settings.people.social.respect.bizRelationshipMultiplier.Evaluate(player.PID);
			foreach (var (nodeId, value) in averageRelsPerNode)
			{
				if (!value.IsNotZero)
				{
					continue;
				}

				Node node = nodeId.FindNode();
				if (node != null)
				{
					node.respect.IncrementBizRespect(player.PID, value * multiplier);
				}
			}
		}

		private static void CompleteSlicedRelationshipRespect(
			PendingBusinessTrackerTurnWork businessWork,
			PendingRelationshipRespectUpdateWork updateWork)
		{
			if (businessWork != null)
			{
				businessWork.RelationshipRespectUpdateWork = null;
			}

			long totalMs = GetElapsedMilliseconds(updateWork.StartedTicks);
			if (businessWork != null)
			{
				businessWork.RelationshipRespectWallMs = totalMs;
				businessWork.RelationshipRespectSliceCpuMs = updateWork.SliceCpuMs;
				businessWork.RelationshipRespectSlices = updateWork.SliceCount;
			}

			if (ShouldLogSystemMaintenanceTotal(totalMs))
			{
				Debug.Log("[PERF][RelationshipRespectSlice] phase=total ms=" + totalMs + " sliceCpuMs=" + updateWork.SliceCpuMs + " slices=" + updateWork.SliceCount + " players=" + (updateWork.Players?.Count ?? 0) + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
		}

		private static void InvokeAfterProhibitionEconomyBusinessUpdatePostfixes(bool initial)
		{
			string source = initial ? "business-update-initial" : "business-update";
			try
			{
				AfterProhibitionPurchaseStockRunMethod?.Invoke(null, new object[] { source, false });
			}
			catch (Exception ex)
			{
				LogBusinessUpdateException(ex);
			}

			try
			{
				AfterProhibitionEmptyBusinessRepairRunMethod?.Invoke(null, new object[] { source, false });
			}
			catch (Exception ex)
			{
				LogBusinessUpdateException(ex);
			}
		}

		private static void EndSlicedUpdateBusinessModulesIfNeeded(PendingBusinessModulesUpdateWork updateWork)
		{
			if (updateWork?.DirtyCashPrefixApplied != true)
			{
				return;
			}

			try
			{
				if (DoesAfterProhibitionEconomyOwnDirtyCashRuntimeSweepMutation())
				{
					GetAfterProhibitionEconomyMethod(ref _afterProhibitionEconomyEndDirtyCashRuntimeSweepBusinessUpdateMethod, "EndDirtyCashRuntimeSweepBusinessUpdate")?.Invoke(null, null);
				}
				else
				{
					DirtyCashUpdateBusinessModulesFinalizerMethod?.Invoke(null, new object[] { null });
				}
			}
			catch (Exception ex)
			{
				LogBusinessUpdateException(ex);
			}
			finally
			{
				updateWork.DirtyCashPrefixApplied = false;
			}
		}

		private static void InvokeBusinessUpdatePhase(BusinessUpdatePhase phase, bool initial)
		{
			if (phase.Method == null)
			{
				return;
			}

			try
			{
				object[] args = phase.TakesInitial ? new object[] { initial } : null;
				phase.Method.Invoke(null, args);
			}
			catch (TargetInvocationException ex)
			{
				LogBusinessUpdateException(ex.InnerException ?? ex);
			}
			catch (Exception ex)
			{
				LogBusinessUpdateException(ex);
			}
		}

		private static void LogBusinessUpdateException(Exception ex)
		{
			try
			{
				global::Game.Game.serv?.stats?.LogException(ex);
			}
			catch
			{
				Debug.LogWarning("[GameplayTweaks] BusinessTracker sliced turn phase failed: " + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void LogBusinessTrackerPhase(string phase, long startTicks)
		{
			long elapsedMs = GetElapsedMilliseconds(startTicks);
			if (elapsedMs >= GetTurnHandlerDetailLogThresholdMs())
			{
				Debug.Log("[PERF][BusinessTrackerPhase] phase=" + phase + " ms=" + elapsedMs + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
		}

		private static void EndBusinessUpdateScopeIfNeeded(PendingBusinessTrackerTurnWork businessWork)
		{
			EndSlicedUpdateBusinessModulesIfNeeded(businessWork?.BusinessModulesUpdateWork);
			if (businessWork != null)
			{
				businessWork.BusinessModulesUpdateWork = null;
				businessWork.RelationshipRespectUpdateWork = null;
			}

			if (businessWork?.BusinessUpdateScopeStarted == true)
			{
				TurnPerformanceOptimizationsPatch.EndBusinessUpdateTickScope();
				businessWork.BusinessUpdateScopeStarted = false;
			}
		}

		private static void LogTurnHandlerDetail(string phase, object target, long startTicks)
		{
			try
			{
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs < GetTurnHandlerDetailLogThresholdMs())
				{
					return;
				}

				string handler = target?.GetType().Name ?? "unknown";
				Debug.Log("[PERF][TurnDetail] phase=" + phase + " handler=" + handler + " ms=" + elapsedMs + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " pid=" + GetPlayerForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
			}
		}

		private static int PatchAdvisorTurnMethods(Harmony harmony)
		{
			int patched = 0;
			foreach (string typeName in AdvisorTurnTypeNames)
			{
				Type type = AccessTools.TypeByName(typeName);
				if (type == null)
				{
					continue;
				}

				foreach (string methodName in AdvisorTurnMethodNames)
				{
					MethodInfo method = FindInstanceMethodNoWarn(type, methodName);
					if (method == null || method.DeclaringType == typeof(AIAdvisor))
					{
						continue;
					}

					PatchTimedMethod(harmony, method, AdvisorTurnSlowStageThresholdMs);
					patched++;
				}
			}

			return patched;
		}

		private static int PatchPoliticsDetailMethods(Harmony harmony)
		{
			int patched = 0;
			string[] wardMethodNames =
			{
				"OnSystemTurn",
				"StartElection",
				"EndElection",
				"GetTotalVotersInWard"
			};

			foreach (string methodName in wardMethodNames)
			{
				MethodInfo method = AccessTools.Method(typeof(Ward), methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, PoliticsDetailThresholdMs);
				patched++;
			}

			string[] electionMethodNames =
			{
				"TickElection",
				"TickTryAdvanceStage",
				"TickUpdateModel",
				"TickDisplayModel",
				"SpinUpStartingElectionState",
				"TryAICampaignActions",
				"StoreElectionResultOnElectionDay",
				"OnElectionComplete",
				"PredictWinnerByPolling",
				"PredictWinnerGivenModel"
			};

			foreach (string methodName in electionMethodNames)
			{
				MethodInfo method = AccessTools.Method(typeof(Election), methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, PoliticsDetailThresholdMs);
				patched++;
			}

			string[] politicsManagerMethodNames =
			{
				"TryRevokeTempLaws",
				"PostReportedTickers",
				"ShouldShowLegislativeSessionTicker",
				"GetElectionStage"
			};

			foreach (string methodName in politicsManagerMethodNames)
			{
				MethodInfo method = AccessTools.Method(typeof(PoliticsManager), methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, PoliticsDetailThresholdMs);
				patched++;
			}

			return patched;
		}

		private static int PatchPlayerAIStartDetailMethods(Harmony harmony)
		{
			Type playerAIType = AccessTools.TypeByName("Game.Session.Player.AI.PlayerAI");
			if (playerAIType == null)
			{
				return 0;
			}

			int patched = 0;
			foreach (string methodName in PlayerAIStartDetailMethodNames)
			{
				MethodInfo method = FindInstanceMethodNoWarn(playerAIType, methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, PlayerAIStartDetailThresholdMs);
				patched++;
			}

			return patched;
		}

		private static int PatchTerritoryAdvisorDetailMethods(Harmony harmony)
		{
			int patched = 0;
			Type territoryAdvisorType = AccessTools.TypeByName("Game.Session.Player.AI.TerritoryAdvisor");
			if (territoryAdvisorType != null)
			{
				foreach (string methodName in TerritoryAdvisorDetailMethodNames)
				{
					MethodInfo method = FindInstanceMethodNoWarn(territoryAdvisorType, methodName);
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(harmony, method, TerritoryAdvisorDetailThresholdMs);
					patched++;
				}
			}

			Type territoryPotentialsType = AccessTools.TypeByName("Game.Session.Player.AI.TerritoryPotentials");
			if (territoryPotentialsType != null)
			{
				foreach (string methodName in TerritoryPotentialDetailMethodNames)
				{
					MethodInfo method = FindInstanceMethodNoWarn(territoryPotentialsType, methodName);
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(harmony, method, TerritoryAdvisorDetailThresholdMs);
					patched++;
				}
			}

			return patched;
		}

		private static int PatchTakeoverDetailMethods(Harmony harmony)
		{
			int patched = 0;
			foreach (string methodName in BusinessAdvisorTakeoverDetailMethodNames)
			{
				MethodInfo method = FindInstanceMethodNoWarn(typeof(BusinessAdvisor), methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, TakeoverDetailThresholdMs);
				patched++;
			}

			Type buildingCallbackType = AccessTools.TypeByName("Game.Session.Player.Commands.AICommandBuildingCallback");
			if (buildingCallbackType != null)
			{
				MethodInfo performTurnActions = FindInstanceMethodNoWarn(buildingCallbackType, "PerformTurnActions");
				if (performTurnActions != null)
				{
					PatchTimedMethod(harmony, performTurnActions, TakeoverDetailThresholdMs);
					patched++;
				}
			}

			foreach (string methodName in PlayerTerritoryTakeoverDetailMethodNames)
			{
				MethodInfo method = FindInstanceMethodNoWarn(typeof(PlayerTerritory), methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo clearOwner = AccessTools.Method(typeof(BusinessTracker), "ClearOwner", new[] { typeof(Entity), typeof(bool) });
			if (clearOwner != null)
			{
				PatchTimedMethod(harmony, clearOwner, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo forceAssignOwner = AccessTools.Method(typeof(BusinessTracker), "ForceAssignOwner", new[] { typeof(Entity), typeof(Entity) });
			if (forceAssignOwner != null)
			{
				PatchTimedMethod(harmony, forceAssignOwner, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo recomputeRespect = AccessTools.Method(typeof(HeatAndRespect), "RecomputeRespect", new[] { typeof(Node), typeof(bool) });
			if (recomputeRespect != null)
			{
				PatchTimedMethod(harmony, recomputeRespect, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo removeBackroomModules = AccessTools.Method(typeof(ModulesComponent), "RemoveBackroomModules");
			if (removeBackroomModules != null)
			{
				PatchTimedMethod(harmony, removeBackroomModules, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo setEnabledOn = AccessTools.Method(typeof(ModulesComponent), "SetEnabledOn", new[] { typeof(SimTime) });
			if (setEnabledOn != null)
			{
				PatchTimedMethod(harmony, setEnabledOn, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo stopPayingTribute = AccessTools.Method(typeof(PlayerOutposts), "StopPayingTributeIfPaying", new[] { typeof(Entity), typeof(Entity), typeof(EntityID) });
			if (stopPayingTribute != null)
			{
				PatchTimedMethod(harmony, stopPayingTribute, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo canBecomeGamblingHouse = AccessTools.Method(typeof(PlayerGambling), "CanBecomeGamblingHouse", new[] { typeof(Entity) });
			if (canBecomeGamblingHouse != null)
			{
				PatchTimedMethod(harmony, canBecomeGamblingHouse, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo installGamblingModule = AccessTools.Method(typeof(PlayerGambling), "InstallGamblingModule", new[] { typeof(Entity), typeof(Label) });
			if (installGamblingModule != null)
			{
				PatchTimedMethod(harmony, installGamblingModule, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo scopeOutAndTakeOverResidence = AccessTools.Method(typeof(PlayerTerritory), "ScopeOutAndTakeOverResidence", new[] { typeof(Entity) });
			if (scopeOutAndTakeOverResidence != null)
			{
				PatchTimedMethod(harmony, scopeOutAndTakeOverResidence, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo scopeOutBuildingWithFeedback = AccessTools.Method(typeof(PlayerTerritory), "ScopeOutBuildingWithFeedback", new[] { typeof(Entity), typeof(EntityID) });
			if (scopeOutBuildingWithFeedback != null)
			{
				PatchTimedMethod(harmony, scopeOutBuildingWithFeedback, TakeoverDetailThresholdMs);
				patched++;
			}

			Type tickerBarType = AccessTools.TypeByName("Game.UI.Session.Tickers.TickerBar");
			MethodInfo addTickerScopeOut = AccessTools.Method(tickerBarType, "AddTickerScopeOut", new[] { typeof(EntityID) });
			if (addTickerScopeOut != null)
			{
				PatchTimedMethod(harmony, addTickerScopeOut, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo setAiCasinoManager = AccessTools.Method(typeof(ResidenceComponent), "SetAICasinoManager", new[] { typeof(Entity) });
			if (setAiCasinoManager != null)
			{
				PatchTimedMethod(harmony, setAiCasinoManager, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo installSmallInventory = AccessTools.Method(typeof(ModulesComponent), "InstallSmallInventory");
			if (installSmallInventory != null)
			{
				PatchTimedMethod(harmony, installSmallInventory, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo installModuleManually = AccessTools.Method(typeof(ModulesComponent), "InstallModuleManually", new[] { typeof(Label), typeof(SimTime) });
			if (installModuleManually != null)
			{
				PatchTimedMethod(harmony, installModuleManually, TakeoverDetailThresholdMs);
				patched++;
			}

			MethodInfo sendImmediate = AccessTools.Method(typeof(SessionEventBus), "SendImmediate", new[] { typeof(SessionEvent) });
			if (sendImmediate != null)
			{
				harmony.Patch(
					sendImmediate,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(SessionEventSendImmediatePrefix))
					{
						priority = Priority.First
					},
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(SessionEventSendImmediatePostfix))
					{
						priority = Priority.Last
					});
				patched++;
			}

			foreach (Tuple<string, string> target in new[]
			{
				Tuple.Create("Game.Session.Quests.GoalHaveModule", "CountModules"),
				Tuple.Create("Game.Session.Quests.GoalHaveModule", "OnBuildingConstruction"),
				Tuple.Create("Game.Session.Quests.GoalHaveBuildings", "GetBuildingCount"),
				Tuple.Create("Game.Session.Quests.GoalHaveBuildings", "OnBuildingChanged"),
				Tuple.Create("Game.UI.Session.Crew.CrewDialog", "OnPlayerCrewAll"),
				Tuple.Create("Game.UI.Session.Crew.CrewDialog", "RecreateAllCards"),
				Tuple.Create("Game.UI.Session.Picks.PickManager", "OnBuildingChanged"),
				Tuple.Create("Game.UI.Session.Picks.PickManager", "RefreshVisibleSummaryPicks")
			})
			{
				Type type = AccessTools.TypeByName(target.Item1);
				MethodInfo method = FindInstanceMethodNoWarn(type, target.Item2);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method, TakeoverDetailThresholdMs);
				patched++;
			}

				MethodInfo crewDialogOnPlayerCrewAll = FindInstanceMethodNoWarn(typeof(CrewDialog), "OnPlayerCrewAll");
				if (crewDialogOnPlayerCrewAll != null)
				{
					harmony.Patch(
						crewDialogOnPlayerCrewAll,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CrewDialogOnPlayerCrewAllDeferredPrefix))
						{
							priority = Priority.First
						});
					patched++;
				}

				MethodInfo crewDialogOnCurrentActiveChanged = FindInstanceMethodNoWarn(typeof(CrewDialog), "OnCurrentActiveChanged");
				if (crewDialogOnCurrentActiveChanged != null)
				{
					harmony.Patch(
						crewDialogOnCurrentActiveChanged,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CrewDialogOnCurrentActiveChangedDeferredPrefix))
						{
							priority = Priority.First
						});
					patched++;
				}

				MethodInfo pickAddOrRefresh = AccessTools.Method(typeof(PickContainer), nameof(PickContainer.AddOrRefreshPick), new[] { typeof(PickTarget), typeof(bool) });
				if (pickAddOrRefresh != null)
				{
					harmony.Patch(
						pickAddOrRefresh,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(PickContainerAddOrRefreshPickPrefix))
					{
						priority = Priority.First
					});
					patched++;
				}

				MethodInfo pickManagerOnBuildingChanged = FindInstanceMethodNoWarn(typeof(PickManager), "OnBuildingChanged");
				if (pickManagerOnBuildingChanged != null)
				{
					harmony.Patch(
						pickManagerOnBuildingChanged,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(PickManagerOnBuildingChangedPrefix))
						{
							priority = Priority.First
						});
					patched++;
				}

				Type createPlayersType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayers");
			MethodInfo cheatGenerateCrewForPlayer = createPlayersType == null ? null : AccessTools.Method(createPlayersType, "CheatGenerateCrewForPlayer", new[] { typeof(PlayerInfo) });
			if (cheatGenerateCrewForPlayer != null)
			{
				harmony.Patch(cheatGenerateCrewForPlayer, prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CheatGenerateCrewForPlayerFastPrefix))
				{
					priority = Priority.Last
				});
				PatchTimedMethod(harmony, cheatGenerateCrewForPlayer, TakeoverDetailThresholdMs);
				patched++;
			}

			Type potentialCacheType = AccessTools.TypeByName("Game.Session.Player.PlayerPotentialCache");
			if (potentialCacheType != null)
			{
				MethodInfo onTakeover = FindInstanceMethodNoWarn(potentialCacheType, "OnTakeover");
				if (onTakeover != null)
				{
					PatchTimedMethod(harmony, onTakeover, TakeoverDetailThresholdMs);
					patched++;
				}
			}

			return patched;
		}

		internal static void ClearRuntimeCheatPlayerSetupCache()
		{
			RuntimeCheatGeneratedPeeps.Clear();
			RuntimeCheatGeneratedEthnicities.Clear();
			RuntimeCheatPoliticianIds.Clear();
			_runtimeCheatGeneratedDay = int.MinValue;
			_runtimeCheatGeneratedTurn = int.MinValue;
		}

		private static bool CheatGenerateCrewForPlayerFastPrefix(PlayerInfo player, ref Entity __result)
		{
			try
			{
				if (!ShouldUseRuntimeCheatPlayerFastSelector(player))
				{
					return true;
				}

				long startTicks = Stopwatch.GetTimestamp();
				Entity peep;
				int scanned;
				int eligible;
				if (!TryFindRuntimeCheatCrewCandidate(player, out peep, out scanned, out eligible))
				{
					return true;
				}

				__result = peep;
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs >= TakeoverDetailThresholdMs)
				{
					Debug.Log("[PERF][CheatGenerateCrewForPlayerFast] ms=" + elapsedMs + " pid=" + player.PID.id + " peep=" + peep.Id.id + " scanned=" + scanned + " eligible=" + eligible + " used=" + RuntimeCheatGeneratedPeeps.Count + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}

				return false;
			}
			catch (Exception ex)
			{
				ClearRuntimeCheatPlayerSetupCache();
				Debug.LogWarning("[GameplayTweaks] Runtime cheat crew fast selector failed: " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static bool ShouldUseRuntimeCheatPlayerFastSelector(PlayerInfo player)
		{
			try
			{
				return player != null
					&& player.PID.IsValid
					&& !player.PID.IsHumanPlayer
					&& (global::Game.Game.ctx?.IsInteractive ?? false)
					&& !(global::Game.Game.ctx?.IsCityGen ?? false);
			}
			catch
			{
				return false;
			}
		}

		private static bool TryFindRuntimeCheatCrewCandidate(PlayerInfo player, out Entity peep, out int scanned, out int eligible)
		{
			peep = null;
			scanned = 0;
			eligible = 0;
			int day = GetDayForLog();
			int turn = GetTurnForLog();
			if (_runtimeCheatGeneratedDay != day || _runtimeCheatGeneratedTurn != turn)
			{
				RuntimeCheatGeneratedPeeps.Clear();
				RuntimeCheatGeneratedEthnicities.Clear();
				RebuildRuntimeCheatPoliticianIds();
				_runtimeCheatGeneratedDay = day;
				_runtimeCheatGeneratedTurn = turn;
			}

			if (global::Game.Game.ctx?.simman?.peoplegen == null)
			{
				return false;
			}

			SimTime now = global::Game.Game.ctx.clock.Now;
			SplitMix64 rng = new SplitMix64((uint)player.PID.id);
			float bestScore = float.MinValue;
			FamilyTree bestFamily = null;
			foreach (Entity candidate in global::Game.Game.ctx.simman.peoplegen.GetAllTrackedPeople())
			{
				scanned++;
				if (candidate == null || !candidate.Id.IsValid || RuntimeCheatGeneratedPeeps.Contains(candidate.Id.id))
				{
					continue;
				}

				if (!IsRuntimeCheatEligibleCrewMemberFast(now, candidate))
				{
					continue;
				}

				FamilyTree family = global::Game.Game.ctx.simman.peoplegen.data.FindFamilyTree(candidate.data.person.famId);
				if (family == null)
				{
					continue;
				}

				eligible++;
				float baseScore = player.IsJustGoon
					? (family.anchor.ethscore == 0f ? 0f : 1f)
					: (RuntimeCheatGeneratedEthnicities.Contains(family.eth) ? 0f : family.anchor.ethscore);
				float score = baseScore + rng.Generate(0f, 0.1f);
				if (score <= bestScore)
				{
					continue;
				}

				bestScore = score;
				peep = candidate;
				bestFamily = family;
			}

			if (peep == null)
			{
				return false;
			}

			RuntimeCheatGeneratedPeeps.Add(peep.Id.id);
			if (bestFamily != null)
			{
				RuntimeCheatGeneratedEthnicities.Add(bestFamily.eth);
			}

			return true;
		}

		private static void RebuildRuntimeCheatPoliticianIds()
		{
			RuntimeCheatPoliticianIds.Clear();
			PoliticsManager politics = global::Game.Game.ctx?.simman?.politics;
			if (politics == null || PoliticsManagerPersistedDataField == null)
			{
				return;
			}

			PoliticsManagerPersistedData data = PoliticsManagerPersistedDataField.GetValue(politics) as PoliticsManagerPersistedData;
			List<PoliticianData> politicians = data?.politicians;
			if (politicians == null)
			{
				return;
			}

			for (int i = 0; i < politicians.Count; i++)
			{
				EntityID id = politicians[i]?.id ?? EntityID.INVALID;
				if (id.IsValid)
				{
					RuntimeCheatPoliticianIds.Add(id.id);
				}
			}
		}

		private static bool IsRuntimeCheatEligibleCrewMemberFast(SimTime now, Entity person)
		{
			if (person == null || !person.Id.IsValid || RuntimeCheatPoliticianIds.Contains(person.Id.id))
			{
				return false;
			}

			PersonData personData = person.data.person;
			return personData.IsAlive
				&& personData.GetAge(now).YearsFloat >= 20f
				&& personData.business.IsNotValid
				&& personData.resassigned.IsNotValid
				&& person.data.agent.pid.id == 0;
		}

		private static int PatchPlayerAIAssignRequestDetailMethods(Harmony harmony)
		{
			Type playerAIType = AccessTools.TypeByName("Game.Session.Player.AI.PlayerAI");
			if (playerAIType == null)
			{
				return 0;
			}

			int patched = 0;
			MethodInfo assignRequests = FindInstanceMethodNoWarn(playerAIType, "AssignRequestsToAvailableCrew");
			if (assignRequests != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					assignRequests,
					nameof(PlayerAIAssignRequestsDetailPrefix),
					nameof(PlayerAIAssignRequestsDetailPostfix));
			}

			MethodInfo tryDispatchToAssigned = FindInstanceMethodNoWarn(playerAIType, "TryDispatchToAssigned");
			if (tryDispatchToAssigned != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					tryDispatchToAssigned,
					nameof(PlayerAIDispatchDetailPrefix),
					nameof(PlayerAIDispatchDetailPostfix));
			}

			MethodInfo tryDispatchToAvailable = FindInstanceMethodNoWarn(playerAIType, "TryDispatchToAvailable");
			if (tryDispatchToAvailable != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					tryDispatchToAvailable,
					nameof(PlayerAIDispatchDetailPrefix),
					nameof(PlayerAIDispatchDetailPostfix));
			}

			patched += PatchPlayerAIRequestCompletionDetailMethods(harmony);

			MethodInfo logRequest = AccessTools.Method(typeof(AILog), "LogRequest", new[] { typeof(bool), typeof(PlayerID), typeof(AdvisorRequest), typeof(string) });
			if (logRequest != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					logRequest,
					nameof(PlayerAILogRequestDetailPrefix),
					nameof(PlayerAILogRequestDetailPostfix));
			}

			MethodInfo runScript = AccessTools.Method(typeof(ScriptDispatcher), "RunScript", new[] { typeof(Label), typeof(PlayerID), typeof(Entity), typeof(Deictics) });
			if (runScript != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					runScript,
					nameof(ScriptDispatcherDetailPrefix),
					nameof(ScriptDispatcherDetailPostfix));
			}

			MethodInfo validate = AccessTools.Method(typeof(Deictics), "Validate", new[] { typeof(IEnumerable<ScriptStep>) });
			if (validate != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					validate,
					nameof(ScriptValidateDetailPrefix),
					nameof(ScriptValidateDetailPostfix));
			}

			MethodInfo stepToCommand = AccessTools.Method(typeof(ScriptDispatcher), "StepToCommand", new[] { typeof(ScriptStep), typeof(Deictics), typeof(PlayerID), typeof(EntityID) });
			if (stepToCommand != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					stepToCommand,
					nameof(ScriptStepToCommandDetailPrefix),
					nameof(ScriptStepToCommandDetailPostfix));
			}

			MethodInfo addCommand = AccessTools.Method(typeof(CommandExecutor), "AddCommand", new[] { typeof(Command) });
			if (addCommand != null)
			{
				patched += TryPatchPlayerAIAssignDetailMethod(
					harmony,
					addCommand,
					nameof(ScriptAddCommandDetailPrefix),
					nameof(ScriptAddCommandDetailPostfix));
			}

			return patched;
		}

		private static int PatchPlayerAIRequestCompletionDetailMethods(Harmony harmony)
		{
			try
			{
				int patched = 0;
				HashSet<MethodInfo> methods = new HashSet<MethodInfo>();
				Type advisorBaseType = typeof(AIAdvisor);
				Type[] types = advisorBaseType.Assembly.GetTypes();
				for (int i = 0; i < types.Length; i++)
				{
					Type type = types[i];
					if (type == null || !advisorBaseType.IsAssignableFrom(type))
					{
						continue;
					}

					MethodInfo method = type.GetMethod(
						"OnRequestDispatched",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
					if (method == null || method.IsAbstract || !methods.Add(method))
					{
						continue;
					}

					patched += TryPatchPlayerAIAssignDetailMethod(
						harmony,
						method,
						nameof(PlayerAIRequestCompletionDetailPrefix),
						nameof(PlayerAIRequestCompletionDetailPostfix));
				}

				return patched;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] PlayerAI request completion detail patches skipped: " + ex.GetType().Name + ":" + ex.Message);
				return 0;
			}
		}

		private static int PatchManufactureModuleDetailMethods(Harmony harmony)
		{
			MethodInfo doUpdate = AccessTools.Method(typeof(ManufactureModule), "DoUpdate");
			if (doUpdate == null)
			{
				return 0;
			}

			try
			{
				int patched = 0;
				harmony.Patch(
					doUpdate,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ManufactureModuleDetailPrefix)),
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ManufactureModuleDetailPostfix)));
				patched++;

				MethodInfo doSellOffInventory = AccessTools.Method(typeof(ManufactureModule), "DoSellOffInventory", new[] { typeof(ModuleQuery), typeof(InventoryModule) });
				if (doSellOffInventory != null)
				{
					harmony.Patch(
						doSellOffInventory,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ManufactureDoSellOffInventoryPrefix))
						{
							priority = Priority.First
						});
					patched++;
				}

				MethodInfo doRefillInventory = AccessTools.Method(typeof(ManufactureModule), "DoRefillInventory", new[] { typeof(ModuleQuery), typeof(InventoryModule) });
				if (doRefillInventory != null)
				{
					harmony.Patch(
						doRefillInventory,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ManufactureDoRefillInventoryPrefix))
						{
							priority = Priority.First
						});
					patched++;
				}

				return patched;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ManufactureModule detail patch skipped: " + ex.GetType().Name + ":" + ex.Message);
				return 0;
			}
		}

		private static int TryPatchPlayerAIAssignDetailMethod(Harmony harmony, MethodInfo target, string prefixName, string postfixName)
		{
			try
			{
				harmony.Patch(
					target,
					prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), prefixName),
					postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), postfixName));
				return 1;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] PlayerAI request assignment detail patch skipped target=" + FormatMethodForLog(target) + " error=" + ex.GetType().Name + ":" + ex.Message);
				return 0;
			}
		}

		private static int PatchStartupStageMethods(Harmony harmony)
		{
			int patched = 0;
			StartupStageMethods.Clear();
			foreach ((string typeName, string methodName) in StartupStageMethodNames)
			{
				Type type = AccessTools.TypeByName(typeName);
				MethodInfo method = FindInstanceMethodNoWarn(type, methodName) ?? AccessTools.Method(type, methodName);
				if (method == null)
				{
					continue;
				}

				StartupStageMethods.Add(method);
				PatchTimedMethod(harmony, method);
				patched++;
			}

			return patched;
		}

		private static int PatchModuleLookupMethods(Harmony harmony)
		{
			int patched = 0;
			MethodInfo findUpgrades = AccessTools.Method(typeof(ModulesUtil), "FindUpgradesOrNull");
			if (findUpgrades != null)
			{
				PatchTimedMethod(harmony, findUpgrades);
				patched++;
			}

			MethodInfo findAllDefs = AccessTools.Method(typeof(ModulesUtil), "FindAllModuleDefsExpensive");
			if (findAllDefs != null)
			{
				PatchTimedMethod(harmony, findAllDefs);
				patched++;
			}

			return patched;
		}

		private static int PatchExternalDirtyCashMethods(Harmony harmony)
		{
			int patched = 0;
			Type legalBusinessType = AccessTools.TypeByName("DirtyCashEconomy.LegalBusinessPatches");
			foreach (string methodName in new[] { "GetPlayerLegalModules", "ShouldShowModule", "IsFrontRoomModule" })
			{
				MethodInfo method = AccessTools.Method(legalBusinessType, methodName);
				if (method == null)
				{
					continue;
				}

				PatchTimedMethod(harmony, method);
				patched++;
			}

			Type findUpgradesPatchType = AccessTools.TypeByName("DirtyCashEconomy.FindUpgradesOrNullPatch");
			MethodInfo postfix = AccessTools.Method(findUpgradesPatchType, "Postfix");
			if (postfix != null)
			{
				PatchTimedMethod(harmony, postfix);
				patched++;
			}

			return patched;
		}

		private static int PatchSocialActionDetailMethods(Harmony harmony)
		{
			int patched = 0;

			try
			{
				foreach (MethodInfo method in typeof(PlayerSocial).GetMethods(InstanceMethodFlags))
				{
					if (!string.Equals(method.Name, "PerformSocialActionOn", StringComparison.Ordinal))
					{
						continue;
					}

					PatchTimedMethod(harmony, method, PlayerAIStartDetailThresholdMs);
					patched++;
				}

				MethodInfo inform = FindInstanceMethodNoWarn(typeof(SocialHistoryData), "InformOfSocialAction");
				if (inform != null)
				{
					PatchTimedMethod(harmony, inform, PlayerAIStartDetailThresholdMs);
					patched++;
				}

				MethodInfo reprocess = FindInstanceMethodNoWarn(typeof(SocialHistoryData), "ReprocessInferences");
				if (reprocess != null)
				{
					harmony.Patch(
						reprocess,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(DeferSocialInferencePrefix))
						{
							priority = Priority.First
						});
					PatchTimedMethod(harmony, reprocess, PlayerAIStartDetailThresholdMs);
					patched++;
				}

				MethodInfo flushQuestRequests = FindInstanceMethodNoWarn(typeof(SocialHistoryData), "FlushQuestRequests");
				if (flushQuestRequests != null)
				{
					HarmonyMethod skipNonHumanPrefix = new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(SkipNonHumanSocialQuestFlushPrefix))
					{
						priority = Priority.First
					};
					harmony.Patch(flushQuestRequests, prefix: skipNonHumanPrefix);
					PatchTimedMethod(harmony, flushQuestRequests, PlayerAIStartDetailThresholdMs);
					patched++;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Social-action performance detail patch setup failed: " + ex.GetType().Name + ":" + ex.Message);
			}

			return patched;
		}

		private static int PatchActionCompletionDetailMethods(Harmony harmony)
		{
			int patched = 0;
			try
			{
				Type convoCallbacksType = AccessTools.TypeByName("Game.UI.Session.Convo.ConvoCallbacks");
				string[] convoMethodNames =
				{
					"StoreVehicleCosts",
					"ExecuteVehicleRepair",
					"StoreTicketSkillChoice",
					"ExecuteTicketSkillsInfo",
					"ExecuteTicketSkillsStartDeliveryQuest"
				};

				foreach (string methodName in convoMethodNames)
				{
					MethodInfo method = FindInstanceMethodNoWarn(convoCallbacksType, methodName);
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(harmony, method, PlayerAIStartDetailThresholdMs);
					patched++;
				}

				string[] vehicleMethodNames =
				{
					"FindRepairCost",
					"MakeRepairInfo",
					"StartRepairVehicleAtHome",
					"StartRepairVehicleDuringVisit",
					"FinishRepairVehicle"
				};

				foreach (string methodName in vehicleMethodNames)
				{
					MethodInfo method = FindInstanceMethodNoWarn(typeof(VehicleModule), methodName);
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(harmony, method, PlayerAIStartDetailThresholdMs);
					patched++;
				}

				string[] skillMethodNames =
				{
					"CanPayForSkill",
					"DoPayForSkill",
					"DoLearnPaidSkill",
					"DoLearnFromSkillTrack",
					"StartQuestFromSkillConvo"
				};

				foreach (string methodName in skillMethodNames)
				{
					MethodInfo method = FindInstanceMethodNoWarn(typeof(PlayerSkills), methodName);
					if (method == null)
					{
						continue;
					}

					PatchTimedMethod(harmony, method, PlayerAIStartDetailThresholdMs);
					if (string.Equals(methodName, "DoLearnPaidSkill", StringComparison.Ordinal)
						|| string.Equals(methodName, "DoLearnFromSkillTrack", StringComparison.Ordinal)
						|| string.Equals(methodName, "DoPayForSkill", StringComparison.Ordinal))
					{
						harmony.Patch(
							method,
							postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ClearDistinctSkillChoiceCacheAfterSkillMutation)));
					}
					patched++;
				}

				MethodInfo startQuestFromSkillConvo = FindInstanceMethodNoWarn(typeof(PlayerSkills), "StartQuestFromSkillConvo");
				if (startQuestFromSkillConvo != null)
				{
					harmony.Patch(
						startQuestFromSkillConvo,
						postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ClearDistinctSkillChoiceCacheAfterSkillQuestStart)));
				}

				MethodInfo findDistinctSkills = FindInstanceMethodNoWarn(typeof(PlayerSkills), "FindAllDistinctSkillsToLearn");
				if (findDistinctSkills != null)
				{
					harmony.Patch(
						findDistinctSkills,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(FindAllDistinctSkillsToLearnCachePrefix))
						{
							priority = Priority.First
						},
						postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(FindAllDistinctSkillsToLearnCachePostfix))
						{
							priority = Priority.Last
						});
					patched++;
				}

				MethodInfo moduleInTerritory = AccessTools.Method(typeof(CheckModuleInPlayerTerritory), "HasModuleAnywhereInTerritory");
				if (moduleInTerritory != null)
				{
					harmony.Patch(
						moduleInTerritory,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ModuleInTerritoryCachePrefix))
						{
							priority = Priority.First
						},
						postfix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(ModuleInTerritoryCachePostfix))
						{
							priority = Priority.Last
						});
					patched++;
				}

				MethodInfo checkCanBuySellDoesPass = AccessTools.Method(typeof(CheckCanBuySell), nameof(CheckCanBuySell.DoesPass), new[] { typeof(VisitState) });
				if (checkCanBuySellDoesPass != null)
				{
					harmony.Patch(
						checkCanBuySellDoesPass,
						prefix: new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(CheckCanBuySellDoesPassCachePrefix))
						{
							priority = Priority.First
						});
					patched++;
				}

				MethodInfo showSkillLearnedTicker = AccessTools.Method(typeof(PlayerSkills), "ShowSkillLearnedTicker");
				if (showSkillLearnedTicker != null)
				{
					PatchTimedMethod(harmony, showSkillLearnedTicker, PlayerAIStartDetailThresholdMs);
					patched++;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Action completion performance detail patch setup failed: " + ex.GetType().Name + ":" + ex.Message);
			}

			return patched;
		}

		private static bool FindAllDistinctSkillsToLearnCachePrefix(VisitState visit, ref List<SkillDef> __result, out DistinctSkillChoiceCacheState __state)
		{
			__state = new DistinctSkillChoiceCacheState
			{
				StartTicks = Stopwatch.GetTimestamp()
			};

			try
			{
				PruneDistinctSkillChoiceCache();
				string key = BuildDistinctSkillChoiceCacheKey(visit, out int currentSkillCount);
				__state.Key = key;
				if (string.IsNullOrEmpty(key))
				{
					return true;
				}

				if (!DistinctSkillChoiceCache.TryGetValue(key, out CachedDistinctSkillChoice cached)
					|| cached.Skills == null
					|| cached.CurrentSkillCount != currentSkillCount
					|| Time.frameCount - cached.Frame > DistinctSkillChoiceCacheFrames)
				{
					if (TryFindAllDistinctSkillsToLearnOptimized(visit, out List<SkillDef> optimized, out int taggedSkillsSkipped))
					{
						__result = optimized;
						__state.OptimizedScan = true;
						__state.TaggedSkillsSkipped = taggedSkillsSkipped;
						return false;
					}

					return true;
				}

				__result = new List<SkillDef>(cached.Skills);
				__state.CacheHit = true;
				_distinctSkillChoiceCacheHits++;
				if (_distinctSkillChoiceCacheHits == 1 || _distinctSkillChoiceCacheHits % 8 == 0)
				{
					Debug.Log("[PERF][SkillChoiceCache] hit count=" + __result.Count + " ageFrames=" + (Time.frameCount - cached.Frame) + " hits=" + _distinctSkillChoiceCacheHits + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static void FindAllDistinctSkillsToLearnCachePostfix(VisitState visit, List<SkillDef> __result, DistinctSkillChoiceCacheState __state)
		{
			try
			{
				if (__state.CacheHit || __result == null || string.IsNullOrEmpty(__state.Key))
				{
					return;
				}

				string key = BuildDistinctSkillChoiceCacheKey(visit, out int currentSkillCount);
				if (!string.Equals(key, __state.Key, StringComparison.Ordinal))
				{
					return;
				}

				DistinctSkillChoiceCache[key] = new CachedDistinctSkillChoice
				{
					Frame = Time.frameCount,
					Day = GetDayForLog(),
					Turn = GetTurnForLog(),
					CurrentSkillCount = currentSkillCount,
					Skills = new List<SkillDef>(__result)
				};

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				if (elapsedMs >= GetCacheMissLogThresholdMs())
				{
					Debug.Log("[PERF][SkillChoiceCache] miss ms=" + elapsedMs + " count=" + __result.Count + " cacheSize=" + DistinctSkillChoiceCache.Count + " optimized=" + __state.OptimizedScan + " taggedSkipped=" + __state.TaggedSkillsSkipped + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
			}
			catch
			{
			}
		}

		private static bool TryFindAllDistinctSkillsToLearnOptimized(VisitState visit, out List<SkillDef> result, out int taggedSkillsSkipped)
		{
			result = null;
			taggedSkillsSkipped = 0;
			try
			{
				if (visit == null || visit.building?.components?.modules == null)
				{
					return false;
				}

				PlayerInfo player = visit.GetPlayer();
				PlayerSkills skills = player?.skills;
				List<SkillDef> allSkills = global::Game.Game.serv?.globals?.settings?.skills?.items;
				if (skills == null || allSkills == null)
				{
					return false;
				}

				TagList moduleSkillTags = visit.building.components.modules.GetSkillTagsForAllModules();
				List<SkillDef> found = new List<SkillDef>();
				HashSet<string> locNames = new HashSet<string>(StringComparer.Ordinal);
				SkillRequirementScanStats requirementStats = new SkillRequirementScanStats();
				for (int i = 0; i < allSkills.Count; i++)
				{
					SkillDef skill = allSkills[i];
					if (skill == null || skills.HasSkill(skill.id))
					{
						continue;
					}

					TagList tags = skill.tags;
					if (tags != null && tags.Count > 0 && (moduleSkillTags == null || !moduleSkillTags.ContainsAtLeastOneOf(tags)))
					{
						taggedSkillsSkipped++;
						continue;
					}

					if (!SkillVisReqsPass(skill, visit, requirementStats))
					{
						continue;
					}

					if (locNames.Add(skill.locname))
					{
						found.Add(skill);
					}
				}

				result = found;
				LogSkillRequirementScanStats(requirementStats, found.Count, taggedSkillsSkipped);
				return true;
			}
			catch
			{
				result = null;
				taggedSkillsSkipped = 0;
				return false;
			}
		}

		private static bool SkillVisReqsPass(SkillDef skill, VisitState visit, SkillRequirementScanStats stats)
		{
			VisitRequirementList reqs = skill?.visreqs;
			if (reqs == null)
			{
				return true;
			}

			stats.SkillsChecked++;
			for (int i = 0; i < reqs.Count; i++)
			{
				IVisitRequirement requirement = reqs[i];
				if (requirement == null)
				{
					continue;
				}

				string typeName = requirement.GetType().Name;
				long reqStart = Stopwatch.GetTimestamp();
				bool passed = requirement.DoesPass(visit);
				long elapsedTicks = Stopwatch.GetTimestamp() - reqStart;
				stats.RequirementsChecked++;
				stats.TotalRequirementTicks += elapsedTicks;
				if (!stats.TicksByType.ContainsKey(typeName))
				{
					stats.TicksByType[typeName] = 0;
					stats.CallsByType[typeName] = 0;
				}

				stats.TicksByType[typeName] += elapsedTicks;
				stats.CallsByType[typeName]++;
				long elapsedMs = (elapsedTicks * 1000L) / Stopwatch.Frequency;
				if (elapsedMs >= SkillRequirementSingleLogThresholdMs)
				{
					Debug.Log("[PERF][SkillReqDetail] type=" + typeName + " ms=" + elapsedMs + " passed=" + passed + " skill=" + skill.id + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}

				if (!passed)
				{
					stats.RequirementsFailed++;
					return false;
				}
			}

			return true;
		}

		private static void LogSkillRequirementScanStats(SkillRequirementScanStats stats, int foundCount, int taggedSkillsSkipped)
		{
			try
			{
				if (stats == null || stats.RequirementsChecked == 0)
				{
					return;
				}

				long totalMs = (stats.TotalRequirementTicks * 1000L) / Stopwatch.Frequency;
				if (totalMs < SkillRequirementDetailLogThresholdMs)
				{
					return;
				}

				string topType = "none";
				long topTicks = 0;
				int topCalls = 0;
				foreach (KeyValuePair<string, long> pair in stats.TicksByType)
				{
					if (pair.Value > topTicks)
					{
						topType = pair.Key;
						topTicks = pair.Value;
						stats.CallsByType.TryGetValue(pair.Key, out topCalls);
					}
				}

				long topMs = (topTicks * 1000L) / Stopwatch.Frequency;
				Debug.Log("[PERF][SkillReqSummary] totalMs=" + totalMs + " topType=" + topType + " topMs=" + topMs + " topCalls=" + topCalls + " reqs=" + stats.RequirementsChecked + " failed=" + stats.RequirementsFailed + " skillsChecked=" + stats.SkillsChecked + " found=" + foundCount + " taggedSkipped=" + taggedSkillsSkipped + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
			}
			catch
			{
			}
		}

		private static void ClearDistinctSkillChoiceCacheAfterSkillMutation(bool __result)
		{
			if (__result)
			{
				ClearDistinctSkillChoiceCache("skill-mutation");
			}
		}

		private static void ClearDistinctSkillChoiceCacheAfterSkillQuestStart()
		{
			ClearDistinctSkillChoiceCache("skill-quest-start");
		}

		private static void ClearDistinctSkillChoiceCache(string source)
		{
			try
			{
				if (DistinctSkillChoiceCache.Count == 0)
				{
					return;
				}

				int count = DistinctSkillChoiceCache.Count;
				DistinctSkillChoiceCache.Clear();
				Debug.Log("[PERF][SkillChoiceCache] cleared source=" + source + " count=" + count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
			}
			catch
			{
			}
		}

		private static void PruneDistinctSkillChoiceCache()
		{
			try
			{
				int frame = Time.frameCount;
				if (frame - _distinctSkillChoiceCacheLastPruneFrame < 120)
				{
					return;
				}

				_distinctSkillChoiceCacheLastPruneFrame = frame;
				if (DistinctSkillChoiceCache.Count == 0)
				{
					return;
				}

				List<string> removeKeys = null;
				foreach (KeyValuePair<string, CachedDistinctSkillChoice> pair in DistinctSkillChoiceCache)
				{
					if (pair.Value == null || frame - pair.Value.Frame > 240)
					{
						removeKeys = removeKeys ?? new List<string>();
						removeKeys.Add(pair.Key);
					}
				}

				if (removeKeys == null)
				{
					return;
				}

				for (int i = 0; i < removeKeys.Count; i++)
				{
					DistinctSkillChoiceCache.Remove(removeKeys[i]);
				}
			}
			catch
			{
			}
		}

		private static string BuildDistinctSkillChoiceCacheKey(VisitState visit, out int currentSkillCount)
		{
			currentSkillCount = -1;
			try
			{
				if (visit == null || visit.building == null)
				{
					return null;
				}

				PlayerInfo player = visit.GetPlayer();
				PlayerSkills skills = player?.skills;
				if (skills == null)
				{
					return null;
				}

				currentSkillCount = skills.CurrentSkillCount;
				ulong buildingId = visit.building.Id.id;
				ulong npcId = visit.npc != null ? visit.npc.Id.id : 0UL;
				ulong vehicleId = visit.vehicle != null ? visit.vehicle.Id.id : 0UL;
				return player.PID.id + ":" + buildingId + ":" + npcId + ":" + vehicleId + ":" + currentSkillCount + ":" + GetDayForLog() + ":" + GetTurnForLog();
			}
			catch
			{
				return null;
			}
		}

		private static bool CheckCanBuySellDoesPassCachePrefix(CheckCanBuySell __instance, VisitState visit, ref bool __result)
		{
			try
			{
				if (__instance == null || visit?.building?.components?.modules == null)
				{
					return true;
				}

				if (TryGetVisitTradeRestrictions(visit, out BizComponent.TradeRestrictions restrictions, out Entity lockedBiz)
					&& restrictions.IsLocked)
				{
					__result = EvaluateCanBuySell(__instance.playercan, canBuy: false, canSell: false);
					_canBuySellAvailabilityLockedBlocks++;
					bool forcedClosed = restrictions.IsForcedClosed;
					string lockedKey = forcedClosed ? BuildCanBuySellLockedKey(visit, restrictions) : null;
					int turn = forcedClosed ? GetTurnForLog() : 0;
					int day = forcedClosed ? GetDayForLog() : 0;
					if (forcedClosed && ShouldRunLockedCanBuySellOncePerTurn(CanBuySellLockedInvalidationTurnByKey, lockedKey, turn))
					{
						InvalidateCanBuySellAvailabilityCache(visit.building, visit.pid, "trade-locked-prefix");
					}
					if ((forcedClosed && ShouldRunLockedCanBuySellEveryDays(CanBuySellLockedBlockLogDayByKey, lockedKey, day, CanBuySellForcedClosedLogIntervalDays))
						|| _canBuySellAvailabilityLockedBlocks == 1
						|| _canBuySellAvailabilityLockedBlocks % CanBuySellLockedLogEvery == 0)
					{
						GameplayTweaksPlugin.VerificationLog(
							"CanBuySellCache",
							$"blocked-locked building={visit.building.Id.id} biz={lockedBiz?.Id.id ?? 0UL} pid={visit.pid.id} playercan={__instance.playercan} result={__result} tied={restrictions.tiedHouseLock.id} territory={restrictions.territoryLock.id} forcedClosedBy={restrictions.forcedClosedBy.id} blocks={_canBuySellAvailabilityLockedBlocks} day={GetDayForLog()} turn={GetTurnForLog()}");
					}
					return false;
				}

				PruneCanBuySellAvailabilityCache();
				string key = BuildCanBuySellAvailabilityCacheKey(visit);
				if (string.IsNullOrEmpty(key))
				{
					return true;
				}

				int frame = Time.frameCount;
				if (CanBuySellAvailabilityCache.TryGetValue(key, out CachedCanBuySellAvailability cached)
					&& cached != null
					&& frame - cached.Frame <= CanBuySellAvailabilityCacheFrames)
				{
					__result = EvaluateCanBuySell(__instance.playercan, cached.CanBuy, cached.CanSell);
					_canBuySellAvailabilityCacheHits++;
					if (ShouldLogPerformanceSample()
						&& (_canBuySellAvailabilityCacheHits == 1 || _canBuySellAvailabilityCacheHits % 128 == 0))
					{
						Debug.Log("[PERF][CanBuySellCache] hit playercan=" + __instance.playercan + " result=" + __result + " ageFrames=" + (frame - cached.Frame) + " hits=" + _canBuySellAvailabilityCacheHits + " cacheSize=" + CanBuySellAvailabilityCache.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
					}
					return false;
				}

				long startTicks = Stopwatch.GetTimestamp();
				bool canBuy = visit.building.components.modules.ProduceAllItemsPlayerCanBuyOrSell(visit.pid, playerBuys: true, playerSells: false).Any();
				bool canSell = visit.building.components.modules.ProduceAllItemsPlayerCanBuyOrSell(visit.pid, playerBuys: false, playerSells: true).Any();
				CanBuySellAvailabilityCache[key] = new CachedCanBuySellAvailability
				{
					Frame = frame,
					CanBuy = canBuy,
					CanSell = canSell
				};

				__result = EvaluateCanBuySell(__instance.playercan, canBuy, canSell);
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs >= GetCacheMissLogThresholdMs())
				{
					_canBuySellAvailabilityCacheMisses++;
					Debug.Log("[PERF][CanBuySellCache] miss ms=" + elapsedMs + " playercan=" + __instance.playercan + " result=" + __result + " canBuy=" + canBuy + " canSell=" + canSell + " misses=" + _canBuySellAvailabilityCacheMisses + " cacheSize=" + CanBuySellAvailabilityCache.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static string BuildCanBuySellLockedKey(VisitState visit, BizComponent.TradeRestrictions restrictions)
		{
			try
			{
				if (visit?.building == null)
				{
					return null;
				}

				return visit.pid.id
					+ ":"
					+ visit.building.Id.id
					+ ":"
					+ restrictions.tiedHouseLock.id
					+ ":"
					+ restrictions.territoryLock.id
					+ ":"
					+ restrictions.forcedClosedBy.id;
			}
			catch
			{
				return null;
			}
		}

		private static bool ShouldRunLockedCanBuySellEveryDays(Dictionary<string, int> tracker, string key, int day, int intervalDays)
		{
			try
			{
				if (tracker == null || string.IsNullOrEmpty(key))
				{
					return true;
				}

				if (tracker.TryGetValue(key, out int lastDay) && day - lastDay < intervalDays)
				{
					return false;
				}

				if (tracker.Count > CanBuySellLockedKeyLimit)
				{
					tracker.Clear();
				}

				tracker[key] = day;
				return true;
			}
			catch
			{
				return true;
			}
		}

		private static bool ShouldRunLockedCanBuySellOncePerTurn(Dictionary<string, int> tracker, string key, int turn)
		{
			try
			{
				if (tracker == null || string.IsNullOrEmpty(key))
				{
					return true;
				}

				if (tracker.TryGetValue(key, out int lastTurn) && lastTurn == turn)
				{
					return false;
				}

				if (tracker.Count > CanBuySellLockedKeyLimit)
				{
					tracker.Clear();
				}

				tracker[key] = turn;
				return true;
			}
			catch
			{
				return true;
			}
		}

		internal static void InvalidateCanBuySellAvailabilityCache(Entity building, PlayerID pid, string source)
		{
			try
			{
				if (building == null || CanBuySellAvailabilityCache.Count == 0)
				{
					return;
				}

				string exactPrefix = pid.IsAnyPlayer ? pid.id + ":" + building.Id.id + ":" : null;
				string buildingNeedle = ":" + building.Id.id + ":";
				List<string> removeKeys = null;
				foreach (string key in CanBuySellAvailabilityCache.Keys)
				{
					if ((!string.IsNullOrEmpty(exactPrefix) && key.StartsWith(exactPrefix, StringComparison.Ordinal))
						|| (string.IsNullOrEmpty(exactPrefix) && key.Contains(buildingNeedle)))
					{
						removeKeys = removeKeys ?? new List<string>();
						removeKeys.Add(key);
					}
				}

				if (removeKeys == null)
				{
					return;
				}

				for (int i = 0; i < removeKeys.Count; i++)
				{
					CanBuySellAvailabilityCache.Remove(removeKeys[i]);
				}

				GameplayTweaksPlugin.VerificationLog(
					"CanBuySellCache",
					$"invalidated building={building.Id.id} pid={pid.id} removed={removeKeys.Count} source={source} cacheSize={CanBuySellAvailabilityCache.Count} day={GetDayForLog()} turn={GetTurnForLog()}");
			}
			catch
			{
			}
		}

		private static bool TryGetVisitTradeRestrictions(VisitState visit, out BizComponent.TradeRestrictions restrictions, out Entity biz)
		{
			restrictions = default(BizComponent.TradeRestrictions);
			biz = null;
			try
			{
				if (visit?.building == null || visit.pid.IsNotAnyPlayer)
				{
					return false;
				}

				biz = visit.biz ?? BuildingUtil.FindBizForBuilding(visit.building);
				if (biz?.components?.biz == null)
				{
					return false;
				}

				restrictions = biz.components.biz.FindTradeRestrictions(visit.pid);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool EvaluateCanBuySell(CheckCanBuySell.BuySellType playerCan, bool canBuy, bool canSell)
		{
			switch (playerCan)
			{
				case CheckCanBuySell.BuySellType.BuyAndSell:
					return canBuy && canSell;
				case CheckCanBuySell.BuySellType.BuyOrSell:
					return canBuy || canSell;
				case CheckCanBuySell.BuySellType.Buy:
					return canBuy;
				case CheckCanBuySell.BuySellType.Sell:
					return canSell;
				case CheckCanBuySell.BuySellType.OnlyBuy:
					return canBuy && !canSell;
				case CheckCanBuySell.BuySellType.OnlySell:
					return canSell && !canBuy;
				case CheckCanBuySell.BuySellType.Neither:
					return !canBuy && !canSell;
				default:
					return false;
			}
		}

		private static string BuildCanBuySellAvailabilityCacheKey(VisitState visit)
		{
			try
			{
				if (visit?.building == null)
				{
					return null;
				}

				return visit.pid.id + ":" + visit.building.Id.id + ":" + BuildBuildingModuleSignature(visit.building) + ":" + GetDayForLog() + ":" + GetTurnForLog();
			}
			catch
			{
				return null;
			}
		}

		private static string BuildBuildingModuleSignature(Entity building)
		{
			try
			{
				List<IModule> slots = building?.components?.modules?.GetAllSlotsUnsafe();
				if (slots == null || slots.Count == 0)
				{
					return "none";
				}

				StringBuilder builder = new StringBuilder(slots.Count * 12);
				for (int i = 0; i < slots.Count; i++)
				{
					if (i > 0)
					{
						builder.Append(',');
					}

					Label id = slots[i]?.ModuleData?.Id ?? Label.NULL;
					builder.Append(id.IsSet ? id.ToString() : "empty");
				}
				return builder.ToString();
			}
			catch
			{
				return "unknown";
			}
		}

		private static void PruneCanBuySellAvailabilityCache()
		{
			try
			{
				int frame = Time.frameCount;
				if (frame - _canBuySellAvailabilityCacheLastPruneFrame < CanBuySellAvailabilityCacheFrames)
				{
					return;
				}

				_canBuySellAvailabilityCacheLastPruneFrame = frame;
				if (CanBuySellAvailabilityCache.Count == 0)
				{
					return;
				}

				List<string> removeKeys = null;
				foreach (KeyValuePair<string, CachedCanBuySellAvailability> pair in CanBuySellAvailabilityCache)
				{
					if (pair.Value == null || frame - pair.Value.Frame > CanBuySellAvailabilityCacheFrames)
					{
						removeKeys = removeKeys ?? new List<string>();
						removeKeys.Add(pair.Key);
					}
				}

				if (removeKeys == null)
				{
					return;
				}

				for (int i = 0; i < removeKeys.Count; i++)
				{
					CanBuySellAvailabilityCache.Remove(removeKeys[i]);
				}
			}
			catch
			{
			}
		}

		private static bool ModuleInTerritoryCachePrefix(CheckModuleInPlayerTerritory __instance, PlayerInfo player, Label moduleId, ref bool __result, out ModuleInTerritoryCacheState __state)
		{
			__state = new ModuleInTerritoryCacheState
			{
				StartTicks = Stopwatch.GetTimestamp()
			};

			try
			{
				PruneModuleInTerritoryCache();
				if (TryGetTerritoryModulePresence(__instance, player, moduleId, out bool presenceResult))
				{
					__result = presenceResult;
					__state.CacheHit = true;
					return false;
				}

				string key = BuildModuleInTerritoryCacheKey(__instance, player, moduleId);
				__state.Key = key;
				if (string.IsNullOrEmpty(key))
				{
					return true;
				}

				if (!ModuleInTerritoryCache.TryGetValue(key, out CachedModuleInTerritoryCheck cached)
					|| Time.frameCount - cached.Frame > ModuleInTerritoryCacheFrames)
				{
					return true;
				}

				__result = cached.Result;
				__state.CacheHit = true;
				_moduleInTerritoryCacheHits++;
				if (ShouldLogPerformanceSample()
					&& (_moduleInTerritoryCacheHits == 1 || _moduleInTerritoryCacheHits % 4096 == 0))
				{
					Debug.Log("[PERF][ModuleTerritoryCache] hit module=" + moduleId + " result=" + cached.Result + " ageFrames=" + (Time.frameCount - cached.Frame) + " hits=" + _moduleInTerritoryCacheHits + " cacheSize=" + ModuleInTerritoryCache.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool TryGetTerritoryModulePresence(CheckModuleInPlayerTerritory check, PlayerInfo player, Label moduleId, out bool result)
		{
			result = false;
			try
			{
				PlayerTerritory territory = player?.territory;
				if (check == null || territory == null)
				{
					return false;
				}

				List<EntityID> controlledBuildings = GetTerritoryControlledBuildings(territory);
				List<NodeID> ownedNodes = GetTerritoryOwnedNodes(territory);
				int controlledCount = controlledBuildings?.Count ?? 0;
				int ownedNodeCount = ownedNodes?.Count ?? 0;
				string key = player.PID.id + ":" + check.scope + ":" + controlledCount + ":" + ownedNodeCount + ":" + GetDayForLog() + ":" + GetTurnForLog();
				int frame = Time.frameCount;
				if (!TerritoryModulePresenceCache.TryGetValue(key, out CachedTerritoryModulePresence cached)
					|| cached == null
					|| cached.Modules == null
					|| frame - cached.Frame > TerritoryModulePresenceCacheFrames)
				{
					cached = BuildTerritoryModulePresence(check.scope, controlledBuildings, ownedNodes, frame);
					if (cached == null)
					{
						return false;
					}

					TerritoryModulePresenceCache[key] = cached;
					_territoryModulePresenceBuilds++;
					if (ShouldLogPerformanceSample()
						&& (_territoryModulePresenceBuilds == 1 || _territoryModulePresenceBuilds % 16 == 0))
					{
						Debug.Log("[PERF][TerritoryModulePresence] built=" + _territoryModulePresenceBuilds + " scope=" + check.scope + " modules=" + cached.Modules.Count + " controlled=" + controlledCount + " ownedNodes=" + ownedNodeCount + " frame=" + frame + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
					}
				}
				else
				{
					_territoryModulePresenceHits++;
					if (ShouldLogPerformanceSample()
						&& (_territoryModulePresenceHits == 1 || _territoryModulePresenceHits % 1024 == 0))
					{
						Debug.Log("[PERF][TerritoryModulePresence] hit=" + _territoryModulePresenceHits + " scope=" + check.scope + " modules=" + cached.Modules.Count + " ageFrames=" + (frame - cached.Frame) + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
					}
				}

				result = cached.Modules.Contains(moduleId);
				return true;
			}
			catch
			{
				result = false;
				return false;
			}
		}

		private static CachedTerritoryModulePresence BuildTerritoryModulePresence(CheckModuleInPlayerTerritory.Scope scope, List<EntityID> controlledBuildings, List<NodeID> ownedNodes, int frame)
		{
			try
			{
				HashSet<Label> modules = new HashSet<Label>();
				if (scope == CheckModuleInPlayerTerritory.Scope.AllBuildings || scope == CheckModuleInPlayerTerritory.Scope.PlayerOnly)
				{
					AddControlledBuildingModules(controlledBuildings, modules);
				}

				if (scope == CheckModuleInPlayerTerritory.Scope.AllBuildings || scope == CheckModuleInPlayerTerritory.Scope.InterestingOnly)
				{
					AddInterestingBuildingModules(ownedNodes, modules);
				}

				return new CachedTerritoryModulePresence
				{
					Frame = frame,
					Modules = modules
				};
			}
			catch
			{
				return null;
			}
		}

		private static List<EntityID> GetTerritoryControlledBuildings(PlayerTerritory territory)
		{
			try
			{
				return PlayerTerritoryGetAllControlledBuildingsUnsafeMethod?.Invoke(territory, null) as List<EntityID>;
			}
			catch
			{
				return null;
			}
		}

		private static List<NodeID> GetTerritoryOwnedNodes(PlayerTerritory territory)
		{
			try
			{
				return PlayerTerritoryGetAllOwnedNodesUnsafeMethod?.Invoke(territory, null) as List<NodeID>;
			}
			catch
			{
				return null;
			}
		}

		private static void AddControlledBuildingModules(List<EntityID> buildings, HashSet<Label> modules)
		{
			if (buildings == null || modules == null)
			{
				return;
			}

			for (int i = 0; i < buildings.Count; i++)
			{
				AddEntityModules(buildings[i].FindEntity(), modules);
			}
		}

		private static void AddInterestingBuildingModules(List<NodeID> nodes, HashSet<Label> modules)
		{
			if (nodes == null || modules == null)
			{
				return;
			}

			for (int i = 0; i < nodes.Count; i++)
			{
				Node node = nodes[i].FindNode();
				List<EntityID> interesting = node?.interesting;
				if (interesting == null)
				{
					continue;
				}

				for (int j = 0; j < interesting.Count; j++)
				{
					AddEntityModules(interesting[j].FindEntity(), modules);
				}
			}
		}

		private static void AddEntityModules(Entity entity, HashSet<Label> modules)
		{
			List<IModule> slots = entity?.components?.modules?.GetAllSlotsUnsafe();
			if (slots == null)
			{
				return;
			}

			for (int i = 0; i < slots.Count; i++)
			{
				Label id = slots[i]?.ModuleData?.Id ?? Label.NULL;
				if (id.IsSet)
				{
					modules.Add(id);
				}
			}
		}

		private static void ModuleInTerritoryCachePostfix(CheckModuleInPlayerTerritory __instance, PlayerInfo player, Label moduleId, bool __result, ModuleInTerritoryCacheState __state)
		{
			try
			{
				if (__state.CacheHit || string.IsNullOrEmpty(__state.Key))
				{
					return;
				}

				string key = BuildModuleInTerritoryCacheKey(__instance, player, moduleId);
				if (!string.Equals(key, __state.Key, StringComparison.Ordinal))
				{
					return;
				}

				ModuleInTerritoryCache[key] = new CachedModuleInTerritoryCheck
				{
					Frame = Time.frameCount,
					Result = __result
				};

				long elapsedMs = GetElapsedMilliseconds(__state.StartTicks);
				if (elapsedMs >= GetCacheMissLogThresholdMs())
				{
					Debug.Log("[PERF][ModuleTerritoryCache] miss ms=" + elapsedMs + " module=" + moduleId + " result=" + __result + " cacheSize=" + ModuleInTerritoryCache.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
			}
			catch
			{
			}
		}

		private static void PruneModuleInTerritoryCache()
		{
			try
			{
				int frame = Time.frameCount;
				if (frame - _moduleInTerritoryCacheLastPruneFrame < 120)
				{
					return;
				}

				_moduleInTerritoryCacheLastPruneFrame = frame;
				if (ModuleInTerritoryCache.Count == 0)
				{
					return;
				}

				List<string> removeKeys = null;
				foreach (KeyValuePair<string, CachedModuleInTerritoryCheck> pair in ModuleInTerritoryCache)
				{
					if (pair.Value == null || frame - pair.Value.Frame > ModuleInTerritoryCacheFrames)
					{
						removeKeys = removeKeys ?? new List<string>();
						removeKeys.Add(pair.Key);
					}
				}

				if (removeKeys == null)
				{
					return;
				}

				for (int i = 0; i < removeKeys.Count; i++)
				{
					ModuleInTerritoryCache.Remove(removeKeys[i]);
				}
			}
			catch
			{
			}
		}

		private static string BuildModuleInTerritoryCacheKey(CheckModuleInPlayerTerritory check, PlayerInfo player, Label moduleId)
		{
			try
			{
				PlayerTerritory territory = player?.territory;
				if (check == null || territory == null)
				{
					return null;
				}

				int controlledCount = GetTerritoryListCount(PlayerTerritoryGetAllControlledBuildingsUnsafeMethod, territory);
				int ownedNodeCount = GetTerritoryListCount(PlayerTerritoryGetAllOwnedNodesUnsafeMethod, territory);
				return player.PID.id + ":" + check.scope + ":" + moduleId + ":" + controlledCount + ":" + ownedNodeCount + ":" + GetDayForLog() + ":" + GetTurnForLog();
			}
			catch
			{
				return null;
			}
		}

		private static int GetTerritoryListCount(MethodInfo method, PlayerTerritory territory)
		{
			try
			{
				if (method == null || territory == null)
				{
					return -1;
				}

				object value = method.Invoke(territory, null);
				if (value is System.Collections.ICollection collection)
				{
					return collection.Count;
				}
			}
			catch
			{
			}

			return -1;
		}

		private static bool DeferSocialInferencePrefix(SocialHistoryData __instance, Relationship rel, EntityID ctx, Label socialaction)
		{
			if (_flushingDeferredSocialInferences)
			{
				return true;
			}

			try
			{
				if (__instance == null || rel == null || PendingSocialInferences.Count >= MaxDeferredSocialInferenceQueue)
				{
					return true;
				}

				PlayerID fromPid = GetEntityAgentPid(rel.from);
				PlayerID toPid = GetEntityAgentPid(rel.to);
				if (!fromPid.IsHumanPlayer && !toPid.IsHumanPlayer)
				{
					return true;
				}

				int frame = Time.frameCount;
				PendingSocialInferences.Enqueue(new DeferredSocialInferenceWork(
					__instance,
					rel,
					ctx,
					socialaction,
					frame,
					frame + DeferredSocialInferenceDelayFrames,
					GetDayForLog(),
					GetTurnForLog()));
				if (PendingSocialInferences.Count == 1)
				{
					Debug.Log("[PERF][SocialInferenceDeferred] queued=1 socialAction=" + socialaction + " frame=" + frame + " earliest=" + (frame + DeferredSocialInferenceDelayFrames) + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		internal static void FlushDeferredSocialInferences(string source)
		{
			if (PendingSocialInferences.Count == 0 || SocialHistoryReprocessInferencesMethod == null)
			{
				return;
			}

			DeferredSocialInferenceWork next = PendingSocialInferences.Peek();
			if (Time.frameCount < next.EarliestFrame)
			{
				return;
			}

			int lastCommandFrame = _lastInteractiveCommandQueueFrame;
			if (lastCommandFrame > 0
				&& Time.frameCount - lastCommandFrame < DeferredSocialInferenceQuietFramesAfterCommandQueue
				&& Time.frameCount - next.EarliestFrame < DeferredSocialInferenceMaxExtraDelayFrames)
			{
				return;
			}

			long startTicks = Stopwatch.GetTimestamp();
			PendingSocialInferences.Dequeue();
			string outcome = "ok";
			if (ShouldDropDeferredSocialInference(next, out string dropReason))
			{
				long droppedMs = GetElapsedMilliseconds(startTicks);
				Debug.Log("[PERF][SocialInferenceDeferred] dropped ms=" + droppedMs + " waitFrames=" + (Time.frameCount - next.QueuedFrame) + " reason=" + dropReason + " remaining=" + PendingSocialInferences.Count + " source=" + source + " socialAction=" + next.SocialAction + " queuedDay=" + next.QueuedDay + " queuedTurn=" + next.QueuedTurn + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				return;
			}

			try
			{
				_flushingDeferredSocialInferences = true;
				if (SocialHistoryReprocessInferences != null)
				{
					SocialHistoryReprocessInferences(next.History, next.Relationship, next.Context, next.SocialAction);
				}
				else
				{
					SocialHistoryReprocessInferencesMethod.Invoke(next.History, new object[] { next.Relationship, next.Context, next.SocialAction });
				}
			}
			catch (Exception ex)
			{
				outcome = ex.GetType().Name;
				Debug.LogWarning("[GameplayTweaks] Deferred social inference failed: " + ex.GetType().Name + ":" + ex.Message);
			}
			finally
			{
				_flushingDeferredSocialInferences = false;
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs >= GetCacheMissLogThresholdMs()
					|| (PendingSocialInferences.Count > 0 && IsPerformanceDiagnosticsEnabled()))
				{
					Debug.Log("[PERF][SocialInferenceDeferred] flushed ms=" + elapsedMs + " waitFrames=" + (Time.frameCount - next.QueuedFrame) + " outcome=" + outcome + " remaining=" + PendingSocialInferences.Count + " source=" + source + " socialAction=" + next.SocialAction + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
			}
		}

		private static bool ShouldDropDeferredSocialInference(DeferredSocialInferenceWork work, out string reason)
		{
			reason = null;
			try
			{
				SessionContext context = global::Game.Game.ctx;
				if (context == null)
				{
					reason = "no-session-context";
					return true;
				}

				if (!context.IsInteractive || context.IsCityGen || context.clock == null || context.botl == null || context.simman?.rels == null)
				{
					reason = "inactive-session";
					return true;
				}

				if (work.History == null || work.Relationship == null)
				{
					reason = "missing-history-or-relationship";
					return true;
				}

				if (!TryGetSocialEndpoint(work.Relationship.from, out Entity fromEntity, out PlayerID fromPid, out bool fromDead, out reason))
				{
					reason = "from-" + reason;
					return true;
				}

				if (!TryGetSocialEndpoint(work.Relationship.to, out Entity toEntity, out PlayerID toPid, out bool toDead, out reason))
				{
					reason = "to-" + reason;
					return true;
				}

				if (!fromPid.IsHumanPlayer && !toPid.IsHumanPlayer)
				{
					reason = "no-human-endpoint";
					return true;
				}

				if (work.SocialAction == SocialConstants.VIOLENCE && (fromDead || toDead))
				{
					reason = "stale-violence-dead-endpoint";
					return true;
				}

				_ = fromEntity;
				_ = toEntity;
				reason = null;
				return false;
			}
			catch (Exception ex)
			{
				reason = "validation-" + ex.GetType().Name;
				return true;
			}
		}

		private static bool TryGetSocialEndpoint(EntityID entityId, out Entity entity, out PlayerID pid, out bool dead, out string reason)
		{
			entity = null;
			pid = PlayerID.INVALID;
			dead = false;
			reason = null;

			try
			{
				if (!entityId.IsValid)
				{
					reason = "invalid-entity";
					return false;
				}

				entity = entityId.FindEntity();
				AgentData agent = entity?.data?.agent;
				if (entity == null || agent == null)
				{
					reason = "missing-agent";
					return false;
				}

				pid = agent.pid;
				if (!pid.IsValid)
				{
					reason = "invalid-pid";
					return false;
				}

				dead = agent.health <= 0 || !agent.nid.IsValid;
				return true;
			}
			catch (Exception ex)
			{
				reason = "endpoint-" + ex.GetType().Name;
				return false;
			}
		}

		private static ReprocessSocialInferencesDelegate CreateReprocessSocialInferencesDelegate()
		{
			try
			{
				if (SocialHistoryReprocessInferencesMethod == null)
				{
					return null;
				}

				return Delegate.CreateDelegate(typeof(ReprocessSocialInferencesDelegate), SocialHistoryReprocessInferencesMethod, false) as ReprocessSocialInferencesDelegate;
			}
			catch
			{
				return null;
			}
		}

		private static bool SkipNonHumanSocialQuestFlushPrefix(EntityID actor, EntityID target)
		{
			try
			{
				PlayerID actorPid = GetEntityAgentPid(actor);
				PlayerID targetPid = GetEntityAgentPid(target);
				if (actorPid.IsHumanPlayer || targetPid.IsHumanPlayer)
				{
					return true;
				}

				if (!actorPid.IsAnyPlayer && !targetPid.IsAnyPlayer)
				{
					return true;
				}

				_skippedNonHumanSocialQuestFlushes++;
				if (IsPerformanceDiagnosticsEnabled()
					&& (_skippedNonHumanSocialQuestFlushes == 1 || _skippedNonHumanSocialQuestFlushes % 25 == 0))
				{
					Debug.Log("[PERF][SocialQuestFlushSkip] skipped=" + _skippedNonHumanSocialQuestFlushes + " reason=non-human-social-action actorPid=" + actorPid + " targetPid=" + targetPid + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
				}

				return false;
			}
			catch
			{
				return true;
			}
		}

		private static PlayerID GetEntityAgentPid(EntityID entityId)
		{
			try
			{
				if (!entityId.IsValid)
				{
					return PlayerID.INVALID;
				}

				return entityId.FindEntity()?.data?.agent?.pid ?? PlayerID.INVALID;
			}
			catch
			{
				return PlayerID.INVALID;
			}
		}

		private static void PatchTimedMethod(Harmony harmony, MethodInfo method)
		{
			PatchTimedMethod(harmony, method, SlowStageThresholdMs);
		}

		private static void PatchTimedMethod(Harmony harmony, MethodInfo method, long thresholdMs)
		{
			if (harmony == null || method == null)
			{
				return;
			}

			try
			{
				HarmonyMethod prefix = new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(TimingPrefix))
				{
					priority = Priority.First
				};
				HarmonyMethod postfix = new HarmonyMethod(typeof(TurnPerformanceDiagnosticsPatch), nameof(TimingPostfix))
				{
					priority = Priority.Last
				};
				ThresholdsByMethod[method] = thresholdMs;
				harmony.Patch(method, prefix: prefix, postfix: postfix);
			}
			catch (Exception ex)
			{
				ThresholdsByMethod.Remove(method);
				string target = FormatMethodForLog(method);
				if (FailedTimedPatchTargets.Add(target))
				{
					Debug.LogWarning("[GameplayTweaks] Turn performance timing patch skipped target=" + target + " error=" + ex.GetType().Name + ":" + ex.Message);
				}
			}
		}

		private static string FormatMethodForLog(MethodBase method)
		{
			if (method == null)
			{
				return "null";
			}

			return (method.DeclaringType?.FullName ?? "unknown-type") + "." + method.Name;
		}

		private static MethodInfo FindInstanceMethodNoWarn(Type type, string methodName)
		{
			if (type == null || string.IsNullOrEmpty(methodName))
			{
				return null;
			}

			try
			{
				return type.GetMethod(methodName, InstanceMethodFlags);
			}
			catch (AmbiguousMatchException)
			{
				foreach (MethodInfo method in type.GetMethods(InstanceMethodFlags))
				{
					if (string.Equals(method.Name, methodName, StringComparison.Ordinal))
					{
						return method;
					}
				}

				return null;
			}
		}

		private static void TimingPrefix(out long __state)
		{
			__state = Stopwatch.GetTimestamp();
		}

		private static void SessionEventSendImmediatePrefix(out long __state)
		{
			__state = Stopwatch.GetTimestamp();
		}

		private static bool PickContainerAddOrRefreshPickPrefix(PickContainer __instance, PickTarget t, bool resetExisting, ref BasePick __result)
		{
			try
			{
				if (__instance == null
					|| __instance.picks == null
					|| resetExisting
					|| t.IsNotValid
					|| !ShouldLimitExistingPickRefresh(__instance.type))
				{
					return true;
				}

				if (Time.frameCount <= _pickRefreshLimiterBypassUntilFrame)
				{
					return true;
				}

				if (!__instance.picks.TryGetValue(t, out BasePick existing) || existing == null)
				{
					return true;
				}

				int frame = Time.frameCount;
				if (_pickRefreshLimiterFrame != frame)
				{
					_pickRefreshLimiterFrame = frame;
					_pickRefreshLimiterExistingRefreshes = 0;
				}

				_pickRefreshLimiterExistingRefreshes++;
				if (_pickRefreshLimiterExistingRefreshes <= ExistingPickRefreshesPerFrame)
				{
					return true;
				}

				if (!QueueDeferredExistingPickRefresh(__instance, t, frame))
				{
					return true;
				}

				__result = existing;
				_pickRefreshLimiterSkipped++;
				if (IsPerformanceDiagnosticsEnabled()
					&& (_pickRefreshLimiterSkipped == 1 || _pickRefreshLimiterSkipped % 256 == 0))
				{
					Debug.Log("[PERF][PickRefreshLimiter] skipped=" + _pickRefreshLimiterSkipped + " type=" + __instance.type + " eid=" + t.eid.id + " frame=" + frame + " perFrameLimit=" + ExistingPickRefreshesPerFrame + " deferred=" + PendingExistingPickRefreshes.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool QueueDeferredExistingPickRefresh(PickContainer container, PickTarget target, int frame)
		{
			try
			{
				if (container == null
					|| container.picks == null
					|| !ShouldLimitExistingPickRefresh(container.type)
					|| target.IsNotValid)
				{
					return false;
				}

				ulong key = target.eid.id;
				if (PendingExistingPickRefreshIds.Contains(key))
				{
					return true;
				}

				if (PendingExistingPickRefreshes.Count >= MaxDeferredExistingPickRefreshes)
				{
					return false;
				}

				PendingExistingPickRefreshIds.Add(key);
				PendingExistingPickRefreshes.Enqueue(new DeferredExistingPickRefreshWork(
					container,
					target,
					frame,
					frame + DeferredExistingPickRefreshDelayFrames));
				return true;
			}
			catch
			{
				return false;
			}
		}

		internal static void FlushDeferredExistingPickRefreshes(string source)
		{
			if (PendingExistingPickRefreshes.Count == 0)
			{
				return;
			}

			int flushed = 0;
			int invalid = 0;
			long startTicks = Stopwatch.GetTimestamp();
			while (flushed < DeferredExistingPickRefreshesPerFrame && PendingExistingPickRefreshes.Count > 0)
			{
				DeferredExistingPickRefreshWork work = PendingExistingPickRefreshes.Peek();
				if (Time.frameCount < work.EarliestFrame)
				{
					break;
				}

				PendingExistingPickRefreshes.Dequeue();
				PendingExistingPickRefreshIds.Remove(work.Target.eid.id);
				try
				{
					if (work.Container == null
						|| work.Container.picks == null
						|| work.Target.IsNotValid)
					{
						invalid++;
						continue;
					}

					BasePick refreshed = work.Container.RefreshPickIfExists(work.Target);
					if (refreshed == null)
					{
						invalid++;
					}
					else
					{
						flushed++;
					}
				}
				catch
				{
					invalid++;
				}
			}

			if (flushed == 0 && invalid == 0)
			{
				return;
			}

			long elapsedMs = GetElapsedMilliseconds(startTicks);
			if (elapsedMs >= GetHudInputLogThresholdMs()
				|| invalid > 0
				|| (PendingExistingPickRefreshes.Count == 0 && IsPerformanceDiagnosticsEnabled()))
			{
				Debug.Log("[PERF][PickRefreshLimiter] deferred-flush flushed=" + flushed + " invalid=" + invalid + " remaining=" + PendingExistingPickRefreshes.Count + " source=" + source + " ms=" + elapsedMs + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
			}
		}

		internal static void BypassPickRefreshLimiterForCurrentFrame(int extraFrames, string reason)
		{
			try
			{
				int untilFrame = Time.frameCount + Math.Max(0, extraFrames);
				if (untilFrame > _pickRefreshLimiterBypassUntilFrame)
				{
					_pickRefreshLimiterBypassUntilFrame = untilFrame;
					_pickRefreshLimiterBypassReason = reason ?? string.Empty;
					if (IsPerformanceDiagnosticsEnabled())
					{
						Debug.Log("[PERF][PickRefreshLimiter] bypass untilFrame=" + _pickRefreshLimiterBypassUntilFrame + " reason=" + _pickRefreshLimiterBypassReason + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
					}
				}
			}
			catch
			{
			}
		}

		private static bool PickManagerOnBuildingChangedPrefix(PickManager __instance, SessionEvent sev)
		{
			try
			{
				if (__instance == null
					|| sev.type != SessionEventType.PlayerBuildingTakeoverImmediate
					|| !sev.pid.IsHumanPlayer
					|| sev.eid.IsNotValid
					|| PendingTakeoverPickRefreshes.Count >= MaxDeferredTakeoverPickRefreshes
					|| PickManagerAddOrRefreshPickMethod == null
					|| PickManagerRefreshVisibleSummaryPicksMethod == null)
				{
					return true;
				}

				int frame = Time.frameCount;
				PendingTakeoverPickRefreshes.Enqueue(new DeferredTakeoverPickRefreshWork(
					__instance,
					sev.eid,
					frame,
					frame + DeferredTakeoverPickRefreshDelayFrames));
				if (PendingTakeoverPickRefreshes.Count == 1)
				{
					Debug.Log("[PERF][TakeoverPickRefreshDeferred] queued=1 eid=" + sev.eid.id + " frame=" + frame + " earliest=" + (frame + DeferredTakeoverPickRefreshDelayFrames) + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool CrewDialogOnPlayerCrewAllDeferredPrefix(CrewDialog __instance, SessionEvent sev)
		{
			try
			{
				if (__instance == null
					|| !ShouldDeferCrewDialogRefresh(sev)
					|| CrewDialogRecreateAllCardsMethod == null)
				{
					return true;
				}

				int frame = Time.frameCount;
				int pendingCount = PendingCrewDialogRefreshes.Count;
				bool coalesced = false;
				for (int i = 0; i < pendingCount; i++)
				{
					DeferredCrewDialogRefreshWork existing = PendingCrewDialogRefreshes.Dequeue();
					if (ReferenceEquals(existing.Dialog, __instance))
					{
						coalesced = true;
					}
					else
					{
						PendingCrewDialogRefreshes.Enqueue(existing);
					}
				}

				if (PendingCrewDialogRefreshes.Count >= MaxDeferredCrewDialogRefreshes)
				{
					return true;
				}

				PendingCrewDialogRefreshes.Enqueue(new DeferredCrewDialogRefreshWork(
					__instance,
					sev.type,
					sev.eid,
					frame,
					frame + DeferredCrewDialogRefreshDelayFrames));
				if (coalesced)
				{
					Debug.Log("[PERF][CrewDialogRefreshDeferred] queued=1 event=" + sev.type + " eid=" + sev.eid.id + " coalesced=" + coalesced + " frame=" + frame + " earliest=" + (frame + DeferredCrewDialogRefreshDelayFrames) + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool CrewDialogOnCurrentActiveChangedDeferredPrefix(CrewDialog __instance, SessionEvent sev)
		{
			try
			{
				if (_flushingDeferredCrewDialogSelectionChange
					|| __instance == null
					|| sev.type != SessionEventType.SelectionActivationChange
					|| CrewDialogOnCurrentActiveChangedMethod == null
					|| !(global::Game.Game.ctx?.IsInteractive ?? false))
				{
					return true;
				}

				int frame = Time.frameCount;
				EntityID previousId = sev.ctx is Entity previousEntity ? previousEntity.Id : EntityID.INVALID;
				if (sev.eid.IsValid
					&& previousId.IsNotValid
					&& IsCrewDialogSelectionAlreadyCurrent(__instance, sev.eid, out string selectedCardType, out EntityID selectedPeepId))
				{
					GameplayTweaksPlugin.RememberBuildingPickVisibilityGraceForVehicle(sev.eid, "crew-dialog-selection-already-current");
					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"crew-dialog-selection-already-current-skipped active={sev.eid.id} peep={selectedPeepId.id} type={selectedCardType} frame={frame}");
					return false;
				}

				int pendingCount = PendingCrewDialogSelectionChanges.Count;
				bool coalesced = false;
				for (int i = 0; i < pendingCount; i++)
				{
					DeferredCrewDialogSelectionWork existing = PendingCrewDialogSelectionChanges.Dequeue();
					if (ReferenceEquals(existing.Dialog, __instance))
					{
						coalesced = true;
					}
					else
					{
						PendingCrewDialogSelectionChanges.Enqueue(existing);
					}
				}

				if (PendingCrewDialogSelectionChanges.Count >= MaxDeferredCrewDialogSelectionChanges)
				{
					return true;
				}

				PendingCrewDialogSelectionChanges.Enqueue(new DeferredCrewDialogSelectionWork(
					__instance,
					sev,
					frame,
					frame + DeferredCrewDialogSelectionDelayFrames));
				GameplayTweaksPlugin.RememberBuildingPickVisibilityGraceForCurrentSelection("crew-dialog-selection-queued");
				if (coalesced)
				{
					Debug.Log("[PERF][CrewDialogSelectionDeferred] queued=1 active=" + sev.eid.id + " previous=" + (sev.ctx is Entity previous ? previous.Id.id : 0UL) + " coalesced=" + coalesced + " frame=" + frame + " earliest=" + (frame + DeferredCrewDialogSelectionDelayFrames) + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool IsCrewDialogSelectionAlreadyCurrent(CrewDialog dialog, EntityID activeId, out string selectedCardType, out EntityID selectedPeepId)
		{
			selectedCardType = "unknown";
			selectedPeepId = EntityID.INVALID;
			if (dialog == null || activeId.IsNotValid || CrewDialogAllCardsField == null)
			{
				return false;
			}

			try
			{
				if (!(CrewDialogAllCardsField.GetValue(dialog) is IEnumerable<CrewCardContext> cards))
				{
					return false;
				}

				foreach (CrewCardContext card in cards)
				{
					CrewCardInfo data = card?.data;
					if (data == null || card.toggle == null || !card.toggle.isOn)
					{
						continue;
					}

					if (DoesCrewCardInfoMatchSelectionTarget(data, activeId))
					{
						selectedCardType = data.type.ToString();
						selectedPeepId = data.crew.peepId;
						return true;
					}
				}
			}
			catch
			{
				return false;
			}

			return false;
		}

		private static bool DoesCrewCardInfoMatchSelectionTarget(CrewCardInfo data, EntityID targetId)
		{
			if (data == null || targetId.IsNotValid)
			{
				return false;
			}

			if (data.building.IsValid && data.building == targetId)
			{
				return true;
			}
			if (data.emptyVehicle.IsValid && data.emptyVehicle == targetId)
			{
				return true;
			}
			if (data.crew.peepId.IsValid && data.crew.peepId == targetId)
			{
				return true;
			}
			return data.crew.IsValid
				&& data.crew.IsInVehicle
				&& data.crew.VehicleID.IsValid
				&& data.crew.VehicleID == targetId;
		}

		internal static void FlushDeferredCrewDialogSelectionChanges(string source)
		{
			if (PendingCrewDialogSelectionChanges.Count == 0)
			{
				return;
			}

			DeferredCrewDialogSelectionWork work = PendingCrewDialogSelectionChanges.Peek();
			if (Time.frameCount < work.EarliestFrame)
			{
				return;
			}

			long startTicks = Stopwatch.GetTimestamp();
			string outcome = "ok";
			try
			{
				if (work.Dialog == null || CrewDialogOnCurrentActiveChangedMethod == null)
				{
					outcome = "invalid";
					PendingCrewDialogSelectionChanges.Dequeue();
					return;
				}

				_flushingDeferredCrewDialogSelectionChange = true;
				CrewDialogOnCurrentActiveChangedMethod.Invoke(work.Dialog, new object[] { work.Event });
				PendingCrewDialogSelectionChanges.Dequeue();
			}
			catch (Exception ex)
			{
				outcome = ex.GetType().Name;
				PendingCrewDialogSelectionChanges.Dequeue();
			}
			finally
			{
				_flushingDeferredCrewDialogSelectionChange = false;
				if (work.ActiveId.IsValid && work.PreviousId.IsNotValid)
				{
					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"crew-dialog-selection-grace-flush-skipped active={work.ActiveId.id} previous={work.PreviousId.id} reason=queued-grace-already-set");
				}
				else
				{
					GameplayTweaksPlugin.RememberBuildingPickVisibilityGraceForCurrentSelection("crew-dialog-selection-flushed");
				}
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (!string.Equals(outcome, "ok", StringComparison.Ordinal)
					|| elapsedMs >= CrewDialogSelectionDeferredLogThresholdMs
					|| PendingCrewDialogSelectionChanges.Count > 0)
				{
					Debug.Log("[PERF][CrewDialogSelectionDeferred] flushed outcome=" + outcome + " ms=" + elapsedMs + " source=" + source + " active=" + work.ActiveId.id + " previous=" + work.PreviousId.id + " queuedFrame=" + work.QueuedFrame + " waitFrames=" + (Time.frameCount - work.QueuedFrame) + " remaining=" + PendingCrewDialogSelectionChanges.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
			}
		}

		internal static void FlushDeferredCrewDialogRefreshes(string source)
		{
			if (PendingCrewDialogRefreshes.Count == 0)
			{
				return;
			}

			DeferredCrewDialogRefreshWork work = PendingCrewDialogRefreshes.Peek();
			if (Time.frameCount < work.EarliestFrame)
			{
				return;
			}

			long startTicks = Stopwatch.GetTimestamp();
			string outcome = "ok";
			try
			{
				if (work.Dialog == null || CrewDialogRecreateAllCardsMethod == null)
				{
					outcome = "invalid";
					PendingCrewDialogRefreshes.Dequeue();
					return;
				}

				CrewDialogRecreateAllCardsMethod.Invoke(work.Dialog, null);
				PendingCrewDialogRefreshes.Dequeue();
			}
			catch (Exception ex)
			{
				outcome = ex.GetType().Name;
				PendingCrewDialogRefreshes.Dequeue();
			}
			finally
			{
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (!string.Equals(outcome, "ok", StringComparison.Ordinal)
					|| elapsedMs >= CrewDialogRefreshDeferredLogThresholdMs
					|| PendingCrewDialogRefreshes.Count > 0)
				{
					Debug.Log("[PERF][CrewDialogRefreshDeferred] flushed event=" + work.EventType + " outcome=" + outcome + " ms=" + elapsedMs + " source=" + source + " queuedFrame=" + work.QueuedFrame + " waitFrames=" + (Time.frameCount - work.QueuedFrame) + " remaining=" + PendingCrewDialogRefreshes.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
			}
		}

		private static bool ShouldDeferCrewDialogRefresh(SessionEvent sev)
		{
			if (!(global::Game.Game.ctx?.IsInteractive ?? false) || !sev.pid.IsHumanPlayer)
			{
				return false;
			}

			switch (sev.type)
			{
				case SessionEventType.PlayerAutomationReassignedSequence:
				case SessionEventType.PlayerAutomationRemovedSequence:
				case SessionEventType.PlayerBuildingTakeoverImmediate:
				case SessionEventType.PlayerBuildingClearControlImmediate:
				case SessionEventType.BuildingConstructionStateChanged:
				case SessionEventType.CrewMemberAdded:
				case SessionEventType.CrewMemberKilled:
				case SessionEventType.CrewVehicleCreated:
				case SessionEventType.CrewVehicleRemoved:
				case SessionEventType.CrewVehicleReassigned:
				case SessionEventType.CrewBuildingReassigned:
				case SessionEventType.SchemeUpdated:
					return true;
				default:
					return false;
			}
		}

		internal static void FlushDeferredTakeoverPickRefreshes(string source)
		{
			if (PendingTakeoverPickRefreshes.Count == 0)
			{
				return;
			}

			DeferredTakeoverPickRefreshWork work = PendingTakeoverPickRefreshes.Peek();
			if (Time.frameCount < work.EarliestFrame)
			{
				return;
			}

			long startTicks = Stopwatch.GetTimestamp();
			string phase = work.BuildingPickRefreshed ? "summary" : "building";
			string outcome = "ok";
			try
			{
				if (work.Manager == null || work.BuildingId.IsNotValid)
				{
					outcome = "invalid";
					PendingTakeoverPickRefreshes.Dequeue();
					return;
				}

				if (!work.BuildingPickRefreshed)
				{
					PickManagerAddOrRefreshPickMethod.Invoke(work.Manager, new object[] { PickType.BuildingPick, new PickTarget(work.BuildingId), false });
					work.BuildingPickRefreshed = true;
					work.EarliestFrame = Time.frameCount + 1;
					PendingTakeoverPickRefreshes.Dequeue();
					PendingTakeoverPickRefreshes.Enqueue(work);
					return;
				}

				PickManagerRefreshVisibleSummaryPicksMethod.Invoke(work.Manager, null);
				PendingTakeoverPickRefreshes.Dequeue();
			}
			catch (Exception ex)
			{
				outcome = ex.GetType().Name;
				PendingTakeoverPickRefreshes.Dequeue();
			}
			finally
			{
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs >= TakeoverDetailThresholdMs || PendingTakeoverPickRefreshes.Count == 0)
				{
					Debug.Log("[PERF][TakeoverPickRefreshDeferred] flushed phase=" + phase + " outcome=" + outcome + " ms=" + elapsedMs + " source=" + source + " queuedFrame=" + work.QueuedFrame + " waitFrames=" + (Time.frameCount - work.QueuedFrame) + " remaining=" + PendingTakeoverPickRefreshes.Count + " day=" + GetDayForLog() + " turn=" + GetTurnForLog());
				}
			}
		}

		private static bool ShouldLimitExistingPickRefresh(PickType type)
		{
			return type == PickType.CrewPick;
		}

		private static void SessionEventSendImmediatePostfix(SessionEvent ev, long __state)
		{
			try
			{
				if (!(global::Game.Game.ctx?.IsInteractive ?? false))
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state);
				long thresholdMs = GetHudInputLogThresholdMs();
				if (elapsedMs < thresholdMs)
				{
					return;
				}

				if (elapsedMs < 1)
				{
					return;
				}

				Debug.Log("[PERF][SessionEventImmediate] type=" + ev.type + " ms=" + elapsedMs + " pid=" + ev.pid.id + " eid=" + ev.eid.id + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
			}
		}

		private static void TimingPostfix(MethodBase __originalMethod, long __state)
		{
			try
			{
				bool isInteractive = global::Game.Game.ctx?.IsInteractive ?? false;
				bool isCityGen = global::Game.Game.ctx?.IsCityGen ?? false;
				bool isStartupStage = __originalMethod != null && StartupStageMethods.Contains(__originalMethod);
				if (!isInteractive && !isCityGen && !isStartupStage)
				{
					return;
				}

				long elapsedMs = GetElapsedMilliseconds(__state);
				long thresholdMs = isCityGen ? CityGenSlowStageThresholdMs : SlowStageThresholdMs;
				if (__originalMethod != null && ThresholdsByMethod.TryGetValue(__originalMethod, out long configuredThresholdMs))
				{
					thresholdMs = isCityGen
						? Math.Max(CityGenSlowStageThresholdMs, configuredThresholdMs)
						: configuredThresholdMs;
				}
				thresholdMs = GetEffectiveStageLogThresholdMs(thresholdMs, isCityGen);

				if (elapsedMs < thresholdMs)
				{
					return;
				}

				bool isSimulationSubmanager =
					__originalMethod?.DeclaringType?.FullName != null
					&& SimulationSubmanagerTypeNames.Contains(__originalMethod.DeclaringType.FullName);
				string phase = isCityGen ? "CityGenStage" : (isSimulationSubmanager ? "SimulationSubmanager" : (isStartupStage ? "StartupStage" : "TurnStage"));
				Debug.Log("[PERF][" + phase + "] method=" + FormatMethodName(__originalMethod) + " ms=" + elapsedMs + " day=" + GetDayForLog() + " year=" + GetYearForLog() + " turn=" + GetTurnForLog() + " pid=" + GetPlayerForLog() + " totalPlayers=" + GetTotalPlayersForLog());
			}
			catch
			{
			}
		}

		private static long GetElapsedMilliseconds(long startTicks)
		{
			return (Stopwatch.GetTimestamp() - startTicks) * 1000L / Stopwatch.Frequency;
		}

		private static string FormatMethodName(MethodBase method)
		{
			if (method == null)
			{
				return "unknown";
			}

			Type declaringType = method.DeclaringType;
			return (declaringType != null ? declaringType.Name : "unknown") + "." + method.Name;
		}

		private static int GetDayForLog()
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

		private static int GetYearForLog()
		{
			try
			{
				return global::Game.Game.ctx?.clock?.Now.YearsInt ?? -1;
			}
			catch
			{
				return -1;
			}
		}

		private static int GetTurnForLog()
		{
			try
			{
				return global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
			}
			catch
			{
				return -1;
			}
		}

		private static int GetPlayerForLog()
		{
			try
			{
				return global::Game.Game.ctx?.clock?.CurrentPlayer.id ?? -1;
			}
			catch
			{
				return -1;
			}
		}

		private static int GetTotalPlayersForLog()
		{
			try
			{
				return global::Game.Game.ctx?.players?.all?.Count ?? -1;
			}
			catch
			{
				return -1;
			}
		}
	}

	internal static class TurnPerformanceOptimizationsPatch
	{
		private static readonly Dictionary<ulong, List<(NodeID, Fixnum)>> RelationshipAverageCacheByPlayerPeep = new Dictionary<ulong, List<(NodeID, Fixnum)>>();
		private static readonly FieldInfo PeopleTrackerCreatorField = AccessTools.Field(typeof(PeopleTracker), "_creator");
		private static readonly FieldInfo PeopleTrackerOnAfterPersonBirthField = AccessTools.Field(typeof(PeopleTracker), "OnAfterPersonBirth");
		private static readonly SortedDictionary<int, List<EntityID>> CityGenBirthScheduleByDay = new SortedDictionary<int, List<EntityID>>();
		private static int _activeBusinessUpdateDepth;
		private static int _relationshipAverageCacheHits;
		private static int _relationshipAverageCacheMisses;
		private static int _lastSummaryDay = int.MinValue;
		private static object _cityGenBirthScheduleContext;
		private static bool _cityGenBirthScheduleInitialized;

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo businessTick = AccessTools.Method(typeof(BusinessUpdate), "Tick", new[] { typeof(bool) });
				MethodInfo produceAverage = AccessTools.Method(typeof(PlayerSocial), "ProduceAverageRelPerNode");
				MethodInfo processBirths = AccessTools.Method(typeof(PeopleTracker), "ProcessBirths", new[] { typeof(SimTime) });
				MethodInfo processMarriages = AccessTools.Method(typeof(PeopleTracker), "ProcessMarriages", new[] { typeof(SimTime) });
				if (businessTick != null)
				{
					harmony.Patch(
						businessTick,
						prefix: new HarmonyMethod(typeof(TurnPerformanceOptimizationsPatch), nameof(BusinessUpdateTickPrefix)),
						finalizer: new HarmonyMethod(typeof(TurnPerformanceOptimizationsPatch), nameof(BusinessUpdateTickFinalizer)));
				}

				if (produceAverage != null)
				{
					harmony.Patch(
						produceAverage,
						prefix: new HarmonyMethod(typeof(TurnPerformanceOptimizationsPatch), nameof(ProduceAverageRelPerNodePrefix)),
						postfix: new HarmonyMethod(typeof(TurnPerformanceOptimizationsPatch), nameof(ProduceAverageRelPerNodePostfix)));
				}

				if (processMarriages != null)
				{
					harmony.Patch(
						processMarriages,
						prefix: new HarmonyMethod(typeof(TurnPerformanceOptimizationsPatch), nameof(ProcessMarriagesPrefix)));
				}

				if (processBirths != null)
				{
					harmony.Patch(
						processBirths,
						prefix: new HarmonyMethod(typeof(TurnPerformanceOptimizationsPatch), nameof(ProcessBirthsPrefix)));
				}

				Debug.Log("[PERF][TurnStage] optimizations-applied businessTick=" + (businessTick != null) + " relationshipAverageCache=" + (produceAverage != null) + " cityGenBirthScheduling=" + (processBirths != null) + " cityGenMarriageMatching=" + (processMarriages != null));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Turn performance optimization patch setup failed: " + ex.Message);
			}
		}

		private static void BusinessUpdateTickPrefix()
		{
			if (_activeBusinessUpdateDepth == 0)
			{
				RelationshipAverageCacheByPlayerPeep.Clear();
				_relationshipAverageCacheHits = 0;
				_relationshipAverageCacheMisses = 0;
			}

			_activeBusinessUpdateDepth++;
		}

		internal static void BeginBusinessUpdateTickScope()
		{
			BusinessUpdateTickPrefix();
		}

		internal static void EndBusinessUpdateTickScope()
		{
			BusinessUpdateTickFinalizer(null);
		}

		private static Exception BusinessUpdateTickFinalizer(Exception __exception)
		{
			_activeBusinessUpdateDepth = Math.Max(0, _activeBusinessUpdateDepth - 1);
			if (_activeBusinessUpdateDepth == 0)
			{
				LogRelationshipAverageCacheSummary();
				RelationshipAverageCacheByPlayerPeep.Clear();
			}

			return __exception;
		}

		private static bool ProduceAverageRelPerNodePrefix(PlayerSocial __instance, List<(NodeID, Fixnum)> results, out bool __state)
		{
			__state = false;
			if (_activeBusinessUpdateDepth <= 0 || __instance == null || results == null)
			{
				return true;
			}

			ulong key = __instance.PlayerPeepId.id;
			if (key == 0)
			{
				return true;
			}

			if (!RelationshipAverageCacheByPlayerPeep.TryGetValue(key, out List<(NodeID, Fixnum)> cached))
			{
				__state = true;
				_relationshipAverageCacheMisses++;
				return true;
			}

			results.Clear();
			results.AddRange(cached);
			_relationshipAverageCacheHits++;
			return false;
		}

		private static void ProduceAverageRelPerNodePostfix(PlayerSocial __instance, List<(NodeID, Fixnum)> results, bool __state)
		{
			if (!__state || _activeBusinessUpdateDepth <= 0 || __instance == null || results == null)
			{
				return;
			}

			ulong key = __instance.PlayerPeepId.id;
			if (key == 0)
			{
				return;
			}

			RelationshipAverageCacheByPlayerPeep[key] = new List<(NodeID, Fixnum)>(results);
		}

		private static bool ProcessBirthsPrefix(PeopleTracker __instance, SimTime now, ref int __result)
		{
			if (!(global::Game.Game.ctx?.IsCityGen ?? false))
			{
				ResetCityGenBirthScheduleIfNeeded();
				return true;
			}

			try
			{
				PersonCreator creator = PeopleTrackerCreatorField?.GetValue(__instance) as PersonCreator;
				RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
				Listeners<Entity> birthListeners = PeopleTrackerOnAfterPersonBirthField?.GetValue(__instance) as Listeners<Entity>;
				if (__instance == null || creator == null || rels == null)
				{
					return true;
				}

				EnsureCityGenBirthSchedule(__instance);
				int processed = 0;
				List<Entity> parentsToReschedule = new List<Entity>();
				while (CityGenBirthScheduleByDay.Count > 0)
				{
					if (!TryGetFirstScheduledBirth(out int dueDay, out List<EntityID> dueParents))
					{
						break;
					}

					if (dueDay >= now.days)
					{
						break;
					}

					EntityID parentId = dueParents[dueParents.Count - 1];
					dueParents.RemoveAt(dueParents.Count - 1);
					if (dueParents.Count == 0)
					{
						CityGenBirthScheduleByDay.Remove(dueDay);
					}

					Entity parent = parentId.FindEntity();
					List<SimTime> futureKids = parent?.data?.person?.futurekids;
					if (futureKids == null || futureKids.Count == 0)
					{
						continue;
					}

					SimTime birthDate = futureKids.LastOrDefaultFast();
					if (birthDate.days != dueDay || birthDate.days >= now.days)
					{
						parentsToReschedule.Add(parent);
						continue;
					}

					futureKids.RemoveLast();
					Entity spouse = rels.GetListOrNull(parent.Id)?.GetSpouse();
					if (spouse?.data?.person != null)
					{
						Entity child = creator.CreateKid(spouse, parent, birthDate);
						birthListeners?.Invoke(child);
						processed++;
					}

					parentsToReschedule.Add(parent);
				}

				foreach (Entity parent in parentsToReschedule)
				{
					ScheduleNextCityGenBirth(parent);
				}

				__result = processed;
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CityGen birth scheduling optimization failed; falling back to vanilla. " + ex.GetType().Name + ":" + ex.Message);
				ResetCityGenBirthScheduleIfNeeded();
				return true;
			}
		}

		private static bool ProcessMarriagesPrefix(PeopleTracker __instance, SimTime now, ref int __result)
		{
			if (!(global::Game.Game.ctx?.IsCityGen ?? false))
			{
				return true;
			}

			try
			{
				PersonCreator creator = PeopleTrackerCreatorField?.GetValue(__instance) as PersonCreator;
				RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
				if (__instance?.data == null || creator == null || rels == null || global::Game.Game.ctx?.session?.mapconfig == null)
				{
					return true;
				}

				List<Entity> due = new List<Entity>(256);
				Dictionary<int, List<Entity>> bachelorBucketsByBirthYear = new Dictionary<int, List<Entity>>();
				float minAge = global::Game.Game.ctx.session.mapconfig.familyGenerator.marriageAgeRange.from;
				foreach (Entity peep in __instance.GetAllTrackedPeople() ?? Array.Empty<Entity>())
				{
					PersonData person = peep?.data?.person;
					if (person == null)
					{
						continue;
					}

					if (person.futuremarriage.days < now.days)
					{
						due.Add(peep);
					}

					if (person.g != Gender.M || person.GetAge(now).YearsFloat < minAge)
					{
						continue;
					}

					RelationshipList candidateRels = rels.GetListOrNull(peep.Id);
					if (candidateRels != null && candidateRels.HasSpouse())
					{
						continue;
					}

					int birthYear = person.born.YearsInt;
					if (!bachelorBucketsByBirthYear.TryGetValue(birthYear, out List<Entity> bucket))
					{
						bucket = new List<Entity>();
						bachelorBucketsByBirthYear[birthYear] = bucket;
					}

					bucket.Add(peep);
				}

				if (due.Count == 0)
				{
					__result = 0;
					return false;
				}

				due.Sort(Entity.Comparison);
				List<Entity> candidates = new List<Entity>(256);
				List<float> scores = new List<float>(256);
				HashSet<ulong> marriedMaleIds = new HashSet<ulong>();
				foreach (Entity spouse in due)
				{
					PersonData spouseData = spouse?.data?.person;
					if (spouseData == null)
					{
						continue;
					}

					spouseData.futuremarriage = SimTime.MAX_DATE;
					RelationshipList spouseRels = rels.GetListOrNull(spouse.Id);
					if (spouseRels != null && spouseRels.HasSpouse())
					{
						continue;
					}

					candidates.Clear();
					scores.Clear();
					int spouseBirthYear = spouseData.born.YearsInt;
					for (int year = spouseBirthYear - 10; year <= spouseBirthYear + 10; year++)
					{
						if (!bachelorBucketsByBirthYear.TryGetValue(year, out List<Entity> bucket))
						{
							continue;
						}

						foreach (Entity candidate in bucket)
						{
							if (candidate == null || marriedMaleIds.Contains(candidate.Id.id))
							{
								continue;
							}

							if (spouseRels != null && spouseRels.HasAny(candidate.Id))
							{
								continue;
							}

							PersonData candidateData = candidate.data?.person;
							if (candidateData == null)
							{
								continue;
							}

							float ageDiff = Math.Abs(spouseData.born.Subtract(candidateData.born).YearsFloat);
							float ageScore = Math.Max(10f - ageDiff, 0f);
							if (ageScore <= 0f)
							{
								continue;
							}

							candidates.Add(candidate);
							scores.Add((spouseData.eth == candidateData.eth ? 3f : 1f) * ageScore);
						}
					}

					if (candidates.Count == 0)
					{
						continue;
					}

					Entity match = __instance.data.rng.PickElement(candidates, scores);
					if (match == null)
					{
						continue;
					}

					int futureKidCount = __instance.data.rng.PickElement(global::Game.Game.ctx.session.mapconfig.familyGenerator.kidsCount);
					creator.LinkCouple(match, spouse, now, futureKidCount);
					ScheduleNextCityGenBirth(spouse);
					marriedMaleIds.Add(match.Id.id);
				}

				__result = due.Count;
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CityGen marriage matching optimization failed; falling back to vanilla. " + ex.GetType().Name + ":" + ex.Message);
				return true;
			}
		}

		private static void EnsureCityGenBirthSchedule(PeopleTracker people)
		{
			object ctx = global::Game.Game.ctx;
			if (_cityGenBirthScheduleInitialized && ReferenceEquals(_cityGenBirthScheduleContext, ctx))
			{
				return;
			}

			CityGenBirthScheduleByDay.Clear();
			_cityGenBirthScheduleContext = ctx;
			_cityGenBirthScheduleInitialized = true;
			foreach (Entity person in people.GetAllTrackedPeople() ?? Array.Empty<Entity>())
			{
				ScheduleNextCityGenBirth(person);
			}
		}

		private static void ScheduleNextCityGenBirth(Entity parent)
		{
			List<SimTime> futureKids = parent?.data?.person?.futurekids;
			if (parent == null || futureKids == null || futureKids.Count == 0)
			{
				return;
			}

			int day = futureKids.LastOrDefaultFast().days;
			if (!CityGenBirthScheduleByDay.TryGetValue(day, out List<EntityID> parentIds))
			{
				parentIds = new List<EntityID>();
				CityGenBirthScheduleByDay[day] = parentIds;
			}

			parentIds.Add(parent.Id);
		}

		private static bool TryGetFirstScheduledBirth(out int day, out List<EntityID> parentIds)
		{
			foreach (KeyValuePair<int, List<EntityID>> entry in CityGenBirthScheduleByDay)
			{
				day = entry.Key;
				parentIds = entry.Value;
				return true;
			}

			day = 0;
			parentIds = null;
			return false;
		}

		private static void ResetCityGenBirthScheduleIfNeeded()
		{
			if (!_cityGenBirthScheduleInitialized)
			{
				return;
			}

			CityGenBirthScheduleByDay.Clear();
			_cityGenBirthScheduleContext = null;
			_cityGenBirthScheduleInitialized = false;
		}

		private static void LogRelationshipAverageCacheSummary()
		{
			bool performanceDiagnosticsEnabled;
			try
			{
				performanceDiagnosticsEnabled = GameplayTweaksPlugin.EnablePerformanceDiagnostics?.Value ?? false;
			}
			catch
			{
				performanceDiagnosticsEnabled = false;
			}

			if (_relationshipAverageCacheHits <= 0 || !performanceDiagnosticsEnabled)
			{
				return;
			}

			int day = GetDayForLog();
			if (day == _lastSummaryDay)
			{
				return;
			}

			_lastSummaryDay = day;
			Debug.Log("[PERF][TurnStage] relationship-average-cache hits=" + _relationshipAverageCacheHits + " misses=" + _relationshipAverageCacheMisses + " players=" + RelationshipAverageCacheByPlayerPeep.Count + " day=" + day);
		}

		private static int GetDayForLog()
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
	}
}

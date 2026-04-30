using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Game.Core;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Board;
using Game.Session.Heatmaps;
using Game.Session.Setup;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Picks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace GameOptimizer
{
    // Helper to access Game.Game.ctx and Game.Game.serv via reflection,
    // since "Game" is both a namespace and a class.
    internal static class G
    {
        private static readonly Type GameType;
        private static readonly FieldInfo CtxField;

        static G()
        {
            GameType = typeof(GameClock).Assembly.GetType("Game.Game");
            if (GameType != null)
                CtxField = GameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static);
        }

        public static dynamic ctx => CtxField?.GetValue(null);

        public static SessionContext SessionContext => CtxField?.GetValue(null) as SessionContext;
    }

    [BepInPlugin("com.mods.gameoptimizer", "Game Optimizer", "1.0.0")]
    public class GameOptimizerPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableClockReset;
        internal static ConfigEntry<float> ClockResetThreshold;
        internal static ConfigEntry<bool> EnableGamblingSkip;
        internal static ConfigEntry<bool> EnableGoonBFSSkip;
        internal static ConfigEntry<bool> EnablePickManagerThrottle;
        internal static ConfigEntry<float> PickManagerThrottleSeconds;
        internal static ConfigEntry<bool> EnableRelationshipCache;
        internal static ConfigEntry<bool> EnablePickManagerNodeCache;
        internal static ConfigEntry<bool> EnableHeatRespectThrottle;
        internal static ConfigEntry<int> HeatRespectInterval;
        internal static ConfigEntry<bool> EnableHeatPropagationThrottle;
        internal static ConfigEntry<int> HeatPropagationInterval;
        internal static ConfigEntry<bool> EnableAICrewLevelups;
        internal static ConfigEntry<int> AILevelupMaxPerCrewPerTurn;
        internal static ConfigEntry<bool> EnableLoadTimings;
        internal static ConfigEntry<bool> EnableLoadAcceleration;
        internal static ConfigEntry<int> LoadAccelerationBatchSize;
        internal static ConfigEntry<bool> LoadAccelerationLogSteps;
        internal static ConfigEntry<bool> EnableHeightmapOptimization;
        internal static ConfigEntry<int> HeightmapResolution;
        internal static ConfigEntry<bool> EnableTerrainDecoOptimization;
        internal static ConfigEntry<int> TerrainDecoResolution;
        internal static ConfigEntry<bool> EnableYieldBatching;
        internal static ConfigEntry<int> YieldBatchSize;
        internal static ConfigEntry<bool> EnableTerrainMeshOptimization;
        internal static ConfigEntry<int> TerrainMeshChunkBatch;
        internal static ConfigEntry<int> TerrainMeshTileVerts;
        internal static ConfigEntry<bool> EnableTerrainSingleSample;
        internal static ConfigEntry<int> TerrainMeshChunkSize;
        internal static ConfigEntry<bool> EnableAssignBuildingsOptimization;
        internal static ConfigEntry<int> AssignBuildingsIterationsPerFrame;
        internal static ConfigEntry<bool> EnableEthnicityPlacementFix;
        internal static ConfigEntry<bool> EnableManufactureModuleFix;
        internal static ConfigEntry<bool> EnableCreateEmptyLotsOptimization;
        internal static ConfigEntry<int> CreateEmptyLotsIterationsPerFrame;
        internal static ConfigEntry<bool> EnableCreateMapNodesOptimization;
        internal static ConfigEntry<int> CreateMapNodesIterationsPerFrame;
        internal static ConfigEntry<bool> EnableMapDisplayOptimization;
        internal static ConfigEntry<int> MapDisplayResolution;
        internal static ConfigEntry<int> MapDisplayBatchSize;
        internal static ConfigEntry<bool> EnableFogRefreshOptimization;
        internal static ConfigEntry<int> FogRefreshNodesPerYield;
        internal static ConfigEntry<bool> PreserveWorldTerrainFidelity;
        internal static ConfigEntry<bool> DisableTerritoryOverlay;
        internal static ConfigEntry<bool> EnableIndirectRendererOptimization;
        internal static ConfigEntry<bool> EnableTerritoryRebuildDebounce;
        internal static ConfigEntry<bool> EnableClockResetOnLoad;
        internal static ConfigEntry<bool> EnableDeferredInteractiveMemoryCleanup;
        internal static ConfigEntry<float> DeferredInteractiveMemoryCleanupDelaySeconds;
        internal static ConfigEntry<bool> UnloadUnusedAssetsDuringInteractiveCleanup;
        internal static ConfigEntry<bool> WaitForPendingFinalizersDuringDeferredCleanup;
        internal static ConfigEntry<bool> EnableAdaptiveLargeMapTuning;
        internal static ConfigEntry<int> AdaptiveLargeMapAreaThreshold;
        internal static ConfigEntry<int> AdaptiveLargeMapBatchMultiplier;
        internal static ConfigEntry<int> AdaptiveLargeMapHeightmapResolution;
        internal static ConfigEntry<int> AdaptiveLargeMapTerrainDecoResolution;
        internal static ConfigEntry<int> AdaptiveLargeMapMapDisplayResolution;
        internal static ConfigEntry<float> AdaptiveRuntimeThrottleMultiplier;
        internal static ConfigEntry<bool> EnableVerboseLogs;

        private void Awake()
        {
            EnableClockReset = Config.Bind("ClockReset", "Enabled", true,
                "Reset cumulativeSeconds periodically to prevent float precision degradation.");
            ClockResetThreshold = Config.Bind("ClockReset", "Threshold", 600f,
                "Reset cumulativeSeconds when it exceeds this value.");

            EnableGamblingSkip = Config.Bind("GamblingSkip", "Enabled", true,
                "Skip gambling module update for players with no gambling houses.");
            EnableGoonBFSSkip = Config.Bind("GoonBFSSkip", "Enabled", true,
                "Skip expensive BFS pathfinding when goon has no harassment planned.");
            EnablePickManagerThrottle = Config.Bind("PickManagerThrottle", "Enabled", true,
                "Throttle PickManager.OnPlayerKBChanged to reduce lag spikes.");
            PickManagerThrottleSeconds = Config.Bind("PickManagerThrottle", "IntervalSeconds", 0.1f,
                "Minimum seconds between OnPlayerKBChanged calls.");

            EnableRelationshipCache = Config.Bind("RelationshipCache", "Enabled", false,
                "Cache Relationship.Evaluate() results per turn. DISABLED BY DEFAULT: Harmony cannot patch this method due to nested struct return type (Relationship.Value).");
            EnablePickManagerNodeCache = Config.Bind("PickManagerNodeCache", "Enabled", false,
                "Cache known nodes for PickManager.RefreshPicksOnKnownBuildings. DISABLED BY DEFAULT: Harmony cannot generate DMD for this method due to type resolution issues.");

            EnableHeatRespectThrottle = Config.Bind("HeatRespectThrottle", "Enabled", false,
                "Only recalculate heat/respect every N turns instead of every turn.");
            HeatRespectInterval = Config.Bind("HeatRespectThrottle", "TurnInterval", 3,
                "Recalculate heat/respect every N turns.");

            EnableHeatPropagationThrottle = Config.Bind("HeatPropagationThrottle", "Enabled", false,
                "Only propagate heat from neighbors every N turns.");
            HeatPropagationInterval = Config.Bind("HeatPropagationThrottle", "TurnInterval", 2,
                "Propagate heat every N turns.");

            EnableAICrewLevelups = Config.Bind("AICrewLevelups", "Enabled", true,
                "Allow AI crew members to automatically level up when they have enough XP.");
            AILevelupMaxPerCrewPerTurn = Config.Bind("AICrewLevelups", "MaxLevelupsPerCrewPerTurn", 3,
                "Maximum levelups a single AI crew member can gain per turn.");

            EnableLoadTimings = Config.Bind("LoadTimings", "Enabled", false,
                "Log stopwatch timings for each map loading step. DISABLED BY DEFAULT: Can cause TypeLoadException on some game versions.");
            EnableLoadAcceleration = Config.Bind("LoadAcceleration", "Enabled", true,
                "Enable balanced load acceleration for new game and save load initialization.");
            LoadAccelerationBatchSize = Config.Bind("LoadAcceleration", "BatchSize", 8,
                "Balanced null-yield batch size used during accelerated board initialization load wrapping.");
            LoadAccelerationLogSteps = Config.Bind("LoadAcceleration", "LogSteps", false,
                "When enabled, logs detailed per-step load timings. Keep OFF for best speed.");

            EnableHeightmapOptimization = Config.Bind("HeightmapOptimization", "Enabled", true,
                "Generate heightmap at lower resolution to speed up map loading.");
            HeightmapResolution = Config.Bind("HeightmapOptimization", "Resolution", 384,
                "Heightmap resolution (original is 512). Lower = faster. 384 preserves river connection detail while still being ~1.8x faster.");

            EnableTerrainDecoOptimization = Config.Bind("TerrainDecoOptimization", "Enabled", true,
                "Reduce terrain deco grid resolution to speed up FillInEmptySpace during map loading.");
            TerrainDecoResolution = Config.Bind("TerrainDecoOptimization", "Resolution", 128,
                "Terrain deco grid resolution (original is 512). Lower = faster. 128 cuts work to ~6% of original.");

            EnableYieldBatching = Config.Bind("YieldBatching", "Enabled", true,
                "Batch null yields during map loading to reduce frame-wait overhead. Each null yield costs ~16ms.");
            YieldBatchSize = Config.Bind("YieldBatching", "BatchSize", 10,
                "Process this many null yields before actually yielding to Unity. Higher = faster load, less responsive loading screen.");

            EnableTerrainMeshOptimization = Config.Bind("TerrainMeshOptimization", "Enabled", true,
                "Reduce frame-wait yields during terrain mesh generation. Original yields every 5 chunks.");
            TerrainMeshChunkBatch = Config.Bind("TerrainMeshOptimization", "ChunkBatchSize", 50,
                "Generate this many terrain chunks before yielding a frame. Higher = faster, less responsive. 0 = no yields at all.");
            TerrainMeshTileVerts = Config.Bind("TerrainMeshOptimization", "TileVerts", 1,
                "Vertex density per tile (original is set by map config, typically 3-4). Lower = fewer vertices = faster. 1 = minimum detail. 0 = don't change.");
            EnableTerrainSingleSample = Config.Bind("TerrainMeshOptimization", "SingleSampleHeightmap", true,
                "Use single heightmap sample instead of 3x3 kernel per vertex. 9x fewer texture lookups with minimal visual difference.");
            TerrainMeshChunkSize = Config.Bind("TerrainMeshOptimization", "ChunkSize", 600,
                "Terrain chunk size in tiles. Larger = fewer chunks = less Unity overhead (uses 32-bit mesh indices). Original is map-defined (typically 40). 0 = don't change.");

            EnableAssignBuildingsOptimization = Config.Bind("AssignBuildingsOptimization", "Enabled", true,
                "Increase lot processing batch size in AssignBuildingsToLots to reduce frame-wait overhead during map loading.");
            AssignBuildingsIterationsPerFrame = Config.Bind("AssignBuildingsOptimization", "IterationsPerFrame", 50000,
                "Process this many lots/edges before yielding a frame. Original is 1000 (~16ms wasted per yield). Set to 0 for no yields.");

            EnableEthnicityPlacementFix = Config.Bind("EthnicityPlacementFix", "Enabled", true,
                "Fix ethnicity placement bias: when ethnic heatmaps are empty/tied, pick randomly instead of always choosing the first alphabetical ethnicity. Prevents one group from clustering near industrial zones.");

            EnableManufactureModuleFix = Config.Bind("ManufactureModuleFix", "Enabled", true,
                "Fix crash when AI installs backroom modules. The game incorrectly checks recipe visibility against HumanPlayer instead of the AI player, causing empty recipe lists.");

            EnableCreateEmptyLotsOptimization = Config.Bind("CreateEmptyLotsOptimization", "Enabled", true,
                "Batch null yields during CreateEmptyLots.MakeLotsAxisAligned to reduce frame-wait overhead during map loading.");
            CreateEmptyLotsIterationsPerFrame = Config.Bind("CreateEmptyLotsOptimization", "IterationsPerFrame", 50000,
                "Process this many iterations before yielding a frame.");

            EnableCreateMapNodesOptimization = Config.Bind("CreateMapNodesOptimization", "Enabled", true,
                "Batch null yields during CreateMapNodes.Start to reduce frame-wait overhead during map loading.");
            CreateMapNodesIterationsPerFrame = Config.Bind("CreateMapNodesOptimization", "IterationsPerFrame", 50000,
                "Process this many iterations before yielding a frame.");

            PreserveWorldTerrainFidelity = Config.Bind("VisualFidelity", "PreserveWorldTerrainFidelity", true,
                "Keep vanilla terrain, map-generation, and texture fidelity. Disables optimizer visual simplifications while preserving non-visual batching where possible.");
            DisableTerritoryOverlay = Config.Bind("VisualFidelity", "DisableTerritoryOverlay", true,
                "Disable the territory overlay texture entirely. Keeps world terrain fidelity intact and skips overlay rendering work.");

            EnableMapDisplayOptimization = Config.Bind("MapDisplayOptimization", "Enabled", true,
                "Reduce territory map display resolution and batch refresh coroutine to improve performance.");
            MapDisplayResolution = Config.Bind("MapDisplayOptimization", "Resolution", 256,
                "Territory map texture resolution (original is 1024). Lower = faster territory updates.");
            MapDisplayBatchSize = Config.Bind("MapDisplayOptimization", "BatchSize", 16000,
                "Process this many pixels before yielding during territory refresh.");
            EnableFogRefreshOptimization = Config.Bind("FogOfWarRefresh", "Enabled", true,
                "Batch known-node reveals during full fog refresh to avoid one frame wait per node on save load.");
            FogRefreshNodesPerYield = Config.Bind("FogOfWarRefresh", "NodesPerYield", 48,
                "Reveal this many known nodes before yielding during a full fog refresh.");

            EnableIndirectRendererOptimization = Config.Bind("IndirectRendererOptimization", "Enabled", true,
                "Force IndirectRenderer to separate culling over frames to reduce per-frame overhead.");

            EnableTerritoryRebuildDebounce = Config.Bind("TerritoryRebuildDebounce", "Enabled", true,
                "Debounce territory rebuild calls to prevent redundant rebuilds within the same frame.");

            EnableClockResetOnLoad = Config.Bind("ClockResetOnLoad", "Enabled", true,
                "Reset animation clock immediately after loading a save to prevent float precision lag.");
            EnableDeferredInteractiveMemoryCleanup = Config.Bind("InteractiveMemoryCleanup", "Enabled", true,
                "Defer GC and asset cleanup after the board becomes interactive so the first playable frames do not hitch.");
            DeferredInteractiveMemoryCleanupDelaySeconds = Config.Bind("InteractiveMemoryCleanup", "DelaySeconds", 10f,
                "Seconds to wait after gameplay becomes interactive before reclaiming memory.");
            UnloadUnusedAssetsDuringInteractiveCleanup = Config.Bind("InteractiveMemoryCleanup", "UnloadUnusedAssets", false,
                "Run Resources.UnloadUnusedAssets during deferred interactive cleanup. OFF by default because it can cause ~1s runtime hitches on live boards.");
            WaitForPendingFinalizersDuringDeferredCleanup = Config.Bind("InteractiveMemoryCleanup", "WaitForPendingFinalizers", false,
                "Wait for pending finalizers during deferred cleanup. Off by default to avoid extra stalls.");

            EnableAdaptiveLargeMapTuning = Config.Bind("AdaptiveLargeMapTuning", "Enabled", true,
                "Auto-apply more aggressive optimization values on very large maps.");
            AdaptiveLargeMapAreaThreshold = Config.Bind("AdaptiveLargeMapTuning", "AreaThreshold", 120000,
                "Map area threshold (width*height) to trigger large-map tuning.");
            AdaptiveLargeMapBatchMultiplier = Config.Bind("AdaptiveLargeMapTuning", "BatchMultiplier", 2,
                "Multiplier for heavy coroutine batch sizes when large-map tuning is active.");
            AdaptiveLargeMapHeightmapResolution = Config.Bind("AdaptiveLargeMapTuning", "HeightmapResolutionCap", 320,
                "Maximum heightmap resolution to use on large maps.");
            AdaptiveLargeMapTerrainDecoResolution = Config.Bind("AdaptiveLargeMapTuning", "TerrainDecoResolutionCap", 96,
                "Maximum terrain deco resolution to use on large maps.");
            AdaptiveLargeMapMapDisplayResolution = Config.Bind("AdaptiveLargeMapTuning", "MapDisplayResolutionCap", 224,
                "Maximum territory map resolution to use on large maps.");
            AdaptiveRuntimeThrottleMultiplier = Config.Bind("AdaptiveLargeMapTuning", "RuntimeThrottleMultiplier", 1.35f,
                "Multiplier for runtime input throttles on large maps (1.0 = unchanged).");

            EnableVerboseLogs = Config.Bind("Diagnostics", "VerboseLogs", false,
                "Enable detailed optimizer diagnostics in Unity log. Keep OFF for best performance.");

            var harmony = new Harmony("com.mods.gameoptimizer");
            harmony.PatchAll(typeof(ClockResetPatch));
            // GamblingSkipPatch disabled: Harmony cannot generate DMD for UpdateGamblingModules (TypeLoadException)
            // harmony.PatchAll(typeof(GamblingSkipPatch));
            harmony.PatchAll(typeof(GoonBFSSkipPatch));
            harmony.PatchAll(typeof(PickManagerThrottlePatch));
            // RelationshipCachePatch and PickManagerNodeCachePatch disabled:
            // Harmony cannot generate DMDs for these methods due to type resolution issues in Unity Mono
            harmony.PatchAll(typeof(HeatRespectThrottlePatch));
            harmony.PatchAll(typeof(HeatPropagationThrottlePatch));
            harmony.PatchAll(typeof(AICrewLevelupPatch));
            harmony.PatchAll(typeof(LoadTimingPatch));
            HeightmapOptimizationPatch.ApplyManualPatch(harmony);
            TerrainDecoOptimizationPatch.ApplyManualPatch(harmony);
            TerrainMeshOptimizationPatch.ApplyManualPatch(harmony);
            AssignBuildingsOptimizationPatch.ApplyManualPatch(harmony);
            ManufactureModuleFixPatch.ApplyManualPatch(harmony);
            CreateEmptyLotsOptimizationPatch.ApplyManualPatch(harmony);
            CreateMapNodesOptimizationPatch.ApplyManualPatch(harmony);
            MapDisplayOptimizationPatch.ApplyManualPatch(harmony);
            FogOfWarRefreshOptimizationPatch.ApplyManualPatch(harmony);
            IndirectRendererOptimizationPatch.ApplyManualPatch(harmony);
            TerritoryRebuildDebouncePatch.ApplyManualPatch(harmony);
            ClockResetOnLoadPatch.ApplyManualPatch(harmony);
            InteractiveMemoryCleanupPatch.ApplyManualPatch(harmony);
            TerrainGenDataNullGuardPatch.ApplyManualPatch(harmony);
            DensityHeatmapNullGuardPatch.ApplyManualPatch(harmony);
            // Ethnicity fix uses a coroutine instead of Harmony (DMD failures)
            if (EnableEthnicityPlacementFix.Value)
            {
                _ethnicityFixHasRun = false;
                StartCoroutine(EthnicityPlacementFixCoroutine());
            }

            Logger.LogInfo("Game Optimizer loaded. Optimizations:");
            Logger.LogInfo($"  ClockReset: {EnableClockReset.Value}");
            Logger.LogInfo($"  GamblingSkip: {EnableGamblingSkip.Value}");
            Logger.LogInfo($"  GoonBFSSkip: {EnableGoonBFSSkip.Value}");
            Logger.LogInfo($"  PickManagerThrottle: {EnablePickManagerThrottle.Value}");
            Logger.LogInfo($"  RelationshipCache: {EnableRelationshipCache.Value}");
            Logger.LogInfo($"  PickManagerNodeCache: {EnablePickManagerNodeCache.Value}");
            Logger.LogInfo($"  HeatRespectThrottle: {EnableHeatRespectThrottle.Value}");
            Logger.LogInfo($"  HeatPropagationThrottle: {EnableHeatPropagationThrottle.Value}");
            Logger.LogInfo($"  AICrewLevelups: {EnableAICrewLevelups.Value}");
            Logger.LogInfo($"  LoadTimings: {EnableLoadTimings.Value}");
            Logger.LogInfo($"  LoadAcceleration: {EnableLoadAcceleration.Value} (batch={LoadAccelerationBatchSize.Value}, logSteps={LoadAccelerationLogSteps.Value})");
            Logger.LogInfo($"  HeightmapOptimization: {EnableHeightmapOptimization.Value} (res={HeightmapResolution.Value})");
            Logger.LogInfo($"  TerrainDecoOptimization: {EnableTerrainDecoOptimization.Value} (res={TerrainDecoResolution.Value})");
            Logger.LogInfo($"  YieldBatching: {EnableYieldBatching.Value} (batch={YieldBatchSize.Value})");
            Logger.LogInfo($"  TerrainMeshOptimization: {EnableTerrainMeshOptimization.Value} (chunkBatch={TerrainMeshChunkBatch.Value}, tileVerts={TerrainMeshTileVerts.Value}, singleSample={EnableTerrainSingleSample.Value})");
            Logger.LogInfo($"  AssignBuildingsOptimization: {EnableAssignBuildingsOptimization.Value} (itersPerFrame={AssignBuildingsIterationsPerFrame.Value})");
            Logger.LogInfo($"  EthnicityPlacementFix: {EnableEthnicityPlacementFix.Value}");
            Logger.LogInfo($"  ManufactureModuleFix: {EnableManufactureModuleFix.Value}");
            Logger.LogInfo($"  CreateEmptyLotsOptimization: {EnableCreateEmptyLotsOptimization.Value} (itersPerFrame={CreateEmptyLotsIterationsPerFrame.Value})");
            Logger.LogInfo($"  CreateMapNodesOptimization: {EnableCreateMapNodesOptimization.Value} (itersPerFrame={CreateMapNodesIterationsPerFrame.Value})");
            Logger.LogInfo($"  PreserveWorldTerrainFidelity: {PreserveWorldTerrainFidelity.Value}");
            Logger.LogInfo($"  DisableTerritoryOverlay: {DisableTerritoryOverlay.Value}");
            Logger.LogInfo($"  MapDisplayOptimization: {EnableMapDisplayOptimization.Value} (res={MapDisplayResolution.Value}, batch={MapDisplayBatchSize.Value})");
            Logger.LogInfo($"  FogOfWarRefreshOptimization: {EnableFogRefreshOptimization.Value} (nodesPerYield={FogRefreshNodesPerYield.Value})");
            Logger.LogInfo($"  IndirectRendererOptimization: {EnableIndirectRendererOptimization.Value}");
            Logger.LogInfo($"  TerritoryRebuildDebounce: {EnableTerritoryRebuildDebounce.Value}");
            Logger.LogInfo($"  ClockResetOnLoad: {EnableClockResetOnLoad.Value}");
            Logger.LogInfo($"  InteractiveMemoryCleanup: {EnableDeferredInteractiveMemoryCleanup.Value} (delay={DeferredInteractiveMemoryCleanupDelaySeconds.Value:0.##}s, unloadUnusedAssets={UnloadUnusedAssetsDuringInteractiveCleanup.Value}, waitForFinalizers={WaitForPendingFinalizersDuringDeferredCleanup.Value})");
            Logger.LogInfo($"  AdaptiveLargeMapTuning: {EnableAdaptiveLargeMapTuning.Value} (threshold={AdaptiveLargeMapAreaThreshold.Value}, batchX={AdaptiveLargeMapBatchMultiplier.Value})");
            Logger.LogInfo($"  RuntimeThrottleMultiplier: {AdaptiveRuntimeThrottleMultiplier.Value}");
            Logger.LogInfo($"  VerboseLogs: {EnableVerboseLogs.Value}");
        }

        private static void LogVerbose(string message)
        {
            if (EnableVerboseLogs != null && EnableVerboseLogs.Value)
                Debug.Log(message);
        }

        private static void ResetRuntimeOptimizations()
        {
            PickManagerThrottlePatch.ResetRuntime();
            HeatRespectThrottlePatch.ResetRuntime();
            HeatPropagationThrottlePatch.ResetRuntime();
            TerritoryRebuildDebouncePatch.ResetRuntime();
            IndirectRendererOptimizationPatch.ResetRuntime();
        }

        private static int TryGetMapArea()
        {
            try
            {
                object ctx = G.ctx;
                if (ctx == null) return 0;
                object session = AccessTools.Field(ctx.GetType(), "session")?.GetValue(ctx);
                if (session == null) return 0;
                object mapconfig = AccessTools.Property(session.GetType(), "mapconfig")?.GetValue(session);
                if (mapconfig == null) return 0;
                object map = AccessTools.Field(mapconfig.GetType(), "map")?.GetValue(mapconfig);
                if (map == null) return 0;
                object mapSizeObj = AccessTools.Field(map.GetType(), "mapSize")?.GetValue(map);
                if (mapSizeObj is IntSize mapSize)
                {
                    long area = (long)mapSize.width * mapSize.height;
                    return area > int.MaxValue ? int.MaxValue : (int)Math.Max(0, area);
                }
            }
            catch
            {
            }
            return 0;
        }

        private static bool IsLargeMapTuningActive()
        {
            if (EnableAdaptiveLargeMapTuning == null || !EnableAdaptiveLargeMapTuning.Value)
                return false;
            int area = TryGetMapArea();
            return area >= Math.Max(1, AdaptiveLargeMapAreaThreshold.Value);
        }

        private static int GetAdaptiveBatchSize(int baseValue)
        {
            int value = Math.Max(1, baseValue);
            if (!IsLargeMapTuningActive())
                return value;
            int mul = Math.Max(1, AdaptiveLargeMapBatchMultiplier.Value);
            long scaled = (long)value * mul;
            return scaled > int.MaxValue ? int.MaxValue : (int)scaled;
        }

        private static int GetAdaptiveHeightmapResolution()
        {
            if (PreserveWorldTerrainFidelity != null && PreserveWorldTerrainFidelity.Value)
                return 512;
            int res = Math.Max(64, HeightmapResolution.Value);
            if (IsLargeMapTuningActive())
                res = Math.Min(res, Math.Max(64, AdaptiveLargeMapHeightmapResolution.Value));
            return Math.Min(512, res);
        }

        private static int GetAdaptiveTerrainDecoResolution()
        {
            if (PreserveWorldTerrainFidelity != null && PreserveWorldTerrainFidelity.Value)
                return 512;
            int res = Math.Max(32, TerrainDecoResolution.Value);
            if (IsLargeMapTuningActive())
                res = Math.Min(res, Math.Max(32, AdaptiveLargeMapTerrainDecoResolution.Value));
            return res;
        }

        private static int GetAdaptiveMapDisplayResolution()
        {
            if ((DisableTerritoryOverlay != null && DisableTerritoryOverlay.Value)
                || (PreserveWorldTerrainFidelity != null && PreserveWorldTerrainFidelity.Value))
                return 1024;
            int res = Math.Max(128, MapDisplayResolution.Value);
            if (IsLargeMapTuningActive())
                res = Math.Min(res, Math.Max(128, AdaptiveLargeMapMapDisplayResolution.Value));
            return Math.Min(1024, res);
        }

        private static float GetAdaptiveRuntimeThrottleSeconds(float baseSeconds)
        {
            float value = Math.Max(0f, baseSeconds);
            if (!IsLargeMapTuningActive())
                return value;
            float mul = (AdaptiveRuntimeThrottleMultiplier != null) ? AdaptiveRuntimeThrottleMultiplier.Value : 1f;
            mul = Mathf.Max(1f, mul);
            return value * mul;
        }

        // =====================================================================
        // 1. Clock Reset — Postfix on GameClock.AdvanceGameAnim
        // =====================================================================
        [HarmonyPatch(typeof(GameClock), "AdvanceGameAnim")]
        private static class ClockResetPatch
        {
            [HarmonyPostfix]
            static void Postfix(GameClock __instance)
            {
                if (EnableClockReset == null || !EnableClockReset.Value || __instance == null)
                    return;

                try
                {
                    GameAnimUpdate anim = __instance.AnimState;
                    if (anim == null)
                        return;

                    float cumSec = anim.cumulativeSeconds;
                    float threshold = Math.Max(30f, ClockResetThreshold.Value);
                    if (cumSec <= threshold)
                        return;

                    anim.cumulativeSeconds = 0f;
                    anim.frameDeltaSeconds = 0f;
                    LogVerbose($"[GameOptimizer] ClockReset: threshold reset from {cumSec:F1}s to 0");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] ClockResetPatch error: {e}");
                }
            }
        }

        // =====================================================================
        // 2. Gambling Module Skip — Prefix on BusinessUpdate.UpdateGamblingModules
        // =====================================================================
        [HarmonyPatch(typeof(BusinessUpdate), "UpdateGamblingModules")]
        private static class GamblingSkipPatch
        {
            private static MethodInfo _doUpdateMethod;

            static GamblingSkipPatch()
            {
                try
                {
                    _doUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] GamblingSkipPatch reflection setup failed: {e}");
                }
            }

            [HarmonyPrefix]
            static bool Prefix(bool initial)
            {
                if (!EnableGamblingSkip.Value) return true;

                try
                {
                    var ctx = G.ctx;
                    if (ctx == null) return true;

                    SimTime now = ctx.clock.Now;
                    foreach (object player in ctx.players.all)
                    {
                        dynamic p = player;
                        var gamblingHouses = p.gambling.GetAllMyGamblingHouses();

                        bool hasAny = false;
                        foreach (var house in gamblingHouses)
                        {
                            hasAny = true;
                            break;
                        }
                        if (!hasAny) continue;

                        foreach (EntityID houseId in p.gambling.GetAllMyGamblingHouses())
                        {
                            var entity = houseId.FindEntity();
                            var modules = entity?.components?.modules;
                            if (modules != null && _doUpdateMethod != null)
                            {
                                _doUpdateMethod.Invoke(modules, new object[] { now, initial });
                            }
                        }
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] GamblingSkipPatch error: {e}");
                    return true;
                }
            }
        }

        // =====================================================================
        // 3. Goon BFS Skip — Prefix on GoonAdvisor.OnTurnUpdate
        // =====================================================================
        [HarmonyPatch(typeof(GoonAdvisor), "OnTurnUpdate")]
        private static class GoonBFSSkipPatch
        {
            private static FieldInfo _dataFieldGoon;
            private static FieldInfo _harassTypePlannedField;
            private static FieldInfo _businessToHarassField;
            private static FieldInfo _rewardsField;
            private static MethodInfo _rollForScriptMethod;

            private static readonly List<Label> GOON_HARRASS_TYPES = new List<Label>
            {
                ScriptNames.BURGLE,
                ScriptNames.VANDALIZE,
                ScriptNames.EXTORT
            };

            static GoonBFSSkipPatch()
            {
                try
                {
                    _dataFieldGoon = AccessTools.Field(typeof(GoonAdvisor), "_data");
                    _rollForScriptMethod = AccessTools.Method(typeof(GoonAdvisor), "RollForScript");

                    if (_dataFieldGoon != null)
                    {
                        var dataType = _dataFieldGoon.FieldType;
                        _harassTypePlannedField = AccessTools.Field(dataType, "harassTypePlanned");
                        _businessToHarassField = AccessTools.Field(dataType, "businessToHarass");
                        _rewardsField = AccessTools.Field(dataType, "rewards");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] GoonBFSSkipPatch reflection setup failed: {e}");
                }
            }

            [HarmonyPrefix]
            static bool Prefix(GoonAdvisor __instance)
            {
                if (!EnableGoonBFSSkip.Value) return true;

                try
                {
                    if (_dataFieldGoon == null || _rollForScriptMethod == null ||
                        _harassTypePlannedField == null || _businessToHarassField == null)
                        return true;

                    var data = _dataFieldGoon.GetValue(__instance);

                    // Check rewards
                    var rewards = _rewardsField?.GetValue(data);
                    if (rewards != null)
                    {
                        var checkMethod = AccessTools.Method(rewards.GetType(), "CheckRelationshipRewards");
                        checkMethod?.Invoke(rewards, null);
                    }

                    // Roll for script
                    Label label = (Label)_rollForScriptMethod.Invoke(__instance, null);

                    bool isHarassment = GOON_HARRASS_TYPES.Contains(label);
                    _harassTypePlannedField.SetValue(data, isHarassment ? label : Label.NULL);

                    if (!isHarassment)
                    {
                        _businessToHarassField.SetValue(data, EntityID.INVALID);
                        return false; // skip expensive BFS
                    }

                    return true; // let original handle FindTarget
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] GoonBFSSkipPatch error: {e}");
                    return true;
                }
            }
        }

        // =====================================================================
        // 4. PickManager Throttle — Prefix on PickManager.OnPlayerKBChanged
        // =====================================================================
        [HarmonyPatch(typeof(PickManager), "OnPlayerKBChanged")]
        private static class PickManagerThrottlePatch
        {
            private static float _lastCallTime = -1f;

            internal static void ResetRuntime()
            {
                _lastCallTime = -1f;
            }

            [HarmonyPrefix]
            static bool Prefix()
            {
                if (!EnablePickManagerThrottle.Value) return true;

                try
                {
                    float now = Time.unscaledTime;
                    float throttleSeconds = GetAdaptiveRuntimeThrottleSeconds(PickManagerThrottleSeconds.Value);
                    if (now - _lastCallTime < throttleSeconds)
                    {
                        return false; // throttled
                    }
                    _lastCallTime = now;
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] PickManagerThrottlePatch error: {e}");
                    return true;
                }
            }
        }

        // =====================================================================
        // 4b. Relationship Cache — cache Evaluate() results per turn
        // =====================================================================
        private static class RelationshipCachePatch
        {
            private static int _lastTurnHash = -1;
            private static Dictionary<long, object> _cache = new Dictionary<long, object>();
            private static Func<Relationship, object> _callOriginal;

            private static long MakeKey(int a, int b) => ((long)a << 32) | (uint)b;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableRelationshipCache.Value) return;
                try
                {
                    var evalMethod = typeof(Relationship).GetMethod("Evaluate", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (evalMethod == null)
                    {
                        Debug.LogError("[GameOptimizer] Relationship.Evaluate() not found");
                        return;
                    }
                    // Create a reverse patch to get an unpatched copy of the original method
                    var stub = typeof(RelationshipCachePatch).GetMethod(nameof(OriginalEvaluateStub), BindingFlags.Static | BindingFlags.NonPublic);
                    var reversePatcher = harmony.CreateReversePatcher(evalMethod, new HarmonyMethod(stub));
                    reversePatcher.Patch();
                    // Now OriginalEvaluateStub contains the original unpatched code
                    _callOriginal = (rel) => OriginalEvaluateStub(rel);

                    var prefix = typeof(RelationshipCachePatch).GetMethod(nameof(EvaluatePrefix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(evalMethod, prefix: new HarmonyMethod(prefix));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] RelationshipCachePatch failed: {e}");
                }
            }

            [HarmonyReversePatch]
            [HarmonyPatch(typeof(Relationship), "Evaluate")]
            static object OriginalEvaluateStub(Relationship instance)
            {
                // This body is replaced by the reverse patcher with the original method code
                throw new NotImplementedException("Reverse patch stub");
            }

            static bool EvaluatePrefix(Relationship __instance, ref object __result)
            {
                if (!EnableRelationshipCache.Value) return true;
                try
                {
                    dynamic ctx = G.ctx;
                    if (ctx == null) return true;
                    int turnHash = ctx.clock.Now.GetHashCode();
                    if (turnHash != _lastTurnHash)
                    {
                        _cache.Clear();
                        _lastTurnHash = turnHash;
                    }
                    long key = MakeKey(__instance.from.GetHashCode(), __instance.to.GetHashCode());
                    if (_cache.TryGetValue(key, out var cached))
                    {
                        __result = cached;
                        return false;
                    }
                    // Cache miss - call original (unpatched) via reverse patch
                    object result = _callOriginal(__instance);
                    _cache[key] = result;
                    __result = result;
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }

        // =====================================================================
        // 4c. PickManager Node Cache — cache known nodes, rebuild once per day
        // =====================================================================
        private static class PickManagerNodeCachePatch
        {
            private static HashSet<NodeID> _cachedKnownNodes = new HashSet<NodeID>();
            private static int _lastCacheTurn = -1;
            private static bool _cacheValid = false;
            private static MethodInfo _refreshBuildingPicksOnNodeMethod;
            private static MethodInfo _refreshSchemePicksMethod;
            private static bool _initialized = false;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnablePickManagerNodeCache.Value) return;
                try
                {
                    _refreshBuildingPicksOnNodeMethod = typeof(PickManager).GetMethod("RefreshBuildingPicksOnNode", BindingFlags.Instance | BindingFlags.NonPublic);
                    _refreshSchemePicksMethod = typeof(PickManager).GetMethod("RefreshSchemePicks", BindingFlags.Instance | BindingFlags.NonPublic);
                    _initialized = _refreshBuildingPicksOnNodeMethod != null && _refreshSchemePicksMethod != null;

                    if (!_initialized)
                    {
                        Debug.LogWarning("[GameOptimizer] PickManagerNodeCachePatch: some methods not found, skipping");
                        return;
                    }

                    var targetMethod = typeof(PickManager).GetMethod("RefreshPicksOnKnownBuildings", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (targetMethod == null)
                    {
                        Debug.LogError("[GameOptimizer] RefreshPicksOnKnownBuildings not found");
                        return;
                    }
                    var prefix = typeof(PickManagerNodeCachePatch).GetMethod(nameof(RefreshPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(targetMethod, new HarmonyMethod(prefix));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] PickManagerNodeCachePatch failed: {e}");
                }
            }

            static bool RefreshPrefix(PickManager __instance, PlayerInfo player)
            {
                if (!EnablePickManagerNodeCache.Value || !_initialized) return true;
                try
                {
                    dynamic ctx = G.ctx;
                    if (ctx == null) return true;
                    int currentDay = ctx.clock.Now.days;
                    if (!_cacheValid || currentDay != _lastCacheTurn)
                    {
                        RebuildKnownNodesCache(player, ctx);
                        _lastCacheTurn = currentDay;
                        _cacheValid = true;
                    }

                    var board = ctx.board;
                    foreach (NodeID nodeId in _cachedKnownNodes)
                    {
                        Node node = board.nodes.GetNode(nodeId);
                        if (node != null)
                        {
                            _refreshBuildingPicksOnNodeMethod.Invoke(__instance, new object[] { node });
                        }
                    }
                    _refreshSchemePicksMethod.Invoke(__instance, null);
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] PickManagerNodeCachePatch error: {e.Message}");
                    _cacheValid = false;
                    return true;
                }
            }

            private static void RebuildKnownNodesCache(PlayerInfo player, dynamic ctx)
            {
                _cachedKnownNodes.Clear();
                if (ctx?.board?.nodes == null) return;
                PlayerID pid = player.PID;
                foreach (Node node in ctx.board.nodes.GetAllNodesUnsafe())
                {
                    if (node.known.Get(pid))
                    {
                        _cachedKnownNodes.Add(node.id);
                    }
                }
            }
        }

        // =====================================================================
        // 5. Heat/Respect Recalc Throttle
        // =====================================================================
        [HarmonyPatch(typeof(BusinessUpdate), "RecalculateHeatAndRespectForNodes")]
        private static class HeatRespectThrottlePatch
        {
            private static int _turnCounter = 0;

            internal static void ResetRuntime()
            {
                _turnCounter = 0;
            }

            [HarmonyPrefix]
            static bool Prefix(bool initial)
            {
                if (!EnableHeatRespectThrottle.Value) return true;
                if (initial) return true;

                try
                {
                    _turnCounter++;
                    if (_turnCounter >= HeatRespectInterval.Value)
                    {
                        _turnCounter = 0;
                        return true;
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] HeatRespectThrottlePatch error: {e}");
                    return true;
                }
            }
        }

        // =====================================================================
        // 6. Heat Propagation Throttle
        // =====================================================================
        [HarmonyPatch(typeof(BusinessUpdate), "UpdateHeatFromNeighbors")]
        private static class HeatPropagationThrottlePatch
        {
            private static int _turnCounter = 0;

            internal static void ResetRuntime()
            {
                _turnCounter = 0;
            }

            [HarmonyPrefix]
            static bool Prefix()
            {
                if (!EnableHeatPropagationThrottle.Value) return true;

                try
                {
                    _turnCounter++;
                    if (_turnCounter >= HeatPropagationInterval.Value)
                    {
                        _turnCounter = 0;
                        return true;
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] HeatPropagationThrottlePatch error: {e}");
                    return true;
                }
            }
        }

        // =====================================================================
        // 7. AI Crew Levelup — Postfix on PlayerCrew.OnPlayerTurnStarted
        // =====================================================================
        [HarmonyPatch(typeof(PlayerCrew), "OnPlayerTurnStarted")]
        private static class AICrewLevelupPatch
        {
            private static FieldInfo _pidField;
            private static FieldInfo _crewdataField;
            private static FieldInfo _rawcrewField;
            private static MethodInfo _hasXPToGainLevelupMethod;
            private static System.Random _rng = new System.Random();
            private static readonly Dictionary<string, int> _priorityLevelupOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "levelup-hoodsgang", 0 },
                { "levelup-hoods", 1 },
                { "levelup-streetcred3", 2 },
                { "levelup-streetcred2", 3 },
                { "levelup-streetcred1", 4 },
                { "levelup-leader1", 5 },
                { "levelup-leader2", 6 },
                { "levelup-captain", 7 },
            };

            static AICrewLevelupPatch()
            {
                try
                {
                    _pidField = AccessTools.Field(typeof(PlayerSubmanager), "_pid");
                    _crewdataField = AccessTools.Field(typeof(PlayerCrew), "_crewdata");
                    if (_crewdataField != null)
                        _rawcrewField = AccessTools.Field(_crewdataField.FieldType, "rawcrew");
                    _hasXPToGainLevelupMethod = AccessTools.Method(typeof(AgentComponent), "HasXPToGainLevelup");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] AICrewLevelupPatch reflection setup failed: {e}");
                }
            }

            private static LevelupDescription PickPreferredLevelup(List<LevelupDescription> available)
            {
                LevelupDescription best = null;
                int bestOrder = int.MaxValue;
                for (int i = 0; i < available.Count; i++)
                {
                    LevelupDescription candidate = available[i];
                    string id = candidate.levelup.id.String;
                    if (!string.IsNullOrEmpty(id) && _priorityLevelupOrder.TryGetValue(id, out int order) && order < bestOrder)
                    {
                        best = candidate;
                        bestOrder = order;
                    }
                }
                if (best != null)
                    return best;
                return available[_rng.Next(available.Count)];
            }

            [HarmonyPostfix]
            static void Postfix(PlayerCrew __instance)
            {
                if (!EnableAICrewLevelups.Value) return;

                try
                {
                    if (_pidField == null || _crewdataField == null ||
                        _rawcrewField == null || _hasXPToGainLevelupMethod == null)
                        return;

                    var pid = (PlayerID)_pidField.GetValue(__instance);
                    if (pid.IsHumanPlayer) return;

                    var crewdata = _crewdataField.GetValue(__instance);
                    var rawcrew = (List<CrewAssignment>)_rawcrewField.GetValue(crewdata);
                    if (rawcrew == null) return;

                    int maxPerTurn = AILevelupMaxPerCrewPerTurn.Value;

                    foreach (var crew in rawcrew)
                    {
                        if (crew.IsDead || crew.IsNotAssigned) continue;

                        Entity peep = crew.GetPeep();
                        if (peep == null) continue;

                        AgentComponent agent = peep.components.agent;
                        if (agent == null) continue;

                        int levelsGained = 0;
                        while (levelsGained < maxPerTurn &&
                               (bool)_hasXPToGainLevelupMethod.Invoke(agent, null))
                        {
                            var availableEnumerable = agent.GetAvailableLevelups(false);
                            List<LevelupDescription> available = availableEnumerable as List<LevelupDescription> ?? availableEnumerable.ToList();
                            if (available.Count == 0) break;

                            LevelupDescription pick = PickPreferredLevelup(available);

                            XP xp = peep.data.agent.xp;
                            xp.lastThreshold++;
                            xp.SetLevelupLevel(pick.levelup.id, pick.nextLevel);

                            levelsGained++;
                        }

                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] AICrewLevelupPatch error: {e}");
                }
            }
        }
        // =====================================================================
        // 8. Load Timing — Prefix on SetupOrchestrator.OnBoardInitStarted
        // =====================================================================
        [HarmonyPatch(typeof(SetupOrchestrator), "OnBoardInitStarted")]
        private static class LoadTimingPatch
        {
            private static FieldInfo _ctxField;
            private static FieldInfo _boardInitTaskField;
            private static MethodInfo _startLoadingFileMethod;
            private static MethodInfo _skipLoadingFileMethod;
            private static MethodInfo _startCoroutineTaskMethod;

            static LoadTimingPatch()
            {
                try
                {
                    _ctxField = AccessTools.Field(typeof(SetupOrchestrator), "_ctx");
                    _boardInitTaskField = AccessTools.Field(typeof(SetupOrchestrator), "_boardInitTask");
                    _startLoadingFileMethod = AccessTools.Method(typeof(SetupOrchestrator), "StartLoadingFile");
                    _skipLoadingFileMethod = AccessTools.Method(typeof(SetupOrchestrator), "SkipLoadingFile");

                    // Find StartCoroutineTask on the sequencer service
                    var seqType = typeof(GameClock).Assembly.GetType("Game.Services.ActionSequencerService");
                    if (seqType != null)
                        _startCoroutineTaskMethod = AccessTools.Method(seqType, "StartCoroutineTask");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] LoadTimingPatch reflection setup failed: {e}");
                }
            }

            [HarmonyPrefix]
            static bool Prefix(SetupOrchestrator __instance)
            {
                bool accelerationEnabled = EnableLoadAcceleration != null && EnableLoadAcceleration.Value;
                bool logSteps = (LoadAccelerationLogSteps != null && LoadAccelerationLogSteps.Value) || (EnableLoadTimings != null && EnableLoadTimings.Value);
                if (!accelerationEnabled && !logSteps) return true;

                try
                {
                    // Call the same pre-work the original does
                    var wireUpMethod = AccessTools.Method(typeof(GameClock).Assembly.GetType("IndirectRendering.IndirectRenderer"), "PreGameSetup");
                    wireUpMethod?.Invoke(null, null);

                    var toggleMethod = AccessTools.Method(typeof(SetupOrchestrator), "ToggleGSGenerating");
                    toggleMethod?.Invoke(__instance, new object[] { true });

                    // Determine save vs new via reflection (avoid dynamic/Microsoft.CSharp)
                    var gameType = typeof(GameClock).Assembly.GetType("Game.Game");
                    object ctx = gameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    bool hasSave = (bool)AccessTools.Property(ctx.GetType(), "HasSaveFile").GetValue(ctx);

                    IEnumerator originalCoroutine;
                    if (hasSave)
                    {
                        object session = AccessTools.Field(ctx.GetType(), "session").GetValue(ctx);
                        object savefile = AccessTools.Field(session.GetType(), "savefile").GetValue(session);
                        originalCoroutine = (IEnumerator)_startLoadingFileMethod.Invoke(__instance, new object[] { savefile });
                    }
                    else
                    {
                        originalCoroutine = (IEnumerator)_skipLoadingFileMethod.Invoke(__instance, null);
                    }

                    IEnumerator timedCoroutine = TimedCoroutineWrapper(originalCoroutine, hasSave ? "LoadSave" : "NewGame", accelerationEnabled, logSteps);

                    // _boardInitTask = Game.serv.sequencer.StartCoroutineTask(timedCoroutine)
                    object serv = gameType.GetField("serv", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    object sequencer = AccessTools.Field(serv.GetType(), "sequencer").GetValue(serv);
                    object task = _startCoroutineTaskMethod.Invoke(sequencer, new object[] { timedCoroutine, null });
                    _boardInitTaskField.SetValue(__instance, task);

                    return false; // skip original
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] LoadTimingPatch error, falling back to original: {e}");
                    return true;
                }
            }

            private static IEnumerator TimedCoroutineWrapper(IEnumerator inner, string mode, bool accelerationEnabled, bool logSteps, int depth = 0)
            {
                string indent = new string(' ', depth * 2);
                if (depth == 0 && logSteps)
                    Debug.Log($"[LoadTiming] === {mode} loading started ===");

                var totalSw = Stopwatch.StartNew();
                var stepSw = Stopwatch.StartNew();
                int stepIndex = 0;
                bool batching = accelerationEnabled && EnableYieldBatching.Value;
                int configuredBatchSize = (LoadAccelerationBatchSize != null) ? LoadAccelerationBatchSize.Value : YieldBatchSize.Value;
                int batchSize = Math.Max(1, configuredBatchSize);
                int nullCount = 0;
                bool finished = false;

                while (!finished)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = inner.MoveNext();
                    }
                    catch (Exception e)
                    {
                        if (logSteps)
                        {
                            Debug.LogError($"[LoadTiming] {indent}Step {stepIndex} threw exception: {e}");
                            Debug.LogWarning("[LoadTiming] Aborting timing wrapper - game will continue loading without timing");
                        }
                        else if (depth == 0)
                        {
                            Debug.LogWarning($"[PERF][BoardLoad] mode={mode} aborted reason=exception");
                        }
                        yield break;
                    }

                    if (!hasNext) break;

                    object current = inner.Current;

                    if (current != null)
                    {
                        nullCount = 0;
                        long ms = stepSw.ElapsedMilliseconds;
                        string label = null;
                        if (logSteps)
                            label = DescribeYield(current);

                        if (logSteps && ms > 1)
                            Debug.Log($"[LoadTiming] {indent}  Step {stepIndex}: {label} = {ms}ms");

                        if (current is IEnumerator subCoroutine && depth < 2)
                        {
                            stepSw.Restart();
                            string childMode = logSteps ? label : mode;
                            yield return TimedCoroutineWrapper(subCoroutine, childMode, accelerationEnabled, logSteps, depth + 1);
                            ms = stepSw.ElapsedMilliseconds;
                            if (logSteps && ms > 1)
                                Debug.Log($"[LoadTiming] {indent}  Step {stepIndex} total: {label} = {ms}ms");
                        }
                        else
                        {
                            yield return current;
                        }

                        stepIndex++;
                        stepSw.Restart();
                    }
                    else
                    {
                        // null yield — batch multiple into one frame wait
                        nullCount++;
                        if (!batching || nullCount >= batchSize)
                        {
                            nullCount = 0;
                            yield return null;
                        }
                        // else: skip this yield, continue processing
                    }
                }

                totalSw.Stop();
                if (depth == 0)
                {
                    if (logSteps)
                        Debug.Log($"[LoadTiming] === {mode} loading completed in {totalSw.ElapsedMilliseconds}ms ({totalSw.Elapsed.TotalSeconds:F1}s) ===");
                    Debug.Log($"[PERF][BoardLoad] mode={mode} ms={totalSw.ElapsedMilliseconds} batching={(batching ? "on" : "off")} batch={batchSize}");
                }
            }

            private static string DescribeYield(object yielded)
            {
                if (yielded == null) return "yield null (frame wait)";
                Type t = yielded.GetType();
                string name = t.Name;

                // Compiler-generated IEnumerator: <MethodName>d__N
                if (name.StartsWith("<"))
                {
                    int end = name.IndexOf('>');
                    string methodName = end > 1 ? name.Substring(1, end - 1) : name;
                    // Get declaring type for context (e.g. CreateHeightmap.<Start>d__5)
                    Type declaring = t.DeclaringType;
                    if (declaring != null)
                        return $"{declaring.Name}.{methodName}";
                    return methodName;
                }

                // IEnumerator from a non-compiler-generated source — inspect fields
                if (yielded is IEnumerator)
                {
                    Type declaring = t.DeclaringType;
                    if (declaring != null)
                        return $"{declaring.Name}.{name}";
                    // Check for <>4__this or similar fields that indicate the owner
                    foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (field.Name.Contains("this"))
                        {
                            object owner = field.GetValue(yielded);
                            if (owner != null)
                                return $"{owner.GetType().Name}.{name}";
                        }
                    }
                    return $"IEnumerator({name})";
                }

                if (yielded is WaitForSeconds wfs)
                {
                    var secField = AccessTools.Field(typeof(WaitForSeconds), "m_Seconds");
                    float sec = secField != null ? (float)secField.GetValue(wfs) : -1f;
                    return $"WaitForSeconds({sec}s)";
                }

                // Fallback: full type name for better identification
                if (name == "Start" || name.Length <= 3)
                    return $"{t.FullName}";

                return name;
            }
        }

        // =====================================================================
        // 9. Heightmap Optimization — Prefix on CreateHeightmap.Start
        // =====================================================================
        private static class HeightmapOptimizationPatch
        {
            private static FieldInfo _ctxField;
            private static FieldInfo _mountainRegionDataField;
            private static FieldInfo _waterRegionDataField;
            private static FieldInfo _mountainBodiesField;
            private static FieldInfo _waterRegionsField;
            private static FieldInfo _waterBodiesField;
            private static FieldInfo _heightmapDataField;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableHeightmapOptimization.Value || (PreserveWorldTerrainFidelity != null && PreserveWorldTerrainFidelity.Value))
                    return;
                try
                {
                    var createHeightmapType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.CreateHeightmap");
                    if (createHeightmapType == null)
                    {
                        Debug.LogError("[GameOptimizer] HeightmapOptimizationPatch: CreateHeightmap type not found");
                        return;
                    }

                    _ctxField = AccessTools.Field(createHeightmapType, "_ctx");
                    var ctxType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.SetupOrchestratorContext");
                    if (ctxType != null)
                    {
                        _mountainRegionDataField = AccessTools.Field(ctxType, "mountainRegionData");
                        _waterRegionDataField = AccessTools.Field(ctxType, "waterRegionData");
                        _heightmapDataField = AccessTools.Field(ctxType, "heightmapData");
                    }

                    var mtnType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.MountainRegionData");
                    if (mtnType != null)
                        _mountainBodiesField = AccessTools.Field(mtnType, "_mountainBodies");

                    var waterType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.WaterRegionData");
                    if (waterType != null)
                    {
                        _waterRegionsField = AccessTools.Field(waterType, "waterRegions");
                        _waterBodiesField = AccessTools.Field(waterType, "waterBodies");
                    }

                    var original = AccessTools.Method(createHeightmapType, "Start");
                    var prefix = new HarmonyMethod(typeof(HeightmapOptimizationPatch), "Prefix");
                    harmony.Patch(original, prefix: prefix);

                    Debug.Log("[GameOptimizer] HeightmapOptimizationPatch applied successfully");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] HeightmapOptimizationPatch setup failed: {e}");
                }
            }

            static bool Prefix(object __instance, ref IEnumerator __result)
            {
                if (!EnableHeightmapOptimization.Value) return true;

                // Store the original method so we can call it as fallback
                __result = SafeOptimizedHeightmap(__instance);
                return false;
            }

            private static IEnumerator SafeOptimizedHeightmap(object instance)
            {
                // Wrap the optimized heightmap in error handling.
                // Since coroutine bodies run during MoveNext(), not during method call,
                // we need to catch errors inside the coroutine itself.
                bool failed = false;
                Exception caughtEx = null;

                IEnumerator optimized = null;
                try
                {
                    optimized = OptimizedHeightmap(instance);
                }
                catch (Exception e)
                {
                    failed = true;
                    caughtEx = e;
                }

                if (!failed && optimized != null)
                {
                    while (true)
                    {
                        bool hasNext;
                        try { hasNext = optimized.MoveNext(); }
                        catch (Exception e)
                        {
                            failed = true;
                            caughtEx = e;
                            break;
                        }
                        if (!hasNext) break;
                        yield return optimized.Current;
                    }
                }

                if (failed)
                {
                    Debug.LogError($"[GameOptimizer] HeightmapOptimization failed, creating fallback heightmap: {caughtEx}");
                    // Create a minimal valid heightmap so the pipeline doesn't break
                    try
                    {
                        FallbackHeightmap(instance);
                    }
                    catch (Exception e2)
                    {
                        Debug.LogError($"[GameOptimizer] Fallback heightmap also failed: {e2}");
                    }
                }
            }

            private static void FallbackHeightmap(object instance)
            {
                // Create a minimal 512x512 heightmap matching the original game's approach
                object ctx = _ctxField?.GetValue(instance);
                if (ctx == null) return;

                var heightmapDataType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.HeightmapData");
                object heightmapData = Activator.CreateInstance(heightmapDataType);
                _heightmapDataField.SetValue(ctx, heightmapData);

                Texture2D texture = new Texture2D(512, 512) { filterMode = FilterMode.Point };
                Color[] pixels = new Color[512 * 512];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color(0f, 0f, 0f, 1f);
                texture.SetPixels(pixels);
                texture.Apply();

                AccessTools.Field(heightmapDataType, "heightMap").SetValue(heightmapData, texture);

                var gameType = typeof(GameClock).Assembly.GetType("Game.Game");
                object gameCtx = gameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                object board = AccessTools.Field(gameCtx.GetType(), "board").GetValue(gameCtx);
                object terrain = AccessTools.Field(board.GetType(), "terrain").GetValue(board);
                AccessTools.Method(terrain.GetType(), "SetHeightmap").Invoke(terrain, new object[] { heightmapData });

                Debug.Log("[GameOptimizer] Fallback heightmap created (512x512 blank)");
            }

            private static IEnumerator OptimizedHeightmap(object instance)
            {
                var sw = Stopwatch.StartNew();
                int res = GetAdaptiveHeightmapResolution();
                // Clamp to reasonable range
                if (res < 64) res = 64;
                if (res > 512) res = 512;

                object ctx = _ctxField.GetValue(instance);

                // Create HeightmapData
                var heightmapDataType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.HeightmapData");
                object heightmapData = Activator.CreateInstance(heightmapDataType);
                _heightmapDataField.SetValue(ctx, heightmapData);

                // Create texture at lower resolution with point filtering (matches original)
                Texture2D texture = new Texture2D(res, res)
                {
                    filterMode = FilterMode.Point
                };

                // Get map size via reflection chain: Game.ctx.session.mapconfig.map.mapSize
                var gameType = typeof(GameClock).Assembly.GetType("Game.Game");
                object gameCtx = gameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                object session = AccessTools.Field(gameCtx.GetType(), "session").GetValue(gameCtx);
                object mapconfigVal = AccessTools.Property(session.GetType(), "mapconfig").GetValue(session);
                object mapField = AccessTools.Field(mapconfigVal.GetType(), "map").GetValue(mapconfigVal);
                IntSize mapSize = (IntSize)AccessTools.Field(mapField.GetType(), "mapSize").GetValue(mapField);

                float scaleX = (float)mapSize.width / (float)res;
                float scaleY = (float)mapSize.height / (float)res;

                Color[] pixels = new Color[res * res];

                // Get terrain data
                object mountainRegionData = _mountainRegionDataField?.GetValue(ctx);
                object waterRegionData = _waterRegionDataField?.GetValue(ctx);

                // If waterRegionData is null (CreateWater didn't set it), create an empty one
                // so downstream code (TerrainGenData.CheckIntersection, DensityHeatmap, etc.) doesn't crash
                if (waterRegionData == null && _waterRegionDataField != null)
                {
                    Debug.LogWarning("[GameOptimizer] HeightmapOpt: waterRegionData is null, creating empty WaterRegionData");
                    var waterRegionDataType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.WaterRegionData");
                    if (waterRegionDataType != null)
                    {
                        waterRegionData = Activator.CreateInstance(waterRegionDataType);
                        _waterRegionDataField.SetValue(ctx, waterRegionData);
                    }
                }

                List<TerrainBody> mountainBodies = null;
                if (mountainRegionData != null && _mountainBodiesField != null)
                    mountainBodies = (List<TerrainBody>)_mountainBodiesField.GetValue(mountainRegionData);

                List<WaterRegion> waterRegions = null;
                List<TerrainBody> waterBodies = null;
                if (waterRegionData != null)
                {
                    if (_waterRegionsField != null)
                        waterRegions = (List<WaterRegion>)_waterRegionsField.GetValue(waterRegionData);
                    if (_waterBodiesField != null)
                        waterBodies = (List<TerrainBody>)_waterBodiesField.GetValue(waterRegionData);
                }

                // Generate at lower resolution
                for (int i = 0; i < res; i++)
                {
                    for (int j = 0; j < res; j++)
                    {
                        Color color = new Color(0f, 0f, 0f, 1f);
                        float x = ((float)j + 0.5f) * scaleX;
                        float y = ((float)i + 0.5f) * scaleY;
                        WorldPos pos = new WorldPos(x, y);

                        // Mountain check
                        if (mountainBodies != null)
                        {
                            for (int m = 0; m < mountainBodies.Count; m++)
                            {
                                if (mountainBodies[m].IsPointWithinBody(pos, out var info))
                                {
                                    color.g = info.height;
                                    // Don't break — later mountains can override height
                                }
                            }
                        }

                        // Water check — matches original: always check water, no mountain skip
                        if (IsWaterAt(x, y, scaleX, scaleY, waterRegions, waterBodies))
                        {
                            color.r = 0.2f;
                        }

                        pixels[i * res + j] = color;
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();

                // Store in HeightmapData
                AccessTools.Field(heightmapDataType, "heightMap").SetValue(heightmapData, texture);

                // Set on terrain manager: Game.ctx.board.terrain.SetHeightmap(heightmapData)
                object board = AccessTools.Field(gameCtx.GetType(), "board").GetValue(gameCtx);
                object terrain = AccessTools.Field(board.GetType(), "terrain").GetValue(board);
                AccessTools.Method(terrain.GetType(), "SetHeightmap").Invoke(terrain, new object[] { heightmapData });

                sw.Stop();
                LogVerbose($"[GameOptimizer] Heightmap generated at {res}x{res} in {sw.ElapsedMilliseconds}ms (original: 512x512)");
                yield break;
            }

            private static bool IsWaterAt(float x, float y, float scaleX, float scaleY, List<WaterRegion> waterRegions, List<TerrainBody> waterBodies)
            {
                if ((waterRegions == null || waterRegions.Count == 0) && (waterBodies == null || waterBodies.Count == 0))
                {
                    return false;
                }
                if (IsPointInsideWater(new WorldPos(x, y), waterRegions, waterBodies))
                {
                    return true;
                }
                if (scaleX <= 1f && scaleY <= 1f)
                {
                    return false;
                }
                float dx = Math.Max(0.05f, scaleX * 0.35f);
                float dy = Math.Max(0.05f, scaleY * 0.35f);
                if (IsPointInsideWater(new WorldPos(x - dx, y - dy), waterRegions, waterBodies)) return true;
                if (IsPointInsideWater(new WorldPos(x - dx, y + dy), waterRegions, waterBodies)) return true;
                if (IsPointInsideWater(new WorldPos(x + dx, y - dy), waterRegions, waterBodies)) return true;
                if (IsPointInsideWater(new WorldPos(x + dx, y + dy), waterRegions, waterBodies)) return true;
                return false;
            }

            private static bool IsPointInsideWater(WorldPos pos, List<WaterRegion> waterRegions, List<TerrainBody> waterBodies)
            {
                if (waterRegions != null)
                {
                    for (int i = 0; i < waterRegions.Count; i++)
                    {
                        if (waterRegions[i].IsPointWithin(pos))
                        {
                            return true;
                        }
                    }
                }
                if (waterBodies != null)
                {
                    for (int i = 0; i < waterBodies.Count; i++)
                    {
                        if (waterBodies[i].IsPointWithinBody(pos))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        // =====================================================================
        // 10. Terrain Deco Resolution Optimization — Prefix on CreateTerrainDecos constructor
        // =====================================================================
        private static class TerrainDecoOptimizationPatch
        {
            private static FieldInfo _resolutionField;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableTerrainDecoOptimization.Value || (PreserveWorldTerrainFidelity != null && PreserveWorldTerrainFidelity.Value))
                    return;
                try
                {
                    var createTerrainDecosType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.CreateTerrainDecos");
                    if (createTerrainDecosType == null)
                    {
                        Debug.LogError("[GameOptimizer] TerrainDecoOptimizationPatch: CreateTerrainDecos type not found");
                        return;
                    }

                    _resolutionField = AccessTools.Field(createTerrainDecosType, "RESOLUTION");
                    if (_resolutionField == null)
                    {
                        Debug.LogError("[GameOptimizer] TerrainDecoOptimizationPatch: RESOLUTION field not found");
                        return;
                    }

                    var ctor = AccessTools.Constructor(createTerrainDecosType, new[] {
                        typeof(GameClock).Assembly.GetType("Game.Session.Setup.SetupOrchestratorContext")
                    });
                    if (ctor == null)
                    {
                        Debug.LogError("[GameOptimizer] TerrainDecoOptimizationPatch: constructor not found");
                        return;
                    }

                    var postfix = new HarmonyMethod(typeof(TerrainDecoOptimizationPatch), "Postfix");
                    harmony.Patch(ctor, postfix: postfix);

                    Debug.Log("[GameOptimizer] TerrainDecoOptimizationPatch applied successfully");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] TerrainDecoOptimizationPatch setup failed: {e}");
                }
            }

            static void Postfix(object __instance)
            {
                if (!EnableTerrainDecoOptimization.Value) return;

                try
                {
                    int res = GetAdaptiveTerrainDecoResolution();
                    if (res < 64) res = 64;
                    if (res > 512) res = 512;

                    Vector2Int original = (Vector2Int)_resolutionField.GetValue(__instance);
                    _resolutionField.SetValue(__instance, new Vector2Int(res, res));
                    LogVerbose($"[GameOptimizer] Terrain deco resolution reduced from {original.x}x{original.y} to {res}x{res}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] TerrainDecoOptimizationPatch error: {e}");
                }
            }
        }

        // =====================================================================
        // 11. Terrain Mesh Generation Optimization
        //     - Batch yield calls to reduce frame-wait overhead
        //     - Reduce tileverts to lower vertex count (quadratic reduction)
        //     - Replace MakeHardEdges with optimized version (single sample, no per-vertex reflection)
        // =====================================================================
        private static class TerrainMeshOptimizationPatch
        {
            // TerrainManager fields
            private static FieldInfo _mapdefField;
            private static FieldInfo _terrainField;
            private static FieldInfo _tilevertsField;

            // TerrainMeshGenerator fields (cached once for MakeHardEdges replacement)
            private static FieldInfo _mgTopo;
            private static FieldInfo _mgHeightmapData;
            private static FieldInfo _mgUvoffset;
            private static FieldInfo _mgUvsize;
            private static FieldInfo _mgXSize;
            private static FieldInfo _mgYSize;
            private static FieldInfo _mgVertices;
            private static FieldInfo _mgUvs;
            private static FieldInfo _mgTriangles;
            private static FieldInfo _mgColors;
            private static FieldInfo _mgTerrainMetadata;
            private static FieldInfo _mgMesh;
            private static FieldInfo _hmHeightMap;
            private static FieldInfo _topoGroundheight;
            private static FieldInfo _topoTerrainscale;
            private static FieldInfo _topoFlattenHills;
            private static MethodInfo _metadataSample;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableTerrainMeshOptimization.Value || (PreserveWorldTerrainFidelity != null && PreserveWorldTerrainFidelity.Value))
                    return;
                try
                {
                    var terrainManagerType = typeof(GameClock).Assembly.GetType("Game.Session.Board.TerrainManager");
                    if (terrainManagerType == null)
                    {
                        Debug.LogError("[GameOptimizer] TerrainMeshOptimizationPatch: TerrainManager type not found");
                        return;
                    }

                    // Patch MakeTerrainCoroutine for yield batching
                    var coroutineMethod = AccessTools.Method(terrainManagerType, "MakeTerrainCoroutine");
                    if (coroutineMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(TerrainMeshOptimizationPatch), "CoroutinePostfix");
                        harmony.Patch(coroutineMethod, postfix: postfix);
                    }

                    // Cache fields for tileverts modification
                    _mapdefField = AccessTools.Field(terrainManagerType, "_mapdef");
                    var mapConfigType = typeof(GameClock).Assembly.GetType("Game.Services.Maps.MapConfig");
                    LogVerbose($"[GameOptimizer] TerrainMesh setup: mapConfigType={mapConfigType != null}");
                    if (mapConfigType != null)
                    {
                        _terrainField = AccessTools.Field(mapConfigType, "terrain");
                        var terrainConfigType = _terrainField?.FieldType;
                        LogVerbose($"[GameOptimizer] TerrainMesh setup: terrainField={_terrainField != null}, terrainConfigType={terrainConfigType?.FullName}");
                        if (terrainConfigType != null)
                        {
                            _tilevertsField = AccessTools.Field(terrainConfigType, "tileverts");
                            _topoGroundheight = AccessTools.Field(terrainConfigType, "groundheight");
                            _topoTerrainscale = AccessTools.Field(terrainConfigType, "terrainscale");
                            _topoFlattenHills = AccessTools.Field(terrainConfigType, "flattenHills");
                            LogVerbose($"[GameOptimizer] TerrainMesh setup: tileverts={_tilevertsField != null}, groundheight={_topoGroundheight != null}, terrainscale={_topoTerrainscale != null}, flattenHills={_topoFlattenHills != null}");
                        }
                    }

                    // Patch StartTerrainMeshGeneration to reduce tileverts
                    var startMethod = AccessTools.Method(terrainManagerType, "StartTerrainMeshGeneration");
                    if (startMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(TerrainMeshOptimizationPatch), "StartPrefix");
                        harmony.Patch(startMethod, prefix: prefix);
                    }

                    // Cache TerrainMeshGenerator fields for MakeHardEdges replacement
                    var meshGenType = typeof(GameClock).Assembly.GetType("Game.Session.Board.TerrainMeshGenerator");
                    LogVerbose($"[GameOptimizer] TerrainMesh setup: meshGenType={meshGenType != null}");
                    if (meshGenType != null)
                    {
                        _mgTopo = AccessTools.Field(meshGenType, "_topo");
                        _mgHeightmapData = AccessTools.Field(meshGenType, "_heightmapData");
                        _mgUvoffset = AccessTools.Field(meshGenType, "_uvoffset");
                        _mgUvsize = AccessTools.Field(meshGenType, "_uvsize");
                        _mgXSize = AccessTools.Field(meshGenType, "_xSize");
                        _mgYSize = AccessTools.Field(meshGenType, "_ySize");
                        _mgVertices = AccessTools.Field(meshGenType, "_vertices");
                        _mgUvs = AccessTools.Field(meshGenType, "_uvs");
                        _mgTriangles = AccessTools.Field(meshGenType, "_triangles");
                        _mgColors = AccessTools.Field(meshGenType, "_colors");
                        _mgTerrainMetadata = AccessTools.Field(meshGenType, "_terrainMetadata");
                        _mgMesh = AccessTools.Field(meshGenType, "_mesh");

                        LogVerbose($"[GameOptimizer] TerrainMesh setup: mgTopo={_mgTopo != null}, mgHD={_mgHeightmapData != null}, mgXS={_mgXSize != null}, mgYS={_mgYSize != null}, mgVerts={_mgVertices != null}, mgMeta={_mgTerrainMetadata != null}");

                        var hmDataType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.HeightmapData");
                        if (hmDataType != null)
                            _hmHeightMap = AccessTools.Field(hmDataType, "heightMap");
                        LogVerbose($"[GameOptimizer] TerrainMesh setup: hmDataType={hmDataType != null}, hmHeightMap={_hmHeightMap != null}");

                        // Patch MakeHardEdges with our optimized version
                        if (EnableTerrainSingleSample.Value)
                        {
                            var makeHardEdges = AccessTools.Method(meshGenType, "MakeHardEdges");
                            if (makeHardEdges != null)
                            {
                                var hardEdgesPrefix = new HarmonyMethod(typeof(TerrainMeshOptimizationPatch), "MakeHardEdgesPrefix");
                                harmony.Patch(makeHardEdges, prefix: hardEdgesPrefix);
                            }
                        }

                        // Cache TerrainMetadata.Sample (global namespace, not Game.Session.Board)
                        var terrainMetadataType = typeof(GameClock).Assembly.GetType("TerrainMetadata");
                        LogVerbose($"[GameOptimizer] TerrainMesh setup: terrainMetadataType={terrainMetadataType != null}");
                        if (terrainMetadataType != null)
                        {
                            _metadataSample = AccessTools.Method(terrainMetadataType, "Sample");
                            LogVerbose($"[GameOptimizer] TerrainMesh setup: metadataSample={_metadataSample != null}");
                        }
                        else
                            Debug.LogWarning("[GameOptimizer] TerrainMetadata type not found in global namespace");
                    }

                    Debug.Log("[GameOptimizer] TerrainMeshOptimizationPatch applied successfully");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] TerrainMeshOptimizationPatch setup failed: {e}");
                }
            }

            // Reduce tileverts before mesh generation starts
            static void StartPrefix(object __instance)
            {
                try
                {
                    object mapdef = _mapdefField.GetValue(__instance);
                    object terrain = _terrainField.GetValue(mapdef);
                    object mapObj = AccessTools.Field(mapdef.GetType(), "map").GetValue(mapdef);
                    IntSize mapSize = (IntSize)AccessTools.Field(mapObj.GetType(), "mapSize").GetValue(mapObj);
                    IntSize chunkTiles = (IntSize)AccessTools.Field(terrain.GetType(), "chunktiles").GetValue(terrain);
                    int chunkCountX = 1 + mapSize.width / chunkTiles.width;
                    int chunkCountY = 1 + mapSize.height / chunkTiles.height;
                    IntSize tv = (IntSize)_tilevertsField.GetValue(terrain);
                    int vertsPerChunk = chunkTiles.width * tv.width * chunkTiles.height * tv.height * 6;
                    LogVerbose($"[GameOptimizer] TerrainMesh: map={mapSize.width}x{mapSize.height}, chunks={chunkCountX}x{chunkCountY}={chunkCountX*chunkCountY} total, chunkSize={chunkTiles.width}x{chunkTiles.height}, tileverts={tv.width}x{tv.height}, vertsPerChunk={vertsPerChunk}");
                }
                catch { }
                LogVerbose($"[GameOptimizer] StartPrefix fired! Enabled={EnableTerrainMeshOptimization.Value}, targetVerts={TerrainMeshTileVerts.Value}");
                if (!EnableTerrainMeshOptimization.Value) return;
                int targetVerts = TerrainMeshTileVerts.Value;
                if (targetVerts <= 0) return;

                try
                {
                    if (_mapdefField == null || _terrainField == null || _tilevertsField == null)
                    {
                        Debug.LogWarning($"[GameOptimizer] StartPrefix: null fields - mapdef={_mapdefField != null}, terrain={_terrainField != null}, tileverts={_tilevertsField != null}");
                        return;
                    }

                    object mapdef = _mapdefField.GetValue(__instance);
                    object terrain = _terrainField.GetValue(mapdef);
                    IntSize original = (IntSize)_tilevertsField.GetValue(terrain);
                    LogVerbose($"[GameOptimizer] Current tileverts: {original.width}x{original.height}, target: {targetVerts}");

                    if (targetVerts < original.width || targetVerts < original.height)
                    {
                        int newW = Math.Max(1, Math.Min(targetVerts, original.width));
                        int newH = Math.Max(1, Math.Min(targetVerts, original.height));
                        _tilevertsField.SetValue(terrain, new IntSize(newW, newH));
                        float reduction = (float)(original.width * original.height) / (float)(newW * newH);
                        LogVerbose($"[GameOptimizer] Terrain tileverts reduced from {original.width}x{original.height} to {newW}x{newH} ({reduction:F1}x fewer vertices)");
                    }

                    // Increase chunk size to reduce per-chunk Unity overhead (Instantiate, RecalcNormals, etc.)
                    int targetChunk = TerrainMeshChunkSize.Value;
                    if (targetChunk > 0)
                    {
                        var chunktileField = AccessTools.Field(terrain.GetType(), "chunktiles");
                        if (chunktileField != null)
                        {
                            IntSize origChunk = (IntSize)chunktileField.GetValue(terrain);
                            if (targetChunk > origChunk.width || targetChunk > origChunk.height)
                            {
                                int newCW = Math.Max(origChunk.width, targetChunk);
                                int newCH = Math.Max(origChunk.height, targetChunk);
                                chunktileField.SetValue(terrain, new IntSize(newCW, newCH));

                                // Calculate new chunk count
                                object mapObj = AccessTools.Field(_mapdefField.GetValue(__instance).GetType(), "map").GetValue(_mapdefField.GetValue(__instance));
                                IntSize mapSize = (IntSize)AccessTools.Field(mapObj.GetType(), "mapSize").GetValue(mapObj);
                                int newCountX = 1 + mapSize.width / newCW;
                                int newCountY = 1 + mapSize.height / newCH;
                                int origCountX = 1 + mapSize.width / origChunk.width;
                                int origCountY = 1 + mapSize.height / origChunk.height;
                                LogVerbose($"[GameOptimizer] Terrain chunks: {origChunk.width}x{origChunk.height} ({origCountX*origCountY} chunks) -> {newCW}x{newCH} ({newCountX*newCountY} chunks)");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] TerrainMeshOptimizationPatch tileverts error: {e}");
                }
            }

            // Replace entire MakeHardEdges with optimized version:
            // - Single heightmap sample instead of 3x3 kernel (9x fewer texture lookups)
            // - All field reads done once at start, pure C# loop with no reflection per vertex
            // Cache for TerrainMetadata Gradient to avoid reflection per-vertex
            private static Gradient _cachedGradient;
            private static FieldInfo _gradientField;

            static bool MakeHardEdgesPrefix(object __instance)
            {
                try
                {
                    if (_mgXSize == null || _mgYSize == null || _mgUvoffset == null || _mgUvsize == null ||
                        _mgTopo == null || _mgHeightmapData == null || _mgVertices == null || _mgUvs == null ||
                        _mgTriangles == null || _mgColors == null || _mgTerrainMetadata == null ||
                        _topoGroundheight == null || _topoTerrainscale == null || _topoFlattenHills == null)
                    {
                        Debug.LogWarning("[GameOptimizer] MakeHardEdgesPrefix: missing cached fields, falling back");
                        return true;
                    }

                    // Set 32-bit index format to support >65K vertices per chunk
                    if (_mgMesh != null)
                    {
                        Mesh mesh = (Mesh)_mgMesh.GetValue(__instance);
                        if (mesh != null)
                            mesh.indexFormat = IndexFormat.UInt32;
                    }

                    int xSize = (int)_mgXSize.GetValue(__instance);
                    int ySize = (int)_mgYSize.GetValue(__instance);
                    Vector2 uvoffset = (Vector2)_mgUvoffset.GetValue(__instance);
                    Vector2 uvsize = (Vector2)_mgUvsize.GetValue(__instance);

                    // Read topo config
                    object topo = _mgTopo.GetValue(__instance);
                    float groundheight = (float)_topoGroundheight.GetValue(topo);
                    float terrainscale = (float)_topoTerrainscale.GetValue(topo);
                    bool flattenHills = (bool)_topoFlattenHills.GetValue(topo);

                    // Get heightmap texture
                    object heightmapData = _mgHeightmapData.GetValue(__instance);
                    Texture2D heightMap = null;
                    if (heightmapData != null && _hmHeightMap != null)
                        heightMap = (Texture2D)_hmHeightMap.GetValue(heightmapData);

                    // Get terrain gradient directly to avoid reflection per-vertex
                    if (_cachedGradient == null)
                    {
                        object terrainMetadata = _mgTerrainMetadata.GetValue(__instance);
                        if (terrainMetadata != null)
                        {
                            if (_gradientField == null)
                                _gradientField = AccessTools.Field(terrainMetadata.GetType(), "_terrainColors");
                            if (_gradientField != null)
                                _cachedGradient = (Gradient)_gradientField.GetValue(terrainMetadata);
                        }
                    }
                    if (_cachedGradient == null)
                    {
                        Debug.LogWarning("[GameOptimizer] MakeHardEdgesPrefix: terrainMetadata gradient is null, falling back");
                        return true;
                    }

                    // Allocate arrays
                    int vertCount = xSize * ySize * 6;
                    int[] triangles = new int[vertCount];
                    Vector3[] vertices = new Vector3[vertCount];
                    Vector2[] uvs = new Vector2[vertCount];
                    Color[] colors = new Color[vertCount];

                    float invXSize = 1f / (float)xSize;
                    float invYSize = 1f / (float)ySize;

                    int idx = 0;
                    for (int row = 0; row < ySize; row++)
                    {
                        for (int col = 0; col < xSize; col++)
                        {
                            // Two triangles per cell, unrolled to avoid array allocation
                            // Vertex order: (col,row), (col,row+1), (col+1,row+1), (col+1,row+1), (col+1,row), (col,row)
                            float nx0 = (float)col * invXSize;
                            float nx1 = (float)(col + 1) * invXSize;
                            float ny0 = (float)row * invYSize;
                            float ny1 = (float)(row + 1) * invYSize;

                            float u00 = nx0 * uvsize.x + uvoffset.x;
                            float u10 = nx1 * uvsize.x + uvoffset.x;
                            float v00 = ny0 * uvsize.y + uvoffset.y;
                            float v01 = ny1 * uvsize.y + uvoffset.y;

                            // Pre-sample the 4 corner heights (shared across 6 verts)
                            float h00 = SampleHeight(heightMap, u00, v00, groundheight, terrainscale, flattenHills);
                            float h01 = SampleHeight(heightMap, u00, v01, groundheight, terrainscale, flattenHills);
                            float h11 = SampleHeight(heightMap, u10, v01, groundheight, terrainscale, flattenHills);
                            float h10 = SampleHeight(heightMap, u10, v00, groundheight, terrainscale, flattenHills);

                            float c00 = SampleBaseH(heightMap, u00, v00);
                            float c01 = SampleBaseH(heightMap, u00, v01);
                            float c11 = SampleBaseH(heightMap, u10, v01);
                            float c10 = SampleBaseH(heightMap, u10, v00);

                            // Triangle 1: (0,0), (0,1), (1,1)
                            vertices[idx] = new Vector3(nx0, ny0, h00);
                            uvs[idx] = new Vector2(u00, v00);
                            triangles[idx] = idx;
                            colors[idx] = _cachedGradient.Evaluate(c00);
                            idx++;

                            vertices[idx] = new Vector3(nx0, ny1, h01);
                            uvs[idx] = new Vector2(u00, v01);
                            triangles[idx] = idx;
                            colors[idx] = _cachedGradient.Evaluate(c01);
                            idx++;

                            vertices[idx] = new Vector3(nx1, ny1, h11);
                            uvs[idx] = new Vector2(u10, v01);
                            triangles[idx] = idx;
                            colors[idx] = _cachedGradient.Evaluate(c11);
                            idx++;

                            // Triangle 2: (1,1), (1,0), (0,0)
                            vertices[idx] = new Vector3(nx1, ny1, h11);
                            uvs[idx] = new Vector2(u10, v01);
                            triangles[idx] = idx;
                            colors[idx] = _cachedGradient.Evaluate(c11);
                            idx++;

                            vertices[idx] = new Vector3(nx1, ny0, h10);
                            uvs[idx] = new Vector2(u10, v00);
                            triangles[idx] = idx;
                            colors[idx] = _cachedGradient.Evaluate(c10);
                            idx++;

                            vertices[idx] = new Vector3(nx0, ny0, h00);
                            uvs[idx] = new Vector2(u00, v00);
                            triangles[idx] = idx;
                            colors[idx] = _cachedGradient.Evaluate(c00);
                            idx++;
                        }
                    }

                    // Write arrays back
                    _mgTriangles.SetValue(__instance, triangles);
                    _mgVertices.SetValue(__instance, vertices);
                    _mgUvs.SetValue(__instance, uvs);
                    _mgColors.SetValue(__instance, colors);

                    return false; // skip original
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] MakeHardEdgesPrefix error, falling back: {e}");
                    return true;
                }
            }

            private static float SampleBaseH(Texture2D heightMap, float u, float v)
            {
                float waterVal = 0f;
                float mountainVal = 0f;
                if (heightMap != null)
                {
                    Color pixel = heightMap.GetPixelBilinear(u, v);
                    waterVal = pixel.r;
                    mountainVal = pixel.g;
                }
                float baseH = 0.4975f;
                if (waterVal > 0f)
                    baseH -= 0.5f * waterVal;
                else if (mountainVal > 0f)
                    baseH += 0.5f * mountainVal;
                return baseH;
            }

            private static float SampleHeight(Texture2D heightMap, float u, float v, float groundheight, float terrainscale, bool flattenHills)
            {
                float baseH = SampleBaseH(heightMap, u, v);
                float elev = 1f - baseH;
                float zDiff = elev - groundheight;
                float scale = (zDiff < 0f && flattenHills) ? 1E-07f : terrainscale;
                return zDiff * scale;
            }

            // Batch yield calls in terrain coroutine
            static void CoroutinePostfix(ref IEnumerator __result)
            {
                if (!EnableTerrainMeshOptimization.Value) return;
                __result = BatchedTerrainCoroutine(__result);
            }

            private static IEnumerator BatchedTerrainCoroutine(IEnumerator original)
            {
                int batchSize = GetAdaptiveBatchSize(TerrainMeshChunkBatch.Value);
                int nullCount = 0;
                var sw = Stopwatch.StartNew();

                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = original.MoveNext();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[GameOptimizer] TerrainMeshOptimizationPatch coroutine error: {e}");
                        throw;
                    }

                    if (!hasNext) break;

                    if (original.Current == null)
                    {
                        nullCount++;
                        if (batchSize > 0 && nullCount >= batchSize)
                        {
                            nullCount = 0;
                            yield return null;
                        }
                    }
                    else
                    {
                        yield return original.Current;
                    }
                }

                sw.Stop();
                LogVerbose($"[GameOptimizer] Terrain mesh generation completed in {sw.ElapsedMilliseconds}ms");
            }
        }

        // =====================================================================
        // 12. AssignBuildingsToLots Optimization — Prefix on Start()
        //     Original yields every 1000 iterations (~16ms wasted per yield).
        //     This patch replaces the coroutine with a configurable batch size.
        // =====================================================================
        private static class AssignBuildingsOptimizationPatch
        {
            private static MethodInfo _markLotsForBoardwalkMethod;
            private static MethodInfo _markLotsForDecosMethod;
            private static MethodInfo _replaceMethod;
            private static MethodInfo _makeRowHousesMethod;
            private static MethodInfo _makeBoardwalkBookendsMethod;
            private static FieldInfo _rngField;
            private static FieldInfo _allField;
            private static FieldInfo _appealField;
            private static FieldInfo _buildingsField;
            private static FieldInfo _genconfigField;
            private static Type _assignBuildingsType;

            public static void ApplyManualPatch(Harmony harmony)
            {
                try
                {
                    _assignBuildingsType = typeof(GameClock).Assembly.GetType("Game.Session.Setup.AssignBuildingsToLots");
                    if (_assignBuildingsType == null)
                    {
                        Debug.LogError("[GameOptimizer] AssignBuildingsOptimizationPatch: AssignBuildingsToLots type not found");
                        return;
                    }

                    _markLotsForBoardwalkMethod = AccessTools.Method(_assignBuildingsType, "MarkLotsForBoardwalk");
                    _markLotsForDecosMethod = AccessTools.Method(_assignBuildingsType, "MarkLotsForDecos");
                    _replaceMethod = AccessTools.Method(_assignBuildingsType, "Replace");
                    _makeRowHousesMethod = AccessTools.Method(_assignBuildingsType, "MakeRowHouses");
                    _makeBoardwalkBookendsMethod = AccessTools.Method(_assignBuildingsType, "MakeBoardwalkBookends");
                    _rngField = AccessTools.Field(_assignBuildingsType, "_rng");
                    _allField = AccessTools.Field(_assignBuildingsType, "_all");
                    _appealField = AccessTools.Field(_assignBuildingsType, "_appeal");
                    _buildingsField = AccessTools.Field(_assignBuildingsType, "_buildings");
                    _genconfigField = AccessTools.Field(_assignBuildingsType, "_genconfig");

                    var original = AccessTools.Method(_assignBuildingsType, "Start");
                    var prefix = new HarmonyMethod(typeof(AssignBuildingsOptimizationPatch), "Prefix");
                    harmony.Patch(original, prefix: prefix);

                    Debug.Log("[GameOptimizer] AssignBuildingsOptimizationPatch applied successfully");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] AssignBuildingsOptimizationPatch setup failed: {e}");
                }
            }

            static bool Prefix(object __instance, ref IEnumerator __result)
            {
                if (!EnableAssignBuildingsOptimization.Value) return true;

                try
                {
                    __result = OptimizedStart(__instance);
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] AssignBuildingsOptimizationPatch error, falling back: {e}");
                    return true;
                }
            }

            private static IEnumerator OptimizedStart(object instance)
            {
                var sw = Stopwatch.StartNew();

                // Replicate the field initialization from original Start()
                var gameType = typeof(GameClock).Assembly.GetType("Game.Game");
                object gameCtx = gameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static).GetValue(null);

                // _genconfig = Game.ctx.session.mapconfig.generator
                object session = AccessTools.Field(gameCtx.GetType(), "session").GetValue(gameCtx);
                object mapconfig = AccessTools.Property(session.GetType(), "mapconfig").GetValue(session);
                object generator = AccessTools.Field(mapconfig.GetType(), "generator").GetValue(mapconfig);
                _genconfigField.SetValue(instance, generator);

                // _rng = Game.ctx.scenario.MakeSeededRng<AssignBuildingsToLots>()
                object scenario = AccessTools.Field(gameCtx.GetType(), "scenario").GetValue(gameCtx);
                var makeSeededRngMethod = AccessTools.Method(scenario.GetType(), "MakeSeededRng").MakeGenericMethod(_assignBuildingsType);
                object rng = makeSeededRngMethod.Invoke(scenario, null);
                _rngField.SetValue(instance, rng);

                // _all = entities query
                object entityman = AccessTools.Field(gameCtx.GetType(), "entityman").GetValue(gameCtx);
                var getCachedMethod = AccessTools.Method(entityman.GetType(), "GetCachedEntitiesByTagUnsafe");

                // TagConstants.TAG_EMPTY_LOT
                var tagConstantsType = typeof(GameClock).Assembly.GetType("Game.Session.Entities.TagConstants");
                Label tagEmptyLot = (Label)AccessTools.Field(tagConstantsType, "TAG_EMPTY_LOT").GetValue(null);

                var rawList = (IEnumerable<Entity>)getCachedMethod.Invoke(entityman, new object[] { tagEmptyLot });
                var all = rawList.Where(e => e.IsEnabled).ToList();
                all.Sort((Entity a, Entity b) => b.Id.index - a.Id.index);
                _allField.SetValue(instance, all);

                // _appeal and _buildings heatmaps
                object heatmaps = AccessTools.Field(gameCtx.GetType(), "heatmaps").GetValue(gameCtx);
                var manualUpdateMethod = AccessTools.Method(heatmaps.GetType(), "ManualUpdate", new[] { typeof(HeatmapType), typeof(int) });
                manualUpdateMethod.Invoke(heatmaps, new object[] { HeatmapType.Appeal, 1 });

                var heatmapType = typeof(GameClock).Assembly.GetType("Game.Session.Heatmaps.Heatmap");
                var findMethod = AccessTools.Method(heatmaps.GetType(), "Find", new[] { typeof(HeatmapType) });
                _appealField.SetValue(instance, findMethod.Invoke(heatmaps, new object[] { HeatmapType.Appeal }));
                _buildingsField.SetValue(instance, findMethod.Invoke(heatmaps, new object[] { HeatmapType.Buildings }));

                // Mark lots
                _markLotsForBoardwalkMethod.Invoke(instance, null);
                _markLotsForDecosMethod.Invoke(instance, null);

                int batchSize = GetAdaptiveBatchSize(AssignBuildingsIterationsPerFrame.Value);
                int i = 0;

                // Process all lots
                while (all.Count > 0)
                {
                    // RemoveLast<Entity> - use the extension method pattern
                    int lastIdx = all.Count - 1;
                    Entity lot = all[lastIdx];
                    all.RemoveAt(lastIdx);

                    _replaceMethod.Invoke(instance, new object[] { lot });
                    i++;
                    if (batchSize > 0 && i % batchSize == 0)
                    {
                        yield return null;
                    }
                }

                // Process edges for row houses and boardwalk bookends
                object board = AccessTools.Field(gameCtx.GetType(), "board").GetValue(gameCtx);
                object nodes = AccessTools.Field(board.GetType(), "nodes").GetValue(board);
                var getAllEdgesMethod = AccessTools.Method(nodes.GetType(), "GetAllEdgesUnsafe");
                var allEdges = (List<NodeEdge>)getAllEdgesMethod.Invoke(nodes, null);

                foreach (NodeEdge edge in allEdges)
                {
                    _makeRowHousesMethod.Invoke(instance, new object[] { edge, true });
                    _makeRowHousesMethod.Invoke(instance, new object[] { edge, false });
                    _makeBoardwalkBookendsMethod.Invoke(instance, new object[] { edge, true });
                    _makeBoardwalkBookendsMethod.Invoke(instance, new object[] { edge, false });
                    i++;
                    if (batchSize > 0 && i % batchSize == 0)
                    {
                        yield return null;
                    }
                }

                sw.Stop();
                LogVerbose($"[GameOptimizer] AssignBuildingsToLots completed in {sw.ElapsedMilliseconds}ms (processed {i} items, batchSize={batchSize})");
            }
        }

        // =====================================================================
        // 13. ManufactureModule Fix — Prefix on ManufactureModule.Initialize
        // Fixes crash when AI installs backroom modules. The game checks recipe
        // visibility against HumanPlayer instead of the building's controlling
        // player, causing empty recipe lists when AI installs modules.
        // =====================================================================
        private static class ManufactureModuleFixPatch
        {
            private static FieldInfo _dataField;
            private static FieldInfo _configField;
            private static FieldInfo _recipeIndexField;
            private static FieldInfo _lastUpdateField;
            private static FieldInfo _lastStallField;
            private static PropertyInfo _recipesProperty;
            private static Type _recipeType;
            private static FieldInfo _visreqsField;
            private static MethodInfo _allPassMethod;
            private static MethodInfo _generateMethod;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableManufactureModuleFix.Value) return;

                try
                {
                    var manufactureModuleType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.Modules.ManufactureModule");
                    if (manufactureModuleType == null)
                    {
                        Debug.LogError("[GameOptimizer] ManufactureModuleFixPatch: ManufactureModule type not found");
                        return;
                    }

                    var initMethod = AccessTools.Method(manufactureModuleType, "Initialize", new[] { typeof(ModuleInitData) });
                    if (initMethod == null)
                    {
                        Debug.LogError("[GameOptimizer] ManufactureModuleFixPatch: Initialize method not found");
                        return;
                    }

                    // Cache fields for performance
                    _dataField = AccessTools.Field(manufactureModuleType, "data");
                    _configField = AccessTools.Field(manufactureModuleType, "config");

                    var dataType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.Modules.ManufactureModuleData");
                    _recipeIndexField = AccessTools.Field(dataType, "recipeIndex");
                    _lastUpdateField = AccessTools.Field(dataType, "lastUpdate");
                    _lastStallField = AccessTools.Field(dataType, "lastStall");

                    var configType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.Modules.ManufactureModuleConfig");
                    _recipesProperty = AccessTools.Property(configType, "recipes");

                    _recipeType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.Modules.Recipe");
                    _visreqsField = AccessTools.Field(_recipeType, "visreqs");

                    var visreqsType = typeof(GameClock).Assembly.GetType("Game.Session.Data.VisReqList");
                    if (visreqsType != null)
                        _allPassMethod = AccessTools.Method(visreqsType, "AllPass");

                    // Get IRandom.Generate(int, int) method via reflection
                    var iRandomType = typeof(GameClock).Assembly.GetType("SomaSim.Util.IRandom");
                    if (iRandomType == null)
                    {
                        // Try the UnityGameTools assembly
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            iRandomType = asm.GetType("SomaSim.Util.IRandom");
                            if (iRandomType != null) break;
                        }
                    }
                    if (iRandomType != null)
                    {
                        _generateMethod = iRandomType.GetMethod("Generate", new[] { typeof(int), typeof(int) });
                    }

                    var prefix = new HarmonyMethod(typeof(ManufactureModuleFixPatch), "Prefix");
                    harmony.Patch(initMethod, prefix: prefix);

                    Debug.Log("[GameOptimizer] ManufactureModuleFixPatch applied successfully");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] ManufactureModuleFixPatch setup failed: {e}");
                }
            }

            static bool Prefix(object __instance, ModuleInitData init)
            {
                if (!EnableManufactureModuleFix.Value) return true;

                try
                {
                    // Get config from init
                    var config = init.config;

                    // Let base.Initialize handle setting config and creating data
                    // We need to call it, but Module<>.Initialize is the base
                    var baseType = __instance.GetType().BaseType;
                    var baseInit = AccessTools.Method(baseType, "Initialize", new[] { typeof(ModuleInitData) });
                    baseInit.Invoke(__instance, new object[] { init });

                    // Only process if this is a new module being created (not loaded)
                    if (!init.IsCreated) return false; // Skip original, we handled it

                    // Get data and config after base init
                    var data = _dataField.GetValue(__instance);
                    var modConfig = _configField.GetValue(__instance);

                    // Get the recipes list
                    object recipes;
                    if (_recipesProperty != null)
                    {
                        recipes = _recipesProperty.GetValue(modConfig);
                    }
                    else
                    {
                        var recipesField = AccessTools.Field(modConfig.GetType(), "recipes");
                        recipes = recipesField.GetValue(modConfig);
                    }

                    var recipesList = recipes as System.Collections.IList;
                    if (recipesList == null || recipesList.Count == 0)
                    {
                        Debug.LogWarning("[GameOptimizer] ManufactureModuleFixPatch: No recipes found");
                        return false;
                    }

                    // Set lastUpdate
                    var clockNow = G.ctx?.clock?.Now ?? default(SimTime);
                    _lastUpdateField.SetValue(data, clockNow);

                    // Create VisitState - but instead of using HumanPlayer, we'll try all recipes
                    // and pick from those that have no visreqs (safe default)
                    var validRecipes = new List<int>();

                    for (int i = 0; i < recipesList.Count; i++)
                    {
                        var recipe = recipesList[i];
                        var visreqs = _visreqsField?.GetValue(recipe);

                        // If no visreqs, recipe is always valid
                        if (visreqs == null)
                        {
                            validRecipes.Add(i);
                        }
                    }

                    // If we found recipes without visreqs, pick from those
                    // Otherwise, fall back to picking index 0 (first recipe)
                    int recipeIndex;
                    if (validRecipes.Count > 0)
                    {
                        // Pick random from valid recipes
                        if (validRecipes.Count == 1)
                        {
                            recipeIndex = validRecipes[0];
                        }
                        else
                        {
                            // Use the init.rng to pick via reflection
                            // Get rng field from init struct via reflection to avoid IRandom type dependency
                            var initType = typeof(ModuleInitData);
                            var rngField = initType.GetField("rng");
                            object rng = rngField?.GetValue(init);

                            int randomIdx;
                            if (_generateMethod != null && rng != null)
                            {
                                randomIdx = (int)_generateMethod.Invoke(rng, new object[] { 0, validRecipes.Count });
                            }
                            else
                            {
                                // Fallback to System.Random if reflection fails
                                randomIdx = new System.Random().Next(0, validRecipes.Count);
                            }
                            recipeIndex = validRecipes[randomIdx];
                        }
                    }
                    else
                    {
                        // No recipes without visreqs - just pick first recipe as fallback
                        // This prevents the crash, though the recipe may not be ideal
                        recipeIndex = 0;
                        Debug.LogWarning($"[GameOptimizer] ManufactureModuleFixPatch: All recipes have visreqs, using index 0 as fallback for {config?.Id}");
                    }

                    _recipeIndexField.SetValue(data, recipeIndex);
                    _lastStallField.SetValue(data, clockNow);

                    return false; // Skip original
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] ManufactureModuleFixPatch error, falling back to original: {e}");
                    return true; // Run original on error
                }
            }
        }

        // =====================================================================
        // Ethnicity Placement Fix — coroutine-based (no Harmony patching).
        // Waits for game to become interactive, then reassigns fake business
        // owners that were assigned with alphabetical bias during generation.
        // =====================================================================
        private static bool _ethnicityFixHasRun = false;
        private static readonly MethodInfo _findEthnicityMapMethod = AccessTools.Method(typeof(HeatmapManager), "FindEthnicityMap", new[] { typeof(Label) });

        private IEnumerator EthnicityPlacementFixCoroutine()
        {
            var rng = new System.Random();

            while (true)
            {
                yield return new WaitForSeconds(1f);

                try
                {
                    SessionContext ctx = G.SessionContext;
                    if (ctx == null || ctx.State < SessionState.Interactive)
                    {
                        continue;
                    }

                    if (_ethnicityFixHasRun)
                    {
                        yield break;
                    }

                    if (ctx.HasSaveFile)
                    {
                        Debug.Log("[GameOptimizer] EthnicityPlacementFix: skipping loaded save");
                        _ethnicityFixHasRun = true;
                        yield break;
                    }

                    if (_findEthnicityMapMethod == null)
                    {
                        Debug.LogWarning("[GameOptimizer] EthnicityPlacementFix: HeatmapManager.FindEthnicityMap unavailable");
                        _ethnicityFixHasRun = true;
                        yield break;
                    }

                    var mapConfig = ctx.session?.mapconfig;
                    List<Label> ethnicities = mapConfig?.GetEthnicitiesUniqueSorted();
                    if (ethnicities == null || ethnicities.Count <= 1)
                    {
                        _ethnicityFixHasRun = true;
                        yield break;
                    }

                    HeatmapManager heatmapManager = ctx.heatmaps;
                    if (heatmapManager == null || ctx.entityman == null)
                    {
                        continue;
                    }

                    var ethnicityMaps = new Dictionary<Label, Heatmap>();
                    bool mapsReady = true;
                    foreach (Label eth in ethnicities)
                    {
                        Heatmap heatmap = null;
                        try
                        {
                            heatmap = _findEthnicityMapMethod.Invoke(heatmapManager, new object[] { eth }) as Heatmap;
                        }
                        catch (Exception invokeEx)
                        {
                            Debug.LogWarning($"[GameOptimizer] EthnicityPlacementFix: heatmap lookup failed for {eth}: {invokeEx.Message}");
                        }

                        if (heatmap == null)
                        {
                            mapsReady = false;
                            break;
                        }

                        ethnicityMaps[eth] = heatmap;
                    }

                    if (!mapsReady || ethnicityMaps.Count <= 1)
                    {
                        continue;
                    }

                    List<Entity> allBiz = ctx.entityman.GetCachedEntitiesBizUnsafe()?.Where(b => b != null).ToList();
                    if (allBiz == null || allBiz.Count == 0)
                    {
                        continue;
                    }

                    int fixedCount = 0;
                    foreach (Entity biz in allBiz)
                    {
                        if (biz.data?.biz == null || biz.data?.board == null)
                        {
                            continue;
                        }

                        BizComponent bizComponent = biz.components?.biz;
                        if (bizComponent == null)
                        {
                            continue;
                        }

                        if (biz.data.biz.owner.IsReal || !biz.data.biz.owner.IsFake)
                        {
                            continue;
                        }

                        WorldPos pos = biz.data.board.worldpos;
                        float bestValue = float.NegativeInfinity;
                        var candidates = new List<Label>();

                        foreach (Label eth in ethnicities)
                        {
                            if (!ethnicityMaps.TryGetValue(eth, out Heatmap heatmap) || heatmap == null)
                            {
                                continue;
                            }

                            float val = heatmap.GetValueSafe(pos);
                            if (val > bestValue)
                            {
                                bestValue = val;
                                candidates.Clear();
                                candidates.Add(eth);
                            }
                            else if (val == bestValue)
                            {
                                candidates.Add(eth);
                            }
                        }

                        if (candidates.Count > 1)
                        {
                            Label newEth = candidates[rng.Next(candidates.Count)];
                            if (newEth != biz.data.biz.owner.eth)
                            {
                                bizComponent.AssignFakeOwner(newEth);
                                fixedCount++;
                            }
                        }
                    }

                    _ethnicityFixHasRun = true;
                    Debug.Log($"[GameOptimizer] EthnicityPlacementFix: reassigned {fixedCount} fake business owners with random tiebreaking");
                    yield break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameOptimizer] EthnicityPlacementFix error: {e}");
                    yield break;
                }
            }
        }

        // =====================================================================
        // 15. CreateEmptyLots Optimization — Batch null yields
        // =====================================================================
        private static class CreateEmptyLotsOptimizationPatch
        {
            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableCreateEmptyLotsOptimization.Value) return;
                try
                {
                    Type type = typeof(GameClock).Assembly.GetType("Game.Session.Setup.CreateEmptyLots");
                    if (type == null)
                    {
                        Debug.LogError("[GameOptimizer] CreateEmptyLotsOptimizationPatch: CreateEmptyLots type not found");
                        return;
                    }
                    MethodInfo method = AccessTools.Method(type, "MakeLotsAxisAligned", null, null);
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CreateEmptyLotsOptimizationPatch), "CoroutinePostfix", null);
                        harmony.Patch(method, null, postfix, null, null, null);
                    }
                    Debug.Log("[GameOptimizer] CreateEmptyLotsOptimizationPatch applied successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] CreateEmptyLotsOptimizationPatch setup failed: {ex}");
                }
            }

            private static void CoroutinePostfix(ref IEnumerator __result)
            {
                if (EnableCreateEmptyLotsOptimization.Value)
                    __result = BatchedCoroutine(__result);
            }

            private static IEnumerator BatchedCoroutine(IEnumerator original)
            {
                int batchSize = GetAdaptiveBatchSize(CreateEmptyLotsIterationsPerFrame.Value);
                int nullCount = 0;
                while (true)
                {
                    bool hasNext;
                    try { hasNext = original.MoveNext(); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[GameOptimizer] CreateEmptyLotsOptimizationPatch coroutine error: {e}");
                        throw;
                    }
                    if (!hasNext) break;
                    if (original.Current == null)
                    {
                        nullCount++;
                        if (batchSize > 0 && nullCount >= batchSize / 200)
                        {
                            nullCount = 0;
                            yield return null;
                        }
                    }
                    else
                    {
                        yield return original.Current;
                    }
                }
            }
        }

        // =====================================================================
        // 16. CreateMapNodes Optimization — Batch null yields
        // =====================================================================
        private static class CreateMapNodesOptimizationPatch
        {
            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableCreateMapNodesOptimization.Value) return;
                try
                {
                    Type type = typeof(GameClock).Assembly.GetType("Game.Session.Setup.CreateMapNodes");
                    if (type == null)
                    {
                        Debug.LogError("[GameOptimizer] CreateMapNodesOptimizationPatch: CreateMapNodes type not found");
                        return;
                    }
                    MethodInfo method = AccessTools.Method(type, "Start", null, null);
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CreateMapNodesOptimizationPatch), "CoroutinePostfix", null);
                        harmony.Patch(method, null, postfix, null, null, null);
                    }
                    Debug.Log("[GameOptimizer] CreateMapNodesOptimizationPatch applied successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] CreateMapNodesOptimizationPatch setup failed: {ex}");
                }
            }

            private static void CoroutinePostfix(ref IEnumerator __result)
            {
                if (EnableCreateMapNodesOptimization.Value)
                    __result = BatchedCoroutine(__result);
            }

            private static IEnumerator BatchedCoroutine(IEnumerator original)
            {
                int batchSize = GetAdaptiveBatchSize(CreateMapNodesIterationsPerFrame.Value);
                int nullCount = 0;
                while (true)
                {
                    bool hasNext;
                    try { hasNext = original.MoveNext(); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[GameOptimizer] CreateMapNodesOptimizationPatch coroutine error: {e}");
                        throw;
                    }
                    if (!hasNext) break;
                    if (original.Current == null)
                    {
                        nullCount++;
                        if (batchSize > 0 && nullCount >= batchSize / 100)
                        {
                            nullCount = 0;
                            yield return null;
                        }
                    }
                    else
                    {
                        yield return original.Current;
                    }
                }
            }
        }

        // =====================================================================
        // 17. MapDisplay Optimization — Reduce resolution & batch refresh
        // =====================================================================
        private static class MapDisplayOptimizationPatch
        {
            private static FieldInfo _resolutionField;
            private static FieldInfo _textureField;
            private static FieldInfo _colorBufferField;
            private static Type _mapDisplayType;
            private static Texture2D _disabledOverlayTexture;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableMapDisplayOptimization.Value && !(DisableTerritoryOverlay != null && DisableTerritoryOverlay.Value)) return;
                try
                {
                    _mapDisplayType = typeof(GameClock).Assembly.GetType("Game.Session.Board.MapDisplayManager");
                    if (_mapDisplayType == null)
                    {
                        Debug.LogError("[GameOptimizer] MapDisplayOptimizationPatch: MapDisplayManager type not found");
                        return;
                    }
                    _resolutionField = AccessTools.Field(_mapDisplayType, "RESOLUTION");
                    _textureField = AccessTools.Field(_mapDisplayType, "_texture");
                    _colorBufferField = AccessTools.Field(_mapDisplayType, "_colorBuffer");
                    if (_resolutionField == null)
                    {
                        Debug.LogError("[GameOptimizer] MapDisplayOptimizationPatch: RESOLUTION field not found");
                        return;
                    }
                    MethodInfo initMethod = AccessTools.Method(_mapDisplayType, "OnInitializeStarted", null, null);
                    if (initMethod != null)
                    {
                        harmony.Patch(initMethod, null, new HarmonyMethod(typeof(MapDisplayOptimizationPatch), "InitPostfix", null), null, null, null);
                    }
                    MethodInfo refreshMethod = AccessTools.Method(_mapDisplayType, "RefreshRegionAsync", null, null);
                    if (refreshMethod != null)
                    {
                        harmony.Patch(refreshMethod, null, new HarmonyMethod(typeof(MapDisplayOptimizationPatch), "RefreshPostfix", null), null, null, null);
                    }
                    Debug.Log("[GameOptimizer] MapDisplayOptimizationPatch applied successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] MapDisplayOptimizationPatch setup failed: {ex}");
                }
            }

            private static void InitPostfix(object __instance)
            {
                try
                {
                    Vector2Int current = (Vector2Int)_resolutionField.GetValue(__instance);
                    if (DisableTerritoryOverlay != null && DisableTerritoryOverlay.Value)
                    {
                        int width = Math.Max(1, current.x);
                        int height = Math.Max(1, current.y);
                        if (_disabledOverlayTexture == null || _disabledOverlayTexture.width != width || _disabledOverlayTexture.height != height)
                        {
                            _disabledOverlayTexture = new Texture2D(width, height);
                            Color[] clearColors = new Color[width * height];
                            for (int i = 0; i < clearColors.Length; i++)
                                clearColors[i] = Color.clear;
                            _disabledOverlayTexture.SetPixels(clearColors);
                            _disabledOverlayTexture.Apply();
                        }

                        _textureField.SetValue(__instance, _disabledOverlayTexture);
                        _colorBufferField.SetValue(__instance, new Color[width * height]);
                        Shader.SetGlobalTexture("_TerritoryTex", _disabledOverlayTexture);
                        Shader.SetGlobalFloat("_TerritoryBlend", 0f);
                        LogVerbose("[GameOptimizer] Territory overlay disabled");
                        return;
                    }

                    Shader.SetGlobalFloat("_TerritoryBlend", 1f);
                    if ((PreserveWorldTerrainFidelity != null && PreserveWorldTerrainFidelity.Value) || !EnableMapDisplayOptimization.Value)
                    {
                        return;
                    }

                    int res = GetAdaptiveMapDisplayResolution();
                    if (res < 128) res = 128;
                    if (res >= 1024) return;
                    if (current.x > res)
                    {
                        _resolutionField.SetValue(__instance, new Vector2Int(res, res));
                        var tex = new Texture2D(res, res);
                        var colors = new Color[res * res];
                        Color clear = Color.clear;
                        for (int i = 0; i < colors.Length; i++)
                            colors[i] = clear;
                        tex.SetPixels(colors);
                        tex.Apply();
                        _textureField.SetValue(__instance, tex);
                        _colorBufferField.SetValue(__instance, colors);
                        Shader.SetGlobalTexture("_TerritoryTex", tex);
                        float ratio = (float)(current.x * current.y) / (float)(res * res);
                        LogVerbose($"[GameOptimizer] Territory map resolution reduced from {current.x}x{current.y} to {res}x{res} ({ratio:F1}x fewer physics queries)");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] MapDisplayOptimizationPatch InitPostfix error: {ex}");
                }
            }

            private static void RefreshPostfix(ref IEnumerator __result)
            {
                if (DisableTerritoryOverlay != null && DisableTerritoryOverlay.Value)
                {
                    __result = DisabledRefresh();
                    return;
                }
                if (!EnableMapDisplayOptimization.Value)
                {
                    return;
                }
                __result = BatchedRefresh(__result);
            }

            private static IEnumerator DisabledRefresh()
            {
                yield break;
            }

            private static IEnumerator BatchedRefresh(IEnumerator original)
            {
                int targetBatch = GetAdaptiveBatchSize(MapDisplayBatchSize.Value);
                int origBatch = 4000;
                int yieldSkips = Math.Max(1, targetBatch / origBatch);
                int nullCount = 0;
                while (true)
                {
                    bool hasNext;
                    try { hasNext = original.MoveNext(); }
                    catch (KeyNotFoundException)
                    {
                        // Some map states can briefly reference a player color key before it's rebuilt.
                        // Abort this refresh batch and let the next refresh recover.
                        yield break;
                    }
                    catch (InvalidOperationException)
                    {
                        // Underlying map state can change during refresh in rare transitions.
                        // Exit this pass and allow next refresh to run with settled data.
                        yield break;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[GameOptimizer] MapDisplayOptimizationPatch coroutine error: {e}");
                        yield break;
                    }
                    if (!hasNext) break;
                    if (original.Current == null)
                    {
                        nullCount++;
                        if (nullCount >= yieldSkips)
                        {
                            nullCount = 0;
                            yield return null;
                        }
                    }
                    else
                    {
                        yield return original.Current;
                    }
                }
            }
        }

        // =====================================================================
        // 18. Fog of War Refresh Optimization — Batch full refresh work
        // =====================================================================
        private static class FogOfWarRefreshOptimizationPatch
        {
            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableFogRefreshOptimization.Value) return;
                try
                {
                    MethodInfo refreshMethod = AccessTools.Method(typeof(FogOfWarManager), "RefreshEntireMap");
                    if (refreshMethod == null)
                    {
                        Debug.LogWarning("[GameOptimizer] FogOfWarRefreshOptimizationPatch: RefreshEntireMap not found");
                        return;
                    }

                    harmony.Patch(refreshMethod, new HarmonyMethod(typeof(FogOfWarRefreshOptimizationPatch), "Prefix"), null, null, null, null);
                    Debug.Log("[GameOptimizer] FogOfWarRefreshOptimizationPatch applied successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] FogOfWarRefreshOptimizationPatch setup failed: {ex}");
                }
            }

            private static bool Prefix(FogOfWarManager __instance, ref IEnumerator __result)
            {
                if (!EnableFogRefreshOptimization.Value || __instance == null)
                    return true;

                __result = BatchedRefresh(__instance);
                return false;
            }

            private static IEnumerator BatchedRefresh(FogOfWarManager manager)
            {
                Stopwatch sw = Stopwatch.StartNew();
                SessionContext ctx = G.SessionContext;
                if (ctx == null)
                    yield break;

                PlayerID pid = ctx.players.Human.PID;
                List<Node> allNodesUnsafe = ctx.board.nodes.GetAllNodesUnsafe();
                int batchSize = Math.Max(1, GetAdaptiveBatchSize(FogRefreshNodesPerYield.Value));
                int revealedSinceYield = 0;
                int totalRevealed = 0;

                for (int i = 0; i < allNodesUnsafe.Count; i++)
                {
                    Node node = allNodesUnsafe[i];
                    if (node == null || !node.known.Get(pid))
                        continue;

                    manager.RevealNodeRegion(node, isFirstCall: true);
                    revealedSinceYield++;
                    totalRevealed++;

                    if (revealedSinceYield >= batchSize)
                    {
                        revealedSinceYield = 0;
                        yield return null;
                    }
                }

                if (revealedSinceYield > 0)
                    yield return null;

                sw.Stop();
                LogVerbose($"[GameOptimizer] Fog refresh completed in {sw.ElapsedMilliseconds}ms for {totalRevealed} nodes (batch={batchSize})");
            }
        }

        // =====================================================================
        // 19. IndirectRenderer Optimization — Force separate culling
        // =====================================================================
        private static class IndirectRendererOptimizationPatch
        {
            private static FieldInfo _separateField;
            private static readonly HashSet<int> _patchedInstances = new HashSet<int>();

            internal static void ResetRuntime()
            {
                _patchedInstances.Clear();
            }

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableIndirectRendererOptimization.Value) return;
                try
                {
                    Type type = typeof(GameClock).Assembly.GetType("IndirectRendering.IndirectRenderer");
                    if (type == null)
                    {
                        Debug.LogError("[GameOptimizer] IndirectRendererOptimizationPatch: IndirectRenderer type not found");
                        return;
                    }
                    _separateField = AccessTools.Field(type, "_separateCullingOverFrames");
                    if (_separateField == null)
                    {
                        Debug.LogError("[GameOptimizer] IndirectRendererOptimizationPatch: _separateCullingOverFrames field not found");
                        return;
                    }
                    MethodInfo method = AccessTools.Method(type, "Update", null, null);
                    if (method != null)
                    {
                        harmony.Patch(method, new HarmonyMethod(typeof(IndirectRendererOptimizationPatch), "UpdatePrefix", null), null, null, null, null);
                    }
                    Debug.Log("[GameOptimizer] IndirectRendererOptimizationPatch applied successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] IndirectRendererOptimizationPatch setup failed: {ex}");
                }
            }

            private static void UpdatePrefix(object __instance)
            {
                try
                {
                    if (__instance == null || _separateField == null)
                        return;

                    int id = __instance.GetHashCode();
                    if (_patchedInstances.Contains(id))
                        return;

                    if (!(bool)_separateField.GetValue(__instance))
                        _separateField.SetValue(__instance, true);

                    _patchedInstances.Add(id);
                }
                catch { }
            }
        }

        // =====================================================================
        // 20. Territory Rebuild Debounce — Prevent redundant rebuilds per frame
        // =====================================================================
        private static class TerritoryRebuildDebouncePatch
        {
            private static FieldInfo _territoryDataField;
            private static FieldInfo _ownedNodesField;
            private static FieldInfo _cachedPotentialsField;
            private static FieldInfo _pidField;
            private static MethodInfo _onTerritoryChangedMethod;
            private static HashSet<int> _dirtyPlayerIds = new HashSet<int>();
            private static int _lastRebuildFrame = -1;

            internal static void ResetRuntime()
            {
                _dirtyPlayerIds.Clear();
                _lastRebuildFrame = -1;
            }

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableTerritoryRebuildDebounce.Value) return;
                try
                {
                    Type terrType = typeof(PlayerTerritory);
                    _territoryDataField = AccessTools.Field(terrType, "_territory");
                    _pidField = AccessTools.Field(typeof(PlayerSubmanager), "_pid");
                    if (_territoryDataField != null)
                        _ownedNodesField = AccessTools.Field(_territoryDataField.FieldType, "ownedNodes");
                    _cachedPotentialsField = AccessTools.Field(terrType, "_cachedPotentials");
                    if (_cachedPotentialsField != null)
                        _onTerritoryChangedMethod = AccessTools.Method(_cachedPotentialsField.FieldType, "OnTerritoryChanged", null, null);

                    MethodInfo method = terrType.GetMethod("UpdateNodeDataOnTerritoryChange", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        harmony.Patch(method, new HarmonyMethod(typeof(TerritoryRebuildDebouncePatch), "Prefix", null), null, null, null, null);
                        Debug.Log("[GameOptimizer] TerritoryRebuildDebouncePatch applied successfully");
                    }
                    else
                    {
                        Debug.LogWarning("[GameOptimizer] TerritoryRebuildDebouncePatch: UpdateNodeDataOnTerritoryChange not found");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] TerritoryRebuildDebouncePatch setup failed: {ex}");
                }
            }

            private static bool Prefix(PlayerTerritory __instance)
            {
                try
                {
                    int frame = Time.frameCount;
                    int playerId = ((PlayerID)_pidField.GetValue(__instance)).id;
                    if (frame == _lastRebuildFrame && _dirtyPlayerIds.Contains(playerId))
                        return false;
                    if (frame != _lastRebuildFrame)
                    {
                        _dirtyPlayerIds.Clear();
                        _lastRebuildFrame = frame;
                    }
                    _dirtyPlayerIds.Add(playerId);
                    return true;
                }
                catch
                {
                    return true;
                }
            }
        }

        // =====================================================================
        // 21. Clock Reset On Load — Reset anim clock after save game load
        // =====================================================================
        private static class ClockResetOnLoadPatch
        {
            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableClockResetOnLoad.Value) return;
                try
                {
                    MethodInfo loadMethod = AccessTools.Method(typeof(GameClock), "Load");
                    if (loadMethod != null)
                    {
                        harmony.Patch(loadMethod, null,
                            new HarmonyMethod(typeof(ClockResetOnLoadPatch), "LoadPostfix", null),
                            null, null, null);
                        Debug.Log("[GameOptimizer] ClockResetOnLoadPatch applied to GameClock.Load");
                    }
                    else
                    {
                        Debug.LogWarning("[GameOptimizer] ClockResetOnLoadPatch: GameClock.Load not found, threshold reset remains the fallback");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] ClockResetOnLoadPatch setup failed: {ex}");
                }
            }

            private static void LoadPostfix(GameClock __instance, ref IEnumerator __result)
            {
                if (__instance == null || __result == null)
                    return;

                __result = WrapLoad(__instance, __result);
            }

            private static IEnumerator WrapLoad(GameClock clock, IEnumerator original)
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = original.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameOptimizer] ClockResetOnLoad wrapper failed during load: {ex}");
                        throw;
                    }

                    if (!hasNext)
                        break;

                    yield return original.Current;
                }

                try
                {
                    ResetRuntimeOptimizations();

                    GameAnimUpdate anim = clock.AnimState;
                    if (anim == null)
                        yield break;

                    float oldValue = anim.cumulativeSeconds;
                    anim.cumulativeSeconds = 0f;
                    anim.frameDeltaSeconds = 0f;
                    Debug.Log($"[GameOptimizer] ClockResetOnLoad: Reset cumulativeSeconds from {oldValue:F1} to 0 after GameClock.Load");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] ClockResetOnLoad post-load reset failed: {ex}");
                }
            }
        }

        // =====================================================================
        // 22. Interactive Memory Cleanup — Defer reclaim work after load
        // =====================================================================
        private static class InteractiveMemoryCleanupPatch
        {
            private static Coroutine _activeCleanup;
            private static readonly FieldInfo MapDisplayCurrentUpdateRoutineField = AccessTools.Field(typeof(MapDisplayManager), "_currentUpdateRoutine");
            private static readonly FieldInfo FogOfWarDirtyField = AccessTools.Field(typeof(FogOfWarManager), "_isDirty");
            private const float CleanupBusyPollSeconds = 1f;
            private const float MinimumInteractiveIdleWaitSeconds = 20f;

            public static void ApplyManualPatch(Harmony harmony)
            {
                if (!EnableDeferredInteractiveMemoryCleanup.Value) return;
                try
                {
                    MethodInfo method = AccessTools.Method(typeof(global::Game.Game), "ReclaimMemoryAsync");
                    if (method == null)
                    {
                        Debug.LogWarning("[GameOptimizer] InteractiveMemoryCleanupPatch: ReclaimMemoryAsync not found");
                        return;
                    }

                    harmony.Patch(method, new HarmonyMethod(typeof(InteractiveMemoryCleanupPatch), "Prefix"), null, null, null, null);
                    Debug.Log("[GameOptimizer] InteractiveMemoryCleanupPatch applied successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] InteractiveMemoryCleanupPatch setup failed: {ex}");
                }
            }

            private static bool Prefix(global::Game.Game __instance, bool waitForGC)
            {
                if (!EnableDeferredInteractiveMemoryCleanup.Value || __instance == null)
                    return true;

                SessionContext ctx = G.SessionContext;
                if (ctx == null)
                    return true;

                try
                {
                    if (_activeCleanup != null)
                        return false;

                    _activeCleanup = __instance.StartCoroutine(RunCleanup(waitForGC));
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] InteractiveMemoryCleanupPatch failed to schedule cleanup: {ex}");
                    return true;
                }
            }

            private static IEnumerator RunCleanup(bool waitForGC)
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    while (true)
                    {
                        SessionContext ctx = G.SessionContext;
                        if (ctx == null || ctx.IsInteractive)
                            break;
                        yield return null;
                    }

                    float delay = Mathf.Max(0f, DeferredInteractiveMemoryCleanupDelaySeconds.Value);
                    if (delay > 0f)
                        yield return new WaitForSecondsRealtime(delay);

                    float idleWaitStartedAt = Time.realtimeSinceStartup;
                    float maxIdleWait = Mathf.Max(MinimumInteractiveIdleWaitSeconds, delay * 3f);
                    while (ShouldWaitForSaferCleanupWindow(out string busyReason))
                    {
                        if (Time.realtimeSinceStartup - idleWaitStartedAt >= maxIdleWait)
                        {
                            LogVerbose($"[GameOptimizer] Interactive cleanup proceeding after max wait while busy reason={busyReason}");
                            break;
                        }

                        LogVerbose($"[GameOptimizer] Interactive cleanup waiting reason={busyReason}");
                        yield return new WaitForSecondsRealtime(CleanupBusyPollSeconds);
                    }

                    GC.Collect();
                    if (waitForGC && WaitForPendingFinalizersDuringDeferredCleanup.Value)
                        GC.WaitForPendingFinalizers();

                    if (UnloadUnusedAssetsDuringInteractiveCleanup.Value)
                    {
                        AsyncOperation unload = Resources.UnloadUnusedAssets();
                        while (unload != null && !unload.isDone)
                            yield return null;
                    }
                    else
                    {
                        LogVerbose("[GameOptimizer] Interactive cleanup skipped Resources.UnloadUnusedAssets to avoid runtime hitching");
                    }

                    sw.Stop();
                    LogVerbose($"[GameOptimizer] Interactive cleanup completed in {sw.ElapsedMilliseconds}ms after {delay:0.##}s delay");
                }
                finally
                {
                    _activeCleanup = null;
                }
            }

            private static bool ShouldWaitForSaferCleanupWindow(out string reason)
            {
                reason = null;
                SessionContext ctx = G.SessionContext;
                if (ctx == null || !ctx.IsInteractive)
                {
                    return false;
                }

                if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                {
                    reason = "mouse-held";
                    return true;
                }

                if (ctx.clock != null && !ctx.clock.AreAnimationsPausedByUI)
                {
                    reason = "animations-not-paused-by-ui";
                    return true;
                }

                if (ctx.mapdisplay != null && MapDisplayCurrentUpdateRoutineField?.GetValue(ctx.mapdisplay) != null)
                {
                    reason = "territory-refresh-active";
                    return true;
                }

                if (ctx.fogofwar != null && FogOfWarDirtyField != null)
                {
                    object dirtyValue = FogOfWarDirtyField.GetValue(ctx.fogofwar);
                    if (dirtyValue is bool isDirty && isDirty)
                    {
                        reason = "fog-refresh-dirty";
                        return true;
                    }
                }

                return false;
            }
        }

        // =====================================================================
        // 22a. TerrainGenData.CheckIntersection Null Guard
        //      Prevents NullRef when waterData is null during terrain mesh generation
        // =====================================================================
        private static class TerrainGenDataNullGuardPatch
        {
            public static void ApplyManualPatch(Harmony harmony)
            {
                try
                {
                    var terrainGenDataType = typeof(GameClock).Assembly.GetType("Game.Session.Board.TerrainGenData");
                    if (terrainGenDataType == null)
                    {
                        Debug.LogWarning("[GameOptimizer] TerrainGenDataNullGuardPatch: TerrainGenData type not found");
                        return;
                    }
                    MethodInfo checkMethod = AccessTools.Method(terrainGenDataType, "CheckIntersection");
                    if (checkMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(TerrainGenDataNullGuardPatch), "Prefix");
                        harmony.Patch(checkMethod, prefix: prefix);
                        Debug.Log("[GameOptimizer] TerrainGenDataNullGuardPatch applied successfully");
                    }
                    else
                    {
                        Debug.LogWarning("[GameOptimizer] TerrainGenDataNullGuardPatch: CheckIntersection method not found");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] TerrainGenDataNullGuardPatch setup failed: {ex}");
                }
            }

            static bool Prefix(object __instance, ref bool __result)
            {
                try
                {
                    // Check if waterData field is null
                    FieldInfo waterDataField = AccessTools.Field(__instance.GetType(), "waterData");
                    if (waterDataField != null)
                    {
                        object waterData = waterDataField.GetValue(__instance);
                        if (waterData == null)
                        {
                            // waterData is null — only check mountains
                            FieldInfo mountainDataField = AccessTools.Field(__instance.GetType(), "mountainData");
                            if (mountainDataField != null)
                            {
                                object mountainData = mountainDataField.GetValue(__instance);
                                if (mountainData != null)
                                {
                                    // Just check mountains, skip water check
                                    // Return false (no intersection) if mountain check also fails
                                    __result = false;
                                    return false; // skip original
                                }
                            }
                            __result = false;
                            return false;
                        }
                    }
                }
                catch { }

                return true; // waterData exists, let original run
            }
        }

        // =====================================================================
        // 22b. DensityHeatmap Null Guard — Prevent NullRef in TryCacheRivers
        //     when WaterData is not set on terrain
        // =====================================================================
        private static class DensityHeatmapNullGuardPatch
        {
            public static void ApplyManualPatch(Harmony harmony)
            {
                try
                {
                    var densityHeatmapType = typeof(GameClock).Assembly.GetType("Game.Session.Heatmaps.DensityHeatmap");
                    if (densityHeatmapType == null)
                    {
                        Debug.LogWarning("[GameOptimizer] DensityHeatmapNullGuardPatch: DensityHeatmap type not found");
                        return;
                    }
                    MethodInfo tryCacheRivers = AccessTools.Method(densityHeatmapType, "TryCacheRivers");
                    if (tryCacheRivers != null)
                    {
                        var prefix = new HarmonyMethod(typeof(DensityHeatmapNullGuardPatch), "Prefix");
                        harmony.Patch(tryCacheRivers, prefix: prefix);
                        Debug.Log("[GameOptimizer] DensityHeatmapNullGuardPatch applied successfully");
                    }
                    else
                    {
                        Debug.LogWarning("[GameOptimizer] DensityHeatmapNullGuardPatch: TryCacheRivers method not found");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] DensityHeatmapNullGuardPatch setup failed: {ex}");
                }
            }

            static bool Prefix(object __instance)
            {
                try
                {
                    // Check if WaterData is available on terrain before proceeding
                    var gameType = typeof(GameClock).Assembly.GetType("Game.Game");
                    object gameCtx = gameType?.GetField("ctx", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (gameCtx == null) return false; // skip

                    object board = AccessTools.Field(gameCtx.GetType(), "board")?.GetValue(gameCtx);
                    if (board == null) return false;

                    object terrain = AccessTools.Field(board.GetType(), "terrain")?.GetValue(board);
                    if (terrain == null) return false;

                    // Check WaterData property
                    var waterDataProp = AccessTools.Property(terrain.GetType(), "WaterData");
                    object waterData = waterDataProp?.GetValue(terrain);
                    if (waterData == null)
                    {
                        // WaterData is null — set _riversCached = true and _riverHeatmapPositions to empty list
                        // to prevent repeated null access attempts
                        FieldInfo riversCachedField = AccessTools.Field(__instance.GetType(), "_riversCached");
                        FieldInfo riverPositionsField = AccessTools.Field(__instance.GetType(), "_riverHeatmapPositions");
                        if (riversCachedField != null)
                            riversCachedField.SetValue(__instance, true);
                        if (riverPositionsField != null)
                        {
                            object existing = riverPositionsField.GetValue(__instance);
                            if (existing == null)
                            {
                                // Create empty List<HeatmapPos>
                                Type heatmapPosType = typeof(GameClock).Assembly.GetType("Game.Core.HeatmapPos");
                                if (heatmapPosType != null)
                                {
                                    var listType = typeof(List<>).MakeGenericType(heatmapPosType);
                                    riverPositionsField.SetValue(__instance, Activator.CreateInstance(listType));
                                }
                            }
                        }
                        Debug.LogWarning("[GameOptimizer] DensityHeatmap.TryCacheRivers skipped: WaterData is null on terrain");
                        return false; // skip original
                    }

                    // Also check waterRegions list
                    FieldInfo waterRegionsField = AccessTools.Field(waterData.GetType(), "waterRegions");
                    object waterRegions = waterRegionsField?.GetValue(waterData);
                    if (waterRegions == null)
                    {
                        FieldInfo riversCachedField = AccessTools.Field(__instance.GetType(), "_riversCached");
                        FieldInfo riverPositionsField = AccessTools.Field(__instance.GetType(), "_riverHeatmapPositions");
                        if (riversCachedField != null)
                            riversCachedField.SetValue(__instance, true);
                        if (riverPositionsField != null)
                        {
                            object existing = riverPositionsField.GetValue(__instance);
                            if (existing == null)
                            {
                                Type heatmapPosType = typeof(GameClock).Assembly.GetType("Game.Core.HeatmapPos");
                                if (heatmapPosType != null)
                                {
                                    var listType = typeof(List<>).MakeGenericType(heatmapPosType);
                                    riverPositionsField.SetValue(__instance, Activator.CreateInstance(listType));
                                }
                            }
                        }
                        Debug.LogWarning("[GameOptimizer] DensityHeatmap.TryCacheRivers skipped: waterRegions is null");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameOptimizer] DensityHeatmapNullGuard prefix error: {ex}");
                    // Don't skip - let original run and potentially throw (will be caught by Unity)
                }

                return true; // WaterData exists, let original run
            }
        }
    }
}

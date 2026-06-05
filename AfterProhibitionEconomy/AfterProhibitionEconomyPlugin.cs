using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Entities;
using HarmonyLib;

namespace AfterProhibitionEconomy
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class AfterProhibitionEconomyPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.economy";
		public const string PluginName = "After Prohibition Economy";
		public const string PluginVersion = "0.9.9";

		internal static ManualLogSource Log { get; private set; }

		internal static AfterProhibitionEconomyPlugin Instance { get; private set; }

		internal static ConfigEntry<bool> EnableEconomyBaselineLog { get; private set; }

		internal static ConfigEntry<bool> EnableStartupEconomyDiagnostics { get; private set; }

		internal static ConfigEntry<bool> EnableStartupEconomyAudit { get; private set; }

		internal static ConfigEntry<bool> EnableShopAccessSampleLog { get; private set; }

		internal static ConfigEntry<int> ShopAccessSampleLimit { get; private set; }

		internal static ConfigEntry<bool> EnableCivicPurchaseAccessSampleLog { get; private set; }

		internal static ConfigEntry<int> CivicPurchaseAccessSampleLimit { get; private set; }

		internal static ConfigEntry<bool> EnableShopAccessBridge { get; private set; }

		internal static ConfigEntry<bool> EnableCivicPurchaseAccessBridge { get; private set; }

		internal static ConfigEntry<bool> EnablePurchaseStockRefresh { get; private set; }

		internal static ConfigEntry<bool> EnablePurchaseStockRefreshSummaryWhenNoCandidates { get; private set; }

		internal static ConfigEntry<bool> EnableEmptyBusinessModuleRepair { get; private set; }

		internal static ConfigEntry<bool> EnableEmptyBusinessModuleRepairSummaryWhenNoCandidates { get; private set; }

		internal static ConfigEntry<bool> EnableEmptyBusinessModuleRepairDetailLog { get; private set; }

		internal static ConfigEntry<int> EmptyBusinessModuleRepairDetailLimit { get; private set; }

		internal static ConfigEntry<bool> EnableDirtyCashRoutingSampleLog { get; private set; }

		internal static ConfigEntry<int> DirtyCashRoutingSampleLimit { get; private set; }

		internal static ConfigEntry<bool> EnableDirtyCashRoutingBridge { get; private set; }

		internal static ConfigEntry<bool> EnableDirtyCashRuntimeSweepBridge { get; private set; }

		internal static ConfigEntry<bool> EnableDirtyCashRuntimeSweepOwner { get; private set; }

		internal static ConfigEntry<bool> EnablePlayerLegalBusinessConsumerBridge { get; private set; }

		internal static ConfigEntry<bool> EnablePlayerLegalBusinessConsumerRuntimeOwner { get; private set; }

		internal static ConfigEntry<bool> EnableFrontResourceAudit { get; private set; }

		internal static ConfigEntry<int> FrontResourceAuditSampleLimit { get; private set; }

		internal static ConfigEntry<bool> EnableFrontResourceBridge { get; private set; }

		internal static ConfigEntry<bool> EnableRouteShopOrderBridge { get; private set; }

		internal static ConfigEntry<bool> EnablePostBusinessUpdateDiagnostics { get; private set; }

		private Harmony _harmony;
		private bool _loggedEconomyBaseline;

		private void Awake()
		{
			Instance = this;
			Log = Logger;

			BindConfig();

			Logger.LogInfo("economy baseline scheduled ownsRoutes=False ownsUi=False ownsFamily=False ownsPolitics=False");

			_harmony = new Harmony(PluginGuid);
			_harmony.PatchAll();
			PurchaseStockRefreshPatch.ApplyPatch(_harmony);
			EmptyBusinessModuleRepairPatch.ApplyPatch(_harmony);
			DirtyCashRuntimeSweepPatch.ApplyPatch(_harmony);
			PlayerLegalBusinessConsumerRuntimePatch.ApplyPatch(_harmony);
			PostBusinessUpdateDiagnosticsPatch.ApplyPatch(_harmony);

			Logger.LogInfo($"{PluginName} {PluginVersion} loaded phase=dirty-cash-runtime-owner ownsRoutes=False ownsUi=False ownsFamily=False ownsPolitics=False");
		}

		private IEnumerator Start()
		{
			yield return null;
			yield return new UnityEngine.WaitForSecondsRealtime(1f);
			LogEconomyBaseline("start-1s");
			yield return LogStartupEconomyDiagnosticsWhenReady("start-1s");
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
			EnableEconomyBaselineLog = Config.Bind(
				"Features",
				"EnableEconomyBaselineLog",
				true,
				"Logs a concise read-only economy baseline once shortly after startup.");

			EnableStartupEconomyDiagnostics = Config.Bind(
				"Features",
				"EnableStartupEconomyDiagnostics",
				false,
				"Allows full startup economy diagnostic scans. Disabled by default because full-map scans can stall map loading on large generated maps. Bridge methods and repair behavior remain available.");

			EnableStartupEconomyAudit = Config.Bind(
				"Features",
				"EnableStartupEconomyAudit",
				true,
				"Logs read-only shop, bank, warehouse, front, stock, and module counts once shortly after startup.");

			EnableShopAccessSampleLog = Config.Bind(
				"Features",
				"EnableShopAccessSampleLog",
				true,
				"Logs read-only shop buy/sell access classification summary and a limited sample once shortly after startup.");

			ShopAccessSampleLimit = Config.Bind(
				"Features",
				"ShopAccessSampleLimit",
				12,
				"Maximum read-only shop-access sample lines to log at startup.");

			EnableCivicPurchaseAccessSampleLog = Config.Bind(
				"Features",
				"EnableCivicPurchaseAccessSampleLog",
				true,
				"Logs read-only bank and wholesale warehouse purchase-access classification once shortly after startup.");

			CivicPurchaseAccessSampleLimit = Config.Bind(
				"Features",
				"CivicPurchaseAccessSampleLimit",
				12,
				"Maximum read-only bank/warehouse purchase-access sample lines to log at startup.");

			EnableShopAccessBridge = Config.Bind(
				"Features",
				"EnableShopAccessBridge",
				true,
				"Exposes read-only shop buy/sell classification bridge methods for GameplayTweaks delegation checks.");

			EnableCivicPurchaseAccessBridge = Config.Bind(
				"Features",
				"EnableCivicPurchaseAccessBridge",
				true,
				"Exposes read-only bank and wholesale warehouse purchase-access classification bridge methods for GameplayTweaks delegation checks.");

			EnablePurchaseStockRefresh = Config.Bind(
				"Features",
				"EnablePurchaseStockRefresh",
				true,
				"Refreshes NPC business purchase stock when installed producing modules have no player-buy offers. GameplayTweaks remains as fallback until live validation completes.");

			EnablePurchaseStockRefreshSummaryWhenNoCandidates = Config.Bind(
				"Features",
				"EnablePurchaseStockRefreshSummaryWhenNoCandidates",
				true,
				"Logs purchase-stock-refresh summary lines even when no refresh candidates are found.");

			EnableEmptyBusinessModuleRepair = Config.Bind(
				"Features",
				"EnableEmptyBusinessModuleRepair",
				true,
				"Repairs NPC businesses whose installed modules are missing or do not match their business configuration. GameplayTweaks remains as fallback until live validation completes.");

			EnableEmptyBusinessModuleRepairSummaryWhenNoCandidates = Config.Bind(
				"Features",
				"EnableEmptyBusinessModuleRepairSummaryWhenNoCandidates",
				true,
				"Logs empty-business-module-repair summary lines even when no repair candidates are found.");

			EnableEmptyBusinessModuleRepairDetailLog = Config.Bind(
				"Features",
				"EnableEmptyBusinessModuleRepairDetailLog",
				false,
				"Logs individual empty-business-module-repaired lines. Disabled by default because initial map generation can repair thousands of businesses.");

			EmptyBusinessModuleRepairDetailLimit = Config.Bind(
				"Features",
				"EmptyBusinessModuleRepairDetailLimit",
				12,
				"Maximum individual empty-business-module-repaired lines to log when detail logging is enabled.");

			EnableDirtyCashRoutingSampleLog = Config.Bind(
				"Features",
				"EnableDirtyCashRoutingSampleLog",
				true,
				"Logs read-only Dirty Cash routing classification after the startup economy diagnostics are ready.");

			DirtyCashRoutingSampleLimit = Config.Bind(
				"Features",
				"DirtyCashRoutingSampleLimit",
				12,
				"Maximum read-only dirty-cash-routing sample lines to log at startup.");

			EnableDirtyCashRoutingBridge = Config.Bind(
				"Features",
				"EnableDirtyCashRoutingBridge",
				true,
				"Exposes read-only Dirty Cash routing classification bridge methods for GameplayTweaks delegation checks.");

			EnableDirtyCashRuntimeSweepBridge = Config.Bind(
				"Features",
				"EnableDirtyCashRuntimeSweepBridge",
				true,
				"Exposes Dirty Cash runtime sweep source classification for GameplayTweaks delegation checks.");

			EnableDirtyCashRuntimeSweepOwner = Config.Bind(
				"Features",
				"EnableDirtyCashRuntimeSweepOwner",
				true,
				"Owns Dirty Cash runtime safety sweep mutation. GameplayTweaks should skip this mutation path when enabled.");

			EnablePlayerLegalBusinessConsumerBridge = Config.Bind(
				"Features",
				"EnablePlayerLegalBusinessConsumerBridge",
				true,
				"Exposes read-only player legal business consumer classification for GameplayTweaks delegation checks. Actual consumer update mutation remains in GameplayTweaks.");

			EnablePlayerLegalBusinessConsumerRuntimeOwner = Config.Bind(
				"Features",
				"EnablePlayerLegalBusinessConsumerRuntimeOwner",
				true,
				"Owns player legal business consumer runtime observation and summary logging. GameplayTweaks should skip this branch when enabled.");

			EnableFrontResourceAudit = Config.Bind(
				"Features",
				"EnableFrontResourceAudit",
				true,
				"Logs read-only front/business resource classification after the startup economy diagnostics are ready.");

			FrontResourceAuditSampleLimit = Config.Bind(
				"Features",
				"FrontResourceAuditSampleLimit",
				12,
				"Maximum read-only front-resource sample lines to log at startup.");

			EnableFrontResourceBridge = Config.Bind(
				"Features",
				"EnableFrontResourceBridge",
				true,
				"Exposes read-only front/business resource classification bridge methods for GameplayTweaks delegation checks.");

			EnableRouteShopOrderBridge = Config.Bind(
				"Features",
				"EnableRouteShopOrderBridge",
				true,
				"Exposes read-only staged route shop-order classification bridge methods for GameplayTweaks delegation checks.");

			EnablePostBusinessUpdateDiagnostics = Config.Bind(
				"Features",
				"EnablePostBusinessUpdateDiagnostics",
				false,
				"Runs Dirty Cash and front-resource diagnostics after BusinessUpdate.UpdateBusinessModules. Disabled by default because full-map scans can block new-game map startup.");
		}

		private void LogEconomyBaseline(string source)
		{
			if (_loggedEconomyBaseline || !EnableEconomyBaselineLog.Value)
			{
				return;
			}

			_loggedEconomyBaseline = true;

			Logger.LogInfo(
				"economy-baseline source=" + source +
				" phase=dirty-cash-runtime-owner ownsRoutes=False ownsUi=False ownsFamily=False ownsPolitics=False ownsCompatibility=False patches=5 dirtyCashRuntimeSweepMutationOwner=" + OwnsDirtyCashRuntimeSweepMutation() +
				" playerLegalBusinessConsumerRuntimeOwner=" + OwnsPlayerLegalBusinessConsumerMutation());
		}

		private IEnumerator LogStartupEconomyDiagnosticsWhenReady(string source)
		{
			if (!EnableStartupEconomyDiagnostics.Value)
			{
				Logger.LogInfo("economy-audit skipped source=" + source + " reason=startup-diagnostics-disabled");
				yield break;
			}

			if (!EnableStartupEconomyAudit.Value
				&& !EnableShopAccessSampleLog.Value
				&& !EnableCivicPurchaseAccessSampleLog.Value
				&& !EnableDirtyCashRoutingSampleLog.Value
				&& !EnableFrontResourceAudit.Value)
			{
				yield break;
			}

			const int maxAttempts = 90;
			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				if (IsEconomyDiagnosticsReady())
				{
					string readySource = source + "-ready-attempt-" + attempt;
					if (EnableStartupEconomyAudit.Value)
					{
						EconomyStartupAudit.LogStartupAudit(readySource, Logger);
					}
					if (EnableShopAccessSampleLog.Value)
					{
						ShopAccessClassifier.LogStartupSamples(readySource, Logger, Math.Max(0, ShopAccessSampleLimit.Value));
					}
					if (EnableCivicPurchaseAccessSampleLog.Value)
					{
						CivicPurchaseAccessClassifier.LogStartupSamples(readySource, Logger, Math.Max(0, CivicPurchaseAccessSampleLimit.Value));
					}
					if (EnableDirtyCashRoutingSampleLog.Value)
					{
						DirtyCashRoutingClassifier.LogStartupSamples(readySource, Logger, Math.Max(0, DirtyCashRoutingSampleLimit.Value));
					}
					if (EnableFrontResourceAudit.Value)
					{
						FrontResourceAudit.LogStartupAudit(readySource, Logger, Math.Max(0, FrontResourceAuditSampleLimit.Value));
					}
					yield break;
				}

				if (ShouldLogDeferredEconomyDiagnosticsAttempt(attempt, maxAttempts))
				{
					Logger.LogInfo("economy-audit deferred source=" + source + " attempt=" + attempt + " reason=" + GetEconomyDiagnosticsNotReadyReason());
				}
				yield return new UnityEngine.WaitForSecondsRealtime(2f);
			}

			Logger.LogInfo("economy-audit skipped source=" + source + " reason=" + GetEconomyDiagnosticsNotReadyReason() + " attempts=" + maxAttempts);
		}

		private static bool ShouldLogDeferredEconomyDiagnosticsAttempt(int attempt, int maxAttempts)
		{
			return attempt == 1
				|| attempt == 5
				|| attempt == 15
				|| attempt == 30
				|| attempt == 60
				|| attempt == maxAttempts;
		}

		private static bool IsEconomyDiagnosticsReady()
		{
			try
			{
				return global::Game.Game.ctx?.entityman != null
					&& (global::Game.Game.ctx?.IsInteractive ?? false)
					&& IsProcgenReadyForEconomyDiagnostics()
					&& TryGetEconomyReadinessCounts(out int cachedBuildingCount, out int businessBuildingCount)
					&& cachedBuildingCount > 0
					&& businessBuildingCount > 0;
			}
			catch
			{
				return false;
			}
		}

		private static string GetEconomyDiagnosticsNotReadyReason()
		{
			try
			{
				if (global::Game.Game.ctx == null)
				{
					return "missing-game-context";
				}
				if (global::Game.Game.ctx.entityman == null)
				{
					return "missing-entity-manager";
				}
				if (!global::Game.Game.ctx.IsInteractive)
				{
					return "session-not-interactive";
				}
				if (!IsProcgenReadyForEconomyDiagnostics())
				{
					return "procgen-not-ready";
				}
				if (!TryGetEconomyReadinessCounts(out int cachedBuildingCount, out int businessBuildingCount) || cachedBuildingCount <= 0)
				{
					return "empty-building-cache";
				}
				if (businessBuildingCount <= 0)
				{
					return "empty-business-building-cache";
				}
			}
			catch (Exception ex)
			{
				return "readiness-error-" + ex.GetType().Name;
			}

			return "unknown";
		}

		private static bool IsProcgenReadyForEconomyDiagnostics()
		{
			try
			{
				return global::Game.Game.ctx.clock.CurrentTurn >= 1
					&& global::Game.Game.ctx.clock.Now >= global::Game.Game.ctx.clock.LastDayOfProcGen;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryGetEconomyReadinessCounts(out int cachedBuildingCount, out int businessBuildingCount)
		{
			cachedBuildingCount = 0;
			businessBuildingCount = 0;
			try
			{
				System.Collections.Generic.IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return false;
				}

				foreach (Entity building in buildings)
				{
					cachedBuildingCount++;
					try
					{
						if (building?.components?.building != null && building.components.building.IsBusinessBuildingType)
						{
							businessBuildingCount++;
						}
					}
					catch
					{
					}
				}

				return true;
			}
			catch
			{
				cachedBuildingCount = 0;
				businessBuildingCount = 0;
				return false;
			}
		}

		public static bool IsEconomyBridgeAvailable()
		{
			return Instance != null;
		}

		public static string GetEconomyBridgeVersion()
		{
			return PluginVersion;
		}

		public static bool OwnsPurchaseStockRefresh()
		{
			return Instance != null && (EnablePurchaseStockRefresh?.Value ?? false);
		}

		public static bool OwnsEmptyBusinessModuleRepair()
		{
			return Instance != null && (EnableEmptyBusinessModuleRepair?.Value ?? false);
		}

		public static bool OwnsShopAccessClassification()
		{
			return Instance != null && (EnableShopAccessBridge?.Value ?? false);
		}

		public static bool OwnsCivicPurchaseAccessClassification()
		{
			return Instance != null && (EnableCivicPurchaseAccessBridge?.Value ?? false);
		}

		public static bool OwnsDirtyCashRoutingClassification()
		{
			return Instance != null && (EnableDirtyCashRoutingBridge?.Value ?? false);
		}

		public static bool OwnsDirtyCashRuntimeSweepClassification()
		{
			return Instance != null && (EnableDirtyCashRuntimeSweepBridge?.Value ?? false);
		}

		public static bool OwnsDirtyCashRuntimeSweepMutation()
		{
			return Instance != null && (EnableDirtyCashRuntimeSweepOwner?.Value ?? false);
		}

		public static bool OwnsPlayerLegalBusinessConsumerClassification()
		{
			return Instance != null && (EnablePlayerLegalBusinessConsumerBridge?.Value ?? false);
		}

		public static bool OwnsPlayerLegalBusinessConsumerMutation()
		{
			return Instance != null && (EnablePlayerLegalBusinessConsumerRuntimeOwner?.Value ?? false);
		}

		public static bool OwnsFrontResourceClassification()
		{
			return Instance != null && (EnableFrontResourceBridge?.Value ?? false);
		}

		public static bool OwnsRouteShopOrderClassification()
		{
			return Instance != null && (EnableRouteShopOrderBridge?.Value ?? false);
		}

		public static string GetEconomyOwnershipSummary()
		{
			return "version=" + PluginVersion +
				" purchaseStockRefresh=" + OwnsPurchaseStockRefresh() +
				" emptyBusinessModuleRepair=" + OwnsEmptyBusinessModuleRepair() +
				" shopAccess=" + OwnsShopAccessClassification() +
				" civicPurchaseAccess=" + OwnsCivicPurchaseAccessClassification() +
				" dirtyCashRouting=" + OwnsDirtyCashRoutingClassification() +
				" dirtyCashRuntimeSweeps=" + OwnsDirtyCashRuntimeSweepClassification() +
				" dirtyCashRuntimeSweepMutation=" + OwnsDirtyCashRuntimeSweepMutation() +
				" playerLegalBusinessConsumer=" + OwnsPlayerLegalBusinessConsumerClassification() +
				" playerLegalBusinessConsumerMutation=" + OwnsPlayerLegalBusinessConsumerMutation() +
				" frontResource=" + OwnsFrontResourceClassification() +
				" routeShopOrders=" + OwnsRouteShopOrderClassification() +
				" postBusinessDiagnostics=" + (EnablePostBusinessUpdateDiagnostics?.Value ?? false);
		}

		public static bool ShouldExposeShopBuyAccess(EntityID buildingId)
		{
			return ShopAccessClassifier.Classify(buildingId).HasBuyAccess;
		}

		public static bool ShouldExposeShopSellAccess(EntityID buildingId)
		{
			return ShopAccessClassifier.Classify(buildingId).HasSellAccess;
		}

		public static string GetShopAccessSummary(EntityID buildingId)
		{
			return ShopAccessClassifier.Classify(buildingId).FormatBridgeSummary();
		}

		public static bool ShouldExposeBankPurchaseAccess(EntityID buildingId)
		{
			CivicPurchaseAccessSummary summary = CivicPurchaseAccessClassifier.Classify(buildingId);
			return summary.IsBank && summary.Access;
		}

		public static bool ShouldExposeWarehousePurchaseAccess(EntityID buildingId)
		{
			CivicPurchaseAccessSummary summary = CivicPurchaseAccessClassifier.Classify(buildingId);
			return summary.IsWarehouse && summary.Access;
		}

		public static string GetCivicPurchaseAccessSummary(EntityID buildingId)
		{
			return CivicPurchaseAccessClassifier.Classify(buildingId).FormatBridgeSummary();
		}

		public static bool ShouldUseExternalDirtyCashRouting(EntityID buildingId)
		{
			return DirtyCashRoutingClassifier.Classify(buildingId).UsesExternalDirtyCashRouting;
		}

		public static bool ShouldTreatAsDirtyCashRoutingCandidate(EntityID buildingId)
		{
			return DirtyCashRoutingClassifier.Classify(buildingId).IsRoutingCandidate;
		}

		public static string GetDirtyCashRoutingSummary(EntityID buildingId)
		{
			return DirtyCashRoutingClassifier.Classify(buildingId).FormatBridgeSummary();
		}

		public static string GetDirtyCashRuntimeSweepSummary(string source)
		{
			return DirtyCashRuntimeSweepClassifier.Classify(source).FormatBridgeSummary();
		}

		public static void BeginDirtyCashRuntimeSweepBusinessUpdate()
		{
			DirtyCashRuntimeSweepPatch.BeginBusinessUpdate();
		}

		public static void CompleteDirtyCashRuntimeSweepBusinessUpdate(bool initial)
		{
			DirtyCashRuntimeSweepPatch.CompleteBusinessUpdate(initial);
		}

		public static void EndDirtyCashRuntimeSweepBusinessUpdate()
		{
			DirtyCashRuntimeSweepPatch.EndBusinessUpdate();
		}

		public static string GetPlayerLegalBusinessConsumerSummary(EntityID buildingId, string moduleId)
		{
			return PlayerLegalBusinessConsumerClassifier.Classify(buildingId, moduleId).FormatBridgeSummary();
		}

		public static bool ShouldObservePlayerLegalBusinessConsumer(EntityID buildingId, string moduleId)
		{
			return PlayerLegalBusinessConsumerClassifier.Classify(buildingId, moduleId).ShouldObserve;
		}

		public static string GetPlayerLegalBusinessConsumerRuntimeSummary(
			EntityID buildingId,
			string moduleId,
			bool initial,
			bool enabled,
			string result,
			int consumeDays,
			int currentDay,
			int lastUpdateDay)
		{
			return PlayerLegalBusinessConsumerClassifier.Classify(
				buildingId,
				moduleId,
				initial,
				enabled,
				result,
				consumeDays,
				currentDay,
				lastUpdateDay).FormatBridgeSummary();
		}

		public static string GetFrontResourceSummary(EntityID buildingId)
		{
			return FrontResourceAudit.Classify(buildingId).FormatBridgeSummary();
		}

		public static bool IsRouteShopOrderValid(EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)
		{
			return RouteShopOrderClassifier.Classify(buildingId, resourceId, playerBuys, quantity, availableCash).Valid;
		}

		public static string GetRouteShopOrderSummary(EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)
		{
			return RouteShopOrderClassifier.Classify(buildingId, resourceId, playerBuys, quantity, availableCash).FormatBridgeSummary();
		}
	}
}

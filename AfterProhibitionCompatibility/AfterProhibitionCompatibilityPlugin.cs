using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace AfterProhibitionCompatibility
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class AfterProhibitionCompatibilityPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.compatibility";
		public const string PluginName = "After Prohibition Compatibility";
		public const string PluginVersion = "0.7.4";

		internal static ManualLogSource Log { get; private set; }

		internal static AfterProhibitionCompatibilityPlugin Instance { get; private set; }

		internal static ConfigEntry<bool> EnableCompatibilityBaselineLog { get; private set; }

		internal static ConfigEntry<bool> EnablePluginDllScan { get; private set; }

		internal static ConfigEntry<bool> EnableAsciiTokenProbe { get; private set; }

		internal static ConfigEntry<bool> EnableBroadUnpatchGuardLog { get; private set; }

		internal static ConfigEntry<bool> EnableBroadUnpatchProtectionClassification { get; private set; }

		internal static ConfigEntry<bool> EnableDirtyCashClassificationLog { get; private set; }

		internal static ConfigEntry<bool> EnableTenKInputClassificationLog { get; private set; }

		internal static ConfigEntry<bool> EnableElectionCheatClassificationLog { get; private set; }

		internal static ConfigEntry<bool> EnableTerritoryGangWarClassificationLog { get; private set; }

		public static CompatibilitySnapshot CurrentSnapshot { get; private set; } = CompatibilitySnapshot.Empty;

		private Harmony _harmony;
		private bool _loggedCompatibilityBaseline;

		private void Awake()
		{
			Instance = this;
			Log = Logger;

			BindConfig();

			Logger.LogInfo("compat baseline scheduled ownsGameplay=False");

			_harmony = new Harmony(PluginGuid);
			_harmony.PatchAll();

			Logger.LogInfo($"{PluginName} {PluginVersion} loaded. compatPatches=0 phase=all-dll-detection ownsGameplay=False");
		}

		private IEnumerator Start()
		{
			yield return null;
			yield return new UnityEngine.WaitForSecondsRealtime(1f);
			LogCompatibilityBaseline("start-1s");
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
			EnableCompatibilityBaselineLog = Config.Bind(
				"Features",
				"EnableCompatibilityBaselineLog",
				true,
				"Logs a concise read-only compatibility baseline once shortly after startup.");

			EnablePluginDllScan = Config.Bind(
				"Features",
				"EnablePluginDllScan",
				true,
				"Allows read-only plugin DLL filename checks for external compatibility hints.");

			EnableAsciiTokenProbe = Config.Bind(
				"Features",
				"EnableAsciiTokenProbe",
				true,
				"Allows targeted read-only ASCII token probes for launcher/safebox compatibility hints.");

			EnableBroadUnpatchGuardLog = Config.Bind(
				"Features",
				"EnableBroadUnpatchGuardLog",
				true,
				"Logs read-only broad-unpatch owner classifications once after the compatibility baseline.");

			EnableBroadUnpatchProtectionClassification = Config.Bind(
				"Features",
				"EnableBroadUnpatchProtectionClassification",
				true,
				"Enables reflection-safe broad-unpatch protection decisions for other After Prohibition plugins.");

			EnableDirtyCashClassificationLog = Config.Bind(
				"Features",
				"EnableDirtyCashClassificationLog",
				true,
				"Logs read-only Dirty Cash Economy compatibility classification after the compatibility baseline.");

			EnableTenKInputClassificationLog = Config.Bind(
				"Features",
				"EnableTenKInputClassificationLog",
				true,
				"Logs read-only 10K Button/Input compatibility classification after the compatibility baseline.");

			EnableElectionCheatClassificationLog = Config.Bind(
				"Features",
				"EnableElectionCheatClassificationLog",
				true,
				"Logs read-only election/core cheat compatibility classification after the compatibility baseline.");

			EnableTerritoryGangWarClassificationLog = Config.Bind(
				"Features",
				"EnableTerritoryGangWarClassificationLog",
				true,
				"Logs read-only territory/gang-war compatibility classification after the compatibility baseline.");
		}

		private void LogCompatibilityBaseline(string source)
		{
			if (_loggedCompatibilityBaseline || !EnableCompatibilityBaselineLog.Value)
			{
				return;
			}

			_loggedCompatibilityBaseline = true;

			try
			{
				CurrentSnapshot = CompatibilityDetector.Capture(
					EnablePluginDllScan.Value,
					EnableAsciiTokenProbe.Value,
					Logger);

				Logger.LogInfo(CurrentSnapshot.FormatPrimaryMatrix(source));
				Logger.LogInfo(CurrentSnapshot.FormatDllAudit(source));
				Logger.LogInfo(CurrentSnapshot.FormatSecondaryMatrix(source));

				if (EnableBroadUnpatchGuardLog.Value)
				{
					BroadUnpatchGuard.LogBaseline(CurrentSnapshot, Logger, source);
				}

				if (EnableDirtyCashClassificationLog.Value)
				{
					DirtyCashCompatibilityClassifier.LogBaseline(CurrentSnapshot, Logger, source);
				}

				if (EnableTenKInputClassificationLog.Value)
				{
					TenKInputCompatibilityClassifier.LogBaseline(CurrentSnapshot, Logger, source);
				}

				if (EnableElectionCheatClassificationLog.Value)
				{
					ElectionCheatCompatibilityClassifier.LogBaseline(CurrentSnapshot, Logger, source);
				}

				if (EnableTerritoryGangWarClassificationLog.Value)
				{
					TerritoryGangWarCompatibilityClassifier.LogBaseline(CurrentSnapshot, Logger, source);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("compat baseline failed source=" + source + " error=" + ex.Message);
			}
		}

		public static bool IsCompatibilityBridgeAvailable()
		{
			return Instance != null;
		}

		public static string GetCompatibilityBridgeVersion()
		{
			return PluginVersion;
		}

		public static bool IsDirtyCashEconomyActive()
		{
			return GetSnapshotForBridge("bridge-dirty-cash").DirtyCashEconomy;
		}

		public static bool IsDirtyCashVolumeFixActive()
		{
			return GetSnapshotForBridge("bridge-dirty-cash-volume").DirtyCashVolumeFix;
		}

		public static bool IsTenKInputActive()
		{
			return GetSnapshotForBridge("bridge-tenk-input").TenKButtonInput;
		}

		public static bool ShouldUseExternalSafeboxUi()
		{
			return GetSnapshotForBridge("bridge-safebox-ui").ExternalSafeboxUi;
		}

		public static bool HasExternalMenuOrSidebarOwner()
		{
			return GetSnapshotForBridge("bridge-menu-sidebar").ExternalMenuSidebar;
		}

		public static bool HasExternalUiEnhancer()
		{
			return GetSnapshotForBridge("bridge-ui-enhancer").PiaUiEnhancer;
		}

		public static bool HasCustomAssets()
		{
			return GetSnapshotForBridge("bridge-custom-assets").CoGCustomAssets;
		}

		public static bool HasCustomPortraits()
		{
			return GetSnapshotForBridge("bridge-custom-portraits").CustomPortraits;
		}

		public static bool HasExternalGangWarsOrTerritory()
		{
			CompatibilitySnapshot snapshot = GetSnapshotForBridge("bridge-gangwars-territory");
			return snapshot.GangWars || snapshot.TerritoryExpansion;
		}

		public static bool HasExternalGangWars()
		{
			return GetSnapshotForBridge("bridge-gangwars").GangWars;
		}

		public static bool HasExternalTerritoryExpansion()
		{
			return GetSnapshotForBridge("bridge-territory-expansion").TerritoryExpansion;
		}

		public static bool HasExternalMafiaHierarchy()
		{
			return GetSnapshotForBridge("bridge-mafia-hierarchy").MafiaHierarchy;
		}

		public static bool HasExternalTickerOwner()
		{
			return GetSnapshotForBridge("bridge-ticker").TickerEnhancer;
		}

		public static bool HasExternalCheatOrMenuOwner()
		{
			CompatibilitySnapshot snapshot = GetSnapshotForBridge("bridge-cheat-menu");
			return snapshot.CoreCheatMenu
				|| snapshot.ElectionCheat
				|| snapshot.BossManagerCheat
				|| snapshot.BlockedCheatPlugins
				|| snapshot.ExternalMenuSidebar;
		}

		public static bool HasExternalCoreCheatMenu()
		{
			return GetSnapshotForBridge("bridge-core-cheat-menu").CoreCheatMenu;
		}

		public static bool HasExternalElectionCheat()
		{
			return GetSnapshotForBridge("bridge-election-cheat").ElectionCheat;
		}

		public static bool HasExternalBossManagerCheat()
		{
			return GetSnapshotForBridge("bridge-boss-manager-cheat").BossManagerCheat;
		}

		public static string GetCompatibilityBridgeSummary()
		{
			return GetSnapshotForBridge("bridge-summary").FormatBridgeSummary();
		}

		public static string GetExternalDllAuditSummary()
		{
			return GetSnapshotForBridge("bridge-dll-audit").FormatDllAudit("bridge-dll-audit");
		}

		public static string GetDirtyCashCompatibilitySummary()
		{
			return DirtyCashCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-dirty-cash-summary")).FormatBridgeString();
		}

		public static string GetDirtyCashDllCheckSummary()
		{
			return DirtyCashCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-dirty-cash-dll-check")).DetectionSummary;
		}

		public static bool IsDirtyCashHarmonyPatchOwnerActive()
		{
			return DirtyCashCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-dirty-cash-harmony-owner")).DirtyCashHarmonyPatchesActive;
		}

		public static string GetDirtyCashPatchOwnerSummary()
		{
			return DirtyCashCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-dirty-cash-patches")).PatchOwnerSummary;
		}

		public static bool ShouldProtectDirtyCashRouteInput()
		{
			return DirtyCashCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-dirty-cash-route-input")).ProtectRouteInput;
		}

		public static bool ShouldProtectDirtyCashOriginalMethodOverrides()
		{
			return DirtyCashCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-dirty-cash-original-overrides")).ProtectOriginalMethodOverrides;
		}

		public static string GetTenKInputCompatibilitySummary()
		{
			return TenKInputCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-tenk-input-summary")).FormatBridgeString();
		}

		public static string GetTenKInputDetectionSummary()
		{
			return TenKInputCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-tenk-detection-summary")).DetectionSummary;
		}

		public static bool ShouldProtectTenKRouteInput()
		{
			return TenKInputCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-tenk-route-input")).ProtectRouteInput;
		}

		public static bool ShouldProtectExternalButtonInput()
		{
			return TenKInputCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-external-button-input")).ProtectButtonInput;
		}

		public static string GetTenKInputOwnerSummary()
		{
			return TenKInputCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-tenk-owner-summary")).OwnerSummary;
		}

		public static string GetElectionCheatCompatibilitySummary()
		{
			return ElectionCheatCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-election-cheat-summary")).FormatBridgeString();
		}

		public static string GetElectionCheatDetectionSummary()
		{
			return ElectionCheatCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-election-cheat-detection")).DetectionSummary;
		}

		public static bool ShouldProtectElectionStateFromExternalCheats()
		{
			return ElectionCheatCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-election-cheat-protect-election")).ProtectElectionState;
		}

		public static bool ShouldProtectBossStateFromExternalCheats()
		{
			return ElectionCheatCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-election-cheat-protect-boss")).ProtectBossState;
		}

		public static string GetTerritoryGangWarCompatibilitySummary()
		{
			return TerritoryGangWarCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-territory-gangwar-summary")).FormatBridgeString();
		}

		public static string GetTerritoryGangWarDetectionSummary()
		{
			return TerritoryGangWarCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-territory-gangwar-detection")).DetectionSummary;
		}

		public static bool ShouldProtectTerritoryVisualsFromExternalMods()
		{
			return TerritoryGangWarCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-territory-gangwar-protect-territory")).ProtectTerritoryVisuals;
		}

		public static bool ShouldProtectGangWarStateFromExternalMods()
		{
			return TerritoryGangWarCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-territory-gangwar-protect-gangwar")).ProtectGangWarState;
		}

		public static bool ShouldProtectTickerUiFromExternalMods()
		{
			return TerritoryGangWarCompatibilityClassifier.Classify(GetSnapshotForBridge("bridge-territory-gangwar-protect-ticker")).ProtectTickerUi;
		}

		public static bool ShouldProtectBroadUnpatchOwner(string owner)
		{
			if (!(EnableBroadUnpatchProtectionClassification?.Value ?? true))
			{
				return false;
			}

			return BroadUnpatchGuard.ShouldProtect(owner);
		}

		public static string GetBroadUnpatchOwnerAction(string owner)
		{
			if (!(EnableBroadUnpatchProtectionClassification?.Value ?? true))
			{
				return "ignore";
			}

			return BroadUnpatchGuard.GetAction(owner);
		}

		public static string GetBroadUnpatchOwnerReason(string owner)
		{
			if (!(EnableBroadUnpatchProtectionClassification?.Value ?? true))
			{
				return "classification-disabled";
			}

			return BroadUnpatchGuard.GetReason(owner);
		}

		public static string ClassifyBroadUnpatchOwner(string owner)
		{
			if (!(EnableBroadUnpatchProtectionClassification?.Value ?? true))
			{
				return BroadUnpatchGuard.Classify(owner).WithDisabledClassification().FormatBridgeString();
			}

			return BroadUnpatchGuard.Classify(owner).FormatBridgeString();
		}

		private static CompatibilitySnapshot GetSnapshotForBridge(string source)
		{
			if (CurrentSnapshot != null && !ReferenceEquals(CurrentSnapshot, CompatibilitySnapshot.Empty))
			{
				return CurrentSnapshot;
			}

			if (Instance == null)
			{
				return CompatibilitySnapshot.Empty;
			}

			return Instance.CaptureBridgeSnapshot(source);
		}

		private CompatibilitySnapshot CaptureBridgeSnapshot(string source)
		{
			try
			{
				bool scanDlls = EnablePluginDllScan?.Value ?? true;
				bool probeAscii = EnableAsciiTokenProbe?.Value ?? true;
				CurrentSnapshot = CompatibilityDetector.Capture(scanDlls, probeAscii, Logger);
				Logger.LogInfo("bridge snapshot captured source=" + source + " " + CurrentSnapshot.FormatBridgeSummary());
			}
			catch (Exception ex)
			{
				Logger.LogWarning("bridge snapshot failed source=" + source + " error=" + ex.Message);
				CurrentSnapshot = CompatibilitySnapshot.Empty;
			}

			return CurrentSnapshot ?? CompatibilitySnapshot.Empty;
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{

	private void CheckForConflictingMods()
	{
		string pluginsDirectory;
		try
		{
			pluginsDirectory = Path.GetDirectoryName(((BaseUnityPlugin)this).Info.Location);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Compatibility scan skipped: failed to resolve plugin directory. " + ex.Message);
			return;
		}

		ResetCompatibilityFlags();

		HashSet<string> dllFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> signalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		ScanPluginDirectorySignals(pluginsDirectory, dllFileNames, signalNames);
		ScanChainloaderSignals(signalNames);
		ComputeCompatibilityDecisions(pluginsDirectory, dllFileNames, signalNames);
		LogCompatibilityMatrix(pluginsDirectory, dllFileNames, signalNames);
	}

	private static void ResetCompatibilityFlags()
	{
		ExternalDirtyCashEconomyDetected = false;
		ExternalDirtyCashVolumeFixDetected = false;
		ExternalSafeboxDetected = false;
		ExternalModLauncherSafeboxDetected = false;
		ExternalModLauncherBridgeDetected = false;
		ExternalPiaModLauncherDetected = false;
		ExternalUIEnhancerDetected = false;
		ExternalCrewHireManagerDetected = false;
		ExternalRemoteInteractionDetected = false;
		ExternalGameOptimizerDetected = false;
		ExternalThirdPartyOptimizerDetected = false;
		ExternalMafiaHierarchyDetected = false;
		ExternalGangWarsDetected = false;
		ExternalTerritoryExpansionDetected = false;
		ExternalTickerEnhancerDetected = false;
		ExternalCheatMenuDetected = false;
		ExternalCoreCheatMenuDetected = false;
		ExternalElectionCheatDetected = false;
		ExternalBossManagerCheatDetected = false;
		ExternalCrewListSorterDetected = false;
		ExternalNaturalDeathModDetected = false;
		BlockedCheatPluginsDetected = false;
		ExternalCustomIconsDetected = false;
		GangWarsAdapterInitialized = false;
		ResetAfterProhibitionCompatibilityBridgeCache();
	}

	private static void ScanPluginDirectorySignals(string pluginsDirectory, HashSet<string> dllFileNames, HashSet<string> signalNames)
	{
		try
		{
			if (!Directory.Exists(pluginsDirectory))
			{
				return;
			}
			foreach (string dllPath in Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories))
			{
				dllFileNames.Add(Path.GetFileName(dllPath) ?? string.Empty);
				signalNames.Add(Path.GetFileNameWithoutExtension(dllPath) ?? string.Empty);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Compatibility directory scan failed: " + ex.Message);
		}
	}

	private static void ScanChainloaderSignals(HashSet<string> signalNames)
	{
		try
		{
			foreach (var pluginInfo in Chainloader.PluginInfos.Values)
			{
				if (pluginInfo?.Metadata?.GUID != null)
				{
					signalNames.Add(pluginInfo.Metadata.GUID);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Compatibility chainloader scan failed: " + ex.Message);
		}
	}

	private static void ComputeCompatibilityDecisions(string pluginsDirectory, HashSet<string> dllFileNames, HashSet<string> signalNames)
	{
		if (File.Exists(Path.Combine(pluginsDirectory, "DirtyCashEconomy.dll")))
		{
			ExternalDirtyCashEconomyDetected = true;
			Debug.Log("[GameplayTweaks] DirtyCashEconomy.dll detected - deferring DoChangeMoney patches to external mod");
		}
		if (File.Exists(Path.Combine(pluginsDirectory, "DirtyCashVolumeFix.dll")))
		{
			ExternalDirtyCashVolumeFixDetected = true;
			Debug.Log("[GameplayTweaks] DirtyCashVolumeFix.dll detected - deferring volume patches to external mod");
		}

		bool localDirtyCashEconomy = ExternalDirtyCashEconomyDetected;
		bool localDirtyCashVolume = ExternalDirtyCashVolumeFixDetected;
		if (TryGetAfterProhibitionCompatibilityDirtyCash(out bool bridgeDirtyCashEconomy, out bool bridgeDirtyCashVolume))
		{
			if (bridgeDirtyCashEconomy && !ExternalDirtyCashEconomyDetected)
			{
				Debug.Log("[GameplayTweaks] DirtyCashEconomy detected by AfterProhibitionCompatibility bridge - deferring DoChangeMoney patches to external mod");
			}

			if (bridgeDirtyCashVolume && !ExternalDirtyCashVolumeFixDetected)
			{
				Debug.Log("[GameplayTweaks] DirtyCashVolumeFix detected by AfterProhibitionCompatibility bridge - deferring volume patches to external mod");
			}

			ExternalDirtyCashEconomyDetected = bridgeDirtyCashEconomy || localDirtyCashEconomy;
			ExternalDirtyCashVolumeFixDetected = bridgeDirtyCashVolume || localDirtyCashVolume;
		}

		string safeboxDllPath = Path.Combine(pluginsDirectory, "Safebox.dll");
		if (File.Exists(safeboxDllPath))
		{
			ExternalSafeboxDetected = true;
			Debug.Log("[GameplayTweaks] Safebox.dll detected - built-in safebox nav/popup will be disabled to avoid conflict");
		}
		string modLauncherDllPath = Path.Combine(pluginsDirectory, "ModLauncher.dll");
		string bridgeLauncherDllPath = Path.Combine(pluginsDirectory, "ProhibitionLauncher.dll");
		string legacyBridgeLauncherDllPath = Path.Combine(pluginsDirectory, "TraplifeModLauncher.dll");
		string activeLauncherPath = File.Exists(modLauncherDllPath)
			? modLauncherDllPath
			: (File.Exists(bridgeLauncherDllPath) ? bridgeLauncherDllPath : (File.Exists(legacyBridgeLauncherDllPath) ? legacyBridgeLauncherDllPath : null));
		if (!string.IsNullOrEmpty(activeLauncherPath))
		{
			bool hasSafeboxToggle = DllContainsAsciiToken(activeLauncherPath, "ToggleSafeBoxMod")
				|| DllContainsAsciiToken(activeLauncherPath, "ToggleSafebox")
				|| DllContainsAsciiToken(activeLauncherPath, "Safebox");
			if (hasSafeboxToggle)
			{
				ExternalModLauncherSafeboxDetected = true;
				Debug.Log("[GameplayTweaks] ModLauncher safebox support detected");
			}
		}
		if (ShouldUseExternalSafeboxUI())
		{
			Debug.Log("[GameplayTweaks] External safebox UI active - crew management safebox button disabled to prevent duplicates.");
		}
		ExternalModLauncherBridgeDetected = signalNames.Contains("com.mods.modlauncher") || dllFileNames.Contains("ProhibitionLauncher.dll") || dllFileNames.Contains("TraplifeModLauncher.dll");
		ExternalPiaModLauncherDetected = signalNames.Contains("com.pia.modlauncher");
		ExternalUIEnhancerDetected = signalNames.Contains("com.pia.cityofgangsters.uienhancer") || signalNames.Contains("UIEnhancer") || dllFileNames.Contains("10K Button & Input.dll");
		bool localUiEnhancer = ExternalUIEnhancerDetected;
		if (TryGetAfterProhibitionCompatibilityUiInput(out bool bridgeTenKInput, out bool bridgeUiEnhancer))
		{
			if ((bridgeTenKInput || bridgeUiEnhancer) && !ExternalUIEnhancerDetected)
			{
				Debug.Log("[GameplayTweaks] UIEnhancer/10K input detected by AfterProhibitionCompatibility bridge - preserving external input/menu ownership.");
			}

			ExternalUIEnhancerDetected = bridgeTenKInput || bridgeUiEnhancer || localUiEnhancer;
		}

		ExternalCrewHireManagerDetected = signalNames.Contains("com.pia.crewhiremanager") || dllFileNames.Contains("CrewHireManager.dll");
		ExternalRemoteInteractionDetected = signalNames.Contains("com.pia.remoteinteraction") || dllFileNames.Contains("RemoteInteraction.dll");
		ExternalGameOptimizerDetected = signalNames.Contains("com.mods.gameoptimizer") || signalNames.Contains("GameOptimizer");
		ExternalThirdPartyOptimizerDetected = signalNames.Contains("com.modding.cityofgangsters.optimizer") || dllFileNames.Contains("CityOfGangstersOptimizer.dll");
		ExternalMafiaHierarchyDetected = signalNames.Contains("com.pia.mafiahierarchy") || signalNames.Contains("MafiaHierarchy");
		ExternalGangWarsDetected = signalNames.Contains("com.pia.gangwars") || dllFileNames.Contains("GangWars.dll");
		ExternalTerritoryExpansionDetected = signalNames.Contains("com.modder.territoryautoexpand") || dllFileNames.Contains("TerritoryExpansionPatch.dll");
		bool localMafiaHierarchy = ExternalMafiaHierarchyDetected;
		bool localGangWars = ExternalGangWarsDetected;
		bool localTerritoryExpansion = ExternalTerritoryExpansionDetected;
		if (TryGetAfterProhibitionCompatibilityTerritory(out bool bridgeGangWars, out bool bridgeTerritoryExpansion, out bool bridgeMafiaHierarchy))
		{
			if (bridgeGangWars && !ExternalGangWarsDetected)
			{
				Debug.Log("[GameplayTweaks] GangWars detected by AfterProhibitionCompatibility bridge - preserving external gang-war ownership.");
			}

			if (bridgeTerritoryExpansion && !ExternalTerritoryExpansionDetected)
			{
				Debug.Log("[GameplayTweaks] TerritoryExpansion detected by AfterProhibitionCompatibility bridge - preserving external territory ownership.");
			}

			if (bridgeMafiaHierarchy && !ExternalMafiaHierarchyDetected)
			{
				Debug.Log("[GameplayTweaks] MafiaHierarchy detected by AfterProhibitionCompatibility bridge - preserving external hierarchy/territory ownership.");
			}

			ExternalGangWarsDetected = bridgeGangWars || localGangWars;
			ExternalTerritoryExpansionDetected = bridgeTerritoryExpansion || localTerritoryExpansion;
			ExternalMafiaHierarchyDetected = bridgeMafiaHierarchy || localMafiaHierarchy;
		}

		ExternalTickerEnhancerDetected = signalNames.Contains("com.pia.tickerenhancer") || dllFileNames.Contains("Tickerenhancer.dll") || dllFileNames.Contains("OutpostTickerMod.dll");
		bool localTickerEnhancer = ExternalTickerEnhancerDetected;
		if (TryGetAfterProhibitionCompatibilityTicker(out bool bridgeTickerEnhancer))
		{
			if (bridgeTickerEnhancer && !ExternalTickerEnhancerDetected)
			{
				Debug.Log("[GameplayTweaks] Ticker enhancer detected by AfterProhibitionCompatibility bridge - preserving external ticker ownership.");
			}

			ExternalTickerEnhancerDetected = bridgeTickerEnhancer || localTickerEnhancer;
		}

		ExternalCoreCheatMenuDetected = signalNames.Contains("com.pia.cogcheat") || dllFileNames.Contains("Cog Ultimate Cheat.dll");
		ExternalElectionCheatDetected = signalNames.Contains("com.pia.electionmanager") || dllFileNames.Contains("ElectionVoteCheat.dll");
		ExternalBossManagerCheatDetected = signalNames.Contains("com.pia.bossmanager") || dllFileNames.Contains("Boss Manager.dll") || dllFileNames.Contains("BossManager.dll");
		ExternalCrewListSorterDetected = signalNames.Contains("com.pia.crewlistsorter") || dllFileNames.Contains("CrewListSorter.dll");
		ExternalNaturalDeathModDetected =
			ExternalCrewListSorterDetected
			|| dllFileNames.Contains("Crew Editor.dll")
			|| dllFileNames.Contains("CrewEditor.dll")
			|| signalNames.Contains("Crew Editor")
			|| signalNames.Contains("CrewEditor")
			|| signalNames.Contains("creweditor")
			|| signalNames.Contains("com.pia.creweditor");
		BlockedCheatPluginsDetected = HasBlockedCheatPluginsDetected();
		ExternalCheatMenuDetected = ExternalCoreCheatMenuDetected || BlockedCheatPluginsDetected;
		bool localCheatMenu = ExternalCheatMenuDetected;
		if (TryGetAfterProhibitionCompatibilityCheatMenu(out bool bridgeCheatMenu))
		{
			if (bridgeCheatMenu && !ExternalCheatMenuDetected)
			{
				Debug.Log("[GameplayTweaks] External cheat/menu owner detected by AfterProhibitionCompatibility bridge.");
			}

			ExternalCheatMenuDetected = bridgeCheatMenu || localCheatMenu;
		}

		ExternalCustomIconsDetected = dllFileNames.Contains("CoGCustomAssets.dll") || dllFileNames.Contains("CustomPortraits.dll") || dllFileNames.Contains("Traiticonlimitplugin.dll");
	}

	private static void LogCompatibilityMatrix(string pluginsDirectory, HashSet<string> dllFileNames, HashSet<string> signalNames)
	{
		VerificationLog("Compat", $"matrix begin pluginsDir=\"{pluginsDirectory}\" dllCount={dllFileNames.Count} loadedGuidCount={signalNames.Count}");
		bool compatibilityBridgeVersionAvailable = TryGetAfterProhibitionCompatibilityVersion(out string compatibilityBridgeVersion);
		VerificationLog("Compat", $"afterprohibitionCompatibility bridgeAvailable={compatibilityBridgeVersionAvailable} version={compatibilityBridgeVersion} source={(compatibilityBridgeVersionAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")}");
		VerificationLog("Compat", $"gangOpsMode=native-dual gangWarsAdapterMode={(ShouldEnableGangWarsPactAdapter() ? "fallback-enabled" : "fallback-disabled")}");
		VerificationLog("Compat", $"easy name=GameOptimizer detected={ExternalGameOptimizerDetected} note=load-speed focused, low overlap");
		VerificationLog("Compat", $"easy name=ThirdPartyOptimizer detected={ExternalThirdPartyOptimizerDetected} note=do-not-run-with-our-optimizer");
		VerificationLog("Compat", $"easy name=UIEnhancer10K detected={ExternalUIEnhancerDetected} note=UI scope, watch shared menu object names");
		bool compatibilityUiInputBridgeAvailable = TryGetAfterProhibitionCompatibilityUiInput(out bool compatibilityTenKInput, out bool compatibilityUiEnhancer);
		VerificationLog("Compat", $"uiInputExternal={ExternalUIEnhancerDetected} source={(compatibilityUiInputBridgeAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")} bridgeAvailable={compatibilityUiInputBridgeAvailable} bridgeTenK={compatibilityTenKInput} bridgeUiEnhancer={compatibilityUiEnhancer} localValue={(signalNames.Contains("com.pia.cityofgangsters.uienhancer") || signalNames.Contains("UIEnhancer") || dllFileNames.Contains("10K Button & Input.dll"))}");
		bool compatibilityMenuSidebarBridgeAvailable = TryGetAfterProhibitionCompatibilityMenuSidebar(out bool compatibilityMenuSidebar);
		VerificationLog("Compat", $"menuSidebarExternal={compatibilityMenuSidebar} source={(compatibilityMenuSidebarBridgeAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")} bridgeAvailable={compatibilityMenuSidebarBridgeAvailable} localValue={(ExternalUIEnhancerDetected || ExternalPiaModLauncherDetected)}");
		VerificationLog("Compat", $"easy name=CrewHireManager detected={ExternalCrewHireManagerDetected} note=hostile inspect/hire target overlap");
		VerificationLog("Compat", $"easy name=RemoteInteraction detected={ExternalRemoteInteractionDetected} note=convo target selection overlap");
		VerificationLog("Compat", $"easy name=TickerEnhancer detected={ExternalTickerEnhancerDetected} note=optional duplicate suppression");
		bool compatibilityTickerBridgeAvailable = TryGetAfterProhibitionCompatibilityTicker(out bool compatibilityTicker);
		VerificationLog("Compat", $"tickerExternal={ExternalTickerEnhancerDetected} source={(compatibilityTickerBridgeAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")} bridgeAvailable={compatibilityTickerBridgeAvailable} bridgeValue={compatibilityTicker} localValue={(signalNames.Contains("com.pia.tickerenhancer") || dllFileNames.Contains("Tickerenhancer.dll") || dllFileNames.Contains("OutpostTickerMod.dll"))}");
		VerificationLog("Compat", $"easy name=DirtyCashEconomy detected={ExternalDirtyCashEconomyDetected} note=deferral already active");
		VerificationLog("Compat", $"easy name=DirtyCashVolumeFix detected={ExternalDirtyCashVolumeFixDetected} note=volume fix deferral already active");
		VerificationLog("Compat", $"adapter name=GangWars detected={ExternalGangWarsDetected} note=retaliation-war overlap");
		VerificationLog("Compat", $"adapter name=TerritoryExpansion detected={ExternalTerritoryExpansionDetected} note=territory visual overlap");
		VerificationLog("Compat", $"adapter name=MafiaHierarchy detected={ExternalMafiaHierarchyDetected} note=war/territory/ui overlap");
		bool compatibilityTerritoryBridgeAvailable = TryGetAfterProhibitionCompatibilityTerritory(out bool compatibilityGangWars, out bool compatibilityTerritoryExpansion, out bool compatibilityMafiaHierarchy);
		VerificationLog("Compat", $"territoryExternal={(ExternalGangWarsDetected || ExternalTerritoryExpansionDetected || ExternalMafiaHierarchyDetected)} source={(compatibilityTerritoryBridgeAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")} bridgeAvailable={compatibilityTerritoryBridgeAvailable} bridgeGangWars={compatibilityGangWars} bridgeTerritoryExpansion={compatibilityTerritoryExpansion} bridgeMafiaHierarchy={compatibilityMafiaHierarchy} localGangWars={(signalNames.Contains("com.pia.gangwars") || dllFileNames.Contains("GangWars.dll"))} localTerritoryExpansion={(signalNames.Contains("com.modder.territoryautoexpand") || dllFileNames.Contains("TerritoryExpansionPatch.dll"))} localMafiaHierarchy={(signalNames.Contains("com.pia.mafiahierarchy") || signalNames.Contains("MafiaHierarchy"))}");
		VerificationLog("Compat", $"adapter name=CrewListSorter detected={ExternalCrewListSorterDetected} note=life-death/crew-list overlap");
		VerificationLog("Compat", $"adapter name=NaturalDeathExternal detected={ExternalNaturalDeathModDetected} note=natural-death overlap");
		VerificationLog("Compat", $"adapter name=CustomIcons detected={ExternalCustomIconsDetected} note=portrait/asset stack");
		VerificationLog("Compat", $"cheat core={ExternalCoreCheatMenuDetected} election={ExternalElectionCheatDetected} bossManager={ExternalBossManagerCheatDetected}");
		VerificationLog("Compat", $"cheat authority={(ShouldKeepGameplayTweaksCheatAuthority() ? "gameplaytweaks" : "external")} blockedElectionManager={ShouldBlockElectionManagerCheats()}");
		VerificationLog("Compat", $"gangwars tribute={(ShouldAllowGangWarsTributeSystems() ? "enabled" : "blocked")} replaceAlliances={(ShouldReplaceGangWarsAlliancesWithPacts() ? "pacts" : "external")} colorSource={(ShouldUseGangWarsColorStyleForPacts() ? "pacts-via-gangwars-style" : "external")} vassals={(ShouldDisableGangWarsVassals() ? "blocked" : "external")}");
		VerificationLog("Compat", $"territory gangExpand={(ShouldAllowGangTerritoryExpansion() ? "enabled" : "blocked")} playerAutoExpand={(ShouldAllowPlayerAutoExpandTerritory() ? "enabled" : "blocked")} outpostAutoExpand={(ShouldAllowOutpostAutoExpand() ? "enabled" : "blocked")} extTerritoryDetected={ExternalTerritoryExpansionDetected}");
		VerificationLog("Compat", $"hiring businessAssignedAllowed={ShouldAllowBusinessAssignedCandidates()}");
		VerificationLog("Compat", $"highrisk name=CheatMenu detected={ExternalCheatMenuDetected} blockedCheatPlugins={BlockedCheatPluginsDetected} note=core-cheat-allowed election/manager-blocked");
		bool compatibilityCheatMenuBridgeAvailable = TryGetAfterProhibitionCompatibilityCheatMenu(out bool compatibilityCheatMenu);
		VerificationLog("Compat", $"cheatMenuExternal={ExternalCheatMenuDetected} source={(compatibilityCheatMenuBridgeAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")} bridgeAvailable={compatibilityCheatMenuBridgeAvailable} bridgeValue={compatibilityCheatMenu} localValue={(ExternalCoreCheatMenuDetected || BlockedCheatPluginsDetected)}");
		if (ShouldBlockElectionManagerCheats())
		{
			List<string> blocked = new List<string>();
			if (ExternalElectionCheatDetected)
			{
				blocked.Add("ElectionVoteCheat.dll/com.pia.electionmanager");
			}
			if (ExternalBossManagerCheatDetected)
			{
				blocked.Add("Boss Manager.dll/com.pia.bossmanager");
			}
			string blockedText = (blocked.Count > 0) ? string.Join(", ", blocked.ToArray()) : "none";
			Debug.LogWarning("[GameplayTweaks] Compatibility: blocked cheat plugins detected. Remove these for stable profile: " + blockedText);
			VerificationLog("Compat", $"blockedCheatPlugins=true detected={blockedText}");
		}
		if (ExternalGameOptimizerDetected && ExternalThirdPartyOptimizerDetected)
		{
			Debug.LogWarning("[GameplayTweaks] Compatibility: both GameOptimizer variants detected. Keep only one optimizer active.");
			VerificationLog("Compat", "blocked-combo optimizers=dual detected=true");
		}
		VerificationLog("Compat", $"launcher bridge={ExternalModLauncherBridgeDetected} pia={ExternalPiaModLauncherDetected} safeboxSignal={ExternalModLauncherSafeboxDetected} bridgeDll=ProhibitionLauncher.dll");
		bool compatibilitySafeboxBridgeAvailable = TryGetAfterProhibitionCompatibilitySafeboxUi(out bool compatibilitySafeboxExternal);
		bool localSafeboxExternal = ExternalSafeboxDetected || ExternalModLauncherSafeboxDetected;
		VerificationLog("Compat", $"safeboxExternal={ShouldUseExternalSafeboxUI()} source={(compatibilitySafeboxBridgeAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")} bridgeAvailable={compatibilitySafeboxBridgeAvailable} bridgeValue={compatibilitySafeboxExternal} localValue={localSafeboxExternal}");
		bool compatibilityDirtyCashBridgeAvailable = TryGetAfterProhibitionCompatibilityDirtyCash(out bool compatibilityDirtyCashExternal, out bool compatibilityDirtyCashVolume);
		VerificationLog("Compat", $"dirtyCashExternal={(ExternalDirtyCashEconomyDetected || ExternalDirtyCashVolumeFixDetected)} source={(compatibilityDirtyCashBridgeAvailable ? "afterprohibition-compatibility-bridge" : "gameplaytweaks-local")} bridgeAvailable={compatibilityDirtyCashBridgeAvailable} bridgeEconomy={compatibilityDirtyCashExternal} bridgeVolume={compatibilityDirtyCashVolume} localEconomy={dllFileNames.Contains("DirtyCashEconomy.dll")} localVolume={dllFileNames.Contains("DirtyCashVolumeFix.dll")}");
		bool uiRethemeBridgeAvailable = IsAfterProhibitionUiRethemeBridgeAvailableForCompat();
		bool crewHudRefreshBridgeAvailable = IsAfterProhibitionUiCrewHudRefreshBridgeAvailableForCompat();
		bool aggroUiRefreshBridgeAvailable = IsAfterProhibitionUiAggroRefreshBridgeAvailableForCompat();
		VerificationLog("Compat", $"activeSubsystems dirtyCashDeferral={(ExternalDirtyCashEconomyDetected || ExternalDirtyCashVolumeFixDetected)} safeboxExternal={ShouldUseExternalSafeboxUI()} retaliationReconcile={(ShouldDeferRetaliationWarReconciliation() ? "deferred" : "active")} territoryVisual={(ShouldDeferTerritoryVisualOverrides() ? "deferred" : "active")} uiRetheme={(uiRethemeBridgeAvailable ? "delegated" : "active")} crewHudRefresh={(crewHudRefreshBridgeAvailable ? "delegated" : "active")} tickerDupes={(ShouldSuppressTickerDupes() ? "suppressed" : "normal")} gangWarsAdapter={(ShouldEnableGangWarsPactAdapter() ? "active" : "inactive")} vassals={(ShouldDisableGangWarsVassals() ? "disabled" : "external")} tribute={(ShouldAllowGangWarsTributeSystems() ? "enabled" : "blocked")} pactAllianceReplace={(ShouldReplaceGangWarsAlliancesWithPacts() ? "on" : "off")} pactColorPrecedence={(ShouldPactColorWinOverGangWarsVisuals() ? "active" : "external")} gangWarsColorStyleForPacts={(ShouldUseGangWarsColorStyleForPacts() ? "on" : "off")} gangExpand={(ShouldAllowGangTerritoryExpansion() ? "on" : "off")} playerAutoExpand={(ShouldAllowPlayerAutoExpandTerritory() ? "on" : "off")} outpostAutoExpand={(ShouldAllowOutpostAutoExpand() ? "on" : "off")} pactAggroBoost={(ShouldUseGangWarsAggroBoostForPacts() ? "active" : "off")} cheatCoreAdapter={((CompatEnableCoreCheatMenuAdapter != null && CompatEnableCoreCheatMenuAdapter.Value && ExternalCoreCheatMenuDetected) ? "active" : "inactive")} blockedCheatPlugins={ShouldBlockElectionManagerCheats()} businessHiringAllowed={ShouldAllowBusinessAssignedCandidates()} naturalDeathAuthority={(ShouldDeferNaturalCauseDeathsToExternalMod() ? "external" : "gameplaytweaks")} aggroUiRefresh={(aggroUiRefreshBridgeAvailable ? "delegated" : "active")}");
		VerificationLog("Compat", $"gangOpsDefaults pact(enabled={(PactOpsDefaultsEnabled?.Value ?? true)} autoProtect={(PactOpsDefaultsAutoProtectEnabled?.Value ?? true)} coordAuto={(PactOpsDefaultsCoordinatedAttackAutoEnabled?.Value ?? false)} revenge={(PactOpsDefaultsRevengeEnabled?.Value ?? true)} hireAuto={(PactOpsDefaultsHireAutomationEnabled?.Value ?? true)}) independent(enabled={(GangOpsDefaultsIndependentEnabled?.Value ?? true)} autoProtect={(GangOpsDefaultsIndependentAutoProtectEnabled?.Value ?? true)} coordAuto={(GangOpsDefaultsIndependentCoordinatedAttackAutoEnabled?.Value ?? false)} revenge={(GangOpsDefaultsIndependentRevengeEnabled?.Value ?? true)} hireAuto={(GangOpsDefaultsIndependentHireAutomationEnabled?.Value ?? true)})");
		VerificationLog("Compat", $"uiRetheme={(uiRethemeBridgeAvailable ? "delegated" : "active")} source={(uiRethemeBridgeAvailable ? "afterprohibition-ui-bridge" : "gameplaytweaks-owned-roots")} bridgeAvailable={uiRethemeBridgeAvailable} externalEnhancer={ExternalUIEnhancerDetected}");
		VerificationLog("Compat", $"crewHudRefresh={(crewHudRefreshBridgeAvailable ? "delegated" : "active")} source={(crewHudRefreshBridgeAvailable ? "afterprohibition-ui-bridge" : "gameplaytweaks-direct")} bridgeAvailable={crewHudRefreshBridgeAvailable}");
		VerificationLog("Compat", $"aggroUiRefresh={(aggroUiRefreshBridgeAvailable ? "delegated" : "active")} source={(aggroUiRefreshBridgeAvailable ? "afterprohibition-ui-bridge" : "gameplaytweaks-direct")} bridgeAvailable={aggroUiRefreshBridgeAvailable}");
		VerificationLog("Compat", "matrix end");
	}

	private static bool IsAfterProhibitionUiRethemeBridgeAvailableForCompat()
	{
		if (!IsAfterProhibitionUiInstalled())
		{
			return false;
		}

		try
		{
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type bridgeType = assembly.GetType("AfterProhibitionUI.UiRethemeBridge", throwOnError: false);
				if (bridgeType == null)
				{
					continue;
				}

				return bridgeType.GetMethod(
					"TryRethemeMenuHierarchy",
					System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
					null,
					new[] { typeof(GameObject), typeof(bool) },
					null) != null;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionUiCrewHudRefreshBridgeAvailableForCompat()
	{
		if (!IsAfterProhibitionUiInstalled())
		{
			return false;
		}

		try
		{
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type bridgeType = assembly.GetType("AfterProhibitionUI.CrewHudRefreshBridge", throwOnError: false);
				if (bridgeType == null)
				{
					continue;
				}

				return bridgeType.GetMethod(
					"RequestCrewHudRefresh",
					System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
					null,
					new[] { typeof(string), typeof(bool) },
					null) != null;
			}
		}
		catch
		{
		}

		return false;
	}

	internal static bool IsAfterProhibitionUiAggroRefreshBridgeAvailableForCompat()
	{
		if (!IsAfterProhibitionUiInstalled())
		{
			return false;
		}

		try
		{
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type bridgeType = assembly.GetType("AfterProhibitionUI.CrewPickAggroRefreshBridge", throwOnError: false);
				if (bridgeType == null)
				{
					continue;
				}

				return bridgeType.GetMethod(
					"RequestAggroRefresh",
					System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
					null,
					new[] { typeof(int), typeof(string) },
					null) != null
					&& bridgeType.GetMethod(
						"FlushAggroRefreshes",
						System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
						null,
						new[] { typeof(string) },
						null) != null;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool _afterProhibitionCompatibilitySafeboxBridgeQueried;
	private static bool _afterProhibitionCompatibilitySafeboxBridgeAvailable;
	private static bool _afterProhibitionCompatibilitySafeboxUi;
	private static bool _afterProhibitionCompatibilityPluginTypeResolved;
	private static Type _afterProhibitionCompatibilityPluginType;
	private static bool _afterProhibitionCompatibilityVersionQueried;
	private static bool _afterProhibitionCompatibilityVersionAvailable;
	private static string _afterProhibitionCompatibilityVersion = "unavailable";

	private static void ResetAfterProhibitionCompatibilityBridgeCache()
	{
		_afterProhibitionCompatibilityPluginTypeResolved = false;
		_afterProhibitionCompatibilityPluginType = null;
		_afterProhibitionCompatibilityVersionQueried = false;
		_afterProhibitionCompatibilityVersionAvailable = false;
		_afterProhibitionCompatibilityVersion = "unavailable";
		_afterProhibitionCompatibilitySafeboxBridgeQueried = false;
		_afterProhibitionCompatibilitySafeboxBridgeAvailable = false;
		_afterProhibitionCompatibilitySafeboxUi = false;
		_afterProhibitionCompatibilityDirtyCashBridgeQueried = false;
		_afterProhibitionCompatibilityDirtyCashBridgeAvailable = false;
		_afterProhibitionCompatibilityDirtyCashEconomy = false;
		_afterProhibitionCompatibilityDirtyCashVolume = false;
		_afterProhibitionCompatibilityUiInputBridgeQueried = false;
		_afterProhibitionCompatibilityUiInputBridgeAvailable = false;
		_afterProhibitionCompatibilityTenKInput = false;
		_afterProhibitionCompatibilityUiEnhancer = false;
		_afterProhibitionCompatibilityMenuSidebarBridgeQueried = false;
		_afterProhibitionCompatibilityMenuSidebarBridgeAvailable = false;
		_afterProhibitionCompatibilityMenuSidebar = false;
		_afterProhibitionCompatibilityTickerBridgeQueried = false;
		_afterProhibitionCompatibilityTickerBridgeAvailable = false;
		_afterProhibitionCompatibilityTicker = false;
		_afterProhibitionCompatibilityCheatMenuBridgeQueried = false;
		_afterProhibitionCompatibilityCheatMenuBridgeAvailable = false;
		_afterProhibitionCompatibilityCheatMenu = false;
		_afterProhibitionCompatibilityTerritoryBridgeQueried = false;
		_afterProhibitionCompatibilityTerritoryBridgeAvailable = false;
		_afterProhibitionCompatibilityGangWars = false;
		_afterProhibitionCompatibilityTerritoryExpansion = false;
		_afterProhibitionCompatibilityMafiaHierarchy = false;
	}

	private static bool TryGetAfterProhibitionCompatibilitySafeboxUi(out bool shouldUseExternalSafeboxUi)
	{
		if (_afterProhibitionCompatibilitySafeboxBridgeQueried)
		{
			shouldUseExternalSafeboxUi = _afterProhibitionCompatibilitySafeboxUi;
			return _afterProhibitionCompatibilitySafeboxBridgeAvailable;
		}

		_afterProhibitionCompatibilitySafeboxBridgeQueried = true;
		_afterProhibitionCompatibilitySafeboxBridgeAvailable = false;
		_afterProhibitionCompatibilitySafeboxUi = false;

		try
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionCompatibility.AfterProhibitionCompatibilityPlugin", throwOnError: false);
				if (pluginType == null)
				{
					continue;
				}

				MethodInfo method = pluginType.GetMethod(
					"ShouldUseExternalSafeboxUi",
					BindingFlags.Static | BindingFlags.Public,
					null,
					Type.EmptyTypes,
					null);
				if (method == null)
				{
					continue;
				}

				object result = method.Invoke(null, null);
				if (result is bool value)
				{
					_afterProhibitionCompatibilitySafeboxBridgeAvailable = true;
					_afterProhibitionCompatibilitySafeboxUi = value;
					shouldUseExternalSafeboxUi = value;
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			VerificationLog("Compat", $"afterprohibition-compatibility safebox bridge failed error={ex.GetType().Name}:{ex.Message}");
		}

		shouldUseExternalSafeboxUi = false;
		return false;
	}

	private static bool _afterProhibitionCompatibilityDirtyCashBridgeQueried;
	private static bool _afterProhibitionCompatibilityDirtyCashBridgeAvailable;
	private static bool _afterProhibitionCompatibilityDirtyCashEconomy;
	private static bool _afterProhibitionCompatibilityDirtyCashVolume;
	private static bool _afterProhibitionCompatibilityUiInputBridgeQueried;
	private static bool _afterProhibitionCompatibilityUiInputBridgeAvailable;
	private static bool _afterProhibitionCompatibilityTenKInput;
	private static bool _afterProhibitionCompatibilityUiEnhancer;
	private static bool _afterProhibitionCompatibilityMenuSidebarBridgeQueried;
	private static bool _afterProhibitionCompatibilityMenuSidebarBridgeAvailable;
	private static bool _afterProhibitionCompatibilityMenuSidebar;
	private static bool _afterProhibitionCompatibilityTickerBridgeQueried;
	private static bool _afterProhibitionCompatibilityTickerBridgeAvailable;
	private static bool _afterProhibitionCompatibilityTicker;
	private static bool _afterProhibitionCompatibilityCheatMenuBridgeQueried;
	private static bool _afterProhibitionCompatibilityCheatMenuBridgeAvailable;
	private static bool _afterProhibitionCompatibilityCheatMenu;
	private static bool _afterProhibitionCompatibilityTerritoryBridgeQueried;
	private static bool _afterProhibitionCompatibilityTerritoryBridgeAvailable;
	private static bool _afterProhibitionCompatibilityGangWars;
	private static bool _afterProhibitionCompatibilityTerritoryExpansion;
	private static bool _afterProhibitionCompatibilityMafiaHierarchy;

	private static bool TryGetAfterProhibitionCompatibilityDirtyCash(out bool dirtyCashEconomy, out bool dirtyCashVolume)
	{
		if (_afterProhibitionCompatibilityDirtyCashBridgeQueried)
		{
			dirtyCashEconomy = _afterProhibitionCompatibilityDirtyCashEconomy;
			dirtyCashVolume = _afterProhibitionCompatibilityDirtyCashVolume;
			return _afterProhibitionCompatibilityDirtyCashBridgeAvailable;
		}

		_afterProhibitionCompatibilityDirtyCashBridgeQueried = true;
		_afterProhibitionCompatibilityDirtyCashBridgeAvailable = false;
		_afterProhibitionCompatibilityDirtyCashEconomy = false;
		_afterProhibitionCompatibilityDirtyCashVolume = false;

		bool bridgeAvailable = false;
		bool economyValue = false;
		bool volumeValue = false;

		if (TryInvokeAfterProhibitionCompatibilityBoolMethod("IsDirtyCashEconomyActive", out bool economyResult))
		{
			bridgeAvailable = true;
			economyValue = economyResult;
		}

		if (TryInvokeAfterProhibitionCompatibilityBoolMethod("IsDirtyCashVolumeFixActive", out bool volumeResult))
		{
			bridgeAvailable = true;
			volumeValue = volumeResult;
		}

		_afterProhibitionCompatibilityDirtyCashBridgeAvailable = bridgeAvailable;
		_afterProhibitionCompatibilityDirtyCashEconomy = economyValue;
		_afterProhibitionCompatibilityDirtyCashVolume = volumeValue;
		dirtyCashEconomy = economyValue;
		dirtyCashVolume = volumeValue;
		return bridgeAvailable;
	}

	private static bool TryGetAfterProhibitionCompatibilityUiInput(out bool tenKInput, out bool uiEnhancer)
	{
		if (_afterProhibitionCompatibilityUiInputBridgeQueried)
		{
			tenKInput = _afterProhibitionCompatibilityTenKInput;
			uiEnhancer = _afterProhibitionCompatibilityUiEnhancer;
			return _afterProhibitionCompatibilityUiInputBridgeAvailable;
		}

		_afterProhibitionCompatibilityUiInputBridgeQueried = true;
		_afterProhibitionCompatibilityUiInputBridgeAvailable = false;
		_afterProhibitionCompatibilityTenKInput = false;
		_afterProhibitionCompatibilityUiEnhancer = false;

		bool bridgeAvailable = false;
		bool tenKValue = false;
		bool uiEnhancerValue = false;

		if (TryInvokeAfterProhibitionCompatibilityBoolMethod("IsTenKInputActive", out bool tenKResult))
		{
			bridgeAvailable = true;
			tenKValue = tenKResult;
		}

		if (TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalUiEnhancer", out bool uiEnhancerResult))
		{
			bridgeAvailable = true;
			uiEnhancerValue = uiEnhancerResult;
		}

		_afterProhibitionCompatibilityUiInputBridgeAvailable = bridgeAvailable;
		_afterProhibitionCompatibilityTenKInput = tenKValue;
		_afterProhibitionCompatibilityUiEnhancer = uiEnhancerValue;
		tenKInput = tenKValue;
		uiEnhancer = uiEnhancerValue;
		return bridgeAvailable;
	}

	private static bool TryGetAfterProhibitionCompatibilityMenuSidebar(out bool menuSidebar)
	{
		if (_afterProhibitionCompatibilityMenuSidebarBridgeQueried)
		{
			menuSidebar = _afterProhibitionCompatibilityMenuSidebar;
			return _afterProhibitionCompatibilityMenuSidebarBridgeAvailable;
		}

		_afterProhibitionCompatibilityMenuSidebarBridgeQueried = true;
		_afterProhibitionCompatibilityMenuSidebarBridgeAvailable = TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalMenuOrSidebarOwner", out bool value);
		_afterProhibitionCompatibilityMenuSidebar = value;
		menuSidebar = value;
		return _afterProhibitionCompatibilityMenuSidebarBridgeAvailable;
	}

	private static bool TryGetAfterProhibitionCompatibilityTicker(out bool ticker)
	{
		if (_afterProhibitionCompatibilityTickerBridgeQueried)
		{
			ticker = _afterProhibitionCompatibilityTicker;
			return _afterProhibitionCompatibilityTickerBridgeAvailable;
		}

		_afterProhibitionCompatibilityTickerBridgeQueried = true;
		_afterProhibitionCompatibilityTickerBridgeAvailable = TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalTickerOwner", out bool value);
		_afterProhibitionCompatibilityTicker = value;
		ticker = value;
		return _afterProhibitionCompatibilityTickerBridgeAvailable;
	}

	private static bool TryGetAfterProhibitionCompatibilityCheatMenu(out bool cheatMenu)
	{
		if (_afterProhibitionCompatibilityCheatMenuBridgeQueried)
		{
			cheatMenu = _afterProhibitionCompatibilityCheatMenu;
			return _afterProhibitionCompatibilityCheatMenuBridgeAvailable;
		}

		_afterProhibitionCompatibilityCheatMenuBridgeQueried = true;
		_afterProhibitionCompatibilityCheatMenuBridgeAvailable = TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalCheatOrMenuOwner", out bool value);
		_afterProhibitionCompatibilityCheatMenu = value;
		cheatMenu = value;
		return _afterProhibitionCompatibilityCheatMenuBridgeAvailable;
	}

	private static bool TryGetAfterProhibitionCompatibilityTerritory(out bool gangWars, out bool territoryExpansion, out bool mafiaHierarchy)
	{
		if (_afterProhibitionCompatibilityTerritoryBridgeQueried)
		{
			gangWars = _afterProhibitionCompatibilityGangWars;
			territoryExpansion = _afterProhibitionCompatibilityTerritoryExpansion;
			mafiaHierarchy = _afterProhibitionCompatibilityMafiaHierarchy;
			return _afterProhibitionCompatibilityTerritoryBridgeAvailable;
		}

		_afterProhibitionCompatibilityTerritoryBridgeQueried = true;
		_afterProhibitionCompatibilityTerritoryBridgeAvailable = false;
		_afterProhibitionCompatibilityGangWars = false;
		_afterProhibitionCompatibilityTerritoryExpansion = false;
		_afterProhibitionCompatibilityMafiaHierarchy = false;

		bool bridgeAvailable = false;
		bool gangWarsValue = false;
		bool territoryExpansionValue = false;
		bool mafiaHierarchyValue = false;

		if (TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalGangWars", out bool gangWarsResult))
		{
			bridgeAvailable = true;
			gangWarsValue = gangWarsResult;
		}

		if (TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalTerritoryExpansion", out bool territoryExpansionResult))
		{
			bridgeAvailable = true;
			territoryExpansionValue = territoryExpansionResult;
		}

		if (TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalMafiaHierarchy", out bool mafiaHierarchyResult))
		{
			bridgeAvailable = true;
			mafiaHierarchyValue = mafiaHierarchyResult;
		}

		if (!bridgeAvailable && TryInvokeAfterProhibitionCompatibilityBoolMethod("HasExternalGangWarsOrTerritory", out bool combinedResult))
		{
			bridgeAvailable = true;
			gangWarsValue = combinedResult;
			territoryExpansionValue = combinedResult;
		}

		_afterProhibitionCompatibilityTerritoryBridgeAvailable = bridgeAvailable;
		_afterProhibitionCompatibilityGangWars = gangWarsValue;
		_afterProhibitionCompatibilityTerritoryExpansion = territoryExpansionValue;
		_afterProhibitionCompatibilityMafiaHierarchy = mafiaHierarchyValue;
		gangWars = gangWarsValue;
		territoryExpansion = territoryExpansionValue;
		mafiaHierarchy = mafiaHierarchyValue;
		return bridgeAvailable;
	}

	private static bool TryInvokeAfterProhibitionCompatibilityBoolMethod(string methodName, out bool value)
	{
		value = false;
		try
		{
			if (!TryGetAfterProhibitionCompatibilityPluginType(out Type pluginType))
			{
				return false;
			}

			MethodInfo method = pluginType.GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.Public,
				null,
				Type.EmptyTypes,
				null);
			if (method == null)
			{
				return false;
			}

			object result = method.Invoke(null, null);
			if (result is bool boolValue)
			{
				value = boolValue;
				return true;
			}
		}
		catch (Exception ex)
		{
			VerificationLog("Compat", $"afterprohibition-compatibility bridge method={methodName} failed error={ex.GetType().Name}:{ex.Message}");
		}

		return false;
	}

	private static bool TryGetAfterProhibitionCompatibilityVersion(out string version)
	{
		if (_afterProhibitionCompatibilityVersionQueried)
		{
			version = _afterProhibitionCompatibilityVersion;
			return _afterProhibitionCompatibilityVersionAvailable;
		}

		_afterProhibitionCompatibilityVersionQueried = true;
		_afterProhibitionCompatibilityVersionAvailable = false;
		_afterProhibitionCompatibilityVersion = "unavailable";

		try
		{
			if (!TryGetAfterProhibitionCompatibilityPluginType(out Type pluginType))
			{
				version = _afterProhibitionCompatibilityVersion;
				return false;
			}

			MethodInfo method = pluginType.GetMethod(
				"GetCompatibilityBridgeVersion",
				BindingFlags.Static | BindingFlags.Public,
				null,
				Type.EmptyTypes,
				null);
			if (method == null)
			{
				version = _afterProhibitionCompatibilityVersion;
				return false;
			}

			object result = method.Invoke(null, null);
			if (result is string text && !string.IsNullOrWhiteSpace(text))
			{
				_afterProhibitionCompatibilityVersion = text;
				_afterProhibitionCompatibilityVersionAvailable = true;
				version = text;
				return true;
			}
		}
		catch (Exception ex)
		{
			VerificationLog("Compat", $"afterprohibition-compatibility bridge version failed error={ex.GetType().Name}:{ex.Message}");
		}

		version = _afterProhibitionCompatibilityVersion;
		return false;
	}

	private static bool TryGetAfterProhibitionCompatibilityPluginType(out Type pluginType)
	{
		if (_afterProhibitionCompatibilityPluginTypeResolved)
		{
			pluginType = _afterProhibitionCompatibilityPluginType;
			return pluginType != null;
		}

		_afterProhibitionCompatibilityPluginTypeResolved = true;
		_afterProhibitionCompatibilityPluginType = null;

		try
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type candidate = assembly.GetType("AfterProhibitionCompatibility.AfterProhibitionCompatibilityPlugin", throwOnError: false);
				if (candidate != null)
				{
					_afterProhibitionCompatibilityPluginType = candidate;
					pluginType = candidate;
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			VerificationLog("Compat", $"afterprohibition-compatibility bridge type lookup failed error={ex.GetType().Name}:{ex.Message}");
		}

		pluginType = null;
		return false;
	}

	private static bool DllContainsAsciiToken(string dllPath, string token)
	{
		if (string.IsNullOrEmpty(dllPath) || string.IsNullOrEmpty(token))
		{
			return false;
		}
		try
		{
			byte[] data = File.ReadAllBytes(dllPath);
			string text = Encoding.ASCII.GetString(data);
			return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
		}
		catch
		{
			// Probe path: binary layout/read failures are expected for some DLLs and should not spam logs.
			return false;
		}
	}
}
}

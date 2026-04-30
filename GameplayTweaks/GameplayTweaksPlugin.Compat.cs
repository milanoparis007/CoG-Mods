using System;
using System.Collections.Generic;
using System.IO;
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
	}

	private static void ScanPluginDirectorySignals(string pluginsDirectory, HashSet<string> dllFileNames, HashSet<string> signalNames)
	{
		try
		{
			if (!Directory.Exists(pluginsDirectory))
			{
				return;
			}
			foreach (string dllPath in Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
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
		ExternalCrewHireManagerDetected = signalNames.Contains("com.pia.crewhiremanager") || dllFileNames.Contains("CrewHireManager.dll");
		ExternalRemoteInteractionDetected = signalNames.Contains("com.pia.remoteinteraction") || dllFileNames.Contains("RemoteInteraction.dll");
		ExternalGameOptimizerDetected = signalNames.Contains("com.mods.gameoptimizer") || signalNames.Contains("GameOptimizer");
		ExternalThirdPartyOptimizerDetected = signalNames.Contains("com.modding.cityofgangsters.optimizer") || dllFileNames.Contains("CityOfGangstersOptimizer.dll");
		ExternalMafiaHierarchyDetected = signalNames.Contains("com.pia.mafiahierarchy") || signalNames.Contains("MafiaHierarchy");
		ExternalGangWarsDetected = signalNames.Contains("com.pia.gangwars") || dllFileNames.Contains("GangWars.dll");
		ExternalTerritoryExpansionDetected = signalNames.Contains("com.modder.territoryautoexpand") || dllFileNames.Contains("TerritoryExpansionPatch.dll");
		ExternalTickerEnhancerDetected = signalNames.Contains("com.pia.tickerenhancer") || dllFileNames.Contains("Tickerenhancer.dll") || dllFileNames.Contains("OutpostTickerMod.dll");
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
		ExternalCustomIconsDetected = dllFileNames.Contains("CoGCustomAssets.dll") || dllFileNames.Contains("CustomPortraits.dll") || dllFileNames.Contains("Traiticonlimitplugin.dll");
	}

	private static void LogCompatibilityMatrix(string pluginsDirectory, HashSet<string> dllFileNames, HashSet<string> signalNames)
	{
		VerificationLog("Compat", $"matrix begin pluginsDir=\"{pluginsDirectory}\" dllCount={dllFileNames.Count} loadedGuidCount={signalNames.Count}");
		VerificationLog("Compat", $"gangOpsMode=native-dual gangWarsAdapterMode={(ShouldEnableGangWarsPactAdapter() ? "fallback-enabled" : "fallback-disabled")}");
		VerificationLog("Compat", $"easy name=GameOptimizer detected={ExternalGameOptimizerDetected} note=load-speed focused, low overlap");
		VerificationLog("Compat", $"easy name=ThirdPartyOptimizer detected={ExternalThirdPartyOptimizerDetected} note=do-not-run-with-our-optimizer");
		VerificationLog("Compat", $"easy name=UIEnhancer10K detected={ExternalUIEnhancerDetected} note=UI scope, watch shared menu object names");
		VerificationLog("Compat", $"easy name=CrewHireManager detected={ExternalCrewHireManagerDetected} note=hostile inspect/hire target overlap");
		VerificationLog("Compat", $"easy name=RemoteInteraction detected={ExternalRemoteInteractionDetected} note=convo target selection overlap");
		VerificationLog("Compat", $"easy name=TickerEnhancer detected={ExternalTickerEnhancerDetected} note=optional duplicate suppression");
		VerificationLog("Compat", $"easy name=DirtyCashEconomy detected={ExternalDirtyCashEconomyDetected} note=deferral already active");
		VerificationLog("Compat", $"easy name=DirtyCashVolumeFix detected={ExternalDirtyCashVolumeFixDetected} note=volume fix deferral already active");
		VerificationLog("Compat", $"adapter name=GangWars detected={ExternalGangWarsDetected} note=retaliation-war overlap");
		VerificationLog("Compat", $"adapter name=TerritoryExpansion detected={ExternalTerritoryExpansionDetected} note=territory visual overlap");
		VerificationLog("Compat", $"adapter name=MafiaHierarchy detected={ExternalMafiaHierarchyDetected} note=war/territory/ui overlap");
		VerificationLog("Compat", $"adapter name=CrewListSorter detected={ExternalCrewListSorterDetected} note=life-death/crew-list overlap");
		VerificationLog("Compat", $"adapter name=NaturalDeathExternal detected={ExternalNaturalDeathModDetected} note=natural-death overlap");
		VerificationLog("Compat", $"adapter name=CustomIcons detected={ExternalCustomIconsDetected} note=portrait/asset stack");
		VerificationLog("Compat", $"cheat core={ExternalCoreCheatMenuDetected} election={ExternalElectionCheatDetected} bossManager={ExternalBossManagerCheatDetected}");
		VerificationLog("Compat", $"cheat authority={(ShouldKeepGameplayTweaksCheatAuthority() ? "gameplaytweaks" : "external")} blockedElectionManager={ShouldBlockElectionManagerCheats()}");
		VerificationLog("Compat", $"gangwars tribute={(ShouldAllowGangWarsTributeSystems() ? "enabled" : "blocked")} replaceAlliances={(ShouldReplaceGangWarsAlliancesWithPacts() ? "pacts" : "external")} colorSource={(ShouldUseGangWarsColorStyleForPacts() ? "pacts-via-gangwars-style" : "external")} vassals={(ShouldDisableGangWarsVassals() ? "blocked" : "external")}");
		VerificationLog("Compat", $"territory gangExpand={(ShouldAllowGangTerritoryExpansion() ? "enabled" : "blocked")} playerAutoExpand={(ShouldAllowPlayerAutoExpandTerritory() ? "enabled" : "blocked")} outpostAutoExpand={(ShouldAllowOutpostAutoExpand() ? "enabled" : "blocked")} extTerritoryDetected={ExternalTerritoryExpansionDetected}");
		VerificationLog("Compat", $"hiring businessAssignedAllowed={ShouldAllowBusinessAssignedCandidates()}");
		VerificationLog("Compat", $"highrisk name=CheatMenu detected={ExternalCheatMenuDetected} blockedCheatPlugins={BlockedCheatPluginsDetected} note=core-cheat-allowed election/manager-blocked");
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
		VerificationLog("Compat", $"activeSubsystems dirtyCashDeferral={(ExternalDirtyCashEconomyDetected || ExternalDirtyCashVolumeFixDetected)} safeboxExternal={ShouldUseExternalSafeboxUI()} retaliationReconcile={(ShouldDeferRetaliationWarReconciliation() ? "deferred" : "active")} territoryVisual={(ShouldDeferTerritoryVisualOverrides() ? "deferred" : "active")} uiRetheme=active tickerDupes={(ShouldSuppressTickerDupes() ? "suppressed" : "normal")} gangWarsAdapter={(ShouldEnableGangWarsPactAdapter() ? "active" : "inactive")} vassals={(ShouldDisableGangWarsVassals() ? "disabled" : "external")} tribute={(ShouldAllowGangWarsTributeSystems() ? "enabled" : "blocked")} pactAllianceReplace={(ShouldReplaceGangWarsAlliancesWithPacts() ? "on" : "off")} pactColorPrecedence={(ShouldPactColorWinOverGangWarsVisuals() ? "active" : "external")} gangWarsColorStyleForPacts={(ShouldUseGangWarsColorStyleForPacts() ? "on" : "off")} gangExpand={(ShouldAllowGangTerritoryExpansion() ? "on" : "off")} playerAutoExpand={(ShouldAllowPlayerAutoExpandTerritory() ? "on" : "off")} outpostAutoExpand={(ShouldAllowOutpostAutoExpand() ? "on" : "off")} pactAggroBoost={(ShouldUseGangWarsAggroBoostForPacts() ? "active" : "off")} cheatCoreAdapter={((CompatEnableCoreCheatMenuAdapter != null && CompatEnableCoreCheatMenuAdapter.Value && ExternalCoreCheatMenuDetected) ? "active" : "inactive")} blockedCheatPlugins={ShouldBlockElectionManagerCheats()} businessHiringAllowed={ShouldAllowBusinessAssignedCandidates()} naturalDeathAuthority={(ShouldDeferNaturalCauseDeathsToExternalMod() ? "external" : "gameplaytweaks")} aggroUiRefresh=true");
		VerificationLog("Compat", $"gangOpsDefaults pact(enabled={(PactOpsDefaultsEnabled?.Value ?? true)} autoProtect={(PactOpsDefaultsAutoProtectEnabled?.Value ?? true)} coordAuto={(PactOpsDefaultsCoordinatedAttackAutoEnabled?.Value ?? false)} revenge={(PactOpsDefaultsRevengeEnabled?.Value ?? true)} hireAuto={(PactOpsDefaultsHireAutomationEnabled?.Value ?? true)}) independent(enabled={(GangOpsDefaultsIndependentEnabled?.Value ?? true)} autoProtect={(GangOpsDefaultsIndependentAutoProtectEnabled?.Value ?? true)} coordAuto={(GangOpsDefaultsIndependentCoordinatedAttackAutoEnabled?.Value ?? false)} revenge={(GangOpsDefaultsIndependentRevengeEnabled?.Value ?? true)} hireAuto={(GangOpsDefaultsIndependentHireAutomationEnabled?.Value ?? true)})");
		VerificationLog("Compat", $"uiRetheme=active source=gameplaytweaks-owned-roots externalEnhancer={ExternalUIEnhancerDetected}");
		VerificationLog("Compat", "matrix end");
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

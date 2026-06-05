namespace AfterProhibitionCompatibility
{
	public sealed class CompatibilitySnapshot
	{
		public static readonly CompatibilitySnapshot Empty = new CompatibilitySnapshot();

		public int LoadedPluginCount { get; internal set; }
		public int LoadedAssemblyCount { get; internal set; }
		public int PluginDllCount { get; internal set; }
		public int KnownExternalDllCount { get; internal set; }
		public int UnknownExternalDllCount { get; internal set; }
		public int LocalSuiteDllCount { get; internal set; }
		public bool PluginDllScanEnabled { get; internal set; }
		public bool AsciiTokenProbeEnabled { get; internal set; }
		public int AsciiTokenProbeCount { get; internal set; }
		public int AsciiTokenHitCount { get; internal set; }
		public string PluginDllNames { get; internal set; } = "none";
		public string KnownExternalDllNames { get; internal set; } = "none";
		public string UnknownExternalDllNames { get; internal set; } = "none";
		public string LocalSuiteDllNames { get; internal set; } = "none";

		public bool DirtyCashEconomy { get; internal set; }
		public bool DirtyCashVolumeFix { get; internal set; }
		public bool TenKButtonInputDll { get; internal set; }
		public bool TenKButtonInputToken { get; internal set; }
		public bool TenKButtonInput { get; internal set; }
		public bool UiEnhancerGuidOrDll { get; internal set; }
		public bool PiaUiEnhancer { get; internal set; }
		public bool MenuSidebarToken { get; internal set; }
		public bool SafeboxDll { get; internal set; }
		public bool SafeboxLauncherSignal { get; internal set; }
		public bool ExternalSafeboxUi { get; internal set; }
		public bool ModLauncherBridge { get; internal set; }
		public bool PiaModLauncher { get; internal set; }
		public bool Launcher { get; internal set; }
		public bool CrewHireManager { get; internal set; }
		public bool RemoteInteraction { get; internal set; }
		public bool GameOptimizer { get; internal set; }
		public bool ThirdPartyOptimizer { get; internal set; }
		public bool DualOptimizer { get; internal set; }
		public bool MafiaHierarchy { get; internal set; }
		public bool GangWars { get; internal set; }
		public bool TerritoryExpansion { get; internal set; }
		public bool TickerEnhancer { get; internal set; }
		public bool CoreCheatGuidOrDll { get; internal set; }
		public bool CoreCheatMenu { get; internal set; }
		public bool ElectionCheatGuidOrDll { get; internal set; }
		public bool ElectionCheat { get; internal set; }
		public bool BossManagerGuidOrDll { get; internal set; }
		public bool BossManagerCheat { get; internal set; }
		public bool CheatMenuToken { get; internal set; }
		public bool BlockedCheatPlugins { get; internal set; }
		public bool CrewListSorter { get; internal set; }
		public bool CrewEditor { get; internal set; }
		public bool NaturalDeathExternal { get; internal set; }
		public bool CoGCustomAssets { get; internal set; }
		public bool CustomPortraits { get; internal set; }
		public bool TraitIconLimit { get; internal set; }
		public bool CustomIcons { get; internal set; }
		public bool ExternalMenuSidebar { get; internal set; }

		public string FormatPrimaryMatrix(string source)
		{
			return "external source=" + source
				+ " phase=detection-matrix ownsGameplay=False"
				+ " loadedPlugins=" + LoadedPluginCount
				+ " loadedAssemblies=" + LoadedAssemblyCount
				+ " pluginDlls=" + PluginDllCount
				+ " dllScan=" + PluginDllScanEnabled
				+ " asciiProbe=" + AsciiTokenProbeEnabled
				+ " asciiProbeCount=" + AsciiTokenProbeCount
				+ " asciiHits=" + AsciiTokenHitCount
				+ " dirtyCash=" + DirtyCashEconomy
				+ " dirtyCashVolume=" + DirtyCashVolumeFix
				+ " tenKInput=" + TenKButtonInput
				+ " safeboxUi=" + ExternalSafeboxUi
				+ " safeboxDll=" + SafeboxDll
				+ " safeboxLauncher=" + SafeboxLauncherSignal
				+ " uiEnhancer=" + PiaUiEnhancer
				+ " menuSidebar=" + ExternalMenuSidebar;
		}

		public string FormatDllAudit(string source)
		{
			return "external-dll-audit source=" + source
				+ " phase=all-dll-detection ownsGameplay=False"
				+ " dllScan=" + PluginDllScanEnabled
				+ " pluginDlls=" + PluginDllCount
				+ " knownExternalDlls=" + KnownExternalDllCount
				+ " unknownExternalDlls=" + UnknownExternalDllCount
				+ " localSuiteDlls=" + LocalSuiteDllCount
				+ " knownExternal=" + KnownExternalDllNames
				+ " unknownExternal=" + UnknownExternalDllNames
				+ " localSuite=" + LocalSuiteDllNames
				+ " allDlls=" + PluginDllNames;
		}

		public string FormatSecondaryMatrix(string source)
		{
			return "external-detail source=" + source
				+ " phase=detection-matrix"
				+ " launcher=" + Launcher
				+ " modLauncherBridge=" + ModLauncherBridge
				+ " piaLauncher=" + PiaModLauncher
				+ " cogCustomAssets=" + CoGCustomAssets
				+ " customPortraits=" + CustomPortraits
				+ " traitIconLimit=" + TraitIconLimit
				+ " customIcons=" + CustomIcons
				+ " gangWars=" + GangWars
				+ " territoryExpansion=" + TerritoryExpansion
				+ " mafiaHierarchy=" + MafiaHierarchy
				+ " ticker=" + TickerEnhancer
				+ " gameOptimizer=" + GameOptimizer
				+ " thirdPartyOptimizer=" + ThirdPartyOptimizer
				+ " dualOptimizer=" + DualOptimizer
				+ " coreCheat=" + CoreCheatMenu
				+ " electionCheat=" + ElectionCheat
				+ " bossManagerCheat=" + BossManagerCheat
				+ " blockedCheats=" + BlockedCheatPlugins
				+ " crewHire=" + CrewHireManager
				+ " remoteInteraction=" + RemoteInteraction
				+ " crewListSorter=" + CrewListSorter
				+ " crewEditor=" + CrewEditor
				+ " naturalDeathExternal=" + NaturalDeathExternal;
		}

		public string FormatBridgeSummary()
		{
			return "bridge phase=reflection-query ownsGameplay=False"
				+ " dirtyCash=" + DirtyCashEconomy
				+ " dirtyCashVolume=" + DirtyCashVolumeFix
				+ " tenKInput=" + TenKButtonInput
				+ " safeboxUi=" + ExternalSafeboxUi
				+ " uiEnhancer=" + PiaUiEnhancer
				+ " menuSidebar=" + ExternalMenuSidebar
				+ " cogCustomAssets=" + CoGCustomAssets
				+ " customPortraits=" + CustomPortraits
				+ " gangWars=" + GangWars
				+ " territoryExpansion=" + TerritoryExpansion
				+ " mafiaHierarchy=" + MafiaHierarchy
				+ " ticker=" + TickerEnhancer
				+ " cheatMenu=" + (CoreCheatMenu || ElectionCheat || BossManagerCheat || BlockedCheatPlugins)
				+ " knownExternalDlls=" + KnownExternalDllCount
				+ " unknownExternalDlls=" + UnknownExternalDllCount
				+ " localSuiteDlls=" + LocalSuiteDllCount;
		}
	}
}

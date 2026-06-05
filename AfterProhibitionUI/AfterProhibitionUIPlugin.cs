using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace AfterProhibitionUI
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class AfterProhibitionUIPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.ui";
		public const string PluginName = "After Prohibition UI";
		public const string PluginVersion = "0.2.8";

		internal static ManualLogSource Log { get; private set; }

		internal static AfterProhibitionUIPlugin Instance { get; private set; }

		internal static ConfigEntry<bool> EnableTextSanitizer { get; private set; }

		internal static ConfigEntry<bool> EnableCrewSidebarJailBars { get; private set; }

		internal static ConfigEntry<bool> EnablePopupDocking { get; private set; }

		internal static ConfigEntry<bool> EnableMenuRethemeBridge { get; private set; }

		internal static ConfigEntry<bool> EnableCrewInfoButtons { get; private set; }

		internal static ConfigEntry<bool> EnableStalePortraitGuards { get; private set; }

		internal static ConfigEntry<bool> EnableCrewManagementJailVisuals { get; private set; }

		internal static ConfigEntry<bool> EnableCrewHudRefreshBridge { get; private set; }

		internal static ConfigEntry<bool> EnableAggroUiRefreshBridge { get; private set; }

		private Harmony _harmony;

		private void Awake()
		{
			Instance = this;
			Log = Logger;

			BindConfig();
			Logger.LogInfo("compat baseline scheduled ownsGameplay=False");

			_harmony = new Harmony(PluginGuid);
			_harmony.PatchAll();
			int uiPatches = 0;
			if (EnableTextSanitizer.Value)
			{
				uiPatches += TmpReplacementCharacterSanitizerPatch.ApplyPatch(_harmony);
			}
			else
			{
				Logger.LogInfo("UISanitize disabled by config");
			}
			if (EnableCrewSidebarJailBars.Value)
			{
				uiPatches += CrewSidebarJailBarsPatch.ApplyPatch(_harmony);
			}
			else
			{
				Logger.LogInfo("Jail crew sidebar jail-bars disabled by config");
			}
			if (EnablePopupDocking.Value)
			{
				uiPatches += PopupDockingPatch.ApplyPatch(_harmony);
			}
			else
			{
				Logger.LogInfo("PopupDocking disabled by config");
			}
			if (EnableCrewInfoButtons.Value)
			{
				uiPatches += CrewInfoButtonsPatch.ApplyPatch(_harmony);
			}
			else
			{
				Logger.LogInfo("CrewInfoButtons disabled by config");
			}
			if (EnableStalePortraitGuards.Value)
			{
				uiPatches += StalePortraitAndConnectionGuardPatch.ApplyPatch(_harmony);
			}
			else
			{
				Logger.LogInfo("StalePortraitGuards disabled by config");
			}
			if (EnableCrewManagementJailVisuals.Value)
			{
				uiPatches += CrewManagementJailVisualPatch.ApplyPatch(_harmony);
			}
			else
			{
				Logger.LogInfo("CrewManagementJailVisuals disabled by config");
			}

			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. uiPatches=" + uiPatches + " phase=menu-sidebar-visuals");
			Logger.LogInfo("CrewInfo action bridge registered reflectionOnly=True");
		}

		private void BindConfig()
		{
			EnableTextSanitizer = Config.Bind(
				"Features",
				"EnableTextSanitizer",
				true,
				"Remove replacement-character glyphs from TextMeshPro text setters.");
			EnableCrewSidebarJailBars = Config.Bind(
				"Features",
				"EnableCrewSidebarJailBars",
				true,
				"Show existing jail-bars visuals on crew sidebar cards for crew in custody.");
			EnablePopupDocking = Config.Bind(
				"Features",
				"EnablePopupDocking",
				true,
				"Keep modal BasePopup panels inside their parent viewport. HUD side panels are excluded.");
			EnableMenuRethemeBridge = Config.Bind(
				"Features",
				"EnableMenuRethemeBridge",
				true,
				"Own generic menu/popup retheme requests delegated by GameplayTweaks. Pact colors and gameplay-coupled UI remain in GameplayTweaks.");
			EnableCrewInfoButtons = Config.Bind(
				"Features",
				"EnableCrewInfoButtons",
				true,
				"Own crew inspect footer button rendering. Actions are delegated to GameplayTweaks through a reflection bridge.");
			EnableStalePortraitGuards = Config.Bind(
				"Features",
				"EnableStalePortraitGuards",
				true,
				"Clear stale portrait UI state and suppress invalid connection cards without mutating relationship or family data.");
			EnableCrewManagementJailVisuals = Config.Bind(
				"Features",
				"EnableCrewManagementJailVisuals",
				true,
				"Own crew-management jail status text on crew cards and crew info descriptions. Assignment blocking remains in GameplayTweaks.");
			EnableCrewHudRefreshBridge = Config.Bind(
				"Features",
				"EnableCrewHudRefreshBridge",
				true,
				"Own coalesced crew HUD/sidebar refresh and rebuild requests delegated by GameplayTweaks. Gameplay state remains in GameplayTweaks.");
			EnableAggroUiRefreshBridge = Config.Bind(
				"Features",
				"EnableAggroUiRefreshBridge",
				true,
				"Own crew-pick aggro dirty/flush scheduling delegated by GameplayTweaks. Vehicle and custody reconciliation remains in GameplayTweaks.");
		}

		private IEnumerator Start()
		{
			yield return null;
			yield return new UnityEngine.WaitForSecondsRealtime(1f);
			LogCompatibilityHeader("start-1s");
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

		internal static void StartDeferredWork(IEnumerator routine)
		{
			Instance?.StartCoroutine(routine);
		}

		public static bool IsTextSanitizerEnabledForExternalOwnerCheck()
		{
			return EnableTextSanitizer?.Value ?? false;
		}

		public static bool IsCrewSidebarJailBarsEnabledForExternalOwnerCheck()
		{
			return EnableCrewSidebarJailBars?.Value ?? false;
		}

		public static bool IsCrewInfoButtonsEnabledForExternalOwnerCheck()
		{
			return EnableCrewInfoButtons?.Value ?? false;
		}

		public static bool IsStalePortraitGuardsEnabledForExternalOwnerCheck()
		{
			return EnableStalePortraitGuards?.Value ?? false;
		}

		public static bool IsCrewManagementJailVisualsEnabledForExternalOwnerCheck()
		{
			return EnableCrewManagementJailVisuals?.Value ?? false;
		}

		public static bool IsCrewHudRefreshBridgeEnabledForExternalOwnerCheck()
		{
			return EnableCrewHudRefreshBridge?.Value ?? false;
		}

		public static bool IsAggroUiRefreshBridgeEnabledForExternalOwnerCheck()
		{
			return EnableAggroUiRefreshBridge?.Value ?? false;
		}

		private static void LogCompatibilityHeader(string source)
		{
			HashSet<string> pluginDlls = GetPluginDllFileNames();
			bool externalEnhancer =
				IsAssemblyLoaded("CityOfGangstersUIEnhancer")
				|| IsPluginGuidLoaded("com.pia.cityofgangsters.uienhancer")
				|| HasPluginDll(pluginDlls, "10K Button & Input.dll");
			bool cogCustomAssets =
				IsAssemblyLoaded("CoGCustomAssets")
				|| IsPluginGuidLoaded("com.cogmod.customassets")
				|| HasPluginDll(pluginDlls, "CoGCustomAssets.dll");
			bool customPortraits =
				IsAssemblyLoaded("CustomPortraits")
				|| IsPluginGuidLoaded("com.yourname.customportraits")
				|| HasPluginDll(pluginDlls, "CustomPortraits.dll");
			bool safeboxUi =
				HasPluginDll(pluginDlls, "Safebox.dll")
				|| IsPluginGuidLoaded("com.mods.modlauncher")
				|| IsPluginGuidLoaded("com.pia.modlauncher")
				|| DllContainsAsciiToken("ModLauncher.dll", "Safebox")
				|| DllContainsAsciiToken("ProhibitionLauncher.dll", "Safebox")
				|| DllContainsAsciiToken("TraplifeModLauncher.dll", "Safebox")
				|| DllContainsAsciiToken("ModLauncher.dll", "ToggleSafeBoxMod")
				|| DllContainsAsciiToken("ProhibitionLauncher.dll", "ToggleSafeBoxMod")
				|| DllContainsAsciiToken("TraplifeModLauncher.dll", "ToggleSafeBoxMod");
			bool menuSidebarExternal =
				externalEnhancer
				|| cogCustomAssets
				|| IsPluginGuidLoaded("com.pia.cogcheat")
				|| IsPluginGuidLoaded("com.pia.mafiahierarchy")
				|| IsPluginGuidLoaded("com.mods.orgchart")
				|| HasPluginDll(pluginDlls, "Cog Ultimate Cheat.dll")
				|| HasPluginDll(pluginDlls, "MafiaHierarchy.dll")
				|| HasPluginDll(pluginDlls, "OrgChartMod.dll");

			Log?.LogInfo(
				"compat source=" + source
				+ " externalEnhancer=" + externalEnhancer
				+ " cogCustomAssets=" + cogCustomAssets
				+ " customPortraits=" + customPortraits
				+ " safeboxUi=" + safeboxUi
				+ " menuSidebarExternal=" + menuSidebarExternal
				+ " rethemeOwner=" + ((EnableMenuRethemeBridge?.Value ?? false) ? "AfterProhibitionUIBridge" : "GameplayTweaks")
				+ " textSanitizer=" + (EnableTextSanitizer?.Value ?? false)
				+ " sidebarJailBars=" + (EnableCrewSidebarJailBars?.Value ?? false)
				+ " popupDocking=" + (EnablePopupDocking?.Value ?? false)
				+ " menuRethemeBridge=" + (EnableMenuRethemeBridge?.Value ?? false)
				+ " crewInfoActionBridge=" + CrewInfoActionBridge.IsAvailable()
				+ " crewInfoButtons=" + (EnableCrewInfoButtons?.Value ?? false)
				+ " stalePortraitGuards=" + (EnableStalePortraitGuards?.Value ?? false)
				+ " crewManagementJailVisuals=" + (EnableCrewManagementJailVisuals?.Value ?? false)
				+ " crewHudRefreshBridge=" + (EnableCrewHudRefreshBridge?.Value ?? false)
				+ " aggroUiRefreshBridge=" + (EnableAggroUiRefreshBridge?.Value ?? false)
				+ " loadedGuidCount=" + BepInEx.Bootstrap.Chainloader.PluginInfos.Count
				+ " pluginDllCount=" + pluginDlls.Count
				+ " ownsGameplay=False");
		}

		private static bool IsAssemblyLoaded(string assemblyName)
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Any(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
		}

		private static bool IsPluginGuidLoaded(string pluginGuid)
		{
			return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(pluginGuid);
		}

		private static HashSet<string> GetPluginDllFileNames()
		{
			HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (Directory.Exists(Paths.PluginPath))
				{
					foreach (string path in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories))
					{
						names.Add(Path.GetFileName(path));
					}
				}
			}
			catch (Exception ex)
			{
				Log?.LogWarning("compat plugin directory scan failed: " + ex.Message);
			}
			return names;
		}

		private static bool HasPluginDll(HashSet<string> pluginDlls, string fileName)
		{
			return pluginDlls != null && pluginDlls.Contains(fileName);
		}

		private static bool DllContainsAsciiToken(string fileName, string token)
		{
			if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(token))
			{
				return false;
			}

			try
			{
				string path = FindPluginDllPath(fileName);
				if (!File.Exists(path))
				{
					return false;
				}

				byte[] data = File.ReadAllBytes(path);
				string text = System.Text.Encoding.ASCII.GetString(data);
				return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch
			{
				return false;
			}
		}

		private static string FindPluginDllPath(string fileName)
		{
			try
			{
				string directPath = Path.Combine(Paths.PluginPath, fileName);
				if (File.Exists(directPath))
				{
					return directPath;
				}

				if (Directory.Exists(Paths.PluginPath))
				{
					return Directory.GetFiles(Paths.PluginPath, fileName, SearchOption.AllDirectories).FirstOrDefault();
				}
			}
			catch
			{
			}

			return null;
		}
	}
}

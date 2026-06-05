using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace AfterProhibitionCompatibility
{
	internal static class CompatibilityDetector
	{
		private const int MaxAsciiProbeBytes = 4 * 1024 * 1024;

		internal static CompatibilitySnapshot Capture(bool scanPluginDlls, bool probeAsciiTokens, ManualLogSource log)
		{
			PluginSignals signals = PluginSignals.Capture(scanPluginDlls, log);
			var snapshot = new CompatibilitySnapshot
			{
				LoadedPluginCount = signals.PluginGuids.Count,
				LoadedAssemblyCount = signals.AssemblyNames.Count,
				PluginDllCount = signals.DllFileNames.Count,
				PluginDllScanEnabled = scanPluginDlls,
				AsciiTokenProbeEnabled = probeAsciiTokens
			};

			snapshot.DirtyCashEconomy = signals.HasExact("com.cogmod.dirtycasheconomy")
				|| signals.HasDll("DirtyCashEconomy")
				|| signals.HasAssembly("DirtyCashEconomy")
				|| signals.HasToken("dirtycasheconomy");
			snapshot.DirtyCashVolumeFix = signals.HasDll("DirtyCashVolumeFix")
				|| signals.HasAssembly("DirtyCashVolumeFix")
				|| signals.HasToken("dirtycashvolumefix");

			snapshot.TenKButtonInputDll = signals.HasDll("10K Button & Input")
				|| signals.HasDll("10KButtonInput");
			snapshot.TenKButtonInputToken = signals.HasToken("10kbutton")
				|| signals.HasToken("10kbuttoninput");
			snapshot.UiEnhancerGuidOrDll = signals.HasExact("com.pia.cityofgangsters.uienhancer")
				|| signals.HasDll("UIEnhancer")
				|| signals.HasAssembly("UIEnhancer");
			snapshot.TenKButtonInput = snapshot.TenKButtonInputDll
				|| snapshot.TenKButtonInputToken
				|| snapshot.UiEnhancerGuidOrDll;
			snapshot.PiaUiEnhancer = snapshot.UiEnhancerGuidOrDll || snapshot.TenKButtonInput;

			snapshot.SafeboxDll = signals.HasDll("Safebox") || signals.HasDll("SafeBox");
			snapshot.ModLauncherBridge = signals.HasExact("com.mods.modlauncher")
				|| signals.HasDll("ModLauncher")
				|| signals.HasDll("ProhibitionLauncher")
				|| signals.HasDll("TraplifeModLauncher");
			snapshot.PiaModLauncher = signals.HasExact("com.pia.modlauncher");
			snapshot.Launcher = snapshot.ModLauncherBridge
				|| snapshot.PiaModLauncher
				|| signals.HasToken("modlauncher")
				|| signals.HasToken("prohibitionlauncher")
				|| signals.HasToken("traplifemodlauncher");

			if (probeAsciiTokens)
			{
				snapshot.SafeboxLauncherSignal = ProbeLauncherSafeboxSignals(signals, snapshot, log);
			}

			snapshot.ExternalSafeboxUi = snapshot.SafeboxDll || snapshot.SafeboxLauncherSignal;
			snapshot.CrewHireManager = signals.HasExact("com.pia.crewhiremanager") || signals.HasDll("CrewHireManager");
			snapshot.RemoteInteraction = signals.HasExact("com.pia.remoteinteraction") || signals.HasDll("RemoteInteraction");
			snapshot.GameOptimizer = signals.HasExact("com.mods.gameoptimizer") || signals.HasDll("GameOptimizer") || signals.HasAssembly("GameOptimizer");
			snapshot.ThirdPartyOptimizer = signals.HasExact("com.modding.cityofgangsters.optimizer") || signals.HasDll("CityOfGangstersOptimizer");
			snapshot.DualOptimizer = snapshot.GameOptimizer && snapshot.ThirdPartyOptimizer;
			snapshot.MafiaHierarchy = signals.HasExact("com.pia.mafiahierarchy") || signals.HasDll("MafiaHierarchy") || signals.HasAssembly("MafiaHierarchy");
			snapshot.GangWars = signals.HasExact("com.pia.gangwars") || signals.HasDll("GangWars") || signals.HasAssembly("GangWars");
			snapshot.TerritoryExpansion = signals.HasExact("com.modder.territoryautoexpand") || signals.HasDll("TerritoryExpansionPatch");
			snapshot.TickerEnhancer = signals.HasExact("com.pia.tickerenhancer") || signals.HasDll("Tickerenhancer") || signals.HasDll("OutpostTickerMod");
			snapshot.CoreCheatGuidOrDll = signals.HasExact("com.pia.cogcheat") || signals.HasDll("Cog Ultimate Cheat");
			snapshot.ElectionCheatGuidOrDll = signals.HasExact("com.pia.electionmanager") || signals.HasDll("ElectionVoteCheat");
			snapshot.BossManagerGuidOrDll = signals.HasExact("com.pia.bossmanager") || signals.HasDll("Boss Manager") || signals.HasDll("BossManager");
			snapshot.CheatMenuToken = signals.HasToken("cheatmenu") || signals.HasToken("cogcheat");
			snapshot.CoreCheatMenu = snapshot.CoreCheatGuidOrDll || snapshot.CheatMenuToken;
			snapshot.ElectionCheat = snapshot.ElectionCheatGuidOrDll;
			snapshot.BossManagerCheat = snapshot.BossManagerGuidOrDll;
			snapshot.BlockedCheatPlugins = snapshot.ElectionCheat || snapshot.BossManagerCheat;
			snapshot.CrewListSorter = signals.HasExact("com.pia.crewlistsorter") || signals.HasDll("CrewListSorter");
			snapshot.CrewEditor = signals.HasExact("com.pia.creweditor") || signals.HasDll("Crew Editor") || signals.HasDll("CrewEditor");
			snapshot.NaturalDeathExternal = snapshot.CrewListSorter || snapshot.CrewEditor;
			snapshot.CoGCustomAssets = signals.HasDll("CoGCustomAssets") || signals.HasAssembly("CoGCustomAssets");
			snapshot.CustomPortraits = signals.HasDll("CustomPortraits") || signals.HasAssembly("CustomPortraits");
			snapshot.TraitIconLimit = signals.HasDll("Traiticonlimitplugin") || signals.HasAssembly("Traiticonlimitplugin");
			snapshot.CustomIcons = snapshot.CoGCustomAssets || snapshot.CustomPortraits || snapshot.TraitIconLimit;
			snapshot.MenuSidebarToken = signals.HasToken("sidebar")
				|| signals.HasToken("menuenhancer")
				|| signals.HasToken("ui enhancer");
			snapshot.ExternalMenuSidebar = snapshot.PiaUiEnhancer
				|| snapshot.PiaModLauncher
				|| snapshot.MenuSidebarToken;

			ApplyDllInventory(snapshot, signals);

			return snapshot;
		}

		private static void ApplyDllInventory(CompatibilitySnapshot snapshot, PluginSignals signals)
		{
			var localSuite = new List<string>();
			var knownExternal = new List<string>();
			var unknownExternal = new List<string>();
			var allDlls = new List<string>(signals.DllFileNames);
			allDlls.Sort(StringComparer.OrdinalIgnoreCase);

			foreach (string dllName in allDlls)
			{
				if (IsLocalSuiteDll(dllName))
				{
					localSuite.Add(dllName);
				}
				else if (IsKnownExternalDll(dllName))
				{
					knownExternal.Add(dllName);
				}
				else
				{
					unknownExternal.Add(dllName);
				}
			}

			snapshot.PluginDllCount = allDlls.Count;
			snapshot.LocalSuiteDllCount = localSuite.Count;
			snapshot.KnownExternalDllCount = knownExternal.Count;
			snapshot.UnknownExternalDllCount = unknownExternal.Count;
			snapshot.PluginDllNames = FormatNameList(allDlls, 64);
			snapshot.LocalSuiteDllNames = FormatNameList(localSuite, 32);
			snapshot.KnownExternalDllNames = FormatNameList(knownExternal, 48);
			snapshot.UnknownExternalDllNames = FormatNameList(unknownExternal, 32);
		}

		private static bool IsLocalSuiteDll(string dllName)
		{
			return ContainsAny(
				dllName,
				"AfterProhibition",
				"GameplayTweaks",
				"CopKilling",
				"BossBuildings",
				"BossDeath",
				"OrgChartMod",
				"AutoLevelup",
				"ModLauncher");
		}

		private static bool IsKnownExternalDll(string dllName)
		{
			return ContainsAny(
				dllName,
				"DirtyCashEconomy",
				"DirtyCashVolumeFix",
				"10K Button",
				"10KButton",
				"10KInput",
				"UIEnhancer",
				"Safebox",
				"SafeBox",
				"ProhibitionLauncher",
				"TraplifeModLauncher",
				"CrewHireManager",
				"RemoteInteraction",
				"GameOptimizer",
				"CityOfGangstersOptimizer",
				"MafiaHierarchy",
				"GangWars",
				"TerritoryExpansion",
				"TerritoryAutoExpand",
				"Tickerenhancer",
				"TickerEnhancer",
				"OutpostTicker",
				"Cog Ultimate Cheat",
				"CogUltimateCheat",
				"ElectionVoteCheat",
				"ElectionManager",
				"Boss Manager",
				"BossManager",
				"CrewListSorter",
				"Crew Editor",
				"CrewEditor",
				"CoGCustomAssets",
				"CustomPortraits",
				"Traiticonlimitplugin");
		}

		private static bool ContainsAny(string value, params string[] tokens)
		{
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}

			foreach (string token in tokens)
			{
				if (!string.IsNullOrEmpty(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static string FormatNameList(List<string> names, int maxNames)
		{
			if (names == null || names.Count <= 0)
			{
				return "none";
			}

			int take = Math.Min(names.Count, Math.Max(1, maxNames));
			string text = string.Join(",", names.GetRange(0, take).ToArray());
			if (names.Count > take)
			{
				text += ",+" + (names.Count - take) + "more";
			}

			return "\"" + text + "\"";
		}

		private static bool ProbeLauncherSafeboxSignals(PluginSignals signals, CompatibilitySnapshot snapshot, ManualLogSource log)
		{
			string[] launcherNames =
			{
				"ModLauncher",
				"ProhibitionLauncher",
				"TraplifeModLauncher"
			};

			string[] tokens =
			{
				"ToggleSafeBoxMod",
				"ToggleSafebox",
				"Safebox"
			};

			bool found = false;
			foreach (string launcherName in launcherNames)
			{
				foreach (string path in signals.GetDllPaths(launcherName))
				{
					foreach (string token in tokens)
					{
						snapshot.AsciiTokenProbeCount++;
						if (DllContainsAsciiToken(path, token, log))
						{
							snapshot.AsciiTokenHitCount++;
							found = true;
						}
					}
				}
			}

			return found;
		}

		private static bool DllContainsAsciiToken(string dllPath, string token, ManualLogSource log)
		{
			if (string.IsNullOrEmpty(dllPath) || string.IsNullOrEmpty(token))
			{
				return false;
			}

			try
			{
				var info = new FileInfo(dllPath);
				if (!info.Exists || info.Length <= 0 || info.Length > MaxAsciiProbeBytes)
				{
					return false;
				}

				byte[] data = File.ReadAllBytes(dllPath);
				string text = Encoding.ASCII.GetString(data);
				return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch (Exception ex)
			{
				log?.LogDebug("ASCII token probe skipped file=" + Path.GetFileName(dllPath) + " token=" + token + " error=" + ex.Message);
				return false;
			}
		}

		private sealed class PluginSignals
		{
			internal readonly HashSet<string> PluginGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			internal readonly HashSet<string> AssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			internal readonly HashSet<string> DllFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			private readonly Dictionary<string, List<string>> _dllPathsByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

			internal static PluginSignals Capture(bool scanPluginDlls, ManualLogSource log)
			{
				var signals = new PluginSignals();
				signals.CapturePluginGuids(log);
				signals.CaptureAssemblyNames(log);
				if (scanPluginDlls)
				{
					signals.CaptureDllNames(log);
				}

				return signals;
			}

			internal bool HasExact(string signal)
			{
				return PluginGuids.Contains(signal) || AssemblyNames.Contains(signal) || DllFileNames.Contains(signal);
			}

			internal bool HasAssembly(string assemblyName)
			{
				return AssemblyNames.Contains(assemblyName);
			}

			internal bool HasDll(string dllNameWithoutExtension)
			{
				return DllFileNames.Contains(dllNameWithoutExtension);
			}

			internal bool HasToken(string token)
			{
				return AnyContains(PluginGuids, token)
					|| AnyContains(AssemblyNames, token)
					|| AnyContains(DllFileNames, token);
			}

			internal IEnumerable<string> GetDllPaths(string dllNameWithoutExtension)
			{
				if (_dllPathsByName.TryGetValue(dllNameWithoutExtension, out List<string> paths))
				{
					return paths;
				}

				return Array.Empty<string>();
			}

			private void CapturePluginGuids(ManualLogSource log)
			{
				try
				{
					foreach (string guid in Chainloader.PluginInfos.Keys)
					{
						if (!string.IsNullOrEmpty(guid))
						{
							PluginGuids.Add(guid);
						}
					}
				}
				catch (Exception ex)
				{
					log?.LogDebug("Plugin GUID scan skipped: " + ex.Message);
				}
			}

			private void CaptureAssemblyNames(ManualLogSource log)
			{
				try
				{
					foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
					{
						string name = assembly.GetName().Name;
						if (!string.IsNullOrEmpty(name))
						{
							AssemblyNames.Add(name);
						}
					}
				}
				catch (Exception ex)
				{
					log?.LogDebug("Assembly scan skipped: " + ex.Message);
				}
			}

			private void CaptureDllNames(ManualLogSource log)
			{
				try
				{
					if (!Directory.Exists(Paths.PluginPath))
					{
						return;
					}

					foreach (string file in Directory.EnumerateFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories))
					{
						string name = Path.GetFileNameWithoutExtension(file);
						if (string.IsNullOrEmpty(name))
						{
							continue;
						}

						DllFileNames.Add(name);
						if (!_dllPathsByName.TryGetValue(name, out List<string> paths))
						{
							paths = new List<string>();
							_dllPathsByName[name] = paths;
						}

						paths.Add(file);
					}
				}
				catch (Exception ex)
				{
					log?.LogDebug("Plugin DLL scan skipped: " + ex.Message);
				}
			}

			private static bool AnyContains(IEnumerable<string> values, string token)
			{
				if (string.IsNullOrEmpty(token))
				{
					return false;
				}

				foreach (string value in values)
				{
					if (!string.IsNullOrEmpty(value) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return true;
					}
				}

				return false;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using HarmonyLib;

namespace AfterProhibitionCompatibility
{
	internal sealed class DirtyCashCompatibilityDecision
	{
		internal bool DirtyCashEconomyActive { get; set; }
		internal bool DirtyCashVolumeFixActive { get; set; }
		internal bool DirtyCashHarmonyPatchesActive { get; set; }
		internal int DirtyCashHarmonyPatchGroups { get; set; }
		internal bool ProtectRouteInput { get; set; }
		internal bool ProtectOriginalMethodOverrides { get; set; }
		internal bool ProtectMoneyRouting { get; set; }
		internal string DetectionSummary { get; set; } = "detection=unscanned";
		internal string PatchOwnerSummary { get; set; } = "patchOwners=unscanned";
		internal string RouteInputReason { get; set; } = "inactive";
		internal string OriginalOverrideReason { get; set; } = "inactive";

		internal string FormatBridgeString()
		{
			return "dirty-cash-classifier active=" + DirtyCashEconomyActive
				+ " volumeFix=" + DirtyCashVolumeFixActive
				+ " harmonyPatches=" + DirtyCashHarmonyPatchesActive
				+ " harmonyPatchGroups=" + DirtyCashHarmonyPatchGroups
				+ " protectRouteInput=" + ProtectRouteInput
				+ " routeReason=" + RouteInputReason
				+ " protectOriginalOverrides=" + ProtectOriginalMethodOverrides
				+ " originalOverrideReason=" + OriginalOverrideReason
				+ " protectMoneyRouting=" + ProtectMoneyRouting
				+ " " + DetectionSummary
				+ " " + PatchOwnerSummary;
		}
	}

	internal sealed class DirtyCashPatchOwnerScan
	{
		internal int ScannedMethods { get; set; }
		internal int ActivePatchGroups { get; set; }
		internal string ActiveMethods { get; set; } = "none-current";
		internal string ScanError { get; set; } = "";

		internal string FormatSummary()
		{
			string summary = "patchOwners owner=" + DirtyCashCompatibilityClassifier.DirtyCashEconomyHarmonyId
				+ " scannedMethods=" + ScannedMethods
				+ " activePatchGroups=" + ActivePatchGroups
				+ " activeMethods=" + ActiveMethods;
			if (!string.IsNullOrEmpty(ScanError))
			{
				summary += " scanError=" + ScanError;
			}

			return summary;
		}
	}

	internal static class DirtyCashCompatibilityClassifier
	{
		internal const string DirtyCashEconomyHarmonyId = "com.cogmod.dirtycasheconomy";
		private static readonly HashSet<string> LoggedClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		internal static DirtyCashCompatibilityDecision Classify(CompatibilitySnapshot snapshot)
		{
			snapshot = snapshot ?? CompatibilitySnapshot.Empty;
			bool dirtyCashActive = snapshot.DirtyCashEconomy;
			bool volumeFixActive = snapshot.DirtyCashVolumeFix;
			DirtyCashPatchOwnerScan patchScan = BuildPatchOwnerScan();
			bool dirtyCashPatchOwnerActive = patchScan.ActivePatchGroups > 0;
			bool anyDirtyCashSignal = dirtyCashActive || volumeFixActive || dirtyCashPatchOwnerActive;

			return new DirtyCashCompatibilityDecision
			{
				DirtyCashEconomyActive = dirtyCashActive,
				DirtyCashVolumeFixActive = volumeFixActive,
				DirtyCashHarmonyPatchesActive = dirtyCashPatchOwnerActive,
				DirtyCashHarmonyPatchGroups = patchScan.ActivePatchGroups,
				ProtectRouteInput = anyDirtyCashSignal,
				ProtectOriginalMethodOverrides = dirtyCashActive || dirtyCashPatchOwnerActive,
				ProtectMoneyRouting = anyDirtyCashSignal,
				DetectionSummary = BuildDetectionSummary(snapshot, dirtyCashPatchOwnerActive),
				RouteInputReason = anyDirtyCashSignal ? "external-route-input" : "inactive",
				OriginalOverrideReason = (dirtyCashActive || dirtyCashPatchOwnerActive) ? "external-original-method-owner" : "inactive",
				PatchOwnerSummary = patchScan.FormatSummary()
			};
		}

		internal static void LogBaseline(CompatibilitySnapshot snapshot, ManualLogSource log, string source)
		{
			if (log == null)
			{
				return;
			}

			DirtyCashCompatibilityDecision decision = Classify(snapshot);
			string key = source + "|" + decision.FormatBridgeString();
			if (!LoggedClassifications.Add(key))
			{
				return;
			}

			log.LogInfo(decision.FormatBridgeString() + " source=" + source);
		}

		private static string BuildDetectionSummary(CompatibilitySnapshot snapshot, bool dirtyCashPatchOwnerActive)
		{
			return "detection"
				+ " economyDllOrGuid=" + snapshot.DirtyCashEconomy
				+ " volumeDllOrGuid=" + snapshot.DirtyCashVolumeFix
				+ " harmonyOwner=" + dirtyCashPatchOwnerActive
				+ " knownExternalDlls=" + snapshot.KnownExternalDllNames;
		}

		private static DirtyCashPatchOwnerScan BuildPatchOwnerScan()
		{
			try
			{
				var methods = new List<MethodBase>
				{
					FindMethodByName(typeof(ConsumerModule), "DoConsumeAndPay"),
					FindMethodByName(typeof(ManufactureModule), "DoConsumeAndProduce"),
					FindMethodByName(typeof(ManufactureModuleConfig), "ProduceMfgItems"),
					FindMethodByName(typeof(PlayerFinances), "DoChangeMoney")
				};

				MethodBase recipeProduceAllItems = FindMethodByName(typeof(Recipe), "ProduceAllItems");
				if (recipeProduceAllItems != null)
				{
					methods.Add(recipeProduceAllItems);
				}

				int scanned = 0;
				int dirtyCashPatchGroups = 0;
				var dirtyCashMethods = new List<string>();
				foreach (MethodBase method in methods)
				{
					if (method == null)
					{
						continue;
					}

					scanned++;
					int groups = CountDirtyCashPatchGroups(method);
					if (groups > 0)
					{
						dirtyCashPatchGroups += groups;
						dirtyCashMethods.Add(method.DeclaringType?.Name + "." + method.Name + ":" + groups);
					}
				}

				string methodsText = dirtyCashMethods.Count > 0 ? string.Join(",", dirtyCashMethods.ToArray()) : "none-current";
				return new DirtyCashPatchOwnerScan
				{
					ScannedMethods = scanned,
					ActivePatchGroups = dirtyCashPatchGroups,
					ActiveMethods = methodsText
				};
			}
			catch (Exception ex)
			{
				return new DirtyCashPatchOwnerScan
				{
					ScanError = ex.GetType().Name
				};
			}
		}

		private static MethodBase FindMethodByName(Type type, string methodName)
		{
			if (type == null || string.IsNullOrEmpty(methodName))
			{
				return null;
			}

			foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
			{
				if (method != null && string.Equals(method.Name, methodName, StringComparison.Ordinal))
				{
					return method;
				}
			}

			return null;
		}

		private static int CountDirtyCashPatchGroups(MethodBase method)
		{
			Patches patches = Harmony.GetPatchInfo(method);
			if (patches == null)
			{
				return 0;
			}

			return CountOwners(patches.Prefixes)
				+ CountOwners(patches.Postfixes)
				+ CountOwners(patches.Transpilers)
				+ CountOwners(patches.Finalizers);
		}

		private static int CountOwners(IEnumerable<Patch> patches)
		{
			int count = 0;
			if (patches == null)
			{
				return 0;
			}

			foreach (Patch patch in patches)
			{
				if (string.Equals(patch?.owner, DirtyCashEconomyHarmonyId, StringComparison.OrdinalIgnoreCase))
				{
					count++;
				}
			}

			return count;
		}
	}
}

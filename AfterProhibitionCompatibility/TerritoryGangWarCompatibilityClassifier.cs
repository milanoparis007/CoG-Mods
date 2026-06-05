using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace AfterProhibitionCompatibility
{
	internal sealed class TerritoryGangWarCompatibilityDecision
	{
		internal bool GangWarsActive { get; set; }
		internal bool TerritoryExpansionActive { get; set; }
		internal bool MafiaHierarchyActive { get; set; }
		internal bool TickerOwnerActive { get; set; }
		internal bool ProtectTerritoryVisuals { get; set; }
		internal bool ProtectGangWarState { get; set; }
		internal bool ProtectTickerUi { get; set; }
		internal string TerritoryReason { get; set; } = "inactive";
		internal string GangWarReason { get; set; } = "inactive";
		internal string TickerReason { get; set; } = "inactive";
		internal string DetectionSummary { get; set; } = "detection=unscanned";
		internal string OwnerSummary { get; set; } = "owners=none";

		internal string FormatBridgeString()
		{
			return "territory-gangwar-classifier"
				+ " gangWars=" + GangWarsActive
				+ " territoryExpansion=" + TerritoryExpansionActive
				+ " mafiaHierarchy=" + MafiaHierarchyActive
				+ " ticker=" + TickerOwnerActive
				+ " protectTerritoryVisuals=" + ProtectTerritoryVisuals
				+ " territoryReason=" + TerritoryReason
				+ " protectGangWarState=" + ProtectGangWarState
				+ " gangWarReason=" + GangWarReason
				+ " protectTickerUi=" + ProtectTickerUi
				+ " tickerReason=" + TickerReason
				+ " " + DetectionSummary
				+ " " + OwnerSummary;
		}
	}

	internal static class TerritoryGangWarCompatibilityClassifier
	{
		private static readonly HashSet<string> LoggedClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		internal static TerritoryGangWarCompatibilityDecision Classify(CompatibilitySnapshot snapshot)
		{
			snapshot = snapshot ?? CompatibilitySnapshot.Empty;
			bool gangWars = snapshot.GangWars;
			bool territoryExpansion = snapshot.TerritoryExpansion;
			bool mafiaHierarchy = snapshot.MafiaHierarchy;
			bool ticker = snapshot.TickerEnhancer;

			return new TerritoryGangWarCompatibilityDecision
			{
				GangWarsActive = gangWars,
				TerritoryExpansionActive = territoryExpansion,
				MafiaHierarchyActive = mafiaHierarchy,
				TickerOwnerActive = ticker,
				ProtectTerritoryVisuals = territoryExpansion || mafiaHierarchy || gangWars,
				ProtectGangWarState = gangWars,
				ProtectTickerUi = ticker,
				TerritoryReason = BuildTerritoryReason(gangWars, territoryExpansion, mafiaHierarchy),
				GangWarReason = gangWars ? "external-gangwars-state-owner" : "inactive",
				TickerReason = ticker ? "external-ticker-ui-owner" : "inactive",
				DetectionSummary = BuildDetectionSummary(snapshot),
				OwnerSummary = BuildOwnerSummary(snapshot)
			};
		}

		internal static void LogBaseline(CompatibilitySnapshot snapshot, ManualLogSource log, string source)
		{
			if (log == null)
			{
				return;
			}

			TerritoryGangWarCompatibilityDecision decision = Classify(snapshot);
			string key = source + "|" + decision.FormatBridgeString();
			if (!LoggedClassifications.Add(key))
			{
				return;
			}

			log.LogInfo(decision.FormatBridgeString() + " source=" + source);
		}

		private static string BuildTerritoryReason(bool gangWars, bool territoryExpansion, bool mafiaHierarchy)
		{
			if (gangWars && territoryExpansion && mafiaHierarchy)
			{
				return "external-gangwars-territory-hierarchy";
			}

			if (gangWars && territoryExpansion)
			{
				return "external-gangwars-territory";
			}

			if (gangWars && mafiaHierarchy)
			{
				return "external-gangwars-hierarchy";
			}

			if (territoryExpansion && mafiaHierarchy)
			{
				return "external-territory-hierarchy";
			}

			if (gangWars)
			{
				return "external-gangwars-visual-owner";
			}

			if (territoryExpansion)
			{
				return "external-territory-expansion-owner";
			}

			if (mafiaHierarchy)
			{
				return "external-mafia-hierarchy-owner";
			}

			return "inactive";
		}

		private static string BuildDetectionSummary(CompatibilitySnapshot snapshot)
		{
			return "detection"
				+ " gangWars=" + snapshot.GangWars
				+ " territoryExpansion=" + snapshot.TerritoryExpansion
				+ " mafiaHierarchy=" + snapshot.MafiaHierarchy
				+ " ticker=" + snapshot.TickerEnhancer
				+ " knownExternalDlls=" + snapshot.KnownExternalDllNames;
		}

		private static string BuildOwnerSummary(CompatibilitySnapshot snapshot)
		{
			var owners = new List<string>();
			if (snapshot.GangWars)
			{
				owners.Add("GangWars");
			}

			if (snapshot.TerritoryExpansion)
			{
				owners.Add("TerritoryExpansion");
			}

			if (snapshot.MafiaHierarchy)
			{
				owners.Add("MafiaHierarchy");
			}

			if (snapshot.TickerEnhancer)
			{
				owners.Add("TickerEnhancer");
			}

			return owners.Count > 0 ? "owners=" + string.Join(",", owners.ToArray()) : "owners=none";
		}
	}
}

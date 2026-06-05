using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace AfterProhibitionCompatibility
{
	internal sealed class ElectionCheatCompatibilityDecision
	{
		internal bool CoreCheatActive { get; set; }
		internal bool ElectionCheatActive { get; set; }
		internal bool BossManagerActive { get; set; }
		internal bool BlockedCheatsActive { get; set; }
		internal bool ProtectElectionState { get; set; }
		internal bool ProtectBossState { get; set; }
		internal bool ProtectPoliticsMenu { get; set; }
		internal string ElectionReason { get; set; } = "inactive";
		internal string BossReason { get; set; } = "inactive";
		internal string MenuReason { get; set; } = "inactive";
		internal string DetectionSummary { get; set; } = "detection=unscanned";
		internal string OwnerSummary { get; set; } = "owners=none";

		internal string FormatBridgeString()
		{
			return "election-cheat-classifier"
				+ " coreCheat=" + CoreCheatActive
				+ " electionCheat=" + ElectionCheatActive
				+ " bossManager=" + BossManagerActive
				+ " blockedCheats=" + BlockedCheatsActive
				+ " protectElectionState=" + ProtectElectionState
				+ " electionReason=" + ElectionReason
				+ " protectBossState=" + ProtectBossState
				+ " bossReason=" + BossReason
				+ " protectPoliticsMenu=" + ProtectPoliticsMenu
				+ " menuReason=" + MenuReason
				+ " " + DetectionSummary
				+ " " + OwnerSummary;
		}
	}

	internal static class ElectionCheatCompatibilityClassifier
	{
		private static readonly HashSet<string> LoggedClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		internal static ElectionCheatCompatibilityDecision Classify(CompatibilitySnapshot snapshot)
		{
			snapshot = snapshot ?? CompatibilitySnapshot.Empty;
			bool electionCheat = snapshot.ElectionCheat;
			bool bossManager = snapshot.BossManagerCheat;
			bool coreCheat = snapshot.CoreCheatMenu;
			bool blocked = snapshot.BlockedCheatPlugins || electionCheat || bossManager;

			return new ElectionCheatCompatibilityDecision
			{
				CoreCheatActive = coreCheat,
				ElectionCheatActive = electionCheat,
				BossManagerActive = bossManager,
				BlockedCheatsActive = blocked,
				ProtectElectionState = electionCheat,
				ProtectBossState = bossManager,
				ProtectPoliticsMenu = coreCheat || blocked,
				ElectionReason = electionCheat ? "external-election-cheat-owner" : "inactive",
				BossReason = bossManager ? "external-boss-manager-owner" : "inactive",
				MenuReason = BuildMenuReason(snapshot, blocked),
				DetectionSummary = BuildDetectionSummary(snapshot),
				OwnerSummary = BuildOwnerSummary(snapshot, blocked)
			};
		}

		internal static void LogBaseline(CompatibilitySnapshot snapshot, ManualLogSource log, string source)
		{
			if (log == null)
			{
				return;
			}

			ElectionCheatCompatibilityDecision decision = Classify(snapshot);
			string key = source + "|" + decision.FormatBridgeString();
			if (!LoggedClassifications.Add(key))
			{
				return;
			}

			log.LogInfo(decision.FormatBridgeString() + " source=" + source);
		}

		private static string BuildDetectionSummary(CompatibilitySnapshot snapshot)
		{
			return "detection"
				+ " coreGuidOrDll=" + snapshot.CoreCheatGuidOrDll
				+ " electionGuidOrDll=" + snapshot.ElectionCheatGuidOrDll
				+ " bossManagerGuidOrDll=" + snapshot.BossManagerGuidOrDll
				+ " cheatMenuToken=" + snapshot.CheatMenuToken
				+ " knownExternalDlls=" + snapshot.KnownExternalDllNames;
		}

		private static string BuildMenuReason(CompatibilitySnapshot snapshot, bool blocked)
		{
			if (blocked && snapshot.CoreCheatMenu)
			{
				return "external-core-and-blocked-cheat-menu";
			}

			if (blocked)
			{
				return "external-blocked-cheat-menu";
			}

			if (snapshot.CoreCheatMenu)
			{
				return "external-core-cheat-menu";
			}

			return "inactive";
		}

		private static string BuildOwnerSummary(CompatibilitySnapshot snapshot, bool blocked)
		{
			var owners = new List<string>();
			if (snapshot.CoreCheatGuidOrDll)
			{
				owners.Add("CoreCheatMenu");
			}

			if (snapshot.ElectionCheatGuidOrDll)
			{
				owners.Add("ElectionCheat");
			}

			if (snapshot.BossManagerGuidOrDll)
			{
				owners.Add("BossManager");
			}

			if (snapshot.CheatMenuToken)
			{
				owners.Add("CheatMenuToken");
			}

			if (blocked)
			{
				owners.Add("BlockedCheats");
			}

			return owners.Count > 0 ? "owners=" + string.Join(",", owners.ToArray()) : "owners=none";
		}
	}
}

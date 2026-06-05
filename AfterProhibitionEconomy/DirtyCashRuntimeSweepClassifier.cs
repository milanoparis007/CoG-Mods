using System;

namespace AfterProhibitionEconomy
{
	internal static class DirtyCashRuntimeSweepClassifier
	{
		internal static DirtyCashRuntimeSweepSummary Classify(string source)
		{
			string normalizedSource = NormalizeSource(source);
			DirtyCashRuntimeSweepSummary summary = new DirtyCashRuntimeSweepSummary
			{
				Source = normalizedSource,
				ExecutionOwner = AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation() ? "AfterProhibitionEconomy" : "GameplayTweaks",
				ClassificationOwner = "AfterProhibitionEconomy",
				MutationMovedToEconomy = AfterProhibitionEconomyPlugin.OwnsDirtyCashRuntimeSweepMutation()
			};

			switch (normalizedSource)
			{
				case "update-business-modules":
				case "business-update-initial":
					summary.Phase = "business-update";
					summary.Reason = "business-module-runtime-safety";
					break;
				case "business-tick":
					summary.Phase = "business-tick";
					summary.Reason = "external-dirty-cash-tick-safety";
					break;
				case "pre-system-turn":
					summary.Phase = "system-turn";
					summary.Reason = "pre-turn-runtime-safety";
					break;
				case "human-turn-start":
					summary.Phase = "human-turn";
					summary.Reason = "player-runtime-safety";
					break;
				case "after-gambling-pre-respect":
					summary.Phase = "respect-recalculate";
					summary.Reason = "post-gambling-respect-safety";
					break;
				case "pre-recalculate":
					summary.Phase = "respect-recalculate";
					summary.Reason = "pre-recalculate-runtime-safety";
					break;
				default:
					summary.Phase = "unknown";
					summary.Reason = "unclassified-runtime-sweep-source";
					break;
			}

			return summary;
		}

		private static string NormalizeSource(string source)
		{
			return string.IsNullOrWhiteSpace(source)
				? "unknown"
				: source.Trim().ToLowerInvariant();
		}
	}

	public sealed class DirtyCashRuntimeSweepSummary
	{
		public string Source { get; internal set; }
		public string Phase { get; internal set; }
		public string Reason { get; internal set; }
		public string ExecutionOwner { get; internal set; }
		public string ClassificationOwner { get; internal set; }
		public bool MutationMovedToEconomy { get; internal set; }

		internal string FormatBridgeSummary()
		{
			return "dirty-cash-runtime-sweep source=" + (Source ?? "unknown") +
				" phase=" + (Phase ?? "unknown") +
				" reason=" + (Reason ?? "unknown") +
				" classificationOwner=" + (ClassificationOwner ?? "unknown") +
				" executionOwner=" + (ExecutionOwner ?? "unknown") +
				" mutationMovedToEconomy=" + MutationMovedToEconomy;
		}
	}
}

using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Session.Entities;
using HarmonyLib;

namespace AfterProhibitionPolitics
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class AfterProhibitionPoliticsPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.politics";
		public const string PluginName = "After Prohibition Politics";
		public const string PluginVersion = "0.6.3";

		internal static ManualLogSource Log { get; private set; }

		internal static AfterProhibitionPoliticsPlugin Instance { get; private set; }

		internal static ConfigEntry<bool> EnablePoliticsBaselineLog { get; private set; }

		internal static ConfigEntry<bool> EnableStartupPoliticsDiagnostics { get; private set; }

		internal static ConfigEntry<bool> EnableStartupPoliticsAudit { get; private set; }

		internal static ConfigEntry<int> PoliticsAuditSampleLimit { get; private set; }

		internal static ConfigEntry<bool> EnablePoliticalStarterQuestBridge { get; private set; }

		internal static ConfigEntry<bool> EnableBribeStateAudit { get; private set; }

		internal static ConfigEntry<bool> EnableBribeStateBridge { get; private set; }

		internal static ConfigEntry<bool> EnableJudgeLawOfficeAudit { get; private set; }

		internal static ConfigEntry<bool> EnableJudgeLawOfficeBridge { get; private set; }

		internal static ConfigEntry<bool> EnableCampaignElectionAudit { get; private set; }

		internal static ConfigEntry<bool> EnableCampaignElectionBridge { get; private set; }

		private Harmony _harmony;
		private bool _loggedPoliticsBaseline;

		private void Awake()
		{
			Instance = this;
			Log = Logger;

			BindConfig();

			Logger.LogInfo("politics baseline scheduled ownsUi=False ownsEconomy=False ownsFamily=False ownsRoutes=False ownsCompatibility=False");

			_harmony = new Harmony(PluginGuid);
			_harmony.PatchAll();
			PoliticsStarterQuestBridge.ApplyPatch(_harmony);

			Logger.LogInfo($"{PluginName} {PluginVersion} loaded phase=campaign-election-bridge ownsStarterQuest=True ownsBribes=False ownsJudgeLawOffice=False ownsCampaignElection=False ownsUi=False ownsEconomy=False ownsFamily=False ownsRoutes=False ownsCompatibility=False");
		}

		private IEnumerator Start()
		{
			yield return null;
			yield return new UnityEngine.WaitForSecondsRealtime(1f);
			LogPoliticsBaseline("start-1s");
			yield return LogStartupPoliticsAuditWhenReady("start-1s");
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
			EnablePoliticsBaselineLog = Config.Bind(
				"Features",
				"EnablePoliticsBaselineLog",
				true,
				"Logs a concise read-only politics baseline once shortly after startup.");

			EnableStartupPoliticsDiagnostics = Config.Bind(
				"Features",
				"EnableStartupPoliticsDiagnostics",
				false,
				"Allows full startup politics diagnostic scans. Disabled by default so ward, bribe, law-office, and campaign/election scans do not compete with map startup. Politics behavior bridges remain available.");

			EnableStartupPoliticsAudit = Config.Bind(
				"Features",
				"EnableStartupPoliticsAudit",
				true,
				"Logs read-only politics manager, ward, starter quest, election, and law-office counts once the session is ready.");

			PoliticsAuditSampleLimit = Config.Bind(
				"Features",
				"PoliticsAuditSampleLimit",
				8,
				"Maximum read-only politics audit sample lines to log at startup.");

			EnablePoliticalStarterQuestBridge = Config.Bind(
				"Features",
				"EnablePoliticalStarterQuestBridge",
				true,
				"Owns the guarded New York political starter quest ensure path. GameplayTweaks remains fallback when this plugin is not installed or this feature is disabled.");

			EnableBribeStateAudit = Config.Bind(
				"Features",
				"EnableBribeStateAudit",
				true,
				"Logs read-only political bribe, judge bribe, and AI mayor bribe state from existing GameplayTweaks save ownership.");

			EnableBribeStateBridge = Config.Bind(
				"Features",
				"EnableBribeStateBridge",
				true,
				"Exposes read-only political bribe state bridge methods. GameplayTweaks still owns payment, cleanup, UI, and save data.");

			EnableJudgeLawOfficeAudit = Config.Bind(
				"Features",
				"EnableJudgeLawOfficeAudit",
				true,
				"Logs read-only judge bribe, legal risk, law-office, and lawyer-retainer state from existing GameplayTweaks ownership.");

			EnableJudgeLawOfficeBridge = Config.Bind(
				"Features",
				"EnableJudgeLawOfficeBridge",
				true,
				"Exposes read-only judge/law-office/retainer bridge methods. GameplayTweaks still owns payments, trial outcomes, UI, and save data.");

			EnableCampaignElectionAudit = Config.Bind(
				"Features",
				"EnableCampaignElectionAudit",
				true,
				"Logs read-only campaign and election state, including active wards, candidate counts, and player campaign action definition counts.");

			EnableCampaignElectionBridge = Config.Bind(
				"Features",
				"EnableCampaignElectionBridge",
				true,
				"Exposes read-only campaign/election bridge methods. GameplayTweaks and vanilla systems still own campaign actions, votes, politician control, and election timing.");
		}

		private void LogPoliticsBaseline(string source)
		{
			if (_loggedPoliticsBaseline || !EnablePoliticsBaselineLog.Value)
			{
				return;
			}

			_loggedPoliticsBaseline = true;

			Logger.LogInfo("politics-baseline source=" + source + " phase=campaign-election-bridge ownsStarterQuest=True ownsBribes=False ownsJudgeLawOffice=False ownsCampaignElection=False ownsUi=False ownsEconomy=False ownsFamily=False ownsRoutes=False ownsCompatibility=False");
		}

		private IEnumerator LogStartupPoliticsAuditWhenReady(string source)
		{
			if (!EnableStartupPoliticsDiagnostics.Value)
			{
				Logger.LogInfo("politics-audit skipped source=" + source + " reason=startup-diagnostics-disabled");
				yield break;
			}

			if (!EnableStartupPoliticsAudit.Value)
			{
				yield break;
			}

			const int maxAttempts = 90;
			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				if (IsPoliticsAuditReady())
				{
					PoliticsStartupAudit.LogStartupAudit(source + "-ready-attempt-" + attempt, Logger, Math.Max(0, PoliticsAuditSampleLimit.Value));
					if (EnableBribeStateAudit.Value)
					{
						PoliticalBribeStateBridge.LogStartupAudit(source + "-ready-attempt-" + attempt, Logger);
					}
					if (EnableJudgeLawOfficeAudit.Value)
					{
						JudgeLawOfficeStateBridge.LogStartupAudit(source + "-ready-attempt-" + attempt, Logger);
					}
					if (EnableCampaignElectionAudit.Value)
					{
						CampaignElectionStateBridge.LogStartupAudit(source + "-ready-attempt-" + attempt, Logger);
					}
					yield break;
				}

				if (ShouldLogDeferredPoliticsAuditAttempt(attempt, maxAttempts))
				{
					Logger.LogInfo("politics-audit deferred source=" + source + " attempt=" + attempt + " reason=" + GetPoliticsAuditNotReadyReason());
				}

				yield return new UnityEngine.WaitForSecondsRealtime(2f);
			}

			Logger.LogInfo("politics-audit skipped source=" + source + " reason=" + GetPoliticsAuditNotReadyReason() + " attempts=" + maxAttempts);
		}

		private static bool IsPoliticsAuditReady()
		{
			try
			{
				return global::Game.Game.ctx?.simman?.politics != null
					&& global::Game.Game.ctx?.players?.Human != null
					&& global::Game.Game.ctx?.session?.mapconfig != null
					&& global::Game.Game.ctx?.quests != null
					&& global::Game.Game.ctx?.entityman != null
					&& (global::Game.Game.ctx?.IsInteractive ?? false)
					&& IsProcgenReadyForPoliticsAudit()
					&& TryGetCachedBuildingCount(out int buildingCount)
					&& buildingCount > 0
					&& TryGetPoliticsReadinessCounts(out int wardCount, out int validCurrentPoliticians, out int localPoliticians)
					&& wardCount > 0
					&& (validCurrentPoliticians > 0 || localPoliticians > 0);
			}
			catch
			{
				return false;
			}
		}

		private static string GetPoliticsAuditNotReadyReason()
		{
			try
			{
				if (global::Game.Game.ctx == null)
				{
					return "missing-game-context";
				}
				if (global::Game.Game.ctx.simman?.politics == null)
				{
					return "missing-politics-manager";
				}
				if (global::Game.Game.ctx.players?.Human == null)
				{
					return "missing-human-player";
				}
				if (global::Game.Game.ctx.session?.mapconfig == null)
				{
					return "missing-map-config";
				}
				if (global::Game.Game.ctx.quests == null)
				{
					return "missing-quest-manager";
				}
				if (global::Game.Game.ctx.entityman == null)
				{
					return "missing-entity-manager";
				}
				if (!global::Game.Game.ctx.IsInteractive)
				{
					return "session-not-interactive";
				}
				if (!IsProcgenReadyForPoliticsAudit())
				{
					return "procgen-not-ready";
				}
				if (!TryGetCachedBuildingCount(out int buildingCount) || buildingCount <= 0)
				{
					return "empty-building-cache";
				}
				if (!TryGetPoliticsReadinessCounts(out int wardCount, out int validCurrentPoliticians, out int localPoliticians) || wardCount <= 0)
				{
					return "empty-politics-wards";
				}
				if (validCurrentPoliticians <= 0 && localPoliticians <= 0)
				{
					return "empty-politics-population";
				}
			}
			catch (Exception ex)
			{
				return "readiness-error-" + ex.GetType().Name;
			}

			return "unknown";
		}

		private static bool ShouldLogDeferredPoliticsAuditAttempt(int attempt, int maxAttempts)
		{
			return attempt == 1
				|| attempt == 5
				|| attempt == 15
				|| attempt == 30
				|| attempt == 60
				|| attempt == maxAttempts;
		}

		private static bool TryGetCachedBuildingCount(out int count)
		{
			count = 0;
			try
			{
				System.Collections.Generic.IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return false;
				}

				foreach (Entity ignored in buildings)
				{
					count++;
					if (count > 0)
					{
						return true;
					}
				}

				return true;
			}
			catch
			{
				count = 0;
				return false;
			}
		}

		private static bool IsProcgenReadyForPoliticsAudit()
		{
			try
			{
				return global::Game.Game.ctx.clock.CurrentTurn >= 1
					&& global::Game.Game.ctx.clock.Now >= global::Game.Game.ctx.clock.LastDayOfProcGen;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryGetPoliticsReadinessCounts(out int wardCount, out int validCurrentPoliticians, out int localPoliticians)
		{
			wardCount = 0;
			validCurrentPoliticians = 0;
			localPoliticians = 0;
			try
			{
				Game.Session.Sim.PoliticsManager politics = global::Game.Game.ctx?.simman?.politics;
				System.Collections.Generic.IEnumerable<Game.Session.Sim.Ward> wards = politics?.GetWards();
				if (wards == null)
				{
					return false;
				}

				foreach (Game.Session.Sim.Ward ward in wards)
				{
					wardCount++;
					try
					{
						if (ward.currentPolitician.IsValid && ward.currentPolitician.FindEntity() != null && politics.GetPoliticianData(ward.currentPolitician) != null)
						{
							validCurrentPoliticians++;
						}
						if (ward.localPoliticians != null)
						{
							localPoliticians += ward.localPoliticians.Count;
						}
					}
					catch
					{
					}
				}

				return true;
			}
			catch
			{
				wardCount = 0;
				validCurrentPoliticians = 0;
				localPoliticians = 0;
				return false;
			}
		}

		public static bool IsPoliticsBridgeAvailable()
		{
			return Instance != null;
		}

		public static string GetPoliticsBridgeVersion()
		{
			return PluginVersion;
		}

		public static bool OwnsPoliticalStarterQuest()
		{
			return Instance != null && (EnablePoliticalStarterQuestBridge?.Value ?? false);
		}

		public static bool OwnsBribeStateClassification()
		{
			return Instance != null && (EnableBribeStateBridge?.Value ?? false);
		}

		public static string GetPoliticalBribeStateSummary()
		{
			return PoliticalBribeStateBridge.Capture().FormatBridgeSummary();
		}

		public static bool OwnsJudgeLawOfficeClassification()
		{
			return Instance != null && (EnableJudgeLawOfficeBridge?.Value ?? false);
		}

		public static string GetJudgeLawOfficeStateSummary()
		{
			return JudgeLawOfficeStateBridge.Capture().FormatBridgeSummary();
		}

		public static bool OwnsCampaignElectionClassification()
		{
			return Instance != null && (EnableCampaignElectionBridge?.Value ?? false);
		}

		public static string GetCampaignElectionStateSummary()
		{
			return CampaignElectionStateBridge.Capture().FormatBridgeSummary();
		}
	}
}

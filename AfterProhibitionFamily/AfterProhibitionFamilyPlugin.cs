using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Entities;
using HarmonyLib;

namespace AfterProhibitionFamily
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class AfterProhibitionFamilyPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.family";
		public const string PluginName = "After Prohibition Family";
		public const string PluginVersion = "0.8.4";

		internal static ManualLogSource Log { get; private set; }

		internal static AfterProhibitionFamilyPlugin Instance { get; private set; }

		internal static ConfigEntry<bool> EnableFamilyBaselineLog { get; private set; }

		internal static ConfigEntry<bool> EnableStartupFamilyDiagnostics { get; private set; }

		internal static ConfigEntry<bool> EnableStartupFamilyAudit { get; private set; }

		internal static ConfigEntry<int> FamilyAuditSampleLimit { get; private set; }

		internal static ConfigEntry<bool> EnableRelationshipSafetyBridge { get; private set; }

		internal static ConfigEntry<bool> EnableRelationshipSafetySampleLog { get; private set; }

		internal static ConfigEntry<bool> EnableBusinessOwnerFamilySafetyBridge { get; private set; }

		internal static ConfigEntry<bool> EnableSpouseSearchMigration { get; private set; }

		internal static ConfigEntry<bool> EnableSpouseSearchSampleLog { get; private set; }

		internal static ConfigEntry<int> SpouseSearchSampleLimit { get; private set; }

		internal static ConfigEntry<bool> EnableSpouseEthnicityPreference { get; private set; }

		internal static ConfigEntry<float> SpouseEthnicityPreferenceChance { get; private set; }

		internal static ConfigEntry<int> MarriageMinAge { get; private set; }

		internal static ConfigEntry<int> MarriageMaxAgeDifference { get; private set; }

		internal static ConfigEntry<bool> EnablePregnancyLifecycleBridge { get; private set; }

		internal static ConfigEntry<bool> EnableFutureKidValidation { get; private set; }

		internal static ConfigEntry<bool> EnablePregnancyTurnAudit { get; private set; }

		internal static ConfigEntry<bool> EnablePregnancyAuditEveryTurn { get; private set; }

		internal static ConfigEntry<bool> EnablePregnancyStartupAudit { get; private set; }

		internal static ConfigEntry<bool> EnableStartupFamilyGenerationFallback { get; private set; }

		internal static ConfigEntry<bool> EnableDeadRelationshipCleanupMigration { get; private set; }

		internal static ConfigEntry<int> DeadRelationshipStartupChunkSize { get; private set; }

		internal static ConfigEntry<int> PregnancyAuditSampleLimit { get; private set; }

		internal static ConfigEntry<int> PregnancyMinDays { get; private set; }

		internal static ConfigEntry<int> PregnancyMaxDays { get; private set; }

		private Harmony _harmony;
		private bool _loggedFamilyBaseline;

		private void Awake()
		{
			Instance = this;
			Log = Logger;

			BindConfig();

			Logger.LogInfo("family baseline scheduled ownsUi=False ownsEconomy=False ownsPolitics=False ownsRoutes=False");

			_harmony = new Harmony(PluginGuid);
			_harmony.PatchAll();
			SpouseSearchPatch.ApplyPatch(_harmony);
			PregnancyLifecycleBridge.ApplyPatch(_harmony);
			StartupFamilyGenerationPatch.ApplyPatch(_harmony);
			DeadRelationshipCleanupPatch.ApplyPatch(_harmony);

			Logger.LogInfo($"{PluginName} {PluginVersion} loaded phase=spouse-futurekid-validation ownsUi=False ownsEconomy=False ownsPolitics=False ownsRoutes=False");
		}

		private IEnumerator Start()
		{
			yield return null;
			yield return new UnityEngine.WaitForSecondsRealtime(1f);
			LogFamilyBaseline("start-1s");
			yield return LogStartupFamilyAuditWhenReady("start-1s");
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

		private void Update()
		{
			DeadRelationshipCleanupPatch.FlushPendingDeadRelationshipStartupScrubs("update");
		}

		private void BindConfig()
		{
			EnableFamilyBaselineLog = Config.Bind(
				"Features",
				"EnableFamilyBaselineLog",
				true,
				"Logs a concise read-only family baseline once shortly after startup.");

			EnableStartupFamilyDiagnostics = Config.Bind(
				"Features",
				"EnableStartupFamilyDiagnostics",
				false,
				"Allows full startup family and relationship diagnostic scans. Disabled by default so generated-person scans do not compete with map startup. Family behavior patches remain available.");

			EnableStartupFamilyAudit = Config.Bind(
				"Features",
				"EnableStartupFamilyAudit",
				true,
				"Logs a read-only relationship and family audit once shortly after startup.");

			FamilyAuditSampleLimit = Config.Bind(
				"Features",
				"FamilyAuditSampleLimit",
				12,
				"Maximum relationship-audit-sample lines to log at startup.");

			EnableRelationshipSafetyBridge = Config.Bind(
				"Features",
				"EnableRelationshipSafetyBridge",
				true,
				"Exposes read-only relationship safety bridge methods for GameplayTweaks delegation checks.");

			EnableRelationshipSafetySampleLog = Config.Bind(
				"Features",
				"EnableRelationshipSafetySampleLog",
				true,
				"Logs read-only relationship safety classification samples after startup family diagnostics are ready.");

			EnableBusinessOwnerFamilySafetyBridge = Config.Bind(
				"Features",
				"EnableBusinessOwnerFamilySafetyBridge",
				true,
				"Exposes read-only family safety classification for GameplayTweaks business-owner replacement and hiring safeguards. This plugin does not assign business owners or execute hires.");

			EnableSpouseSearchMigration = Config.Bind(
				"Features",
				"EnableSpouseSearchMigration",
				true,
				"Patches spouse search and candidate scoring in AfterProhibitionFamily. GameplayTweaks should skip its matching spouse-search patches when this bridge is active.");

			EnableSpouseSearchSampleLog = Config.Bind(
				"Features",
				"EnableSpouseSearchSampleLog",
				true,
				"Logs limited spouse-search classification and selection samples.");

			SpouseSearchSampleLimit = Config.Bind(
				"Features",
				"SpouseSearchSampleLimit",
				12,
				"Maximum spouse-search lines to log per game day.");

			EnableSpouseEthnicityPreference = Config.Bind(
				"SpouseSearch",
				"EnableSpouseEthnicityPreference",
				true,
				"Prefer same-ethnicity spouses while preserving relationship safety checks.");

			SpouseEthnicityPreferenceChance = Config.Bind(
				"SpouseSearch",
				"SpouseEthnicityPreferenceChance",
				0.8f,
				"Chance that generated spouse scoring strongly prefers same-ethnicity candidates.");

			MarriageMinAge = Config.Bind(
				"SpouseSearch",
				"MarriageMinAge",
				18,
				"Minimum age for spouse candidates.");

			MarriageMaxAgeDifference = Config.Bind(
				"SpouseSearch",
				"MarriageMaxAgeDifference",
				10,
				"Maximum spouse candidate age difference in years.");

			EnablePregnancyLifecycleBridge = Config.Bind(
				"Features",
				"EnablePregnancyLifecycleBridge",
				true,
				"Exposes pregnancy scheduling and child lifecycle validation bridge methods for GameplayTweaks.");

			EnableFutureKidValidation = Config.Bind(
				"Features",
				"EnableFutureKidValidation",
				true,
				"Validates pending future-child entries before vanilla birth processing and removes entries whose mother/spouse state would crash or create invalid children.");

			EnablePregnancyTurnAudit = Config.Bind(
				"Features",
				"EnablePregnancyTurnAudit",
				false,
				"Logs read-only future-child pregnancy audit counts before PeopleTracker processes births. Disabled by default because large saves can feel paused during skipped turns.");

			EnablePregnancyAuditEveryTurn = Config.Bind(
				"Features",
				"EnablePregnancyAuditEveryTurn",
				false,
				"Logs pregnancy audit every PeopleTracker turn. Default false lets the bridge throttle turn-start audits.");

			EnablePregnancyStartupAudit = Config.Bind(
				"Features",
				"EnablePregnancyStartupAudit",
				false,
				"Allows pregnancy audit logging before the session becomes interactive. Default false avoids startup family-generation log spam.");

			EnableStartupFamilyGenerationFallback = Config.Bind(
				"Features",
				"EnableStartupFamilyGenerationFallback",
				true,
				"Patches startup parent fallback and generated person fallback in AfterProhibitionFamily. GameplayTweaks should skip matching family-generation startup patches when this bridge is active.");

			EnableDeadRelationshipCleanupMigration = Config.Bind(
				"Features",
				"EnableDeadRelationshipCleanupMigration",
				true,
				"Patches dead person relationship cleanup in AfterProhibitionFamily. GameplayTweaks should skip matching relationship cleanup hooks when this bridge is active.");

			DeadRelationshipStartupChunkSize = Config.Bind(
				"Features",
				"DeadRelationshipStartupChunkSize",
				96,
				"Maximum dead people to scrub from relationship data per startup cleanup pass. Lower values reduce load hitches; higher values finish cleanup sooner.");

			PregnancyAuditSampleLimit = Config.Bind(
				"Features",
				"PregnancyAuditSampleLimit",
				8,
				"Maximum pregnancy-audit-sample lines to log per audit.");

			PregnancyMinDays = Config.Bind(
				"Pregnancy",
				"PregnancyMinDays",
				210,
				"Minimum pregnancy duration in days.");

			PregnancyMaxDays = Config.Bind(
				"Pregnancy",
				"PregnancyMaxDays",
				284,
				"Maximum pregnancy duration in days.");
		}

		private void LogFamilyBaseline(string source)
		{
			if (_loggedFamilyBaseline || !EnableFamilyBaselineLog.Value)
			{
				return;
			}

			_loggedFamilyBaseline = true;
			Logger.LogInfo("family-baseline source=" + source + " phase=spouse-futurekid-validation patches=8 ownsSpouseSearch=" + OwnsSpouseSearchMigration() + " ownsPregnancy=" + OwnsPregnancyLifecycle() + " ownsFutureKidValidation=" + OwnsFutureKidValidation() + " ownsChildren=False ownsFamilyGeneration=" + OwnsStartupFamilyGenerationFallback() + " ownsDeadRelationshipCleanup=" + OwnsDeadRelationshipCleanupMigration() + " deadRelationshipStartupChunkSize=" + GetDeadRelationshipStartupChunkSize() + " ownsRelationshipSafety=True ownsBusinessOwnerFamilySafety=" + OwnsBusinessOwnerFamilySafety());
		}

		private IEnumerator LogStartupFamilyAuditWhenReady(string source)
		{
			if (!EnableStartupFamilyDiagnostics.Value)
			{
				Logger.LogInfo("relationship-audit skipped source=" + source + " reason=startup-diagnostics-disabled");
				yield break;
			}

			if (!EnableStartupFamilyAudit.Value && !EnableRelationshipSafetySampleLog.Value)
			{
				yield break;
			}

			const int maxAttempts = 90;
			bool loggedPendingDeadCleanupDeferral = false;
			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				if (!(global::Game.Game.ctx?.IsInteractive ?? false))
				{
					if (ShouldLogDeferredFamilyAuditAttempt(attempt, maxAttempts))
					{
						Logger.LogInfo("relationship-audit deferred source=" + source + " attempt=" + attempt + " reason=session-not-interactive");
					}
					yield return new UnityEngine.WaitForSecondsRealtime(2f);
					continue;
				}

				if (TryGetTrackedPersonCount(out int personCount) && personCount > 0 && HasRelationshipTracker())
				{
					if (OwnsDeadRelationshipCleanupMigration() && DeadRelationshipCleanupPatch.HasPendingDeadRelationshipStartupScrubs())
					{
						bool flushed = DeadRelationshipCleanupPatch.FlushPendingDeadRelationshipStartupScrubs("pre-family-audit");
						if (DeadRelationshipCleanupPatch.HasPendingDeadRelationshipStartupScrubs())
						{
							if (!loggedPendingDeadCleanupDeferral || ShouldLogDeferredFamilyAuditAttempt(attempt, maxAttempts))
							{
								loggedPendingDeadCleanupDeferral = true;
								Logger.LogInfo("relationship-audit deferred source=" + source + " attempt=" + attempt + " reason=pending-dead-relationship-cleanup pending=" + DeadRelationshipCleanupPatch.PendingDeadRelationshipStartupScrubCount() + " flushed=" + flushed);
							}
							yield return new UnityEngine.WaitForSecondsRealtime(2f);
							continue;
						}
					}

					string readySource = source + "-ready-attempt-" + attempt;
					if (EnableStartupFamilyAudit.Value)
					{
						FamilyStartupAudit.LogStartupAudit(readySource, Logger, Math.Max(0, FamilyAuditSampleLimit.Value));
					}
					if (EnableRelationshipSafetySampleLog.Value)
					{
						RelationshipSafetyClassifier.LogStartupSamples(readySource, Logger, Math.Max(0, FamilyAuditSampleLimit.Value));
					}
					yield break;
				}

				if (ShouldLogDeferredFamilyAuditAttempt(attempt, maxAttempts))
				{
					string reason = global::Game.Game.ctx?.simman?.peoplegen == null
						? "missing-people-tracker"
						: !HasRelationshipTracker()
							? "missing-relationship-tracker"
							: "empty-people-cache";
					Logger.LogInfo("relationship-audit deferred source=" + source + " attempt=" + attempt + " reason=" + reason);
				}
				yield return new UnityEngine.WaitForSecondsRealtime(2f);
			}

			Logger.LogInfo("relationship-audit skipped source=" + source + " reason=missing-family-state-after-retries attempts=" + maxAttempts);
		}

		private static bool ShouldLogDeferredFamilyAuditAttempt(int attempt, int maxAttempts)
		{
			return attempt == 1
				|| attempt == 5
				|| attempt == 15
				|| attempt == 30
				|| attempt == 60
				|| attempt == maxAttempts;
		}

		private static bool TryGetTrackedPersonCount(out int count)
		{
			count = 0;
			try
			{
				IEnumerable people = global::Game.Game.ctx?.simman?.peoplegen?.GetAllTrackedPeople();
				if (people == null)
				{
					return false;
				}

				foreach (object ignored in people)
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

		private static bool HasRelationshipTracker()
		{
			return global::Game.Game.ctx?.simman?.rels?.data?.entries != null;
		}

		public static bool IsFamilyBridgeAvailable()
		{
			return Instance != null;
		}

		public static string GetFamilyBridgeVersion()
		{
			return PluginVersion;
		}

		public static bool OwnsRelationshipSafetyClassification()
		{
			return Instance != null && (EnableRelationshipSafetyBridge?.Value ?? false);
		}

		public static bool OwnsBusinessOwnerFamilySafety()
		{
			return Instance != null && (EnableBusinessOwnerFamilySafetyBridge?.Value ?? false);
		}

		public static bool OwnsSpouseSearchMigration()
		{
			return Instance != null && (EnableSpouseSearchMigration?.Value ?? false);
		}

		public static bool OwnsPregnancyLifecycle()
		{
			return Instance != null && (EnablePregnancyLifecycleBridge?.Value ?? false);
		}

		public static bool OwnsFutureKidValidation()
		{
			return Instance != null && (EnableFutureKidValidation?.Value ?? false);
		}

		public static bool OwnsStartupFamilyGenerationFallback()
		{
			return Instance != null && (EnableStartupFamilyGenerationFallback?.Value ?? false);
		}

		public static bool OwnsDeadRelationshipCleanupMigration()
		{
			return Instance != null && (EnableDeadRelationshipCleanupMigration?.Value ?? false);
		}

		public static int GetDeadRelationshipStartupChunkSize()
		{
			int configured = DeadRelationshipStartupChunkSize?.Value ?? 96;
			return Math.Max(16, Math.Min(512, configured));
		}

		public static int GetDeadRelationshipStartupEntryScanBudget()
		{
			return Math.Max(32, Math.Min(512, GetDeadRelationshipStartupChunkSize()));
		}

		public static bool TrySchedulePregnancyForCrewPeep(
			Entity selectedPeep,
			out ulong motherId,
			out int dueDay,
			out int durationDays,
			out int futureKidsCount,
			out string reason)
		{
			return PregnancyLifecycleBridge.TrySchedulePregnancyForCrewPeep(
				selectedPeep,
				out motherId,
				out dueDay,
				out durationDays,
				out futureKidsCount,
				out reason);
		}

		public static bool IsValidSpouseCandidate(Entity current, Entity other, out string reason)
		{
			if (!OwnsRelationshipSafetyClassification())
			{
				reason = "bridge-disabled";
				return false;
			}

			return RelationshipSafetyClassifier.IsValidSpouseCandidate(current, other, out reason);
		}

		public static bool IsValidParentChildLink(Entity parent, Entity child, out string reason)
		{
			if (!OwnsRelationshipSafetyClassification())
			{
				reason = "bridge-disabled";
				return false;
			}

			return RelationshipSafetyClassifier.IsValidParentChildLink(parent, child, out reason);
		}

		public static bool IsBusinessOwnerFamilySafe(Entity person, out string reason)
		{
			if (!OwnsBusinessOwnerFamilySafety())
			{
				reason = "bridge-disabled";
				return false;
			}

			return RelationshipSafetyClassifier.IsBusinessOwnerFamilySafe(person, out reason);
		}

		public static bool IsRelationshipTargetAlive(EntityID peepId, out string reason)
		{
			if (!OwnsRelationshipSafetyClassification())
			{
				reason = "bridge-disabled";
				return false;
			}

			return RelationshipSafetyClassifier.IsRelationshipTargetAlive(peepId, out reason);
		}

		public static string GetRelationshipSafetySummary(EntityID peepId)
		{
			if (!OwnsRelationshipSafetyClassification())
			{
				return "status=disabled reason=bridge-disabled";
			}

			return RelationshipSafetyClassifier.GetRelationshipSafetySummary(peepId);
		}
	}
}

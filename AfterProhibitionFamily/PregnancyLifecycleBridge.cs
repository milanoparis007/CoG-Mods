using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using HarmonyLib;

namespace AfterProhibitionFamily
{
	internal static class PregnancyLifecycleBridge
	{
		private const int PregnancyAuditCooldownDays = 28;

		private static readonly System.Random Rng = new System.Random();
		private static readonly bool VerbosePregnancyAudit = string.Equals(Environment.GetEnvironmentVariable("COG_FAMILY_VERBOSE_PREGNANCY_AUDIT"), "1", StringComparison.Ordinal);
		private static readonly HashSet<ulong> FutureKidParentIds = new HashSet<ulong>();
		private static readonly List<ulong> FutureKidParentScanBuffer = new List<ulong>(1024);
		private static int _lastAuditDay = int.MinValue;
		private static bool _sawInteractivePregnancyTurnAuditWindow;
		private static object _futureKidCacheContext;
		private static bool _futureKidCacheInitialized;

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				harmony.PatchAll(typeof(PeopleTrackerTurnAuditPatch));
				MethodInfo processBirths = AccessTools.Method(typeof(PeopleTracker), "ProcessBirths", new[] { typeof(SimTime) });
				if (processBirths != null)
				{
					harmony.Patch(
						processBirths,
						prefix: new HarmonyMethod(typeof(BirthValidationPatch), nameof(BirthValidationPatch.Prefix)),
						finalizer: new HarmonyMethod(typeof(BirthValidationPatch), nameof(BirthValidationPatch.Finalizer)));
				}
				MethodInfo linkCouple = AccessTools.Method(typeof(PersonCreator), "LinkCouple", new[] { typeof(Entity), typeof(Entity), typeof(SimTime), typeof(int) });
				if (linkCouple != null)
				{
					harmony.Patch(
						linkCouple,
						postfix: new HarmonyMethod(typeof(BirthValidationPatch), nameof(BirthValidationPatch.LinkCouplePostfix)));
				}
				AfterProhibitionFamilyPlugin.Log?.LogInfo("pregnancy lifecycle audit patch applied peopleTurn=True birthValidation=" + (processBirths != null));
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("pregnancy lifecycle audit patch failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		internal static bool TrySchedulePregnancyForCrewPeep(
			Entity selectedPeep,
			out ulong motherId,
			out int dueDay,
			out int durationDays,
			out int futureKidsCount,
			out string reason)
		{
			motherId = 0UL;
			dueDay = -1;
			durationDays = 0;
			futureKidsCount = 0;
			reason = "ok";

			if (!AfterProhibitionFamilyPlugin.OwnsPregnancyLifecycle())
			{
				reason = "bridge-disabled";
				return false;
			}
			if (selectedPeep?.data?.person == null)
			{
				reason = "missing-selected-person-data";
				return false;
			}

			RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
			RelationshipList list = rels?.GetListOrNull(selectedPeep.Id);
			Entity spouse = list?.GetSpouse();
			if (spouse?.data?.person == null)
			{
				reason = "missing-spouse";
				return false;
			}

			Entity mother = selectedPeep.data.person.g == Gender.F ? selectedPeep : spouse;
			Entity father = ReferenceEquals(mother, selectedPeep) ? spouse : selectedPeep;
			if (mother?.data?.person == null)
			{
				reason = "missing-mother-person-data";
				return false;
			}
			if (father?.data?.person == null)
			{
				reason = "missing-father-person-data";
				return false;
			}
			if (mother.data.person.g != Gender.F)
			{
				reason = "missing-female-parent";
				return false;
			}
			if (!mother.data.person.IsAlive || !father.data.person.IsAlive)
			{
				reason = "dead-parent";
				return false;
			}
			if (!RelationshipSafetyClassifier.IsRelationshipTargetAlive(mother.Id, out string motherAliveReason))
			{
				reason = "invalid-mother:" + motherAliveReason;
				return false;
			}
			if (!RelationshipSafetyClassifier.IsRelationshipTargetAlive(father.Id, out string fatherAliveReason))
			{
				reason = "invalid-father:" + fatherAliveReason;
				return false;
			}

			SimTime now = global::Game.Game.ctx?.clock != null
				? global::Game.Game.ctx.clock.Now
				: SimTime.MIN_DATE;
			int minDays = Math.Max(1, AfterProhibitionFamilyPlugin.PregnancyMinDays.Value);
			int maxDays = Math.Max(minDays, AfterProhibitionFamilyPlugin.PregnancyMaxDays.Value);
			durationDays = Rng.Next(minDays, maxDays + 1);
			SimTime dueDate = now.IncrementDays(durationDays);
			mother.data.person.futurekids.Add(dueDate);
			mother.data.person.futurekids.Sort(SimTime.CompareDescending);
			TrackFutureKidParent(mother);
			motherId = mother.Id.id;
			dueDay = dueDate.days;
			futureKidsCount = mother.data.person.futurekids.Count;

			AfterProhibitionFamilyPlugin.Log?.LogInfo(
				"pregnancy-scheduled source=bridge peep=" + IdText(selectedPeep.Id) +
				" mother=" + IdText(mother.Id) +
				" father=" + IdText(father.Id) +
				" dueDay=" + dueDay +
				" durationDays=" + durationDays +
				" futureKids=" + futureKidsCount);
			return true;
		}

		private static class PeopleTrackerTurnAuditPatch
		{
			[HarmonyPatch(typeof(PeopleTracker), "OnSystemTurn")]
			[HarmonyPrefix]
			private static void Prefix(PeopleTracker __instance)
			{
				if (!AfterProhibitionFamilyPlugin.EnablePregnancyTurnAudit.Value)
				{
					return;
				}

				if (!ShouldRunPregnancyTurnAudit())
				{
					return;
				}

				LogPregnancyAudit("turn-start", __instance);
			}
		}

		private static class BirthValidationPatch
		{
			internal static void Prefix(PeopleTracker __instance, SimTime now)
			{
				ValidateFutureKids("pre-birth", __instance, now, dueOnly: true);
			}

			internal static Exception Finalizer(Exception __exception, ref int __result)
			{
				if (__exception == null)
				{
					return null;
				}

				__result = 0;
				AfterProhibitionFamilyPlugin.Log?.LogWarning("futurekid-validation birth-processing-suppressed error=" + __exception.GetType().Name + ":" + __exception.Message);
				return null;
			}

			internal static void LinkCouplePostfix(Entity f)
			{
				TrackFutureKidParent(f);
			}
		}

		private static void ValidateFutureKids(string source, PeopleTracker people, SimTime now, bool dueOnly)
		{
			if (!AfterProhibitionFamilyPlugin.OwnsFutureKidValidation())
			{
				return;
			}
			if (!AfterProhibitionFamilyPlugin.EnablePregnancyStartupAudit.Value
				&& !(global::Game.Game.ctx?.IsInteractive ?? false))
			{
				return;
			}

			try
			{
				long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
				if (people == null || rels == null)
				{
					return;
				}

				int parentsScanned = 0;
				int invalidParents = 0;
				int removedFutureKids = 0;
				int samples = 0;
				int sampleLimit = GetHotPathPregnancySampleLimit(source);

				EnsureFutureKidParentCache(people);
				CopyFutureKidParentKeysToBuffer();
				for (int i = 0; i < FutureKidParentScanBuffer.Count; i++)
				{
					Entity parent = EntityID.FromID(FutureKidParentScanBuffer[i]).FindEntity();
					PersonData pdata = parent?.data?.person;
					List<SimTime> futureKids = pdata?.futurekids;
					if (futureKids == null || futureKids.Count == 0)
					{
						if (parent != null)
						{
							FutureKidParentIds.Remove(parent.Id.id);
						}
						continue;
					}

					if (dueOnly && futureKids[futureKids.Count - 1].days > now.days)
					{
						continue;
					}

					parentsScanned++;
					string reason = GetPregnancyInvalidReason(parent, rels);
					if (string.Equals(reason, "ok", StringComparison.Ordinal))
					{
						continue;
					}

					int removed = futureKids.Count;
					futureKids.Clear();
					invalidParents++;
					removedFutureKids += removed;
					if (samples < sampleLimit)
					{
						samples++;
						AfterProhibitionFamilyPlugin.Log?.LogInfo("futurekid-validation-sample source=" + source + " parent=" + IdText(parent.Id) + " removed=" + removed + " reason=" + reason);
					}
				}

				if (removedFutureKids > 0)
				{
					AfterProhibitionFamilyPlugin.Log?.LogInfo("futurekid-validation source=" + source + " parentsScanned=" + parentsScanned + " invalidParents=" + invalidParents + " removedFutureKids=" + removedFutureKids + " ms=" + ElapsedMilliseconds(startTicks));
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("futurekid-validation failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void LogPregnancyAudit(string source, PeopleTracker people)
		{
			try
			{
				if (!AfterProhibitionFamilyPlugin.EnablePregnancyStartupAudit.Value
					&& !(global::Game.Game.ctx?.IsInteractive ?? false))
				{
					return;
				}

				SimTime now = global::Game.Game.ctx?.clock != null
					? global::Game.Game.ctx.clock.Now
					: SimTime.MIN_DATE;
				if (!AfterProhibitionFamilyPlugin.EnablePregnancyAuditEveryTurn.Value
					&& _lastAuditDay != int.MinValue
					&& now.days < _lastAuditDay + PregnancyAuditCooldownDays)
				{
					return;
				}

				_lastAuditDay = now.days;
				long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

				RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
				int pregnancies = 0;
				int due = 0;
				int invalidMothers = 0;
				int invalidFathers = 0;
				int duplicateDueDates = 0;
				int sampleLimit = GetHotPathPregnancySampleLimit(source);
				int samples = 0;

				EnsureFutureKidParentCache(people);
				CopyFutureKidParentKeysToBuffer();
				for (int i = 0; i < FutureKidParentScanBuffer.Count; i++)
				{
					Entity person = EntityID.FromID(FutureKidParentScanBuffer[i]).FindEntity();
					PersonData pdata = person?.data?.person;
					List<SimTime> futureKids = pdata?.futurekids;
					if (futureKids == null || futureKids.Count == 0)
					{
						if (person != null)
						{
							FutureKidParentIds.Remove(person.Id.id);
						}
						continue;
					}

					pregnancies += futureKids.Count;
					HashSet<int> seenDueDays = new HashSet<int>();
					foreach (SimTime futureKid in futureKids)
					{
						if (futureKid.days <= now.days)
						{
							due++;
						}
						if (!seenDueDays.Add(futureKid.days))
						{
							duplicateDueDates++;
						}
					}

					string invalidReason = GetPregnancyInvalidReason(person, rels);
					if (string.Equals(invalidReason, "invalid-mother", StringComparison.Ordinal))
					{
						invalidMothers += futureKids.Count;
					}
					else if (!string.Equals(invalidReason, "ok", StringComparison.Ordinal))
					{
						invalidFathers += futureKids.Count;
					}

					if (!string.Equals(invalidReason, "ok", StringComparison.Ordinal) && samples < sampleLimit)
					{
						samples++;
						AfterProhibitionFamilyPlugin.Log?.LogInfo("pregnancy-audit-sample source=" + source + " parent=" + IdText(person.Id) + " count=" + futureKids.Count + " reason=" + invalidReason);
					}
				}

				AfterProhibitionFamilyPlugin.Log?.LogInfo(
					"pregnancy-audit source=" + source +
					" pregnancies=" + pregnancies +
					" due=" + due +
					" invalidMothers=" + invalidMothers +
					" invalidFathers=" + invalidFathers +
					" duplicateDueDates=" + duplicateDueDates +
					" ms=" + ElapsedMilliseconds(startTicks));
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("pregnancy-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static bool ShouldRunPregnancyTurnAudit()
		{
			if (AfterProhibitionFamilyPlugin.EnablePregnancyAuditEveryTurn.Value || VerbosePregnancyAudit)
			{
				return true;
			}

			if (!(global::Game.Game.ctx?.IsInteractive ?? false))
			{
				return false;
			}

			SimTime now = global::Game.Game.ctx?.clock != null
				? global::Game.Game.ctx.clock.Now
				: SimTime.MIN_DATE;
			if (!_sawInteractivePregnancyTurnAuditWindow || _lastAuditDay == int.MinValue)
			{
				_sawInteractivePregnancyTurnAuditWindow = true;
				_lastAuditDay = now.days;
				return false;
			}

			return now.days >= _lastAuditDay + PregnancyAuditCooldownDays;
		}

		private static int GetHotPathPregnancySampleLimit(string source)
		{
			if (!VerbosePregnancyAudit
				&& (string.Equals(source, "turn-start", StringComparison.Ordinal)
					|| string.Equals(source, "pre-birth", StringComparison.Ordinal)))
			{
				return 0;
			}

			return Math.Max(0, AfterProhibitionFamilyPlugin.PregnancyAuditSampleLimit.Value);
		}

		private static long ElapsedMilliseconds(long startTicks)
		{
			return (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000L / System.Diagnostics.Stopwatch.Frequency;
		}

		private static string GetPregnancyInvalidReason(Entity mother, RelationshipTracker rels)
		{
			PersonData motherPerson = mother?.data?.person;
			if (motherPerson == null || motherPerson.g != Gender.F || !motherPerson.IsAlive)
			{
				return "invalid-mother";
			}

			Entity spouse = rels?.GetListOrNull(mother.Id)?.GetSpouse();
			PersonData spousePerson = spouse?.data?.person;
			if (spousePerson == null)
			{
				return "missing-spouse";
			}
			if (!spousePerson.IsAlive)
			{
				return "dead-spouse";
			}
			if (!RelationshipSafetyClassifier.IsValidExistingSpouseLink(mother, spouse, out string spouseReason))
			{
				return "invalid-spouse:" + spouseReason;
			}

			return "ok";
		}

		private static void TrackFutureKidParent(Entity parent)
		{
			try
			{
				if (parent?.data?.person?.futurekids == null || parent.data.person.futurekids.Count == 0)
				{
					return;
				}

				FutureKidParentIds.Add(parent.Id.id);
			}
			catch
			{
			}
		}

		private static void EnsureFutureKidParentCache(PeopleTracker people)
		{
			if (people == null)
			{
				return;
			}

			object context = global::Game.Game.ctx;
			if (_futureKidCacheInitialized && ReferenceEquals(_futureKidCacheContext, context))
			{
				return;
			}

			FutureKidParentIds.Clear();
			_futureKidCacheContext = context;
			_futureKidCacheInitialized = true;
			foreach (Entity person in people?.GetAllTrackedPeople() ?? Array.Empty<Entity>())
			{
				TrackFutureKidParent(person);
			}
		}

		private static void CopyFutureKidParentKeysToBuffer()
		{
			FutureKidParentScanBuffer.Clear();
			foreach (ulong parentId in FutureKidParentIds)
			{
				FutureKidParentScanBuffer.Add(parentId);
			}
		}

		private static string IdText(EntityID id)
		{
			return id.IsValid ? id.id.ToString() : "invalid";
		}
	}
}

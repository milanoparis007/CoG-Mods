using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace AfterProhibitionFamily
{
	internal static class SpouseSearchPatch
	{
		private static readonly System.Random Rng = new System.Random();
		private static bool _forceSameEthnicity;
		private static int _lastSampleDay = -1;
		private static int _samplesLoggedToday;

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				harmony.PatchAll(typeof(FindMatchAndLinkCouplePatch));
				harmony.PatchAll(typeof(ScoreCandidatePatch));

				var findRando = AccessTools.Method(typeof(PeopleTracker), "FindRandoToMarry");
				if (findRando != null)
				{
					harmony.Patch(findRando, prefix: new HarmonyMethod(typeof(FindRandoToMarryPatch), nameof(FindRandoToMarryPatch.Prefix)));
				}

				AfterProhibitionFamilyPlugin.Log?.LogInfo("spouse-search patches applied findRando=" + (findRando != null) + " scoreCandidate=True findMatch=True");
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("spouse-search patch failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		[HarmonyPatch(typeof(PeopleTracker), "FindMatchAndLinkCouple")]
		private static class FindMatchAndLinkCouplePatch
		{
			[HarmonyPrefix]
			private static void Prefix()
			{
				if (!AfterProhibitionFamilyPlugin.OwnsSpouseSearchMigration() || !IsInteractiveSession())
				{
					_forceSameEthnicity = false;
					return;
				}

				_forceSameEthnicity = AfterProhibitionFamilyPlugin.EnableSpouseEthnicityPreference.Value
					&& Rng.NextDouble() < Math.Max(0d, Math.Min(1d, AfterProhibitionFamilyPlugin.SpouseEthnicityPreferenceChance.Value));
			}
		}

		[HarmonyPatch(typeof(PeopleTracker), "ScoreCandidate")]
		private static class ScoreCandidatePatch
		{
			[HarmonyPrefix]
			private static bool Prefix(Entity current, Entity other, ref float __result)
			{
				if (!AfterProhibitionFamilyPlugin.OwnsSpouseSearchMigration() || !IsInteractiveSession())
				{
					return true;
				}

				if (!RelationshipSafetyClassifier.IsValidSpouseCandidate(current, other, out string reason))
				{
					__result = 0f;
					return false;
				}

				PersonData currentPerson = current.data.person;
				PersonData otherPerson = other.data.person;
				float ageDiff = Math.Abs(currentPerson.born.Subtract(otherPerson.born).YearsFloat);
				float ageScore = Mathf.Clamp(10f - ageDiff, 0f, 10f);
				if (_forceSameEthnicity)
				{
					__result = currentPerson.eth == otherPerson.eth ? 3f * ageScore : 0f;
				}
				else
				{
					__result = (currentPerson.eth == otherPerson.eth ? 3f : 1f) * ageScore;
				}

				return false;
			}
		}

		private static class FindRandoToMarryPatch
		{
			internal static bool Prefix(PeopleTracker __instance, Entity peep, ref Entity __result)
			{
				if (!AfterProhibitionFamilyPlugin.OwnsSpouseSearchMigration() || !IsInteractiveSession())
				{
					return true;
				}

				try
				{
					if (__instance == null || peep?.data?.person == null)
					{
						return true;
					}

					PersonData person = peep.data.person;
					Gender goalGender = person.g == Gender.F ? Gender.M : Gender.F;
					SimTime now = global::Game.Game.ctx?.clock != null
						? global::Game.Game.ctx.clock.Now
						: SimTime.MIN_DATE;
					int minAge = Math.Max(0, AfterProhibitionFamilyPlugin.MarriageMinAge.Value);
					int maxAgeDiff = Math.Max(0, AfterProhibitionFamilyPlugin.MarriageMaxAgeDifference.Value);
					int rejectedFamily = 0;
					int rejectedDead = 0;
					int rejectedBusinessOwner = 0;
					List<(Entity candidate, float score)> candidates = new List<(Entity candidate, float score)>();

					foreach (Entity candidate in __instance.GetAllTrackedPeople())
					{
						PersonData candidatePerson = candidate?.data?.person;
						if (candidatePerson == null || candidatePerson.g != goalGender)
						{
							continue;
						}

						if (!candidatePerson.IsAlive)
						{
							rejectedDead++;
							continue;
						}

						if (candidatePerson.GetAge(now).YearsFloat < minAge)
						{
							continue;
						}

						float ageDiff = Math.Abs(candidatePerson.born.YearsFloat - person.born.YearsFloat);
						if (ageDiff > maxAgeDiff)
						{
							continue;
						}

						if (candidatePerson.business.IsValid)
						{
							rejectedBusinessOwner++;
							continue;
						}

						if (!RelationshipSafetyClassifier.IsValidSpouseCandidate(peep, candidate, out string safetyReason))
						{
							if (safetyReason.IndexOf("family", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								rejectedFamily++;
							}
							else if (string.Equals(safetyReason, "dead-person", StringComparison.Ordinal))
							{
								rejectedDead++;
							}
							continue;
						}

						RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
						RelationshipList candidateRels = rels?.GetListOrNull(candidate.Id);
						if (candidateRels != null && candidateRels.HasSpouse())
						{
							continue;
						}

						RelationshipList currentRels = rels?.GetListOrNull(peep.Id);
						if (currentRels != null && currentRels.HasAny(candidate.Id))
						{
							continue;
						}

						candidates.Add((candidate, (float)maxAgeDiff - ageDiff + 1f));
					}

					if (candidates.Count == 0)
					{
						LogSpouseSample("find-rando", peep, null, allowed: false, "no-candidates", selected: false, candidates: 0, rejectedFamily, rejectedDead, rejectedBusinessOwner);
						return true;
					}

					float totalScore = candidates.Sum(candidate => candidate.score);
					if (totalScore <= 0f)
					{
						LogSpouseSample("find-rando", peep, null, allowed: false, "no-positive-score", selected: false, candidates: candidates.Count, rejectedFamily, rejectedDead, rejectedBusinessOwner);
						return true;
					}

					float roll = (float)Rng.NextDouble() * totalScore;
					float running = 0f;
					foreach ((Entity candidate, float score) item in candidates)
					{
						running += item.score;
						if (roll <= running)
						{
							__result = item.candidate;
							LogSpouseSample("find-rando", peep, item.candidate, allowed: true, "selected", selected: true, candidates: candidates.Count, rejectedFamily, rejectedDead, rejectedBusinessOwner);
							return false;
						}
					}

					__result = candidates[0].candidate;
					LogSpouseSample("find-rando", peep, __result, allowed: true, "selected-fallback", selected: true, candidates: candidates.Count, rejectedFamily, rejectedDead, rejectedBusinessOwner);
					return false;
				}
				catch (Exception ex)
				{
					AfterProhibitionFamilyPlugin.Log?.LogWarning("spouse-search failed source=find-rando peep=" + IdText(peep?.Id ?? EntityID.INVALID) + " error=" + ex.GetType().Name + ":" + ex.Message);
					return true;
				}
			}
		}

		private static void LogSpouseSample(string source, Entity current, Entity other, bool allowed, string reason, bool selected, int candidates, int rejectedFamily, int rejectedDead, int rejectedBusinessOwner)
		{
			if (!AfterProhibitionFamilyPlugin.EnableSpouseSearchSampleLog.Value)
			{
				return;
			}

			int day = global::Game.Game.ctx?.clock?.Now.days ?? -1;
			if (day != _lastSampleDay)
			{
				_lastSampleDay = day;
				_samplesLoggedToday = 0;
			}

			if (_samplesLoggedToday >= Math.Max(0, AfterProhibitionFamilyPlugin.SpouseSearchSampleLimit.Value))
			{
				return;
			}

			_samplesLoggedToday++;
			AfterProhibitionFamilyPlugin.Log?.LogInfo(
				"spouse-search source=" + source +
				" peep=" + IdText(current?.Id ?? EntityID.INVALID) +
				" candidate=" + IdText(other?.Id ?? EntityID.INVALID) +
				" allowed=" + allowed +
				" selected=" + selected +
				" candidates=" + candidates +
				" rejectedFamily=" + rejectedFamily +
				" rejectedDead=" + rejectedDead +
				" rejectedBusinessOwner=" + rejectedBusinessOwner +
				" reason=" + reason);
		}

		private static string IdText(EntityID id)
		{
			return id.IsValid ? id.id.ToString() : "invalid";
		}

		private static bool IsInteractiveSession()
		{
			try
			{
				return global::Game.Game.ctx?.IsInteractive ?? false;
			}
			catch
			{
				return false;
			}
		}
	}
}

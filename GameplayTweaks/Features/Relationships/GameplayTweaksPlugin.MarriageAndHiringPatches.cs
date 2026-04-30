using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	[HarmonyPatch(typeof(PeopleTracker), "FindMatchAndLinkCouple")]
	private static class SpouseEthnicityLinkPatch
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			if (!EnableSpouseEthnicity.Value)
			{
				ForceSameEthnicity = false;
			}
			else
			{
				ForceSameEthnicity = SharedRng.NextDouble() < (double)SpouseEthnicityChance.Value;
			}
		}
	}

	[HarmonyPatch(typeof(PeopleTracker), "ScoreCandidate")]
	private static class SpouseEthnicityCandidatePatch
	{
		[HarmonyPrefix]
		private static bool Prefix(Entity current, Entity other, ref float __result)
		{
			if (!EnableSpouseEthnicity.Value || !GameplayTweaksPlugin.AreRuntimePromptsReady())
			{
				return true;
			}
			PersonData person = current.data.person;
			PersonData person2 = other.data.person;
			SimTimeSpan val = person.born.Subtract(person2.born);
			float num = Math.Abs(val.YearsFloat);
			float num2 = Mathf.Clamp(10f - num, 0f, 10f);
			if (ForceSameEthnicity)
			{
				__result = ((person.eth == person2.eth) ? (3f * num2) : 0f);
			}
			else
			{
				__result = ((person.eth == person2.eth) ? 3f : 1f) * num2;
			}
			return false;
		}
	}

	private static class FindRandoToMarryPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(PeopleTracker), "FindRandoToMarry", (Type[])null, (Type[])null);
				if (!(methodInfo == null))
				{
					harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(FindRandoToMarryPatch), "Prefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] FindRandoToMarryPatch failed: {arg}");
			}
		}

		private static bool Prefix(PeopleTracker __instance, Entity peep, ref Entity __result)
		{
			try
			{
				if (__instance == null || peep?.data?.person == null || !GameplayTweaksPlugin.AreRuntimePromptsReady())
				{
					return true;
				}

				RelationshipTracker rels = G.GetRels();
				PersonData person = peep.data.person;
				Gender val = (Gender)(((int)person.g == 2) ? 1 : 2);
				SimTime now = G.GetNow();
				int value = MarriageMinAge.Value;
				int value2 = MarriageMaxAgeDiff.Value;
				List<(Entity, float)> list = new List<(Entity, float)>();
				foreach (Entity allTrackedPerson in __instance.GetAllTrackedPeople())
				{
					PersonData person2 = allTrackedPerson.data.person;
					if (person2.g != val || !person2.IsAlive)
					{
						continue;
					}
					SimTimeSpan age = person2.GetAge(now);
					if (age.YearsFloat < (float)value)
					{
						continue;
					}
					float num = Math.Abs(person2.born.YearsFloat - person.born.YearsFloat);
					if (num > (float)value2 || person2.business.IsValid)
					{
						continue;
					}
					RelationshipList val2 = ((rels != null) ? rels.GetListOrNull(allTrackedPerson.Id) : null);
					if (val2 == null || !val2.HasSpouse())
					{
						RelationshipList val3 = ((rels != null) ? rels.GetListOrNull(peep.Id) : null);
						if (val3 == null || !val3.HasAny(allTrackedPerson.Id))
						{
							list.Add((allTrackedPerson, (float)value2 - num + 1f));
						}
					}
				}
				if (list.Count == 0)
				{
					return true;
				}
				float num2 = list.Sum<(Entity, float)>(((Entity entity, float score) c) => c.score);
				if (num2 <= 0f)
				{
					return true;
				}
				float num3 = (float)SharedRng.NextDouble() * num2;
				float num4 = 0f;
				foreach (var item3 in list)
				{
					Entity item = item3.Item1;
					float item2 = item3.Item2;
					num4 += item2;
					if (num3 <= num4)
					{
						__result = item;
						return false;
					}
				}
				__result = list[0].Item1;
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	private static class HireableAgePatch
	{
		private delegate bool IsEligibleDelegate(SimTime now, Entity person);

		private static IsEligibleDelegate _originalTrampoline;

		private static Detour _detour;

		private static int _lastBusinessHireRuleLogDay = -1;

		public static void ApplyManualDetour()
		{
			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(PlayerSocial), "IsEligibleCrewMember", (Type[])null, (Type[])null);
				MethodInfo methodInfo2 = AccessTools.Method(typeof(HireableAgePatch), "Replacement", (Type[])null, (Type[])null);
				_detour = new Detour((MethodBase)methodInfo, (MethodBase)methodInfo2);
				_originalTrampoline = _detour.GenerateTrampoline<IsEligibleDelegate>();
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] HireableAgePatch failed: {arg}");
			}
		}

		private static bool Replacement(SimTime now, Entity person)
		{
			if (!EnableHireableAge.Value && _originalTrampoline != null)
			{
				return _originalTrampoline(now, person);
			}
			try
			{
				PersonData person2 = person.data.person;
				if (person2.IsAlive)
				{
					SimTimeSpan age = person2.GetAge(now);
					bool allowBusinessAssigned = GameplayTweaksPlugin.ShouldAllowBusinessAssignedCandidates();
					if (_lastBusinessHireRuleLogDay != now.days)
					{
						_lastBusinessHireRuleLogDay = now.days;
						VerificationLog("Hiring", $"businessAssignedAllowed={allowBusinessAssigned}");
					}
					bool hasBlockedAssignment = !person2.business.IsNotValid || !person2.resassigned.IsNotValid;
					float minHireAge = Mathf.Max(18f, (HireableMinAge != null) ? HireableMinAge.Value : 18f);
					if (age.YearsFloat >= minHireAge
						&& (allowBusinessAssigned || !hasBlockedAssignment)
						&& G.GetPoliticianData(person.Id) == null
						&& !PotentialBizOwnerEligibilityPatch.IsGangAffiliatedCandidate(person))
					{
						return person.data.agent.pid.id == 0;
					}
				}
				return false;
			}
			catch
			{
				return false;
			}
		}
	}

	internal static class PotentialBizOwnerEligibilityPatch
	{
		private static readonly HashSet<string> _loggedOwnerBlocks = new HashSet<string>(StringComparer.Ordinal);

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(PlayerSocial), "IsEligiblePotentialBizOwner", null, null);
				if (methodInfo != null)
				{
					harmony.Patch(methodInfo, postfix: new HarmonyMethod(typeof(PotentialBizOwnerEligibilityPatch), nameof(Postfix)));
				}
				MethodInfo findAnyOwner = AccessTools.Method(typeof(PlayerSocial), "FindAnyoneToOwnBiz", null, null);
				if (findAnyOwner != null)
				{
					harmony.Patch(findAnyOwner, postfix: new HarmonyMethod(typeof(PotentialBizOwnerEligibilityPatch), nameof(ValidateOwnerSelectionPostfix)));
				}
				MethodInfo findConnectionOwner = AccessTools.Method(typeof(PlayerSocial), "FindConnectionToOwnBiz", new[] { typeof(Entity), typeof(bool) }, null);
				if (findConnectionOwner != null)
				{
					harmony.Patch(findConnectionOwner, postfix: new HarmonyMethod(typeof(PotentialBizOwnerEligibilityPatch), nameof(ValidateOwnerSelectionPostfix)));
				}
				MethodInfo canPersonOwnBusiness = AccessTools.Method(typeof(BusinessTracker), "CanPersonOwnBusiness", new[] { typeof(Entity), typeof(SimTime) }, null);
				if (canPersonOwnBusiness != null)
				{
					harmony.Patch(canPersonOwnBusiness, postfix: new HarmonyMethod(typeof(PotentialBizOwnerEligibilityPatch), nameof(CanPersonOwnBusinessPostfix)));
				}
				MethodInfo assignRealOwner = AccessTools.Method(typeof(BizComponent), "AssignRealOwner", new[] { typeof(Entity) }, null);
				if (assignRealOwner != null)
				{
					harmony.Patch(assignRealOwner, prefix: new HarmonyMethod(typeof(PotentialBizOwnerEligibilityPatch), nameof(AssignRealOwnerPrefix)));
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] PotentialBizOwnerEligibilityPatch failed: " + ex.Message);
			}
		}

		[HarmonyPostfix]
		private static void Postfix(SimTime now, Entity person, ref bool __result)
		{
			if (!__result || person?.Id.IsValid != true || person.data?.agent == null)
			{
				return;
			}

			__result = IsSafeBusinessOwnerCandidate(now, person);
		}

		[HarmonyPostfix]
		private static void ValidateOwnerSelectionPostfix(ref EntityID __result)
		{
			if (__result.IsNotValid)
			{
				return;
			}

			SimTime now = G.GetNow();
			Entity selected = __result.FindEntity();
			if (IsSafeBusinessOwnerCandidate(now, selected))
			{
				return;
			}

			IEnumerable<Entity> allPeople = global::Game.Game.ctx?.entityman?.GetCachedEntitiesPersonsUnsafe();
			if (allPeople != null)
			{
				Entity replacement = allPeople
					.Where(candidate => IsSafeBusinessOwnerCandidate(now, candidate))
					.OrderBy(candidate => candidate.Id.id)
					.FirstOrDefault();
				if (replacement != null)
				{
					VerificationLog("Hiring", $"biz-owner-selection-normalized old={__result.id} new={replacement.Id.id}");
					__result = replacement.Id;
					return;
				}
			}

			VerificationLog("Hiring", $"biz-owner-selection-cleared old={__result.id}");
			__result = EntityID.INVALID;
		}

		internal static bool IsSafeBusinessOwnerCandidate(SimTime now, Entity person)
		{
			if (person?.Id.IsValid != true || person.data?.person == null || person.data?.agent == null)
			{
				return false;
			}

			PersonData personData = person.data.person;
			if (!personData.IsAlive || personData.GetAge(now).YearsFloat < 35f)
			{
				return false;
			}
			if (!personData.business.IsNotValid || !personData.resassigned.IsNotValid)
			{
				return false;
			}
			if (person.data.agent.pid.id != 0)
			{
				return false;
			}
			if (G.GetPoliticianData(person.Id) != null)
			{
				return false;
			}
			if (global::Game.Game.ctx?.players?.Human?.gambling?.IsGamblingSomewhere(person) == true)
			{
				return false;
			}
			return !IsGangAffiliatedCandidate(person);
		}

		[HarmonyPostfix]
		private static void CanPersonOwnBusinessPostfix(Entity person, SimTime now, ref bool __result)
		{
			if (!__result || IsSafeAutoBusinessOwnerCandidate(now, person))
			{
				return;
			}

			string reason = GetBusinessOwnerBlockReason(now, person, 18f, 60f, allowHumanPlayer: false, allowedOwnedBusiness: EntityID.INVALID);
			LogOwnerBlock("biz-owner-auto-candidate-blocked", person, null, reason);
			__result = false;
		}

		[HarmonyPrefix]
		private static bool AssignRealOwnerPrefix(BizComponent __instance, Entity owner)
		{
			string reason = GetBusinessOwnerBlockReason(G.GetNow(), owner, 18f, 60f, allowHumanPlayer: true, allowedOwnedBusiness: EntityID.INVALID);
			if (string.IsNullOrEmpty(reason))
			{
				return true;
			}
			if (owner?.data?.person == null || __instance == null)
			{
				return true;
			}

			EntityID blockedBusiness = owner.data.person.business;
			if (blockedBusiness.IsValid)
			{
				owner.data.person.business = EntityID.INVALID;
			}
			__instance.AssignFakeOwner(owner.data.person.eth);
			LogOwnerBlock("biz-owner-invalid-replaced", owner, __instance.BuildingID, reason + (blockedBusiness.IsValid ? $":cleared={blockedBusiness.id}" : string.Empty) + ":replacement=fake");
			return false;
		}

		internal static bool IsSafeAutoBusinessOwnerCandidate(SimTime now, Entity person)
		{
			return string.IsNullOrEmpty(GetBusinessOwnerBlockReason(now, person, 18f, 60f, allowHumanPlayer: false, allowedOwnedBusiness: EntityID.INVALID));
		}

		private static string GetBusinessOwnerBlockReason(SimTime now, Entity person, float minimumAge, float maximumAge, bool allowHumanPlayer, EntityID allowedOwnedBusiness)
		{
			if (person?.Id.IsValid != true || person.data?.person == null || person.data?.agent == null)
			{
				return "missing-person";
			}

			PersonData personData = person.data.person;
			if (!personData.IsAlive)
			{
				return "dead";
			}
			float age = personData.GetAge(now).YearsFloat;
			if (age < minimumAge || age > maximumAge)
			{
				return "age";
			}
			if (personData.business.IsValid && personData.business != allowedOwnedBusiness)
			{
				return "already-business";
			}
			if (!personData.resassigned.IsNotValid)
			{
				return "resassigned";
			}
			PlayerID pid = person.data.agent.pid;
			if (pid.IsHumanPlayer && allowHumanPlayer)
			{
				return string.Empty;
			}
			if (pid.id != 0)
			{
				return pid.IsAIPlayer ? "ai-player" : "agent-pid";
			}
			if (G.GetPoliticianData(person.Id) != null)
			{
				return "politician";
			}
			if (global::Game.Game.ctx?.players?.Human?.gambling?.IsGamblingSomewhere(person) == true)
			{
				return "gambling";
			}
			string affiliationReason = GetGangAffiliationBlockReason(person);
			if (!string.IsNullOrEmpty(affiliationReason))
			{
				return affiliationReason;
			}
			return string.Empty;
		}

		private static void LogOwnerBlock(string eventName, Entity person, EntityID? buildingId, string reason)
		{
			ulong peepId = person?.Id.id ?? 0uL;
			string building = buildingId.HasValue && buildingId.Value.IsValid ? buildingId.Value.id.ToString() : "none";
			string key = $"{G.GetNow().days}:{eventName}:{peepId}:{building}:{reason}";
			if (_loggedOwnerBlocks.Add(key))
			{
				VerificationLog("Hiring", $"{eventName} peep={peepId} building={building} reason={reason}");
			}
		}

		internal static bool IsGangAffiliatedCandidate(Entity person)
		{
			return !string.IsNullOrEmpty(GetGangAffiliationBlockReason(person));
		}

		internal static string GetGangAffiliationBlockReason(Entity person)
		{
			if (person == null || !person.Id.IsValid)
			{
				return "missing-person";
			}

			foreach (PlayerInfo player in G.GetAllPlayers())
			{
				if (player == null)
				{
					continue;
				}

				if (player.social != null && player.social.PlayerPeepId == person.Id)
				{
					if (player.IsJustGang)
					{
						return "gang-leader";
					}
					if (player.IsJustGoon)
					{
						return "troublemaker-leader";
					}
					if (player.IsCopOrFed)
					{
						return "law-leader";
					}
					if (!player.IsHuman)
					{
						return "player-leader";
					}
				}

				PlayerCrew crew = player.crew;
				if (crew != null && crew.GetCrewForPeep(person.Id).IsValid)
				{
					if (player.IsJustGang)
					{
						return "gang-crew";
					}
					if (player.IsJustGoon)
					{
						return "troublemaker-crew";
					}
					if (player.IsCopOrFed)
					{
						return "law-crew";
					}
					if (!player.IsHuman)
					{
						return "player-crew";
					}
				}
			}

			return string.Empty;
		}
	}
}
}

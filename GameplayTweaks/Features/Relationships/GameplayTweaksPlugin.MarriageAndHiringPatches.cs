using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Setup;
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
			if (IsForbiddenSpouseCandidate(current, other, G.GetRels(), out _))
			{
				__result = 0f;
				return false;
			}

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

	private static bool IsForbiddenSpouseCandidate(Entity current, Entity other, RelationshipTracker rels, out string reason)
	{
		reason = "ok";
		if (current?.data?.person == null || other?.data?.person == null)
		{
			reason = "missing-person-data";
			return true;
		}

		if (current.Id == other.Id)
		{
			reason = "same-person";
			return true;
		}

		PersonData currentPerson = current.data.person;
		PersonData otherPerson = other.data.person;
		if (!currentPerson.IsAlive || !otherPerson.IsAlive)
		{
			reason = "dead-person";
			return true;
		}

		if (currentPerson.famId >= 0 && currentPerson.famId == otherPerson.famId)
		{
			reason = "same-family-tree";
			return true;
		}

		Relationship currentToOther = rels?.GetOrNull(current.Id, other.Id);
		if (currentToOther != null && currentToOther.IsAnyFamily)
		{
			reason = $"family-current-to-other:{currentToOther.type}";
			return true;
		}

		Relationship otherToCurrent = rels?.GetOrNull(other.Id, current.Id);
		if (otherToCurrent != null && otherToCurrent.IsAnyFamily)
		{
			reason = $"family-other-to-current:{otherToCurrent.type}";
			return true;
		}

		return false;
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
				if (__instance == null || peep?.data?.person == null)
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
					PersonData person2 = allTrackedPerson?.data?.person;
					if (person2 == null)
					{
						continue;
					}

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
					if (IsForbiddenSpouseCandidate(peep, allTrackedPerson, rels, out _))
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

	private static class HumanStartupParentFallbackPatch
	{
		private const int MaxStartupRootCoupleAttempts = 8;

		private static readonly FieldInfo PeopleCreatorField = AccessTools.Field(typeof(PeopleTracker), "_creator");
		private static readonly MethodInfo GenerateRootCoupleMethod = AccessTools.Method(typeof(PersonCreator), "GenerateRootCouple", new[] { typeof(Label) });

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type helperType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayersHumanHelper");
				MethodInfo methodInfo = helperType == null
					? null
					: AccessTools.Method(helperType, "PickAndSetValidParents", new[] { typeof(List<Entity>), typeof(bool) });
				if (methodInfo == null)
				{
					Debug.LogWarning("[GameplayTweaks] Human startup parent fallback patch skipped: method not found");
					return;
				}

				harmony.Patch(methodInfo, postfix: new HarmonyMethod(typeof(HumanStartupParentFallbackPatch), nameof(Postfix)));
				Debug.Log("[GameplayTweaks] Human startup parent fallback patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanStartupParentFallbackPatch failed: " + ex.Message);
			}
		}

		private static void Postfix(List<Entity> eligible, bool anyeth)
		{
			try
			{
				if (eligible == null || eligible.Count > 0)
				{
					return;
				}

				if (TryFindOrCreateStartupParent(out Entity parent, out Entity spouse, out string reason))
				{
					eligible.Add(parent);
					VerificationLog("NewGameFamily", $"startup-parent-fallback parent={parent.Id.id} spouse={spouse?.Id.id ?? 0UL} anyeth={anyeth} reason={reason}");
				}
				else
				{
					VerificationLog("NewGameFamily", $"startup-parent-fallback-failed anyeth={anyeth} reason={reason}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Human startup parent fallback failed: " + ex.Message);
			}
		}

		private static bool TryFindOrCreateStartupParent(out Entity parent, out Entity spouse, out string reason)
		{
			parent = null;
			spouse = null;
			reason = "unknown";

			PeopleTracker peopleGen = G.GetPeopleGen();
			RelationshipTracker rels = G.GetRels();
			if (peopleGen == null || rels == null)
			{
				reason = "missing-trackers";
				return false;
			}

			SimTime now = G.GetNow();
			List<Entity> trackedPeople = peopleGen.GetAllTrackedPeople()
				.Where(entity => entity?.data?.person != null && entity.data.person.IsAlive)
				.ToList();

			if (trackedPeople.Count == 0 && TryGenerateStartupParentCouple(peopleGen, rels, now, out parent, out spouse, out string generatedReason))
			{
				reason = generatedReason;
				return true;
			}

			if (TryFindMarriedMale(trackedPeople, rels, now, strictAge: true, out parent, out spouse))
			{
				reason = "existing-married-strict-age";
				return true;
			}

			if (TryCreateMarriedMale(trackedPeople, peopleGen, rels, now, strictAge: true, out parent, out spouse))
			{
				reason = "created-marriage-strict-age";
				return true;
			}

			if (TryFindMarriedMale(trackedPeople, rels, now, strictAge: false, out parent, out spouse))
			{
				reason = "existing-married-relaxed-age";
				return true;
			}

			if (TryCreateMarriedMale(trackedPeople, peopleGen, rels, now, strictAge: false, out parent, out spouse))
			{
				reason = "created-marriage-relaxed-age";
				return true;
			}

			reason = $"no-candidates tracked={trackedPeople.Count}";
			return false;
		}

		private static bool TryGenerateStartupParentCouple(PeopleTracker peopleGen, RelationshipTracker rels, SimTime now, out Entity parent, out Entity spouse, out string reason)
		{
			parent = null;
			spouse = null;
			reason = "unknown";
			if (peopleGen == null)
			{
				reason = "missing-peoplegen";
				return false;
			}

			object creator = PeopleCreatorField?.GetValue(peopleGen);
			if (creator == null || GenerateRootCoupleMethod == null)
			{
				reason = "missing-person-creator-reflection";
				return false;
			}

			Label ethnicity = ResolveStartupEthnicity();
			int beforePeople = peopleGen.GetAllTrackedPeople().Count();
			int beforeFamilies = peopleGen.data?.famTrees?.Count ?? 0;
			Exception lastException = null;
			for (int i = 0; i < MaxStartupRootCoupleAttempts; i++)
			{
				try
				{
					GenerateRootCoupleMethod.Invoke(creator, new object[] { ethnicity });
					TryPlaceGeneratedFamilies(peopleGen);
				}
				catch (Exception ex)
				{
					lastException = ex.InnerException ?? ex;
					break;
				}

				List<Entity> generatedPeople = peopleGen.GetAllTrackedPeople()
					.Where(entity => entity?.data?.person != null && entity.data.person.IsAlive)
					.ToList();
				if (TryCreateStartupParentCoupleFromRootFamilies(generatedPeople, peopleGen, rels, now, out parent, out spouse, out string coupleReason))
				{
					reason = $"generated-adult-parent-couple ethnicity={ethnicity} attempts={i + 1} beforePeople={beforePeople} beforeFamilies={beforeFamilies} {coupleReason}";
					return true;
				}
			}

			int afterPeople = peopleGen.GetAllTrackedPeople().Count();
			reason = lastException == null
				? $"adult-parent-generation-no-couple ethnicity={ethnicity} beforePeople={beforePeople} afterPeople={afterPeople} beforeFamilies={beforeFamilies}"
				: $"adult-parent-generation-error ethnicity={ethnicity} error={lastException.GetType().Name}:{lastException.Message}";
			return false;
		}

		private static bool TryCreateStartupParentCoupleFromRootFamilies(List<Entity> people, PeopleTracker peopleGen, RelationshipTracker rels, SimTime now, out Entity parent, out Entity spouse, out string reason)
		{
			parent = null;
			spouse = null;
			reason = "unknown";

			List<(Entity father, Entity mother)> rootCouples = FindRootCouples(people, rels).ToList();
			if (rootCouples.Count == 0)
			{
				reason = "no-root-couples";
				return false;
			}

			(Entity father, Entity mother) paternalRoots = rootCouples[0];
			(Entity father, Entity mother) maternalRoots = rootCouples.Count > 1 ? rootCouples[1] : rootCouples[0];
			Entity createdFather = CreateAdultChildForStartup(peopleGen, paternalRoots.father, paternalRoots.mother, Gender.M, now, ageYears: 45);
			Entity createdMother = CreateAdultChildForStartup(peopleGen, maternalRoots.father, maternalRoots.mother, Gender.F, now, ageYears: 43);
			if (createdFather?.data?.person == null || createdMother?.data?.person == null)
			{
				reason = "adult-child-create-failed";
				return false;
			}

			peopleGen.ForceMarry(createdFather, createdMother);
			Entity confirmedSpouse = rels.GetListOrNull(createdFather.Id)?.GetSpouse();
			if (confirmedSpouse?.data?.person == null || confirmedSpouse.Id != createdMother.Id)
			{
				reason = "adult-parent-marriage-failed";
				return false;
			}

			if (!HasLivingParents(createdFather, rels) || !HasLivingParents(createdMother, rels))
			{
				reason = $"adult-parent-grandparent-links-missing fatherLinks={HasLivingParents(createdFather, rels)} motherLinks={HasLivingParents(createdMother, rels)}";
				return false;
			}

			parent = createdFather;
			spouse = confirmedSpouse;
			reason = $"parent={createdFather.Id.id} spouse={confirmedSpouse.Id.id} rootCouples={rootCouples.Count}";
			return true;
		}

		private static IEnumerable<(Entity father, Entity mother)> FindRootCouples(IEnumerable<Entity> people, RelationshipTracker rels)
		{
			HashSet<EntityID> seen = new HashSet<EntityID>();
			foreach (Entity person in people)
			{
				if (person?.data?.person == null || !person.data.person.IsAlive || person.data.person.g != Gender.M || seen.Contains(person.Id))
				{
					continue;
				}

				Entity spouse = rels.GetListOrNull(person.Id)?.GetSpouse();
				if (spouse?.data?.person == null || !spouse.data.person.IsAlive || spouse.data.person.g != Gender.F || seen.Contains(spouse.Id))
				{
					continue;
				}

				seen.Add(person.Id);
				seen.Add(spouse.Id);
				yield return (person, spouse);
			}
		}

		private static Entity CreateAdultChildForStartup(PeopleTracker peopleGen, Entity father, Entity mother, Gender gender, SimTime now, int ageYears)
		{
			if (peopleGen == null || father?.data?.person == null || mother?.data?.person == null)
			{
				return null;
			}

			PersonData fatherData = father.data.person;
			PeepCreationDetails deets = new PeepCreationDetails(
				fatherData.eth,
				GetFallbackFirstName(fatherData.eth, gender),
				fatherData.last,
				gender,
				fatherData.s);
			return peopleGen.ManuallyMakePerson(now.IncrementYears(-ageYears), deets, fatherData.famId, father, mother);
		}

		private static string GetFallbackFirstName(Label ethnicity, Gender gender)
		{
			try
			{
				EthnicityDef def = global::Game.Game.serv.globals.settings.ethnicities.FindEthnicityDef(ethnicity);
				return def.loc.GetRandomFirstName(global::Game.Game.ctx.simman.peoplegen.data.rng, gender);
			}
			catch
			{
				return gender == Gender.M ? "John" : "Jane";
			}
		}

		private static bool HasLivingParents(Entity person, RelationshipTracker rels)
		{
			RelationshipList relationships = rels.GetListOrNull(person.Id);
			Entity father = relationships?.GetFather();
			Entity mother = relationships?.GetMother();
			return father?.data?.person != null
				&& father.data.person.IsAlive
				&& mother?.data?.person != null
				&& mother.data.person.IsAlive;
		}

		private static void TryPlaceGeneratedFamilies(PeopleTracker peopleGen)
		{
			try
			{
				peopleGen.RunNewGameFamilyPlacement();
			}
			catch (Exception ex)
			{
				VerificationLog("NewGameFamily", $"startup-root-family-placement-warning error={ex.GetType().Name}:{ex.Message}");
			}
		}

		private static Label ResolveStartupEthnicity()
		{
			try
			{
				Label playerEthnicity = global::Game.Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
				if (playerEthnicity.IsSet)
				{
					return playerEthnicity;
				}
			}
			catch
			{
			}

			try
			{
				List<Label> ethnicities = global::Game.Game.ctx.session.mapconfig.GetEthnicitiesUniqueSorted();
				if (ethnicities != null && ethnicities.Count > 0 && ethnicities[0].IsSet)
				{
					return ethnicities[0];
				}
			}
			catch
			{
			}

			return EthnicitySettings.DEFAULT_ETHNICITY;
		}

		private static bool TryFindMarriedMale(IEnumerable<Entity> people, RelationshipTracker rels, SimTime now, bool strictAge, out Entity parent, out Entity spouse)
		{
			parent = null;
			spouse = null;
			foreach (Entity person in people)
			{
				if (!IsCandidateMaleParent(person, now, strictAge))
				{
					continue;
				}

				Entity existingSpouse = rels.GetListOrNull(person.Id)?.GetSpouse();
				if (existingSpouse?.data?.person == null || !existingSpouse.data.person.IsAlive)
				{
					continue;
				}

				parent = person;
				spouse = existingSpouse;
				return true;
			}

			return false;
		}

		private static bool TryCreateMarriedMale(List<Entity> people, PeopleTracker peopleGen, RelationshipTracker rels, SimTime now, bool strictAge, out Entity parent, out Entity spouse)
		{
			parent = null;
			spouse = null;
			foreach (Entity person in people)
			{
				if (!IsCandidateMaleParent(person, now, strictAge) || (rels.GetListOrNull(person.Id)?.HasSpouse() ?? false))
				{
					continue;
				}

				Entity candidate = peopleGen.FindRandoToMarry(person) ?? FindManualSpouseCandidate(people, rels, person, now, strictAge);
				if (candidate?.data?.person == null)
				{
					continue;
				}

				peopleGen.ForceMarry(person, candidate);
				Entity confirmedSpouse = rels.GetListOrNull(person.Id)?.GetSpouse();
				if (confirmedSpouse != null)
				{
					parent = person;
					spouse = confirmedSpouse;
					return true;
				}
			}

			return false;
		}

		private static Entity FindManualSpouseCandidate(IEnumerable<Entity> people, RelationshipTracker rels, Entity parent, SimTime now, bool strictAge)
		{
			foreach (Entity candidate in people)
			{
				if (candidate?.data?.person == null || candidate.Id == parent.Id)
				{
					continue;
				}

				PersonData person = candidate.data.person;
				if (!person.IsAlive || person.g != Gender.F || (rels.GetListOrNull(candidate.Id)?.HasSpouse() ?? false))
				{
					continue;
				}

				int years = person.GetAge(now).YearsInt;
				if (strictAge && (years < 35 || years > 65))
				{
					continue;
				}

				if (!strictAge && years < 18)
				{
					continue;
				}

				if (IsForbiddenSpouseCandidate(parent, candidate, rels, out _))
				{
					continue;
				}

				return candidate;
			}

			return null;
		}

		private static bool IsCandidateMaleParent(Entity candidate, SimTime now, bool strictAge)
		{
			if (candidate?.data?.person == null)
			{
				return false;
			}

			PersonData person = candidate.data.person;
			if (!person.IsAlive || person.g != Gender.M)
			{
				return false;
			}

			int years = person.GetAge(now).YearsInt;
			return strictAge ? years >= 35 && years <= 65 : years >= 18;
		}
	}

	private static class HumanStartupSafehouseReplacementPatch
	{
		private static readonly MethodInfo CreateBusinessUnattachedMethod = AccessTools.Method(typeof(BusinessTracker), "CreateBusinessUnattached", new[] { typeof(EntityConfig) });
		private static readonly MethodInfo AttachBusinessToBuildingMethod = AccessTools.Method(typeof(BusinessTracker), "AttachBusinessToBuilding", new[] { typeof(Entity), typeof(Entity) });

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type createPlayersType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayers");
				MethodInfo methodInfo = createPlayersType == null
					? null
					: AccessTools.Method(createPlayersType, "PutSafehouseInBusiness", new[] { typeof(PlayerStartData), typeof(EntityID), typeof(Node), typeof(Entity) });
				if (methodInfo == null)
				{
					Debug.LogWarning("[GameplayTweaks] Human startup safehouse replacement patch skipped: method not found");
					return;
				}

				harmony.Patch(methodInfo, prefix: new HarmonyMethod(typeof(HumanStartupSafehouseReplacementPatch), nameof(Prefix)));
				Debug.Log("[GameplayTweaks] Human startup safehouse replacement patch applied");

				MethodInfo createSafehouseOrBusiness = createPlayersType == null
					? null
					: AccessTools.Method(createPlayersType, "CreateSafehouseOrBusiness", new[] { typeof(PlayerInfo), typeof(PlayerStartData), typeof(Entity) });
				if (createSafehouseOrBusiness != null)
				{
					harmony.Patch(createSafehouseOrBusiness, postfix: new HarmonyMethod(typeof(HumanStartupSafehouseReplacementPatch), nameof(CreateSafehouseOrBusinessPostfix)));
					Debug.Log("[GameplayTweaks] Human startup safehouse ownership reconcile patch applied");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanStartupSafehouseReplacementPatch failed: " + ex.Message);
			}
		}

		private static bool Prefix(PlayerStartData start, EntityID bid, Node node, Entity newOwner, ref Entity __result)
		{
			try
			{
				if (start?.frontBiz == null || !start.InstallSafeHouseInBusiness || bid.IsNotValid)
				{
					return true;
				}

				BuildingAndBusinessData data = BuildingUtil.FindDataForBuilding(bid);
				Entity building = data.building ?? bid.FindEntity();
				if (building?.components?.building == null)
				{
					return true;
				}
				if ((building.data?.building?.controlled.Get().IsAnyPlayer ?? false) || building.components.building.IsSafehouse)
				{
					VerificationLog("NewGameFamily", $"startup-safehouse-replacement-skipped building={building.Id.id} reason=already-controlled-or-safehouse node={node?.id.ToString() ?? "null"}");
					return true;
				}

				if (data.biz?.components?.biz != null)
				{
					return true;
				}

				BusinessTracker businesses = global::Game.Game.ctx?.simman?.businesses;
				if (businesses == null)
				{
					return true;
				}

				Entity business = CreateBusinessUnattachedMethod?.Invoke(businesses, new object[] { start.frontBiz }) as Entity;
				if (business?.components?.biz == null || AttachBusinessToBuildingMethod == null)
				{
					return true;
				}

				AttachBusinessToBuildingMethod.Invoke(businesses, new object[] { business, building });
				if (newOwner?.data?.person != null)
				{
					businesses.ForceAssignOwner(business, newOwner);
				}

				__result = building;
				VerificationLog("NewGameFamily", $"startup-safehouse-replaced-missing-business building={building.Id.id} business={business.Id.id} node={node?.id.ToString() ?? "null"} owner={newOwner?.Id.id ?? 0UL} front={start.frontBiz.Template}");
				return false;
			}
			catch (Exception ex)
			{
				VerificationLog("NewGameFamily", $"startup-safehouse-replacement-failed error={ex.GetType().Name}:{ex.Message}");
				return true;
			}
		}

		private static void CreateSafehouseOrBusinessPostfix(PlayerInfo player, PlayerStartData start, Entity bizOwner, Entity __result)
		{
			try
			{
				if (player == null || !player.IsHuman || player.territory == null || __result?.components?.building == null)
				{
					return;
				}
				if (!IsStartupBusinessSafehouseRepairContext(player, start, __result, out string contextReason))
				{
					VerificationLog("NewGameFamily", $"startup-safehouse-reconcile-skipped building={__result.Id.id} reason={contextReason}");
					return;
				}

				if (player.territory.Safehouse != __result.Id || !__result.components.building.IsSafehouseOf(player.PID))
				{
					player.territory.SafehouseData.SetSafehouse(__result);
				}

				bool controlledRepaired = false;
				if ((__result.data?.building?.controlled.Get() ?? PlayerID.INVALID) != player.PID)
				{
					player.territory.ScopeOutBuilding(__result, procgen: true, setControlled: true);
					controlledRepaired = true;
				}

				BuildingAndBusinessData data = BuildingUtil.FindDataForBuilding(__result.Id);
				Entity business = data.biz;
				bool ownerRepaired = false;
				if (business?.components?.biz != null
					&& bizOwner?.data?.person != null
					&& business.components.biz.OwnerID != bizOwner.Id)
				{
					BusinessTracker businesses = global::Game.Game.ctx?.simman?.businesses;
					businesses?.ForceAssignOwner(business, bizOwner);
					ownerRepaired = business.components.biz.OwnerID == bizOwner.Id;
				}

				EnsureHumanSafehouseTerritoryColorOwner("startup-safehouse-reconcile", refreshColors: false);
				DirtyCashEconomyCompatibilityPatch.RequestDeferredHumanTerritoryRefresh("startup-safehouse-reconcile", delayFrames: 5, passes: 6);
				VerificationLog(
					"NewGameFamily",
					$"startup-safehouse-reconciled building={__result.Id.id} business={business?.Id.id ?? 0UL} owner={bizOwner?.Id.id ?? 0UL} front={start?.frontBiz?.Template.ToString() ?? "none"} controlledRepaired={controlledRepaired} ownerRepaired={ownerRepaired}");
			}
			catch (Exception ex)
			{
				VerificationLog("NewGameFamily", $"startup-safehouse-reconcile-failed error={ex.GetType().Name}:{ex.Message}");
			}
		}

		private static bool IsStartupBusinessSafehouseRepairContext(PlayerInfo player, PlayerStartData start, Entity result, out string reason)
		{
			reason = "ok";
			if (player?.territory == null || result?.components?.building == null)
			{
				reason = "missing-player-or-building";
				return false;
			}
			if (start == null || !start.InstallSafeHouseInBusiness || start.frontBiz == null)
			{
				reason = "not-business-safehouse-start";
				return false;
			}

			EntityID existingSafehouseId = player.territory.Safehouse;
			if (existingSafehouseId.IsValid && existingSafehouseId != result.Id)
			{
				Entity existingSafehouse = existingSafehouseId.FindEntity();
				if (existingSafehouse?.components?.building != null && existingSafehouse.components.building.IsSafehouseOf(player.PID))
				{
					reason = "existing-safehouse-different-treat-as-relocation";
					return false;
				}
			}

			reason = "startup-business-safehouse-repair";
			return true;
		}
	}

	private static class StartupGeneratedPopulationPatch
	{
		private const int MaxRootFamilyAttempts = 8;

		private static readonly FieldInfo PeopleCreatorField = AccessTools.Field(typeof(PeopleTracker), "_creator");
		private static readonly MethodInfo GenerateRootCoupleMethod = AccessTools.Method(typeof(PersonCreator), "GenerateRootCouple", new[] { typeof(Label) });
		private static readonly MethodInfo CreatePersonMethod = AccessTools.Method(typeof(PersonCreator), "CreatePerson", new[] { typeof(bool), typeof(SimTime), typeof(PeepCreationDetails), typeof(int), typeof(bool) });
		private static readonly MethodInfo GenerateTraitsForEntityMethod = AccessTools.Method(typeof(PersonCreator), "GenerateTraitsForEntity");
		private static readonly HashSet<string> GeneratedStartupLastNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static int _generatedStartupCandidateCount;
		private static bool _loggedStandaloneCreationFailure;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo gangCandidateMethod = AccessTools.Method(typeof(PlayerSetup), "GetAndRemoveCandidatePeep");
				if (gangCandidateMethod != null)
				{
					harmony.Patch(gangCandidateMethod, postfix: new HarmonyMethod(typeof(StartupGeneratedPopulationPatch), nameof(GangCandidatePostfix)));
				}

				Type copsType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayersCops");
				MethodInfo copsAssignMethod = copsType == null
					? null
					: AccessTools.Method(copsType, "AssignEntitiesAsOfficers", new[] { typeof(Entity), typeof(List<Entity>), typeof(int) });
				if (copsAssignMethod != null)
				{
					harmony.Patch(copsAssignMethod, prefix: new HarmonyMethod(typeof(StartupGeneratedPopulationPatch), nameof(CopsAssignPrefix)));
				}

				Type fedsType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayersFeds");
				MethodInfo fedSetupMethod = fedsType == null
					? null
					: AccessTools.Method(fedsType, "SetUpFedPlayer", new[] { typeof(PlayerInfo) });
				if (fedSetupMethod != null)
				{
					harmony.Patch(fedSetupMethod, prefix: new HarmonyMethod(typeof(StartupGeneratedPopulationPatch), nameof(FedSetupPrefix)));
				}

				Debug.Log("[GameplayTweaks] Startup generated population patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] StartupGeneratedPopulationPatch failed: " + ex.Message);
			}
		}

		private static void GangCandidatePostfix(ref Entity __result, bool goons)
		{
			if (__result != null)
			{
				return;
			}

			Entity candidate = CreateUnassignedAdultCandidate(goons ? "gang-goon" : "gang");
			if (candidate != null)
			{
				__result = candidate;
				VerificationLog("NewGameFamily", $"startup-generated-gang-candidate peep={candidate.Id.id} goons={goons}");
			}
		}

		private static void CopsAssignPrefix(Entity station, List<Entity> candidates, int number)
		{
			if (candidates == null)
			{
				return;
			}

			while (candidates.Count < number)
			{
				Entity candidate = CreateUnassignedAdultCandidate("cop-officer");
				if (candidate == null)
				{
					VerificationLog("NewGameFamily", $"startup-generated-cop-candidate-failed station={station?.Id.id ?? 0UL} count={candidates.Count} needed={number}");
					break;
				}

				candidates.Add(candidate);
				VerificationLog("NewGameFamily", $"startup-generated-cop-candidate station={station?.Id.id ?? 0UL} peep={candidate.Id.id} needed={number}");
			}
		}

		private static void FedSetupPrefix()
		{
			try
			{
				SimTime now = G.GetNow();
				bool hasEligible = G.GetPeopleGen()?.GetAllTrackedPeople()
					.Any(person => IsEligibleStartupOfficer(now, person)) == true;
				if (hasEligible)
				{
					return;
				}

				Entity candidate = CreateUnassignedAdultCandidate("fed-officer");
				if (candidate != null)
				{
					VerificationLog("NewGameFamily", $"startup-generated-fed-candidate peep={candidate.Id.id}");
				}
			}
			catch (Exception ex)
			{
				VerificationLog("NewGameFamily", $"startup-generated-fed-candidate-failed error={ex.GetType().Name}:{ex.Message}");
			}
		}

		private static Entity CreateUnassignedAdultCandidate(string source)
		{
			PeopleTracker peopleGen = G.GetPeopleGen();
			RelationshipTracker rels = G.GetRels();
			if (peopleGen == null || rels == null)
			{
				return null;
			}

			for (int i = 0; i < MaxRootFamilyAttempts; i++)
			{
				List<(Entity father, Entity mother)> rootCouples = FindRootCouples(peopleGen.GetAllTrackedPeople(), rels).ToList();
				if (rootCouples.Count == 0 && !TryGenerateRootCouple(peopleGen, out string generatedReason))
				{
					VerificationLog("NewGameFamily", $"startup-generated-population-no-root source={source} reason={generatedReason}");
					return null;
				}

				rootCouples = FindRootCouples(peopleGen.GetAllTrackedPeople(), rels).ToList();
				if (rootCouples.Count == 0)
				{
					continue;
				}

				(Entity father, Entity mother) roots = rootCouples[_generatedStartupCandidateCount % rootCouples.Count];
				Gender gender = (_generatedStartupCandidateCount % 2 == 0) ? Gender.M : Gender.F;
				_generatedStartupCandidateCount++;
				Entity candidate = CreateStandaloneAdultCandidate(peopleGen, roots.father, gender, G.GetNow(), 24 + (_generatedStartupCandidateCount % 18));
				if (IsEligibleStartupOfficer(G.GetNow(), candidate))
				{
					VerificationLog("NewGameFamily", $"startup-generated-population source={source} peep={candidate.Id.id} fam={candidate.data.person.famId}");
					return candidate;
				}
			}

			return null;
		}

		private static bool TryGenerateRootCouple(PeopleTracker peopleGen, out string reason)
		{
			reason = "unknown";
			object creator = PeopleCreatorField?.GetValue(peopleGen);
			if (creator == null || GenerateRootCoupleMethod == null)
			{
				reason = "missing-person-creator-reflection";
				return false;
			}

			Label ethnicity = ResolveStartupEthnicity();
			try
			{
				GenerateRootCoupleMethod.Invoke(creator, new object[] { ethnicity });
				try
				{
					peopleGen.RunNewGameFamilyPlacement();
				}
				catch
				{
				}

				reason = $"generated ethnicity={ethnicity}";
				return true;
			}
			catch (Exception ex)
			{
				Exception inner = ex.InnerException ?? ex;
				reason = $"{inner.GetType().Name}:{inner.Message}";
				return false;
			}
		}

		private static IEnumerable<(Entity father, Entity mother)> FindRootCouples(IEnumerable<Entity> people, RelationshipTracker rels)
		{
			HashSet<EntityID> seen = new HashSet<EntityID>();
			foreach (Entity person in people)
			{
				if (person?.data?.person == null || !person.data.person.IsAlive || person.data.person.g != Gender.M || seen.Contains(person.Id))
				{
					continue;
				}

				Entity spouse = rels.GetListOrNull(person.Id)?.GetSpouse();
				if (spouse?.data?.person == null || !spouse.data.person.IsAlive || spouse.data.person.g != Gender.F || seen.Contains(spouse.Id))
				{
					continue;
				}

				seen.Add(person.Id);
				seen.Add(spouse.Id);
				yield return (person, spouse);
			}
		}

		private static Entity CreateStandaloneAdultCandidate(PeopleTracker peopleGen, Entity seedPerson, Gender gender, SimTime now, int ageYears)
		{
			if (peopleGen?.data?.famTrees == null || seedPerson?.data?.person == null)
			{
				return null;
			}

			object creator = PeopleCreatorField?.GetValue(peopleGen);
			if (creator == null || CreatePersonMethod == null || GenerateTraitsForEntityMethod == null)
			{
				LogStandaloneCreationFailure($"missing-reflection creator={(creator != null)} create={(CreatePersonMethod != null)} traits={(GenerateTraitsForEntityMethod != null)}");
				return null;
			}

			try
			{
				PersonData seedData = seedPerson.data.person;
				FamilyTree seedFamily = null;
				try
				{
					seedFamily = peopleGen.data.FindFamilyTree(seedData.famId);
				}
				catch
				{
				}

				int familyId = peopleGen.data.famTrees.Count;
				FamilyTree familyTree = new FamilyTree
				{
					famId = familyId,
					eth = seedData.eth,
					skin = seedData.s,
					anchor = seedFamily?.anchor ?? default
				};
				peopleGen.data.famTrees.Add(familyTree);

				string lastName = GetGeneratedCandidateLastName(seedData.eth, gender, seedData.last);
				PeepCreationDetails deets = new PeepCreationDetails(
					seedData.eth,
					GetFallbackFirstName(seedData.eth, gender),
					lastName,
					gender,
					seedData.s);

				Entity created = CreatePersonMethod.Invoke(creator, new object[] { true, now.IncrementYears(-ageYears), deets, familyId, false }) as Entity;
				if (created != null)
				{
					object[] traitArgs = GenerateTraitsForEntityMethod.GetParameters().Length == 1
						? new object[] { created }
						: new object[] { created, null };
					GenerateTraitsForEntityMethod.Invoke(creator, traitArgs);
				}

				return created;
			}
			catch (Exception ex)
			{
				Exception inner = ex.InnerException ?? ex;
				LogStandaloneCreationFailure($"{inner.GetType().Name}:{inner.Message}");
				return null;
			}
		}

		private static void LogStandaloneCreationFailure(string reason)
		{
			if (_loggedStandaloneCreationFailure)
			{
				return;
			}

			_loggedStandaloneCreationFailure = true;
			VerificationLog("NewGameFamily", $"startup-standalone-candidate-create-failed reason={reason}");
		}

		private static bool IsEligibleStartupOfficer(SimTime now, Entity person)
		{
			PersonData data = person?.data?.person;
			return data != null
				&& data.IsAlive
				&& data.GetAge(now).YearsFloat >= 20f
				&& data.business.IsNotValid
				&& person.data.agent.pid.id == 0;
		}

		private static string GetFallbackFirstName(Label ethnicity, Gender gender)
		{
			try
			{
				EthnicityDef def = global::Game.Game.serv.globals.settings.ethnicities.FindEthnicityDef(ethnicity);
				return def.loc.GetRandomFirstName(global::Game.Game.ctx.simman.peoplegen.data.rng, gender);
			}
			catch
			{
				return gender == Gender.M ? "John" : "Jane";
			}
		}

		private static string GetGeneratedCandidateLastName(Label ethnicity, Gender gender, string fallback)
		{
			try
			{
				EthnicityDef def = global::Game.Game.serv.globals.settings.ethnicities.FindEthnicityDef(ethnicity);
				for (int i = 0; i < 8; i++)
				{
					string candidate = def.loc.GetRandomLastName(global::Game.Game.ctx.simman.peoplegen.data.rng, gender);
					if (!string.IsNullOrWhiteSpace(candidate) && GeneratedStartupLastNames.Add(candidate))
					{
						return candidate;
					}
				}

				string last = def.loc.GetRandomLastName(global::Game.Game.ctx.simman.peoplegen.data.rng, gender);
				return string.IsNullOrWhiteSpace(last) ? fallback : last;
			}
			catch
			{
				return string.IsNullOrWhiteSpace(fallback) ? "Smith" : fallback;
			}
		}

		private static Label ResolveStartupEthnicity()
		{
			try
			{
				Label playerEthnicity = global::Game.Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
				if (playerEthnicity.IsSet)
				{
					return playerEthnicity;
				}
			}
			catch
			{
			}

			try
			{
				List<Label> ethnicities = global::Game.Game.ctx.session.mapconfig.GetEthnicitiesUniqueSorted();
				if (ethnicities != null && ethnicities.Count > 0 && ethnicities[0].IsSet)
				{
					return ethnicities[0];
				}
			}
			catch
			{
			}

			return EthnicitySettings.DEFAULT_ETHNICITY;
		}
	}

	private static class StartupStarterPackNullGuardPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type afterCreatePlayersType = AccessTools.TypeByName("Game.Session.Setup.AfterCreatePlayers");
				MethodInfo methodInfo = afterCreatePlayersType == null
					? null
					: AccessTools.Method(afterCreatePlayersType, "GrantMapStarterPack");
				if (methodInfo == null)
				{
					Debug.LogWarning("[GameplayTweaks] Startup starter-pack null guard skipped: method not found");
					return;
				}

				harmony.Patch(methodInfo, finalizer: new HarmonyMethod(typeof(StartupStarterPackNullGuardPatch), nameof(Finalizer)));
				Debug.Log("[GameplayTweaks] Startup starter-pack null guard applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] StartupStarterPackNullGuardPatch failed: " + ex.Message);
			}
		}

		private static Exception Finalizer(Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}

			VerificationLog("NewGameFamily", $"startup-starter-pack-skipped error={__exception.GetType().Name}:{__exception.Message}");
			return null;
		}
	}

	private static class StartupNpcSafehouseOwnerGuardPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(BusinessTracker), "OnNPCSafehouseCreation");
				if (methodInfo == null)
				{
					Debug.LogWarning("[GameplayTweaks] Startup NPC safehouse owner guard skipped: method not found");
					return;
				}

				harmony.Patch(methodInfo, finalizer: new HarmonyMethod(typeof(StartupNpcSafehouseOwnerGuardPatch), nameof(Finalizer)));
				Debug.Log("[GameplayTweaks] Startup NPC safehouse owner guard applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] StartupNpcSafehouseOwnerGuardPatch failed: " + ex.Message);
			}
		}

		private static Exception Finalizer(Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}

			VerificationLog("NewGameFamily", $"startup-npc-safehouse-owner-assignment-skipped error={__exception.GetType().Name}:{__exception.Message}");
			return null;
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
				MethodInfo potentialOwnerFind = AccessTools.Method(typeof(BusinessPotentialOwnersCache), "Find", new[] { typeof(Node), typeof(SomaSim.Util.IRandom), typeof(Entity) }, null);
				if (potentialOwnerFind != null)
				{
					harmony.Patch(potentialOwnerFind, finalizer: new HarmonyMethod(typeof(PotentialBizOwnerEligibilityPatch), nameof(PotentialOwnerFindFinalizer)));
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
			string reason = GetBusinessOwnerBlockReason(G.GetNow(), owner, 18f, float.MaxValue, allowHumanPlayer: true, allowedOwnedBusiness: EntityID.INVALID);
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
			if ((BusinessOwnerEnforceAfterProhibitionFamilySafety?.Value ?? false)
				&& TryGetBusinessOwnerFamilyBlockReasonWithAfterProhibitionFamily(person, out string familyReason))
			{
				return "family-" + familyReason;
			}
			string affiliationReason = GetGangAffiliationBlockReason(person);
			if (!string.IsNullOrEmpty(affiliationReason))
			{
				return affiliationReason;
			}
			return string.Empty;
		}

		private static Exception PotentialOwnerFindFinalizer(Exception __exception, ref Entity __result)
		{
			if (__exception == null)
			{
				return null;
			}

			__result = null;
			VerificationLog("Hiring", $"potential-owner-cache-find-skipped reason={__exception.GetType().Name}:{__exception.Message}");
			return null;
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

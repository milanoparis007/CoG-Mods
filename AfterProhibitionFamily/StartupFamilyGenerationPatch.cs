using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Setup;
using Game.Session.Sim;
using HarmonyLib;

namespace AfterProhibitionFamily
{
	internal static class StartupFamilyGenerationPatch
	{
		private const int MaxStartupRootCoupleAttempts = 8;
		private const int MaxRootFamilyAttempts = 8;

		private static readonly FieldInfo PeopleCreatorField = AccessTools.Field(typeof(PeopleTracker), "_creator");
		private static readonly MethodInfo GenerateRootCoupleMethod = AccessTools.Method(typeof(PersonCreator), "GenerateRootCouple", new[] { typeof(Label) });
		private static readonly MethodInfo CreatePersonMethod = AccessTools.Method(typeof(PersonCreator), "CreatePerson", new[] { typeof(bool), typeof(SimTime), typeof(PeepCreationDetails), typeof(int), typeof(bool) });
		private static readonly MethodInfo GenerateTraitsForEntityMethod = AccessTools.Method(typeof(PersonCreator), "GenerateTraitsForEntity");
		private static readonly HashSet<string> GeneratedStartupLastNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static int _generatedStartupCandidateCount;
		private static bool _loggedStandaloneCreationFailure;

		internal static void ApplyPatch(Harmony harmony)
		{
			if (!AfterProhibitionFamilyPlugin.OwnsStartupFamilyGenerationFallback())
			{
				AfterProhibitionFamilyPlugin.Log?.LogInfo("startup family-generation fallback patches skipped by config");
				return;
			}

			bool parentFallback = false;
			bool gangCandidates = false;
			bool copCandidates = false;
			bool fedCandidates = false;
			try
			{
				Type helperType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayersHumanHelper");
				MethodInfo parentMethod = helperType == null
					? null
					: AccessTools.Method(helperType, "PickAndSetValidParents", new[] { typeof(List<Entity>), typeof(bool) });
				if (parentMethod != null)
				{
					harmony.Patch(parentMethod, postfix: new HarmonyMethod(typeof(StartupFamilyGenerationPatch), nameof(ParentFallbackPostfix)));
					parentFallback = true;
				}

				MethodInfo gangCandidateMethod = AccessTools.Method(typeof(PlayerSetup), "GetAndRemoveCandidatePeep");
				if (gangCandidateMethod != null)
				{
					harmony.Patch(gangCandidateMethod, postfix: new HarmonyMethod(typeof(StartupFamilyGenerationPatch), nameof(GangCandidatePostfix)));
					gangCandidates = true;
				}

				Type copsType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayersCops");
				MethodInfo copsAssignMethod = copsType == null
					? null
					: AccessTools.Method(copsType, "AssignEntitiesAsOfficers", new[] { typeof(Entity), typeof(List<Entity>), typeof(int) });
				if (copsAssignMethod != null)
				{
					harmony.Patch(copsAssignMethod, prefix: new HarmonyMethod(typeof(StartupFamilyGenerationPatch), nameof(CopsAssignPrefix)));
					copCandidates = true;
				}

				Type fedsType = AccessTools.TypeByName("Game.Session.Setup.CreatePlayersFeds");
				MethodInfo fedSetupMethod = fedsType == null
					? null
					: AccessTools.Method(fedsType, "SetUpFedPlayer", new[] { typeof(PlayerInfo) });
				if (fedSetupMethod != null)
				{
					harmony.Patch(fedSetupMethod, prefix: new HarmonyMethod(typeof(StartupFamilyGenerationPatch), nameof(FedSetupPrefix)));
					fedCandidates = true;
				}

				AfterProhibitionFamilyPlugin.Log?.LogInfo(
					"startup family-generation patches applied parentFallback=" + parentFallback +
					" gangCandidates=" + gangCandidates +
					" copCandidates=" + copCandidates +
					" fedCandidates=" + fedCandidates);
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("startup family-generation patch failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void ParentFallbackPostfix(List<Entity> eligible, bool anyeth)
		{
			try
			{
				if (!AfterProhibitionFamilyPlugin.OwnsStartupFamilyGenerationFallback() || eligible == null || eligible.Count > 0)
				{
					return;
				}

				if (TryFindOrCreateStartupParent(out Entity parent, out Entity spouse, out string reason))
				{
					eligible.Add(parent);
					AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-parent-fallback parent=" + IdText(parent) + " spouse=" + IdText(spouse) + " anyeth=" + anyeth + " reason=" + reason);
				}
				else
				{
					AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-parent-fallback-failed anyeth=" + anyeth + " reason=" + reason);
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("startup-parent-fallback failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void GangCandidatePostfix(ref Entity __result, bool goons)
		{
			if (!AfterProhibitionFamilyPlugin.OwnsStartupFamilyGenerationFallback() || __result != null)
			{
				return;
			}

			Entity candidate = CreateUnassignedAdultCandidate(goons ? "gang-goon" : "gang");
			if (candidate != null)
			{
				__result = candidate;
				AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-generated-gang-candidate peep=" + IdText(candidate) + " goons=" + goons);
			}
		}

		private static void CopsAssignPrefix(Entity station, List<Entity> candidates, int number)
		{
			if (!AfterProhibitionFamilyPlugin.OwnsStartupFamilyGenerationFallback() || candidates == null)
			{
				return;
			}

			while (candidates.Count < number)
			{
				Entity candidate = CreateUnassignedAdultCandidate("cop-officer");
				if (candidate == null)
				{
					AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-generated-cop-candidate-failed station=" + IdText(station) + " count=" + candidates.Count + " needed=" + number);
					break;
				}

				candidates.Add(candidate);
				AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-generated-cop-candidate station=" + IdText(station) + " peep=" + IdText(candidate) + " needed=" + number);
			}
		}

		private static void FedSetupPrefix()
		{
			if (!AfterProhibitionFamilyPlugin.OwnsStartupFamilyGenerationFallback())
			{
				return;
			}

			try
			{
				SimTime now = GetNow();
				bool hasEligible = GetPeopleGen()?.GetAllTrackedPeople()
					.Any(person => IsEligibleStartupOfficer(now, person)) == true;
				if (hasEligible)
				{
					return;
				}

				Entity candidate = CreateUnassignedAdultCandidate("fed-officer");
				if (candidate != null)
				{
					AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-generated-fed-candidate peep=" + IdText(candidate));
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-generated-fed-candidate-failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static bool TryFindOrCreateStartupParent(out Entity parent, out Entity spouse, out string reason)
		{
			parent = null;
			spouse = null;
			reason = "unknown";

			PeopleTracker peopleGen = GetPeopleGen();
			RelationshipTracker rels = GetRels();
			if (peopleGen == null || rels == null)
			{
				reason = "missing-trackers";
				return false;
			}

			SimTime now = GetNow();
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

			reason = "no-candidates tracked=" + trackedPeople.Count;
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
					reason = "generated-adult-parent-couple ethnicity=" + ethnicity + " attempts=" + (i + 1) + " beforePeople=" + beforePeople + " beforeFamilies=" + beforeFamilies + " " + coupleReason;
					return true;
				}
			}

			int afterPeople = peopleGen.GetAllTrackedPeople().Count();
			reason = lastException == null
				? "adult-parent-generation-no-couple ethnicity=" + ethnicity + " beforePeople=" + beforePeople + " afterPeople=" + afterPeople + " beforeFamilies=" + beforeFamilies
				: "adult-parent-generation-error ethnicity=" + ethnicity + " error=" + lastException.GetType().Name + ":" + lastException.Message;
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

			if (!RelationshipSafetyClassifier.IsValidSpouseCandidate(createdFather, createdMother, out string spouseSafetyReason))
			{
				reason = "adult-parent-spouse-blocked:" + spouseSafetyReason;
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
				reason = "adult-parent-grandparent-links-missing fatherLinks=" + HasLivingParents(createdFather, rels) + " motherLinks=" + HasLivingParents(createdMother, rels);
				return false;
			}

			parent = createdFather;
			spouse = confirmedSpouse;
			reason = "parent=" + IdText(createdFather) + " spouse=" + IdText(confirmedSpouse) + " rootCouples=" + rootCouples.Count;
			return true;
		}

		private static Entity CreateUnassignedAdultCandidate(string source)
		{
			PeopleTracker peopleGen = GetPeopleGen();
			RelationshipTracker rels = GetRels();
			if (peopleGen == null || rels == null)
			{
				return null;
			}

			for (int i = 0; i < MaxRootFamilyAttempts; i++)
			{
				List<(Entity father, Entity mother)> rootCouples = FindRootCouples(peopleGen.GetAllTrackedPeople(), rels).ToList();
				if (rootCouples.Count == 0 && !TryGenerateRootCouple(peopleGen, out string generatedReason))
				{
					AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-generated-population-no-root source=" + source + " reason=" + generatedReason);
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
				Entity candidate = CreateStandaloneAdultCandidate(peopleGen, roots.father, gender, GetNow(), 24 + (_generatedStartupCandidateCount % 18));
				if (IsEligibleStartupOfficer(GetNow(), candidate))
				{
					AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-generated-population source=" + source + " peep=" + IdText(candidate) + " fam=" + candidate.data.person.famId);
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
				TryPlaceGeneratedFamilies(peopleGen);
				reason = "generated ethnicity=" + ethnicity;
				return true;
			}
			catch (Exception ex)
			{
				Exception inner = ex.InnerException ?? ex;
				reason = inner.GetType().Name + ":" + inner.Message;
				return false;
			}
		}

		private static IEnumerable<(Entity father, Entity mother)> FindRootCouples(IEnumerable<Entity> people, RelationshipTracker rels)
		{
			HashSet<EntityID> seen = new HashSet<EntityID>();
			foreach (Entity person in people ?? Array.Empty<Entity>())
			{
				if (person?.data?.person == null || !person.data.person.IsAlive || person.data.person.g != Gender.M || seen.Contains(person.Id))
				{
					continue;
				}

				Entity spouse = rels?.GetListOrNull(person.Id)?.GetSpouse();
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

		private static Entity CreateStandaloneAdultCandidate(PeopleTracker peopleGen, Entity seedPerson, Gender gender, SimTime now, int ageYears)
		{
			if (peopleGen?.data?.famTrees == null || seedPerson?.data?.person == null)
			{
				return null;
			}

			object creator = PeopleCreatorField?.GetValue(peopleGen);
			if (creator == null || CreatePersonMethod == null || GenerateTraitsForEntityMethod == null)
			{
				LogStandaloneCreationFailure("missing-reflection creator=" + (creator != null) + " create=" + (CreatePersonMethod != null) + " traits=" + (GenerateTraitsForEntityMethod != null));
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
				LogStandaloneCreationFailure(inner.GetType().Name + ":" + inner.Message);
				return null;
			}
		}

		private static bool TryFindMarriedMale(IEnumerable<Entity> people, RelationshipTracker rels, SimTime now, bool strictAge, out Entity parent, out Entity spouse)
		{
			parent = null;
			spouse = null;
			foreach (Entity person in people ?? Array.Empty<Entity>())
			{
				if (!IsCandidateMaleParent(person, now, strictAge))
				{
					continue;
				}

				Entity existingSpouse = rels?.GetListOrNull(person.Id)?.GetSpouse();
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
			foreach (Entity person in people ?? Enumerable.Empty<Entity>())
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
				if (!RelationshipSafetyClassifier.IsValidSpouseCandidate(person, candidate, out _))
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
			foreach (Entity candidate in people ?? Array.Empty<Entity>())
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

				if (!RelationshipSafetyClassifier.IsValidSpouseCandidate(parent, candidate, out _))
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

		private static bool HasLivingParents(Entity person, RelationshipTracker rels)
		{
			RelationshipList relationships = rels?.GetListOrNull(person.Id);
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
				AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-root-family-placement-warning error=" + ex.GetType().Name + ":" + ex.Message);
			}
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

		private static void LogStandaloneCreationFailure(string reason)
		{
			if (_loggedStandaloneCreationFailure)
			{
				return;
			}

			_loggedStandaloneCreationFailure = true;
			AfterProhibitionFamilyPlugin.Log?.LogInfo("startup-standalone-candidate-create-failed reason=" + reason);
		}

		private static PeopleTracker GetPeopleGen()
		{
			return global::Game.Game.ctx?.simman?.peoplegen;
		}

		private static RelationshipTracker GetRels()
		{
			return global::Game.Game.ctx?.simman?.rels;
		}

		private static SimTime GetNow()
		{
			return global::Game.Game.ctx?.clock != null
				? global::Game.Game.ctx.clock.Now
				: SimTime.MIN_DATE;
		}

		private static string IdText(Entity entity)
		{
			return entity == null ? "0" : entity.Id.id.ToString();
		}
	}
}

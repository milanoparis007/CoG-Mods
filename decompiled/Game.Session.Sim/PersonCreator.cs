using System;
using System.Collections.Generic;
using System.Linq;
using CatSAT;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Sim;

public class PersonCreator
{
	private sealed class TransientGenerationState
	{
		public bool initialized;

		public Problem traitGenerationSATProblem;

		public string[] allTraitsCache;
	}

	private TransientGenerationState _genstate;

	private PeopleTracker _tracker;

	private MapConfig _mapdef;

	private GlobalSettings _settings;

	private readonly LabelDictionary<int> tmp_inherited = new LabelDictionary<int>();

	private PeopleTracker.PeopleTrackerPersistedData Data => _tracker.data;

	private Xorshift Rng => _tracker.data.rng;

	public void Initialize(PeopleTracker tracker, MapConfig mapdef)
	{
		_settings = Game.serv.globals.settings;
		_tracker = tracker;
		_genstate = new TransientGenerationState();
		_mapdef = mapdef;
	}

	public void Release()
	{
		_genstate = null;
		_tracker = null;
		_mapdef = null;
		_settings = null;
	}

	public void OnTurn()
	{
		if (!_genstate.initialized)
		{
			_genstate.initialized = true;
			bool hasSaveFile = Game.ctx.HasSaveFile;
			OnFirstUpdate(hasSaveFile);
		}
	}

	private void OnFirstUpdate(bool loading)
	{
		InitializeTraitGenerator();
		InitializePeopleGenerator(loading);
	}

	private void InitializePeopleGenerator(bool loading)
	{
		if (!loading)
		{
			List<Label> ethnicitiesUniqueSorted = _mapdef.GetEthnicitiesUniqueSorted();
			List<Label> ethnicitiesAllSorted = _mapdef.GetEthnicitiesAllSorted();
			ethnicitiesAllSorted.Sort();
			int num = Game.ctx.board.nodes.CountAllInterestingBuildings();
			int num2 = (int)(_mapdef.familyGenerator.initialFamiliesRatio * (float)num);
			for (int i = 0; i < num2; i++)
			{
				Label ethName = ((i < ethnicitiesUniqueSorted.Count) ? ethnicitiesUniqueSorted[i] : Rng.PickElement(ethnicitiesAllSorted));
				GenerateRootCouple(ethName);
			}
		}
	}

	private void GenerateRootCouple(Label ethName)
	{
		Skin randomSkinForEth = Game.ctx.session.mapconfig.ethnicMakeup.GetRandomSkinForEth(Rng, ethName);
		FamilyTree familyTree = new FamilyTree
		{
			famId = Data.famTrees.Count,
			eth = ethName,
			skin = randomSkinForEth
		};
		Data.famTrees.Add(familyTree);
		EthnicityDef eth = _settings.ethnicities.FindEthnicityDef(ethName);
		int year = Game.serv.globals.settings.general.generator.procGenYears.from;
		SimTime simTime = new SimTime(year, 0);
		SimTime bdate = simTime.IncrementYears(Rng.Generate(20f, 30f) * -1f);
		Entity m = CreatePersonRoot(bdate, eth, Gender.M, familyTree);
		SimTime bdate2 = simTime.IncrementYears(Rng.Generate(20f, 30f) * -1f);
		Entity f = CreatePersonRoot(bdate2, eth, Gender.F, familyTree);
		float deltaYears = Rng.Generate(_mapdef.familyGenerator.marriageAgeRange);
		SimTime marriageTime = bdate2.IncrementYears(deltaYears);
		int value = Rng.PickElement(_mapdef.familyGenerator.kidsCount);
		value = MathUtil.ClampMin(value, 4);
		LinkCouple(m, f, marriageTime, value);
	}

	private Entity CreatePersonRoot(SimTime bdate, EthnicityDef eth, Gender gender, FamilyTree treeTraits)
	{
		string randomLastName = eth.loc.GetRandomLastName(Rng, gender);
		string randomFirstName = eth.loc.GetRandomFirstName(Rng, gender);
		PeepCreationDetails deets = new PeepCreationDetails(eth.id, randomFirstName, randomLastName, gender);
		Entity entity = CreatePerson(root: true, bdate, deets, treeTraits.famId);
		GenerateTraitsForEntity(entity);
		return entity;
	}

	private Entity CreatePerson(bool root, SimTime bdate, PeepCreationDetails deets, int famId, bool manual = false)
	{
		MapConfig.FamilyGenConfig familyGenerator = _mapdef.familyGenerator;
		Entity entity = Game.ctx.entityman.CreateByName(EntityConstants.PERSON);
		PersonData person = entity.data.person;
		FamilyTree familyTree = Data.FindFamilyTree(famId);
		person.g = deets.gender;
		person.s = ((deets.skin != Skin.Unknown) ? deets.skin : familyTree.skin);
		person.portrait = deets.portrait;
		person.eth = deets.ethnicity;
		person.first = deets.fname;
		person.last = deets.lname;
		person.born = bdate;
		person.died = SimTime.MAX_DATE;
		person.famId = famId;
		if (!manual)
		{
			if (!root && deets.gender == Gender.F && Rng.CheckProbability(familyGenerator.marriageRatio))
			{
				float deltaYears = Rng.Generate(familyGenerator.marriageAgeRange);
				person.futuremarriage = person.born.IncrementYears(deltaYears);
			}
			RandomRangeF range = ((!root && Rng.CheckProbability(familyGenerator.childMortalityRatio)) ? familyGenerator.childMortalityAgeRange : familyGenerator.adultMortalityAgeRange);
			float deltaYears2 = Rng.Generate(range);
			person.futuredeath = person.born.IncrementYears(deltaYears2);
			int num = Rng.Generate(familyGenerator.friendsAge);
			person.futurefriends = person.born.IncrementYears(num);
		}
		_tracker.AddToCache(entity);
		return entity;
	}

	public void LinkCouple(Entity m, Entity f, SimTime marriageTime, int futureKidCount)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		PersonData person = m.data.person;
		PersonData person2 = f.data.person;
		rels.GetOrCreate(m.Id, f.Id, RelationshipType.Spouse, warnOnExisting: true);
		rels.GetOrCreate(f.Id, m.Id, RelationshipType.Spouse, warnOnExisting: true);
		person2.last = person.last;
		for (int i = 0; i < futureKidCount; i++)
		{
			float deltaYears = Rng.Generate(1f, 2 * i + 3);
			SimTime item = marriageTime.IncrementYears(deltaYears);
			person2.futurekids.Add(item);
		}
		person2.futurekids.Sort(SimTime.CompareDescending);
	}

	public Entity ManuallyMakePerson(SimTime bdate, PeepCreationDetails deets, int famId, Entity father, Entity mother)
	{
		Entity entity = CreatePerson(root: false, bdate, deets, famId, manual: true);
		if (deets.traits == null)
		{
			GenerateTraitsForChild(entity, father.data.person, mother.data.person);
		}
		else
		{
			ForceTraitsForChild(entity, deets.traits);
		}
		return LinkChildToFamily(father, mother, entity);
	}

	public Entity CreateKid(Entity father, Entity mother, SimTime bdate)
	{
		Gender gender = (Rng.CoinFlip() ? Gender.M : Gender.F);
		PersonData person = father.data.person;
		PersonData person2 = mother.data.person;
		string fname = MakeChildName(person.eth, gender);
		PeepCreationDetails deets = new PeepCreationDetails(person.eth, fname, person.last, gender);
		Entity entity = CreatePerson(root: false, bdate, deets, person.famId);
		GenerateTraitsForChild(entity, person, person2);
		return LinkChildToFamily(father, mother, entity);
	}

	private string MakeChildName(Label fathereth, Gender gender)
	{
		Label id = (Rng.CoinFlip() ? fathereth : EthnicitySettings.DEFAULT_ETHNICITY);
		return _settings.ethnicities.FindEthnicityDef(id).loc.GetRandomFirstName(Rng, gender);
	}

	private static Entity LinkChildToFamily(Entity father, Entity mother, Entity kid)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		EntityID id = kid.Id;
		EntityID id2 = mother.Id;
		EntityID id3 = father.Id;
		rels.GetOrCreate(id, id2, RelationshipType.Mother, warnOnExisting: true);
		rels.GetOrCreate(id2, id, RelationshipType.Child, warnOnExisting: true);
		rels.GetOrCreate(id, id3, RelationshipType.Father, warnOnExisting: true);
		rels.GetOrCreate(id3, id, RelationshipType.Child, warnOnExisting: true);
		HookUpSiblings(id, id2);
		HookUpParentSiblings(id, id2, mothersSide: true);
		HookUpParentSiblings(id, id3, mothersSide: false);
		return kid;
	}

	private static void HookUpSiblings(EntityID kid, EntityID mother)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		List<Relationship> data = rels.GetListOrCreate(mother).data;
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			Relationship relationship = data[i];
			if (relationship.type == RelationshipType.Child && relationship.to != kid)
			{
				EntityID to = relationship.to;
				rels.GetOrCreate(to, kid, RelationshipType.Sibling, warnOnExisting: true);
				rels.GetOrCreate(kid, to, RelationshipType.Sibling, warnOnExisting: true);
			}
		}
	}

	private static void HookUpParentSiblings(EntityID kid, EntityID parent, bool mothersSide)
	{
		List<Relationship> data = Game.ctx.simman.rels.GetListOrCreate(parent).data;
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			Relationship relationship = data[i];
			if (relationship.type == RelationshipType.Sibling)
			{
				HookUpAuntOrUncle(kid, relationship.to, mothersSide);
			}
		}
	}

	private static void HookUpAuntOrUncle(EntityID kid, EntityID relative, bool mothersSide)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		rels.GetOrCreate(kid, relative, mothersSide ? RelationshipType.MotherSib : RelationshipType.FatherSib, warnOnExisting: true);
		rels.GetOrCreate(relative, kid, RelationshipType.SibChild, warnOnExisting: true);
		List<Relationship> data = rels.GetListOrCreate(relative).data;
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			Relationship relationship = data[i];
			if (relationship.type == RelationshipType.Child)
			{
				EntityID to = relationship.to;
				rels.GetOrCreate(to, kid, RelationshipType.Cousin, warnOnExisting: false);
				rels.GetOrCreate(kid, to, RelationshipType.Cousin, warnOnExisting: true);
			}
		}
	}

	private void GenerateTraitsForChild(Entity peep, PersonData father, PersonData mother)
	{
		tmp_inherited.Clear();
		foreach (Label traitId in father.traitIds)
		{
			tmp_inherited.Increment(traitId, 1);
		}
		foreach (Label traitId2 in mother.traitIds)
		{
			tmp_inherited.Increment(traitId2, 1);
		}
		GenerateTraitsForEntity(peep, tmp_inherited);
		tmp_inherited.Clear();
	}

	private void ForceTraitsForChild(Entity peep, List<Label> traits)
	{
		foreach (Label trait in traits)
		{
			peep.data.person.traitIds.Add(trait);
		}
	}

	private void InitializeTraitGenerator()
	{
		TraitList traits = _settings.people.traits;
		MapConfig.FamilyGenConfig familyGenerator = _mapdef.familyGenerator;
		Problem problem = new Problem("trait generator");
		_genstate.traitGenerationSATProblem = problem;
		_genstate.allTraitsCache = traits.Select((Trait def) => def.id.String).ToArray();
		Literal[] literals = ((IEnumerable<string>)_genstate.allTraitsCache).Select((Func<string, Literal>)((string tr) => tr)).ToArray();
		problem.Quantify(familyGenerator.traitsPerPerson.from, familyGenerator.traitsPerPerson.to, literals);
		foreach (Trait item in traits)
		{
			if (item.not != null)
			{
				foreach (Label item2 in item.not)
				{
					problem.Inconsistent(item.id.String, item2.String);
				}
			}
			if (item.implies == null)
			{
				continue;
			}
			foreach (Label imply in item.implies)
			{
				problem.Assert((Literal)item.id.String > imply.String);
			}
		}
		problem.Optimize();
	}

	private void GenerateTraitsForEntity(Entity peep, LabelDictionary<int> inherited = null)
	{
		MapConfig.FamilyGenConfig familyGenerator = _mapdef.familyGenerator;
		TraitList traits = _settings.people.traits;
		CatSAT.Random.SetSeed(HashUtil.Hash(peep.Id.index));
		Problem problem = (Problem.Current = _genstate.traitGenerationSATProblem);
		problem.ResetPropositions();
		foreach (Trait item in traits)
		{
			int num = inherited?.FindOrDefault(item.id, 0) ?? 0;
			float initialProbability = ((num > 0) ? ((float)num * familyGenerator.probTraitInherited) : familyGenerator.probTraitOther);
			((Proposition)item.id.String).InitialProbability = initialProbability;
		}
		Solution solution = problem.Solve();
		TagList traitIds = peep.data.person.traitIds;
		string[] allTraitsCache = _genstate.allTraitsCache;
		foreach (string text in allTraitsCache)
		{
			if (solution[(Literal)text])
			{
				traitIds.Add((Label)text);
			}
		}
	}
}

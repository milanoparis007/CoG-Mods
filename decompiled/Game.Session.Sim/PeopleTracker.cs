using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public class PeopleTracker : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>, ISaveLoadProvider, ILoadObserver
{
	public sealed class PeopleTrackerPersistedData
	{
		public List<FamilyTree> famTrees = new List<FamilyTree>();

		public Xorshift rng = Game.ctx.scenario.MakeSeededRng<PeopleTracker>();

		public FamilyTree FindFamilyTree(int id)
		{
			if (id < 0 || id >= famTrees.Count)
			{
				id = 0;
			}
			return famTrees[id];
		}
	}

	private class FamPlacementAtNode
	{
		public const int TOP_ETH_COUNT = 5;

		public Node node;

		public List<Label> eths;

		public List<float> scores;

		public FamilyTree fam;

		internal float FindScoreForEth(Label eth)
		{
			if (fam == null)
			{
				int i = 0;
				for (int count = eths.Count; i < count; i++)
				{
					if (eths[i] == eth)
					{
						return scores[i];
					}
				}
			}
			return 0f;
		}
	}

	private struct BirthRequest
	{
		public Entity entity;

		public SimTime bdate;
	}

	public PeopleTrackerPersistedData data;

	private MapConfig _mapdef;

	private PersonCreator _creator;

	private SortedDictionary<int, List<Entity>> _peepsByYearOfBirth;

	private Listeners<Entity> OnAfterPersonBirth;

	public Listeners<Entity> OnBeforePersonDeath;

	private static FloatMapper ETH_SCORING_FN = new FloatMapper
	{
		x = { 0f, 0.2f, 0.5f, 0.7f, 1f },
		y = { 0.01f, 0.01f, 0.1f, 1f, 10f }
	};

	private readonly Stopwatch tmp_stopwatch = Stopwatch.StartNew();

	private readonly List<BirthRequest> tmp_births = new List<BirthRequest>(1024);

	private readonly List<Entity> tmp_process = new List<Entity>(1024);

	private readonly List<Entity> tmp_bachelors = new List<Entity>(1024);

	private readonly List<float> tmp_scores = new List<float>(1024);

	public void Initialize(SimulationManager manager)
	{
		data = new PeopleTrackerPersistedData();
		_peepsByYearOfBirth = new SortedDictionary<int, List<Entity>>();
		_mapdef = Game.ctx.session.mapconfig;
		_creator = new PersonCreator();
		_creator.Initialize(this, _mapdef);
		OnAfterPersonBirth = new Listeners<Entity>();
		OnBeforePersonDeath = new Listeners<Entity>();
	}

	public void Release()
	{
		OnBeforePersonDeath = (OnAfterPersonBirth = null);
		foreach (Entity allTrackedPerson in GetAllTrackedPeople())
		{
			Game.ctx.entityman.DestroyEntity(allTrackedPerson, shutdown: true);
		}
		_creator.Release();
		_creator = null;
		_mapdef = null;
		_peepsByYearOfBirth.Clear();
		data = null;
	}

	public IEnumerable<Entity> GetAllTrackedPeople()
	{
		foreach (List<Entity> value in _peepsByYearOfBirth.Values)
		{
			foreach (Entity item in value)
			{
				yield return item;
			}
		}
	}

	private static int GetPersonBirthYear(Entity person)
	{
		return person.data.person.born.YearsInt;
	}

	public void AddToCache(Entity person)
	{
		int personBirthYear = GetPersonBirthYear(person);
		if (!_peepsByYearOfBirth.TryGetValue(personBirthYear, out var value))
		{
			value = (_peepsByYearOfBirth[personBirthYear] = new List<Entity>());
		}
		value.Add(person);
	}

	private void FindPeopleAroundDate(SimTime date, int plusOrMinusYears, Predicate<Entity> fn, List<Entity> results, bool clearFirst)
	{
		if (clearFirst)
		{
			results.Clear();
		}
		int yearsInt = date.YearsInt;
		int num = yearsInt - plusOrMinusYears;
		int num2 = yearsInt + plusOrMinusYears;
		foreach (KeyValuePair<int, List<Entity>> item in _peepsByYearOfBirth)
		{
			if (item.Key < num || item.Key > num2)
			{
				continue;
			}
			foreach (Entity item2 in item.Value)
			{
				if (fn(item2))
				{
					results.Add(item2);
				}
			}
		}
		results.Sort(Entity.Comparison);
	}

	public void ProducePeopleWhere(Predicate<Entity> fn, List<Entity> results, bool clearFirst)
	{
		if (clearFirst)
		{
			results.Clear();
		}
		foreach (KeyValuePair<int, List<Entity>> item in _peepsByYearOfBirth)
		{
			foreach (Entity item2 in item.Value)
			{
				if (fn(item2))
				{
					results.Add(item2);
				}
			}
		}
		results.Sort(Entity.Comparison);
	}

	internal FamilyTree.Anchor FindFamilyAnchor(Entity peep)
	{
		return data.FindFamilyTree(peep.data.person.famId).anchor;
	}

	internal static bool IsNodeEligibleForStartPosition(Node node)
	{
		if (!node.HasRoad || node.HasRail || !node.IsOnGround)
		{
			return false;
		}
		int num = 0;
		ushort num2 = (ushort)Game.ctx.board.nodes.GetNeighborFlags(node, (NodeEdge edge) => edge.IsRoad);
		for (int num3 = 0; num3 < 4; num3++)
		{
			if (((1 << num3) & num2) > 0)
			{
				num++;
			}
		}
		return num >= 2;
	}

	private List<FamPlacementAtNode> GenerateFamPlacementCandidates()
	{
		return Game.ctx.board.nodes.GetAllNodesUnsafe().Where(IsNodeEligibleForStartPosition).Select(MakeFamPlacementCandidate)
			.ToList();
	}

	private FamPlacementAtNode MakeFamPlacementCandidate(Node node)
	{
		var (eths, scores) = node.FindEthnicities(5);
		return new FamPlacementAtNode
		{
			node = node,
			eths = eths,
			scores = scores,
			fam = null
		};
	}

	public void RunNewGameFamilyPlacement()
	{
		List<FamPlacementAtNode> list = GenerateFamPlacementCandidates();
		List<float> weights = ListGenerators.ListOfDefaultValues<float>(list.Count);
		List<float> scores = ListGenerators.ListOfDefaultValues<float>(list.Count);
		data.rng.Shuffle(list);
		for (int i = 0; i < data.famTrees.Count; i++)
		{
			FamilyTree fam = data.famTrees[i];
			AssignLocationToFam(fam, list, scores, weights);
			if (list.Count == 0)
			{
				Logger.Warning("Ran out of nodes during family assignment, there are more families than street corners" + $" - at fam {i + 1} of {data.famTrees.Count}, map {Game.ctx.session.mapconfig.citytype} {Game.ctx.session.mapconfig.id}");
				break;
			}
		}
	}

	private void AssignLocationToFam(FamilyTree fam, List<FamPlacementAtNode> pegs, List<float> scores, List<float> weights)
	{
		bool normalized = RescoreForFam(fam, pegs, scores, weights);
		int num = data.rng.PickIndex(pegs, weights, normalized);
		if (num < 0)
		{
			num = pegs.Count - 1;
		}
		FamPlacementAtNode famPlacementAtNode = pegs.SwapRemoveAt(num);
		float ethscore = scores.SwapRemoveAt(num);
		weights.SwapRemoveAt(num);
		famPlacementAtNode.fam = fam;
		fam.anchor = new FamilyTree.Anchor
		{
			nodeId = famPlacementAtNode.node.id,
			pos = famPlacementAtNode.node.pos,
			ethscore = ethscore
		};
	}

	private static bool RescoreForFam(FamilyTree fam, List<FamPlacementAtNode> pegs, List<float> scores, List<float> weights)
	{
		int i = 0;
		for (int count = pegs.Count; i < count; i++)
		{
			float input = (scores[i] = pegs[i].FindScoreForEth(fam.eth));
			weights[i] = ETH_SCORING_FN.Eval(input);
		}
		return weights.Normalize();
	}

	public void ForceFamily(int famId, Label? eth = null, Skin? skin = null)
	{
		FamilyTree familyTree = data.FindFamilyTree(famId);
		if (familyTree != null)
		{
			if (eth.HasValue)
			{
				familyTree.eth = eth.Value;
			}
			if (skin.HasValue)
			{
				familyTree.skin = skin.Value;
			}
		}
	}

	public void OnSystemTurn()
	{
		SimTime now = Game.ctx.clock.State.now;
		_creator.OnTurn();
		tmp_stopwatch.Restart();
		ProcessBirths(now);
		ProcessMarriages(now);
		ProcessDeaths(now);
		tmp_stopwatch.Stop();
	}

	private int CountPeopleInCache()
	{
		return _peepsByYearOfBirth.Sum((KeyValuePair<int, List<Entity>> e) => e.Value.Count);
	}

	private int ProcessBirths(SimTime now)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		tmp_births.Clear();
		foreach (Entity allTrackedPerson in GetAllTrackedPeople())
		{
			List<SimTime> futurekids = allTrackedPerson.data.person.futurekids;
			if (futurekids.Count > 0)
			{
				SimTime bdate = futurekids.LastOrDefaultFast();
				if (bdate.days < now.days)
				{
					tmp_births.Add(new BirthRequest
					{
						entity = allTrackedPerson,
						bdate = bdate
					});
					futurekids.RemoveLast();
				}
			}
		}
		int count = tmp_births.Count;
		while (tmp_births.Count > 0)
		{
			BirthRequest birthRequest = tmp_births.RemoveLast();
			Entity entity = birthRequest.entity;
			Entity spouse = rels.GetListOrNull(entity.Id).GetSpouse();
			Entity arg = _creator.CreateKid(spouse, entity, birthRequest.bdate);
			OnAfterPersonBirth.Invoke(arg);
		}
		return count;
	}

	private int ProcessMarriages(SimTime now)
	{
		ProducePeopleWhere((Entity peep) => peep.data.person.futuremarriage.days < now.days, tmp_process, clearFirst: true);
		int count = tmp_process.Count;
		while (tmp_process.Count > 0)
		{
			Entity entity = tmp_process.RemoveLast();
			entity.data.person.futuremarriage = SimTime.MAX_DATE;
			FindMatchAndLinkCouple(entity, now);
		}
		return count;
	}

	private int ProcessDeaths(SimTime now)
	{
		if (Game.ctx.IsInteractive)
		{
			return -1;
		}
		ProducePeopleWhere((Entity peep) => peep.data.person.futuredeath.days < now.days, tmp_process, clearFirst: true);
		int count = tmp_process.Count;
		while (tmp_process.Count > 0)
		{
			Entity entity = tmp_process.RemoveLast();
			MarkAsDead(entity, entity.data.person.futuredeath);
		}
		return count;
	}

	public void MarkAsDead(Entity peep, SimTime timeOfDeath)
	{
		OnBeforePersonDeath.Invoke(peep);
		peep.components.agent.OnDeath();
		PersonData person = peep.data.person;
		person.died = timeOfDeath;
		person.futuredeath = SimTime.MAX_DATE;
		person.futuremarriage = SimTime.MAX_DATE;
		person.futurekids.Clear();
	}

	private void FindMatchAndLinkCouple(Entity f, SimTime now)
	{
		Entity entity = FindMatchFor(f, now);
		if (entity != null)
		{
			int futureKidCount = data.rng.PickElement(_mapdef.familyGenerator.kidsCount);
			_creator.LinkCouple(entity, f, now, futureKidCount);
		}
	}

	private Entity FindMatchFor(Entity f, SimTime now)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		float minage = _mapdef.familyGenerator.marriageAgeRange.from;
		PersonData person = f.data.person;
		tmp_bachelors.Clear();
		tmp_scores.Clear();
		FindPeopleAroundDate(person.born, 10, delegate(Entity peep)
		{
			PersonData person2 = peep.data.person;
			if (person2.g != Gender.M)
			{
				return false;
			}
			RelationshipList listOrNull = rels.GetListOrNull(f.Id);
			if (listOrNull != null && listOrNull.HasAny(peep.Id))
			{
				return false;
			}
			return !rels.GetListOrNull(peep.Id).HasSpouse() && person2.GetAge(now).YearsFloat >= minage;
		}, tmp_bachelors, clearFirst: true);
		if (tmp_bachelors.Count == 0)
		{
			return null;
		}
		tmp_scores.Clear();
		int num = 0;
		for (int count = tmp_bachelors.Count; num < count; num++)
		{
			tmp_scores.Add(ScoreCandidate(f, tmp_bachelors[num]));
		}
		Entity result = data.rng.PickElement(tmp_bachelors, tmp_scores);
		tmp_bachelors.Clear();
		tmp_scores.Clear();
		return result;
	}

	private static float ScoreCandidate(Entity current, Entity other)
	{
		PersonData person = current.data.person;
		PersonData person2 = other.data.person;
		float num = Math.Abs(person.born.Subtract(person2.born).YearsFloat);
		float num2 = MathUtil.Clamp(10f - num, 0f, 10f);
		return ((person.eth == person2.eth) ? 3f : 1f) * num2;
	}

	public Entity ManuallyMakePerson(SimTime bdate, PeepCreationDetails deets, int fam, Entity father, Entity mother)
	{
		return _creator.ManuallyMakePerson(bdate, deets, fam, father, mother);
	}

	public Entity FindRandoToMarry(Entity peep)
	{
		RelationshipTracker rels = Game.ctx.simman.rels;
		PersonData pdata = peep.data.person;
		Gender goalGender = ((peep.data.person.g == Gender.F) ? Gender.M : Gender.F);
		return (from other in GetAllTrackedPeople()
			where CanMarry(other)
			select other).FirstOrDefault();
		bool CanMarry(Entity other)
		{
			PersonData person = other.data.person;
			if (person.g != goalGender)
			{
				return false;
			}
			if (Math.Abs(person.born.YearsFloat - pdata.born.YearsFloat) > 10f)
			{
				return false;
			}
			if (person.business.IsValid)
			{
				return false;
			}
			if ((rels.GetListOrNull(other.Id)?.GetSpouse() ?? null) != null)
			{
				return false;
			}
			return true;
		}
	}

	public void ForceMarry(Entity a, Entity b)
	{
		Entity m = ((a.data.person.g == Gender.M) ? a : b);
		Entity f = ((a.data.person.g == Gender.F) ? a : b);
		_creator.LinkCouple(m, f, Game.ctx.clock.Now, 0);
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(PeopleTrackerPersistedData result)
		{
			this.data = result;
		});
		yield break;
	}

	public void OnAfterManagerLoad()
	{
	}

	public void OnAfterEntityLoad()
	{
		_peepsByYearOfBirth.Clear();
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesPersonsUnsafe())
		{
			AddToCache(item);
		}
	}
}

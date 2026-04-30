using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class CreatePlayersHumanHelper
{
	public struct Results
	{
		public PlayerStartupDetails details;

		public Entity player;

		public Entity francine;

		public Entity ziggy;
	}

	private const int MIN_PARENT_AGE = 35;

	private const int MAX_PARENT_AGE = 65;

	private SimTime _now;

	private readonly Xorshift _rng;

	private readonly PlayerStartupDetails _peepDetails;

	private static FloatMapper ETH_SCORING_FN = new FloatMapper
	{
		x = { 0f, 0.7f, 1f },
		y = { 1f, 10f, 100f }
	};

	private const int MIN_PLAYER_AGE = 6570;

	private const int MAX_PLAYER_AGE = 8760;

	private const int MIN_FRANCINE_DELTA = -730;

	private const int MAX_FRANCINE_DELTA = 730;

	private const int MIN_ZIGGY_DELTA = -730;

	private const int MAX_ZIGGY_DELTA = 730;

	public CreatePlayersHumanHelper(PlayerID pid)
	{
		_rng = Game.ctx.scenario.MakeSeededRng(pid);
		_now = Game.ctx.clock.Now;
		_peepDetails = Game.ctx.scenario.newgamepars.playerdetails;
	}

	public Results CreatePlayerPeepAndFrancine()
	{
		(Entity father, Entity mother) playerParents = GetPlayerParents();
		Entity item = playerParents.father;
		Entity item2 = playerParents.mother;
		(Entity francine, Entity spouse) tuple = CreateFrancineAndSpouse(item, item2);
		Entity item3 = tuple.francine;
		Entity item4 = tuple.spouse;
		Entity entity = CreateHumanPlayerPeep(item, item2);
		Entity ziggy = (_peepDetails.tutorial ? CreateZiggy(entity, item3, item4) : null);
		string last = item.data.person.last;
		SetFamilySurnameRecursive(item2, last, entity.data.person.last);
		SetFamilySurnameRecursive(item, last, entity.data.person.last);
		return new Results
		{
			player = entity,
			francine = item3,
			ziggy = ziggy,
			details = _peepDetails
		};
	}

	private (Entity father, Entity mother) GetPlayerParents()
	{
		Entity item;
		Entity item2;
		using (ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate())
		{
			PickAndSetValidParents(pooledBlockList);
			Entity entity = _rng.PickElement(pooledBlockList);
			Entity spouse = Game.ctx.simman.rels.data.entries[entity.Id].GetSpouse();
			if (entity.data.person.g == Gender.M)
			{
				item = entity;
				item2 = spouse;
			}
			else
			{
				item = spouse;
				item2 = entity;
			}
		}
		return (father: item, mother: item2);
	}

	private void PickAndSetValidParents(List<Entity> eligible, bool anyeth = false)
	{
		PeopleTracker peoplegen = Game.ctx.simman.peoplegen;
		Label eth = _peepDetails.player.ethnicity;
		Skin skin = _peepDetails.player.skin;
		List<FamilyTree> list = (from fam in peoplegen.data.famTrees
			where anyeth || fam.eth == eth
			where !_peepDetails.tutorial || fam.skin == Skin.Light
			select fam).ToList();
		List<float> list2 = list.Select(ScoreFamily).ToList();
		while (list.Count > 0)
		{
			int num = _rng.PickIndex(list, list2, normalized: false);
			if (num < 0)
			{
				break;
			}
			list2.RemoveAt(num);
			FamilyTree familyTree = list.RemoveAndReturn(num);
			int famId = familyTree.famId;
			peoplegen.ProducePeopleWhere((Entity e) => IsEligibleFamilyMember(e, famId), eligible, clearFirst: true);
			if (eligible.Count > 0)
			{
				if (anyeth)
				{
					ForceSetFamilyEthnicity(famId);
				}
				if (skin != Skin.Unknown)
				{
					ForceSetFamilySkin(famId, skin);
				}
				return;
			}
		}
		if (!anyeth)
		{
			PickAndSetValidParents(eligible, anyeth: true);
			return;
		}
		Logger.Error("Couldn't find a single eligible family from the whole set?");
		peoplegen.ProducePeopleWhere((Entity p) => p.data.person.IsAlive && Game.ctx.simman.rels.data.entries[p.Id].GetSpouse() != null, eligible, clearFirst: true);
	}

	private float ScoreFamily(FamilyTree fam)
	{
		return ETH_SCORING_FN.Eval(fam.anchor.ethscore);
	}

	private static void ProduceFamilyMembers(int famid, List<Entity> members)
	{
		Game.ctx.simman.peoplegen.ProducePeopleWhere((Entity p) => p.data.person.famId == famid, members, clearFirst: true);
	}

	private void ForceSetFamilyEthnicity(int famid)
	{
		Label ethnicity = _peepDetails.player.ethnicity;
		using (ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate())
		{
			ProduceFamilyMembers(famid, pooledBlockList);
			foreach (Entity item in pooledBlockList)
			{
				item.data.person.eth = ethnicity;
			}
		}
		Game.ctx.simman.peoplegen.ForceFamily(famid, ethnicity);
	}

	private void ForceSetFamilySkin(int famid, Skin skin)
	{
		using (ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate())
		{
			ProduceFamilyMembers(famid, pooledBlockList);
			foreach (Entity item in pooledBlockList)
			{
				item.data.person.s = skin;
			}
		}
		Game.ctx.simman.peoplegen.ForceFamily(famid, null, skin);
	}

	private bool IsEligibleFamilyMember(Entity e, int famid)
	{
		PersonData person = e.data.person;
		if (!person.IsAlive)
		{
			return false;
		}
		if (person.famId != famid)
		{
			return false;
		}
		if (person.g != Gender.M)
		{
			return false;
		}
		int yearsInt = e.data.person.GetAge(_now).YearsInt;
		if (yearsInt < 35 || yearsInt > 65)
		{
			return false;
		}
		return Game.ctx.simman.rels.data.entries[e.Id].GetSpouse() != null;
	}

	private Entity CreateHumanPlayerPeep(Entity father, Entity mother)
	{
		PeopleTracker peoplegen = Game.ctx.simman.peoplegen;
		int famId = father.data.person.famId;
		PeepCreationDetails player = _peepDetails.player;
		SimTime bdate = _now.IncrementDays(-1 * _rng.Generate(6570, 8760));
		return peoplegen.ManuallyMakePerson(bdate, player, famId, father, mother);
	}

	private (Entity francine, Entity spouse) CreateFrancineAndSpouse(Entity playerfather, Entity playermother)
	{
		Entity entity = (_rng.CoinFlip() ? playerfather : playermother);
		Entity father = Game.ctx.simman.rels.data.entries[entity.Id].GetFather();
		Entity mother = Game.ctx.simman.rels.data.entries[entity.Id].GetMother();
		Gender gender = (_rng.CoinFlip() ? Gender.M : Gender.F);
		PersonData person = father.data.person;
		int deltaDays = _rng.Generate(-730, 730);
		SimTime bdate = entity.data.person.born.IncrementDays(deltaDays);
		string ethnicFirstName = GetEthnicFirstName(person, gender);
		PeepCreationDetails deets = new PeepCreationDetails(person.eth, ethnicFirstName, person.last, gender);
		Entity entity2 = Game.ctx.simman.peoplegen.ManuallyMakePerson(bdate, deets, person.famId, father, mother);
		Entity entity3 = Game.ctx.simman.peoplegen.FindRandoToMarry(entity2);
		Game.ctx.simman.peoplegen.ForceMarry(entity2, entity3);
		return (francine: entity2, spouse: entity3);
	}

	private Entity CreateZiggy(Entity player, Entity francine, Entity spouse)
	{
		Entity father = ((francine.data.person.g == Gender.M) ? francine : spouse);
		Entity mother = ((francine.data.person.g == Gender.F) ? francine : spouse);
		Gender gender = Gender.M;
		PersonData person = francine.data.person;
		int deltaDays = _rng.Generate(-730, 730);
		SimTime bdate = player.data.person.born.IncrementDays(deltaDays);
		string ethnicFirstName = GetEthnicFirstName(person, gender);
		PeepCreationDetails deets = new PeepCreationDetails(person.eth, ethnicFirstName, person.last, gender);
		return Game.ctx.simman.peoplegen.ManuallyMakePerson(bdate, deets, person.famId, father, mother);
	}

	private string GetEthnicFirstName(PersonData data, Gender gender)
	{
		Label id = (_rng.CoinFlip() ? data.eth : EthnicitySettings.DEFAULT_ETHNICITY);
		return Game.serv.globals.settings.ethnicities.FindEthnicityDef(id).loc.GetRandomFirstName(_rng, gender);
	}

	private static void SetFamilySurnameRecursive(Entity peep, string originalSurname, string newSurname)
	{
		string text = peep?.data.person.last;
		if (text == null || text != originalSurname || text == newSurname)
		{
			return;
		}
		peep.data.person.last = newSurname;
		RelationshipTracker rels = Game.ctx.simman.rels;
		foreach (Relationship datum in rels.GetListOrCreate(peep.Id).data)
		{
			if (datum.type == RelationshipType.Child && datum.to != peep.Id)
			{
				SetFamilySurnameRecursive(datum.to.FindEntity(), originalSurname, newSurname);
			}
		}
		SetFamilySurnameRecursive(rels.data.entries[peep.Id].GetMother(), originalSurname, newSurname);
		SetFamilySurnameRecursive(rels.data.entries[peep.Id].GetFather(), originalSurname, newSurname);
	}
}

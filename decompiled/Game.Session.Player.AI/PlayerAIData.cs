using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class PlayerAIData
{
	public bool waitForEndOfTurn;

	public Label personality;

	public List<Label> aspects;

	public UnitsAdvisorData units = new UnitsAdvisorData();

	public SocialAdvisorData social = new SocialAdvisorData();

	public BusinessAdvisorData business = new BusinessAdvisorData();

	public SafehouseAdvisorData safehouse = new SafehouseAdvisorData();

	public TerritoryAdvisorData territory = new TerritoryAdvisorData();

	public PrecinctAdvisorData precinct = new PrecinctAdvisorData();

	public FedsAdvisorData feds = new FedsAdvisorData();

	public GoonAdvisorData goon = new GoonAdvisorData();

	public AttackAdvisorData attack = new AttackAdvisorData();

	public CombatAdvisorData combat = new CombatAdvisorData();

	private static NPCSettings Settings => Game.serv.globals.settings.npc;

	public PlayerAIData()
	{
	}

	public PlayerAIData(PlayerID pid)
	{
		uint seed = Game.ctx.scenario.MakeSeed(pid);
		TypeUtils.GetMemberInstances<BaseAdvisorData>(this).ForEach(delegate(BaseAdvisorData data)
		{
			data.rng.Init(seed);
		});
	}

	public NPCPersonalityDefinition GetPersonalityDef()
	{
		if (!personality.IsSet)
		{
			return null;
		}
		return Settings.FindPersonalityDef(personality);
	}

	public Label FindPersonalityAspect(List<Label> candidates)
	{
		if (aspects != null)
		{
			int i = 0;
			for (int count = candidates.Count; i < count; i++)
			{
				Label label = candidates[i];
				if (aspects.Contains(label))
				{
					return label;
				}
			}
		}
		return Label.NULL;
	}

	public void InitializePersonality(PlayerInfo player)
	{
		if (!player.IsGangOrGoon)
		{
			return;
		}
		Xorshift rng = Game.ctx.scenario.MakeSeededRng(player.PID);
		if (personality.IsNotSet)
		{
			personality = ChoosePersonality(player, rng);
			aspects = GeneratePersonalityAspects(player, personality, rng);
			if (aspects.Count > 0)
			{
				AILog.LogMilestone(player.PID, EntityID.INVALID, string.Format("{0} got personality = {1}, aspects = {2}", player.PID, personality, string.Join(",", aspects)));
			}
		}
	}

	private static Label ChoosePersonality(PlayerInfo info, IRandom rng)
	{
		if (info.IsJustGoon)
		{
			return ChooseGoonPersonality(info);
		}
		if (info.IsJustGang)
		{
			return ChooseGangPersonality(info, rng);
		}
		return Label.NULL;
	}

	private static Label ChooseGangPersonality(PlayerInfo info, IRandom rng)
	{
		using ListPool<Label>.PooledBlockList pooledBlockList = ListPool<Label>.Allocate();
		pooledBlockList.AddRange(from personality in Settings.personalities.Values
			where personality.npccategory == NPCPersonalityCategory.GangOnly
			select personality.id);
		if (pooledBlockList.Count > 0)
		{
			return rng.PickElement(pooledBlockList);
		}
		return Label.NULL;
	}

	private static Label ChooseGoonPersonality(PlayerInfo info)
	{
		Entity playerPeep = info.social.GetPlayerPeep();
		if (playerPeep == null)
		{
			return Label.NULL;
		}
		TagList traitIds = playerPeep.data.person.traitIds;
		foreach (PersonalityFromTraits personalitiesFromTrait in Settings.personalitiesFromTraits)
		{
			if (personalitiesFromTrait.traits == null || personalitiesFromTrait.traits.ContainsAtLeastOneOf(traitIds))
			{
				return personalitiesFromTrait.givespersonality;
			}
		}
		return Label.NULL;
	}

	private static List<Label> GeneratePersonalityAspects(PlayerInfo player, Label personality, IRandom rng)
	{
		List<Label> list = new List<Label>();
		if (personality.IsNotSet)
		{
			return list;
		}
		NPCPersonalityDefinition nPCPersonalityDefinition = Settings.FindPersonalityDef(personality);
		if (nPCPersonalityDefinition == null)
		{
			return list;
		}
		int num = (int)nPCPersonalityDefinition.aspects.Evaluate(new ModQuery(player.PID));
		if (num <= 0)
		{
			return list;
		}
		List<Label> list2 = new List<Label>(Settings.personalityAspects.Keys);
		for (int i = 0; i < num; i++)
		{
			Label categoryId = rng.PickAndRemoveElement(list2);
			List<NPCPersonalityAspect> list3 = Settings.FindAspectListByCategory(categoryId);
			NPCPersonalityAspect nPCPersonalityAspect = rng.PickElement(list3);
			list.Add(nPCPersonalityAspect.id);
		}
		return list;
	}
}

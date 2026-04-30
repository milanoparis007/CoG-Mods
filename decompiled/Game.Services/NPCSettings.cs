using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using Game.Session.Player.AI;
using SomaSim.Util;

namespace Game.Services;

public sealed class NPCSettings
{
	public RulesRunner fallback;

	public List<NPCScript> scriptdefs;

	public List<NPCDefinition> players;

	public GoonSettings goons;

	public PickColors pickColors;

	public LabelDictionary<AIAdvisorConfig> advisorConfigs;

	public List<PersonalityFromTraits> personalitiesFromTraits;

	public LabelDictionary<NPCPersonalityDefinition> personalities;

	public LabelDictionary<List<NPCPersonalityAspect>> personalityAspects;

	private KeyedListCache<Label, NPCScript> _scriptCache;

	public NPCDefinition FindNPCDefinition(PlayerType type)
	{
		foreach (NPCDefinition player in players)
		{
			if (player.type == type)
			{
				return player;
			}
		}
		return null;
	}

	public NPCScript FindScriptDef(Label scriptId)
	{
		if (_scriptCache == null)
		{
			_scriptCache = new KeyedListCache<Label, NPCScript>(new LabelEqualityComparer(), scriptdefs, (Label label, NPCScript def) => def.id == label);
		}
		return _scriptCache.Get(scriptId);
	}

	public NPCPersonalityDefinition FindPersonalityDef(Label personalityid)
	{
		if (personalities.ContainsKey(personalityid))
		{
			return personalities[personalityid];
		}
		return null;
	}

	public List<NPCPersonalityAspect> FindAspectListByCategory(Label categoryId)
	{
		return personalityAspects.FindOrNullAndWarn(categoryId, "Unknown personality aspect category");
	}

	public Label FindCategoryForAspectLabel(Label aspectId)
	{
		foreach (KeyValuePair<Label, List<NPCPersonalityAspect>> personalityAspect in personalityAspects)
		{
			foreach (NPCPersonalityAspect item in personalityAspect.Value)
			{
				if (item.id == aspectId)
				{
					return personalityAspect.Key;
				}
			}
		}
		return Label.NULL;
	}

	public NPCPersonalityAspect FindAspectByID(Label aspectId)
	{
		Label categoryId = FindCategoryForAspectLabel(aspectId);
		List<NPCPersonalityAspect> list = FindAspectListByCategory(categoryId);
		if (list == null)
		{
			return null;
		}
		foreach (NPCPersonalityAspect item in list)
		{
			if (item.id == aspectId)
			{
				return item;
			}
		}
		return null;
	}

	public T FindAdvisorConfig<T>(Label configid) where T : AIAdvisorConfig
	{
		AIAdvisorConfig aIAdvisorConfig = advisorConfigs.FindOrNull(configid);
		if (aIAdvisorConfig == null)
		{
			return null;
		}
		if (!(aIAdvisorConfig is T result))
		{
			return null;
		}
		return result;
	}
}

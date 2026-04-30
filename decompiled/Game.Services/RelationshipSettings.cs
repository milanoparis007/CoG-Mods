using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class RelationshipSettings
{
	public class SocialActionsList : LabelKeyedList<SocialActionDef>
	{
		public SocialActionsList()
			: base((Func<SocialActionDef, Label>)((SocialActionDef def) => def.id))
		{
		}
	}

	public struct SocialSkipChance
	{
		public SocialLink linktype;

		public float probability;
	}

	public ModValue milestones;

	public ModValue affiliateLevel;

	public RelationshipTradeEffects tradeEffects;

	public SocialActionsList socialActions;

	public List<SocialSkipChance> socialInferenceSkipChance;

	public SocialActionDef GetSocialActionDefinition(Label label)
	{
		return socialActions.FindCachedOrDefault(label);
	}

	public float GetInferenceSkipChance(SocialLink link)
	{
		List<SocialSkipChance> list = socialInferenceSkipChance;
		int i = 0;
		for (int count = socialInferenceSkipChance.Count; i < count; i++)
		{
			if (list[i].linktype == link)
			{
				return list[i].probability;
			}
		}
		return 0f;
	}
}

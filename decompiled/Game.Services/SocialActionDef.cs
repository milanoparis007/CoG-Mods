using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class SocialActionDef
{
	public const int DEFAULT_SOCIAL_ACTION_DAYS = 365;

	public Label id;

	public Label buffid;

	public SocialValence valence;

	public SocialCategory category;

	public string locblurb;

	public string locentry;

	public bool dontinfer;

	public bool flushQuestRequests;

	public List<Label> cancels;

	public int dayz = 365;

	public SocialActionInfo MakeInfoFromDefinition(EntityID entityCtx, SimTime expires)
	{
		return new SocialActionInfo
		{
			started = Game.ctx.clock.Now,
			expires = expires,
			category = category,
			defid = id,
			buffid = buffid,
			valence = valence,
			entityCtx = entityCtx,
			locblurb = locblurb
		};
	}

	public SocialActionInfo MakeInfoFromDefinition(EntityID entityCtx, QuestUUID quest, SimTime expires)
	{
		return new SocialActionInfo
		{
			started = Game.ctx.clock.Now,
			expires = expires,
			category = category,
			defid = id,
			buffid = buffid,
			valence = valence,
			entityCtx = entityCtx,
			questCtx = quest,
			locblurb = locblurb
		};
	}
}

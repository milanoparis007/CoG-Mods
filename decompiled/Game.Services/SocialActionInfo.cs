using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public struct SocialActionInfo
{
	public Label defid;

	public Label buffid;

	public SocialValence valence;

	public SocialCategory category;

	public EntityID entityCtx;

	public QuestUUID questCtx;

	public string locblurb;

	public SimTime started;

	public SimTime expires;

	public static int CompareExpiryAscending(SocialActionInfo a, SocialActionInfo b)
	{
		return a.expires.days - b.expires.days;
	}
}

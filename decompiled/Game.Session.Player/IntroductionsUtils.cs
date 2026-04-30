using Game.Session.Data;
using Game.Session.Entities;

namespace Game.Session.Player;

public static class IntroductionsUtils
{
	public static string MakeRelFlavor(Entity owner, Entity other, string key)
	{
		return ConvoBlurbUtils.LocWithRelationship(owner, other, key, null);
	}
}

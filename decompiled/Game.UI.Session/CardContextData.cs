using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.UI.Session;

internal sealed class CardContextData
{
	public Entity cardPeep;

	public PersonInfoUtil.Overview overview;

	public Relationship rel;

	public bool isAlive;

	public bool isAdult;

	public bool isKnownByHuman;

	public bool ShouldShow(ConnFilter filter)
	{
		if (!isKnownByHuman && filter != ConnFilter.Debug)
		{
			return false;
		}
		switch (filter)
		{
		case ConnFilter.Debug:
			return true;
		case ConnFilter.None:
			return true;
		case ConnFilter.Family:
			return rel.IsAnyFamily;
		case ConnFilter.Friends:
			return !rel.IsAnyFamily;
		default:
		{
			PlayerInfo playerInfo = rel.to.FindEntity()?.data.agent?.pid.FindPlayer();
			if (playerInfo != null)
			{
				switch (filter)
				{
				case ConnFilter.Gangs:
					return playerInfo.IsJustGang;
				case ConnFilter.Goons:
					return playerInfo.IsJustGoon;
				case ConnFilter.Cops:
					return playerInfo.IsCopOrFed;
				}
			}
			return false;
		}
		}
	}

	public bool CanInteract(ConnFilter filter)
	{
		if (isAlive && isAdult)
		{
			if (overview.workplace == null || !isKnownByHuman)
			{
				return filter == ConnFilter.Debug;
			}
			return true;
		}
		return false;
	}
}

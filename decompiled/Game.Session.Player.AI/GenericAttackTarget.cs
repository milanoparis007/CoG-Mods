using Game.Core;

namespace Game.Session.Player.AI;

public struct GenericAttackTarget
{
	public PlayerID enemy;

	public EntityID building;

	public bool IsValid
	{
		get
		{
			if (building.IsValid)
			{
				return enemy.IsValid;
			}
			return false;
		}
	}
}

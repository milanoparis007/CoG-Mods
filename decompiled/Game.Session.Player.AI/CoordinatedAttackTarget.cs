using Game.Core;

namespace Game.Session.Player.AI;

public struct CoordinatedAttackTarget
{
	public PlayerID enemy;

	public EntityID rallyPoint;

	public bool IsValid
	{
		get
		{
			if (rallyPoint.IsValid)
			{
				return enemy.IsValid;
			}
			return false;
		}
	}
}

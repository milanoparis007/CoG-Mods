using Game.Core;

namespace Game.Session.Player.AI;

public struct OutpostToSteal
{
	public PlayerID playerId;

	public OutpostID outpostId;

	public bool isEnemy;

	public OutpostToSteal(PlayerID playerId, OutpostID outpostId, bool isEnemy)
	{
		this = default(OutpostToSteal);
		this.playerId = playerId;
		this.outpostId = outpostId;
		this.isEnemy = isEnemy;
	}

	public override string ToString()
	{
		return $"{playerId}/{outpostId}";
	}
}

using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

public class CoordinatedAttackState
{
	public PlayerID targetPlayer;

	public List<EntityID> attackingCrew;

	public EntityID rallyPoint;

	public NodeID rallyPointNodeId;

	public SimTime rallyExpires;

	public CoordinatedAttackState()
	{
	}

	public CoordinatedAttackState(PlayerID target, EntityID rallyPoint, int turns)
	{
		targetPlayer = target;
		this.rallyPoint = rallyPoint;
		rallyPointNodeId = rallyPoint.FindEntity().components.board.GetNodeID();
		rallyExpires = Game.ctx.clock.Now.IncrementTurns(turns);
		attackingCrew = null;
	}
}

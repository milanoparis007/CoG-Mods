using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

public sealed class HumanAtLocation : IRuleCondition
{
	public bool Satisfied(Entity entity, PlayerID pid)
	{
		NodeID nid = entity.data.agent.nid;
		foreach (EntityID item in Game.ctx.transit.GetAllAgentsAtNodeUnsafe(nid))
		{
			if (!(item == entity.Id))
			{
				PlayerID pid2 = item.FindEntity().data.agent.pid;
				if (pid2.IsHumanPlayer)
				{
					return true;
				}
			}
		}
		return false;
	}
}

using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

public sealed class LocationNotSafehouse : IRuleCondition
{
	public bool Satisfied(Entity entity, PlayerID pid)
	{
		Node node = entity.data.agent.nid.FindNode();
		Node headquartersNode = pid.FindPlayer().territory.GetHeadquartersNode();
		return node != headquartersNode;
	}
}

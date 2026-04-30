using Game.Core;
using Game.Session.Board;

namespace Game.Session.Player.Commands;

internal class AICommandExploreNode : InstantAICommand
{
	public NodeID nodeId;

	public AICommandExploreNode()
	{
	}

	public AICommandExploreNode(PlayerID pid, EntityID eid, NodeID nodeId)
		: base(pid, CommandType.ExploreNode, eid)
	{
		this.nodeId = nodeId;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		Node node = nodeId.FindNode();
		GetPlayer().meetings.MarkNodeAsKnown(node, expectedSeen: true, instant: true);
		ConsumePeepActions();
	}
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class CreateTrainStationLocations
{
	private SetupOrchestratorContext _ctx;

	internal CreateTrainStationLocations(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	public IEnumerator Start()
	{
		List<Node> nodes = (from node in Game.ctx.board.nodes.GetAllNodesUnsafe()
			where node.HasTerminal
			select FindNearbyRoadNode(node)).WhereNotNull().ToList();
		_ctx.trainStationData = new SpecialBuildingLocationData(nodes);
		yield return null;
	}

	private Node FindNearbyRoadNode(Node terminalNode)
	{
		Node result = null;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(terminalNode, 50, delegate(Node n)
		{
			if (n.HasRoad && n.IsOnGround)
			{
				result = n;
			}
		}, null, null, (Node n) => result != null);
		return result;
	}
}

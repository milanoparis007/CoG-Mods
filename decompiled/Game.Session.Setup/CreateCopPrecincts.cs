using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Setup;

public class CreateCopPrecincts
{
	private const int ITERATIONS_PER_FRAME = 1000;

	private static Dictionary<Entity, Deque<Node>> queues = new Dictionary<Entity, Deque<Node>>();

	private SetupOrchestratorContext _ctx;

	internal CreateCopPrecincts(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	public IEnumerator Start()
	{
		foreach (Entity item in _ctx.copStationData.placed)
		{
			Game.ctx.simman.cops.InformOfStationInstallation(item);
		}
		using ListPool<Entity>.PooledBlockList stations = ListPool<Entity>.Allocate();
		foreach (EntityID stationID in Game.ctx.simman.cops.data.stationIDs)
		{
			stations.Add(stationID.FindEntity());
		}
		foreach (Entity item2 in stations)
		{
			queues[item2] = new Deque<Node>();
			queues[item2].AddFirst(item2.components.board.GetNode());
		}
		int iterations = 0;
		bool anyleft = true;
		while (anyleft)
		{
			anyleft = false;
			foreach (Entity item3 in stations)
			{
				anyleft = SpreadStationPrecinct(item3, queues[item3]) || anyleft;
				int num = iterations + 1;
				iterations = num;
				if (num % 1000 == 0)
				{
					yield return null;
				}
			}
		}
	}

	private static bool SpreadStationPrecinct(Entity station, Deque<Node> queue)
	{
		if (queue.Count == 0)
		{
			return false;
		}
		Node node = queue.RemoveFirst();
		if (node.precinctId.IsValid)
		{
			return true;
		}
		node.precinctId = station.data.police.precinctID;
		for (Direction direction = Direction.N; direction <= Direction.W; direction++)
		{
			NodeEdge nodeEdge = node.GetEdgeID(direction).FindEdge();
			if (nodeEdge != null && !nodeEdge.IsNotValid && nodeEdge.IsRoad)
			{
				Node node2 = node.FindNeighbor(direction);
				if (node2.IsValid)
				{
					queue.AddLast(node2);
				}
			}
		}
		return true;
	}
}

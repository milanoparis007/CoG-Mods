using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Sim;

namespace Game.Session.Setup;

internal sealed class CreateIslandConnections
{
	private class IslandData
	{
		public List<NodeIsland> islands = new List<NodeIsland>();
	}

	private class NodeIsland
	{
		public int id;

		public List<Node> nodes;

		public bool allowDisconnected;
	}

	private SetupOrchestratorContext _ctx;

	private NodeManager _nodeManager;

	private TransitManager _transit;

	public CreateIslandConnections(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_nodeManager = Game.ctx.board.nodes;
		_transit = Game.ctx.transit;
	}

	private IEnumerator GenerateIslands(IslandData data)
	{
		List<Node> allNodesUnsafe = _nodeManager.GetAllNodesUnsafe();
		List<Node> masterList = new List<Node>(allNodesUnsafe);
		int islandIndex = 0;
		while (masterList.Count > 0)
		{
			Node node = masterList[0];
			bool flag = false;
			while (!node.HasRoad)
			{
				masterList.Remove(node);
				if (masterList.Count == 0)
				{
					flag = true;
					break;
				}
				node = masterList[0];
			}
			if (flag)
			{
				break;
			}
			NodeIsland nodeIsland = new NodeIsland();
			nodeIsland.id = islandIndex;
			nodeIsland.nodes = new List<Node>();
			GetNeighbors(node, ref nodeIsland.nodes, ref masterList, islandIndex);
			nodeIsland.allowDisconnected = nodeIsland.nodes.Any((Node n) => n.procData.disconnectedIslandRoot);
			data.islands.Add(nodeIsland);
			islandIndex++;
			yield return null;
		}
		data.islands.Sort((NodeIsland first, NodeIsland second) => second.nodes.Count.CompareTo(first.nodes.Count));
	}

	private void GetNeighbors(Node testNode, ref List<Node> foundNodes, ref List<Node> masterList, int currentIslandIndex)
	{
		testNode.roadIslandId = currentIslandIndex;
		foundNodes.Add(testNode);
		masterList.Remove(testNode);
		Direction[] aLL_DIRECTIONS = DirectionUtil.ALL_DIRECTIONS;
		foreach (Direction dir in aLL_DIRECTIONS)
		{
			NodeEdge nodeEdge = testNode.GetEdgeID(dir).FindEdge();
			if (nodeEdge != null && nodeEdge.HasTransitType(TransitFlags.Road))
			{
				Node node = _nodeManager.GetNode(nodeEdge.GetOtherNodeID(testNode.id));
				if (!foundNodes.Contains(node) && node.HasRoad)
				{
					GetNeighbors(node, ref foundNodes, ref masterList, currentIslandIndex);
				}
			}
		}
	}

	public IEnumerator Start()
	{
		IslandData islandData = new IslandData();
		yield return GenerateIslands(islandData);
		List<int> list = new List<int>();
		foreach (NodeEdge skippedEdge in _ctx.roadNetworkData.skippedEdges)
		{
			if (!skippedEdge.IsTransitEmpty)
			{
				continue;
			}
			Node node = _nodeManager.GetNode(skippedEdge.a);
			Node node2 = _nodeManager.GetNode(skippedEdge.b);
			if (node.HasValidRoadIsland && node2.HasValidRoadIsland && node.roadIslandId != node2.roadIslandId)
			{
				int item = (1 << node.roadIslandId) | (1 << node2.roadIslandId);
				if (!list.Contains(item))
				{
					_transit.AddTransitFlagsAlong(skippedEdge, TransitFlags.Road);
					list.Add(item);
				}
			}
		}
		if (islandData.islands.Count > 1)
		{
			for (int i = 1; i < islandData.islands.Count; i++)
			{
				foreach (Node node3 in islandData.islands[i].nodes)
				{
					Direction[] aLL_DIRECTIONS = DirectionUtil.ALL_DIRECTIONS;
					foreach (Direction direction in aLL_DIRECTIONS)
					{
						if (!node3.GetEdgeID(direction).IsNotValid)
						{
							continue;
						}
						WorldPos worldPos = node3.GenerateDirectionDelta(direction);
						WorldPos pos = node3.pos + worldPos;
						List<Node> list2 = _nodeManager.FindAndSortNodesInRadius(pos, 10f).FindAll((Node testNode) => testNode.HasValidRoadIsland && node3.roadIslandId != testNode.roadIslandId);
						Node node4 = null;
						Direction bDirection = Direction.E;
						foreach (Node item3 in list2)
						{
							Direction[] aLL_DIRECTIONS2 = DirectionUtil.ALL_DIRECTIONS;
							foreach (Direction direction2 in aLL_DIRECTIONS2)
							{
								if (item3.GetEdgeID(direction2).IsNotValid && CreateGridConnections.CanBridgeGridConnections(node3, direction, item3, direction2) && CreateGridConnections.IsAllLandBetween(_ctx.heightmapData, node3, item3, direction))
								{
									node4 = item3;
									bDirection = direction2;
									break;
								}
							}
						}
						if (node4 != null)
						{
							int item2 = (1 << node3.roadIslandId) | (1 << node4.roadIslandId);
							if (!list.Contains(item2))
							{
								_nodeManager.AddEdgeSpecial(node3, node4, direction, bDirection);
								NodeEdge edgeInDirection = _nodeManager.GetEdgeInDirection(node3, direction);
								_transit.AddTransitFlagsAlong(edgeInDirection, TransitFlags.Road);
								list.Add(item2);
							}
						}
					}
				}
			}
		}
		IslandData newIslandData = new IslandData();
		yield return GenerateIslands(newIslandData);
		if (newIslandData.islands.Count <= 1)
		{
			yield break;
		}
		for (int num2 = 1; num2 < newIslandData.islands.Count; num2++)
		{
			NodeIsland nodeIsland = newIslandData.islands[num2];
			if (nodeIsland.allowDisconnected)
			{
				continue;
			}
			foreach (Node node5 in nodeIsland.nodes)
			{
				Direction[] aLL_DIRECTIONS = DirectionUtil.ALL_DIRECTIONS;
				foreach (Direction dir in aLL_DIRECTIONS)
				{
					NodeEdge edgeInDirection2 = _nodeManager.GetEdgeInDirection(node5, dir);
					if (edgeInDirection2 != null && edgeInDirection2.HasTransitType(TransitFlags.Road))
					{
						_transit.ClearTransitFlagsAlong(edgeInDirection2, TransitFlags.Road);
					}
				}
			}
		}
	}
}

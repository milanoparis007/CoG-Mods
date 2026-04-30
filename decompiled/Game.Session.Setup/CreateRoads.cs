using System.Collections;
using Game.Core;
using Game.Session.Board;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Setup;

internal sealed class CreateRoads
{
	private const int ROAD_MARGIN_FROM_EDGE = 0;

	public const float ROAD_CHANCE = 0.6f;

	public const float BRIDGE_CHANCE = 0.3f;

	public const float RAIL_CROSSING_CHANCE = 0.6f;

	public const int MAX_BRIDGE_LENGTH = 8;

	private Xorshift _rng;

	private BoardManager _board;

	private NodeManager _nodes;

	private TransitManager _transit;

	private SetupOrchestratorContext _ctx;

	public CreateRoads(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_ctx.roadNetworkData = new RoadNetworkData();
	}

	internal IEnumerator Start()
	{
		_rng = Game.ctx.scenario.MakeSeededRng<CreateRoads>();
		_board = Game.ctx.board;
		_nodes = _board.nodes;
		_transit = Game.ctx.transit;
		ConnectRoads();
		AddRailCrossings();
		yield return null;
	}

	private void ConnectRoads()
	{
		IntSize mapSize = _board.MapConfig.mapSize;
		foreach (Node item in _nodes.GetAllNodesUnsafe())
		{
			if (IsValidRoadNode(mapSize, item.pos))
			{
				Direction[] aLL_DIRECTIONS = DirectionUtil.ALL_DIRECTIONS;
				foreach (Direction dir in aLL_DIRECTIONS)
				{
					TryConnectNode(item, dir);
				}
			}
		}
	}

	private bool IsValidRoadNode(IntSize map, WorldPos pos)
	{
		int num = 0;
		int width = map.width;
		int num2 = 0;
		int height = map.height;
		if (pos.x > (float)num && pos.x < (float)width && pos.y > (float)num2)
		{
			return pos.y < (float)height;
		}
		return false;
	}

	private void TryConnectNode(Node a, Direction dir)
	{
		if (a == null)
		{
			return;
		}
		Node neighborOrNull = _nodes.GetNeighborOrNull(a, dir);
		if (neighborOrNull == null)
		{
			return;
		}
		NodeEdge edgeToTarget = _nodes.GetEdgeToTarget(a, neighborOrNull);
		if (NodesOnEdgeCollideWithTransit(a, neighborOrNull))
		{
			return;
		}
		edgeToTarget.roadBeads.Find((RoadBead bead) => !bead.canMakeBridge);
		bool num = a.IsOnGround && (a.HasRoad || a.HasNoTransit);
		bool flag = neighborOrNull.IsOnGround && (neighborOrNull.HasRoad || neighborOrNull.HasNoTransit);
		if (num && flag)
		{
			float probability = ((a.procData.dontSkipRoads && neighborOrNull.procData.dontSkipRoads) ? 1f : 0.6f);
			if (_rng.CheckProbability(probability))
			{
				_transit.AddTransitFlagsAlong(edgeToTarget, TransitFlags.Road);
			}
			else
			{
				_ctx.roadNetworkData.skippedEdges.Add(edgeToTarget);
			}
		}
		bool num2 = a.IsOnGround && a.HasRoad;
		bool flag2 = neighborOrNull.IsOnWater && neighborOrNull.HasNoTransit;
		bool flag3 = num2 && flag2;
		if ((flag3 && _rng.CheckProbability(0.3f)) || (flag3 && a.procData.forceBridges))
		{
			int num3 = TryConnectBridge(a, dir, 8);
			if (num3 > 0)
			{
				DoConnectBridge(a, dir, num3);
			}
		}
	}

	private bool NodesOnEdgeCollideWithTransit(Node a, Node b)
	{
		foreach (Node item in _nodes.FindAndSortNodesInRadius(a.pos, 10f))
		{
			NodeEdgeID[] edges = item.edges;
			for (int i = 0; i < edges.Length; i++)
			{
				NodeEdgeID id = edges[i];
				if (id.IsNotValid)
				{
					continue;
				}
				NodeEdge edge = _nodes.GetEdge(id);
				if (edge.IsRoad || edge.IsRail)
				{
					Node node = _nodes.GetNode(edge.a);
					Node node2 = _nodes.GetNode(edge.b);
					if (DoEdgesIntersect(a.pos, b.pos, node.pos, node2.pos))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private bool DoEdgesIntersect(WorldPos p1, WorldPos p2, WorldPos p3, WorldPos p4)
	{
		float num = p2.x - p1.x;
		float num2 = p2.y - p1.y;
		float num3 = p4.x - p3.x;
		float num4 = p4.y - p3.y;
		float num5 = num * num4 - num2 * num3;
		if (num5 == 0f)
		{
			return false;
		}
		float num6 = p3.x - p1.x;
		float num7 = p3.y - p1.y;
		float num8 = (num6 * num4 - num7 * num3) / num5;
		float num9 = (num6 * num2 - num7 * num) / num5;
		if (num8 < 0.01f || num8 > 0.99f || num9 < 0.01f || num9 > 0.99f)
		{
			return false;
		}
		return true;
	}

	private int TryConnectBridge(Node startNode, Direction dir, int max)
	{
		if (max <= 0)
		{
			return -1;
		}
		Node neighborOrNull = _nodes.GetNeighborOrNull(startNode, dir);
		if (neighborOrNull == null)
		{
			return -1;
		}
		if (neighborOrNull.IsOnGround && (neighborOrNull.HasNoTransit || neighborOrNull.HasRoad))
		{
			return 1;
		}
		if (neighborOrNull.IsOnGround || neighborOrNull.HasAnyTransit)
		{
			return -1;
		}
		int num = TryConnectBridge(neighborOrNull, dir, max - 1);
		if (num >= 0)
		{
			return num + 1;
		}
		return num;
	}

	private void DoConnectBridge(Node startNode, Direction dir, int length)
	{
		for (int i = 0; i < length; i++)
		{
			NodeEdge edgeInDirection = _nodes.GetEdgeInDirection(startNode, dir);
			_transit.AddTransitFlagsAlong(edgeInDirection, TransitFlags.Road);
			startNode = _board.nodes.GetNeighborOrNull(startNode, dir);
		}
	}

	private bool TestCanConnectBridge(Node startNode, Direction dir, int length)
	{
		for (int i = 0; i < length; i++)
		{
			NodeEdge edgeInDirection = _nodes.GetEdgeInDirection(startNode, dir);
			_transit.AddTransitFlagsAlong(edgeInDirection, TransitFlags.Road);
			startNode = _board.nodes.GetNeighborOrNull(startNode, dir);
		}
		return true;
	}

	private void AddRailCrossings()
	{
		foreach (Node item in _nodes.GetAllNodesUnsafe())
		{
			TryRailCrossing(item, Direction.E);
			TryRailCrossing(item, Direction.N);
		}
	}

	private bool TryRailCrossing(Node a, Direction direction)
	{
		if (a == null || !a.HasRoad)
		{
			return false;
		}
		Node neighborOrNull = _nodes.GetNeighborOrNull(a, direction);
		if (neighborOrNull == null || neighborOrNull.transit != TransitFlags.Rail)
		{
			return false;
		}
		Node neighborOrNull2 = _nodes.GetNeighborOrNull(neighborOrNull, direction);
		if (neighborOrNull2 == null || neighborOrNull2.HasRail || !neighborOrNull2.HasRoad)
		{
			return false;
		}
		NodeEdge edgeInDirection = _nodes.GetEdgeInDirection(a, direction);
		if (edgeInDirection != null && (!edgeInDirection.IsTransitEmpty || a.IsOnWater || neighborOrNull.IsOnWater))
		{
			return false;
		}
		NodeEdge edgeInDirection2 = _nodes.GetEdgeInDirection(neighborOrNull, direction);
		if (edgeInDirection2 != null && (!edgeInDirection2.IsTransitEmpty || neighborOrNull.IsOnWater || neighborOrNull2.IsOnWater))
		{
			return false;
		}
		if (!_rng.CheckProbability(0.6f))
		{
			return false;
		}
		_transit.AddTransitFlagsAt(neighborOrNull, TransitFlags.Road);
		_transit.AddTransitFlagsAlong(edgeInDirection, TransitFlags.Road);
		_transit.AddTransitFlagsAlong(edgeInDirection2, TransitFlags.Road);
		return true;
	}
}

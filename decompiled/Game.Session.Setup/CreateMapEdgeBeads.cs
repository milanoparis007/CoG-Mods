using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using SomaSim.Util;

namespace Game.Session.Setup;

internal sealed class CreateMapEdgeBeads
{
	private const int ITERATIONS_PER_FRAME = 200;

	private NodeManager _manager;

	private IntSize _beadSpacing;

	private SetupOrchestratorContext _ctx;

	public CreateMapEdgeBeads(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_ctx.transitTileData = new TransitTileData();
	}

	public IEnumerator Start()
	{
		_manager = Game.ctx.board.nodes;
		_beadSpacing = Game.ctx.board.MapConfig.beadSpacing;
		int iter = 0;
		foreach (NodeEdge item in _manager.GetAllEdgesUnsafe())
		{
			if (!item.beadsCreated)
			{
				if (item.gridConnection)
				{
					AddBeadsToEdgeForGridConnection(item);
				}
				else if (item.isBridge)
				{
					DoCreateBridgeBeads(item, firstBridge: true);
				}
				else
				{
					AddBeadsToEdge(item);
				}
				int num = iter + 1;
				iter = num;
				if (num % 200 == 0)
				{
					yield return null;
				}
			}
		}
	}

	private RoadBeadPlacement RailBeadsForStartingNotEnding(int beadIndex, int totalBeads, bool isRail)
	{
		if (beadIndex == 1)
		{
			if (!isRail)
			{
				return RoadBeadPlacement.RoadBridgeStart;
			}
			return RoadBeadPlacement.RailBridgeStart;
		}
		if (!isRail)
		{
			return RoadBeadPlacement.RoadBridgeNormal;
		}
		return RoadBeadPlacement.RailBridgeNormal;
	}

	private RoadBeadPlacement RailBeadsForEndingNotStarting(int beadIndex, int totalBeads, bool isRail)
	{
		if (beadIndex == totalBeads - 1)
		{
			if (!isRail)
			{
				return RoadBeadPlacement.RoadBridgeStart;
			}
			return RoadBeadPlacement.RailBridgeStart;
		}
		if (!isRail)
		{
			return RoadBeadPlacement.RoadBridgeNormal;
		}
		return RoadBeadPlacement.RailBridgeNormal;
	}

	private RoadBeadPlacement RailBeadsForNoStartOrEnd(int beadIndex, int totalBeads, bool isRail)
	{
		if (!isRail)
		{
			return RoadBeadPlacement.RoadBridgeNormal;
		}
		return RoadBeadPlacement.RailBridgeNormal;
	}

	private RoadBeadPlacement RailBeadsForStartAndEnd(int beadIndex, int totalBeads, bool isRail)
	{
		if ((float)beadIndex < (float)totalBeads / 2f)
		{
			return RailBeadsForStartingNotEnding(beadIndex, totalBeads, isRail);
		}
		return RailBeadsForEndingNotStarting(beadIndex, totalBeads, isRail);
	}

	private void AddBeadsToEdge(NodeEdge edge)
	{
		if (edge.IsValid)
		{
			Node node = _manager.GetNode(edge.a);
			Node node2 = _manager.GetNode(edge.b);
			WorldPos normalized = (node2.pos - node.pos).Normalized;
			float magnitude = (node2.pos - node.pos).Magnitude;
			int inDirection = _beadSpacing.GetInDirection(edge.abDir);
			int num = (int)Math.Round(magnitude / (float)inDirection);
			int capacity = num - 1;
			edge.roadBeads = new List<RoadBead>(capacity);
			for (int i = 1; i < num; i++)
			{
				WorldPos pos = node.pos + normalized * i;
				RoadBeadPlacement placement = ((edge.IsRail || MathUtil.IsEven(i)) ? RoadBeadPlacement.NormalTile : RoadBeadPlacement.None);
				RoadBead item = new RoadBead(new GridTransform
				{
					pos = pos,
					deg = node.deg
				}, placement);
				edge.roadBeads.Add(item);
			}
			if (edge.IsRail)
			{
				_ctx.transitTileData.railEdges.Add(edge);
			}
			int num2 = num + 3;
			edge.lotBeads = new List<LotBead>(num2);
			for (int j = -1; j < num2 - 1; j++)
			{
				WorldPos pos2 = node.pos + normalized * j;
				Node node3 = NodeEdge.PickCloserNode(node, node2, pos2);
				LotBead item2 = new LotBead(new GridTransform(pos2, node.deg), node3.id);
				edge.lotBeads.Add(item2);
			}
			edge.beadsCreated = true;
		}
	}

	private void DoCreateBridgeBeads(NodeEdge edge, bool firstBridge)
	{
		if (!edge.IsValid || edge.beadsCreated)
		{
			return;
		}
		Node node = _manager.GetNode(edge.a);
		Node node2 = _manager.GetNode(edge.b);
		if (firstBridge)
		{
			NodeEdgeID edgeID = node.GetEdgeID(DirectionUtil.GetOppositeDirection(edge.abDir));
			if (edgeID.IsValid && _manager.GetEdge(edgeID).isBridge)
			{
				return;
			}
		}
		bool flag = false;
		Direction oppositeDirection = DirectionUtil.GetOppositeDirection(edge.baDir);
		NodeEdgeID edgeID2 = node2.GetEdgeID(oppositeDirection);
		if (node2.IsOnWater && edgeID2.IsValid)
		{
			flag = _manager.GetEdge(edgeID2).isBridge;
		}
		WorldPos normalized = (node2.pos - node.pos).Normalized;
		float magnitude = (node2.pos - node.pos).Magnitude;
		int inDirection = _beadSpacing.GetInDirection(edge.abDir);
		int num = (int)Math.Round(magnitude / (float)inDirection);
		if (firstBridge && !flag)
		{
			num--;
		}
		if (!firstBridge && flag)
		{
			num++;
		}
		for (int i = 1; i < num; i++)
		{
			WorldPos pos = node.pos + normalized * i;
			RoadBeadPlacement roadBeadPlacement = (MathUtil.IsEven(i) ? RoadBeadPlacement.NormalTile : RoadBeadPlacement.None);
			if (node.terraintype == TerrainType.Ground && node2.terraintype == TerrainType.Water)
			{
				roadBeadPlacement = RailBeadsForStartingNotEnding(i, num, edge.IsRail);
			}
			else if (node.terraintype == TerrainType.Water && node2.terraintype == TerrainType.Ground)
			{
				roadBeadPlacement = RailBeadsForEndingNotStarting(i, num, edge.IsRail);
			}
			else if (node.terraintype == TerrainType.Water && node2.terraintype == TerrainType.Water)
			{
				roadBeadPlacement = RailBeadsForNoStartOrEnd(i, num, edge.IsRail);
			}
			else if (node.terraintype == TerrainType.Ground || node2.terraintype == TerrainType.Ground)
			{
				roadBeadPlacement = RailBeadsForStartAndEnd(i, num, edge.IsRail);
			}
			if (firstBridge && i == 2 && roadBeadPlacement == RoadBeadPlacement.RoadBridgeNormal)
			{
				roadBeadPlacement = RoadBeadPlacement.RoadBridgeSlope;
			}
			else if (!flag && i == num - 2 && roadBeadPlacement == RoadBeadPlacement.RoadBridgeNormal)
			{
				roadBeadPlacement = RoadBeadPlacement.RoadBridgeSlope;
			}
			if (firstBridge)
			{
				pos += normalized * 0.5f;
			}
			else
			{
				pos -= normalized * 0.5f;
			}
			BridgeBead item = new BridgeBead(new GridTransform
			{
				pos = pos,
				deg = node.deg
			}, roadBeadPlacement);
			edge.bridgeBeads.Add(item);
		}
		edge.beadsCreated = true;
		edge.lotBeads = new List<LotBead>();
		if (flag)
		{
			NodeEdge edge2 = _manager.GetEdge(edgeID2);
			DoCreateBridgeBeads(edge2, firstBridge: false);
		}
	}

	private void AddBeadsToEdgeForGridConnection(NodeEdge edge)
	{
		if (edge.IsValid)
		{
			Node node = _manager.GetNode(edge.a);
			Node node2 = _manager.GetNode(edge.b);
			WorldPos worldPos = node.pos + node.GenerateDirectionDelta(edge.abDir);
			WorldPos worldPos2 = node2.pos + node2.GenerateDirectionDelta(edge.baDir) - worldPos;
			WorldPos normalized = worldPos2.Normalized;
			float magnitude = worldPos2.Magnitude;
			int inDirection = _beadSpacing.GetInDirection(edge.abDir);
			int num = (int)Math.Round(magnitude / (float)inDirection);
			int capacity = MathUtil.ClampMin(num - 1, 0);
			edge.roadBeads = new List<RoadBead>(capacity);
			for (int i = 0; i < num; i++)
			{
				WorldPos pos = worldPos + normalized * i;
				RoadBeadPlacement placement = (MathUtil.IsEven(i) ? RoadBeadPlacement.NormalTile : RoadBeadPlacement.None);
				RoadBead item = new RoadBead(new GridTransform
				{
					pos = pos,
					deg = node.deg
				}, placement);
				edge.roadBeads.Add(item);
			}
			int num2 = num + 1;
			edge.lotBeads = new List<LotBead>(num2);
			for (int j = 0; j < num2; j++)
			{
				WorldPos pos2 = worldPos + normalized * j;
				Node node3 = NodeEdge.PickCloserNode(node, node2, pos2);
				LotBead item2 = new LotBead(new GridTransform(pos2, node.deg), node3.id);
				edge.lotBeads.Add(item2);
			}
		}
	}
}

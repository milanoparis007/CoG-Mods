using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using UnityEngine;

namespace Game.Session.Setup;

internal class CreateGridConnections
{
	private const int ITERATIONS_PER_FRAME = 20;

	private List<Node> _nodesBridged = new List<Node>();

	private NodeManager _nodes;

	private SetupOrchestratorContext _ctx;

	public CreateGridConnections(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_ctx.specialEdgeData = new SpecialEdgeData();
	}

	public IEnumerator Start()
	{
		_nodes = Game.ctx.board.nodes;
		int i = 0;
		foreach (NodeBridgeAttempt bridgeAttempt in _ctx.gridConnectionData.bridgeAttempts)
		{
			TryCreateBridges(bridgeAttempt);
			int num = i + 1;
			i = num;
			if (num % 20 == 0)
			{
				yield return null;
			}
		}
	}

	private void TryCreateBridges(NodeBridgeAttempt bridgeAttempt)
	{
		if (bridgeAttempt.node.GetEdgeID(bridgeAttempt.direction).IsValid)
		{
			return;
		}
		Node node = bridgeAttempt.node;
		Node destinationNode = bridgeAttempt.destinationNode;
		if (_nodesBridged.Contains(bridgeAttempt.node) || _nodesBridged.Contains(bridgeAttempt.destinationNode))
		{
			return;
		}
		Direction[] aLL_DIRECTIONS = DirectionUtil.ALL_DIRECTIONS;
		foreach (Direction direction in aLL_DIRECTIONS)
		{
			if (destinationNode.GetEdgeID(direction).IsNotValid && CanBridgeGridConnections(node, bridgeAttempt.direction, destinationNode, direction) && IsAllLandBetween(_ctx.heightmapData, node, destinationNode, bridgeAttempt.direction))
			{
				NodeEdge nodeEdge = _nodes.AddEdgeSpecial(node, destinationNode, bridgeAttempt.direction, direction);
				_nodesBridged.Add(node);
				_nodesBridged.Add(destinationNode);
				if (nodeEdge != null)
				{
					_ctx.specialEdgeData.specialEdges.Add(nodeEdge);
				}
			}
		}
	}

	public static bool IsAllLandBetween(HeightmapData heighmapData, Node nodeA, Node nodeB, Direction dir)
	{
		WorldPos worldPos = nodeB.pos - nodeA.pos;
		float magnitude = worldPos.Magnitude;
		worldPos = worldPos.Normalized;
		int inDirection = Game.ctx.board.MapConfig.beadSpacing.GetInDirection(dir);
		int num = (int)Math.Round(magnitude / (float)inDirection);
		for (int i = 1; i < num; i++)
		{
			WorldPos pos = nodeA.pos + worldPos * i;
			if (heighmapData.GetTerrainType(pos) != TerrainType.Ground)
			{
				return false;
			}
		}
		return true;
	}

	public static bool CanBridgeGridConnections(Node nodeA, Direction dirA, Node nodeB, Direction dirB)
	{
		Vector3 normalized = nodeA.GenerateDirectionDelta(dirA).AsVector3XZ.normalized;
		Vector3 normalized2 = nodeB.GenerateDirectionDelta(dirB).AsVector3XZ.normalized;
		Vector3 normalized3 = (nodeB.pos.AsVector3XZ - nodeA.pos.AsVector3XZ).normalized;
		float num = Vector3.Dot(normalized, normalized2);
		float num2 = Vector3.Dot(normalized, normalized3);
		float num3 = Vector3.Dot(normalized2, -normalized3);
		if (num < 0f && num2 > 0.8f && num3 > 0.8f)
		{
			return true;
		}
		return false;
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Board;
using Game.Session.Sim;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal sealed class CreateRail
{
	internal struct RailSideData
	{
		public Direction side;

		public float distance;

		public RailSideData(Direction dir, float dist)
		{
			side = dir;
			distance = dist;
		}
	}

	private class TrainPathContext : IPathContext
	{
		private BoardManager _board;

		private HashSet<NodeID> _excludedNodes;

		private TrainPathSettings _path;

		public TrainPathContext(BoardManager board, HashSet<NodeID> excludedNodes)
		{
			_board = board;
			_excludedNodes = excludedNodes;
			_path = Game.serv.globals.settings.general.trains.path;
		}

		public void OnSearchStart(PlayerID pid, EntityID eid)
		{
		}

		public void OnSearchEnd()
		{
		}

		public Fixnum LeastCostEstimate(PlayerID pid, Node current, Node target)
		{
			return new Fixnum((current.pos - target.pos).Magnitude * _path.defaultCost * _path.sharedTrackCostMultiplier);
		}

		private bool IsExcluded(Node node)
		{
			if (_excludedNodes != null)
			{
				return _excludedNodes.Contains(node.id);
			}
			return false;
		}

		public void GetNeighbors(PathElement pathSoFar, List<PathContextNeighbor> outNeighbors)
		{
			Node node = pathSoFar.target.node;
			bool flag = pathSoFar.edge?.IsVertical ?? false;
			Direction[] aLL_DIRECTIONS = DirectionUtil.ALL_DIRECTIONS;
			foreach (Direction dir in aLL_DIRECTIONS)
			{
				NodeEdge edgeInDirection = _board.nodes.GetEdgeInDirection(node, dir);
				Node node2 = edgeInDirection?.FindOtherNode(node);
				if (node2 == null || !IsValidTrackNode(node2) || IsExcluded(node2))
				{
					continue;
				}
				float num = _path.defaultCost;
				if (flag != edgeInDirection.IsVertical || edgeInDirection.gridConnection)
				{
					if (node.IsOnWater)
					{
						continue;
					}
					num += _path.headingChangePenalty;
				}
				if (node2.HasRail)
				{
					num *= _path.sharedTrackCostMultiplier;
				}
				num *= Math.Min(node2.procData.railCost, node.procData.railCost);
				outNeighbors.Add(new PathContextNeighbor(node2, edgeInDirection, new Fixnum(num)));
			}
		}
	}

	private Xorshift _rng;

	private BoardManager _board;

	private TransitManager _transit;

	private Pathfinding _pather;

	private TrainSettings _trains;

	public IEnumerator Start()
	{
		_rng = Game.ctx.scenario.MakeSeededRng<CreateRail>();
		_transit = Game.ctx.transit;
		_board = Game.ctx.board;
		_pather = new Pathfinding();
		_trains = Game.serv.globals.settings.general.trains;
		CreateHardCodedRails(_board.MapConfig.rails.Where((RailConfig r) => !r.random));
		CreateRandomRails(_board.MapConfig.rails.Where((RailConfig r) => r.random));
		yield return null;
	}

	private void CreateHardCodedRails(IEnumerable<RailConfig> rails)
	{
		foreach (RailConfig rail in rails)
		{
			CreateHardcodedRail(rail);
		}
	}

	private void CreateHardcodedRail(RailConfig railCfg)
	{
		if (railCfg.nodes.Count <= 0)
		{
			return;
		}
		WorldPos pos = railCfg.nodes[0];
		if (!IsValidTerminalPos(_board.MapConfig.mapSize, pos))
		{
			return;
		}
		Node node = _board.nodes.FindNearestNodeAround(pos, 5f);
		if (!IsValidTerminalNode(node))
		{
			return;
		}
		List<Node> list = new List<Node> { node };
		for (int i = 1; i < railCfg.nodes.Count; i++)
		{
			WorldPos pos2 = railCfg.nodes[i];
			Node node2 = _board.nodes.FindNearestNodeAround(pos2, 5f);
			if (node2 == null)
			{
				return;
			}
			list.Add(node2);
		}
		List<PathElementResult> list2 = BuildTrackList(list);
		if (list2 != null)
		{
			_transit.AddRailroad(list2);
		}
	}

	private Node ChooseTerminalFromArea(List<Node> potentialTerminals, IntSize mapsize, RailConfig railCfg)
	{
		int num = 0;
		while (num < _trains.randomRegionRetries)
		{
			num++;
			WorldPos pos = railCfg.randomRect.MakeRandomPosInside(_rng);
			if (IsValidTerminalPos(mapsize, pos))
			{
				Node node = _board.nodes.FindNearestNodeAround(pos, 5f);
				if (node != null && IsValidTerminalPos(mapsize, node.pos) && IsValidTerminalNode(node) && potentialTerminals.Remove(node))
				{
					return node;
				}
			}
		}
		return null;
	}

	private Node ChooseTerminal(List<Node> potentialTerminals)
	{
		while (potentialTerminals.Count > 0)
		{
			Node node = _rng.PickAndRemoveElement(potentialTerminals);
			if (_board.nodes.FindAndSortNodesInRadius(node.pos, _trains.otherTerminalMinDistance).TrueForAll((Node other) => !other.HasTerminal))
			{
				return node;
			}
		}
		return null;
	}

	private void CreateRandomRails(IEnumerable<RailConfig> railCfgs)
	{
		List<Node> potentialTerminals = new List<Node>();
		IntSize mapSize = _board.MapConfig.mapSize;
		SetPotentialTerminals(mapSize, potentialTerminals);
		int num = 0;
		foreach (RailConfig railCfg in railCfgs)
		{
			Node node = ((railCfg.randomRect.Area > 0f) ? ChooseTerminalFromArea(potentialTerminals, mapSize, railCfg) : ChooseTerminal(potentialTerminals));
			if (node == null)
			{
				Debug.DrawLine(railCfg.randomRect.Center.AsVector3XZ, railCfg.randomRect.Center.AsVector3XZ + Vector3.up * 10f, Color.red, 10000f);
				continue;
			}
			List<RailSideData> closestSides = GetClosestSides(node);
			List<Node> potentialExits = new List<Node>();
			RailSideData railSideData = closestSides[0];
			foreach (Node item in _board.nodes.GetAllNodesUnsafe())
			{
				if (IsValidExitPosForSide(mapSize, node.pos, item.pos, railSideData.side) && item.HasNoTransitAndOnGround)
				{
					potentialExits.Add(item);
				}
			}
			if (!CreateNextRail(node, ref potentialExits) && num >= _trains.randomRailRetries)
			{
				Debug.LogError($"Unable to create terminal at {node} {_trains.randomRailRetries} in a row, giving up");
			}
		}
	}

	private static void SetPotentialTerminals(IntSize mapsize, ICollection<Node> potentialTerminals)
	{
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			if (!item.pos.IsZero && IsValidTerminalPos(mapsize, item.pos) && IsValidTerminalNode(item))
			{
				potentialTerminals.Add(item);
			}
		}
	}

	private List<RailSideData> GetClosestSides(Node node)
	{
		IntSize mapSize = _board.MapConfig.mapSize;
		float x = node.pos.x;
		float dist = (float)mapSize.width - node.pos.x;
		float y = node.pos.y;
		float dist2 = (float)mapSize.height - node.pos.y;
		List<RailSideData> list = new List<RailSideData>();
		list.Add(new RailSideData(Direction.W, x));
		list.Add(new RailSideData(Direction.E, dist));
		list.Add(new RailSideData(Direction.S, y));
		list.Add(new RailSideData(Direction.N, dist2));
		list.Sort((RailSideData a, RailSideData b) => a.distance.CompareTo(b.distance));
		return list;
	}

	private static bool IsValidTerminalPos(IntSize map, WorldPos pos)
	{
		int terminalEdgeMinDistance = Game.serv.globals.settings.general.trains.terminalEdgeMinDistance;
		if (pos.x > (float)terminalEdgeMinDistance && pos.x < (float)(map.width - terminalEdgeMinDistance) && pos.y > (float)terminalEdgeMinDistance)
		{
			return pos.y < (float)(map.height - terminalEdgeMinDistance);
		}
		return false;
	}

	private bool IsValidExitPosForSide(IntSize map, WorldPos refPos, WorldPos pos, Direction dir)
	{
		int exitEdgeMaxDistance = _trains.exitEdgeMaxDistance;
		int exitLateralMaxDistance = _trains.exitLateralMaxDistance;
		switch (dir)
		{
		case Direction.N:
			if (pos.y > (float)(map.height - exitEdgeMaxDistance))
			{
				return Mathf.Abs(refPos.x - pos.x) < (float)exitLateralMaxDistance;
			}
			return false;
		case Direction.S:
			if (pos.y < (float)exitEdgeMaxDistance)
			{
				return Mathf.Abs(refPos.x - pos.x) < (float)exitLateralMaxDistance;
			}
			return false;
		case Direction.E:
			if (pos.x > (float)(map.width - exitEdgeMaxDistance))
			{
				return Mathf.Abs(refPos.y - pos.y) < (float)exitLateralMaxDistance;
			}
			return false;
		case Direction.W:
			if (pos.x < (float)exitEdgeMaxDistance)
			{
				return Mathf.Abs(refPos.y - pos.y) < (float)exitLateralMaxDistance;
			}
			return false;
		default:
			return false;
		}
	}

	private bool CreateNextRail(Node terminal, ref List<Node> potentialExits)
	{
		for (int i = 0; i < _trains.railConnectionsRetries; i++)
		{
			if (potentialExits.Count <= 0)
			{
				break;
			}
			Node node = _rng.PickElement(potentialExits);
			if (!node.HasNoTransitAndOnGround)
			{
				potentialExits.Remove(node);
				continue;
			}
			List<Node> waypoints = new List<Node> { terminal, node };
			List<PathElementResult> list = BuildTrackList(waypoints);
			if (list != null)
			{
				_transit.AddRailroad(list);
				potentialExits.Remove(node);
				return true;
			}
		}
		return false;
	}

	private List<PathElementResult> BuildTrackList(List<Node> waypoints)
	{
		List<PathElementResult> track = new List<PathElementResult>();
		HashSet<NodeID> seenNodes = new HashSet<NodeID>();
		TrainPathContext ctx = new TrainPathContext(_board, seenNodes);
		for (int i = 0; i < waypoints.Count - 1; i++)
		{
			Node source = waypoints[i];
			Node target = waypoints[i + 1];
			bool success = false;
			_pather.Run(PlayerID.System, EntityID.INVALID, source, target, ctx, delegate(Pathfinding.Result result)
			{
				if (result.status == Pathfinding.Status.Success)
				{
					foreach (PathElement item in result.path)
					{
						track.Add(item.GenerateResult());
						seenNodes.Add(item.target.node.id);
					}
					success = true;
				}
			});
			if (!success)
			{
				return null;
			}
		}
		return track;
	}

	private static bool IsValidTerminalNode(Node node)
	{
		if (node != null)
		{
			if (!node.HasNoTransitAndOnGround)
			{
				return node.HasTerminal;
			}
			return true;
		}
		return false;
	}

	private static bool IsValidTrackNode(Node node)
	{
		if (!node.IsOnWater || Game.serv.globals.settings.general.trains.allowRailOverWater)
		{
			if (node.transit != TransitFlags.Empty)
			{
				return node.transit == TransitFlags.Rail;
			}
			return true;
		}
		return false;
	}
}

using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Board;

public sealed class NodeManager : ISubManager<BoardManager>, ILoadObserver
{
	private BoardManager _manager;

	private NodePersistedData _data;

	private NodeLocationCache _cache;

	private float _minNodeDistance;

	public void Initialize(BoardManager manager)
	{
		_manager = manager;
		_data = manager.data.nodes;
		_cache = new NodeLocationCache();
		_cache.Initialize(manager, 3);
		_cache.Refresh(_data);
		_minNodeDistance = 20f;
		foreach (MapNodesConfig node in manager.MapConfig.nodes)
		{
			_minNodeDistance = Math.Min(_minNodeDistance, node.nodeSpacing.width);
			_minNodeDistance = Math.Min(_minNodeDistance, node.nodeSpacing.height);
		}
		_minNodeDistance = MathUtil.ClampMin(_minNodeDistance, 8.5f);
	}

	public void Release()
	{
		_cache.Release();
		_cache = null;
		_data = null;
		_manager = null;
	}

	public void OnAfterManagerLoad()
	{
		_data = _manager.data.nodes;
		_cache.Refresh(_data);
	}

	public void OnAfterEntityLoad()
	{
	}

	public bool CanAddNode(Node oldNode, WorldPos pos, out Node hitNode)
	{
		hitNode = _cache.FindClosestNodeInRadius(pos, _minNodeDistance);
		if (hitNode != null)
		{
			return hitNode == oldNode;
		}
		return true;
	}

	public Node AddNode(MapNodesConfig cfg, GridTransform transform, TerrainType terrainType, IntPos genOffset)
	{
		Node node = new Node(new NodeID(_data.nodes.Count), cfg, transform);
		node.terraintype = terrainType;
		node.genOffset = genOffset;
		_data.nodes.Add(node);
		_cache.Add(node);
		return node;
	}

	public Node FindNearestNodeAroundUserInput(WorldPos pos)
	{
		return FindNearestNodeAround(pos, 5f);
	}

	public Node FindNearestNodeAround(WorldPos pos, float epsilon = 0.1f)
	{
		return _cache.FindClosestNodeInRadius(pos, epsilon);
	}

	public List<Node> FindAndSortNodesInRadius(WorldPos pos, float radius, bool sort = true, List<Node> result = null)
	{
		if (result == null)
		{
			result = new List<Node>();
		}
		result.Clear();
		_cache.FindAndSortNodesInRadius(pos, radius, sort, result);
		return result;
	}

	public Node GetNode(NodeID id)
	{
		return _data.nodes[id.index];
	}

	public NodeEdge GetEdge(NodeEdgeID id)
	{
		return _data.edges[id.index];
	}

	public List<Node> GetAllNodesUnsafe()
	{
		return _data.nodes;
	}

	public List<NodeEdge> GetAllEdgesUnsafe()
	{
		return _data.edges;
	}

	public int CountAllInterestingBuildings()
	{
		return _data.nodes.SelectAndSum((Node n) => n.interesting.Count);
	}

	internal void AddEdge(Node oldNode, Node newNode, Direction dir, bool isBridge)
	{
		Node node = NodeEdge.PickNodeA(oldNode, newNode);
		Node node2 = ((node == oldNode) ? newNode : oldNode);
		Direction oppositeDirection = DirectionUtil.GetOppositeDirection(dir);
		Direction abDir = ((node == oldNode) ? dir : oppositeDirection);
		Direction baDir = ((node == oldNode) ? oppositeDirection : dir);
		NodeEdgeID edgeID = oldNode.GetEdgeID(dir);
		NodeEdgeID edgeID2 = newNode.GetEdgeID(oppositeDirection);
		if (!edgeID.IsValid && !edgeID2.IsValid)
		{
			NodeEdge nodeEdge = new NodeEdge(new NodeEdgeID(_data.edges.Count), node.id, node2.id, abDir, baDir, isGridConnection: false);
			nodeEdge.isBridge = isBridge;
			_data.edges.Add(nodeEdge);
			oldNode.edges[(int)dir] = nodeEdge.neid;
			newNode.edges[(int)oppositeDirection] = nodeEdge.neid;
		}
	}

	public NodeEdge AddEdgeSpecial(Node aNode, Node bNode, Direction aDirection, Direction bDirection)
	{
		NodeEdgeID edgeID = aNode.GetEdgeID(aDirection);
		NodeEdgeID edgeID2 = bNode.GetEdgeID(bDirection);
		if (edgeID.IsValid || edgeID2.IsValid)
		{
			return null;
		}
		NodeEdge nodeEdge = new NodeEdge(new NodeEdgeID(_data.edges.Count), aNode.id, bNode.id, aDirection, bDirection, isGridConnection: true);
		_data.edges.Add(nodeEdge);
		aNode.edges[(int)aDirection] = nodeEdge.neid;
		bNode.edges[(int)bDirection] = nodeEdge.neid;
		return nodeEdge;
	}

	public NodeEdge GetEdgeInDirection(Node source, Direction dir)
	{
		NodeEdgeID edgeID = source.GetEdgeID(dir);
		if (!edgeID.IsValid)
		{
			return null;
		}
		return GetEdge(edgeID);
	}

	public NodeEdge GetEdgeToTarget(Node source, Node target)
	{
		NodeID id = source.id;
		NodeID id2 = target.id;
		NodeEdgeID[] edges = source.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdgeID id3 = edges[i];
			if (!id3.IsNotValid)
			{
				NodeEdge edge = GetEdge(id3);
				if (edge.IsEdgeBetween(id, id2))
				{
					return edge;
				}
			}
		}
		return null;
	}

	public Node GetNeighborOrNull(Node source, Direction dir)
	{
		return GetEdgeInDirection(source, dir)?.FindOtherNode(source);
	}

	public IEnumerable<Node> FindNeighborNodes(Node node, Predicate<Node> nodeTest = null, Predicate<NodeEdge> edgeTest = null)
	{
		NodeEdgeID[] edges = node.edges;
		int i = 0;
		for (int count = edges.Length; i < count; i++)
		{
			NodeEdgeID id = edges[i];
			if (id.IsNotValid)
			{
				continue;
			}
			NodeEdge edge = GetEdge(id);
			if (edge != null && (edgeTest == null || edgeTest(edge)))
			{
				Node node2 = edge.FindOtherNode(node);
				if (node2 != null && (nodeTest == null || nodeTest(node2)))
				{
					yield return node2;
				}
			}
		}
	}

	internal int VisitNeighborhoodBFS(Node source, int maxNodes, Action<Node> callback, Predicate<Node> nodeTest = null, Predicate<NodeEdge> edgeTest = null, Predicate<Node> stopWhen = null, bool onlyBizNodes = false)
	{
		if (maxNodes <= 0)
		{
			return 0;
		}
		HashSet<Node> hashSet = new HashSet<Node>();
		Deque<Node> deque = new Deque<Node>(64);
		deque.AddLast(source);
		while (deque.Count > 0)
		{
			Node node = deque.RemoveFirst();
			if (hashSet.Contains(node))
			{
				continue;
			}
			callback(node);
			hashSet.Add(node);
			if (hashSet.Count >= maxNodes || (stopWhen != null && stopWhen(node)))
			{
				break;
			}
			NodeEdgeID[] edges = node.edges;
			int i = 0;
			for (int num = edges.Length; i < num; i++)
			{
				NodeEdgeID id = edges[i];
				if (id.IsNotValid)
				{
					continue;
				}
				NodeEdge edge = GetEdge(id);
				if (edge != null && (!onlyBizNodes || edge.IsRoad) && (edgeTest == null || edgeTest(edge)))
				{
					Node node2 = edge.FindOtherNode(node);
					if (node2 != null && (!onlyBizNodes || node2.IsOnGround) && (nodeTest == null || nodeTest(node2)))
					{
						deque.AddLast(node2);
					}
				}
			}
		}
		return hashSet.Count;
	}

	internal int VisitNeighborhoodBFSMaxDistance(Node source, int maxDistance, Action<Node> callback, Predicate<Node> nodeTest = null, Predicate<NodeEdge> edgeTest = null, Predicate<Node> stopWhen = null, bool onlyBizNodes = false)
	{
		if (maxDistance <= 0)
		{
			return 0;
		}
		HashSet<Node> hashSet = new HashSet<Node>();
		Deque<(Node, int)> deque = new Deque<(Node, int)>(64);
		deque.AddLast((source, 0));
		while (deque.Count > 0)
		{
			var (node, num) = deque.RemoveFirst();
			if (num > maxDistance || hashSet.Contains(node))
			{
				continue;
			}
			callback(node);
			hashSet.Add(node);
			if (stopWhen != null && stopWhen(node))
			{
				break;
			}
			NodeEdgeID[] edges = node.edges;
			int i = 0;
			for (int num2 = edges.Length; i < num2; i++)
			{
				NodeEdgeID id = edges[i];
				if (id.IsNotValid)
				{
					continue;
				}
				NodeEdge edge = GetEdge(id);
				if (edge != null && (!onlyBizNodes || edge.IsRoad) && (edgeTest == null || edgeTest(edge)))
				{
					Node node2 = edge.FindOtherNode(node);
					if (node2 != null && (!onlyBizNodes || node2.IsOnGround) && (nodeTest == null || nodeTest(node2)))
					{
						deque.AddLast((node2, num + 1));
					}
				}
			}
		}
		return hashSet.Count;
	}

	public NeighborFlags GetNeighborFlags(Node node, Func<NodeEdge, bool> isNeighbor)
	{
		NeighborFlags neighborFlags = NeighborFlags.None;
		NodeEdge edgeInDirection = GetEdgeInDirection(node, Direction.N);
		if (edgeInDirection != null && isNeighbor(edgeInDirection))
		{
			neighborFlags |= NeighborFlags.North;
		}
		NodeEdge edgeInDirection2 = GetEdgeInDirection(node, Direction.E);
		if (edgeInDirection2 != null && isNeighbor(edgeInDirection2))
		{
			neighborFlags |= NeighborFlags.East;
		}
		NodeEdge edgeInDirection3 = GetEdgeInDirection(node, Direction.S);
		if (edgeInDirection3 != null && isNeighbor(edgeInDirection3))
		{
			neighborFlags |= NeighborFlags.South;
		}
		NodeEdge edgeInDirection4 = GetEdgeInDirection(node, Direction.W);
		if (edgeInDirection4 != null && isNeighbor(edgeInDirection4))
		{
			neighborFlags |= NeighborFlags.West;
		}
		return neighborFlags;
	}

	internal void TryDetachEntity(Entity entity)
	{
		LotBeadHandle bead = entity.data.board.bead;
		if (!bead.IsNotValid)
		{
			Node node = bead.nodeId.FindNode();
			NodeEdge nodeEdge = bead.edgeId.FindEdge();
			bool left = nodeEdge.lotBeads[bead.beadIndex].IsLeft(entity.Id);
			node.contained.Remove(entity.Id);
			ClearBeadReferences(nodeEdge, bead.beadIndex, left, entity);
		}
	}

	internal LotBeadHandle AttachOrReattachEntity(Entity entity, NodeEdge edge, LotBead bead, bool left)
	{
		if (edge == null)
		{
			return LotBeadHandle.INVALID;
		}
		TryDetachEntity(entity);
		WorldPos worldpos = entity.data.board.worldpos;
		Node node = NodeEdge.PickCloserNode(edge.a.FindNode(), edge.b.FindNode(), worldpos);
		int lotBeadIndex = edge.GetLotBeadIndex(bead);
		node.contained.Add(entity.Id);
		SetBeadReferences(edge, lotBeadIndex, left, entity);
		return new LotBeadHandle(node.id, edge.neid, lotBeadIndex, left);
	}

	internal LotBeadHandle AttachOrReattachEntity(Entity entity, NodeEdge edge, RoadBead bead)
	{
		if (edge == null)
		{
			return LotBeadHandle.INVALID;
		}
		int roadBeadIndex = edge.GetRoadBeadIndex(bead);
		SetBeadReferences(edge, roadBeadIndex, entity);
		return new LotBeadHandle(NodeID.INVALID, edge.neid, 0, left: false, roadBeadIndex, validRoadIndex: true);
	}

	internal bool CanEntityAttachToBeads(EntityConfig config, NodeEdge edge, int beadindex, bool left)
	{
		int size = (int)config.board.lotsize.width;
		return CheckAllBeadReferences(EntityID.INVALID, edge, beadindex, left, size);
	}

	internal bool IsEntityAttachedToBeads(Entity entity, NodeEdge edge, int beadindex, bool left)
	{
		int size = (int)entity.config.board.lotsize.width;
		return CheckAllBeadReferences(entity.Id, edge, beadindex, left, size);
	}

	private bool CheckAllBeadReferences(EntityID eid, NodeEdge edge, int beadIndex, bool left, int size)
	{
		int num = beadIndex - (left ? (size - 1) : 0);
		int num2 = beadIndex + ((!left) ? (size - 1) : 0);
		if (num < 0 || num2 >= edge.lotBeads.Count)
		{
			return false;
		}
		for (int i = num; i <= num2; i++)
		{
			if (edge.lotBeads[i].GetEntity(left) != eid)
			{
				return false;
			}
		}
		return true;
	}

	private void SetBeadReferences(NodeEdge edge, int beadIndex, bool left, Entity entity)
	{
		int num = (int)entity.config.board.lotsize.width;
		EntityID id = entity.Id;
		int num2 = beadIndex - (left ? (num - 1) : 0);
		int num3 = beadIndex + ((!left) ? (num - 1) : 0);
		for (int i = num2; i <= num3; i++)
		{
			edge.lotBeads[i].SetEntity(id, left);
		}
	}

	private void SetBeadReferences(NodeEdge edge, int roadBeadIndex, Entity entity)
	{
		EntityID id = entity.Id;
		edge.roadBeads[roadBeadIndex].SetEntity(id);
	}

	private void ClearBeadReferences(NodeEdge edge, int beadIndex, bool left, Entity entity)
	{
		int num = (int)entity.config.board.lotsize.width;
		EntityID id = entity.Id;
		int num2 = beadIndex - (left ? (num - 1) : 0);
		int num3 = beadIndex + ((!left) ? (num - 1) : 0);
		for (int i = num2; i <= num3; i++)
		{
			edge.lotBeads[i].ClearEntity(id, left);
		}
	}
}

using System;
using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Core;

public sealed class Pathfinding
{
	public enum Status
	{
		NotStarted,
		Success,
		Failure
	}

	public sealed class Result
	{
		public Status status;

		public List<PathElement> path;

		public List<PathElement> visited;

		internal Result(int capacity)
		{
			path = new List<PathElement>(capacity);
			visited = new List<PathElement>(capacity);
		}

		internal void Reset()
		{
			status = Status.NotStarted;
			path.Clear();
			visited.Clear();
		}

		public void PopulatePath(PathData result, WorldPos start, Fixnum maxCost, List<PathNode> exclude = null)
		{
			bool num = PathfindingUtil.PopulatePath(result, path, start, maxCost, exclude);
			result.fuzznode = default(NodeID);
			if (num)
			{
				result.fuzznode = (path.LastOrDefaultFast()?.target.node)?.id ?? NodeID.INVALID;
			}
		}
	}

	public static readonly Fixnum MAXPATHCOST = new Fixnum(4000);

	public static readonly int OPENSET_CAP = 512;

	private ObjectPool<PathElement> elementpool;

	private Dictionary<Node, PathNode> nav;

	public PathElement[] openset;

	public int opensetidx;

	public Result result;

	public IPathContext ctx;

	public PathNode source;

	public PathNode target;

	public PlayerID pid;

	public EntityID eid;

	private static readonly List<PathContextNeighbor> _neighborsScratch = new List<PathContextNeighbor>(4);

	public Pathfinding()
	{
		opensetidx = -1;
		openset = new PathElement[OPENSET_CAP];
		elementpool = new ObjectPool<PathElement>();
		elementpool.Initialize();
		nav = new Dictionary<Node, PathNode>();
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			nav.Add(item, new PathNode(item));
		}
		result = new Result(512);
	}

	public void Run(PlayerID pid, EntityID eid, Node source, Node target, IPathContext ctx, Action<Result> callback)
	{
		try
		{
			this.pid = pid;
			this.eid = eid;
			this.source = GetPathNode(source);
			this.target = GetPathNode(target);
			this.ctx = ctx;
			InitializeSearch();
			RunSearch();
		}
		catch (Exception ex)
		{
			Logger.Error("NavTask error: " + ex);
			GenerateResult(null);
		}
		callback(result);
		ResetAfterSearch();
	}

	private void GenerateResult(PathElement lastlink)
	{
		ctx.OnSearchEnd();
		if (lastlink == null)
		{
			result.status = Status.Failure;
			return;
		}
		result.status = Status.Success;
		while (lastlink.previous != null)
		{
			result.path.Add(lastlink);
			lastlink = lastlink.previous;
		}
		result.path.ReverseInPlace();
	}

	private void ResetAfterSearch()
	{
		result.Reset();
		while (opensetidx >= 0)
		{
			PathElement element = openset[opensetidx];
			elementpool.Free(element);
			openset[opensetidx] = null;
			opensetidx--;
		}
		foreach (KeyValuePair<Node, PathNode> item in nav)
		{
			item.Value.ResetVisitState();
		}
		ctx = null;
		source = null;
		target = null;
		pid = PlayerID.System;
		eid = EntityID.INVALID;
	}

	private PathNode GetPathNode(Node node)
	{
		return nav.FindOrNull(node);
	}

	private void InitializeSearch()
	{
		ctx.OnSearchStart(pid, eid);
		Fixnum estToGoal = ctx.LeastCostEstimate(pid, source.node, target.node);
		source.SetVisitState(0, estToGoal);
		AddToOpenSet(source, source, null, null);
	}

	private bool AddToOpenSet(PathNode source, PathNode target, NodeEdge edge, PathElement previous)
	{
		if (opensetidx >= OPENSET_CAP - 1)
		{
			Logger.Error("Ran out of open set space");
			return false;
		}
		if (target == null || source == null)
		{
			Logger.Error($"Invalid nodes in search: {source} {edge.abDir} => {target}");
			return false;
		}
		PathElement pathElement = elementpool.Allocate();
		pathElement.Set(source, target, edge, previous);
		openset[++opensetidx] = pathElement;
		target.status = PathNode.Status.InOpenSet;
		for (int num = opensetidx; num > 0; num--)
		{
			PathElement pathElement2 = openset[num - 1];
			PathNode pathNode = pathElement2.target;
			if (pathNode == null || pathNode.costTotal >= target.costTotal)
			{
				break;
			}
			openset[num] = pathElement2;
			openset[num - 1] = pathElement;
		}
		return true;
	}

	private void RunSearch()
	{
		while (opensetidx >= 0 && result.status != Status.Failure)
		{
			PathElement pathElement = openset[opensetidx];
			openset[opensetidx--] = null;
			result.visited.Add(pathElement);
			pathElement.target.status = PathNode.Status.InClosedSet;
			if (pathElement.target.node == target.node)
			{
				GenerateResult(pathElement);
				return;
			}
			ctx.GetNeighbors(pathElement, _neighborsScratch);
			for (int i = 0; i < _neighborsScratch.Count; i++)
			{
				AddToSearch(pathElement, _neighborsScratch[i]);
			}
			_neighborsScratch.Clear();
		}
		GenerateResult(null);
	}

	private void AddToSearch(PathElement current, PathContextNeighbor neighborData)
	{
		PathNode pathNode = current.target;
		PathNode pathNode2 = GetPathNode(neighborData.neighbor);
		Fixnum fixnum = pathNode.costSoFar + neighborData.cost;
		if (fixnum >= pathNode2.costSoFar || fixnum >= MAXPATHCOST)
		{
			return;
		}
		bool flag = pathNode2.status != PathNode.Status.InOpenSet;
		if (flag || fixnum < pathNode2.costSoFar)
		{
			Fixnum estToGoal = ctx.LeastCostEstimate(pid, pathNode2.node, target.node);
			pathNode2.SetVisitState(fixnum, estToGoal);
			if (flag)
			{
				AddToOpenSet(pathNode, pathNode2, neighborData.edge, current);
			}
		}
	}
}

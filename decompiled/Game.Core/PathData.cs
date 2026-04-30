using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Core;

public sealed class PathData
{
	public List<PathNode> nodes = new List<PathNode>(16);

	public List<WorldPos> world = new List<WorldPos>(64);

	public Fixnum cost;

	public NodeID fuzznode;

	public void Reset()
	{
		nodes.Clear();
		world.Clear();
		cost = 0;
		fuzznode = default(NodeID);
	}

	public PathData()
	{
	}

	public PathData(PathData source)
	{
		nodes = new List<PathNode>(source.nodes);
		world = new List<WorldPos>(source.world);
		cost = source.cost;
		fuzznode = source.fuzznode;
	}
}

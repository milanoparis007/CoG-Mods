using SomaSim.Util;

namespace Game.Core;

public struct PathContextNeighbor
{
	public Node neighbor;

	public NodeEdge edge;

	public Fixnum cost;

	public PathContextNeighbor(Node neighbor, NodeEdge edge, Fixnum cost)
	{
		this = default(PathContextNeighbor);
		this.neighbor = neighbor;
		this.edge = edge;
		this.cost = cost;
	}
}

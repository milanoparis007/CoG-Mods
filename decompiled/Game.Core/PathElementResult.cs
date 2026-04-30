namespace Game.Core;

public struct PathElementResult
{
	public Node source;

	public Node target;

	public NodeEdge edge;

	public PathElementResult(Node source, Node target, NodeEdge edge)
	{
		this.source = source;
		this.target = target;
		this.edge = edge;
	}
}

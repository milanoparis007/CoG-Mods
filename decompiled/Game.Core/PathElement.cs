using System.Diagnostics;
using SomaSim.Util;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public sealed class PathElement : IObjectPoolElement
{
	public PathNode source;

	public PathNode target;

	public NodeEdge edge;

	public PathElement previous;

	public bool IsReset => target == null;

	public bool InOpenSet
	{
		get
		{
			if (target != null)
			{
				return target.status == PathNode.Status.InOpenSet;
			}
			return false;
		}
	}

	private string DebugString => ToString();

	public void Set(PathNode source, PathNode target, NodeEdge edge, PathElement previous)
	{
		this.source = source;
		this.target = target;
		this.edge = edge;
		this.previous = previous;
	}

	public void Reset()
	{
		source = null;
		target = null;
		edge = null;
		previous = null;
	}

	public PathElementResult GenerateResult()
	{
		return new PathElementResult(source.node, target.node, edge);
	}

	public (WorldPos start, WorldPos end) GetStartEndPos()
	{
		return (start: source.node.pos, end: target.node.pos);
	}

	public override string ToString()
	{
		return $"[NAV {source?.node?.pos} {edge?.abDir} => {target?.node?.pos}]";
	}
}

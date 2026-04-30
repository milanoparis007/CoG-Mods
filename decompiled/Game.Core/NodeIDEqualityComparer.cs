using System.Collections.Generic;

namespace Game.Core;

public sealed class NodeIDEqualityComparer : IEqualityComparer<NodeID>
{
	public bool Equals(NodeID x, NodeID y)
	{
		return NodeID.Equals(x, y);
	}

	public int GetHashCode(NodeID nid)
	{
		return nid.GetHashCode();
	}
}

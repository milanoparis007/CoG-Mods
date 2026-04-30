using System;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct NodeID : IEquatable<NodeID>
{
	public static readonly NodeID INVALID = new NodeID(0);

	public short index;

	public bool IsValid => index > 0;

	public bool IsNotValid => index <= 0;

	private string DebugString => $"NID_{index}";

	public NodeID(int index)
	{
		this.index = (short)index;
	}

	public static bool Equals(NodeID a, NodeID b)
	{
		return a.index == b.index;
	}

	public bool Equals(NodeID other)
	{
		return Equals(this, other);
	}

	public static bool operator ==(NodeID a, NodeID b)
	{
		return a.index == b.index;
	}

	public static bool operator !=(NodeID a, NodeID b)
	{
		return a.index != b.index;
	}

	public static NodeID GetOtherNode(NodeID node, NodeID a, NodeID b)
	{
		if (a.index != node.index)
		{
			if (b.index != node.index)
			{
				return INVALID;
			}
			return a;
		}
		return b;
	}

	public override bool Equals(object other)
	{
		if (other is NodeID)
		{
			return Equals(this, (NodeID)other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return index;
	}

	public override string ToString()
	{
		return DebugString;
	}
}

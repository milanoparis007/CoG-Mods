using System;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct NodeEdgeID : IEquatable<NodeEdgeID>
{
	public static readonly NodeEdgeID INVALID = new NodeEdgeID(0);

	public short index;

	public bool IsValid => index > 0;

	public bool IsNotValid => index <= 0;

	private string DebugString => $"NED_{index}";

	public NodeEdgeID(int index)
	{
		this.index = (short)index;
	}

	public bool Equals(NodeEdgeID other)
	{
		return index == other.index;
	}

	public override bool Equals(object obj)
	{
		if (obj is NodeEdgeID nodeEdgeID)
		{
			return nodeEdgeID.Equals(this);
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

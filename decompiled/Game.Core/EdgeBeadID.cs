using System;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct EdgeBeadID : IEquatable<EdgeBeadID>
{
	public static readonly EdgeBeadID INVALID;

	public NodeEdgeID edgeId;

	public byte index;

	private string DebugString => $"BEAD_{index}";

	public EdgeBeadID(NodeEdgeID edge, int index)
	{
		edgeId = edge;
		this.index = (byte)index;
	}

	public bool Equals(EdgeBeadID other)
	{
		if (edgeId.Equals(other.edgeId))
		{
			return index == other.index;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is EdgeBeadID edgeBeadID)
		{
			return edgeBeadID.Equals(this);
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

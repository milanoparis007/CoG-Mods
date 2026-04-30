using System;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct GridTransform : IEquatable<GridTransform>
{
	public WorldPos pos;

	public float deg;

	private string DebugString => ToString();

	public GridTransform(WorldPos pos, float deg)
	{
		this.pos = pos;
		this.deg = deg;
	}

	public WorldPos RotateDelta(float dx, float dy)
	{
		return new WorldPos(dx, dy).Rotate(deg);
	}

	public WorldPos RotateAndAdd(float dx, float dy)
	{
		return pos + RotateDelta(dx, dy);
	}

	public static bool Equals(GridTransform a, GridTransform b)
	{
		if (a.pos.Equals(b.pos))
		{
			return a.deg == b.deg;
		}
		return false;
	}

	public bool Equals(GridTransform other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is GridTransform b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return pos.GetHashCode() ^ (int)deg;
	}

	public override string ToString()
	{
		return $"[TR {pos}, {deg} deg]";
	}
}

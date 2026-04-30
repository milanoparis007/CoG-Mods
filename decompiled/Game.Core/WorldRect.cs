using System;
using System.Diagnostics;
using SomaSim.Util;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct WorldRect : IEquatable<WorldRect>
{
	public float x;

	public float y;

	public float width;

	public float height;

	public WorldPos Pos => new WorldPos(x, y);

	public WorldSize Size => new WorldSize(width, height);

	public float Area => width * height;

	public WorldPos Center => new WorldPos(x + width / 2f, y + height / 2f);

	private string DebugString => ToString();

	public WorldRect(float x, float y, float width, float height)
	{
		this.x = x;
		this.y = y;
		this.width = width;
		this.height = height;
	}

	public WorldRect(WorldPos pos, WorldSize size)
	{
		x = pos.x;
		y = pos.y;
		width = size.width;
		height = size.height;
	}

	public bool Contains(WorldPos other)
	{
		if (x <= other.x && y <= other.y && other.x < x + width)
		{
			return other.y < y + height;
		}
		return false;
	}

	public WorldPos MakeRandomPosInside(IRandom rng)
	{
		return new WorldPos(rng.Generate(x, x + width), rng.Generate(y, y + height));
	}

	public static bool operator ==(WorldRect a, WorldRect b)
	{
		return a.Equals(b);
	}

	public static bool operator !=(WorldRect a, WorldRect b)
	{
		return !a.Equals(b);
	}

	public bool Equals(WorldRect other)
	{
		if (x == other.x && y == other.y && width == other.width)
		{
			return height == other.height;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is WorldRect)
		{
			return this == (WorldRect)obj;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return x.GetHashCode() ^ y.GetHashCode();
	}

	public override string ToString()
	{
		return $"WR[({x},{y}),({width}x{height})]";
	}
}

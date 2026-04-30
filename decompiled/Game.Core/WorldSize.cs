using System;
using System.Diagnostics;
using UnityEngine;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct WorldSize : IEquatable<WorldSize>
{
	public float width;

	public float height;

	public WorldPos Half => new WorldPos(width / 2f, height / 2f);

	public float Area => width * height;

	public float Radius => new WorldPos(width / 2f, height / 2f).Magnitude;

	public float RadiusSquared => new WorldPos(width / 2f, height / 2f).MagnitudeSquared;

	public Vector3 AsVector3XZ => new Vector3(width, 0f, height);

	private string DebugString => ToString();

	public WorldSize(float w, float h)
	{
		width = w;
		height = h;
	}

	public WorldSize Increment(float dx, float dy)
	{
		return new WorldSize(width + dx, height + dy);
	}

	public static WorldSize operator *(WorldSize a, float scale)
	{
		return new WorldSize(a.width * scale, a.height * scale);
	}

	public static bool operator ==(WorldSize a, WorldSize b)
	{
		return a.Equals(b);
	}

	public static bool operator !=(WorldSize a, WorldSize b)
	{
		return !a.Equals(b);
	}

	public bool Equals(WorldSize other)
	{
		if (width == other.width)
		{
			return height == other.height;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is WorldSize)
		{
			return this == (WorldSize)obj;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return width.GetHashCode() ^ height.GetHashCode();
	}

	public override string ToString()
	{
		return $"WS({width}x{height})";
	}
}

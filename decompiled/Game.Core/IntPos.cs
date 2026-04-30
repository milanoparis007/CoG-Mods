using System;

namespace Game.Core;

public struct IntPos : IEquatable<IntPos>
{
	public int x;

	public int y;

	public IntSize AsIntSize => new IntSize(x, y);

	public IntPos(int x, int y)
	{
		this.x = x;
		this.y = y;
	}

	public bool Equals(IntPos other)
	{
		if (x == other.x)
		{
			return y == other.y;
		}
		return false;
	}

	public static IntPos operator +(IntPos a, IntPos b)
	{
		return new IntPos(a.x + b.x, a.y + b.y);
	}

	public static IntPos operator -(IntPos a, IntPos b)
	{
		return new IntPos(a.x - b.x, a.y - b.y);
	}
}

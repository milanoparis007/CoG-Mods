using System;

namespace Game.Core;

public struct IntSize : IEquatable<IntSize>
{
	public int width;

	public int height;

	public IntSize Half => new IntSize(width / 2, height / 2);

	public IntPos AsIntPos => new IntPos(width, height);

	public IntSize(int width, int height)
	{
		this.width = width;
		this.height = height;
	}

	public bool Equals(IntSize other)
	{
		if (width == other.width)
		{
			return height == other.height;
		}
		return false;
	}
}

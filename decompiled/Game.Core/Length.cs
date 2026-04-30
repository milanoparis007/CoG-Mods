using System;
using Game.Services;

namespace Game.Core;

public struct Length : IEquatable<Length>
{
	public static readonly Length ZERO;

	public int inches;

	private string DebugString => ToString();

	public Length(int feet)
	{
		inches = feet;
	}

	public bool Equals(Length other)
	{
		return inches == other.inches;
	}

	public override bool Equals(object obj)
	{
		if (obj is Length length)
		{
			return length.Equals(this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return inches;
	}

	public static bool operator ==(Length a, Length b)
	{
		return a.inches == b.inches;
	}

	public static bool operator !=(Length a, Length b)
	{
		return a.inches != b.inches;
	}

	public static Length operator *(Length v, int multiplier)
	{
		return new Length(v.inches * multiplier);
	}

	public static Length operator +(Length a, Length b)
	{
		return new Length(a.inches + b.inches);
	}

	public static Length operator -(Length a, Length b)
	{
		return new Length(a.inches - b.inches);
	}

	public static explicit operator Length(int cubicfeet)
	{
		return new Length
		{
			inches = cubicfeet
		};
	}

	public static explicit operator int(Length length)
	{
		return length.inches;
	}

	public override string ToString()
	{
		return Loc.Get("ui.volume.in", "num", inches);
	}

	public string ToFeetAndIn()
	{
		return string.Concat("" + inches % 12 + "'", Math.Floor((float)inches / 12f).ToString(), "\"");
	}
}

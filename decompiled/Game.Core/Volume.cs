using System;
using System.Diagnostics;
using Game.Services;
using SomaSim.Util;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct Volume : IEquatable<Volume>
{
	public static readonly Volume ZERO;

	public Fixnum cubicfeet;

	private string DebugString => ToString();

	public Volume(Fixnum cubicfeet)
	{
		this.cubicfeet = cubicfeet;
	}

	public bool Equals(Volume other)
	{
		return cubicfeet == other.cubicfeet;
	}

	public override bool Equals(object obj)
	{
		if (obj is Volume volume)
		{
			return volume.Equals(this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return cubicfeet.scaled;
	}

	public static bool operator ==(Volume a, Volume b)
	{
		return a.cubicfeet == b.cubicfeet;
	}

	public static bool operator !=(Volume a, Volume b)
	{
		return a.cubicfeet != b.cubicfeet;
	}

	public static Volume operator *(Volume v, Fixnum multiplier)
	{
		return new Volume(v.cubicfeet * multiplier);
	}

	public static Volume operator +(Volume a, Volume b)
	{
		return new Volume(a.cubicfeet + b.cubicfeet);
	}

	public static Volume operator -(Volume a, Volume b)
	{
		return new Volume(a.cubicfeet - b.cubicfeet);
	}

	public static explicit operator Volume(int cubicfeet)
	{
		return new Volume
		{
			cubicfeet = cubicfeet
		};
	}

	public static explicit operator Fixnum(Volume vol)
	{
		return vol.cubicfeet;
	}

	public override string ToString()
	{
		return Loc.Get("ui.volume.ft3", "num", cubicfeet);
	}
}

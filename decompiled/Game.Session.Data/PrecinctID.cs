using System;
using System.Diagnostics;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct PrecinctID : IEquatable<PrecinctID>
{
	public short id;

	public static readonly PrecinctID INVALID;

	public bool IsValid => !Equals(this, INVALID);

	public bool IsNotValid => Equals(this, INVALID);

	private string DebugString => $"[PRECINCT_{id}]";

	public PrecinctID(short id)
	{
		this.id = id;
	}

	public override string ToString()
	{
		return DebugString;
	}

	public static bool Equals(PrecinctID a, PrecinctID b)
	{
		return a.id == b.id;
	}

	public static bool operator ==(PrecinctID a, PrecinctID b)
	{
		return Equals(a, b);
	}

	public static bool operator !=(PrecinctID a, PrecinctID b)
	{
		return !Equals(a, b);
	}

	public bool Equals(PrecinctID other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is PrecinctID b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return id;
	}
}

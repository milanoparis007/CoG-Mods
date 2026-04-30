using System;
using System.Diagnostics;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct AutomationID : IEquatable<AutomationID>
{
	public static readonly AutomationID INVALID;

	public int id;

	public bool IsValid => !Equals(this, INVALID);

	public bool IsNotValid => Equals(this, INVALID);

	public string DebugString => $"AutoID_{id}";

	public static bool Equals(AutomationID a, AutomationID b)
	{
		return a.id == b.id;
	}

	public bool Equals(AutomationID other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is AutomationID b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return id;
	}

	public override string ToString()
	{
		return DebugString;
	}
}

using System;
using System.Diagnostics;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct MfgItem : IEquatable<MfgItem>, IComparable<MfgItem>
{
	public Label id;

	public bool consumed;

	public bool illegal;

	private string DebugString => ToString();

	public MfgItem(Label id, bool consumed)
	{
		this = default(MfgItem);
		this.id = id;
		this.consumed = consumed;
		illegal = Resource.Find(id).GetIsIllegal();
	}

	public Resource FindResource()
	{
		return Resource.Find(id);
	}

	public static bool Equals(MfgItem a, MfgItem b)
	{
		if (a.id.Index == b.id.Index && a.consumed == b.consumed)
		{
			return a.illegal == b.illegal;
		}
		return false;
	}

	public int CompareTo(MfgItem other)
	{
		return id.Index - other.id.Index;
	}

	public bool Equals(MfgItem other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is MfgItem b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return id.Index << 2 + (consumed ? 2 : 0) + (illegal ? 1 : 0);
	}

	public override string ToString()
	{
		return $"[MfgItem {id} consumed={consumed} illegal={illegal}]";
	}
}

using System;
using System.Diagnostics;
using SomaSim.Util;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct BuySellElement : IEquatable<BuySellElement>
{
	public MfgItem item;

	public Fixnum qty;

	private string DebugString => ToString();

	public static bool Equals(BuySellElement a, BuySellElement b)
	{
		if (MfgItem.Equals(a.item, b.item))
		{
			return Fixnum.Equals(a.qty, b.qty);
		}
		return false;
	}

	public bool Equals(BuySellElement other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is BuySellElement b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return item.GetHashCode();
	}

	public override string ToString()
	{
		return $"[BuySellElt {item.id}={qty}]";
	}
}

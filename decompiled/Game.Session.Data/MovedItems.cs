using System;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.UI.Session.Deliveries;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct MovedItems : IEquatable<MovedItems>
{
	public bool iscash;

	public Label res;

	public int qty;

	public AmtChoiceType type;

	public static readonly MovedItems EMPTY;

	public bool All => type == AmtChoiceType.Everything;

	private string DebugString => (iscash ? "cash" : res.String) + " : " + (All ? "all" : qty.ToString());

	public Resource FindResource()
	{
		return Resource.Find(res);
	}

	public static bool Equals(MovedItems a, MovedItems b)
	{
		if (a.iscash == b.iscash && a.res == b.res && a.type == b.type)
		{
			return a.qty == b.qty;
		}
		return false;
	}

	public bool Equals(MovedItems other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is MovedItems b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return res.GetHashCode() ^ qty;
	}

	public override string ToString()
	{
		return DebugString;
	}
}

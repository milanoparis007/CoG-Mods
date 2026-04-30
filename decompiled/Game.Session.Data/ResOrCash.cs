using System;
using System.Diagnostics;
using Game.Core;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct ResOrCash : IEquatable<ResOrCash>
{
	public ResourceAndQty raq;

	public Money money;

	public string SortName
	{
		get
		{
			if (!IsCash)
			{
				return raq.FindResource().GetName();
			}
			return "";
		}
	}

	public bool IsCash => money.IsNotZero;

	public bool IsResource => money.IsZero;

	public string DebugString => ToString();

	public ResOrCash(ResourceAndQty raq)
	{
		this.raq = raq;
		money = Money.ZERO;
	}

	public ResOrCash(Money money)
	{
		raq = ResourceAndQty.NONE;
		this.money = money;
	}

	public static bool Equals(ResOrCash a, ResOrCash b)
	{
		if (ResourceAndQty.Equals(a.raq, b.raq))
		{
			return a.money == b.money;
		}
		return false;
	}

	public bool Equals(ResOrCash other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is ResOrCash b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return raq.GetHashCode() ^ money.GetHashCode();
	}

	public static int CompareByName(ResOrCash a, ResOrCash b)
	{
		return string.Compare(a.SortName, b.SortName);
	}

	public override string ToString()
	{
		return $"[ROC res={raq} cash={money}]";
	}
}

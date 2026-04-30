using System;
using System.Diagnostics;
using Game.Services;
using SomaSim.Util;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct Money : IEquatable<Money>, IComparable<Money>
{
	public static readonly Money ZERO = new Money(0);

	public Fixnum cash;

	public bool IsPositiveOrZero => cash >= 0;

	public bool IsPositive => cash > 0;

	public bool IsZero => cash.IsZero;

	public bool IsNotZero => cash.IsNotZero;

	public Price AsPrice => new Price(cash);

	public Money RoundedCoarse => new Money(cash.RoundCoarse());

	private string DebugString => ToString();

	public Money(Fixnum cash)
	{
		this.cash = cash;
	}

	public bool Equals(Money other)
	{
		return cash == other.cash;
	}

	public override bool Equals(object obj)
	{
		if (obj is Money money)
		{
			return money.Equals(this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return cash.scaled;
	}

	public static bool operator ==(Money a, Money b)
	{
		return a.cash == b.cash;
	}

	public static bool operator !=(Money a, Money b)
	{
		return a.cash != b.cash;
	}

	public static bool operator <(Money a, Money b)
	{
		return a.cash < b.cash;
	}

	public static bool operator >(Money a, Money b)
	{
		return a.cash > b.cash;
	}

	public static bool operator <=(Money a, Money b)
	{
		return a.cash <= b.cash;
	}

	public static bool operator >=(Money a, Money b)
	{
		return a.cash >= b.cash;
	}

	public static Money operator +(Money v)
	{
		return v;
	}

	public static Money operator -(Money v)
	{
		return new Money
		{
			cash = -v.cash
		};
	}

	public static Money operator +(Money a, Money b)
	{
		return new Money(a.cash + b.cash);
	}

	public static Money operator -(Money a, Money b)
	{
		return new Money(a.cash - b.cash);
	}

	public static Money operator +(Money a, Price b)
	{
		return new Money(a.cash + b.cash);
	}

	public static Money operator -(Money a, Price b)
	{
		return new Money(a.cash - b.cash);
	}

	public static Money operator *(Money a, Fixnum b)
	{
		return new Money(a.cash * b);
	}

	public static Money operator /(Money a, Fixnum b)
	{
		return new Money(a.cash / b);
	}

	public static explicit operator int(Money money)
	{
		return (int)money.cash;
	}

	public int CompareTo(Money other)
	{
		return cash.CompareTo(other.cash);
	}

	public override string ToString()
	{
		return Loc.Money(this);
	}
}

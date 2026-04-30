using System;
using System.Diagnostics;
using Game.Services;
using SomaSim.Util;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct Price : IEquatable<Price>, IComparable<Price>
{
	public static readonly Price ZERO = new Price(0);

	public Fixnum cash;

	public bool IsNegative => cash < 0;

	public bool IsNegativeOrZero => cash <= 0;

	public bool IsPositive => cash > 0;

	public bool IsPositiveOrZero => cash >= 0;

	public bool IsNonZero => cash != 0;

	public bool IsZero => cash.IsZero;

	public Money AsMoney => new Money(cash);

	public Price Abs
	{
		get
		{
			if (!(cash >= 0))
			{
				return -this;
			}
			return this;
		}
	}

	public Price RoundedCoarse => new Price(cash.RoundCoarse());

	private string DebugString => ToString();

	public Price(Fixnum cash)
	{
		this = default(Price);
		this.cash = cash;
	}

	public bool Equals(Price other)
	{
		return cash == other.cash;
	}

	public override bool Equals(object obj)
	{
		if (obj is Price price)
		{
			return price.Equals(this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return cash.scaled;
	}

	public static bool operator ==(Price a, Price b)
	{
		return a.cash == b.cash;
	}

	public static bool operator !=(Price a, Price b)
	{
		return a.cash != b.cash;
	}

	public static Price operator +(Price v)
	{
		return v;
	}

	public static Price operator -(Price v)
	{
		return new Price
		{
			cash = -v.cash
		};
	}

	public static Price operator +(Price a, Price b)
	{
		return new Price(a.cash + b.cash);
	}

	public static Price operator -(Price a, Price b)
	{
		return new Price(a.cash - b.cash);
	}

	public static Price operator *(Price a, Fixnum b)
	{
		return new Price(a.cash * b);
	}

	public static Price operator /(Price a, Fixnum b)
	{
		return new Price(a.cash / b);
	}

	public static explicit operator int(Price price)
	{
		return (int)price.cash;
	}

	public static implicit operator Price(Fixnum cash)
	{
		return new Price(cash);
	}

	public static implicit operator Price(int cash)
	{
		return new Price(cash);
	}

	public int CompareTo(Price other)
	{
		return cash.CompareTo(other.cash);
	}

	public override string ToString()
	{
		return Loc.Price(this);
	}
}

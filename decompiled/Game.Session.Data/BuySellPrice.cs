using System;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public struct BuySellPrice : IEquatable<BuySellPrice>
{
	public Price baseUnitPrice;

	public Price baseTotalPrice;

	public Price scaledUnitPrice;

	public Price scaledTotalPrice;

	private BuySellPrice(Price baseUnitPrice, Price baseTotalPrice, Price scaledUnitPrice, Price scaledTotalPrice)
	{
		this.baseUnitPrice = baseUnitPrice;
		this.baseTotalPrice = baseTotalPrice;
		this.scaledUnitPrice = scaledUnitPrice;
		this.scaledTotalPrice = scaledTotalPrice;
	}

	public BuySellPrice(Price baseUnitPrice, Price baseTotalPrice)
		: this(baseUnitPrice, baseTotalPrice, baseUnitPrice, baseTotalPrice)
	{
	}

	public BuySellPrice Multiply(Fixnum multiplier)
	{
		return new BuySellPrice(baseUnitPrice, baseTotalPrice, baseUnitPrice * multiplier, baseTotalPrice * multiplier);
	}

	public static bool Equals(BuySellPrice a, BuySellPrice b)
	{
		if (a.baseTotalPrice == b.baseTotalPrice && a.baseUnitPrice == b.baseUnitPrice && a.scaledTotalPrice == b.scaledTotalPrice)
		{
			return a.scaledUnitPrice == b.scaledUnitPrice;
		}
		return false;
	}

	public bool Equals(BuySellPrice other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is BuySellPrice b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return baseUnitPrice.GetHashCode() ^ baseTotalPrice.GetHashCode();
	}
}

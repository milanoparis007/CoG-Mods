using System;
using SomaSim.Util;

namespace Game.Session.Data;

public struct QtyAndDir : IEquatable<QtyAndDir>
{
	public static readonly QtyAndDir ZERO = new QtyAndDir(Fixnum.ZERO, toBuilding: true);

	public Fixnum qty;

	public bool toBldg;

	public bool IsToBuilding => toBldg;

	public bool IsToPlayer => !toBldg;

	public Fixnum ToBuilding
	{
		get
		{
			if (!toBldg)
			{
				return -qty;
			}
			return qty;
		}
	}

	public Fixnum ToPlayer
	{
		get
		{
			if (!toBldg)
			{
				return qty;
			}
			return -qty;
		}
	}

	public QtyAndDir(Fixnum qty, bool toBuilding)
	{
		this.qty = qty;
		toBldg = toBuilding;
	}

	public QtyAndDir IncrementQty(Fixnum qtyDelta)
	{
		return new QtyAndDir(qty + qtyDelta, toBldg);
	}

	public QtyAndDir Increment(QtyAndDir other)
	{
		return IncrementQty((toBldg == other.toBldg) ? other.qty : (-other.qty));
	}

	public static QtyAndDir FromSignedQuantity(Fixnum signed)
	{
		return new QtyAndDir(signed.Abs, signed >= 0);
	}

	public static bool Equals(QtyAndDir a, QtyAndDir b)
	{
		if (a.qty == b.qty)
		{
			return a.toBldg == b.toBldg;
		}
		return false;
	}

	public bool Equals(QtyAndDir other)
	{
		return Equals(this, other);
	}
}

using System;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct ResourceAndQty : IEquatable<ResourceAndQty>
{
	public static readonly ResourceAndQty NONE;

	public Label id;

	public Label eth;

	public Fixnum qty;

	public bool needsEthPack;

	public bool IsSet => id != NONE.id;

	public bool IsNotSet => id == NONE.id;

	public ResourceAndQty Negative => new ResourceAndQty(id, -qty);

	private string DebugString => ToString();

	public ResourceAndQty(Label id, Fixnum qty)
	{
		this = default(ResourceAndQty);
		this.id = id;
		this.qty = qty;
	}

	public ResourceAndQty SetQuantity(Fixnum newQuantity)
	{
		return new ResourceAndQty(id, newQuantity);
	}

	public ResourceAndQty IncrementQuantity(Fixnum delta)
	{
		return new ResourceAndQty(id, qty + delta);
	}

	public Volume FindTotalVolume()
	{
		return FindResource().FindTotalVolume(qty);
	}

	public Resource FindResource()
	{
		return Resource.Find(id);
	}

	public static bool Equals(ResourceAndQty a, ResourceAndQty b)
	{
		if (a.id == b.id)
		{
			return a.qty == b.qty;
		}
		return false;
	}

	public bool Equals(ResourceAndQty other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is ResourceAndQty b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return id.Index ^ qty.scaled;
	}

	public string MakeResourceOnlyString()
	{
		return FindResource().GetIconAndName();
	}

	public string MakeScaledLocString(Fixnum scale, bool abs = false)
	{
		Fixnum fixnum = (abs ? qty.Abs : qty) * scale;
		return FindResource().GetIconNameAndUnits(fixnum);
	}

	public string MakeQuantityLocString()
	{
		return FindResource().GetQtyIconName(qty);
	}

	public string MakeQuantityXLocString()
	{
		return FindResource().GetQtyXIconName(qty);
	}

	public string MakeQuantityIconString()
	{
		return FindResource().GetQtyIcon(qty);
	}

	public string MakeQuantityXNameString()
	{
		return FindResource().GetQtyXName(qty);
	}

	public string MakeQuantityAndPriceLocString()
	{
		return FindResource().GetQtyIconNamePrice(qty);
	}

	public override string ToString()
	{
		return $"[RESQTY {id}: {qty}]";
	}
}

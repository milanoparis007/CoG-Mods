using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public class InventoryModuleData : ModuleData<InventoryModule, InventoryModuleConfig, InventoryModuleData>
{
	public static readonly Label RES_CASH = (Label)"cash";

	public Money money = Money.ZERO;

	public List<ResourceAndQty> contents = new List<ResourceAndQty>();

	public bool FindIsEmpty(bool includeCash = false)
	{
		int i = 0;
		for (int count = contents.Count; i < count; i++)
		{
			if (contents[i].qty.IsNotZero)
			{
				return false;
			}
		}
		if (includeCash && money.IsNotZero)
		{
			return false;
		}
		return true;
	}

	public ResourceAndQty Get(Resource res)
	{
		return Get(res.resid);
	}

	public ResourceAndQty Get(Label resid)
	{
		int i = 0;
		for (int count = contents.Count; i < count; i++)
		{
			ResourceAndQty result = contents[i];
			if (result.id == resid)
			{
				return result;
			}
		}
		return ResourceAndQty.NONE;
	}

	public bool WillBeNonNegative(Label resid, Fixnum delta)
	{
		return Get(resid).qty + delta >= 0;
	}

	public bool Increment(ResourceAndQty resAndQty)
	{
		return Increment(resAndQty.id, resAndQty.qty);
	}

	public bool Increment(Label resid, Fixnum delta)
	{
		int i = 0;
		for (int count = contents.Count; i < count; i++)
		{
			ResourceAndQty resourceAndQty = contents[i];
			if (resourceAndQty.id == resid)
			{
				if (resourceAndQty.qty + delta < 0)
				{
					return false;
				}
				contents[i] = contents[i].IncrementQuantity(delta);
				if (contents[i].qty.IsZero)
				{
					contents.RemoveAt(i);
				}
				return true;
			}
		}
		if (delta > 0)
		{
			contents.Add(new ResourceAndQty(resid, delta));
			return true;
		}
		return false;
	}

	internal void DoChangeMoney(PlayerFinances _, Price delta)
	{
		money += delta;
	}

	public bool CanChangeMoney(Price delta)
	{
		return (money + delta).IsPositiveOrZero;
	}

	public void ClearResources()
	{
		contents.Clear();
	}

	public void ClearAndPopulateFrom(InventoryModuleData source)
	{
		contents.Clear();
		if (source != null && source.contents != null)
		{
			contents.AddRange(source.contents);
		}
	}
}

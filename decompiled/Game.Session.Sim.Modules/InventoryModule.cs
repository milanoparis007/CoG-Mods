using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public class InventoryModule : Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>
{
	public override bool IsEnabled(SimTime time)
	{
		return time.days >= data.EnableTime.days;
	}

	public override ModuleResult DoUpdate(ModuleQuery q, SimTime time, bool initial, bool enabled)
	{
		return ModuleResult.Default;
	}

	public Volume CalculateUsedCapacity()
	{
		Volume result = default(Volume);
		foreach (ResourceAndQty content in data.contents)
		{
			result += content.FindTotalVolume();
		}
		return result;
	}

	public Volume CalculateAvailableCapacity()
	{
		return config.capacity - CalculateUsedCapacity();
	}

	public bool HasEnoughCapacityToAdd(Volume delta)
	{
		return CalculateUsedCapacity().cubicfeet + delta.cubicfeet <= config.capacity.cubicfeet;
	}

	public bool HasEnoughCapacityToAdd(Resource res, Fixnum qty)
	{
		return HasEnoughCapacityToAdd(res.FindTotalVolume(qty));
	}

	public float GetUsedCapacityAsPercent()
	{
		if (!(config.capacity.cubicfeet <= 0))
		{
			return (float)(CalculateUsedCapacity().cubicfeet / config.capacity.cubicfeet);
		}
		return 1f;
	}

	public static List<Label> ProduceAllItems(bool includeEmpty, params InventoryModule[] modules)
	{
		List<Label> list = new List<Label>();
		foreach (InventoryModule inventoryModule in modules)
		{
			if (inventoryModule == null)
			{
				continue;
			}
			foreach (ResourceAndQty content in inventoryModule.data.contents)
			{
				if (!list.Contains(content.id))
				{
					Fixnum qty = content.qty;
					if (!qty.IsZero || includeEmpty)
					{
						list.Add(content.id);
					}
				}
			}
		}
		return list;
	}

	public int HowManyResourcesCanFit(Resource res)
	{
		Fixnum cubicfeet = CalculateAvailableCapacity().cubicfeet;
		Fixnum cubicfeet2 = res.FindTotalVolume(1).cubicfeet;
		return ((cubicfeet2 > 0) ? (cubicfeet / cubicfeet2) : ((Fixnum)0)).IntFloor();
	}

	public bool CanResourceListFit(List<ResourceAndQty> resList, int multiplier)
	{
		Fixnum cubicfeet = CalculateAvailableCapacity().cubicfeet;
		foreach (ResourceAndQty res in resList)
		{
			cubicfeet += res.FindTotalVolume().cubicfeet * multiplier;
		}
		return cubicfeet >= 0;
	}

	public int TryAddResourcesIfSpaceAvailable(Label id, int qty)
	{
		Resource res = Resource.Find(id);
		int num = Math.Min(HowManyResourcesCanFit(res), qty);
		if (num <= 0)
		{
			return 0;
		}
		data.Increment(id, num);
		return num;
	}

	public int TryRemoveResourcesUpToAll(Label id, int qty)
	{
		int num = (int)data.Get(id).qty;
		if (num == 0)
		{
			return 0;
		}
		int num2 = Math.Max(qty, -num);
		data.Increment(id, num2);
		return num2;
	}

	public int ForceAddResourcesRegardlessOfSpace(Label id, int qty)
	{
		data.Increment(id, qty);
		return qty;
	}

	internal static int ProduceClickMoveQty()
	{
		bool isShiftDown = KeyUtil.IsShiftDown;
		bool isCtrlDown = KeyUtil.IsCtrlDown;
		if (!(isShiftDown && isCtrlDown))
		{
			if (!isShiftDown)
			{
				if (!isCtrlDown)
				{
					return 1;
				}
				return 10;
			}
			return 100;
		}
		return 1000;
	}
}

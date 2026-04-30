using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class Recipe : BizModuleLocData
{
	public int produceConsumeDayz;

	public List<RefillElement> refill = new List<RefillElement>();

	public List<ResourceAndQty> consume = new List<ResourceAndQty>();

	public List<ResourceAndQty> produce = new List<ResourceAndQty>();

	public List<SellOffElement> selloff = new List<SellOffElement>();

	public VisitGrantList grants;

	public RecipeMods mods = new RecipeMods();

	public VisitRequirementList visreqs;

	public bool IsConsumer
	{
		get
		{
			if (consume != null)
			{
				return consume.Count > 0;
			}
			return false;
		}
	}

	public bool IsProducer
	{
		get
		{
			if (produce != null)
			{
				return produce.Count > 0;
			}
			return false;
		}
	}

	public RefillElement? FindRefillByID(Label id)
	{
		if (refill != null)
		{
			int i = 0;
			for (int count = refill.Count; i < count; i++)
			{
				if (refill[i].id == id)
				{
					return refill[i];
				}
			}
		}
		return null;
	}

	public SellOffElement? FindSellOffByID(Label id)
	{
		if (selloff != null)
		{
			int i = 0;
			for (int count = selloff.Count; i < count; i++)
			{
				if (selloff[i].id == id)
				{
					return selloff[i];
				}
			}
		}
		return null;
	}

	public IEnumerable<MfgItem> ProduceAllItems(ManufactureModuleConfig _)
	{
		return consume.Select((ResourceAndQty elt) => new MfgItem(elt.id, consumed: true)).Concat(produce.Select((ResourceAndQty elt) => new MfgItem(elt.id, consumed: false)));
	}

	internal DeliveryInfo ProduceDeliveryInfo(Label resId, ModuleQuery q, IModule module)
	{
		int days = ModProduceConsumeDays(q, module);
		RefillElement? refillElement = FindRefillByID(resId);
		if (refillElement.HasValue)
		{
			return new DeliveryInfo(resId, days, refillElement.Value.get, toBuilding: true);
		}
		SellOffElement? sellOffElement = FindSellOffByID(resId);
		if (sellOffElement.HasValue)
		{
			return new DeliveryInfo(resId, days, -sellOffElement.Value.sell, toBuilding: false);
		}
		return DeliveryInfo.INVALID;
	}

	public ResourceAndQty ModProduceQty(ResourceAndQty val, ModuleQuery q, IModule module)
	{
		return ModProduce(val, q, module, explain: false).resAndQty;
	}

	public ResourceAndQty ModConsumeQty(ResourceAndQty val, ModuleQuery q, IModule module)
	{
		return ModConsume(val, q, module, explain: false).resAndQty;
	}

	public int ModProduceConsumeDays(ModuleQuery q, IModule module)
	{
		return (int)ModProduceConsumeDays(q, module, explain: false).resAndQty.qty;
	}

	public RecipeMods.Result ModProduce(ResourceAndQty val, ModuleQuery q, IModule module, bool explain)
	{
		return RecipeMods.ModQty(val, q, mods?.produceqty, module, ModuleExpansionTarget.Produce, explain);
	}

	public RecipeMods.Result ModConsume(ResourceAndQty val, ModuleQuery q, IModule module, bool explain)
	{
		return RecipeMods.ModQty(val, q, mods?.consumeqty, module, ModuleExpansionTarget.Consume, explain);
	}

	public RecipeMods.Result ModProduceConsumeDays(ModuleQuery q, IModule module, bool explain)
	{
		return RecipeMods.ModQty(new ResourceAndQty
		{
			qty = produceConsumeDayz
		}, q, null, module, ModuleExpansionTarget.ProduceConsumeDays, explain);
	}

	internal Fixnum ProduceBuyCap(Label resId, ModuleQuery q, IModule module)
	{
		Fixnum a = 0;
		Fixnum b = 0;
		foreach (ResourceAndQty item in consume)
		{
			if (item.id == resId)
			{
				a = ModConsumeQty(item, q, module).qty.Abs;
				break;
			}
		}
		foreach (RefillElement item2 in refill)
		{
			if (item2.id == resId)
			{
				Fixnum below = item2.below;
				Fixnum abs = below.Abs;
				below = item2.get;
				b = abs + below.Abs;
				break;
			}
		}
		return Fixnum.Max(a, b);
	}
}

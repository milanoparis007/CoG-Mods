using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class ConsumerRecipe : BizModuleLocData
{
	public sealed class AreaDef
	{
		public Fixnum customerRadius = 0;

		public ModValue respectRadius;

		public ModValue respectPointsInRadius;

		public string respectLockey;

		public ModValue priceMarkup = new ModValue
		{
			value = 1
		};
	}

	public int consumeDayz = 1;

	public List<RefillElement> refill = new List<RefillElement>();

	public List<ResourceAndQty> consume = new List<ResourceAndQty>();

	public RecipeMods mods = new RecipeMods();

	public AreaDef aoe = new AreaDef();

	public VisitGrantList grants;

	private List<RefillElement> _refillCache;

	private List<ResourceAndQty> _consumeCache;

	public List<RefillElement> AllRefill
	{
		get
		{
			List<RefillElement> obj = _refillCache ?? ParseEthAlcs(ModulesUtil.ExpandGroups(refill));
			List<RefillElement> result = obj;
			_refillCache = obj;
			return result;
		}
	}

	public List<ResourceAndQty> AllConsume
	{
		get
		{
			List<ResourceAndQty> obj = _consumeCache ?? ParseEthAlcs(ModulesUtil.ExpandGroups(consume));
			List<ResourceAndQty> result = obj;
			_consumeCache = obj;
			return result;
		}
	}

	public RefillElement? FindRefillByID(Label id)
	{
		if (AllRefill != null)
		{
			int i = 0;
			for (int count = AllRefill.Count; i < count; i++)
			{
				if (AllRefill[i].id == id)
				{
					return AllRefill[i];
				}
			}
		}
		return null;
	}

	public IEnumerable<MfgItem> ProduceAllItems(ConsumerModuleConfig _)
	{
		return AllConsume.Select((ResourceAndQty elt) => new MfgItem(elt.id, consumed: true));
	}

	internal DeliveryInfo ProduceDeliveryInfo(Label resId, ModuleQuery q, IModule module)
	{
		int days = ModConsumeDays(q, module);
		RefillElement? refillElement = FindRefillByID(resId);
		if (refillElement.HasValue)
		{
			return new DeliveryInfo(resId, days, refillElement.Value.get, toBuilding: true);
		}
		return DeliveryInfo.INVALID;
	}

	public ResourceAndQty ModConsumeQty(ResourceAndQty val, ModuleQuery q, IModule module)
	{
		return ModConsume(val, q, module, explain: false).resAndQty;
	}

	public int ModConsumeDays(ModuleQuery q, IModule module)
	{
		return (int)ModConsumeDays(q, module, explain: false).resAndQty.qty;
	}

	public RecipeMods.Result ModConsume(ResourceAndQty val, ModuleQuery q, IModule module, bool explain)
	{
		return RecipeMods.ModQty(val, q, mods?.consumeqty, module, ModuleExpansionTarget.Consume, explain);
	}

	public RecipeMods.Result ModConsumeDays(ModuleQuery q, IModule module, bool explain)
	{
		return RecipeMods.ModQty(new ResourceAndQty
		{
			qty = consumeDayz
		}, q, null, module, ModuleExpansionTarget.ConsumeDays, explain);
	}

	internal Fixnum ProduceBuyCap(Label resId, ModuleQuery q, IModule module)
	{
		Fixnum a = 0;
		Fixnum b = 0;
		foreach (ResourceAndQty item in AllConsume)
		{
			if (item.id == resId)
			{
				a = ModConsumeQty(item, q, module).qty.Abs;
				break;
			}
		}
		foreach (RefillElement item2 in AllRefill)
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

	public List<ResourceAndQty> ParseEthAlcs(List<ResourceAndQty> resources)
	{
		List<ResourceAndQty> list = new List<ResourceAndQty>();
		foreach (ResourceAndQty resource in resources)
		{
			if (resource.needsEthPack)
			{
				Label eth = resource.eth;
				if (eth.IsSet)
				{
					if (PlayerCrew.HasEthPackAndIsEth(resource.eth) && PlayerCrew.EthPackHasUniqueAlc())
					{
						list.Add(resource);
					}
				}
				else if (PlayerCrew.HasEthPackForCurrEth() && PlayerCrew.EthPackHasUniqueAlc())
				{
					list.Add(resource);
				}
			}
			else
			{
				list.Add(resource);
			}
		}
		return list;
	}

	public List<RefillElement> ParseEthAlcs(List<RefillElement> resources)
	{
		List<RefillElement> list = new List<RefillElement>();
		foreach (RefillElement resource in resources)
		{
			if (resource.needsEthPack)
			{
				Label eth = resource.eth;
				if (eth.IsSet)
				{
					if (PlayerCrew.HasEthPackAndIsEth(resource.eth) && PlayerCrew.EthPackHasUniqueAlc())
					{
						list.Add(resource);
					}
				}
				else if (PlayerCrew.HasEthPackForCurrEth() && PlayerCrew.EthPackHasUniqueAlc())
				{
					list.Add(resource);
				}
			}
			else
			{
				list.Add(resource);
			}
		}
		return list;
	}
}

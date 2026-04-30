using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class ManufactureModule : Module<ManufactureModule, ManufactureModuleConfig, ManufactureModuleData>, IBizModule, IModule
{
	private static readonly Label BOOZE = new Label("rescat-booze");

	internal Recipe CurrentRecipe => config.recipes[data.recipeIndex];

	public Label BizModuleID => config.id;

	public bool IsInteresting => config.interesting;

	public BizModuleLocData LocData => CurrentRecipe;

	public override void Initialize(ModuleInitData init)
	{
		base.Initialize(init);
		if (init.IsCreated)
		{
			data.lastUpdate = Game.ctx.clock.Now;
			VisitState visit = new VisitState(CrewAssignment.EMPTY, Game.ctx.clock.Now, PlayerID.HumanPlayer);
			data.recipeIndex = init.rng.PickIndex(config.recipes.Where((Recipe x) => x.visreqs == null || x.visreqs.AllPass(visit)).ToList());
			data.lastStall = Game.ctx.clock.Now;
		}
	}

	public IEnumerable<MfgItem> ProduceAllItemsInCurrentRecipe()
	{
		return CurrentRecipe.ProduceAllItems(config);
	}

	public DeliveryInfo ProduceDeliveryInfo(Label resId, ModuleQuery q)
	{
		return CurrentRecipe.ProduceDeliveryInfo(resId, q, this);
	}

	public Fixnum ProduceBuyCap(Label resId, ModuleQuery q)
	{
		return CurrentRecipe.ProduceBuyCap(resId, q, this);
	}

	public override bool IsEnabled(SimTime time)
	{
		return time.days >= data.EnableTime.days;
	}

	public override ModuleResult DoUpdate(ModuleQuery q, SimTime time, bool initial, bool enabled)
	{
		ModuleResult moduleResult = ModuleResult.Default;
		data.monthlyStats.Update(time);
		if (!enabled)
		{
			data.lastUpdate = time;
			return moduleResult;
		}
		Entity container = q.container;
		MaybeInformAboutConstruction(container, time, initial);
		InventoryModule inventory = container.components.modules.inventory;
		if (inventory == null)
		{
			BuildingUtil.FindBizForBuilding(container);
			return moduleResult;
		}
		int num = ComputeUpdateIntervals(time, initial, q);
		if (num <= 0)
		{
			return moduleResult;
		}
		if (num > 5)
		{
			num = 5;
		}
		data.lastProduced.Clear();
		for (int i = 0; i < num; i++)
		{
			moduleResult |= DoConsumeAndProduce(q, inventory);
		}
		if (moduleResult == ModuleResult.MfgOutOfInputs || moduleResult == ModuleResult.MfgOutOfStorageSpace)
		{
			data.lastStall = Game.ctx.clock.Now;
		}
		DoSellOffInventory(q, inventory);
		DoRefillInventory(q, inventory);
		ModulesUtil.GiveXPToManager(q, moduleResult, num);
		data.monthlyStats.Add(q, data.lastProduced, time);
		data.lastUpdate = time;
		return moduleResult;
	}

	internal int ComputeUpdateIntervals(SimTime time, bool isInitialVisit, ModuleQuery q)
	{
		bool ownerIsHumanPlayer = q.OwnerIsHumanPlayer;
		if (ownerIsHumanPlayer && isInitialVisit)
		{
			return 0;
		}
		if (isInitialVisit)
		{
			return 1;
		}
		VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
		int period = (ownerIsHumanPlayer ? (from x in config.recipes
			where x.visreqs == null || x.visreqs.AllPass(visit)
			select x.ModProduceConsumeDays(q, this)).Min() : CurrentRecipe.ModProduceConsumeDays(q, this));
		if (!isInitialVisit)
		{
			return MathUtil.CountIntervals(data.lastUpdate.days, time.days, period);
		}
		return 1;
	}

	private void DoRefillInventory(ModuleQuery q, InventoryModule inventory)
	{
		if (q.OwnerIsHumanPlayer)
		{
			return;
		}
		foreach (RefillElement item in CurrentRecipe.refill)
		{
			if (item.get < 0)
			{
				Logger.Warning($"Refill recipe should not be negative in {item.id}, {item.get}");
			}
			bool canFakeRefill = inventory.data.Get(item.id).qty < item.below;
			this.TryDeliveryOrFakeIt(q, inventory, item.id, item.get, canFakeRefill);
		}
	}

	private void DoSellOffInventory(ModuleQuery q, InventoryModule inventory)
	{
		if (q.OwnerIsHumanPlayer)
		{
			return;
		}
		foreach (SellOffElement item in CurrentRecipe.selloff)
		{
			if (item.sell > 0)
			{
				Logger.Warning($"Refill recipe should not be negative in {item.id}, {item.sell}");
			}
			bool canFakeRefill = inventory.data.Get(item.id).qty > item.above;
			this.TryDeliveryOrFakeIt(q, inventory, item.id, item.sell, canFakeRefill);
		}
	}

	[Conditional("UNITY_EDITOR")]
	private void ValidateInventoryCapacity(ModuleQuery q, InventoryModule inventory)
	{
		Entity container = q.container;
		if (inventory.GetUsedCapacityAsPercent() > 1f)
		{
			BuildingUtil.FindBizForBuilding(container);
		}
	}

	private ModuleResult DoConsumeAndProduce(ModuleQuery q, InventoryModule inventory)
	{
		if (q.container.components.building.HasDamage())
		{
			return ModuleResult.BuildingDamaged;
		}
		ModuleResult moduleResult = ModuleResult.Default;
		if (q.OwnerIsHumanPlayer)
		{
			VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
			for (int i = 0; i < config.recipes.Count; i++)
			{
				Recipe recipe = config.recipes[i];
				if (recipe.visreqs == null || recipe.visreqs.AllPass(visit))
				{
					moduleResult = DoConsumeAndProduce(inventory, recipe, q);
					if ((moduleResult & ModuleResult.MfgCompleted) != ModuleResult.Default)
					{
						break;
					}
				}
			}
		}
		else
		{
			moduleResult = DoConsumeAndProduce(inventory, CurrentRecipe, q);
		}
		return moduleResult;
	}

	private ModuleResult DoConsumeAndProduce(InventoryModule inventory, Recipe recipe, ModuleQuery q)
	{
		if (!recipe.IsConsumer && !recipe.IsProducer)
		{
			return ModuleResult.Default;
		}
		List<ResourceAndQty> list = IsHumanFrontRoomOverproducing(inventory, recipe, q);
		bool num = HasInventoryToConsume(inventory, recipe, q);
		bool flag = HasCapacityToProduce(inventory, recipe, q);
		if (num && flag)
		{
			ProduceConsumeHelper(this, inventory, recipe, increment: false, q, list);
			ProduceConsumeHelper(this, inventory, recipe, increment: true, q, list);
			ProduceGrantsOnSuccess(recipe, q);
			if (list == null)
			{
				return ModuleResult.MfgCompleted;
			}
			return ModuleResult.MfgLegitOverproduced;
		}
		if (!flag)
		{
			return ModuleResult.MfgOutOfStorageSpace;
		}
		return ModuleResult.MfgOutOfInputs;
	}

	private List<ResourceAndQty> IsHumanFrontRoomOverproducing(InventoryModule inventory, Recipe recipe, ModuleQuery q)
	{
		List<ResourceAndQty> list = null;
		int num = 4;
		if (!q.OwnerIsHumanPlayer || recipe.produce == null || recipe.selloff == null)
		{
			return list;
		}
		TagList tags = config.Common.tags;
		if (tags == null || !tags.Contains(TagConstants.TAG_SAFEHOUSE_FRONTROOMS))
		{
			return list;
		}
		foreach (ResourceAndQty item in recipe.produce)
		{
			Fixnum qty = inventory.data.Get(item.id).qty;
			Fixnum fixnum = recipe.FindSellOffByID(item.id)?.above ?? ((Fixnum)0);
			if (fixnum <= 0)
			{
				Fixnum qty2 = item.qty;
				fixnum = qty2.Abs;
			}
			Fixnum fixnum2 = fixnum * num;
			if ((qty - fixnum2).IsPositive)
			{
				list = list ?? new List<ResourceAndQty>();
				list.Add(item);
			}
		}
		return list;
	}

	private bool HasCapacityToProduce(InventoryModule inventory, Recipe recipe, ModuleQuery q)
	{
		if (recipe.produce == null)
		{
			return true;
		}
		Volume delta = default(Volume);
		foreach (ResourceAndQty item in recipe.produce)
		{
			ResourceAndQty resourceAndQty = recipe.ModProduceQty(item, q, this);
			delta.cubicfeet += resourceAndQty.FindTotalVolume().cubicfeet;
		}
		if (recipe.consume != null)
		{
			foreach (ResourceAndQty item2 in recipe.consume)
			{
				ResourceAndQty resourceAndQty2 = recipe.ModConsumeQty(item2, q, this);
				delta.cubicfeet += resourceAndQty2.FindTotalVolume().cubicfeet;
			}
		}
		return inventory.HasEnoughCapacityToAdd(delta);
	}

	private bool HasInventoryToConsume(InventoryModule inventory, Recipe recipe, ModuleQuery q)
	{
		if (recipe.consume == null)
		{
			return true;
		}
		foreach (ResourceAndQty item in recipe.consume)
		{
			ResourceAndQty resourceAndQty = recipe.ModConsumeQty(item, q, this);
			if (inventory.data.Get(item.id).qty + resourceAndQty.qty < 0)
			{
				return false;
			}
		}
		return true;
	}

	private void ProduceConsumeHelper(IModule module, InventoryModule inventory, Recipe recipe, bool increment, ModuleQuery q, List<ResourceAndQty> disabled)
	{
		if (recipe == null)
		{
			return;
		}
		List<ResourceAndQty> list = (increment ? recipe.produce : recipe.consume);
		if (list == null)
		{
			return;
		}
		foreach (ResourceAndQty item in list)
		{
			ResourceAndQty resourceAndQty = (increment ? recipe.ModProduceQty(item, q, module) : recipe.ModConsumeQty(item, q, module));
			if (increment)
			{
				_ = resourceAndQty.qty < 0;
			}
			if (!increment)
			{
				_ = resourceAndQty.qty > 0;
			}
			if (disabled == null || !disabled.Contains(resourceAndQty))
			{
				inventory.data.Increment(resourceAndQty);
				if (resourceAndQty.FindResource().rescat == BOOZE && q.manager != null)
				{
					q.manager?.components.agent.IncrementStat(CrewStats.BoozeProduced, (int)resourceAndQty.qty);
				}
				if (increment)
				{
					data.lastProduced.Increment(resourceAndQty);
				}
			}
		}
	}

	private void ProduceGrantsOnSuccess(Recipe recipe, ModuleQuery q)
	{
		if (recipe?.grants != null && q.IsBuilding)
		{
			Entity container = q.container;
			PlayerID controllingPlayer = container.components.building.GetControllingPlayer();
			BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(container);
			SimTime now = Game.ctx.clock.Now;
			VisitState visit = new VisitState(CrewAssignment.EMPTY, bbdata, now, controllingPlayer);
			GrantContext ctx = new GrantContext(visit);
			recipe.grants.ApplyAll(ctx);
		}
	}
}

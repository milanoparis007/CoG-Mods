using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class ConsumerModule : Module<ConsumerModule, ConsumerModuleConfig, ConsumerModuleData>, IBizModule, IModule
{
	private static readonly Label BOOZE = new Label("rescat-booze");

	public Label BizModuleID => config.id;

	public bool IsInteresting => config.interesting;

	public BizModuleLocData LocData => config.sink;

	public bool DidModuleConsumeThisTurn => data.isWorking;

	public override void Initialize(ModuleInitData init)
	{
		base.Initialize(init);
		if (init.IsCreated)
		{
			data.lastUpdate = Game.ctx.clock.Now;
		}
	}

	public override void Release(bool shutdown)
	{
		base.Release(shutdown);
	}

	public IEnumerable<MfgItem> ProduceAllItemsInCurrentRecipe()
	{
		return config.sink.ProduceAllItems(config);
	}

	public DeliveryInfo ProduceDeliveryInfo(Label resId, ModuleQuery q)
	{
		return config.sink.ProduceDeliveryInfo(resId, q, this);
	}

	public Fixnum ProduceBuyCap(Label resId, ModuleQuery q)
	{
		return config.sink.ProduceBuyCap(resId, q, null);
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
		var (num, fixnum) = ComputeConsumeIntervals(time, initial, q);
		if (fixnum <= 0)
		{
			return moduleResult;
		}
		if (fixnum > 1)
		{
			fixnum = 1;
		}
		data.lastConsumed.Clear();
		moduleResult |= DoConsumeAndPay(q, inventory, fixnum, num);
		DoRefillInventory(q, inventory);
		SpreadRespectToNodesInAOE(q);
		ModulesUtil.GiveXPToManager(q, moduleResult, num);
		data.monthlyStats.Add(q, data.lastConsumed, time);
		data.lastUpdate = time;
		return moduleResult;
	}

	internal (int crossed, Fixnum intervals) ComputeConsumeIntervals(SimTime time, bool isInitialVisit, ModuleQuery q)
	{
		if (q.OwnerIsHumanPlayer && isInitialVisit)
		{
			return (crossed: 0, intervals: 0);
		}
		if (isInitialVisit)
		{
			return (crossed: 0, intervals: 1);
		}
		int num = config.sink.ModConsumeDays(q, this);
		if (num <= 1)
		{
			return (crossed: 0, intervals: 1);
		}
		Fixnum item = (Fixnum)MathUtil.ClampMin(time.days - data.lastUpdate.days, 0) / (Fixnum)num;
		return (crossed: MathUtil.CountIntervals(data.lastUpdate.days, time.days, num), intervals: item);
	}

	private void DoRefillInventory(ModuleQuery q, InventoryModule inventory)
	{
		if (q.OwnerIsHumanPlayer)
		{
			return;
		}
		foreach (RefillElement item in config.sink.AllRefill)
		{
			if (item.get < 0)
			{
				Logger.Warning($"Refill recipe should not be negative in {item.id}, {item.get}");
			}
			bool canFakeRefill = inventory.data.Get(item.id).qty < item.below;
			this.TryDeliveryOrFakeIt(q, inventory, item.id, item.get, canFakeRefill);
		}
	}

	[Conditional("UNITY_EDITOR")]
	private void ValidateInventoryCapacity(Entity building, InventoryModule inventory)
	{
		if (inventory.GetUsedCapacityAsPercent() > 1f)
		{
			BuildingUtil.FindBizForBuilding(building);
		}
	}

	private ModuleResult DoConsumeAndPay(ModuleQuery q, InventoryModule inventory, Fixnum fractionalIntervals, int crossedIntervals)
	{
		if (config.sink?.AllConsume == null)
		{
			return ModuleResult.Default;
		}
		if (config.sink.AllConsume.Count == 0)
		{
			return ModuleResult.Default;
		}
		if (q.container.components.building.HasDamage())
		{
			return ModuleResult.BuildingDamaged;
		}
		bool flag = false;
		Entity container = q.container;
		Price delta = default(Price);
		foreach (ResourceAndQty item in config.sink.AllConsume)
		{
			ResourceAndQty resourceAndQty = config.sink.ModConsumeQty(item, q, this);
			_ = resourceAndQty.qty > 0;
			Fixnum b = resourceAndQty.qty * fractionalIntervals;
			Fixnum fixnum = Fixnum.Max(-inventory.data.Get(resourceAndQty.id).qty, b);
			if (!(fixnum >= 0))
			{
				inventory.data.Increment(resourceAndQty.id, fixnum);
				data.lastConsumed.Increment(resourceAndQty.id, -fixnum);
				data.lifetimeConsumed.Increment(resourceAndQty.id, -fixnum);
				flag = true;
				if (q.OwnerIsHumanPlayer)
				{
					Fixnum fixnum2 = config.sink.aoe.priceMarkup.Evaluate(q.MakeManagerModQuery());
					Price price = resourceAndQty.FindResource().sell * fixnum2 * fixnum.Abs;
					delta += price;
				}
				if (item.FindResource().rescat == BOOZE && q.manager != null)
				{
					q.manager?.components.agent.IncrementStat(CrewStats.BoozeSold, (int)(-fixnum));
				}
			}
		}
		data.isWorking = flag;
		if (q.OwnerIsHumanPlayer && flag)
		{
			Game.ctx.players.Human.finances.DoChangeMoney(container, delta, MoneyReason.BusinessIncome, container.Id);
		}
		if (flag && crossedIntervals > 0)
		{
			ProduceGrantsOnSuccess(config.sink, q);
		}
		if (!flag)
		{
			return ModuleResult.ConsOutOfInputs;
		}
		return ModuleResult.ConsCompleted;
	}

	private void ProduceGrantsOnSuccess(ConsumerRecipe recipe, ModuleQuery q)
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

	public void SpreadRespectToNodesInAOE(ModuleQuery q)
	{
		if (!DidModuleConsumeThisTurn || !q.OwnerExists)
		{
			return;
		}
		ConsumerRecipe.AreaDef aoe = config.sink.aoe;
		if (aoe.respectRadius == null || aoe.respectPointsInRadius == null)
		{
			return;
		}
		Node node = q.container.components.board.GetNode();
		ModQuery query = q.MakeManagerModQuery();
		query.nodeId = node.id;
		Fixnum fixnum = aoe.respectRadius.Evaluate(query);
		Fixnum delta = aoe.respectPointsInRadius.Evaluate(query);
		using ListPool<Node>.PooledBlockList pooledBlockList = ListPool<Node>.Allocate();
		Game.ctx.board.nodes.FindAndSortNodesInRadius(node.pos, (float)fixnum, sort: false, pooledBlockList);
		foreach (Node item in pooledBlockList)
		{
			item.respect.IncrementAOERespect(q.pid, delta);
		}
	}
}

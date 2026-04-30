using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public static class BuySellUtils
{
	public static readonly Label BOOZE = new Label("rescat-booze");

	public static Fixnum FindMaxAffordedToBuySell(PlayerInfo player, Entity building, Entity biz, InventoryModule target, Label item, Fixnum desiredQty, bool isPlayerBuying, CrewAssignment crew)
	{
		new VisitState(crew, BuildingUtil.FindDataForBuilding(building), Game.ctx.clock.Now, player.PID);
		if (isPlayerBuying)
		{
			Fixnum cash = target.data.money.cash;
			return FindHowMuchPlayerCanAffordToBuy(player, biz, item, desiredQty, cash);
		}
		foreach (BuySellElement item2 in building.components.modules.ProduceAllItemsPlayerCanBuyOrSell(player.PID, playerBuys: false, playerSells: true))
		{
			if (item2.item.id == item)
			{
				if (desiredQty > item2.qty)
				{
					desiredQty = item2.qty;
				}
				if (desiredQty < 0)
				{
					desiredQty = 0;
				}
			}
		}
		return desiredQty.Floor();
	}

	private static Fixnum FindHowMuchPlayerCanAffordToBuy(PlayerInfo player, Entity biz, Label item, Fixnum desiredQty, Fixnum cashAvailable)
	{
		Resource res = Resource.Find(item);
		Price price = FindPriceNegotiatedAbs(player, biz, res, playerBuys: true);
		if (!price.IsPositive)
		{
			return desiredQty;
		}
		Fixnum b = cashAvailable / price.cash;
		return Fixnum.Min(desiredQty, b).Floor();
	}

	public static Price FindPriceNegotiatedAbs(PlayerInfo player, Entity biz, Resource res, bool playerBuys)
	{
		var (flag, multiplier) = biz.components.biz.GetDiscount(player.PID, res);
		if (!flag)
		{
			multiplier = 1;
		}
		return res.GetPriceWithMultiplier(playerBuys, multiplier, player.PID);
	}

	public static (bool success, Fixnum playerMoneyDelta) ExecuteHumanBuySell(PlayerInfo player, VisitState visit, ConvoDataBuySell data, QtyAndDir qtyAndDir, bool scheduled)
	{
		return ExecuteHumanBuySell(player, visit.crew, visit.building, data.FindResource(), qtyAndDir, scheduled);
	}

	public static (bool success, Fixnum playerMoneyDelta) ExecuteHumanBuySell(PlayerInfo player, CrewAssignment crew, Entity building, Resource res, QtyAndDir qtyAndDir, bool recurring)
	{
		if (!qtyAndDir.qty.IsPositive)
		{
			return (success: false, playerMoneyDelta: 0);
		}
		InventoryModule inventory = ModulesUtil.GetInventory(crew);
		InventoryModule inventory2 = ModulesUtil.GetInventory(building);
		Entity biz = BuildingUtil.FindBizForBuilding(building);
		Fixnum toBuilding = qtyAndDir.ToBuilding;
		Fixnum toPlayer = qtyAndDir.ToPlayer;
		Price delta = FindPriceNegotiatedAbs(player, biz, res, qtyAndDir.IsToPlayer) * qtyAndDir.ToBuilding;
		player.finances.DoChangeMoney(inventory.data, delta, recurring ? MoneyReason.ScheduledBuySell : MoneyReason.BuySell);
		inventory.data.Increment(res.resid, toPlayer);
		inventory2.data.Increment(res.resid, toBuilding);
		AddTradeBuff(player.PID, crew, building, res, recurring);
		RecordBuySell(player.PID, biz, res.resid, toBuilding);
		player.social.Transactions.AddTransaction(crew.GetPeep(), res.resid, qtyAndDir);
		if (res.rescat == BOOZE && toBuilding > 0)
		{
			crew.GetPeep()?.components.agent.IncrementStat(CrewStats.BoozeSold, (int)toBuilding);
		}
		crew.GetPeep()?.components.agent.IncrementStat(CrewStats.TotalTransactions, 1);
		return (success: true, playerMoneyDelta: delta.cash);
	}

	public static void ExecuteAIBuySell(PlayerInfo player, Entity peep, Entity building, Label resId, bool buying)
	{
		PlayerID pID = player.PID;
		Entity entity = BuildingUtil.FindBizForBuilding(building);
		BizComponent.TradeRestrictions tradeRestrictions = entity?.components.biz.FindTradeRestrictions(pID) ?? default(BizComponent.TradeRestrictions);
		if (entity == null || tradeRestrictions.IsLocked)
		{
			return;
		}
		CrewAssignment crew = peep.components.agent.FindCrewAssignment();
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		InventoryModule inventory2 = ModulesUtil.GetInventory(crew);
		Fixnum value = Fixnum.Clamp(player.ai?.business?.FindTradeEfficiency() ?? ((Fixnum)1), 0, 1);
		int? max = (buying ? ((int?)null) : FindSellOffLimit(building, resId));
		Fixnum fixnum = (buying ? ModulesUtil.TransferResourceRespectingLimits(inventory, inventory2, resId, null, value) : (-ModulesUtil.TransferResourceRespectingLimits(inventory2, inventory, resId, max, value)));
		if (fixnum.IsNotZero)
		{
			AddTradeBuff(pID, crew, building, Resource.Find(resId), recurring: false);
			RecordBuySell(pID, entity, resId, -fixnum);
		}
		if (!buying)
		{
			Fixnum qty = inventory2.data.Get(resId).qty;
			if (qty > 0)
			{
				inventory2.data.Increment(resId, -qty);
			}
		}
		player.ai?.business?.OnExecuteAIBuySellComplete(building.Id, peep.Id);
	}

	private static int? FindSellOffLimit(Entity building, Label resId)
	{
		ModuleQuery q = ModulesUtil.MakeModuleQuery(building);
		DeliveryInfo deliveryInfo = ModulesUtil.FindItemDeliveryFromModules(building, q, resId);
		if (deliveryInfo.IsNotValid)
		{
			return null;
		}
		return deliveryInfo.max.qty.Abs.IntFloor();
	}

	public static void ExecuteAIPickUpDropOff(PlayerInfo player, Entity peep, Entity building, Label resId, bool peepPicksUp)
	{
		PlayerID pID = player.PID;
		if (!building.components.building.IsControlledBy(pID))
		{
			_ = peep.data.person.FullName;
			return;
		}
		CrewAssignment crew = peep.components.agent.FindCrewAssignment();
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		InventoryModule inventory2 = ModulesUtil.GetInventory(crew);
		Fixnum value = Fixnum.Clamp(player.ai?.business?.FindTradeEfficiency() ?? ((Fixnum)1), 0, 1);
		if (!peepPicksUp)
		{
			_ = -ModulesUtil.TransferResourceRespectingLimits(inventory2, inventory, resId, null, value);
		}
		else
		{
			ModulesUtil.TransferResourceRespectingLimits(inventory, inventory2, resId, null, value);
		}
		if (!peepPicksUp)
		{
			Fixnum qty = inventory2.data.Get(resId).qty;
			if (qty > 0)
			{
				inventory2.data.Increment(resId, -qty);
			}
		}
	}

	private static Relationship FindRelFromBuildingOwnerToPlayer(PlayerID pid, Entity building)
	{
		if (building.components.building == null)
		{
			return null;
		}
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
		if (entity == null)
		{
			return null;
		}
		return pid.FindPlayer().social.GetRelationshipFromSourceToPlayer(entity.Id);
	}

	private static bool AddTradeBuff(PlayerID pid, CrewAssignment crew, Entity building, Resource res, bool recurring)
	{
		Relationship relationship = FindRelFromBuildingOwnerToPlayer(pid, building);
		if (relationship == null)
		{
			return false;
		}
		relationship.AddTradeStat(crew.GetPeep(), res, recurring);
		relationship.AddBuff(recurring ? BuffConstants.TRADE_RECURRING_STATS_BUFF : BuffConstants.TRADE_ONETIME_STATS_BUFF, crew.peepId);
		relationship.AddBuff(BuffConstants.TRADE_FROM_CREW_PERSONALITY, crew.peepId);
		if (res.GetIsIllegal())
		{
			Node node = building.components.board.GetNode();
			node.heat.AddTradeHeatBuff(pid, building, crew.peepId, node, recurring);
		}
		return true;
	}

	private static void RecordBuySell(PlayerID pid, Entity biz, Label resid, Fixnum qtyToBuilding)
	{
		biz.components.biz.RecordBuySell(pid, resid, qtyToBuilding);
	}
}

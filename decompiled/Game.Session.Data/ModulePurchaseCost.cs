using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.Session.Data;

public class ModulePurchaseCost
{
	public TagList size;

	public TagList verticals;

	public Price cashCost;

	public CrewCost crewCost;

	public ModValue buildTurns;

	public List<ResourceAndQty> consume = new List<ResourceAndQty>();

	public bool CanPlayerAfford(PlayerID pid, Entity entity)
	{
		InventoryModule inventory = entity.components.modules.inventory;
		BusinessSettings.GlobalModuleModifiers globalModuleModifiers = Game.serv.globals.settings.people.businessSettings.globalModuleModifiers;
		Node node = entity?.components.board.GetNode();
		Fixnum costMultiplier = globalModuleModifiers.buildCostModifier.Evaluate(pid, node, entity, null);
		if (CanPlayerAffordPrice(pid, entity, costMultiplier))
		{
			return CanPlayerConsume(inventory, costMultiplier);
		}
		return false;
	}

	private bool CanPlayerAffordPrice(PlayerID pid, Entity entity, Fixnum costMultiplier)
	{
		return Game.ctx.players.WithID(pid).finances.CanChangeMoney(entity, cashCost * costMultiplier);
	}

	private bool CanPlayerConsume(InventoryModule inventory, Fixnum costMultiplier)
	{
		foreach (ResourceAndQty item in consume)
		{
			Fixnum fixnum = item.qty * costMultiplier;
			if (fixnum > 0)
			{
				return false;
			}
			if (inventory == null)
			{
				return false;
			}
			if (!inventory.data.WillBeNonNegative(item.id, fixnum))
			{
				return false;
			}
		}
		return true;
	}

	public bool DoSpendAndConsume(PlayerID pid, Entity container, MoneyReason reason)
	{
		InventoryModule inventory = container.components.modules.inventory;
		BusinessSettings.GlobalModuleModifiers globalModuleModifiers = Game.serv.globals.settings.people.businessSettings.globalModuleModifiers;
		Node node = container?.components.board.GetNode();
		Fixnum costMultiplier = globalModuleModifiers.buildCostModifier.Evaluate(pid, node, container, null);
		if (DoSpendCash(pid, container, reason, costMultiplier))
		{
			return DoConsume(inventory, costMultiplier);
		}
		return false;
	}

	internal bool DoSpendCash(PlayerID pid, Entity container, MoneyReason reason, Fixnum costMultiplier)
	{
		Game.ctx.players.WithID(pid).finances.DoChangeMoney(container, cashCost * costMultiplier, reason);
		return true;
	}

	internal bool DoConsume(InventoryModule inventory, Fixnum costMultiplier)
	{
		bool flag = true;
		foreach (ResourceAndQty item in consume)
		{
			if (inventory == null)
			{
				return false;
			}
			flag = flag && inventory.data.Increment(item.id, item.qty * costMultiplier);
		}
		return flag;
	}

	public (int turns, int days) FindInstallTime(PlayerID pid, Entity building)
	{
		Node node = building?.components.board.GetNode();
		int num = (int)(buildTurns?.Evaluate(pid, node, building, null) ?? ((Fixnum)0));
		return (turns: num, days: num * Game.ctx.clock.DaysPerTurn);
	}

	public int FindInstallDays(PlayerID pid, Entity building)
	{
		return FindInstallTime(pid, building).days;
	}

	public Fixnum FindInstallDonePercentage(PlayerID pid, Entity building, int daysleft)
	{
		Fixnum fixnum = FindInstallDays(pid, building);
		Fixnum fixnum2 = fixnum - daysleft;
		if (!(fixnum > 0))
		{
			return 0;
		}
		return fixnum2 / fixnum;
	}

	public (bool valid, string explanation) Explain(PlayerID pid, Entity entity)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		bool flag = true;
		BusinessSettings.GlobalModuleModifiers globalModuleModifiers = Game.serv.globals.settings.people.businessSettings.globalModuleModifiers;
		Node node = entity?.components.board.GetNode();
		Fixnum costMultiplier = globalModuleModifiers.buildCostModifier.Evaluate(pid, node, entity, null);
		if (cashCost.IsNonZero)
		{
			stringBuilder.Append(FeedbackUtil.Explain(cashCost, pid, entity, costMultiplier, out var valid));
			flag = flag && valid;
		}
		if (consume != null && consume.Count > 0)
		{
			InventoryModule inventory = entity.components.modules.inventory;
			stringBuilder.Append(FeedbackUtil.Explain(consume, inventory, costMultiplier, out var valid2));
			flag = flag && valid2;
		}
		if (crewCost.IsNonZero)
		{
			stringBuilder.Append(FeedbackUtil.Explain(crewCost));
		}
		if (buildTurns != null)
		{
			int num = FindInstallDays(pid, entity);
			int num2 = Game.ctx.clock.DaysToTurnsRoundedUp(num);
			string message = Loc.Get("module.install.construct-time", "turns", num2, "days", num);
			stringBuilder.AppendLine(TextUtil.ColorWrap(message, ColorConstants.TEXT_HEX_CONSTRUCTION));
		}
		return (valid: flag, explanation: stringBuilder.ToStringAndReturnToPool());
	}

	public Price GetDestroyPrice(PlayerID pid, Entity entity)
	{
		BusinessSettings businessSettings = Game.serv.globals.settings.people.businessSettings;
		Node node = entity?.components.board?.GetNode();
		Fixnum fixnum = businessSettings.globalModuleModifiers.destroyCostModifier.Evaluate(pid, node, entity, null);
		return new Price(cashCost.cash * fixnum);
	}

	public bool CanPlayerAffordDestroy(PlayerID pid, Entity entity)
	{
		Price destroyPrice = GetDestroyPrice(pid, entity);
		return Game.ctx.players.WithID(pid).finances.CanChangeMoney(entity, destroyPrice);
	}

	public bool DoPayForDestroy(PlayerID pid, Entity entity, MoneyReason reason)
	{
		Price destroyPrice = GetDestroyPrice(pid, entity);
		Game.ctx.players.WithID(pid).finances.DoChangeMoney(entity, destroyPrice, reason);
		return true;
	}
}

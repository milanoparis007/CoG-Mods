using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public static class FeedbackUtil
{
	public static string Explain(Price price, PlayerID pid, Entity container, Fixnum costMultiplier, out bool valid)
	{
		valid = Game.ctx.players.WithID(pid).finances.CanChangeMoney(container, price * costMultiplier);
		return Loc.IconLine(valid, Loc.Price(price)) + "\n";
	}

	public static string Explain(List<ResourceAndQty> consume, InventoryModule inventory, Fixnum costMultiplier, out bool valid)
	{
		valid = true;
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		foreach (ResourceAndQty item in consume)
		{
			bool flag = inventory.data.WillBeNonNegative(item.id, item.qty * costMultiplier);
			valid &= flag;
			stringBuilder.AppendLine(Loc.IconLine(flag, item.MakeQuantityLocString()));
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	public static string Explain(CrewCost cost)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		if (cost.actions > 0)
		{
			stringBuilder.AppendLine(Loc.Get("ui.crewinfo.act-cost.action", "cost", cost.actions));
		}
		if (cost.moves > 0)
		{
			stringBuilder.AppendLine(Loc.Get("ui.crewinfo.act-cost.move", "cost", cost.moves));
		}
		return stringBuilder.ToStringAndReturnToPool();
	}
}

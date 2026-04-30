using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public sealed class CheckNpcHasLoot : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo playerInfo = visit.npc.components.agent?.GetPlayer();
		if (playerInfo == null)
		{
			return false;
		}
		if (SimTime.Equals(playerInfo.territory.SafehouseData.lastThiefLootDrop, Game.ctx.clock.Now))
		{
			return false;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(playerInfo.territory.Safehouse);
		if (inventory == null)
		{
			return false;
		}
		return inventory.CalculateUsedCapacity().cubicfeet > 0;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ai.hasnoloot"));
	}
}

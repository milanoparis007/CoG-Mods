using System;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player.Commands;

public sealed class AICommandEmptyInventory : InstantAICommand
{
	public AICommandEmptyInventory()
	{
	}

	public AICommandEmptyInventory(PlayerID pid, EntityID eid)
		: base(pid, CommandType.EmptyInventory, eid)
	{
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		Entity entity = peepId.FindEntity();
		IsPeepAtSafehouse();
		InventoryModule inventory = ModulesUtil.GetInventory(GetPlayer().territory.Safehouse.FindEntity());
		InventoryModule inventory2 = ModulesUtil.GetInventory(entity.components.agent.FindCrewAssignment());
		using (ListPool<Resource>.PooledBlockList pooledBlockList = ListPool<Resource>.Allocate())
		{
			pooledBlockList.AddRange(inventory2.data.contents.Select((ResourceAndQty resourceAndQty) => resourceAndQty.FindResource()));
			foreach (Resource item in pooledBlockList)
			{
				int val = inventory.HowManyResourcesCanFit(item);
				Fixnum qty = inventory2.data.Get(item).qty;
				int num = Math.Min(val, (int)qty);
				inventory.data.Increment(item.resid, num);
				inventory2.data.Increment(item.resid, -num);
			}
		}
		ConsumePeepActions();
	}
}

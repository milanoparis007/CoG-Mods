using System;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player.Commands;

public class AICommandBurgleBuilding : InstantAICommand
{
	public EntityID targetBuildingId;

	public AICommandBurgleBuilding()
	{
	}

	public AICommandBurgleBuilding(PlayerID pid, EntityID eid, EntityID targetBuildingId)
		: base(pid, CommandType.BurgleBuilding, eid)
	{
		this.targetBuildingId = targetBuildingId;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		InventoryModule inventory = ModulesUtil.GetInventory(targetBuildingId);
		if (inventory == null)
		{
			return;
		}
		Entity vehicle = GetPlayer().crew.GetCrewForPeep(peepId).GetVehicle();
		if (vehicle != null)
		{
			InventoryModule inventory2 = ModulesUtil.GetInventory(vehicle);
			using ListPool<Resource>.PooledBlockList pooledBlockList = ListPool<Resource>.Allocate();
			foreach (ResourceAndQty content in inventory.data.contents)
			{
				Resource resource = content.FindResource();
				if (resource.GetIsIllegal())
				{
					pooledBlockList.Add(resource);
				}
			}
			peepId.FindEntity().data.ident.rng.Shuffle(pooledBlockList);
			foreach (Resource item in pooledBlockList)
			{
				int val = inventory2.HowManyResourcesCanFit(item);
				int num = Math.Min((int)inventory.data.Get(item).qty, val);
				inventory2.data.Increment(item.resid, num);
				inventory.data.Increment(item.resid, -num);
			}
		}
		else
		{
			Game.serv.stats.LogException($"{this}: car is null for {pid} {peepId}");
		}
		ConsumePeepActions();
	}
}

using System.Collections.Generic;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public sealed class CheckTargetHasSpaceForProductionReqs : AbstractVisitRequirement
{
	public int multiplier;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		Entity building = visit.building;
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		if (building.components.modules.FindBackroomModule() is ManufactureModule manufactureModule)
		{
			List<ResourceAndQty> consume = manufactureModule.CurrentRecipe.consume;
			return inventory.CanResourceListFit(consume, multiplier) == expected;
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

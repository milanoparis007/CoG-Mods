using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class GrantProductionRequiredResources : VisitGrant
{
	public int multiplier;

	public override GrantReq RequiredContext => GrantReq.VisitBuilding;

	public override void Apply(GrantContext ctx)
	{
		Entity building = ctx.visit.building;
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		if (!(building.components.modules.FindBackroomModule() is ManufactureModule manufactureModule))
		{
			return;
		}
		foreach (ResourceAndQty item in manufactureModule.CurrentRecipe.consume)
		{
			ResourceAndQty current = item;
			current.qty = -(current.qty * multiplier);
			inventory.TryAddResourcesIfSpaceAvailable(current.id, (int)current.qty);
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.production-required-resources");
	}
}

using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class AddResource : VisitGrant
{
	public enum Target
	{
		Crew,
		Safehouse,
		Building,
		Tag
	}

	public Label id;

	public int qty;

	public Target at;

	public Label buildingTag;

	public bool ignoreCapacity;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		if (GetResource() == null)
		{
			Label label = id;
			Logger.Warning("Add resource grant: invalid resource id " + label.ToString());
			return;
		}
		if (!player.skills.HasResourceUnlocked(id))
		{
			player.skills.UnlockResource(id, startup: false);
		}
		Entity entity = player.territory.Safehouse.FindEntity();
		Entity entity2 = ((at == Target.Crew) ? GetCrewPeepVehicle(ctx) : ((at == Target.Building) ? ctx.visit.building : ((at == Target.Tag) ? player.territory.FindFirstControlledBuildingWithTag(buildingTag) : ctx.buildingTarget.FindEntity())));
		if (entity2 == null)
		{
			entity2 = entity;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(entity2);
		if (inventory == null)
		{
			Logger.Warning("Can't apply resource grant, no inventory found");
			return;
		}
		int num;
		if (ignoreCapacity)
		{
			num = inventory.ForceAddResourcesRegardlessOfSpace(id, qty);
			return;
		}
		num = inventory.TryAddResourcesIfSpaceAvailable(id, qty);
		if (at == Target.Crew && num != qty)
		{
			ModulesUtil.GetInventory(entity).TryAddResourcesIfSpaceAvailable(id, qty - num);
		}
	}

	private Entity GetCrewPeepVehicle(GrantContext ctx)
	{
		return ctx.visit?.vehicle ?? ctx.GetPlayer().crew.GetCrewForPlayerPeep().GetVehicle();
	}

	private Resource GetResource()
	{
		return Game.ctx.simman.FindResource(id);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.addresource.describe", "value", GetResource()?.GetIconNameAndUnits(qty));
	}

	public override SelectorRequest RequestSelector()
	{
		if (at == Target.Safehouse)
		{
			return SelectorRequest.Building;
		}
		return SelectorRequest.None;
	}
}

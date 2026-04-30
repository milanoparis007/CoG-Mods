using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public class TeleportFromVehicleToSafehouse : VisitGrant
{
	public bool resources;

	public bool money;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitVehicle;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		InventoryModule inventory = ModulesUtil.GetInventory(player.territory.Safehouse);
		InventoryModule inventory2 = ModulesUtil.GetInventory(ctx.visit.vehicle);
		if (inventory2 == null || inventory == null)
		{
			return;
		}
		if (money)
		{
			ModulesUtil.TransferCashBetweenPlayerInventories(player.PID, inventory2, inventory);
		}
		if (!resources)
		{
			return;
		}
		foreach (Label item in inventory2.data.contents.SelectIntoNewList((ResourceAndQty raq) => raq.id))
		{
			ModulesUtil.TransferResourceBetweenPlayerInventories(player.PID, inventory2, inventory, item);
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.teleportgoods.describe");
	}
}

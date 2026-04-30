using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class AddCash : VisitGrant
{
	public enum Target
	{
		Crew,
		Safehouse,
		Tag
	}

	public int amount;

	public Target at;

	public Label buildingTag;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep | GrantReq.VisitVehicle;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		Entity entity = ((at == Target.Crew) ? ctx.visit.vehicle : ((at == Target.Tag) ? player.territory.FindFirstControlledBuildingWithTag(buildingTag) : player.territory.Safehouse.FindEntity()));
		if (entity == null)
		{
			entity = player.territory.Safehouse.FindEntity();
		}
		player.finances.DoChangeMoney(entity, new Price(amount), MoneyReason.Other);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.addcash.describe", "value", Loc.Price(new Price(amount)));
	}
}

using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class AddCashAtBuilding : VisitGrant
{
	public int amount;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitBuilding;

	public override void Apply(GrantContext ctx)
	{
		Entity building = ctx.visit.building;
		ctx.GetPlayer().finances.DoChangeMoney(building, new Price(amount), MoneyReason.OrderDelivery);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.addcash.describe", "value", Loc.Price(new Price(amount)));
	}
}

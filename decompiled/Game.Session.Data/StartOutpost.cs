using Game.Services;

namespace Game.Session.Data;

public class StartOutpost : VisitGrant
{
	public bool inside;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		ctx.GetPlayer().outposts.SetOutpost(ctx.visit.building);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_START, TickerTitle.OUTPOST_START, Loc.Get("ui.grants.startoutpost.message"), ctx.visit.building.Id);
	}
}

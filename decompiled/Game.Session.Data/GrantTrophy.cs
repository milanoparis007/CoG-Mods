using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class GrantTrophy : VisitGrant
{
	public Label id;

	public string locsig;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		ctx.visit.GetPlayer().throne.AddToThroneRoom(id, Loc.Get(locsig));
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.trophy", "name", Loc.Get(Game.serv.globals.settings.throne.GetTrophyForId(id).loctitle));
	}
}

using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class GrantKickOffBoard : VisitGrant
{
	public int days;

	public override GrantReq RequiredContext => GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		Entity peep = ctx.visit.peep;
		ctx.GetPlayer().crew.RemoveCrewFromBoard(peep, PlayerCrewData.OffBoardReason.LayingLow, removeCar: true, days);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.kick-off-board");
	}
}

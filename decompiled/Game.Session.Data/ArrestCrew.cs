using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class ArrestCrew : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		CrewAssignment crew = ctx.visit.crew;
		Game.ctx.simman.cops.StartArrest(ctx.GetPlayer(), crew);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.arrest-crew", "name", ctx.visit.crew.GetPeep().data.person.FullName, "city", Game.ctx.session.mapconfig.CityName);
	}
}

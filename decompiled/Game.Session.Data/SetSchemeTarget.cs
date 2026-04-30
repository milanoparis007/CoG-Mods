using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Data;

public class SetSchemeTarget : VisitGrant
{
	public EntityID target;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		Entity peep = ctx.visit.peep;
		ctx.GetPlayer().schemes.GetSchemeForCrew(peep).SetTarget(target);
	}
}

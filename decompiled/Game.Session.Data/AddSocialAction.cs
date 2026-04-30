using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class AddSocialAction : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		ctx.GetPlayer().social.PerformSocialActionOn(id, ctx.visit.npc.Id, ctx.visit.crew.peepId);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.addsocialaction.describe", "name", NameUtils.GetPeepFullName(ctx.visit.npc.Id));
	}
}

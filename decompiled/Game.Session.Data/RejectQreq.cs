namespace Game.Session.Data;

public class RejectQreq : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		Game.ctx.quests.Requests.RejectRequest(ctx.visit);
	}
}

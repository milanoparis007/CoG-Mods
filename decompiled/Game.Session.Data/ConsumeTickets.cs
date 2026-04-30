namespace Game.Session.Data;

public class ConsumeTickets : VisitGrant
{
	public int count = 1;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		ctx.GetPlayer().social.SpendTickets(ctx.visit, count);
	}
}

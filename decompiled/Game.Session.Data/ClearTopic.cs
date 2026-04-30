namespace Game.Session.Data;

public class ClearTopic : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		ctx.visit.topic = null;
	}
}

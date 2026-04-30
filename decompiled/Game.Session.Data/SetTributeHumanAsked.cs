namespace Game.Session.Data;

public class SetTributeHumanAsked : VisitGrant
{
	public bool value = true;

	public override GrantReq RequiredContext => GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		ctx.visit.biz.components.biz.SetHumanMentionedTribute(value);
	}
}

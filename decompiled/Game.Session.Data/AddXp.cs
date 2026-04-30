using Game.Services;

namespace Game.Session.Data;

public class AddXp : VisitGrant
{
	public XPSource source;

	public int delta;

	public override GrantReq RequiredContext => GrantReq.VisitState | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		int num = delta + ctx.visit.peep.components.agent.FindXPFor(source).value;
		if (num > 0)
		{
			ctx.visit.peep.components.agent.AddXP(num);
		}
	}
}

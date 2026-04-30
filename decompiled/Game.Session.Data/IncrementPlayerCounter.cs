using Game.Core;

namespace Game.Session.Data;

public class IncrementPlayerCounter : VisitGrant
{
	public Label id;

	public int delta;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		ctx.GetPlayer().skills.IncrementCounter(id, delta);
	}
}

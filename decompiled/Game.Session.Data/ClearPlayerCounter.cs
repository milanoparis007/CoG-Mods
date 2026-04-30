using Game.Core;

namespace Game.Session.Data;

public class ClearPlayerCounter : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		ctx.GetPlayer().skills.RemoveCounter(id);
	}
}

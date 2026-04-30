using Game.Core;

namespace Game.Session.Data;

public class SetPlayerCounter : VisitGrant
{
	public Label id;

	public int value;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		ctx.GetPlayer().skills.SetCounter(id, value);
	}
}

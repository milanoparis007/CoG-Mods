using Game.Core;

namespace Game.Session.Data;

public class SetPlayerFlag : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		ctx.GetPlayer().skills.ToggleFlag(id, value: true);
	}
}

using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class GrantFedTipOffOnRival : VisitGrant
{
	public PlayerID target;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		Game.ctx.players.all.Find((PlayerInfo x) => x.IsJustFed).ai.feds.RequestInvestigation(target);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.fed-tip-off-rival", "rival", Game.ctx.players.WithID(target).social.FindPlayerGroupNameColorized());
	}
}

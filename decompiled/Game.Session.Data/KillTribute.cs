using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class KillTribute : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		Entity biz = ctx.visit.biz;
		if (player.outposts.IsBizPayingTribute(biz))
		{
			player.outposts.StopPayingTribute(ctx.visit.building.Id, EntityID.INVALID);
		}
	}
}

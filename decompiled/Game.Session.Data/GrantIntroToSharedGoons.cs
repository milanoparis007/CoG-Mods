using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class GrantIntroToSharedGoons : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		EntityID playerPeepId = ctx.GetPlayer().social.PlayerPeepId;
		foreach (var item2 in TicketGoonBoosts.FindBoostTargets(ctx.visit))
		{
			Entity item = item2.peep;
			TicketIntroductions.PerformGoonBoostSocialAction(item.Id, playerPeepId);
		}
	}
}

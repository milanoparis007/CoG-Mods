using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public sealed class CheckPeepHasPlayerHistory : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		EntityID playerPeepId = Game.ctx.players.Human.social.PlayerPeepId;
		return Game.ctx.simman.rels.GetOrNull(visit.npc.Id, playerPeepId)?.HasSocialHistory() ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.player.history"));
	}
}

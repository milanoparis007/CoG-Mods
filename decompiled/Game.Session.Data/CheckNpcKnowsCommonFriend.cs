using System.Linq;
using Game.Services;

namespace Game.Session.Data;

public sealed class CheckNpcKnowsCommonFriend : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return Game.ctx.players.Human.social.TicketActionFindBoostTargets(visit.npc).Any();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ai.knowscommonfriend.none"));
	}
}

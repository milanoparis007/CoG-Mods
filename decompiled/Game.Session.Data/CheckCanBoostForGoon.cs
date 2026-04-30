using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckCanBoostForGoon : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return TicketGoonBoosts.CanBoostForGoon(visit);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.goon.boost"));
	}
}

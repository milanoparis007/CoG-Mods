using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckCanIntroForBusiness : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return TicketModuleIntroductions.CanProduceModuleIntroCandidate(visit);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.biz.intro"));
	}
}

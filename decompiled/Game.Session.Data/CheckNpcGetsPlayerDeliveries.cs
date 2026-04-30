using Game.Services;

namespace Game.Session.Data;

public sealed class CheckNpcGetsPlayerDeliveries : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		if (!visit.AtValidBiz)
		{
			return false;
		}
		return visit.building?.components.delivery?.HasRecentHumanDelivery() == true;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ai.trades.none"));
	}
}

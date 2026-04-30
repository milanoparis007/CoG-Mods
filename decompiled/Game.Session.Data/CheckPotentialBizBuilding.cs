using Game.Services;

namespace Game.Session.Data;

public class CheckPotentialBizBuilding : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).territory.FindPotentialBuildingForTakeover(visit).IsValid;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.biz.rent"));
	}
}

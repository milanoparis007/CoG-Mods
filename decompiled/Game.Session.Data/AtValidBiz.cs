using Game.Services;

namespace Game.Session.Data;

public sealed class AtValidBiz : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return visit.AtValidBiz;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.location.biz-req"));
	}
}

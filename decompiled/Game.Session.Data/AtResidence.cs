using Game.Services;

namespace Game.Session.Data;

public sealed class AtResidence : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return visit.building?.components.residence != null;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.location.res-req"));
	}
}

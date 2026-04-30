using Game.Services;

namespace Game.Session.Data;

public class CheckOutpostCanStartExpanding : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).outposts.CanStartNewOutpostExpansion(visit.building) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.expansion-avail.expected") : Loc.Get("ui.requirements.expansion-avail.unexpected"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}

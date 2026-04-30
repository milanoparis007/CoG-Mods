using Game.Services;

namespace Game.Session.Data;

public class CheckOutpostExpanding : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).outposts.IsOutpostExpanding(visit.building) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.expansion.expected") : Loc.Get("ui.requirements.expansion.unexpected"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}

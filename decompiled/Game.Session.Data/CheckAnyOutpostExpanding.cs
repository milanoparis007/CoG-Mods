using Game.Services;

namespace Game.Session.Data;

public class CheckAnyOutpostExpanding : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).outposts.IsAnyOutpostExpanding() == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.expansion.any.expected") : Loc.Get("ui.requirements.expansion.any.unexpected"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}

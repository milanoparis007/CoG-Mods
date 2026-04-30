using Game.Session.Entities;

namespace Game.Session.Data;

public class CheckSchemeTargetIsInTerritory : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return visit.GetPlayer().schemes.GetSchemeForCrew(visit.peep).overallTarget.FindEntity().components.board.GetNode().owner.Is(visit.GetPlayer().PID) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

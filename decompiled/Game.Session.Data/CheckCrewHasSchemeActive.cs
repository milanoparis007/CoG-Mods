namespace Game.Session.Data;

public class CheckCrewHasSchemeActive : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		bool flag = visit.GetPlayer().schemes.GetSchemeForCrew(visit.crew.peepId) != null;
		return expected == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

namespace Game.Session.Data;

public class CheckSchemeCrewAlive : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return visit.npc.data.person.IsAlive;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

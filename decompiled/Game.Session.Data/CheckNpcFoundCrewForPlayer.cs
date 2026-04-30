namespace Game.Session.Data;

public sealed class CheckNpcFoundCrewForPlayer : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		if (!visit.AtValidBiz)
		{
			return false;
		}
		return GetPlayer(visit).crew.FindFirstCrewIntroducedBy(visit.npc.Id).IsValid;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

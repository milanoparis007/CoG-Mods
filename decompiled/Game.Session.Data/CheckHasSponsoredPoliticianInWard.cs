using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckHasSponsoredPoliticianInWard : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		Ward ward = ((visit.building != null) ? Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id) : Game.ctx.simman.politics.GetWardForID(visit.GetCrewNode().precinctId));
		if (ward == null)
		{
			return false;
		}
		return expected == ward.currElection.HasSponsoredACandidate(visit.GetPlayer().PID);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

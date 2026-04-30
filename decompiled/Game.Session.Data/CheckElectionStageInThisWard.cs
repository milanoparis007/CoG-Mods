using Game.Services;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckElectionStageInThisWard : AbstractVisitRequirement
{
	public Election.ElectionStage stage;

	public bool expected = true;

	public override bool DoesPass(VisitState visit)
	{
		Ward ward = ((visit.building != null) ? Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id) : Game.ctx.simman.politics.GetWardForID(visit.GetCrewNode().precinctId));
		if (ward == null)
		{
			return false;
		}
		if (ward.currElection == null)
		{
			return stage == Election.ElectionStage.Offcycle == expected;
		}
		return ward.currElection.stage == stage == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.election-stage-in-ward", "phase", stage.ToString()));
	}
}

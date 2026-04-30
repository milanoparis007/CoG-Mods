using Game.Services;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckNpcIsIncumbentPolitician : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		if (wardForBuilding == null)
		{
			return false;
		}
		return wardForBuilding.currentPolitician == visit.npc.Id == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.incumbent"));
	}
}

using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public sealed class CheckCasinoHasBannableGamblers : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		GamblingModule gambling = visit.building.components.modules.gambling;
		return visit.GetPlayer().gambling.FindBannableGamblers(gambling).Count > 0;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

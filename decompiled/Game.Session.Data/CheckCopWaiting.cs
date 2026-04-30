using Game.Session.Player.AI;
using Game.Session.Sim;

namespace Game.Session.Data;

public class CheckCopWaiting : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return CopUtil.FindPrecinctOrNull(visit.npc)?.ai.precinct?.HasDonationFrom(GetPlayer(visit).PID) == DonationState.WaitingForRefresh == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

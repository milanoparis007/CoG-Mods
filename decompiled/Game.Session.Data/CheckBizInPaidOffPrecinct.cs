using Game.Services;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckBizInPaidOffPrecinct : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo obj = visit.building?.components.board?.GetNode()?.precinctId.FindPrecinct();
		return (obj != null && obj.ai.precinct?.HasDonationFrom(GetPlayer(visit).PID) == DonationState.PaidOff) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get(expected ? "ui.requirements.precinct-paidoff.expected" : "ui.requirements.precinct-paidoff.unexpected"));
	}
}

using Game.Services;
using Game.Session.Player.AI;
using Game.Session.Sim;

namespace Game.Session.Data;

public class CheckCopDonation : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return CopUtil.FindPrecinctOrNull(visit.npc)?.ai.precinct?.HasDonationFrom(GetPlayer(visit).PID) == DonationState.PaidOff == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string key = (expected ? "ui.requirements.cop-donation.expected" : "ui.requirements.cop-donation.unexpected");
		return new ReqExplanation(DoesPass(visit), Loc.Get(key));
	}
}

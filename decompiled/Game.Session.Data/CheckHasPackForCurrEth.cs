using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckHasPackForCurrEth : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return PlayerCrew.HasEthPackForCurrEth();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

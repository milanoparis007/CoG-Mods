using Game.Core;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckHasEthPackAndIsEth : AbstractVisitRequirement
{
	public Label eth;

	public override bool DoesPass(VisitState visit)
	{
		return PlayerCrew.HasEthPackAndIsEth(eth);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

using System.Linq;

namespace Game.Session.Data;

public class SpecialTradingWithEnemyAi : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return visit.biz?.components.biz?.FindTradingAIsAtWarWith(visit.pid).Any() == true == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

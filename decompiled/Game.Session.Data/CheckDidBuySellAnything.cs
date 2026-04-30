using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckDidBuySellAnything : AbstractVisitRequirement
{
	public bool expected = true;

	public override bool DoesPass(VisitState visit)
	{
		return ((visit.biz?.components.biz)?.HasAnyTradeHistory(PlayerID.HumanPlayer) ?? false) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string key = (expected ? "ui.requirements.buysell-any.expected" : "ui.requirements.buysell-any.unexpected");
		return new ReqExplanation(DoesPass(visit), Loc.Get(key));
	}
}

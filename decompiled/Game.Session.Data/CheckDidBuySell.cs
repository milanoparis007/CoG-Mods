using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckDidBuySell : VisitWithValueRequirement
{
	public Label resource;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return (visit.biz?.components.biz)?.FindBuySellQty(PlayerID.HumanPlayer, resource).Abs ?? Fixnum.ZERO;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string text = Resource.Find(resource)?.GetIconNameAndUnits(value);
		string message = Loc.Get("ui.requirements.buysell-qty", "resource-and-qty", text);
		return new ReqExplanation(DoesPass(visit), message);
	}
}

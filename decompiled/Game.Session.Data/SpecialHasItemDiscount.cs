using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialHasItemDiscount : IConvoButtonRequirement, IRequirement
{
	public bool expected;

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		ConvoDataBuySell convoDataBuySell = bstate.data as ConvoDataBuySell;
		if (convoDataBuySell == null)
		{
			Logger.Warning("Unexpected data in item discount:" + convoDataBuySell);
			return false;
		}
		return visit.biz.components.biz.HasDiscount(visit.pid, convoDataBuySell.FindResource()) == expected;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		string message = (expected ? Loc.Get("ui.requirements.discount.expected") : Loc.Get("ui.requirements.discount.unexpected"));
		return new ReqExplanation(DoesPass(visit, bstate), message);
	}
}

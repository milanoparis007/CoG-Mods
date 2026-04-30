using Game.Session.Player;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialBizPurchaseButton : IConvoButtonRequirement, IRequirement
{
	public SpecialBizPurchaseUtil.Type test;

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		PlayerTerritory.TakeoverData takeover = ((bstate.data is ConvoDataTicketBuilding convoDataTicketBuilding) ? convoDataTicketBuilding.takeover : default(PlayerTerritory.TakeoverData));
		return SpecialBizPurchaseUtil.DoesPass(visit, takeover, test);
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		PlayerTerritory.TakeoverData takeover = ((bstate.data is ConvoDataTicketBuilding convoDataTicketBuilding) ? convoDataTicketBuilding.takeover : default(PlayerTerritory.TakeoverData));
		return SpecialBizPurchaseUtil.Explain(visit, takeover, test);
	}
}

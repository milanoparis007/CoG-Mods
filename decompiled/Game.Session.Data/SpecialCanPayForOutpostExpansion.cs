using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForOutpostExpansion : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataOutpostExpansion convoDataOutpostExpansion))
		{
			return null;
		}
		return convoDataOutpostExpansion.monthlyCost;
	}
}

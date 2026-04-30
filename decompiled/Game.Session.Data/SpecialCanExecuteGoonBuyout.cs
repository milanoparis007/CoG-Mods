using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanExecuteGoonBuyout : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataGoonBuyout convoDataGoonBuyout))
		{
			return null;
		}
		return convoDataGoonBuyout.price;
	}
}

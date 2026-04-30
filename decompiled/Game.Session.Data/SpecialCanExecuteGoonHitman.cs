using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanExecuteGoonHitman : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataGoonHitman convoDataGoonHitman))
		{
			return null;
		}
		return convoDataGoonHitman.price;
	}
}

using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForGangRequest : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataGangRequests convoDataGangRequests))
		{
			return null;
		}
		return convoDataGangRequests.price;
	}
}

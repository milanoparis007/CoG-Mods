using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForCloseOutpostDemand : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataCloseOutpost convoDataCloseOutpost))
		{
			return null;
		}
		return convoDataCloseOutpost.bribeInfo.price;
	}
}

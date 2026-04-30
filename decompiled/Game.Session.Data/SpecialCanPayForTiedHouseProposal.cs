using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForTiedHouseProposal : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataTiedHouseProposal convoDataTiedHouseProposal))
		{
			return null;
		}
		return convoDataTiedHouseProposal.price;
	}
}

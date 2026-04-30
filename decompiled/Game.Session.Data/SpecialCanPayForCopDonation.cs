using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForCopDonation : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataCopsDonation convoDataCopsDonation))
		{
			return null;
		}
		return convoDataCopsDonation.price;
	}
}

using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForCopJailBribe : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataCopsJailBribe convoDataCopsJailBribe))
		{
			return null;
		}
		return convoDataCopsJailBribe.payOffCost;
	}
}

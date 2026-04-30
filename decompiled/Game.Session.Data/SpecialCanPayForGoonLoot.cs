using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForGoonLoot : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataGoonRewards convoDataGoonRewards))
		{
			return null;
		}
		return convoDataGoonRewards.cost;
	}
}

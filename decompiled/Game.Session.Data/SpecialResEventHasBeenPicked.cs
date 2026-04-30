using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialResEventHasBeenPicked : IConvoButtonRequirement, IRequirement
{
	public bool expected;

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		ConvoDataResEvent convoDataResEvent = bstate.data as ConvoDataResEvent;
		if (convoDataResEvent == null)
		{
			Logger.Warning("Unexpected data in res event test:" + convoDataResEvent);
			return false;
		}
		return convoDataResEvent.IsValid == expected;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}

using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanGetGangRequest : IConvoButtonRequirement, IRequirement
{
	public bool expected;

	public bool DoesPass(VisitState _, ConvoButtonState button)
	{
		if (button.data is ConvoDataGangRequests convoDataGangRequests)
		{
			return convoDataGangRequests.accept == expected;
		}
		return false;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}

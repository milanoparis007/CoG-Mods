using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialHasValidTiedHouseProposal : IConvoButtonRequirement, IRequirement
{
	public bool expected;

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataTiedHouseProposal convoDataTiedHouseProposal))
		{
			return false;
		}
		return convoDataTiedHouseProposal.IsValid == expected;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}

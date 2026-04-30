using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanExecuteCopFedHint : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataCopsFedHint convoDataCopsFedHint))
		{
			return false;
		}
		return convoDataCopsFedHint.CanAsk;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}

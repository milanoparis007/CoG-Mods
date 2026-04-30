using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCheckHasLegalRevealOptions : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataTicketResourceReveal convoDataTicketResourceReveal))
		{
			return false;
		}
		if (convoDataTicketResourceReveal.constructions.Count <= 0)
		{
			return convoDataTicketResourceReveal.containers.Count > 0;
		}
		return true;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}

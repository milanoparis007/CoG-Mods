using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCheckHasLegalRevealSpecific : IConvoButtonRequirement, IRequirement
{
	public enum RevealType
	{
		Containers,
		Construction
	}

	public RevealType reveal;

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataTicketResourceReveal convoDataTicketResourceReveal))
		{
			return false;
		}
		if (reveal != RevealType.Containers)
		{
			return convoDataTicketResourceReveal.constructions.Count > 0;
		}
		return convoDataTicketResourceReveal.containers.Count > 0;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), Loc.Get("ui.requirements.reveal.known", "items", (reveal == RevealType.Containers) ? Loc.Get("ui.requirements.reveal.type.containers") : Loc.Get("ui.requirements.reveal.type.construction")));
	}
}

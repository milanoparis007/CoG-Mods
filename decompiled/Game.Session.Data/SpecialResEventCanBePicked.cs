using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialResEventCanBePicked : IConvoButtonRequirement, IRequirement
{
	public bool expected;

	public bool DoesPass(VisitState visit, ConvoButtonState _)
	{
		return Game.ctx.simman.resevents.FindPossibleResEventToCreate(PlayerID.HumanPlayer, visit.npc).HasValue == expected;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}

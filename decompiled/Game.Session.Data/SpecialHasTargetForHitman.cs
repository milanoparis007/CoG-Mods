using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialHasTargetForHitman : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		PlayerID pid = visit.npc.data.agent.pid;
		if (!pid.IsAIPlayer)
		{
			return false;
		}
		return visit.GetPlayer().meetings.FindPotentialHitmanTarget(pid) != null;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}

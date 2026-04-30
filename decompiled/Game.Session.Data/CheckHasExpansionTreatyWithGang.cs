using Game.Core;

namespace Game.Session.Data;

public class CheckHasExpansionTreatyWithGang : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		PlayerID pid = visit.npc.data.agent.pid;
		return expected == visit.GetPlayer().ai.territory.HasExpansionTreatyWith(pid);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

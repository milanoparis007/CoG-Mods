using Game.Core;

namespace Game.Session.Data;

public class CheckSchemeCrewInCanada : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		Node node = visit.npc.components.agent.GetNode();
		if (node == null)
		{
			return false;
		}
		return !visit.GetPlayer().schemes.NotInCanada(node) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

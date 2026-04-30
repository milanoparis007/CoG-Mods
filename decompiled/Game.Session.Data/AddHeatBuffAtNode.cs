using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Data;

public class AddHeatBuffAtNode : GrantHeatBuff
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep;

	public override List<Node> GetTargets(GrantContext ctx)
	{
		VisitState visit = ctx.visit;
		return new List<Node> { visit.peep.components.agent.GetNode() };
	}
}

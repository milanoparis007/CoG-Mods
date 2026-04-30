using System.Collections.Generic;
using Game.Core;
using Game.Session.Player;

namespace Game.Session.Data;

public abstract class GrantHeatBuff : VisitGrant
{
	public Label id;

	public bool instant;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		List<Node> targets = GetTargets(ctx);
		EntityID entityID = ctx.visit?.crew.peepId ?? EntityID.INVALID;
		foreach (Node item in targets)
		{
			ModQuery query = new ModQuery(ctx.pid, EntityID.INVALID, entityID, item.id);
			PlayerTerritory territory = ctx.GetPlayer().territory;
			territory.AddHeatBuff(item, id, entityID, query);
			territory.RecomputeHeat(item, instant);
		}
	}

	public abstract List<Node> GetTargets(GrantContext ctx);
}

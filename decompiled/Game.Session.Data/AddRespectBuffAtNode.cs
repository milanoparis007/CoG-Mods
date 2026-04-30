using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class AddRespectBuffAtNode : VisitGrant
{
	public Label id;

	public bool instant;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		Node node = ctx.visit.peep.components.agent.GetNode();
		EntityID entityID = ctx.visit?.crew.peepId ?? EntityID.INVALID;
		ModQuery query = new ModQuery(ctx.pid, EntityID.INVALID, entityID, node.id);
		PlayerTerritory territory = ctx.GetPlayer().territory;
		territory.AddRespectBuff(node, id, entityID, query);
		territory.RecomputeRespect(node, instant);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.addrespectbuffatnode");
	}
}

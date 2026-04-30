using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Data;

public class GrantInfluenceFromPolitician : VisitGrant
{
	public int influenceGain;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitBuilding;

	public override void Apply(GrantContext ctx)
	{
		Entity npc = ctx.visit.npc;
		Game.ctx.simman.politics.DoChangeInfluence(ctx.visit.pid, influenceGain, npc.Id);
	}

	public override string Describe(GrantContext ctx)
	{
		EntityID currentPolitician = ctx.visit.building.components.board.GetNode().precinctId.FindWard().currentPolitician;
		return Loc.Get("ui.grants.influence-politician", "name", currentPolitician.FindEntity().data.person.FullName, "amt", influenceGain);
	}
}

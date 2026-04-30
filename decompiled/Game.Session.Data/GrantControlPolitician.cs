using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Data;

public class GrantControlPolitician : VisitGrant
{
	public int influenceGain;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitBuilding;

	public override void Apply(GrantContext ctx)
	{
		EntityID currentPolitician = ((ctx.visit.building != null) ? Game.ctx.simman.politics.GetWardForBuilding(ctx.visit.building.Id) : Game.ctx.simman.politics.GetWardForID(ctx.visit.GetCrewNode().precinctId)).currentPolitician;
		Game.ctx.simman.politics.GetPoliticianData(currentPolitician).relToHumanInLastElection = PoliticalRelationshipType.Sponsored;
		Game.ctx.simman.politics.DoChangeInfluence(PlayerID.HumanPlayer, influenceGain, currentPolitician);
	}

	public override string Describe(GrantContext ctx)
	{
		EntityID currentPolitician = ((ctx.visit.building != null) ? Game.ctx.simman.politics.GetWardForBuilding(ctx.visit.building.Id) : Game.ctx.simman.politics.GetWardForID(ctx.visit.GetCrewNode().precinctId)).currentPolitician;
		return Loc.Get("ui.grants.control-politician", "name", currentPolitician.FindEntity().data.person.FullName, "amt", influenceGain);
	}
}

using Game.Core;
using Game.Session.Sim;

namespace Game.Session.Data;

public class GrantActionToSupportedPolitician : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		Ward ward = ((ctx.visit.building != null) ? Game.ctx.simman.politics.GetWardForBuilding(ctx.visit.building.Id) : Game.ctx.simman.politics.GetWardForID(ctx.visit.GetCrewNode().precinctId));
		if (ward != null && ward.currElection != null)
		{
			ward.currElection.PerformPlayerCampaignAction(ctx.GetPlayer().PID, id, shouldCauseCooldown: false);
		}
	}
}

using Game.Core;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Sim;

namespace Game.Session.Data;

public class GrantCopsPaidOffInPrecinct : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.VisitBuilding;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo playerInfo = ctx.visit.building.components.board.GetNode().precinctId.FindPrecinct();
		ModQuery query = new ModQuery(ctx.visit.pid, playerInfo.social.PlayerPeepId, ctx.visit.GetPlayer().social.PlayerPeepId);
		playerInfo.ai.precinct.StartHumanDonation(ctx.visit, RaidChecker.Settings.donationFromPolDayz.Evaluate(query).IntCeiling(), 0);
		ctx.visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.PoliticalEvents, 1);
		ctx.visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.CopsBribed, 1);
	}
}

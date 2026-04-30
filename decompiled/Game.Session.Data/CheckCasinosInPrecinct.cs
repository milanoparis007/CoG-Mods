using Game.Services;
using Game.Session.Player;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckCasinosInPrecinct : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		(int, int) precinctGamblingInfo = GetPrecinctGamblingInfo(visit);
		return precinctGamblingInfo.Item1 > precinctGamblingInfo.Item2;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		(int, int) precinctGamblingInfo = GetPrecinctGamblingInfo(visit);
		return new ReqExplanation(DoesPass(visit), Loc.GetPluralized("ui.requirements.casinos-in-precinct", precinctGamblingInfo.Item1, "numAllowed", precinctGamblingInfo.Item1, "numCurrent", precinctGamblingInfo.Item2));
	}

	private (int numAllowed, int numCurrent) GetPrecinctGamblingInfo(VisitState visit)
	{
		PlayerInfo playerInfo = CopUtil.FindPrecinctOrNull(visit.GetBldgNode());
		if (playerInfo == null)
		{
			Logger.Warning("Called CheckCasinoInPrecinct with a visitstate that has a null precinct?");
		}
		int item = Game.ctx.players.Human.gambling.CountAllMyGamblingHousesInPrecinct(playerInfo);
		return (numAllowed: Game.serv.globals.settings.gambling.startup.casinosPerPrecinct.Evaluate(default(ModQuery)).IntFloor(), numCurrent: item);
	}
}

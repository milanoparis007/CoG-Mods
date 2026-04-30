using System.Linq;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Sim;

public class VictoryNoVendetta : VictorySubgoal
{
	public VictoryNoVendetta(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		int num = (from p in Game.ctx.players.all.Where((PlayerInfo p) => p.IsHuman || p.IsJustGang).ToList()
			where p.social.ContainsSocialActionBy(PlayerID.HumanPlayer, SocialConstants.GANG_DEATH)
			select p).Count();
		VictoryResult result = ((num == 0) ? VictoryResult.Pass : VictoryResult.Fail);
		state = new VictorySubgoalState(num, 0, result);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

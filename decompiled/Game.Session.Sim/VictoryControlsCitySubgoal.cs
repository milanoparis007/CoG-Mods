using System.Linq;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryControlsCitySubgoal : VictorySubgoal
{
	public VictoryControlsCitySubgoal(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		Fixnum fixnum = Game.ctx.players.Human.territory.GetAllOwnedNodesUnsafe().Count;
		Fixnum fixnum2 = (from n in Game.ctx.board.nodes.GetAllNodesUnsafe()
			where n.HasRoad
			select n).Count();
		fixnum2 /= (Fixnum)3;
		Fixnum fixnum3 = ((fixnum2 > 0) ? Fixnum.Clamp(fixnum / fixnum2, 0, 1) : ((Fixnum)0));
		Fixnum territoryControl = base.Settings.territoryControl;
		bool pass = fixnum3 >= territoryControl;
		state = new VictorySubgoalState(fixnum3, territoryControl, pass);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalPercent();
	}
}

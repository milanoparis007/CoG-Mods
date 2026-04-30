using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryBoozeStolen : VictorySubgoal
{
	public VictoryBoozeStolen(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		int amtBoozeStolen = Game.ctx.achievements.GetAmtBoozeStolen();
		Fixnum amtBoozeStolen2 = base.Settings.amtBoozeStolen;
		bool pass = amtBoozeStolen >= amtBoozeStolen2;
		state = new VictorySubgoalState(amtBoozeStolen, amtBoozeStolen2, pass);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

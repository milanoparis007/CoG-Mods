using SomaSim.Util;

namespace Game.Session.Sim;

public class VictorySchemesSubgoal : VictorySubgoal
{
	public VictorySchemesSubgoal(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		Fixnum numberOfSchemes = base.Settings.numberOfSchemes;
		int numUniqueFinishedSchemes = Game.ctx.players.Human.schemes.GetNumUniqueFinishedSchemes();
		state = new VictorySubgoalState(numUniqueFinishedSchemes, numberOfSchemes, numUniqueFinishedSchemes >= numberOfSchemes);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

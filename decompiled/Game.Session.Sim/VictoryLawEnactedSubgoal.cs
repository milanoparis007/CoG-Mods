using Game.Core;

namespace Game.Session.Sim;

public class VictoryLawEnactedSubgoal : VictorySubgoal
{
	public Label law;

	public VictoryLawEnactedSubgoal(string locdesc, Label law)
		: base(locdesc)
	{
		this.law = law;
	}

	public override void RecomputeState()
	{
		int num = (Game.ctx.simman.politics.IsLawEnacted(law) ? 1 : 0);
		state = new VictorySubgoalState(num, 1, num >= 1);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

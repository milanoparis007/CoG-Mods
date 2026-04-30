using Game.Core;
using Game.Session.Achievements;

namespace Game.Session.Sim;

public class VictorySpecificSaleSubgoal : VictorySubgoal
{
	public string alcoholId;

	public int goal;

	public VictorySpecificSaleSubgoal(string locdesc, string alcoholId, int goal)
		: base(locdesc)
	{
		this.alcoholId = alcoholId;
		this.goal = goal;
	}

	public override void RecomputeState()
	{
		int boozeInfo = AchievementTests.GetBoozeInfo(new Label(alcoholId));
		int num = goal;
		bool pass = boozeInfo >= num;
		state = new VictorySubgoalState(boozeInfo, num, pass);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

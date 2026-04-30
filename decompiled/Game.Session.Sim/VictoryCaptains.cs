using System.Linq;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryCaptains : VictorySubgoal
{
	public VictoryCaptains(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		Fixnum numberOfCaptains = base.Settings.numberOfCaptains;
		int num = Game.ctx.players.Human.crew.AllCrew.Count(IsCaptain);
		state = new VictorySubgoalState(num, numberOfCaptains, num >= numberOfCaptains);
	}

	private bool IsCaptain(CrewAssignment crew)
	{
		return crew.GetPeep()?.components.agent?.IsCaptain() == true;
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

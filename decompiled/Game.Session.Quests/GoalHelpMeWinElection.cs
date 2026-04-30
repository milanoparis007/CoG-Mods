using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Quests;

public sealed class GoalHelpMeWinElection : BaseGoal
{
	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-win-election";

	public override void OnAdded()
	{
		Game.ctx.simman.politics.GetWardForPolitician(state.target);
		RegisterProgress(GetDidWin(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.ElectionEnded, OnElectionEnded);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.ElectionEnded, OnElectionEnded);
	}

	public void OnElectionEnded(SessionEvent _)
	{
		RegisterProgress(GetDidWin(), startup: false);
	}

	public int GetDidWin()
	{
		Ward wardForPolitician = Game.ctx.simman.politics.GetWardForPolitician(state.target);
		Election currElection = wardForPolitician.currElection;
		if ((currElection != null && currElection.IsUnresolved) || !(wardForPolitician.currentPolitician == state.target))
		{
			return 0;
		}
		return 1;
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		Ward wardForPolitician = Game.ctx.simman.politics.GetWardForPolitician(state.target);
		string[] array = new string[4]
		{
			"politician",
			state.target.FindEntity().data.person.FullName,
			"wardName",
			wardForPolitician.WardName
		};
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

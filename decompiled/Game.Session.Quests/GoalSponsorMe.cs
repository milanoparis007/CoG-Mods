using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Quests;

public sealed class GoalSponsorMe : BaseGoal
{
	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-sponsor";

	public override void OnAdded()
	{
		RegisterProgress(GetHasSponsoredMe(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.ElectionInteraction, OnLawChanged);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.ElectionInteraction, OnLawChanged);
	}

	public void OnLawChanged(SessionEvent _)
	{
		RegisterProgress(GetHasSponsoredMe(), startup: false);
	}

	public int GetHasSponsoredMe()
	{
		if (!((Game.ctx.simman.politics.GetWardForPolitician(state.target).currElection?.candidateStats[state.target].sponsor ?? PlayerID.INVALID) == PlayerID.HumanPlayer))
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

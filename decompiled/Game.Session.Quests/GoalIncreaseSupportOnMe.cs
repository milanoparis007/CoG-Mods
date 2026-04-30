using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Quests;

public sealed class GoalIncreaseSupportOnMe : BaseGoal
{
	public Fixnum startingPercent;

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-increase-support";

	public override void OnAdded()
	{
		Ward wardForPolitician = Game.ctx.simman.politics.GetWardForPolitician(state.target);
		startingPercent = wardForPolitician.currElection?.GetCurrentCandidatePercent(state.target) ?? new Fixnum(0);
		if (startingPercent + goal > 100)
		{
			goal = 100 - (int)startingPercent;
		}
		RegisterProgress(GetAmountImproved(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnLawChanged);
		Game.ctx.events.AddListener(SessionEventType.ElectionInteraction, OnLawChanged);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnLawChanged);
		Game.ctx.events.RemoveListener(SessionEventType.ElectionInteraction, OnLawChanged);
	}

	public void OnLawChanged(SessionEvent _)
	{
		RegisterProgress(GetAmountImproved(), startup: false);
	}

	public int GetAmountImproved()
	{
		return (int)(Game.ctx.simman.politics.GetWardForPolitician(state.target).currElection?.GetCurrentCandidatePercentInDisplay(state.target) ?? new Fixnum(0)) - (int)startingPercent;
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string[] array = new string[4]
		{
			"politician",
			state.target.FindEntity().data.person.FullName,
			"percent",
			Loc.Percentage(MathUtil.ClampMax(startingPercent / 100 + new Fixnum((float)goal / 100f), 100))
		};
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

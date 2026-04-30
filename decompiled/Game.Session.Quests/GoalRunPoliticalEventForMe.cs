using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Quests;

public sealed class GoalRunPoliticalEventForMe : BaseGoal
{
	public Label eventId;

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey
	{
		get
		{
			if (!eventId.IsSet)
			{
				return "goal-run-pol-event-any";
			}
			return "goal-run-pol-event";
		}
	}

	public override void OnAdded()
	{
		RegisterProgress(GetHasRunEvent(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.ElectionInteraction, OnLawChanged);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.ElectionInteraction, OnLawChanged);
	}

	public void OnLawChanged(SessionEvent _)
	{
		RegisterProgress(GetHasRunEvent(), startup: false);
	}

	public int GetHasRunEvent()
	{
		Ward wardForPolitician = Game.ctx.simman.politics.GetWardForPolitician(state.target);
		int num = 0;
		if (eventId.IsSet)
		{
			return wardForPolitician.currElection?.electionLog.Where((Election.ElectionEvent x) => IsRightEventAndCandidate(x)).Count() ?? 0;
		}
		return wardForPolitician.currElection?.electionLog.Where((Election.ElectionEvent x) => x.candidate == state.target).Count() ?? 0;
		bool IsRightEventAndCandidate(Election.ElectionEvent x)
		{
			if (x.candidate == state.target)
			{
				return eventId == x.id;
			}
			return false;
		}
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string[] array = new string[4]
		{
			"politician",
			state.target.FindEntity().data.person.FullName,
			"eventName",
			eventId.IsSet ? Loc.Get(Game.serv.globals.settings.politics.FindPlayerCandidateAction(eventId).locname) : ""
		};
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

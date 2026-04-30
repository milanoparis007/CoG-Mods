namespace Game.Session.Quests;

public class GoalHaveCrew : BaseGoal
{
	public override string LocDescKey => "goal-have-crew.desc";

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override void OnAdded()
	{
		Game.ctx.events.AddListener(SessionEventType.CrewMemberAdded, OnCrewChanged);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberKilled, OnCrewChanged);
		RegisterProgress(GetHumanCrewSize(), startup: true);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberAdded, OnCrewChanged);
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberKilled, OnCrewChanged);
	}

	private void OnCrewChanged(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RegisterProgress(GetHumanCrewSize(), startup: false);
		}
	}

	private int GetHumanCrewSize()
	{
		return Game.ctx.players.Human.crew.LivingCrewCount;
	}
}

namespace Game.Session.Quests;

public class GoalHaveBuildings : BaseGoal
{
	public override string LocDescKey => "goal-have-buildings.desc";

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override void OnAdded()
	{
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnBuildingChanged);
		RegisterProgress(GetBuildingCount(), startup: true);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnBuildingChanged);
	}

	private void OnBuildingChanged(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RegisterProgress(GetBuildingCount(), startup: false);
		}
	}

	private int GetBuildingCount()
	{
		return Game.ctx.players.Human.territory.CountControlledBuildings();
	}
}

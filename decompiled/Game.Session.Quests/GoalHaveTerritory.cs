namespace Game.Session.Quests;

public class GoalHaveTerritory : BaseGoal
{
	public override string LocDescKey => "goal-have-territory.desc";

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override void OnAdded()
	{
		Game.ctx.events.AddListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
		RegisterProgress(GetHumanPlayerTerritorySize(), startup: true);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
	}

	private void OnTerritoryChanged(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RegisterProgress(GetHumanPlayerTerritorySize(), startup: false);
		}
	}

	private int GetHumanPlayerTerritorySize()
	{
		return Game.ctx.players.Human.territory.OwnedNodeCount;
	}
}

using Game.Core;

namespace Game.Session.Quests;

public class GoalHaveCash : BaseGoal
{
	public override string LocDescKey => "goal-have-cash.desc";

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override void OnAdded()
	{
		Game.ctx.events.AddListener(SessionEventType.PlayerFinancesChanged, OnPlayerFinancesChanged);
		RegisterProgress((int)GetHumanPlayerTotalCash(), startup: true);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerFinancesChanged, OnPlayerFinancesChanged);
	}

	private void OnPlayerFinancesChanged(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RegisterProgress((int)GetHumanPlayerTotalCash(), startup: false);
		}
	}

	private Money GetHumanPlayerTotalCash()
	{
		return Game.ctx.players.Human.finances.GetMoneyThisTurn().endMoney;
	}
}

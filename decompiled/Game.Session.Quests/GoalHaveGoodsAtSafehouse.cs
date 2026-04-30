using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Quests;

public class GoalHaveGoodsAtSafehouse : BaseGoal
{
	public Label resource;

	public override string LocDescKey => "goal-have-goods-at-safehouse.desc";

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string GetItemName()
	{
		return Resource.Find(resource).GetIconAndName();
	}

	public override void OnAdded()
	{
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnHumanEvent);
		Game.ctx.events.AddListener(SessionEventType.UIHUDDialogClosed, OnHumanEvent);
		RegisterProgress((int)GetResourceAtSafehouse(), startup: true);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnHumanEvent);
		Game.ctx.events.AddListener(SessionEventType.UIHUDDialogClosed, OnHumanEvent);
	}

	private void OnHumanEvent(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RegisterProgress((int)GetResourceAtSafehouse(), startup: false);
		}
	}

	private Fixnum GetResourceAtSafehouse()
	{
		return ModulesUtil.GetInventory(Game.ctx.players.Human.territory.Safehouse).data.Get(resource).qty;
	}
}

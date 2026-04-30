using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Quests;

public class GoalDeliverGoods : DeliveryGoal
{
	public Label resource;

	public override Type GoalType => Type.DeliveryManual;

	public override string LocDescKey => "goal-deliver-goods.desc";

	public override string LocBriefKey => "goal-deliver-goods.brief";

	public override string LocRefundKey => "goal-deliver-goods.refund";

	public string LocRefundExplainKey => "goal-deliver-goods.refexplain";

	public override void OnAdded()
	{
		base.OnAdded();
		Game.ctx.events.AddListener(SessionEventType.ConvoQuestDeliveryStep, OnConvoDelivery);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.ConvoQuestDeliveryStep, OnConvoDelivery);
		base.OnRemoved();
	}

	private void OnConvoDelivery(SessionEvent sev)
	{
		if (sev.ctx is Summary summary && !(summary.delivered.raq.id != resource) && !(summary.visit.npc.Id != state.target))
		{
			int num = (int)summary.delivered.raq.qty;
			if (num > 0)
			{
				RegisterProgress(state.current + num, startup: false);
			}
		}
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string val = new ResourceAndQty(resource, goal).MakeQuantityXLocString();
		return base.MakeStringReplacements().AddReplacements("resAndQty", val);
	}

	protected override string DescribeRefund(Fixnum refundRate)
	{
		return new ResourceAndQty(resource, state.current * refundRate).MakeQuantityLocString();
	}

	protected override bool RefundImplementation(Fixnum refundRate)
	{
		ResourceAndQty resAndQty = new ResourceAndQty(resource, state.current * refundRate);
		if (resAndQty.qty.IsNotZero)
		{
			ModulesUtil.GetInventory(Game.ctx.players.Human.territory.Safehouse.FindEntity()).data.Increment(resAndQty);
			string message = Loc.Get(LocRefundExplainKey, "resAndQty", DescribeRefund(refundRate));
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.QUEST_UPDATE, TickerTitle.QUEST_UPDATE, message);
		}
		return true;
	}
}

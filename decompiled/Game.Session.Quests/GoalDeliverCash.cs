using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Quests;

public class GoalDeliverCash : DeliveryGoal
{
	public override Type GoalType => Type.DeliveryManual;

	public override string LocDescKey => "goal-deliver-cash.desc";

	public override string LocBriefKey => "goal-deliver-cash.brief";

	public override string LocRefundKey => "goal-deliver-cash.refund";

	public string LocRefundExplainKey => "goal-deliver-cash.refexplain";

	public override void OnAdded()
	{
		base.OnAdded();
		Game.ctx.events.AddListener(SessionEventType.ConvoQuestDeliveryStep, OnInventory);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.ConvoQuestDeliveryStep, OnInventory);
		base.OnRemoved();
	}

	private void OnInventory(SessionEvent sev)
	{
		if (sev.ctx is Summary summary && !(summary.visit.npc.Id != state.target))
		{
			int num = (int)summary.delivered.money.cash;
			if (num > 0)
			{
				RegisterProgress(state.current + num, startup: false);
			}
		}
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string text = Loc.Percentage(ProduceGoalCompletionFraction());
		string text2 = Loc.Price(new Price(state.current));
		string text3 = Loc.Price(new Price(goal));
		string text4 = Loc.Price(new Price(MathUtil.ClampMin(goal - state.current, 0)));
		string[] array = new string[10] { "value", text2, "goal", text3, "remains", text4, "percent", text, "resAndQty", text3 };
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}

	protected override string DescribeRefund(Fixnum refundRate)
	{
		return Loc.Price(new Price(state.current * refundRate));
	}

	protected override bool RefundImplementation(Fixnum refundRate)
	{
		PlayerInfo human = Game.ctx.players.Human;
		Price delta = new Price(state.current * refundRate);
		if (delta.cash.IsNotZero)
		{
			human.finances.DoChangeMoneyOnSafehouse(delta, MoneyReason.QuestReward);
			string message = Loc.Get(LocRefundExplainKey, "resAndQty", DescribeRefund(refundRate));
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.QUEST_UPDATE, TickerTitle.QUEST_UPDATE, message);
		}
		return true;
	}
}

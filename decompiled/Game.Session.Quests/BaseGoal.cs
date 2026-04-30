using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Quests;

public abstract class BaseGoal
{
	public enum Type
	{
		HaveSomething,
		DeliveryManual,
		DeliveryStanding
	}

	public GoalComparison test;

	public int goal;

	public GoalState state;

	public int Delta => goal - state.current;

	public abstract string LocDescKey { get; }

	public abstract Type GoalType { get; }

	public abstract bool IsRefundable { get; }

	public virtual void OnInitialize(string guuid, EntityID target)
	{
		state = new GoalState(guuid, target);
	}

	public abstract void OnAdded();

	public abstract void OnRemoved();

	protected void RegisterProgress(int current, bool startup)
	{
		state.current = current;
		switch (test)
		{
		case GoalComparison.AtLeast:
			state.completed = current >= goal;
			break;
		case GoalComparison.AtMost:
			state.completed = current <= goal;
			break;
		case GoalComparison.LessThan:
			state.completed = current < goal;
			break;
		case GoalComparison.MoreThan:
			state.completed = current > goal;
			break;
		default:
			Logger.Error("Unknown test type: " + test);
			state.completed = true;
			break;
		}
		if (!startup && Game.ctx.events != null)
		{
			SessionEvent ev = new SessionEvent(SessionEventType.GoalProgress, EntityID.INVALID, PlayerID.HumanPlayer, state.guuid);
			Game.ctx.events.SendImmediate(ev);
		}
	}

	public float ProduceGoalCompletionFraction()
	{
		if (goal != 0)
		{
			return MathUtil.Clamp((float)state.current / (float)goal, 0f, 1f);
		}
		return 1f;
	}

	public virtual LocReplacementContext MakeStringReplacements()
	{
		string itemName = GetItemName();
		string text = Loc.Percentage(ProduceGoalCompletionFraction());
		string text2 = Loc.FormatNumber(state.current);
		string text3 = Loc.FormatNumber(goal);
		string text4 = Loc.FormatNumber(MathUtil.ClampMin(goal - state.current, 0));
		string[] array = new string[10] { "value", text2, "goal", text3, "remains", text4, "percent", text, "item", itemName };
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}

	public virtual string GetItemName()
	{
		return "";
	}

	public virtual string GetLocDesc()
	{
		return Loc.Get(LocDescKey, MakeStringReplacements());
	}

	public virtual bool OnRefund(Fixnum refundRate)
	{
		return false;
	}

	public void DoRefund(Fixnum refundRate)
	{
		if (IsRefundable && !OnRefund(refundRate))
		{
			Logger.Warning("Goal declared to be refundable but fails to refund:", GetType().Name);
		}
	}
}

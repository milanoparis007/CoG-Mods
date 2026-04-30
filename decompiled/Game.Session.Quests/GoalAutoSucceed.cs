using Game.Services;

namespace Game.Session.Quests;

public sealed class GoalAutoSucceed : BaseGoal
{
	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-auto-succeed";

	public override void OnAdded()
	{
		RegisterProgress(GetAutoSucceed(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.LawChanged, OnLawChanged);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.LawChanged, OnLawChanged);
	}

	public void OnLawChanged(SessionEvent _)
	{
		RegisterProgress(GetAutoSucceed(), startup: false);
	}

	public int GetAutoSucceed()
	{
		return goal;
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string[] array = new string[0];
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

using Game.Services;

namespace Game.Session.Quests;

public sealed class GoalLookAtLawBook : BaseGoal
{
	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-look-at-lawbook";

	public override void OnAdded()
	{
		Game.ctx.simman.politics.GetWardForPolitician(state.target);
		RegisterProgress(0, startup: true);
		Game.ctx.events.AddListener(SessionEventType.LawbookLook, OnLawbookLook);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.LawbookLook, OnLawbookLook);
	}

	public void OnLawbookLook(SessionEvent _)
	{
		RegisterProgress(1, startup: false);
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string[] array = new string[0];
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

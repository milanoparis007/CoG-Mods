using Game.Core;
using Game.Services;

namespace Game.Session.Quests;

public sealed class GoalHaveInfluence : BaseGoal
{
	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-have-influence";

	public override void OnAdded()
	{
		Game.ctx.simman.politics.GetWardForPolitician(state.target);
		RegisterProgress(GetInfluenceAmt(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.InfluenceChange, OnInfluenceChange);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.InfluenceChange, OnInfluenceChange);
	}

	public void OnInfluenceChange(SessionEvent _)
	{
		RegisterProgress(GetInfluenceAmt(), startup: false);
	}

	public int GetInfluenceAmt()
	{
		return Game.ctx.simman.politics.GetInfluenceWallet(PlayerID.HumanPlayer).current;
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string[] array = new string[2]
		{
			"goal",
			goal.ToString()
		};
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

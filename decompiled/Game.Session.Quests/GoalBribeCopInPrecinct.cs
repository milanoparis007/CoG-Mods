using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.AI;
using Game.Session.Sim;

namespace Game.Session.Quests;

public sealed class GoalBribeCopInPrecinct : BaseGoal
{
	public PrecinctID precinct;

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-bribe-cop-in-precinct";

	public override void OnAdded()
	{
		precinct = BuildingUtil.FindBuildingForBizOwner(state.target.FindEntity()).components.board.GetNode().precinctId;
		RegisterProgress(GetCopIsBribed(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.ConversationEnded, OnConversationEnded);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.ConversationEnded, OnConversationEnded);
	}

	public void OnConversationEnded(SessionEvent _)
	{
		precinct = BuildingUtil.FindBuildingForBizOwner(state.target.FindEntity()).components.board.GetNode().precinctId;
		RegisterProgress(GetCopIsBribed(), startup: false);
	}

	public int GetCopIsBribed()
	{
		if (precinct.FindPrecinct().ai.precinct.HasDonationFrom(PlayerID.HumanPlayer) != DonationState.PaidOff)
		{
			return 0;
		}
		return 1;
	}
}

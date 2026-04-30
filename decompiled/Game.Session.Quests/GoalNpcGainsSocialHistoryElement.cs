using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Quests;

public sealed class GoalNpcGainsSocialHistoryElement : BaseGoal
{
	public List<Label> labelled;

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-has-social-history-element.desc";

	public override void OnAdded()
	{
		Game.ctx.events.AddListener(SessionEventType.SocialActionPerformed, OnSocialAction);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.SocialActionPerformed, OnSocialAction);
	}

	private void OnSocialAction(SessionEvent sev)
	{
		if (sev.ctx is Label item && !(state.target != sev.eid) && labelled.Contains(item))
		{
			RegisterProgress(state.current + 1, startup: false);
		}
	}
}

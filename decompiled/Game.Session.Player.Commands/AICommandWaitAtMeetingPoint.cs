using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

internal class AICommandWaitAtMeetingPoint : MultiActionCommand
{
	public NodeID nodeId;

	public override string Message => Loc.Get("ui.command.internal.wait");

	public AICommandWaitAtMeetingPoint()
	{
	}

	public AICommandWaitAtMeetingPoint(PlayerID pid, EntityID eid, NodeID nodeId)
		: base(pid, CommandType.WaitAtMeetingPoint, eid)
	{
		this.nodeId = nodeId;
	}

	protected override StartStatus CanStart()
	{
		return StartStatus.OK;
	}

	protected override void OnStarted()
	{
		base.OnStarted();
		peepId.FindEntity().components.agent.GetNode();
		GetPlayer().ai.social.InformOnCommandWaiting(pid, nodeId, _: true);
	}

	protected override bool CanConsumePoints()
	{
		return true;
	}

	protected override bool ContinuesToNextTurn()
	{
		return GetPlayer().ai.social.ShouldContinueWaiting();
	}

	protected override void DoConsumePoints()
	{
		peepId.FindEntity().components.agent.ConsumeAllPoints();
	}

	protected override void OnFinished(bool success)
	{
		base.OnFinished(success);
		peepId.FindEntity().components.agent.GetNode();
		GetPlayer().ai.social.InformOnCommandWaiting(pid, nodeId, _: false);
	}
}

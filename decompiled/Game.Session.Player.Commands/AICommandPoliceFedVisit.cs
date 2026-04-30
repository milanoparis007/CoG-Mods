using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public class AICommandPoliceFedVisit : MultiActionCommand
{
	public NodeID targetNodeId;

	public int days;

	public SimTime start;

	public SimTime end;

	public override string Message => Loc.Get("ui.command.internal.raid");

	public AICommandPoliceFedVisit()
	{
	}

	public AICommandPoliceFedVisit(PlayerID pid, EntityID eid, NodeID targetNodeId, int days)
		: base(pid, CommandType.PoliceOrFedVisit, eid)
	{
		this.targetNodeId = targetNodeId;
		this.days = days;
		start = (end = SimTime.MIN_DATE);
	}

	protected override StartStatus CanStart()
	{
		return StartStatus.OK;
	}

	protected override void OnStarted()
	{
		base.OnStarted();
		start = Game.ctx.clock.Now;
		end = start.IncrementDays(days);
		peepId.FindEntity().components.agent.GetNode();
		PlayerInfo player = GetPlayer();
		if (player.IsJustCop)
		{
			player.ai.precinct.MarkRaidStart(targetNodeId, peepId);
		}
		if (player.IsJustFed)
		{
			player.ai.feds.MarkInvestigationStart(targetNodeId, peepId);
		}
	}

	protected override bool CanConsumePoints()
	{
		return true;
	}

	protected override bool ContinuesToNextTurn()
	{
		return Game.ctx.clock.Now < end;
	}

	protected override void DoConsumePoints()
	{
		peepId.FindEntity().components.agent.ConsumeAllPoints();
	}

	protected override void OnFinished(bool success)
	{
		base.OnFinished(success);
		peepId.FindEntity().components.agent.GetNode();
		PlayerInfo player = GetPlayer();
		if (player.IsJustCop)
		{
			player.ai.precinct.MarkRaidEnd(targetNodeId, peepId);
		}
		if (player.IsJustFed)
		{
			player.ai.feds.MarkInvestigationEnd();
		}
	}
}

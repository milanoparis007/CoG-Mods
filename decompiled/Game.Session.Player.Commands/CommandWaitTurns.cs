using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class CommandWaitTurns : MultiActionCommand
{
	public int turnsLeft;

	public override string Message => Loc.Get("ui.command.type.wait");

	public CommandWaitTurns()
	{
	}

	public CommandWaitTurns(PlayerID pid, EntityID peepId, int turns)
		: base(pid, CommandType.WaitTurns, peepId)
	{
		turnsLeft = turns;
	}

	protected override bool CanConsumePoints()
	{
		return peepId.FindEntity().components.agent.HasActionsRemaining;
	}

	protected override void DoConsumePoints()
	{
		peepId.FindEntity().components.agent.ConsumeAllPoints();
	}

	protected override StartStatus CanStart()
	{
		return StartStatus.OK;
	}

	protected override void PerformTurnActions()
	{
		turnsLeft--;
	}

	protected override bool ContinuesToNextTurn()
	{
		return turnsLeft > 0;
	}
}

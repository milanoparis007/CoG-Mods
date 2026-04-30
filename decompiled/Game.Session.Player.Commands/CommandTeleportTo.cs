using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class CommandTeleportTo : MultiActionCommand
{
	public NodeID goalID;

	public override string Message => Loc.Get("ui.command.teleportto.action");

	public CommandTeleportTo()
	{
	}

	public CommandTeleportTo(PlayerID pid, EntityID peepId, Node goal)
		: base(pid, CommandType.TeleportTo, peepId)
	{
		if (goal == null || goal.id.IsNotValid)
		{
			Logger.Error("Invalid node passed to CommandGoto.");
		}
		else
		{
			goalID = goal.id;
		}
	}

	protected override bool CanConsumePoints()
	{
		return true;
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
		Game.ctx.transit.SetAgentAtNode(goalID, peepId.FindEntity());
		Game.ctx.transit.TeleportCarToNode(peepId.FindEntity().components.agent.FindCrewAssignment().GetVehicle(), goalID);
	}

	protected override bool ContinuesToNextTurn()
	{
		return false;
	}
}

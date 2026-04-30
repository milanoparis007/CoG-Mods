using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class CommandBoardCallback : MultiActionCommand
{
	public EntityID targetPeep;

	public string message;

	public override string Message => Loc.Get("ui.command.teleportto.action");

	public CommandBoardCallback()
	{
	}

	public CommandBoardCallback(CommandType type, PlayerID pid, EntityID peep, string message)
		: base(pid, type, peep)
	{
		targetPeep = peep;
		this.message = message;
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
		base.PerformTurnActions();
		switch (type)
		{
		case CommandType.RemoveFromBoard:
			GetPlayer().crew.RemoveCrewFromBoard(targetPeep, PlayerCrewData.OffBoardReason.Scheme, removeCar: true);
			break;
		case CommandType.ReturnToBoard:
			GetPlayer().crew.ReturnCrewToBoard(targetPeep);
			break;
		default:
			Logger.Warning($"Unknown command type for {this}: {type}");
			break;
		}
	}

	protected override bool ContinuesToNextTurn()
	{
		return false;
	}
}

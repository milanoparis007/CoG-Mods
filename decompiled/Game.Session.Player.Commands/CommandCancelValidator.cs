using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class CommandCancelValidator : HumanCommandValidator
{
	public override CommandType Type => CommandType.Cancel;

	public override int SortOrder => 98;

	public override CommandStatus Validate(PlayerID pid, CrewAssignment crew)
	{
		CommandStatus commandStatus = new CommandStatus(Type, Loc.Get("ui.command.cancel.icon"));
		if (!pid.FindPlayer().commands.PeepHasTask(crew.peepId))
		{
			return commandStatus.Set(CommandEnabledStatus.Hidden, null);
		}
		return commandStatus.Set(CommandEnabledStatus.Enabled, Loc.Get("ui.command.cancel.mo"));
	}

	protected override void PostCommandAsHuman(CrewAssignment crew)
	{
		Entity peep = crew.GetPeep();
		peep.components.agent.ConsumeAllPointsAndStop();
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerCommandFinished, peep.Id, PlayerID.HumanPlayer));
	}
}

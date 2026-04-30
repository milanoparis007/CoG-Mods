using Game.Core;
using Game.Services;
using Game.Session.Input;

namespace Game.Session.Player.Commands;

public sealed class CommandGoToValidator : HumanCommandValidator
{
	public override CommandType Type => CommandType.GoTo;

	public override int SortOrder => 90;

	public override CommandStatus Validate(PlayerID pid, CrewAssignment crew)
	{
		CommandStatus commandStatus = new CommandStatus(Type, Loc.Get("ui.command.goto.icon"));
		if (!crew.IsInVehicle)
		{
			return commandStatus.Set(CommandEnabledStatus.Hidden, null);
		}
		if (crew.GetPeep().components.agent.HasMovesRemaining)
		{
			return commandStatus.Set(CommandEnabledStatus.Enabled, Loc.Get("ui.command.goto.mo"));
		}
		return commandStatus.Set(CommandEnabledStatus.DisabledOther, Loc.Get("ui.command.goto.points"));
	}

	protected override void PostCommandAsHuman(CrewAssignment crew)
	{
		Game.ctx.selection.SetActive(null);
		Game.serv.input.Replace(new CarInputMode(crew.GetVehicle(), fromButton: true));
	}
}

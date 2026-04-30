namespace Game.Session.Player.Commands;

public sealed class CommandButtonState
{
	public readonly HumanCommandValidator handler;

	public readonly CommandStatus status;

	public readonly CrewAssignment crew;

	public CommandButtonState(CrewAssignment crew, HumanCommandValidator handler, CommandStatus status)
	{
		this.crew = crew;
		this.handler = handler;
		this.status = status;
	}

	public static int Compare(CommandButtonState a, CommandButtonState b)
	{
		return a.handler.SortOrder - b.handler.SortOrder;
	}
}

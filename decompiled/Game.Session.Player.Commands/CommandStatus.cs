using Game.Core;

namespace Game.Session.Player.Commands;

public struct CommandStatus
{
	public CommandType type;

	public CommandEnabledStatus status;

	public string icon;

	public string mouseover;

	public bool IsEnabled => status == CommandEnabledStatus.Enabled;

	public bool IsDisabledButShown
	{
		get
		{
			if (status != CommandEnabledStatus.Enabled)
			{
				return status != CommandEnabledStatus.Hidden;
			}
			return false;
		}
	}

	public bool IsDisabledOrHidden => status != CommandEnabledStatus.Enabled;

	public bool IsHidden => status == CommandEnabledStatus.Hidden;

	public bool IsNotHidden => status != CommandEnabledStatus.Hidden;

	public CommandStatus(CommandType type, string icon)
	{
		this = default(CommandStatus);
		this.type = type;
		this.icon = icon;
		status = CommandEnabledStatus.Hidden;
		mouseover = null;
	}

	public CommandStatus Set(CommandEnabledStatus newstatus, string newmouseover)
	{
		return new CommandStatus
		{
			type = type,
			icon = icon,
			status = newstatus,
			mouseover = newmouseover
		};
	}
}

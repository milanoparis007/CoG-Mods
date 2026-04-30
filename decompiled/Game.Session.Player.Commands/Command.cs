using System;
using Game.Core;

namespace Game.Session.Player.Commands;

public abstract class Command
{
	public enum StartStatus
	{
		OK,
		SkipThisTurn,
		Failed
	}

	public CommandType type;

	public PlayerID pid;

	public EntityID peepId;

	public abstract string Message { get; }

	public Command()
	{
	}

	protected Command(PlayerID pid, CommandType type, EntityID peepId)
	{
		this.pid = pid;
		this.type = type;
		this.peepId = peepId;
	}

	protected PlayerInfo GetPlayer()
	{
		return Game.ctx.players.WithID(pid);
	}

	protected abstract StartStatus CanStart();

	public abstract bool CanActivateAfterDequeue();

	protected abstract void SubclassStart();

	protected abstract void SubclassFinish(bool success);

	public abstract bool ExecuteSingleTurn();

	public StartStatus Start()
	{
		StartStatus startStatus = CanStart();
		if (startStatus == StartStatus.OK)
		{
			EnqueueEvent(SessionEventType.PlayerCommandStarted, this);
			try
			{
				SubclassStart();
			}
			catch (Exception ex)
			{
				Game.serv.stats.LogException(ex);
			}
		}
		return startStatus;
	}

	public void Finish(bool success)
	{
		try
		{
			SubclassFinish(success);
		}
		catch (Exception ex)
		{
			Game.serv.stats.LogException(ex);
		}
		EnqueueEvent(SessionEventType.PlayerCommandFinished, this);
	}

	protected static void EnqueueEvent(SessionEventType type, Command cmd)
	{
		Game.ctx.events.EnqueueOnce(new SessionEvent(type, cmd.peepId, cmd.pid, cmd));
	}
}

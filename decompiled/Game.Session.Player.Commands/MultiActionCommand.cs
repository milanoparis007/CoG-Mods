using Game.Core;

namespace Game.Session.Player.Commands;

public abstract class MultiActionCommand : Command
{
	public MultiActionCommand()
	{
	}

	protected MultiActionCommand(PlayerID pid, CommandType type, EntityID peepId)
		: base(pid, type, peepId)
	{
	}

	protected sealed override void SubclassStart()
	{
		OnStarted();
	}

	protected sealed override void SubclassFinish(bool success)
	{
		OnFinished(success);
	}

	protected virtual void OnStarted()
	{
	}

	protected virtual void OnTurnStarted()
	{
	}

	protected virtual void PerformTurnActions()
	{
	}

	protected virtual bool ContinuesToNextTurn()
	{
		return false;
	}

	protected virtual void OnFinished(bool success)
	{
	}

	protected abstract bool CanConsumePoints();

	protected abstract void DoConsumePoints();

	public override bool CanActivateAfterDequeue()
	{
		return CanConsumePoints();
	}

	public sealed override bool ExecuteSingleTurn()
	{
		OnTurnStarted();
		if (!CanConsumePoints())
		{
			return false;
		}
		DoConsumePoints();
		PerformTurnActions();
		Command.EnqueueEvent(SessionEventType.PlayerCommandExecutedOneTurn, this);
		return ContinuesToNextTurn();
	}
}

using System;
using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public abstract class NormalCommand : Command
{
	public CrewCost leftToPay;

	public NormalCommand()
	{
	}

	protected NormalCommand(PlayerID pid, CommandType type, EntityID peepId)
		: base(pid, type, peepId)
	{
		leftToPay = CrewCost.ZERO;
	}

	protected sealed override void SubclassStart()
	{
		leftToPay = CalculateCost();
		OnStarted();
	}

	protected sealed override void SubclassFinish(bool success)
	{
		OnFinished(success);
	}

	public override bool CanActivateAfterDequeue()
	{
		return true;
	}

	protected override StartStatus CanStart()
	{
		return StartStatus.OK;
	}

	protected abstract CrewCost CalculateCost();

	protected virtual void OnStarted()
	{
	}

	protected virtual bool CanContinueToRun()
	{
		return true;
	}

	protected virtual void PerformCommandSuccess()
	{
	}

	protected virtual void OnFinished(bool success)
	{
	}

	protected virtual bool ContinuesToNextTurn()
	{
		return leftToPay.actions > 0;
	}

	public sealed override bool ExecuteSingleTurn()
	{
		if (!CanContinueToRun())
		{
			return false;
		}
		PayPoints();
		Command.EnqueueEvent(SessionEventType.PlayerCommandExecutedOneTurn, this);
		if (ContinuesToNextTurn())
		{
			return true;
		}
		PerformCommandSuccess();
		return false;
	}

	protected virtual void PayPoints()
	{
		AgentComponent agent = peepId.FindEntity().components.agent;
		CrewCost cost = CrewCost.OnlyActions(Math.Min(leftToPay.actions, agent.ActionsRemaining));
		agent.DoPay(cost, "PayPoints");
		leftToPay = leftToPay.Add(0, -cost.actions);
		if (leftToPay.actions > 0)
		{
			agent.ConsumeAllPoints(actions: false);
		}
	}
}

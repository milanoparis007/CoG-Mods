using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class AutomationSequence
{
	public AutomationID id;

	public EntityID vehicle;

	public string name;

	public List<AutomationStep> steps = new List<AutomationStep>();

	public int nextstep = -1;

	public bool IsAutoActive => nextstep >= 0;

	public bool IsAutoNotActive => nextstep < 0;

	public AutomationStep GetNextStep()
	{
		if (nextstep < 0 || nextstep >= steps.Count)
		{
			return null;
		}
		return steps[nextstep];
	}

	public void SetStep(int next)
	{
		nextstep = next;
	}

	public void IncrementStep()
	{
		if (steps.Count > 0)
		{
			SetStep((nextstep + 1) % steps.Count);
		}
	}

	public void DecrementStep()
	{
		if (steps.Count > 0 && nextstep < steps.Count && nextstep > 0)
		{
			SetStep((nextstep - 1) % steps.Count);
		}
	}

	public void Stop()
	{
		nextstep = -1;
	}

	public void Reset()
	{
		nextstep = 0;
	}

	public bool IsStepEnabled(int index)
	{
		return steps.GetOrDefaultFast(index)?.enabled ?? false;
	}

	public void SetStepEnabled(int index, bool enabled)
	{
		AutomationStep orDefaultFast = steps.GetOrDefaultFast(index);
		if (orDefaultFast != null)
		{
			orDefaultFast.enabled = enabled;
		}
	}

	internal bool ContainsAction(AutoAction action)
	{
		return steps.Any((AutomationStep step) => step.action == action);
	}

	internal bool ContainsTarget(EntityID eid)
	{
		return steps.Any((AutomationStep step) => step.target == eid);
	}

	internal int FindIndexOfTarget(EntityID eid)
	{
		return steps.FindIndex((AutomationStep step) => step.target == eid);
	}

	public CrewAssignment FindCrew()
	{
		if (!vehicle.IsValid)
		{
			return CrewAssignment.EMPTY;
		}
		return Game.ctx.players.Human.crew.GetCrewForTarget(vehicle);
	}

	internal void SendAutomationChangedEvent(PlayerID pid)
	{
		if (pid.IsHumanPlayer)
		{
			SessionEvent ev = new SessionEvent(SessionEventType.CrewAutomationChanged, EntityID.INVALID, pid, this);
			Game.ctx.events.EnqueueOnce(ev);
		}
	}
}

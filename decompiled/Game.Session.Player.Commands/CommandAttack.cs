using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.Session.Player.Commands;

public sealed class CommandAttack : MultiActionCommand
{
	public CrewAssignment target;

	public Label weapon;

	public override string Message => Loc.Get("ui.command.type.attack");

	public CommandAttack()
	{
	}

	public CommandAttack(PlayerID pid, EntityID peepId, EntityID target)
		: base(pid, CommandType.Attack, peepId)
	{
		this.target = target.FindEntity().components.agent.FindCrewAssignment();
	}

	public CommandAttack(PlayerID pid, EntityID peepId)
		: base(pid, CommandType.Attack, peepId)
	{
	}

	protected override StartStatus CanStart()
	{
		if (!ValidateState())
		{
			return StartStatus.Failed;
		}
		return StartStatus.OK;
	}

	private bool ValidateState()
	{
		if (target.IsValid)
		{
			NodeID nid = target.GetPeep().data.agent.nid;
			NodeID nid2 = peepId.FindEntity().data.agent.nid;
			return nid.Equals(nid2);
		}
		List<EntityID> attackTargetsAtSameLocation = CombatManager.GetAttackTargetsAtSameLocation(peepId.FindEntity());
		if (attackTargetsAtSameLocation == null)
		{
			return false;
		}
		target = attackTargetsAtSameLocation[0].FindEntity().components.agent.FindCrewAssignment();
		return target.IsValid;
	}

	protected override void PerformTurnActions()
	{
		CrewAssignment crewForPeep = pid.FindPlayer().crew.GetCrewForPeep(peepId);
		Game.ctx.simman.combat.PerformAICombat(crewForPeep, target);
	}

	protected override bool CanConsumePoints()
	{
		return Game.ctx.simman.combat.CanPayAttackCost(peepId.FindEntity());
	}

	protected override void DoConsumePoints()
	{
		Game.ctx.simman.combat.DoPayAttackCost(peepId.FindEntity());
	}
}

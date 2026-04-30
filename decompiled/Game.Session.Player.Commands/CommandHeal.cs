using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class CommandHeal : MultiActionCommand
{
	public override string Message => Loc.Get("ui.command.heal.action");

	protected override StartStatus CanStart()
	{
		if (!peepId.FindEntity().components.agent.IsWounded)
		{
			return StartStatus.Failed;
		}
		return StartStatus.OK;
	}

	public CommandHeal()
	{
	}

	public CommandHeal(PlayerID pid, EntityID peepId)
		: base(pid, CommandType.Heal, peepId)
	{
	}

	protected override bool CanConsumePoints()
	{
		CrewCost healCost = Game.serv.globals.settings.people.social.costs.healCost;
		return peepId.FindEntity().components.agent.CanPay(healCost);
	}

	protected override void DoConsumePoints()
	{
		peepId.FindEntity().components.agent.ConsumeAllPoints();
	}

	protected override void PerformTurnActions()
	{
		CrewAssignment crewForPeep = pid.FindPlayer().crew.GetCrewForPeep(peepId);
		Game.ctx.simman.combat.PerformHealing(pid, crewForPeep);
		if (crewForPeep.GetVehicle() != null)
		{
			Game.ctx.vfx.PlayOneShotPFX(PFXType.AttackFX, crewForPeep.GetVehicle().data.mobile.worldpos, pid, 2f);
		}
		if (pid.IsHumanPlayer)
		{
			Game.ctx.sfx.PlayHealing();
		}
	}

	protected override bool ContinuesToNextTurn()
	{
		return CanStart() == StartStatus.OK;
	}
}

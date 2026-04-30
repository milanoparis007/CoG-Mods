using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckCanClearGamblerDebt : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		Fixnum cash = visit.pid.FindPlayer().gambling.FindGamblerState(visit.npc).cash.cash;
		return ModulesUtil.GetInventory(visit.vehicle).data.CanChangeMoney(new Price(cash));
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.can-pay-debt"));
	}
}

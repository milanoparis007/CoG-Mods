using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckHasCashInVehicle : AbstractVisitRequirement
{
	public Fixnum amount;

	public override bool DoesPass(VisitState visit)
	{
		return ModulesUtil.GetInventory(visit.vehicle).data.CanChangeMoney(new Price(-amount));
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.check-has-cash-in-vehicle", "amount", Loc.Money(amount), "value", Loc.Money(ModulesUtil.GetInventory(visit.vehicle).data.money.cash)));
	}
}

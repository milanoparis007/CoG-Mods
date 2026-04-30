using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckGamblingHouseInDebt : AbstractVisitRequirement
{
	public enum Delta
	{
		None,
		Positive,
		Negative
	}

	public Delta delta;

	public override bool DoesPass(VisitState visit)
	{
		GamblingModule gamblingModule = visit.building?.components.modules?.gambling;
		if (gamblingModule == null)
		{
			Logger.Warning("CheckGamblingHouseInDebt called on non-gambling house?");
			return false;
		}
		Fixnum fixnum = gamblingModule.FindMinimumOperationalValue(visit);
		Fixnum cash = ModulesUtil.GetInventory(visit.building).data.money.cash;
		return ((cash < fixnum) ? 2 : ((cash > fixnum) ? 1 : 0)) == (int)delta;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

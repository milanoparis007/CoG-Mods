using Game.Core;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckCanPayGamblingHouseDebt : AbstractVisitRequirement
{
	public bool expected;

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
		Money money = Game.ctx.players.Human.finances.GetMoney(visit.vehicle);
		Fixnum fixnum2 = fixnum - cash;
		if (fixnum2 < 0)
		{
			return false;
		}
		return money.cash >= fixnum2 == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

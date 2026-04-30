using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public sealed class CheckGamblingHouseHasDebtors : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		GamblingModule gamblingModule = visit.building?.components.modules.gambling;
		if (gamblingModule == null)
		{
			Logger.Warning("CheckGamblingHouseInDebt called on non-gambling house?");
			return false;
		}
		return gamblingModule.GetNumberOfActiveDebtors(visit.GetPlayer()) > 0;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

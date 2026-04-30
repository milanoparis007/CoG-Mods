using Game.Session.Entities;

namespace Game.Session.Data;

public class SpecialIsTradeLocked : AbstractVisitRequirement
{
	public enum Reason
	{
		Any,
		TiedHouse,
		Territory,
		ForceClosed
	}

	public bool expected;

	public Reason reason;

	public override bool DoesPass(VisitState visit)
	{
		BizComponent.TradeRestrictions tradeRestrictions = visit.biz.components.biz.FindTradeRestrictions(visit.pid);
		switch (reason)
		{
		case Reason.Any:
			return tradeRestrictions.IsLocked == expected;
		case Reason.TiedHouse:
			return tradeRestrictions.IsTiedHouseLocked == expected;
		case Reason.Territory:
			return tradeRestrictions.IsTerritoryLocked == expected;
		case Reason.ForceClosed:
			return tradeRestrictions.IsForcedClosed == expected;
		default:
			Logger.Warning("Unknown reason", reason);
			return false;
		}
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

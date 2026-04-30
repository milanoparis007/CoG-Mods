using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public static class SpecialBizPurchaseUtil
{
	public enum Type
	{
		CurrentOnCrew,
		HighWatermark
	}

	public static bool DoesPass(VisitState visit, PlayerTerritory.TakeoverData takeover, Type test)
	{
		PlayerFinances finances = visit.GetPlayer().finances;
		Money? money = null;
		switch (test)
		{
		case Type.CurrentOnCrew:
			money = finances.GetMoney(visit.crew.VehicleID);
			break;
		case Type.HighWatermark:
			money = finances.GetMoneyHighWatermark();
			break;
		}
		if (!money.HasValue)
		{
			return false;
		}
		if (takeover.buildingId.IsValid)
		{
			return money.Value.cash >= takeover.cost.cash;
		}
		return false;
	}

	public static ReqExplanation Explain(VisitState visit, PlayerTerritory.TakeoverData takeover, Type test)
	{
		string text = Loc.Price(takeover.cost);
		string message = ((test == Type.CurrentOnCrew) ? Loc.Get("ui.requirements.biz.purchase.onperson", "cash", text) : Loc.Get("ui.requirements.biz.purchase.highwater", "cash", text));
		return new ReqExplanation(DoesPass(visit, takeover, test), message);
	}
}

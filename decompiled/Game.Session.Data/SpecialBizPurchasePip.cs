using Game.Session.Player;

namespace Game.Session.Data;

public class SpecialBizPurchasePip : AbstractVisitRequirement
{
	public SpecialBizPurchaseUtil.Type test;

	public override bool DoesPass(VisitState visit)
	{
		PlayerTerritory.TakeoverData takeover = FindTakeoverData(visit);
		return SpecialBizPurchaseUtil.DoesPass(visit, takeover, test);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		PlayerTerritory.TakeoverData takeover = FindTakeoverData(visit);
		return SpecialBizPurchaseUtil.Explain(visit, takeover, test);
	}

	private PlayerTerritory.TakeoverData FindTakeoverData(VisitState visit)
	{
		return GetPlayer(visit).territory.FindTakeoverData(visit);
	}
}

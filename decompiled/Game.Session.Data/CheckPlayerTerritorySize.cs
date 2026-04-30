using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerTerritorySize : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).territory.OwnedNodeCount;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.territory.size", "value", value));
	}
}

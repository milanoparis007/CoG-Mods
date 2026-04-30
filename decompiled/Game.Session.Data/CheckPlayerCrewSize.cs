using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerCrewSize : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).crew.LivingCrewCount;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.crew.size", "value", value));
	}
}

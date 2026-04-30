using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerMetGangs : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).meetings.CountGangsAlreadyMet();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.metgangs.count", "value", value));
	}
}

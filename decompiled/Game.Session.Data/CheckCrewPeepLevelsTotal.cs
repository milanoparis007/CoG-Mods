using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckCrewPeepLevelsTotal : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return (visit.peep?.data.agent?.xp?.GetSumOfLevels()).GetValueOrDefault();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = Loc.Get("ui.requirements.checkcrew.levelups", "num", value);
		return new ReqExplanation(DoesPass(visit), message);
	}
}

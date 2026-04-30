using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckNumSkillsOffered : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return visit.GetPlayer().skills.FindAllDistinctSkillsToLearn(visit).Count;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.skill-num", "value", value));
	}
}

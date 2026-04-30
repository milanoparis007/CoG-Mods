using Game.Services;

namespace Game.Session.Data;

public class CheckCanLearnNewSkills : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).skills.HasOneSkillsToLearn(visit);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.skills.new"));
	}
}

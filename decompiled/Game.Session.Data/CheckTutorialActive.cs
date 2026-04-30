using Game.Services;

namespace Game.Session.Data;

public class CheckTutorialActive : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return Game.ctx.tutorial.AreConvoReqsSuppressed == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string key = (expected ? "ui.requirements.tut-yes" : "ui.requirements.tut-no");
		return new ReqExplanation(DoesPass(visit), Loc.Get(key));
	}
}

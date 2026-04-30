using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckUnderConcurrentSchemeMax : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		Fixnum fixnum = Game.serv.globals.settings.schemes.concurrentSchemesAllowed.Evaluate(default(ModQuery));
		return visit.GetPlayer().schemes.GetNumOngoingSchemes() < fixnum == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		Fixnum fixnum = Game.serv.globals.settings.schemes.concurrentSchemesAllowed.Evaluate(default(ModQuery));
		return new ReqExplanation(DoesPass(visit), Loc.GetPluralized("ui.requirements.scheme.concurrent-max", fixnum, "max", fixnum));
	}
}

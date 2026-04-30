using Game.Services;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckBlockingCop : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		var (flag, flag2) = CopUtil.HasBlockingCop(visit.pid, visit.GetCrewNodeID());
		return (flag || flag2) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get(expected ? "ui.requirements.copatnode.expected" : "ui.requirements.copatnode.unexpected"));
	}
}

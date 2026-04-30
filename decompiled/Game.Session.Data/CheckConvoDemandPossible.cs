using Game.Services;

namespace Game.Session.Data;

public class CheckConvoDemandPossible : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		bool flag = Game.ctx.simman.demands.CanPlaceDemand(visit);
		return expected == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.demands.new.unallowed") : Loc.Get("ui.requirements.demands.new.allowed"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}

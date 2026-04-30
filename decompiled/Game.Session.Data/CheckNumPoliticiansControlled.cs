using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckNumPoliticiansControlled : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return Game.ctx.simman.politics.GetNumPoliticiansControlled();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.politicians-controlled", "value", value));
	}
}

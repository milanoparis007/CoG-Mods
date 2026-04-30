using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckCurrentTurn : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState _)
	{
		return Game.ctx.clock.CurrentTurn;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.gameturn", "value", value));
	}
}

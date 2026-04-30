using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckPlayerCounter : VisitWithValueRequirement
{
	public Label id;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return visit.GetPlayer().skills.GetCounterOrDefault(id);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

using Game.Core;

namespace Game.Session.Data;

public sealed class CheckThroneType : AbstractVisitRequirement
{
	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		return visit.GetPlayer().throne.GetThroneStyle() == id;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

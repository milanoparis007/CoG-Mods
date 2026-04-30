using Game.Core;

namespace Game.Session.Data;

public class CheckSchemeIsActiveById : AbstractVisitRequirement
{
	public Label id;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		bool flag = visit.GetPlayer().schemes.GetOngoingSchemeForID(id) != null;
		return expected == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

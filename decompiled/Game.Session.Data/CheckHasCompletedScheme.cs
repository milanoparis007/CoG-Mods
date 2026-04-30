using Game.Core;

namespace Game.Session.Data;

public class CheckHasCompletedScheme : AbstractVisitRequirement
{
	public bool expected;

	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		bool flag = visit.GetPlayer().schemes.HasCompletedScheme(id);
		return expected == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

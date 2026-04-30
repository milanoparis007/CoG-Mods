using Game.Core;

namespace Game.Session.Data;

public class CheckSchemeCooldown : AbstractVisitRequirement
{
	public bool expected;

	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		bool flag = visit.GetPlayer().schemes.IsSchemeOnCooldownById(id) || visit.GetPlayer().schemes.GetOngoingSchemeForID(id) != null;
		return expected == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

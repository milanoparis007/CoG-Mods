using SomaSim.Util;

namespace Game.Session.Data;

public class CheckRandom : AbstractVisitRequirement
{
	public Fixnum chance;

	public override bool DoesPass(VisitState visit)
	{
		return visit.peep.components.ident.GetIdentityRNGUnchanging().CheckProbability(chance);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

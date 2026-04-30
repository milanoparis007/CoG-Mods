using SomaSim.Util;

namespace Game.Session.Data;

public class CheckOutpostClosures : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).outposts.GetOutpostClosures(visit.building);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), "");
	}
}

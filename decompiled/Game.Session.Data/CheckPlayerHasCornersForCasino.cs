using Game.Services;

namespace Game.Session.Data;

public class CheckPlayerHasCornersForCasino : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return visit.GetPlayer().gambling.HasEnoughCornersToStartGambling();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.casinos-hascorners", "corners", visit.GetPlayer().gambling.GetCornersToStartGambling()));
	}
}

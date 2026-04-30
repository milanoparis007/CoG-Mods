using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Data;

public class CheckKnowBizWithTemplate : AbstractVisitRequirement
{
	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		foreach (Entity allKnownBusiness in Game.ctx.overlays.GetAllKnownBusinesses())
		{
			Entity entity = BuildingUtil.FindBizForBuilding(allKnownBusiness);
			if (entity != null && entity.config.Template == id && allKnownBusiness.components.building.IsScopedBy(PlayerID.HumanPlayer))
			{
				return true;
			}
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

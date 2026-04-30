using System.Collections.Generic;
using Game.Session.Entities;

namespace Game.Session.Data;

public sealed class CheckPotentialCasinoInRange : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		List<BuildingUtil.PotentialGamblingHouse> potentialGamblingHousesOnVisit = BuildingUtil.GetPotentialGamblingHousesOnVisit(visit);
		if (potentialGamblingHousesOnVisit != null)
		{
			return potentialGamblingHousesOnVisit.Count > 0;
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public sealed class CheckPlayerProductionStalled : AbstractVisitRequirement
{
	public int daysBack;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		List<EntityID> allControlledBuildingsUnsafe = visit.GetPlayer().territory.GetAllControlledBuildingsUnsafe();
		SimTime simTime = Game.ctx.clock.Now.IncrementDays(-daysBack);
		bool flag = false;
		foreach (EntityID item in allControlledBuildingsUnsafe)
		{
			if (item.FindEntity().components.modules.FindBackroomModule() is ManufactureModule manufactureModule && manufactureModule.data.lastStall >= simTime)
			{
				flag = true;
			}
		}
		return flag == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}

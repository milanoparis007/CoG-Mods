using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckAllCasinosOperational : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		foreach (EntityID item in from x in Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe()
			where x.FindEntity().components.modules.gambling != null
			select x)
		{
			Fixnum cash = ModulesUtil.GetInventory(item).data.money.cash;
			Fixnum fixnum = item.FindEntity().components.modules.gambling.FindMinimumOperationalValue(visit);
			if (cash < fixnum)
			{
				return false;
			}
		}
		return true;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.casinos-operational"));
	}
}

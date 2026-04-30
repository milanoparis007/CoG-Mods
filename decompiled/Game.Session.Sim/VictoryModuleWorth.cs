using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryModuleWorth : VictoryAbstractWorthSubgoal
{
	public bool countManufacture;

	public bool countConsume;

	public VictoryModuleWorth(bool manufacture, bool consume, string locdesc)
		: base(locdesc)
	{
		countManufacture = manufacture;
		countConsume = consume;
	}

	protected override List<Money> ProduceWorthPerPlayer(List<PlayerInfo> allOutfits)
	{
		return allOutfits.Select((PlayerInfo p) => GetValueForPlayer(p)).ToList();
	}

	public override string ExplainState()
	{
		return ExplainStateAsRanking();
	}

	private int GetValueForBuilding(Entity building)
	{
		Fixnum zERO = Fixnum.ZERO;
		foreach (IBizModule bizModule in ModulesUtil.GetBizModules(building))
		{
			if (countManufacture && bizModule is ManufactureModule manufactureModule)
			{
				zERO += manufactureModule.data.monthlyStats.average;
			}
			if (countConsume && bizModule is ConsumerModule consumerModule)
			{
				zERO += consumerModule.data.monthlyStats.average;
			}
		}
		return zERO.IntFloor();
	}

	private Money GetValueForPlayer(PlayerInfo p)
	{
		int num = (from eid in p.territory.GetAllControlledBuildingsUnsafe()
			select eid.FindEntity()).Sum((Entity building) => GetValueForBuilding(building));
		_ = Game.settings.IsEditor;
		return new Money(num);
	}
}

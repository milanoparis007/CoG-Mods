using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryLuxuryCasinoSubgoal : VictorySubgoal
{
	private static readonly Label LUXURY_CASINO = new Label("casino-large");

	public VictoryLuxuryCasinoSubgoal(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		PlayerInfo human = Game.ctx.players.Human;
		IEnumerable<(GamblingModule module, Entity building)> enumerable = from x in human.gambling.GetAllMyGamblingHouses()
			select (module: x.FindEntity().components.modules.gambling, building: x.FindEntity()) into x
			where x.module.config.id == LUXURY_CASINO
			select x;
		int num = 0;
		Fixnum numberOfCasinos = base.Settings.numberOfCasinos;
		foreach (var item3 in enumerable)
		{
			GamblingModule item = item3.module;
			Entity item2 = item3.building;
			ModQuery query = item.MakeManagerBasedModQuery(human, item2);
			if (item.data.amenities.Count >= item.config.gambling.amenityCount.Evaluate(query))
			{
				num++;
			}
		}
		state = new VictorySubgoalState(num, numberOfCasinos, num >= numberOfCasinos);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

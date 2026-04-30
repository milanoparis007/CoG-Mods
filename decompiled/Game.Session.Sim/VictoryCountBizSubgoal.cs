using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Session.Sim;

public class VictoryCountBizSubgoal : VictorySubgoal
{
	public List<Label> bizConfigs;

	public bool mustBeTiedHouse;

	public VictoryCountBizSubgoal(string locdesc, bool mustBeTiedHouse, List<string> bizConfigs)
		: base(locdesc)
	{
		this.bizConfigs = bizConfigs.Select((string s) => new Label(s)).ToList();
		this.mustBeTiedHouse = mustBeTiedHouse;
	}

	public override void RecomputeState()
	{
		int num = CountBusinesses();
		state = new VictorySubgoalState(num, 1, num >= 1);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}

	public int CountBusinesses()
	{
		return bizConfigs.Sum((Label bizConfig) => CountBusinesses(bizConfig));
	}

	private int CountBusinesses(Label bizConfig)
	{
		return Game.ctx.players.Human.territory.CountModuleAndChildrenInTerritory(bizConfig, mustBeTiedHouse, mustHaveTradeHistory: true);
	}
}

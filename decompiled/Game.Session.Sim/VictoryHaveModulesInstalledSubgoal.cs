using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryHaveModulesInstalledSubgoal : VictorySubgoal
{
	public List<string> moduleIds;

	public VictoryHaveModulesInstalledSubgoal(string locdesc, List<string> moduleIds)
		: base(locdesc)
	{
		this.moduleIds = moduleIds;
	}

	public override void RecomputeState()
	{
		Fixnum amtSpiritProds = base.Settings.amtSpiritProds;
		int num = 0;
		foreach (string moduleId in moduleIds)
		{
			if (Game.ctx.players.Human.territory.CheckModuleOrChildrenInControlled(moduleId))
			{
				num++;
			}
		}
		state = new VictorySubgoalState(num, amtSpiritProds, num >= amtSpiritProds);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

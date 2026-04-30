using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Sim;

public class VictorySpecificBuffSubgoal : VictorySubgoal
{
	public List<Label> buffs;

	public List<Label> bizConfigs;

	public VictorySpecificBuffSubgoal(string locdesc, List<string> bizConfigs, List<Label> buffs)
		: base(locdesc)
	{
		this.buffs = buffs;
		this.bizConfigs = bizConfigs.Select((string s) => new Label(s)).ToList();
	}

	public override void RecomputeState()
	{
		bool flag = buffs.Any((Label buff) => HasBuff(buff));
		state = new VictorySubgoalState(flag ? 1 : 0, 1, flag);
	}

	public bool HasBuff(Label buff)
	{
		bool flag = false;
		PlayerInfo human = Game.ctx.players.Human;
		foreach (Label bizConfig in bizConfigs)
		{
			foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(bizConfig))
			{
				Entity entity = BuildingUtil.FindOwnerForBiz(item);
				if (entity != null && human.social.GetRelationshipFromPlayerTo(entity.Id) != null)
				{
					flag = human.social.GetRelationshipFromSourceToPlayer(entity.Id).HasBuff(buff);
					if (flag)
					{
						break;
					}
				}
			}
			if (flag)
			{
				break;
			}
		}
		return flag;
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}

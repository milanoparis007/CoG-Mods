using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

public sealed class RulesRunner
{
	public List<Rule> rules;

	public void RunRules(Entity entity, PlayerID pid)
	{
		IterateOverRules(rules, entity, pid);
	}

	private static void IterateOverRules(IEnumerable<Rule> ruleslist, Entity entity, PlayerID pid)
	{
		foreach (Rule item in ruleslist)
		{
			if (item.AllSatisfied(entity, pid))
			{
				if (item.result.IsSet)
				{
					ScriptDispatcher.RunScript(item.result, pid, entity);
					AILog.LogRuleScript(pid, entity.Id, item.result.String);
				}
				else if (item.rulesresult != null)
				{
					IterateOverRules(item.rulesresult, entity, pid);
				}
				break;
			}
		}
	}
}

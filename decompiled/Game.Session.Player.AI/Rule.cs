using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

[DebuggerDisplay("Rule result={result}")]
public class Rule
{
	public List<IRuleCondition> conditions;

	public Label result = Label.NULL;

	public List<Rule> rulesresult;

	public bool AllSatisfied(Entity entity, PlayerID pid)
	{
		foreach (IRuleCondition condition in conditions)
		{
			if (!condition.Satisfied(entity, pid))
			{
				return false;
			}
		}
		return true;
	}
}

using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

public class HasTask : IRuleCondition
{
	public bool Satisfied(Entity peep, PlayerID pid)
	{
		return pid.FindPlayer().commands.PeepHasTask(peep.Id);
	}
}

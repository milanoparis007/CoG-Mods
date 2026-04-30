using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

public interface IRuleCondition
{
	bool Satisfied(Entity entity, PlayerID pid);
}

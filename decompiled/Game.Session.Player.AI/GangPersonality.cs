using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.AI;

public sealed class GangPersonality : IRuleCondition
{
	public enum Value
	{
		Not,
		EqualTo
	}

	public Value @is;

	public Label value;

	public bool Satisfied(Entity entity, PlayerID pid)
	{
		Label personality = pid.FindPlayer().ai.Data.personality;
		return @is switch
		{
			Value.Not => !value.Equals(personality), 
			Value.EqualTo => value.Equals(personality), 
			_ => false, 
		};
	}
}

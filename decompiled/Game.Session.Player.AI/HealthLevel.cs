using Game.Core;
using Game.Session.Entities;
using UnityEngine;

namespace Game.Session.Player.AI;

public sealed class HealthLevel : IRuleCondition
{
	public enum Value
	{
		LessThan,
		GreaterThan,
		EqualTo
	}

	public Value @is;

	public float thresh;

	public bool Satisfied(Entity entity, PlayerID pid)
	{
		float currentHealthAsFraction = entity.components.agent.CurrentHealthAsFraction;
		return @is switch
		{
			Value.LessThan => currentHealthAsFraction < thresh, 
			Value.GreaterThan => currentHealthAsFraction > thresh, 
			Value.EqualTo => Mathf.Abs(currentHealthAsFraction - thresh) < float.Epsilon, 
			_ => false, 
		};
	}
}

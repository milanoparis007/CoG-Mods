using System;
using Game.Core;

namespace Game.Session.Quests;

public struct GoalState : IEquatable<GoalState>
{
	public static readonly GoalState EMPTY;

	public string guuid;

	public int current;

	public bool completed;

	public EntityID target;

	public bool IsNotSet => Equals(this, EMPTY);

	public bool IsSet => !Equals(this, EMPTY);

	public GoalState(string guuid, EntityID target)
	{
		this = default(GoalState);
		this.target = target;
		this.guuid = guuid;
		current = 0;
		completed = false;
	}

	public static bool Equals(GoalState a, GoalState b)
	{
		if (a.guuid == b.guuid && a.completed == b.completed && a.current == b.current)
		{
			return a.target == b.target;
		}
		return false;
	}

	public bool Equals(GoalState other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is GoalState a)
		{
			return Equals(a, this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return guuid.GetHashCode();
	}
}

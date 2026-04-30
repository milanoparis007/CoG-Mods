using System;
using System.Diagnostics;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public struct CrewCost : IEquatable<CrewCost>
{
	public static readonly CrewCost ZERO;

	public int moves;

	public int actions;

	public bool IsZero
	{
		get
		{
			if (moves == 0)
			{
				return actions == 0;
			}
			return false;
		}
	}

	public bool IsNonZero
	{
		get
		{
			if (moves == 0)
			{
				return actions != 0;
			}
			return true;
		}
	}

	private string DebugString => $"MOV {moves} ACT {actions}";

	public CrewCost(int moves, int actions)
	{
		this = default(CrewCost);
		this.moves = moves;
		this.actions = actions;
	}

	public static CrewCost OnlyMovement(int value)
	{
		return new CrewCost(value, 0);
	}

	public static CrewCost OnlyActions(int value)
	{
		return new CrewCost(0, value);
	}

	public CrewCost Add(int dmoves, int dactions)
	{
		return new CrewCost(moves + dmoves, actions + dactions);
	}

	public CrewCost Add(CrewCost other)
	{
		return new CrewCost(moves + other.moves, actions + other.actions);
	}

	public bool Equals(CrewCost other)
	{
		if (moves == other.moves)
		{
			return actions == other.actions;
		}
		return false;
	}

	public override string ToString()
	{
		return DebugString;
	}

	public override bool Equals(object obj)
	{
		if (obj is CrewCost crewCost)
		{
			return crewCost.Equals(this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (moves << 8) ^ actions;
	}
}

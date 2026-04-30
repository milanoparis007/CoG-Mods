using System.Collections.Generic;

namespace Game.Core;

public sealed class CommandTypeEqualityComparer : IEqualityComparer<CommandType>
{
	public bool Equals(CommandType x, CommandType y)
	{
		return x == y;
	}

	public int GetHashCode(CommandType type)
	{
		return (int)type;
	}
}

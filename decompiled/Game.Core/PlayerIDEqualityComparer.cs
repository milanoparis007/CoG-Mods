using System.Collections.Generic;

namespace Game.Core;

public sealed class PlayerIDEqualityComparer : IEqualityComparer<PlayerID>
{
	public bool Equals(PlayerID x, PlayerID y)
	{
		return x.id == y.id;
	}

	public int GetHashCode(PlayerID obj)
	{
		return obj.id;
	}
}

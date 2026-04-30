using System.Collections.Generic;

namespace Game.UI.Session.Picks;

public sealed class PickTypeEqualityComparer : IEqualityComparer<PickType>
{
	public bool Equals(PickType x, PickType y)
	{
		return x == y;
	}

	public int GetHashCode(PickType type)
	{
		return (int)type;
	}
}

using System.Collections.Generic;

namespace Game.Core;

public class ZoneTypeEqualityComparer : IEqualityComparer<ZoneType>
{
	public bool Equals(ZoneType x, ZoneType y)
	{
		return x == y;
	}

	public int GetHashCode(ZoneType obj)
	{
		return (int)obj;
	}
}

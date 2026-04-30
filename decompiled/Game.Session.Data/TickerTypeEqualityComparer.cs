using System.Collections.Generic;

namespace Game.Session.Data;

public class TickerTypeEqualityComparer : IEqualityComparer<TickerType>
{
	public bool Equals(TickerType x, TickerType y)
	{
		return x == y;
	}

	public int GetHashCode(TickerType obj)
	{
		return (int)obj;
	}
}

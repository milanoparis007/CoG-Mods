using System.Collections.Generic;

namespace Game.Session.Player;

public class MoneyReasonEqualityComparer : IEqualityComparer<MoneyReason>
{
	public bool Equals(MoneyReason x, MoneyReason y)
	{
		return x == y;
	}

	public int GetHashCode(MoneyReason obj)
	{
		return (int)obj;
	}
}

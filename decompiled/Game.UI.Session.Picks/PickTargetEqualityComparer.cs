using System.Collections.Generic;

namespace Game.UI.Session.Picks;

public class PickTargetEqualityComparer : IEqualityComparer<PickTarget>
{
	public bool Equals(PickTarget x, PickTarget y)
	{
		return PickTarget.Equals(x, y);
	}

	public int GetHashCode(PickTarget t)
	{
		return t.GetHashCode();
	}
}

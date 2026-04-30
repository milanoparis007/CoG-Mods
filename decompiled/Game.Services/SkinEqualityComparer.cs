using System.Collections.Generic;

namespace Game.Services;

public sealed class SkinEqualityComparer : IEqualityComparer<Skin>
{
	public bool Equals(Skin x, Skin y)
	{
		return x == y;
	}

	public int GetHashCode(Skin obj)
	{
		return (int)obj;
	}
}

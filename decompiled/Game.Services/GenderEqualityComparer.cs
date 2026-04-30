using System.Collections.Generic;

namespace Game.Services;

public sealed class GenderEqualityComparer : IEqualityComparer<Gender>
{
	public bool Equals(Gender x, Gender y)
	{
		return x == y;
	}

	public int GetHashCode(Gender obj)
	{
		return (int)obj;
	}
}

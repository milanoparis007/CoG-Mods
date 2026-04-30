using System.Collections.Generic;

namespace Game.UI.Mouseovers;

public sealed class MouseoverTypeEqualityComparer : IEqualityComparer<MouseoverType>
{
	public bool Equals(MouseoverType x, MouseoverType y)
	{
		return x == y;
	}

	public int GetHashCode(MouseoverType obj)
	{
		return (int)obj;
	}
}

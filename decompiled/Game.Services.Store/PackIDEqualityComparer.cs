using System.Collections.Generic;

namespace Game.Services.Store;

public class PackIDEqualityComparer : IEqualityComparer<PackID>
{
	public bool Equals(PackID x, PackID y)
	{
		return x == y;
	}

	public int GetHashCode(PackID obj)
	{
		return (int)obj;
	}
}

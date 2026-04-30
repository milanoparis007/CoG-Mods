using System.Collections.Generic;

namespace Game.Core;

public class EntityIDEqualityComparer : IEqualityComparer<EntityID>
{
	public bool Equals(EntityID x, EntityID y)
	{
		return x.id == y.id;
	}

	public int GetHashCode(EntityID obj)
	{
		return obj.GetHashCode();
	}
}

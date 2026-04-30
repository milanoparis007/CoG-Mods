using System.Collections.Generic;

namespace Game.Session.Entities;

public class EntityWithConfigCache<C> : HashSet<Entity> where C : BaseConfig
{
	public bool AddIfConfigPresent(Entity e, C config)
	{
		if (config != null)
		{
			return Add(e);
		}
		return false;
	}

	public bool RemoveIfConfigPresent(Entity e, C config)
	{
		if (config != null)
		{
			return Remove(e);
		}
		return false;
	}
}

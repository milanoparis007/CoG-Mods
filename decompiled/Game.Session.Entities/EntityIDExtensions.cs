using Game.Core;

namespace Game.Session.Entities;

public static class EntityIDExtensions
{
	public static Entity FindEntity(this EntityID id)
	{
		if (!id.IsValid)
		{
			return null;
		}
		return Game.ctx.entityman.Find(id);
	}
}

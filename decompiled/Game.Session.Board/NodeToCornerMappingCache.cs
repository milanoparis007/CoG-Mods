using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Board;

public sealed class NodeToCornerMappingCache
{
	public Dictionary<NodeID, EntityID> _data = new Dictionary<NodeID, EntityID>(new NodeIDEqualityComparer());

	public (bool valid, Entity corner) FindCorner(NodeID nodeId)
	{
		HashSet<Entity> cachedEntitiesByTemplateUnsafe = Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(EntityConstants.CORNER_TMPL);
		if (cachedEntitiesByTemplateUnsafe == null || cachedEntitiesByTemplateUnsafe.Count == 0)
		{
			Logger.Warning("Calling FindCorner during procgen, this shouldn't happen");
			return (valid: false, corner: null);
		}
		if (_data.TryGetValue(nodeId, out var value))
		{
			return (valid: true, corner: value.FindEntity());
		}
		foreach (Entity item in cachedEntitiesByTemplateUnsafe)
		{
			if (item.data.corner.nid == nodeId)
			{
				_data.Add(nodeId, item.Id);
				return (valid: true, corner: item);
			}
		}
		_data.Add(nodeId, EntityID.INVALID);
		return (valid: true, corner: null);
	}
}

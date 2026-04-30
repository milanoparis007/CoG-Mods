using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Sim;

public sealed class RelationshipEntries : Dictionary<EntityID, RelationshipList>
{
	public const int INITIAL_CAPACITY = 4096;

	public RelationshipEntries()
		: base(4096, (IEqualityComparer<EntityID>)new EntityIDEqualityComparer())
	{
	}
}

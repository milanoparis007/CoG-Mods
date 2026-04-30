using System.Collections.Generic;
using Game.Core;

namespace Game.UI.Session.Convo;

public interface IConvoDataWithSelector
{
	bool HasAny { get; }

	List<EntityID> Entries { get; }

	bool SelectTarget(EntityID entityID);
}

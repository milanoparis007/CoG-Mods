using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Sim;

public sealed class ResourceManagerPersistedData
{
	public List<Label> resourcesForcedLegal = new List<Label>();

	public List<Label> resourcesForcedIllegal = new List<Label>();
}

using System.Collections.Generic;
using Game.Session.Data;

namespace Game.Session.Sim;

public sealed class DemandsTrackerPersistedData
{
	public List<Demand> entries = new List<Demand>();
}

using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Sim;

public class CopTrackerPersistedData
{
	public Xorshift rng = new Xorshift();

	public List<EntityID> stationIDs = new List<EntityID>();

	public int stationCount;

	public List<ArrestEntry> fedArrests = new List<ArrestEntry>();

	public List<ImprisonedEntry> fedImprisoned = new List<ImprisonedEntry>();
}

using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class TransitManagerPersistedData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<TransitManagerPersistedData>();

	public Dictionary<EntityID, TrainData> trainsByID = new Dictionary<EntityID, TrainData>();

	public List<EntityID> cars = new List<EntityID>();

	public Dictionary<NodeID, List<EntityID>> nodeToAgents = new Dictionary<NodeID, List<EntityID>>();

	public List<RailroadData> railroads = new List<RailroadData>();
}

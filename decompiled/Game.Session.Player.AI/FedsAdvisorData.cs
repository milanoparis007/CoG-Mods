using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Player.AI;

public class FedsAdvisorData : BaseAdvisorData
{
	public List<FedInvestigation> queue = new List<FedInvestigation>();

	public List<EntityID> precincts = new List<EntityID>();
}

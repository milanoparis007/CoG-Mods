using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class BusinessTrackerPersistedData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<BusinessTracker>();

	public List<BusinessTracker.EventHistoryListItem> eventHistory = new List<BusinessTracker.EventHistoryListItem>();
}

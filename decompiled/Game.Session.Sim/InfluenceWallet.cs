using System.Collections.Generic;

namespace Game.Session.Sim;

public sealed class InfluenceWallet
{
	public int current;

	public int highwater;

	public List<InfluenceSource> influenceSources = new List<InfluenceSource>();
}

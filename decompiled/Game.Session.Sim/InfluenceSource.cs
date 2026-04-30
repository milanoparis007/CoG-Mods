using Game.Core;

namespace Game.Session.Sim;

public struct InfluenceSource
{
	public EntityID source;

	public int amount;

	public InfluenceSource(EntityID source, int amount)
	{
		this.source = source;
		this.amount = amount;
	}
}

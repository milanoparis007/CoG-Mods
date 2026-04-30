using Game.Session.Data;

namespace Game.Services;

public sealed class HeatSettings
{
	public ModValue velocityPerTurn;

	public ModValue finalHeatMultiplier;

	public ModValue bizRelationshipMultiplier;

	public ModValue fromNeighborsProportion;

	public ModValue fromBusinesses;
}

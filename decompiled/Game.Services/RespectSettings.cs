using Game.Session.Data;

namespace Game.Services;

public sealed class RespectSettings
{
	public ModValue gainThreshold;

	public ModValue lossThreshold;

	public ModValue velocityPerTurn;

	public ModValue finalRespectMultiplier;

	public ModValue bizRelationshipMultiplier;

	public ModValue fromNeighborOutside;

	public ModValue fromNeighborInside;

	public ModValue fromSameEthnicity;

	public ModValue fromSafehouse;
}

using Game.Session.Data;

namespace Game.Services;

public sealed class CrewCostSettings
{
	public ModValue moveOnKnownNode;

	public ModValue moveOnOwnedNode;

	public ModValue moveOnUnknownNode;

	public ModValue pathOnUnknownPenalty;

	public CrewCost scopeOutCost;

	public CrewCost attackCost;

	public CrewCost healCost;

	public CrewCost convoCost;
}

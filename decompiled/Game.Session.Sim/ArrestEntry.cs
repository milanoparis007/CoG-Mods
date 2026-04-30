using Game.Core;

namespace Game.Session.Sim;

public class ArrestEntry
{
	public PlayerID playerId;

	public EntityID peepId;

	public SimTime trialDate;

	public Price payOffCost;

	public bool paidOff;
}

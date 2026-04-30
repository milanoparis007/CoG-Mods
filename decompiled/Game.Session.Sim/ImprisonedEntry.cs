using Game.Core;

namespace Game.Session.Sim;

public class ImprisonedEntry
{
	public PlayerID playerId;

	public EntityID peepId;

	public int years;

	public SimTime startDate;

	public SimTime endDate;
}

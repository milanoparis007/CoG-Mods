using Game.Core;

namespace Game.Session.Player.AI;

public class FedHintStatus
{
	public SimTime lastHint = SimTime.MIN_DATE;

	public SimTime expiration = SimTime.MIN_DATE;
}

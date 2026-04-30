using Game.Core;

namespace Game.Session.Player.AI;

public class CopDonation
{
	public PlayerID payer;

	public Price paid;

	public SimTime expiration;

	public CopDonation()
	{
	}

	public CopDonation(PlayerID payer, Price paid, SimTime expiration)
	{
		this.payer = payer;
		this.paid = paid;
		this.expiration = expiration;
	}
}

using System.Collections.Generic;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerSocialData
{
	public EntityID peepId;

	public string fullname;

	public string lastname;

	public Label eth;

	public string groupname;

	public EntityID francine;

	public TransactionHistory transactions;

	public Xorshift rng;

	public List<HistoryLedgerItem> historyledger;

	public PlayerSocialData()
	{
	}

	public PlayerSocialData(PlayerID pid)
	{
		if (pid.IsHumanPlayer)
		{
			rng = Game.ctx.scenario.MakeSeededRng(pid);
		}
	}
}

using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Player;
using Game.UI.Session.Ledger;

namespace Game.Session.Sim;

public class VictoryNetWorthSubgoal : VictoryAbstractWorthSubgoal
{
	public VictoryNetWorthSubgoal(string locdesc)
		: base(locdesc)
	{
	}

	protected override List<Money> ProduceWorthPerPlayer(List<PlayerInfo> allOutfits)
	{
		return allOutfits.Select((PlayerInfo p) => LedgerReportGenerator.GetNetWorthOfPlayer(p)).ToList();
	}
}

using System.Collections.Generic;
using Game.Session.Data;
using Game.Session.Sim;

namespace Game.UI.Session.Tickers;

public class TickerCombatResultsData : TickerData
{
	public CombatSummary summary;

	public List<CombatResults> results;
}

namespace Game.UI.Session.Tickers;

public sealed class TickerCombatResults : BaseTicker
{
	protected override bool HandleClick()
	{
		if (data is TickerCombatResultsData tickerCombatResultsData)
		{
			PhotoPopupUtils.ShowCombatSummary(tickerCombatResultsData.summary, tickerCombatResultsData.results, isAttackerAI: true);
		}
		data.target.TweenCamera();
		return true;
	}
}

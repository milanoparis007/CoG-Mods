using Game.Session.Data;

namespace Game.UI.Session.Tickers;

public class TickerLawbookData : TickerData
{
	public override bool OnClickCallback()
	{
		Game.serv.ui.AddPopup(new LawPopup());
		return true;
	}
}

using Game.Session.Data;
using Game.UI.Session.Popups;

namespace Game.UI.Session.Tickers;

public class TickerThroneData : TickerData
{
	public bool toThrone;

	public override bool OnClickCallback()
	{
		if (toThrone && Game.ctx.players.Human.throne.GetThroneStyle().IsSet)
		{
			Game.serv.ui.AddPopup(new ThronePopup());
		}
		else
		{
			Game.serv.ui.AddPopup(new ThroneSelectionPopup());
		}
		return true;
	}
}

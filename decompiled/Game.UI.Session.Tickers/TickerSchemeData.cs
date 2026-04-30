using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.UI.Session.Tickers;

public class TickerSchemeData : TickerData
{
	public override bool OnClickCallback()
	{
		SchemeData schemeForCrew = Game.ctx.players.Human.schemes.GetSchemeForCrew(target.entityId.FindEntity());
		Game.serv.ui.AddPopup(new SchemePopup(schemeForCrew));
		return true;
	}
}

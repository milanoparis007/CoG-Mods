using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Tickers;

public class TickerWardData : TickerData
{
	public override bool OnClickCallback()
	{
		Game.ctx.selection.SetActive(target.entityId.FindEntity());
		return true;
	}
}

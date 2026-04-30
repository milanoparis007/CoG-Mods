using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Tickers;

public class TickerLevelupData : TickerData
{
	public override bool OnClickCallback()
	{
		target.entityId.FindEntity()?.components.agent?.ShowLevelupPopup();
		return true;
	}
}

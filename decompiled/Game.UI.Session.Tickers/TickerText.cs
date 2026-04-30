using Game.UI.Mouseovers;

namespace Game.UI.Session.Tickers;

public sealed class TickerText : BaseTicker
{
	protected override bool HandleClick()
	{
		Game.serv.mouseovers.OnMouseOut(MouseoverType.TickerButton);
		data.target.TweenCamera();
		return data.OnClickCallback();
	}
}

using System.Text;
using Game.Services;
using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI.Session.Tickers;

public class TickerButtonMouseover : BaseMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TickerMouseover;

	public override void RefreshContents()
	{
		BaseTicker.Context component = context.GetComponent<BaseTicker.Context>();
		bool isSet = component.ticker.data.target.IsSet;
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		stringBuilder.AppendLine(component.ticker.data.message);
		stringBuilder.AppendLine();
		if (isSet)
		{
			stringBuilder.AppendLine(Loc.Get("ui.tickers.leftclick"));
		}
		stringBuilder.AppendLine(Loc.Get("ui.tickers.rightclick"));
		go.SetText("Text", stringBuilder.ToStringAndReturnToPool());
	}
}

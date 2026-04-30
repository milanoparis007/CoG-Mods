using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGoonBuyout : ConvoData
{
	public PlayerID goon;

	public Price price;

	public string goonStr;

	public string priceStr;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[4] { "amt", priceStr, "name", goonStr };
	}

	public ConvoDataGoonBuyout()
	{
	}

	public ConvoDataGoonBuyout(PlayerInfo goon, Price price)
	{
		this.price = price;
		priceStr = Loc.Price(price, abs: true);
		this.goon = goon.PID;
		goonStr = goon.social.PlayerFullName;
	}
}

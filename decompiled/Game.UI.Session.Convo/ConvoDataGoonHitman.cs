using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGoonHitman : ConvoData
{
	public PlayerID goon;

	public PlayerID target;

	public Price price;

	public string goonStr;

	public string targetStr;

	public string priceStr;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[6] { "thisgroup", goonStr, "targetgroup", targetStr, "amt", priceStr };
	}

	public ConvoDataGoonHitman()
	{
	}

	public ConvoDataGoonHitman(PlayerInfo goon, PlayerInfo target, Price price)
	{
		this.price = price;
		priceStr = Loc.Price(price, abs: true);
		this.goon = goon.PID;
		goonStr = goon.social.PlayerGroupName;
		this.target = target.PID;
		targetStr = target.social.PlayerGroupName;
	}
}

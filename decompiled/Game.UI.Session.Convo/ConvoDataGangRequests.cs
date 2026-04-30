using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGangRequests : ConvoData
{
	public PlayerID source;

	public PlayerID target;

	public PlayerID thirdParty;

	public bool accept;

	public Price price;

	public int days;

	public bool IsStartedByHuman => source.IsHumanPlayer;

	public bool IsTargetingHuman => target.IsHumanPlayer;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[6]
		{
			"amt",
			Loc.Price(price, abs: true),
			"days",
			Loc.FormatNumber(days),
			"outfitNameTarget",
			thirdParty.IsSystem ? "" : (thirdParty.FindPlayer()?.social?.FindPlayerGroupNameColorized() ?? "")
		};
	}

	public ConvoDataGangRequests()
	{
	}

	public ConvoDataGangRequests(PlayerID source, PlayerID target, bool accept, Price price, int days, PlayerID thirdParty = default(PlayerID))
	{
		this.source = source;
		this.target = target;
		this.thirdParty = thirdParty;
		this.accept = accept;
		this.price = price;
		this.days = days;
	}
}

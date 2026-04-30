using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataCopsDonation : ConvoData
{
	public Price price;

	public int days;

	public ConvoDataCopsDonation()
	{
	}

	public ConvoDataCopsDonation(Price price, int days)
	{
		this.price = price;
		this.days = days;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[4]
		{
			"amt",
			Loc.Price(price, abs: true),
			"days",
			Loc.FormatNumber(days)
		};
	}
}

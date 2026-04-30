using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public class ConvoDataDemand : ConvoData
{
	public Demand.Type type;

	public Demand.Target target;

	public Price price;

	public ConvoDataDemand()
	{
	}

	public ConvoDataDemand(Demand.Type type, Demand.Target target)
	{
		this.type = type;
		this.target = target;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[2]
		{
			"price",
			Loc.Price(price)
		};
	}

	public bool CanExecuteDemandPayment(PlayerID payer)
	{
		return payer.FindPlayer().finances.CanChangeMoneyOnPlayer(price);
	}
}

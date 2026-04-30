using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public class ConvoDataVehicleCosts : ConvoData
{
	public Price repairPrice;

	public Price buyBackPrice;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[4]
		{
			"rprice",
			Loc.Price(repairPrice, abs: true),
			"bprice",
			Loc.Price(buyBackPrice, abs: true)
		};
	}

	public ConvoDataVehicleCosts()
	{
	}

	public ConvoDataVehicleCosts(Price repairPrice, Price buyBackPrice)
	{
		this.repairPrice = repairPrice;
		this.buyBackPrice = buyBackPrice;
	}
}

using System.Linq;

namespace Game.Session.Data;

public class CheckCanBuySell : AbstractVisitRequirement
{
	public enum BuySellType
	{
		Fail,
		OnlyBuy,
		OnlySell,
		BuyAndSell,
		BuyOrSell,
		Buy,
		Sell,
		Neither
	}

	public BuySellType playercan;

	public override bool DoesPass(VisitState visit)
	{
		return CheckAvailability(visit);
	}

	public bool CheckAvailability(VisitState visit)
	{
		bool flag = visit.building.components.modules.ProduceAllItemsPlayerCanBuyOrSell(visit.pid, playerBuys: true, playerSells: false).Any();
		bool flag2 = visit.building.components.modules.ProduceAllItemsPlayerCanBuyOrSell(visit.pid, playerBuys: false, playerSells: true).Any();
		switch (playercan)
		{
		case BuySellType.BuyAndSell:
			return flag && flag2;
		case BuySellType.BuyOrSell:
			return flag || flag2;
		case BuySellType.Buy:
			return flag;
		case BuySellType.Sell:
			return flag2;
		case BuySellType.OnlyBuy:
			if (flag)
			{
				return !flag2;
			}
			return false;
		case BuySellType.OnlySell:
			if (flag2)
			{
				return !flag;
			}
			return false;
		case BuySellType.Neither:
			if (!flag)
			{
				return !flag2;
			}
			return false;
		default:
			return false;
		}
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), "");
	}
}

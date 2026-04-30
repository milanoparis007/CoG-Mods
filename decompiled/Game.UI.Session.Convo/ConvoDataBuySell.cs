using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataBuySell : ConvoData
{
	public BuySellElement elt;

	public DeliveryInfo info;

	public bool haveInCar;

	public bool playerBuys;

	public string resname;

	public CrewAssignment crew;

	public Price unitprice;

	public Fixnum multiplier;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[16]
		{
			"items",
			resname,
			"unitprice",
			Loc.Price(unitprice.Abs),
			"unitname",
			FindResource().unitdef.GetBareUnitNoun(1),
			"multiplier",
			Loc.Percentage(multiplier),
			"priceshift",
			((double)(float)multiplier < 1.0) ? Loc.Get("convo.buy-select.discount", "price", Loc.Percentage(1 - multiplier)) : (((double)(float)multiplier == 1.0) ? "" : Loc.Get("convo.sell-select.gouge", "price", Loc.Percentage(multiplier - 1))),
			"percentoff",
			Loc.Percentage(multiplier - 1),
			"maxunits",
			FindResource().unitdef.GetQtyAndUnits(info.max.qty.Abs),
			"every-x-days",
			Loc.GetPluralized("ui.every-x-days", info.days, "num", info.days)
		};
	}

	public override string MakeIconOrNull()
	{
		return elt.item.FindResource().GetIcon();
	}

	public ConvoDataBuySell()
	{
	}

	public ConvoDataBuySell(BuySellElement elt, DeliveryInfo info, bool haveInCar, bool playerBuys, Fixnum multiplier)
	{
		this.elt = elt;
		this.haveInCar = haveInCar;
		this.playerBuys = playerBuys;
		this.info = info;
		this.multiplier = multiplier;
		Resource resource = elt.item.FindResource();
		resname = resource.GetName();
		unitprice = resource.GetPriceWithMultiplier(playerBuys, multiplier, PlayerID.HumanPlayer);
	}

	public void UpdateMultiplier(Fixnum multiplier)
	{
		this.multiplier = multiplier;
		unitprice = FindResource().GetPriceWithMultiplier(playerBuys, multiplier, PlayerID.HumanPlayer);
	}

	public Resource FindResource()
	{
		return elt.item.FindResource();
	}
}

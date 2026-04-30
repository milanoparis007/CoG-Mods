using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session.Deliveries;
using SomaSim.Util;

namespace Game.Session.Data;

public static class AutomationUtil
{
	public const string SMALL_BOTTLES = "small-bottles";

	public static string Describe(AutomationStep step, int newlines = 0)
	{
		if (step == null)
		{
			return Loc.Get("ui.deliveries.undefined");
		}
		Entity entity = BuildingUtil.FindBizForBuilding(step.target);
		GamblingModule gamblingModule = step.target.FindEntity()?.components.modules.gambling;
		if (entity == null && gamblingModule == null)
		{
			return Loc.Get("ui.deliveries.next-step-empty");
		}
		string pluralized = Loc.GetPluralized("ui.deliveries.biz-name", newlines, "bizname", BuildingUtil.FindBuildingName(step.target));
		if (step.action == AutoAction.None)
		{
			return pluralized + Loc.Get("ui.deliveries.action-empty");
		}
		string text = "?";
		switch (step.action)
		{
		case AutoAction.Buy:
			text = Loc.Get("ui.deliveries.actions.buy");
			break;
		case AutoAction.Sell:
			text = Loc.Get("ui.deliveries.actions.sell");
			break;
		case AutoAction.PickUp:
			text = Loc.Get("ui.deliveries.actions.pickup");
			break;
		case AutoAction.DropOff:
			text = Loc.Get("ui.deliveries.actions.dropoff");
			break;
		case AutoAction.HaveCash:
			text = Loc.Get("ui.deliveries.actions.cash-on-hand");
			break;
		case AutoAction.FrontVisit:
			text = Loc.Get("ui.deliveries.actions.collect-front");
			break;
		case AutoAction.BottlePickup:
			text = Loc.Get("ui.deliveries.actions.bottle-pickup");
			break;
		case AutoAction.VehicleRepair:
			text = Loc.Get("ui.deliveries.actions.repair-vehicle");
			break;
		}
		string text2 = "?";
		if (step.action == AutoAction.FrontVisit || step.action == AutoAction.BottlePickup)
		{
			text2 = Loc.Get("ui.deliveries.cash.agreed");
		}
		else if (step.items.iscash)
		{
			text2 = DescribeCashHelper(step);
		}
		else if (step.items.res.IsSet)
		{
			text2 = DescribeResHelper(step, newlines);
		}
		if (step.action == AutoAction.VehicleRepair)
		{
			return pluralized + text;
		}
		return pluralized + Loc.GetPluralized("ui.deliveries.action-qty", newlines, "action", text, "qtyres", text2);
	}

	internal static string DescribeCashHelper(AutomationStep step)
	{
		string result = "?";
		if (step.action == AutoAction.HaveCash)
		{
			result = Loc.Get("ui.deliveries.cash.exact", "amount", Loc.Money(step.items.qty));
		}
		else
		{
			string text = ((step.items.type == AmtChoiceType.Everything) ? null : Loc.Money(step.items.qty));
			switch (step.items.type)
			{
			case AmtChoiceType.Everything:
				result = Loc.Get("ui.deliveries.cash.all-available");
				break;
			case AmtChoiceType.EnsureAmount:
				result = Loc.Get("ui.deliveries.cash.ensure", "target", (step.action == AutoAction.PickUp) ? Loc.Get("ui.deliveries.res.vehicle") : Loc.Get("ui.deliveries.res.location"), "quantity", Loc.Money(step.items.qty));
				break;
			case AmtChoiceType.AllBut:
				result = Loc.Get("ui.deliveries.cash.excess", "quantity", text);
				break;
			case AmtChoiceType.Amount:
				result = Loc.Get("ui.deliveries.cash.up-to", "quantity", text);
				break;
			}
		}
		return result;
	}

	internal static string DescribeResHelper(AutomationStep step, int newlines)
	{
		string text = null;
		Resource resource = Resource.Find(step.items.res);
		string[] array = new string[2]
		{
			"icon&name",
			resource.GetIconAndName()
		};
		if (step.items.type != AmtChoiceType.Everything)
		{
			array = array.Append("quantity", resource.unitdef.GetQtyAndUnits(step.items.qty));
		}
		switch (step.items.type)
		{
		case AmtChoiceType.AllBut:
			text = "ui.deliveries.res.excess";
			break;
		case AmtChoiceType.Amount:
			text = "ui.deliveries.res.up-to";
			break;
		case AmtChoiceType.Everything:
			text = "ui.deliveries.res.max-possible";
			break;
		case AmtChoiceType.EnsureAmount:
		{
			bool flag = step.action == AutoAction.PickUp || step.action == AutoAction.Buy || step.action == AutoAction.Sell;
			array = array.Append("target", flag ? Loc.Get("ui.deliveries.res.vehicle") : Loc.Get("ui.deliveries.res.location"));
			text = "ui.deliveries.res.ensure";
			break;
		}
		}
		if (text == null)
		{
			return "?";
		}
		string key = text;
		object[] replacements = array;
		return Loc.GetPluralized(key, newlines, replacements);
	}
}

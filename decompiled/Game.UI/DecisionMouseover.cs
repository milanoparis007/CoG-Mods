using System.Collections.Generic;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI;

public class DecisionMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		DecisionCtx component = context.GetComponent<DecisionCtx>();
		if (component == null)
		{
			return Loc.Get("ui.scheme.call-off");
		}
		string text = "";
		InventoryModule inventory = ModulesUtil.GetInventory(Game.ctx.players.Human.territory.Safehouse.FindEntity());
		List<ResOrCash> cost = component.info.cost;
		if (cost != null)
		{
			foreach (ResOrCash item in cost)
			{
				if (item.IsCash)
				{
					text = text + Loc.IconLineSubItem(inventory.data.money.cash.Abs >= item.money.cash.Abs, Loc.Price(item.money.cash.Abs)) + "\n";
				}
				if (item.IsResource)
				{
					string text2 = text;
					bool valid = inventory.data.WillBeNonNegative(item.raq.id, -item.raq.qty);
					ResourceAndQty raq = item.raq;
					text = text2 + Loc.IconLineSubItem(valid, raq.MakeQuantityLocString()) + "\n";
				}
			}
		}
		if (cost == null && component.info.nextState.IsNotSet)
		{
			return Loc.Get("ui.scheme.call-off");
		}
		if (cost == null)
		{
			return null;
		}
		return Loc.Get("ui.requirements.scheme.canpay", "reqs", text);
	}

	public override void RefreshContents()
	{
		string text = ProduceText();
		go.SetText("Text", text ?? "");
		if (text == null)
		{
			Game.serv.mouseovers.OnMouseOut(MouseoverType.SchemeDecision);
		}
	}
}

using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPaySchemeStartupCosts : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (bstate.data is ConvoDataScheme convoDataScheme)
		{
			SchemeDef scheme = Game.serv.globals.settings.schemes.FindSchemeById(convoDataScheme.selected);
			return Game.ctx.players.Human.schemes.CanPaySchemeStartupCost(scheme);
		}
		return false;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		string text = "";
		if (bstate.data is ConvoDataScheme convoDataScheme)
		{
			SchemeDef schemeDef = Game.serv.globals.settings.schemes.FindSchemeById(convoDataScheme.selected);
			InventoryModule inventory = ModulesUtil.GetInventory(visit.pid.FindPlayer().territory.Safehouse.FindEntity());
			foreach (ResOrCash item in schemeDef.startup.cost)
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
		return new ReqExplanation(DoesPass(visit, bstate), Loc.Get("ui.requirements.scheme.canpay", "reqs", text));
	}
}

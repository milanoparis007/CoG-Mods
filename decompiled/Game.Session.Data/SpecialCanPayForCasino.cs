using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public class SpecialCanPayForCasino : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (bstate.data == null)
		{
			return visit.GetPlayer().gambling.CanPayForUpgradeCasino(visit);
		}
		if (bstate.data is ConvoDataGamblingHouseSelection convoDataGamblingHouseSelection)
		{
			GamblingModuleConfig def = ModulesUtil.FindModuleDef(convoDataGamblingHouseSelection.gamblingModuleId) as GamblingModuleConfig;
			return visit.GetPlayer().gambling.CanPayForStartCasino(visit, def);
		}
		Logger.Warning("Neither buying nor upgrading casino in SpecialCanPayForCasino?");
		return false;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		Price price = GetPrice(visit, bstate);
		string key = ((bstate.data == null) ? "ui.requirements.can-pay-upgrade-casino" : "ui.requirements.can-pay-start-casino");
		Fixnum amt = ((bstate.data == null) ? ModulesUtil.GetInventory(visit.building).data.money.cash : ModulesUtil.GetInventory(visit.vehicle).data.money.cash);
		return new ReqExplanation(DoesPass(visit, bstate), Loc.Get(key, "cost", Loc.Money(price.cash), "onHand", Loc.Money(amt)));
	}

	public Price GetPrice(VisitState visit, ConvoButtonState bstate)
	{
		ModQuery query = new ModQuery(visit.pid, EntityID.INVALID, visit.crew.peepId);
		if (bstate.data == null)
		{
			return (ModulesUtil.FindModuleDef(visit.building.components.modules.gambling.config.gambling.upgradeModule) as GamblingModuleConfig).gambling.upgradeCost.Evaluate(query);
		}
		if (bstate.data is ConvoDataGamblingHouseSelection convoDataGamblingHouseSelection)
		{
			return (ModulesUtil.FindModuleDef(convoDataGamblingHouseSelection.gamblingModuleId) as GamblingModuleConfig).gambling.purchaseCost.Evaluate(query);
		}
		return 0;
	}
}

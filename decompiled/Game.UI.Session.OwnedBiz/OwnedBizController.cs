using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session.Crew;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

public sealed class OwnedBizController : HUDController<OwnedBizModel, OwnedBizDialog, OwnedBizController>
{
	internal struct ExpansionInfo
	{
		public List<ModuleExpansionConfig> canBeInstalled;

		public List<ModuleExpansionConfig> alreadyInstalled;

		public int countMax;

		public bool CanInstallAny => canBeInstalled.Count > 0;

		public bool NoneAvailable => countMax == 0;
	}

	internal void SetModel(VisitState visit)
	{
		base.Model.visit = visit;
	}

	internal void OnBeforeViewHide()
	{
		SetModel(null);
	}

	internal void HideSubviews()
	{
		base.View.ShowSubview(ViewType.None);
	}

	internal void OnCloseButton()
	{
		Game.ctx.selection.ClearActive();
	}

	internal void OnSwitchBuildingButton(int delta)
	{
		List<EntityID> allControlledBuildingsUnsafe = Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe();
		int num = allControlledBuildingsUnsafe.FindIndex((EntityID x) => x == base.Model.visit.building.Id);
		Game.ctx.selection.SetActive(allControlledBuildingsUnsafe[num + delta].FindEntity());
	}

	internal void OnSwitchCrew(CrewAssignment newCrew)
	{
		base.Model.visit.SetCrew(newCrew);
		base.Model.invstate.RefreshOnCrewChange(base.Model);
		base.View.ControllerRequestsFullRefresh(resetSubview: false);
	}

	internal void OnManagerInfoButton(Entity peep)
	{
		Game.ctx.hud.personInfo.Show(peep, base.Model.visit.building);
	}

	internal void OnManagerEditButton()
	{
		Game.ctx.selection.ClearActive();
		Game.serv.ui.AddPopup<CrewManagementPopup>();
	}

	internal void OnModuleButtonClick(GameObject card)
	{
		ModuleToggleContext component = card.GetComponent<ModuleToggleContext>();
		OnModuleButtonClick(component);
	}

	internal void OnModuleButtonClick(ModuleToggleContext ctx)
	{
		base.Model.currentSlot = ctx;
		Game.ctx.sfx.PlayOwnedBizTab();
		if (!ctx.IsModuleInstalled)
		{
			base.View.ShowSubview(ViewType.ViewAddModule);
		}
		else
		{
			base.View.ShowSubview(ModuleTypeToView(ctx.module));
		}
	}

	private ViewType ModuleTypeToView(IModule module)
	{
		if (!(module is InventoryModule))
		{
			if (!(module is ManufactureModule) && !(module is ConsumerModule) && !(module is VehicleModule) && !(module is ExplanationModule))
			{
				if (module == null)
				{
					return ViewType.None;
				}
				return ViewType.None;
			}
			return ViewType.ViewDescribeModule;
		}
		return ViewType.ViewInventory;
	}

	public void SetInventoryLoadingMode(bool loading, bool shutdown)
	{
		base.Model.invstate.ToggleLoading(base.Model, loading);
		if (base.View.CurrentSubview is ViewInventory viewInventory && !shutdown)
		{
			viewInventory.RefreshAllPanels();
		}
	}

	internal bool CanChangeManager(Entity manager)
	{
		if (manager != null)
		{
			return true;
		}
		return base.Model.visit.GetPlayer().crew.LivingCrewCount > 1;
	}

	private (InventoryModule source, InventoryModule target) FindInventories(bool toBldg)
	{
		InventoryModule item = (toBldg ? base.Model.invstate.vehicle : base.Model.invstate.building);
		InventoryModule item2 = (toBldg ? base.Model.invstate.building : base.Model.invstate.vehicle);
		return (source: item, target: item2);
	}

	internal bool CanLoadAtLeastOne(ResOrCash item, bool toBldg, bool isJunk)
	{
		if (isJunk && !toBldg && !item.IsCash)
		{
			return false;
		}
		(InventoryModule source, InventoryModule target) tuple = FindInventories(toBldg);
		InventoryModule item2 = tuple.source;
		InventoryModule item3 = tuple.target;
		Fixnum fixnum = ((!item.IsCash) ? FindResourcesToMove(item, item2, item3, 1) : FindCashToMove(item2, item3, 1).cash);
		return fixnum > 0;
	}

	internal void DoPerformLoading(bool toBldg, ResOrCash item)
	{
		(InventoryModule source, InventoryModule target) tuple = FindInventories(toBldg);
		InventoryModule item2 = tuple.source;
		InventoryModule item3 = tuple.target;
		int requested = InventoryModule.ProduceClickMoveQty();
		if (item.IsCash)
		{
			Price price = FindCashToMove(item2, item3, requested);
			item2.data.DoChangeMoney(null, -price);
			item3.data.DoChangeMoney(null, price);
		}
		else
		{
			Fixnum fixnum = FindResourcesToMove(item, item2, item3, requested);
			item2.data.Increment(item.raq.id, -fixnum);
			item3.data.Increment(item.raq.id, fixnum);
		}
	}

	private static Price FindCashToMove(InventoryModule source, InventoryModule _, int requested)
	{
		Fixnum cash = source.data.money.cash;
		return new Price(Fixnum.Min(requested, cash));
	}

	private static Fixnum FindResourcesToMove(ResOrCash item, InventoryModule source, InventoryModule target, int requested)
	{
		Fixnum fixnum = Fixnum.Min(source.data.Get(item.raq.id).qty, requested);
		if (fixnum.IsZero)
		{
			return fixnum;
		}
		Fixnum b = target.HowManyResourcesCanFit(item.raq.FindResource());
		return Fixnum.Min(fixnum, b);
	}

	internal bool CanInstall(AddModuleDef moduledef)
	{
		if (moduledef.passesReqs)
		{
			return CanPlayerAffordCosts(moduledef);
		}
		return false;
	}

	private bool CanPlayerAffordCosts(AddModuleDef moduledef)
	{
		return (moduledef.config?.Common?.purchase)?.CanPlayerAfford(base.Model.visit.pid, base.Model.visit.building) ?? false;
	}

	internal (bool valid, string explanation) ExplainCosts(IModuleConfig config)
	{
		return (config?.Common?.purchase)?.Explain(base.Model.visit.pid, base.Model.visit.building) ?? (false, null);
	}

	internal void OnModuleAddButtonClick()
	{
		Game.serv.ui.AddPopup(new OwnedBizAddModulePopup(this));
	}

	internal void OnDestroyModuleButtonClick()
	{
		ModulePurchaseCost purchase = base.Model.visit.building.components.modules.FindBackroomModule().ModuleConfig.Common.purchase;
		Price destroyPrice = purchase.GetDestroyPrice(PlayerID.HumanPlayer, base.Model.visit.building);
		Game.serv.ui.AddPopup(new OkCancelPopup(Loc.Get("ui.ownedbiz.destroy-module.confirm", "price", Loc.Price(destroyPrice.Abs)), Loc.Get("ui.ownedbiz.destroy-module.confirm-ok"), Loc.Get("button.cancel"), delegate
		{
			OnModuleDestroyConfirm();
		}, delegate
		{
		}, purchase.CanPlayerAffordDestroy(PlayerID.HumanPlayer, base.Model.visit.building)));
	}

	public void OnUpgradeClick(IModule _, List<AddModuleDef> upgrades)
	{
		List<IModuleConfig> upgrades2 = upgrades.SelectIntoNewList((AddModuleDef def) => def.config);
		Game.serv.ui.AddPopup(new OwnedBizAddModulePopup(this, upgrades2));
	}

	internal void OnModuleAddConfirm(AddModuleDef moduledef, Label previous, ModuleSlot slotdef)
	{
		if (previous.IsSet)
		{
			List<Label> ids = new List<Label> { previous };
			base.Model.visit.building.components.modules.RemoveModules(ids, shutdown: false);
			ModulesUtil.MakeModuleQuery(base.Model.visit.building).manager?.components.agent.IncrementStat(CrewStats.ExpansionsUpgradesBuilt, 1);
		}
		Label id = moduledef.config.Id;
		base.Model.visit.building.components.modules.InstallModuleFromUI(id, base.Model.visit.time);
		moduledef.config.Common.purchase.DoSpendAndConsume(base.Model.visit.pid, base.Model.visit.building, MoneyReason.OwnedBizCosts);
		base.View.ControllerRequestsFullRefresh(resetSubview: true);
		base.View.ControllerRequestsModuleTab(slotdef);
	}

	internal void OnModuleDestroyConfirm()
	{
		Label id = base.Model.currentSlot.module.ModuleConfig.Id;
		base.Model.currentSlot.module.ModuleConfig.Common.purchase.DoPayForDestroy(base.Model.visit.pid, base.Model.visit.building, MoneyReason.OwnedBizCosts);
		base.Model.visit.building.components.modules.RemoveModules(new List<Label> { id }, shutdown: false);
		base.View.ControllerRequestsFullRefresh(resetSubview: true);
	}

	internal void OnInventoryReplacementClick(InventoryModuleConfig replacement, ExplanationModuleConfig backroom)
	{
		ModulesComponent modules = base.Model.visit.building.components.modules;
		InventoryModule inv = modules.inventory;
		string text = Loc.Volume(inv.config.capacity);
		string text2 = Loc.Volume(replacement.capacity);
		OkPopup.ShowOkCancel(Loc.Get("ui.viewaddmodule.confirm.addstorage", "currentvolume", text, "futurevolume", text2), delegate
		{
			DoInventoryReplacement(inv, replacement, backroom);
		}, delegate
		{
		});
	}

	private void DoInventoryReplacement(InventoryModule oldinv, InventoryModuleConfig replacement, ExplanationModuleConfig backroom)
	{
		List<ResourceAndQty> contents = oldinv.data.contents;
		Money money = oldinv.data.money;
		ModulesComponent modules = base.Model.visit.building.components.modules;
		modules.RemoveModules(new List<Label> { oldinv.config.id }, shutdown: false);
		modules.InstallModuleManually(replacement.id, Game.ctx.clock.Now);
		InventoryModule inventory = modules.inventory;
		inventory.data.money = money;
		foreach (ResourceAndQty item in contents)
		{
			Label id = item.id;
			Fixnum qty = item.qty;
			inventory.ForceAddResourcesRegardlessOfSpace(id, qty.IntFloor());
		}
		modules.InstallModuleManually(backroom.id, Game.ctx.clock.Now);
		base.View.ControllerRequestsFullRefresh(resetSubview: true);
		base.View.ControllerRequestsModuleTab(modules.FindModuleSlotForModule(inventory));
	}

	internal List<AddModuleDef> FindModulesToAddForSlot(UpgradeContext upctx)
	{
		Entity biz = base.Model.visit.biz;
		ModuleSlot slotdef = base.Model.currentSlot.slotdef;
		IEnumerable<IModuleConfig> configs = from cfg in ModulesUtil.FindAllModuleDefsExpensive()
			where biz.components.biz.CanInstallModuleInSlot(cfg, slotdef)
			select cfg;
		List<AddModuleDef> list = (from config in FilterUpgrades(configs, upctx)
			select ModulesUtil.MakeAddModuleDef(config, base.Model.visit) into def
			where def.passesVisreqs
			select def).ToList();
		list.StableSort((AddModuleDef a, AddModuleDef b) => (!a.passesReqs || b.passesReqs) ? ((!a.passesReqs && b.passesReqs) ? 1 : 0) : (-1));
		return list;
	}

	private IEnumerable<IModuleConfig> FilterUpgrades(IEnumerable<IModuleConfig> configs, UpgradeContext upctx)
	{
		List<IModuleConfig> permitted = upctx?.permitted;
		if (permitted != null)
		{
			return configs.Where((IModuleConfig cfg) => permitted.Contains(cfg));
		}
		return configs;
	}

	internal ExpansionInfo GetExpansions(IModule module)
	{
		List<ModuleExpansionConfig> alreadyInstalled = module.FindExpansionsInstalled(base.Model.visit);
		List<ModuleExpansionConfig> canBeInstalled = module.FindExpansionsToOffer(base.Model.visit);
		int item = module.GetExpansionCounts(base.Model.visit).max;
		return new ExpansionInfo
		{
			alreadyInstalled = alreadyInstalled,
			canBeInstalled = canBeInstalled,
			countMax = item
		};
	}

	internal void InstallExpansion(IModule module, ModuleExpansionConfig config)
	{
		DoPayExpansionCosts(config);
		module.AddExpansion(config.id);
		ModulesUtil.MakeModuleQuery(base.Model.visit.building).manager?.components.agent.IncrementStat(CrewStats.ExpansionsUpgradesBuilt, 1);
	}

	internal void RemoveExpansion(IModule module, ModuleExpansionConfig config)
	{
		module.RemoveExpansion(config.id);
	}

	internal bool CanPlayerAffordCosts(ModuleExpansionConfig config)
	{
		Price cashCost = config.purchase.cashCost;
		return base.Model.visit.GetPlayer().finances.CanChangeMoney(base.Model.visit.building, cashCost);
	}

	private void DoPayExpansionCosts(ModuleExpansionConfig config)
	{
		Price cashCost = config.purchase.cashCost;
		base.Model.visit.GetPlayer().finances.DoChangeMoney(base.Model.visit.building, cashCost, MoneyReason.OwnedBizCosts);
	}

	internal string ExplainCosts(ModuleExpansionConfig config)
	{
		Price cashCost = config.purchase.cashCost;
		_ = base.Model.visit.building.components.modules.inventory;
		BusinessSettings.GlobalModuleModifiers globalModuleModifiers = Game.serv.globals.settings.people.businessSettings.globalModuleModifiers;
		Node node = base.Model.visit.building?.components.board.GetNode();
		bool valid;
		return FeedbackUtil.Explain(costMultiplier: globalModuleModifiers.buildCostModifier.Evaluate(base.Model.visit.pid, node, base.Model.visit.building, null), price: cashCost, pid: base.Model.visit.pid, container: base.Model.visit.building, valid: out valid);
	}

	internal void ShowDestroyPopup(ResOrCash item)
	{
		Game.serv.ui.AddPopup(new DestroyResourcePopup(base.Model.visit.building, item.raq.FindResource(), delegate(ResourceAndQty raq)
		{
			ConfirmDestroy(raq);
		}));
	}

	private void ConfirmDestroy(ResourceAndQty raq)
	{
		ModulesUtil.GetInventory(base.Model.visit.building).data.Increment(raq.id, -raq.qty.Abs);
		base.View.ControllerRequestsSubviewRefresh();
	}
}

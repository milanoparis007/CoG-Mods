using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedGambling;

public sealed class OwnedGamblingController : HUDController<OwnedGamblingModel, OwnedGamblingDialog, OwnedGamblingController>
{
	internal void SetModel(VisitState visit)
	{
		base.Model.visit = visit;
	}

	internal void OnBeforeViewHide()
	{
		SetModel(null);
	}

	internal void OnCloseButton()
	{
		Game.ctx.selection.ClearActive();
	}

	internal void OnSwitchCrew(CrewAssignment newCrew)
	{
		base.Model.visit.SetCrew(newCrew);
		base.View.ControllerRequestsFullRefresh();
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
	}

	internal bool CanChangeManager(Entity manager)
	{
		return true;
	}

	public void OnAddAmenityClick()
	{
		Game.serv.ui.AddPopup(new OwnedGamblingAmenityAddPopup(this));
	}

	internal void AddAmenity(AmenityDef def)
	{
		base.Model.visit.GetPlayer().gambling.DoCreateAmenity(def, base.Model.visit, base.Model.Module);
		base.View.ControllerRequestsFullRefresh();
	}

	public void DestroyAmenity(AmenityData amenity)
	{
		base.Model.visit.GetPlayer().gambling.DoDestroyAmenity(amenity, base.Model.Module);
		base.View.ControllerRequestsFullRefresh();
	}

	internal void OnUpgradeButton()
	{
		GamblingModuleConfig gamblingModuleConfig = ModulesUtil.FindModuleDef(base.Model.Module.config.gambling.upgradeModule) as GamblingModuleConfig;
		ModQuery query = base.Model.Module.MakeManagerBasedModQuery(Game.ctx.players.Human, base.Model.visit.building);
		Fixnum size = gamblingModuleConfig.gambling.aoeRadius.Evaluate(query);
		string text = Loc.Get(gamblingModuleConfig.Common.display.locname);
		string text2 = Loc.Get(base.Model.Module.config.common.display.locname);
		string text3 = Loc.Get("ui.ownedcasino.module-upgrade.popup-text.header", "oldModule", text2, "newModule", text);
		string text4 = Loc.Get(gamblingModuleConfig.Common.display.locdesc);
		bool okEnabled = gamblingModuleConfig.gambling.installReqs.AllPass(base.Model.visit, default(ConvoButtonState));
		string text5 = gamblingModuleConfig.gambling.installReqs.Explain(base.Model.visit, default(ConvoButtonState));
		string text6 = Loc.Get("ui.ownedcasino.module-upgrade.popup-text.reqs-explain", "requirements", text5);
		Fixnum value = gamblingModuleConfig.gambling.amenityCount.Evaluate(query);
		string key = Game.serv.globals.settings.gambling.startup.FindSizeLabelFor(size);
		string text7 = Loc.Get("ui.ownedcasino.module-upgrade.popup-text.stats", "amenitySlots", Loc.FormatNumber(value), "aoe", Loc.Get(key));
		string message = text3 + "\n\n" + text4 + "\n\n" + text6 + "\n" + text7;
		Game.serv.ui.AddPopup(new OkCancelPopup(message, Loc.Get("ui.ownedcasino.module-upgrade.popup-text.ok-button"), Loc.Get("ui.ownedcasino.module-upgrade.popup-text.cancel-button"), delegate
		{
			DoUpgrade();
		}, delegate
		{
		}, okEnabled));
	}

	private void DoUpgrade()
	{
		VisitState visit = base.Model.visit;
		visit.GetPlayer().gambling.PayForUpgradeCasino(visit);
		Label upgradeModule = base.Model.Module.config.gambling.upgradeModule;
		List<AmenityData> amenities = base.Model.Module.data.amenities;
		List<Label> ids = new List<Label> { base.Model.Module.config.id };
		visit.building.components.modules.RemoveModules(ids, shutdown: false);
		visit.building.components.modules.InstallModuleManually(upgradeModule, base.Model.visit.time);
		base.Model.Module.data.amenities = amenities;
		base.View.ControllerRequestsFullRefresh();
	}

	public List<AmenityDef> GetInstallableAmenities()
	{
		return (from x in base.Model.Module.config.gambling.amenityIds
			select Game.serv.globals.settings.gambling.FindAmenityById(x) into x
			where x.visreqs.AllPass(base.Model.visit)
			select x).ToList();
	}

	public (Fixnum cash, Fixnum cost, bool canPay) GetInstallCosts(AmenityDef def)
	{
		Fixnum cash = ModulesUtil.GetInventory(base.Model.visit.building).data.money.cash;
		Fixnum item = def.buildCost.Evaluate(base.Model.Module.MakeManagerBasedModQuery(base.Model.visit.GetPlayer(), base.Model.visit.building));
		return new ValueTuple<Fixnum, Fixnum, bool>(item3: cash >= item.Abs, item1: cash, item2: item);
	}

	public bool CanInstall(AmenityDef def)
	{
		bool num = def.visreqs.AllPass(base.Model.visit) && def.reqs.AllPass(base.Model.visit);
		bool item = GetInstallCosts(def).canPay;
		return num && item;
	}

	public string ExplainCanInstall(AmenityDef def)
	{
		string text = def.reqs.Explain(base.Model.visit);
		var (amt, fixnum, valid) = GetInstallCosts(def);
		return (Loc.Get("ui.ownedcasino.requirements") + "\n" + (string.IsNullOrWhiteSpace(text) ? "" : (text + "\n")) + Loc.IconLine(valid, Loc.Get("ui.ownedcasino.costline", "price", Loc.Price(fixnum), "money", Loc.Money(amt)))).Trim();
	}

	public void BanGamblerConvoFromButton(EntityID selected)
	{
		ConvoDataGamblingCollect startDat = new ConvoDataGamblingCollect(new Price(0), new Money(0), new Money(0), new Money(0), base.Model.visit.building.Id, selected);
		VisitState visit = base.Model.visit;
		visit.npc = BuildingUtil.FindOwnerOrManagerForAnyBuilding(visit.building);
		PlayerInfo otherPlayer = base.Model.visit.building.data.building.controlled.pid.FindPlayer();
		Game.ctx.hud.convoDialog.Controller.SpecialStartConvoHelper(ConversationConstants.GAMBLING_BAN_CONFIRM, visit, otherPlayer, ConversationModel.Source.ControlledGambling, requireCrew: false, startDat);
	}

	internal void OnSwitchBuildingButton(int delta)
	{
		List<EntityID> allControlledBuildingsUnsafe = Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe();
		int num = allControlledBuildingsUnsafe.FindIndex((EntityID x) => x == base.Model.visit.building.Id);
		Game.ctx.selection.SetActive(allControlledBuildingsUnsafe[num + delta].FindEntity());
	}

	internal void OpenGamblerFamily(EntityID gamblerId)
	{
		Entity building = base.Model.visit.building;
		Game.ctx.selection.ClearActive();
		Game.ctx.hud.personInfo.Show(gamblerId.FindEntity(), building);
	}
}

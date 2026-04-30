using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session.HUD;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

internal sealed class ViewDescribeModule : SubviewEntry
{
	internal class ManagerButtonContext : MonoBehaviour
	{
		public PlayerID pid;

		public EntityID manager;

		public void Set(PlayerID pid, EntityID manager)
		{
			this.pid = pid;
			this.manager = manager;
		}
	}

	internal class ExpansionButtonContext : MonoBehaviour
	{
		public ModuleExpansionConfig exp;

		public void Set(ModuleExpansionConfig exp)
		{
			this.exp = exp;
		}
	}

	private class ExpansionDesc : ItemListPopup<ExpansionDesc>.IEntry
	{
		public ModuleExpansionConfig config;

		public string icon;

		public string name;

		public string describe;

		public string explainReqs;

		public string explainCost;

		public bool passReqs;

		public bool passCost;

		public ExpansionDesc(ModuleExpansionConfig config, OwnedBizController ctrl)
		{
			VisitState visit = ctrl.Model.visit;
			this.config = config;
			icon = config.display.GetLocIcon();
			name = config.display.GetLocName();
			describe = config.display.GetLocInstall();
			explainReqs = Loc.Get("ui.viewdescribemodule.expansion.desc.req") + (config.reqs?.Explain(visit) ?? Loc.Get("ui.viewdescribemodule.expansion.desc.req.none"));
			explainCost = Loc.Get("ui.viewdescribemodule.expansion.desc.cost") + ctrl.ExplainCosts(config);
			passReqs = config.reqs?.AllPass(visit) ?? true;
			passCost = ctrl.CanPlayerAffordCosts(config);
		}

		public string GetIcon()
		{
			return icon;
		}

		public string GetName()
		{
			return name;
		}

		public string GetDescription()
		{
			return Loc.Get("ui.viewdescribemodule.expansion.desc.format", "name", name, "describe", describe, "explainReqs", explainReqs, "explainCost", explainCost);
		}

		public string GetDebug()
		{
			return name;
		}

		public bool GetIsAvailable()
		{
			if (passReqs)
			{
				return passCost;
			}
			return false;
		}
	}

	private const string TEMPLATES = "Templates";

	private const string TMPL_DESCPANEL = "Templates/Module Desc Panel";

	private const string DESC_CONTAINER = "Detail View/Viewport/Content/";

	private const string MODULE_BG = "Module BG";

	private const string UPGRADE_TEXT = "Upgrade/Text";

	private const string UPGRADE_BTN = "Upgrade/Manage";

	private const string UPGRADE_BTN_TEXT = "Upgrade/Manage/Text";

	private const string EXPANSION_TEXT = "Expansions/Text";

	private const string EXPANSION_BTN_PREFIX = "Expansions/Manage_";

	private const string DESTROY_CONTAINER = "Destroy";

	private const string DESTROY_TEXT = "Destroy/Text";

	private const string DESTROY_BTN = "Destroy/Button";

	private const string DESTROY_BTN_TEXT = "Destroy/Button/Text";

	private const string OP_HEADER = "Manager/Title";

	private const string OP_PORTRAIT_BUTTON = "Manager/Button";

	private const string OP_PORTRAIT_FRAME = "Manager/Portrait";

	private const string OP_PORTRAIT_IMAGE = "Manager/Portrait/Portrait";

	private const string OP_NAME = "Manager/Name";

	private const string OP_REL = "Manager/Rel";

	private const string OP_INFO = "Manager/Info";

	public static readonly Label INVENTORY_UPGRADE_MODULE = (Label)"inventory-basement-improved";

	public static readonly Label INVENTORY_EXPANSION_BACKROOM = (Label)"explanation-inventory-improved";

	private static readonly string[] EXP_BUTTONS = new string[3] { "Expansions/Manage_0", "Expansions/Manage_1", "Expansions/Manage_2" };

	public ViewDescribeModule(ViewType type, GameObject go)
		: base(type, go)
	{
	}

	private GameObject GetDescContainer()
	{
		return go.GetChild("Detail View/Viewport/Content/");
	}

	public override void RefreshSubview()
	{
		RefreshBackground();
		RefreshManager();
		RefreshDescription();
		RefreshUpgrade();
		RefreshDestroy();
		RefreshExpansions();
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOwnedBizDescribeModule, PlayerID.HumanPlayer);
	}

	private void RefreshBackground()
	{
		ModuleCommon common = Model.currentSlot.module?.ModuleConfig.Common;
		Entity building = Model.visit.building;
		ModulesUIUtil.RefreshModuleBackground(go.GetChild("Module BG"), building, common);
	}

	private void RefreshManager()
	{
		Entity manager = ModulesUtil.MakeModuleQuery(Model.visit.building).manager;
		go.SetActive("Manager/Portrait", manager != null);
		bool flag = Controller.CanChangeManager(manager);
		go.SetText("Manager/Title", Loc.Get("ui.crewmgmt.manager", "name", "").Trim());
		go.GetButton("Manager/Button").interactable = flag;
		go.GetChild("Manager/Button").GetOrAddComponent<ManagerButtonContext>().Set(PlayerID.HumanPlayer, manager?.Id ?? EntityID.INVALID);
		go.GetChild("Manager/Button").SetChildText(TextUtil.ColorEnabledIf(flag, "+"));
		go.SetImage("Manager/Portrait/Portrait", HUDUtil.GetCrewSprite(manager));
		go.SetText("Manager/Name", FindManagerDescription(manager));
		go.SetText("Manager/Rel", "");
		go.SetActive("Manager/Info", manager != null);
		go.SetButtonListener("Manager/Info", delegate
		{
			Controller.OnManagerInfoButton(manager);
		});
		go.SetButtonListener("Manager/Button", delegate
		{
			Controller.OnManagerEditButton();
		});
	}

	private string FindManagerDescription(Entity npc)
	{
		if (npc == null)
		{
			return Loc.Get("module.mgmt.none");
		}
		return NameUtils.GetPeepFullName(npc);
	}

	private void RefreshDescription()
	{
		GetDescContainer().transform.DestroyAllChildren();
		RefreshDetails(Model.currentSlot.module);
	}

	internal static string GetModuleHeader(bool isFrontRoom)
	{
		string key = (isFrontRoom ? "ui.viewdescribemodule.opdetails.front" : "ui.viewdescribemodule.opdetails.back");
		return Loc.Get("ui.viewdescribemodule.opdetails.header", "info", Loc.Get(key));
	}

	private void RefreshDetails(IModule module)
	{
		ModuleDescPanelBuilder moduleDescPanelBuilder = new ModuleDescPanelBuilder(Dialog.GetTmpl("Templates/Module Desc Panel"), GetDescContainer(), Model.visit);
		bool isFrontRoom = Model.currentSlot.common.tags?.Contains(TagConstants.TAG_SAFEHOUSE_FRONTROOMS) ?? false;
		moduleDescPanelBuilder.AddTextEntry(GetModuleHeader(isFrontRoom));
		if (module.ModuleConfig != null)
		{
			InventoryModule inventory = ModulesUtil.GetInventory(Model.visit.building);
			moduleDescPanelBuilder.DescribeInstalledModule(Model.visit.building, module, inventory.data);
		}
	}

	private void RefreshUpgrade()
	{
		IModule module = Model.currentSlot.module;
		List<AddModuleDef> upgrades = ModulesUtil.FindUpgradesOrNull(module, Model.visit, Game.ctx.clock.Now);
		bool flag = upgrades != null && upgrades.Count > 0;
		string message = (flag ? Loc.Get("ui.viewdescribemodule.upgrade.available") : Loc.Get("ui.viewdescribemodule.upgrade.unavailable"));
		go.SetText("Upgrade/Text", TextUtil.ColorEnabledIf(flag, message));
		go.GetButton("Upgrade/Manage").interactable = flag;
		go.SetText("Upgrade/Manage/Text", TextUtil.ColorEnabledIf(flag, Loc.Get("ui.icon.gear")));
		if (flag)
		{
			go.GetButton("Upgrade/Manage").onClick.SetListener(delegate
			{
				SwitchToUpgrade(module, upgrades);
			});
		}
	}

	private void SwitchToUpgrade(IModule module, List<AddModuleDef> upgrades)
	{
		Controller.OnUpgradeClick(module, upgrades);
	}

	private void RefreshDestroy()
	{
		bool isModuleInstalled = Model.currentSlot.IsModuleInstalled;
		bool active = Model.currentSlot.slotdef == Model.visit.building.components.modules.FindBackroomModuleSlot() && Model.visit.building.components.modules.FindBackroomModule().ModuleConfig.Id != INVENTORY_EXPANSION_BACKROOM;
		go.GetChild("Destroy").SetActive(active);
		string message = (isModuleInstalled ? Loc.Get("ui.viewdescribemodule.destroy.available") : Loc.Get("ui.viewdescribemodule.destroy.unavailable"));
		go.SetText("Destroy/Text", TextUtil.ColorEnabledIf(isModuleInstalled, message));
		go.GetButton("Destroy/Button").interactable = isModuleInstalled;
		go.SetText("Destroy/Button/Text", TextUtil.ColorEnabledIf(isModuleInstalled, Loc.Get("ui.icon.destroy")));
		if (isModuleInstalled)
		{
			go.GetButton("Destroy/Button").onClick.SetListener(OnDestroyModuleButtonClick);
		}
	}

	private void OnDestroyModuleButtonClick()
	{
		Controller.OnDestroyModuleButtonClick();
	}

	private void RefreshExpansions()
	{
		OwnedBizController.ExpansionInfo expansions = Controller.GetExpansions(Model.currentSlot.module);
		string message = (expansions.CanInstallAny ? Loc.Get("ui.viewdescribemodule.expansion.available") : (expansions.NoneAvailable ? Loc.Get("ui.viewdescribemodule.expansion.unavailable") : Loc.Get("ui.viewdescribemodule.expansion.default")));
		go.SetText("Expansions/Text", TextUtil.ColorDisabledIf(expansions.NoneAvailable, message));
		List<GameObject> list = EXP_BUTTONS.Select((string name) => go.GetChild(name)).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			GameObject card = list[num];
			bool isVisible = num < expansions.countMax;
			bool flag = num < expansions.alreadyInstalled.Count;
			ModuleExpansionConfig moduleExpansionConfig = (flag ? expansions.alreadyInstalled[num] : null);
			string icon = (flag ? moduleExpansionConfig.display.GetLocIcon() : Loc.Get("ui.viewdescribemodule.expansion.icon"));
			InitExpansionButton(card, moduleExpansionConfig, isVisible, flag, icon);
		}
	}

	private void InitExpansionButton(GameObject card, ModuleExpansionConfig expansion, bool isVisible, bool isInstalled, string icon)
	{
		card.SetActive(isVisible);
		card.GetOrAddComponent<ExpansionButtonContext>().Set(expansion);
		card.SetChildText(icon);
		card.GetButton().onClick.SetListener(delegate
		{
			OnExpansionClick(card, isInstalled);
		});
	}

	private void OnExpansionClick(GameObject card, bool isInstalled)
	{
		if (isInstalled)
		{
			ModuleExpansionConfig exp = card.GetComponent<ExpansionButtonContext>().exp;
			OkCancelPopup popup = new OkCancelPopup(Loc.Get("ui.viewdescribemodule.expansion.delete.confirm"), delegate
			{
				RemoveExpansion(exp);
			}, delegate
			{
			});
			Game.serv.ui.AddPopup(popup);
		}
		else
		{
			List<ModuleExpansionConfig> configs = Model.currentSlot.module.FindExpansionsToOffer(Model.visit);
			ShowExpansionsDialog(configs);
		}
	}

	private void RemoveExpansion(ModuleExpansionConfig exp)
	{
		if (Controller.GetExpansions(Model.currentSlot.module).alreadyInstalled.Contains(exp))
		{
			Controller.RemoveExpansion(Model.currentSlot.module, exp);
		}
		RefreshSubview();
	}

	private void ShowExpansionsDialog(List<ModuleExpansionConfig> configs)
	{
		Model.currentSlot.module.FindExpansionsToOffer(Model.visit);
		string title = Loc.Get("ui.viewdescribemodule.expansion.select");
		string desc = Loc.Get("ui.viewdescribemodule.expansion.select.header");
		List<ExpansionDesc> entries = configs.SelectIntoNewList((ModuleExpansionConfig config) => new ExpansionDesc(config, Controller));
		Game.serv.ui.AddPopup(new ItemListPopup<ExpansionDesc>(title, desc, entries, OnSelected));
	}

	private void OnSelected(ExpansionDesc item)
	{
		IModule module = Model.currentSlot.module;
		Controller.InstallExpansion(module, item.config);
		RefreshSubview();
	}
}

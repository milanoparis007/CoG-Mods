using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedBiz;

public class OwnedBizAddModulePopup : BasePopup
{
	private const string TEMPLATES = "Templates";

	private const string TMPL_DESCPANEL = "Templates/Module Desc Panel";

	private const string TMPL_MCARD = "Templates/Module Card";

	private const string TITLE = "Panel/Title";

	private const string NAME = "Panel/Header";

	private const string B_CLOSE = "Panel/Close Button";

	private const string B_INSTALL = "Panel/Footer/OK";

	private const string B_CANCEL = "Panel/Footer/Cancel";

	private const string MCARD_LIST = "Panel/Module List/Viewport/Content";

	private const string DESC_LIST = "Panel/Description List/Viewport/Content";

	private OwnedBizController _controller;

	private ModuleAddButtonContext _selected;

	private Label _previous;

	private GameObject _tmplModuleCard;

	private GameObject _tmplDescPanel;

	private GameObject _mcardContainer;

	private GameObject _descContainer;

	private ModuleDescPanelBuilder _descBuilder;

	private UpgradeContext _upgradeCtx;

	private const string CARD_ICON = "Icon";

	private const string CARD_DESC = "Description";

	public override UIReference UIReference => UIElements.OwnedBizModulePopup;

	public bool IsUpgrade => _previous.IsSet;

	public OwnedBizAddModulePopup(OwnedBizController ownedBizController, List<IModuleConfig> upgrades = null)
	{
		_controller = ownedBizController;
		_upgradeCtx = null;
		_previous = default(Label);
		SetUpgrade(upgrades);
	}

	private void SetUpgrade(List<IModuleConfig> upgrades)
	{
		if (upgrades != null && upgrades.Count > 0)
		{
			_upgradeCtx = new UpgradeContext
			{
				permitted = upgrades
			};
			_previous = ((_upgradeCtx == null) ? Label.NULL : _controller.Model.currentSlot.module.ModuleConfig.Id);
		}
	}

	protected override void InitializeOnPush()
	{
		_go.SetActive("Templates", value: false);
		_tmplModuleCard = _go.GetChild("Templates/Module Card");
		_tmplDescPanel = _go.GetChild("Templates/Module Desc Panel");
		_go.SetText("Panel/Title", Loc.Get("module.addpopup.back"));
		if (_previous.IsSet)
		{
			_go.SetText("Panel/Header", Loc.Get("module.addpopup.upgrade"));
		}
		else
		{
			_go.SetText("Panel/Header", Loc.Get("module.addpopup.build-confirm"));
		}
		_go.SetButtonListener("Panel/Footer/OK", OnConfirm);
		_go.SetButtonListener("Panel/Footer/Cancel", OnCancel);
		_go.SetButtonListener("Panel/Close Button", OnCancel);
		_descContainer = _go.GetChild("Panel/Description List/Viewport/Content");
		_descContainer.DestroyAllChildren();
		_mcardContainer = _go.GetChild("Panel/Module List/Viewport/Content");
		_mcardContainer.GetComponent<ToggleGroup>().allowSwitchOff = false;
		_descBuilder = new ModuleDescPanelBuilder(_tmplDescPanel, _descContainer, _controller.Model.visit);
		RefreshCards();
		RefreshDetails(null, shutdown: false);
		RefreshButtons();
	}

	protected override void ReleaseOnPop()
	{
		RefreshDetails(null, shutdown: true);
		_descContainer = (_mcardContainer = (_tmplModuleCard = (_tmplDescPanel = null)));
	}

	private void OnCancel()
	{
		Close();
	}

	private void OnConfirm()
	{
		OkCancelPopup popup = MakeConfirmPopup(_controller, _selected, _previous);
		Close();
		Game.serv.ui.AddPopup(popup);
	}

	private static OkCancelPopup MakeConfirmPopup(OwnedBizController controller, ModuleAddButtonContext selected, Label previous)
	{
		return new OkCancelPopup(DescribeModule(controller, selected, previous), delegate
		{
			controller.OnModuleAddConfirm(selected.addmoduledef, previous, selected.slotdef);
		}, delegate
		{
		});
	}

	private static string DescribeModule(OwnedBizController controller, ModuleAddButtonContext selected, Label previous)
	{
		AddModuleDef addmoduledef = selected.addmoduledef;
		string text = Loc.Get(addmoduledef.config.Common.display.locname);
		string text2 = Loc.Get("module.add.confirm.name", "name", text);
		if (previous.IsSet)
		{
			string text3 = Loc.Get(controller.Model.currentSlot.module.ModuleConfig.Common.display.locname);
			text2 += Loc.Get("module.add.confirm.upgrade", "existing", text3);
			text2 += Loc.Get("module.add.confirm.upgrade.perform");
		}
		else
		{
			text2 += Loc.Get("module.add.confirm.new.install");
		}
		int num = addmoduledef.config.Common.purchase.FindInstallDays(PlayerID.HumanPlayer, controller.Model.visit.building);
		int num2 = Game.ctx.clock.DaysToTurnsRoundedUp(num);
		string message = Loc.Get("module.add.daysmsg", "days", num, "turns", num2);
		return text2 + TextUtil.ColorWrap(message, ColorConstants.TEXT_HEX_CONSTRUCTION);
	}

	public void RefreshCards()
	{
		List<AddModuleDef> list = _controller.FindModulesToAddForSlot(_upgradeCtx);
		_mcardContainer.EnsureChildCount(list.Count, _tmplModuleCard);
		_mcardContainer.InitializeChildren(list, InitializeCard);
	}

	public void RefreshButtons()
	{
		bool interactable = _selected != null && _selected.canInstall;
		_go.GetButton("Panel/Footer/OK").interactable = interactable;
	}

	private void InitializeCard(int index, GameObject card, AddModuleDef def)
	{
		OwnedBizModel model = _controller.Model;
		ModuleSlot slotdef = model.currentSlot.slotdef;
		bool canInstall = _controller.CanInstall(def);
		string item = _controller.ExplainCosts(def.config).explanation;
		card.GetOrAddComponent<ModuleAddButtonContext>().Set(slotdef, def, model.visit.MakeOwnerModQuery(), canInstall, item);
		card.SetImage("Icon", ModulesUIUtil.FindModuleIcon(def.config.Common));
		card.SetText("Description", ModulesUIUtil.FindModuleName(def.config.Common));
		Toggle componentInChildren = card.GetComponentInChildren<Toggle>();
		componentInChildren.group = _mcardContainer.GetComponent<ToggleGroup>();
		componentInChildren.SetIsOnWithoutNotify(value: false);
		componentInChildren.onValueChanged.SetListener(delegate(bool isOn)
		{
			if (isOn)
			{
				OnCardClick(card);
			}
		});
	}

	private void OnCardClick(GameObject card)
	{
		ModuleAddButtonContext component = card.GetComponent<ModuleAddButtonContext>();
		RefreshDetails(component, shutdown: false);
		RefreshButtons();
	}

	private void RefreshDetails(ModuleAddButtonContext ctx, bool shutdown)
	{
		_selected = ctx;
		_descBuilder.ClearContainer();
		if (!shutdown)
		{
			IModuleConfig moduleConfig = ((ctx != null) ? ctx.addmoduledef.config : null);
			if (moduleConfig == null)
			{
				string text = Loc.Get("module.add.default");
				_descBuilder.AddTextEntry(text);
			}
			else
			{
				_descBuilder.DescribeAddModule(moduleConfig);
			}
		}
	}
}

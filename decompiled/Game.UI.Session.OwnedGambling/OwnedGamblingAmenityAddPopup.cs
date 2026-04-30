using System.Collections.Generic;
using Game.Services;
using Game.Session.Sim.Modules;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedGambling;

public class OwnedGamblingAmenityAddPopup : BasePopup
{
	private const string TEMPLATES = "Templates";

	private const string TMPL_MCARD = "Templates/Module Card";

	private const string TITLE = "Panel/Title";

	private const string NAME = "Panel/Header";

	private const string B_CLOSE = "Panel/Close Button";

	private const string B_INSTALL = "Panel/Footer/OK";

	private const string B_CANCEL = "Panel/Footer/Cancel";

	private const string BANNER = "Panel/Description List/Banner";

	private const string MCARD_LIST = "Panel/Module List/Viewport/Content";

	private const string DESC_LIST = "Panel/Description List/Viewport/Content";

	private const string DESC_TEXT = "Panel/Description List/Viewport/Content/Text";

	private OwnedGamblingController _controller;

	private AmenityAddButtonCtx _selected;

	private GameObject _tmplModuleCard;

	private GameObject _mcardContainer;

	private const string CARD_ICON = "Icon";

	private const string CARD_DESC = "Description";

	public override UIReference UIReference => UIElements.OwnedGamblingAmenityPopup;

	public OwnedGamblingAmenityAddPopup(OwnedGamblingController ownedBizController)
	{
		_controller = ownedBizController;
	}

	protected override void InitializeOnPush()
	{
		_go.SetActive("Templates", value: false);
		_tmplModuleCard = _go.GetChild("Templates/Module Card");
		_go.SetText("Panel/Title", Loc.Get("ui.ownedcasino.add-popup"));
		_go.SetText("Panel/Header", Loc.Get("ui.ownedcasino.add-desc"));
		_go.SetButtonListener("Panel/Footer/OK", OnConfirm);
		_go.SetButtonListener("Panel/Footer/Cancel", OnCancel);
		_go.SetButtonListener("Panel/Close Button", OnCancel);
		_mcardContainer = _go.GetChild("Panel/Module List/Viewport/Content");
		_mcardContainer.GetComponent<ToggleGroup>().allowSwitchOff = false;
		RefreshCards();
		RefreshDetails(null, shutdown: false);
		RefreshButtons();
	}

	protected override void ReleaseOnPop()
	{
		RefreshDetails(null, shutdown: true);
		_mcardContainer = (_tmplModuleCard = null);
	}

	private void OnCancel()
	{
		Close();
	}

	private void OnConfirm()
	{
		OkCancelPopup popup = MakeConfirmPopup(_controller, _selected);
		Close();
		Game.serv.ui.AddPopup(popup);
	}

	private static OkCancelPopup MakeConfirmPopup(OwnedGamblingController controller, AmenityAddButtonCtx selected)
	{
		return new OkCancelPopup(Loc.Get("ui.ownedcasino.add-confirm", "amenity", Loc.Get(selected.def.locname), "module", Loc.Get(controller.Model.Module.config.common.display.locname)), delegate
		{
			controller.AddAmenity(selected.def);
		}, delegate
		{
		});
	}

	public void RefreshCards()
	{
		List<AmenityDef> installableAmenities = _controller.GetInstallableAmenities();
		_mcardContainer.EnsureChildCount(installableAmenities.Count, _tmplModuleCard);
		_mcardContainer.InitializeChildren(installableAmenities, InitializeCard);
	}

	public void RefreshButtons()
	{
		bool interactable = _selected != null && _selected.canInstall;
		_go.GetButton("Panel/Footer/OK").interactable = interactable;
	}

	private void InitializeCard(int index, GameObject card, AmenityDef def)
	{
		_ = _controller.Model;
		bool canInstall = _controller.CanInstall(def);
		card.SetText("Icon", Loc.Get(def.locicon));
		card.SetText("Description", Loc.Get(def.locname));
		card.GetOrAddComponent<AmenityAddButtonCtx>().Set(def, canInstall);
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
		AmenityAddButtonCtx component = card.GetComponent<AmenityAddButtonCtx>();
		RefreshDetails(component, shutdown: false);
		RefreshButtons();
	}

	private void RefreshDetails(AmenityAddButtonCtx ctx, bool shutdown)
	{
		_selected = ctx;
		ClearDescription();
		SetBannerImage(null);
		if (!shutdown)
		{
			if (ctx != null)
			{
				SetDescription(ctx);
				SetBannerImage(ctx.def.banner);
			}
			GameObject child = _go.GetChild("Panel/Description List/Viewport/Content");
			child.ForceRebuildLayoutImmediate();
			child.ForceRebuildLayoutImmediate();
			child.ForceRebuildLayoutImmediate();
		}
	}

	private void ClearDescription()
	{
		_go.SetText("Panel/Description List/Viewport/Content/Text", "");
		_go.GetChild("Panel/Description List/Banner").SetActive(value: false);
	}

	private void SetDescription(AmenityAddButtonCtx ctx)
	{
		AmenityDef def = ctx.def;
		string text = Loc.Get(def.locname);
		(string, string) tuple = Game.ctx.players.Human.gambling.DescribeAmenity(default(AmenityData.LastTurnResults), ctx.def, _controller.Model.Module, _controller.Model.visit, useLastTurn: false);
		string text2 = tuple.Item1 + tuple.Item2;
		string text3 = _controller.ExplainCanInstall(def);
		string text4 = Loc.Get("ui.ownedcasino.text-format", "title", text, "desc", text2, "reqs", text3);
		_go.SetText("Panel/Description List/Viewport/Content/Text", text4);
	}

	private void SetBannerImage(string path)
	{
		GameObject child = _go.GetChild("Panel/Description List/Banner");
		Sprite sprite = ModulesUIUtil.FindLargeBannerOrNull(path);
		if (sprite != null)
		{
			_go.SetImage("Panel/Description List/Banner", sprite);
			child.SetActive(value: true);
		}
		else
		{
			child.SetActive(value: false);
		}
	}
}

using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.Commands;
using Game.UI.Mouseovers;
using Game.UI.Session.Crew;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class BuildingPick : BasePick
{
	private const string BUTTON = "Button";

	private const string PIP_TAB = "Pips";

	private const string PIP_TEXT = "Pips/Text";

	private const string WARNINGS_CONTAINER = "Button/Warnings";

	private const string PIP_TICKET = "Button/Warnings/Ticket Pip";

	private const string WARNING_UNLOCK = "Button/Warnings/Resource Pip";

	private const string BG_IMAGE = "Button/BG Image";

	private const string BG_UNSCOPED_ICON = "Button/BG Unscoped Icon";

	private const string BG_OWNED_BACKMODULE = "Button/Module Icon";

	private const string OVERLAY_BACK = "Button/BG Overlay";

	private const string OVERLAY_FRONT = "Button/FG Overlay";

	private const string BORDER_SAFEHOUSE = "Button/Border Safehouse";

	private const string BORDER_POLICE = "Button/Border Police";

	private const string FLARE_GAMBLING = "Button/Flare Gambling";

	private const string FLARE_RESEVENT = "Button/Flare Res Event";

	private const string HEALTH_CONTAINER = "Button/Bar Panel";

	private const string HEALTH_BAR = "Button/Bar Panel/Bar";

	private const string TEXT = "Button/Text";

	private const float BUTTON_CLOSED_Y = 25f;

	private const float BUTTON_OPEN_Y = 44f;

	private const float BAR_ASSET_MAX_WIDTH = 44f;

	private BuildingAndBusinessData _bbd;

	private BuildingPickData _pickdata;

	public override PickType Type => PickType.BuildingPick;

	public bool IsScoped => _pickdata.scoped;

	public bool HasInfoPips => _pickdata.showpips;

	public bool HasTickets => _pickdata.tickets > 0;

	protected override bool CanShowPick()
	{
		if (base.CanShowPick())
		{
			return base.Target.FindEntity()?.components.building?.IsPickSuppressed != true;
		}
		return false;
	}

	public override void RefreshContents()
	{
		_bbd = BuildingUtil.FindDataForBuilding(base.Target.FindEntity());
		_pickdata = BuildingPickData.GenerateBuildingButtonData(_bbd);
		go.SetImageOrHide("Button/Module Icon", _pickdata.ownedBuildingIcon);
		bool value = _pickdata.scoped && _pickdata.ownedBuildingIcon == null;
		go.SetText("Button/Text", _pickdata.icon);
		go.SetActive("Button/Text", value);
		go.SetActive("Button/BG Unscoped Icon", !_pickdata.scoped);
		go.GetChild("Button/BG Image").GetImage().color = _pickdata.color;
		go.GetButton("Button").interactable = _pickdata.interactable;
		ShowHideOverlay(!_pickdata.canCrewInteract);
		bool value2 = _pickdata.scoped && _pickdata.humanControlled;
		go.SetActive("Button/Border Safehouse", value2);
		go.SetActive("Button/Flare Gambling", _pickdata.showGamblingFlare);
		go.SetActive("Button/Flare Res Event", _pickdata.showResidentialFlare);
		bool value3 = _pickdata.scoped && _pickdata.policeStation;
		go.SetActive("Button/Border Police", value3);
		MaybeShowWarnings(_pickdata);
		RefreshPips(_pickdata);
		RefreshHealthBar();
	}

	private void RefreshHealthBar()
	{
		BuildingSettings.HealthInfo healthInfo = Game.serv.globals.settings.people.buildingSettings.FindHealthInfo(_pickdata.healthCurrent);
		bool flag = healthInfo.showbar && _pickdata.HasDamage;
		go.SetActive("Button/Bar Panel", flag);
		if (flag)
		{
			Fixnum fixnum = _pickdata.healthCurrent / _pickdata.healthMax;
			go.GetChild("Button/Bar Panel/Bar").SetUIElementWidth((float)fixnum * 44f);
			go.GetImage("Button/Bar Panel/Bar").color = healthInfo.color;
		}
	}

	private void MaybeShowWarnings(BuildingPickData data)
	{
		bool hasWarningsToShow = data.HasWarningsToShow;
		go.SetActive("Button/Warnings", hasWarningsToShow);
		if (hasWarningsToShow)
		{
			bool flag = data.tickets > 0 && !base.Target.FindEntity().components.building.IsSafehouseOrControlledNotBy(PlayerID.HumanPlayer);
			go.SetActive("Button/Warnings/Ticket Pip", flag);
			if (flag)
			{
				string sourceText = Loc.Get("ui.ticket");
				go.GetChild("Button/Warnings/Ticket Pip").GetChildText().SetText(sourceText);
			}
			go.SetActive("Button/Warnings/Resource Pip", data.resUnlocked != null);
			if (data.resUnlocked != null)
			{
				go.GetChild("Button/Warnings/Resource Pip").SetChildText(data.resUnlocked.GetIcon());
			}
		}
	}

	private void RefreshPips(BuildingPickData data)
	{
		string text = BasePickUtil.GeneratePipIcons(data.showpips, data.pips);
		go.SetText("Pips/Text", text);
		bool flag = !string.IsNullOrWhiteSpace(text);
		go.SetActive("Pips", flag);
		go.GetChild("Button").SetUIElementY(flag ? 44f : 25f);
	}

	public override void OnClick()
	{
		Game.serv.mouseovers.OnMouseOut(MouseoverType.BuildingPickMouseover);
		if (_pickdata.scopable)
		{
			TryScopeOut(base.Target.FindEntity());
		}
		else
		{
			Game.ctx.selection.SetActive(base.Target.FindEntity());
		}
	}

	internal void OnHighlight()
	{
		BlinkOverlay();
	}

	private void BlinkOverlay()
	{
		Blink("Button/BG Overlay", 1f);
		Blink("Button/FG Overlay", 1f);
		void Blink(string name, float t)
		{
			GameObject overlay = go.GetChild(name);
			LeanTween.value(overlay, delegate(float f)
			{
				overlay.SetImageAlpha(f);
			}, 0f, 1f, t).setEase(LeanTweenType.easeInOutSine);
		}
	}

	private void ShowHideOverlay(bool show)
	{
		Cancel("Button/BG Overlay");
		Cancel("Button/FG Overlay");
		void Cancel(string name)
		{
			GameObject child = go.GetChild(name);
			LeanTween.cancel(child);
			child.SetImageAlpha(1f);
			child.SetActive(show);
		}
	}

	private void TryScopeOut(Entity entity)
	{
		EntitySelectionPopup.ShowCrewSelector(entity.components.board.GetNode().id, Loc.Get("ui.entityselection.scopeout"), delegate(EntityID peepId)
		{
			Continue(Game.ctx.players.Human.crew.GetCrewForPeep(peepId));
		});
		void Continue(CrewAssignment crew)
		{
			CommandButtonScopeOut commandButtonScopeOut = HumanCommandValidator.FindValidator(CommandType.ScopeOut) as CommandButtonScopeOut;
			CommandStatus commandStatus = commandButtonScopeOut.Validate(PlayerID.HumanPlayer, crew);
			if (commandStatus.IsEnabled)
			{
				commandButtonScopeOut.OnHumanButtonClick(crew, entity);
			}
			else if (commandStatus.status == CommandEnabledStatus.DisabledOther)
			{
				string text = TextUtil.ColorWrap(Loc.Get("ui.actioncost.fail"), ColorConstants.TEXT_HEX_RED);
				Game.ctx.hud.flyouts.MakeSimpleTextFlyout(entity.data.board.worldpos, text, 3f);
				Game.ctx.sfx.PlayOutOfPoints(moves: false, actions: true);
				Game.ctx.events.EnqueueOnce(SessionEventType.UIInsufficientActionPoints, PlayerID.HumanPlayer);
			}
			else
			{
				OkPopup.Show(crew.peepId, commandStatus.mouseover);
			}
		}
	}

	public override Vector3 MakeSceneVector()
	{
		float y = _bbd.building?.config.board.colliderHeight ?? 1f;
		return base.MakeSceneVector().SetY(y);
	}

	public override string MakeMouseoverMessage()
	{
		return BuildingPickUtil.MakeMouseover(base.Target.FindEntity(), _pickdata);
	}
}

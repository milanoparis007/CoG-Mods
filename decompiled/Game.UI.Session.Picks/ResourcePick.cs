using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Overlays;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.UI.Session.OwnedBiz;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class ResourcePick : BasePick
{
	private const string BUTTON = "Button";

	private const string STATE_ICON = "State Icon";

	private const string BG_OVERLAY = "BG Overlay";

	private const string TEXT_ICON = "Text";

	private const string MODULE_ICON = "Module Icon";

	private const string MULTI_BUTTON = "Multi";

	private const float PICK_Y_HEIGHT = 1f;

	private OverlayResDir _desc;

	private GameObject _parent;

	public override PickType Type => PickType.ResourcePick;

	public override void SetTarget(PickTarget target)
	{
		base.SetTarget(target);
		_desc = Game.ctx.overlays.FindResourceOverlayForBuilding(target.FindEntity());
		go.GetButton("Multi").onClick.SetListener(delegate
		{
			OnClick();
		});
	}

	public override void RefreshContents()
	{
		if (_desc.resources.Count > 1)
		{
			_parent = go.GetChild("Multi");
			go.GetChild("Button").SetActive(value: false);
			go.GetButton("Multi").interactable = true;
		}
		else
		{
			_parent = go.GetChild("Button");
			go.GetChild("Multi").SetActive(value: false);
			go.GetButton("Button").interactable = true;
		}
		_parent.SetActive(value: true);
		var (sprite, text) = GetPickIcons(_desc);
		if (sprite != null)
		{
			text = null;
		}
		_parent.GetImage("BG Overlay").color = _desc.GetPickColor();
		_parent.SetImageOrHide("Module Icon", sprite);
		_parent.SetTextOrHide("Text", text);
		RefreshInfoIcon();
	}

	private static (Sprite moduleIcon, string textIcon) GetPickIcons(OverlayResDir e)
	{
		string text = ((e.pickIcon != null) ? e.pickIcon : "");
		if (text == "" && e.resources.Count != 0)
		{
			foreach (EntityResDir resource in e.resources)
			{
				text = text + " " + resource.res.GetIcon();
			}
		}
		Entity entity = e.eid.FindEntity();
		PlayerID? playerID = entity?.components.building?.GetControllingPlayer();
		return (moduleIcon: (playerID.HasValue && playerID.GetValueOrDefault().IsHumanPlayer) ? ModulesUIUtil.FindIconBackModuleOrGambling(entity) : null, textIcon: text);
	}

	private void RefreshInfoIcon()
	{
		PlayerInfo human = Game.ctx.players.Human;
		Entity entity = _desc.eid.FindEntity();
		if (entity.data.police != null)
		{
			bool flag = entity.data.police.copPlayerId.FindPlayer().ai.precinct.HasDonationFrom(human.PID) == DonationState.PaidOff;
			bool flag2 = entity.data.police.copPlayerId.FindPlayer().ai.precinct.HasDonationFrom(human.PID) == DonationState.WaitingForRefresh;
			bool flag3 = human.territory.IsScoped(entity);
			go.SetText("State Icon", (flag && flag3) ? Loc.Get("ui.copicon.paidyes") : ((flag2 && flag3) ? Loc.Get("ui.copicon.waiting") : ""));
		}
		else if (IsInDeliveryOverlay())
		{
			go.SetText("State Icon", (Game.ctx.hud.deliveries.CurrentDest() == entity) ? Loc.Get("ui.check") : Loc.Get("ui.command.cancel.icon"));
		}
		else
		{
			go.SetText("State Icon", _desc.GetPickBuySell());
		}
	}

	public override string MakeMouseoverMessage()
	{
		if (!_desc.IsValid)
		{
			return null;
		}
		return _desc.GetPickMouseover();
	}

	public override void OnClick()
	{
		Entity building = _desc.eid.FindEntity();
		if (IsInDeliveryOverlay())
		{
			Game.ctx.hud.deliveries.EditDest(building);
		}
		else
		{
			Game.ctx.selection.SetActive(base.Target.FindEntity());
		}
	}

	private bool IsInDeliveryOverlay()
	{
		return Game.ctx.overlays.resources.mode == OverlayManager.ResourceState.Mode.Deliveries;
	}

	public override Vector3 MakeSceneVector()
	{
		return base.MakeSceneVector().SetY(1f);
	}
}

using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Overlays;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class OrderArrowPick : BasePick
{
	private const string BUTTON = "Button";

	private const string BG_OVERLAY = "Button/BG Overlay";

	private const string TEXT = "Button/Text";

	private Entity _building;

	private VFXManager.WorldArrowHandle? _handle;

	private ArrowChainLink _link;

	public override PickType Type => PickType.OrderArrowPick;

	public override void SetTarget(PickTarget target)
	{
		base.SetTarget(target);
		_building = base.Target.FindEntity();
		_handle = base.Target.arrow;
		_link = Game.ctx.vfx.FindArrowExtras(target.arrow) as ArrowChainLink;
	}

	public override void Reset()
	{
		base.Reset();
		_building = null;
		_handle = null;
		_link = null;
	}

	public override Vector3 MakeSceneVector()
	{
		if (!_handle.HasValue)
		{
			return base.MakeSceneVector();
		}
		return _handle.Value.MakeArrowPoints().midpoint.AsVector3XZ.Add(0f, 3f, 0f);
	}

	public override void RefreshContents()
	{
		if (_link.step == null)
		{
			go.GetButton("Button").interactable = false;
			_ = ColorConstants.ARROW_SELL;
			string text = ((_link.res == null) ? Loc.Get("ui.cash") : (_link.res?.GetIcon() ?? ""));
			go.SetText("Button/Text", text);
			return;
		}
		AutoAction action = _link.step.action;
		bool num = action == AutoAction.Buy || action == AutoAction.PickUp || action == AutoAction.HaveCash;
		bool flag = action == AutoAction.Sell || action == AutoAction.DropOff;
		go.GetButton("Button").interactable = false;
		Color color = (num ? ColorConstants.ARROW_BUY : (flag ? ColorConstants.ARROW_SELL : ColorConstants.IMG_DISABLED));
		string text2 = (_link.step.items.iscash ? Loc.Get("ui.cash") : (_link.res?.GetIcon() ?? ""));
		go.GetImage("Button/BG Overlay").color = color;
		go.SetText("Button/Text", text2);
	}

	public override void OnClick()
	{
	}

	public override string MakeMouseoverMessage()
	{
		string text = BuildingUtil.FindBuildingName(_building);
		if (_link?.res == null)
		{
			return Loc.Get("ui.arrowpick.no-icon.mo");
		}
		return text + "\n" + Loc.Get("ui.arrowpick.icon.mo", "icon", _link.res.GetIconAndName());
	}
}

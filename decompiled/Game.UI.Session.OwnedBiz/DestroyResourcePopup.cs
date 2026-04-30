using System;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.UI.Session.OwnedBiz;

public sealed class DestroyResourcePopup : BasePopup
{
	public const string TITLE = "Panel/Title";

	public const string TOPTEXT = "Panel/Top";

	public const string BTMTEXT = "Panel/Bottom";

	public const string AMOUNT = "Panel/Amount/Number";

	public const string BTN_PLUS = "Panel/Amount/Plus";

	public const string BTN_MINUS = "Panel/Amount/Minus";

	public const string BTN_CLOSE = "Panel/Close Button";

	public const string BTN_CANCEL = "Panel/Footer/Cancel";

	public const string BTN_OK = "Panel/Footer/Ok";

	public Entity building;

	public Resource res;

	public Action<ResourceAndQty> onOk;

	private int _qtyBefore;

	private int _qtyToDestroy;

	public override UIReference UIReference => UIElements.DestroyResourcePopup;

	public DestroyResourcePopup(Entity building, Resource res, Action<ResourceAndQty> onOk)
	{
		this.building = building;
		this.res = res;
		this.onOk = onOk;
		_qtyBefore = ModulesUtil.GetInventory(building).data.Get(res).qty.IntFloor();
	}

	protected override void InitializeOnPush()
	{
		_go.GetButton("Panel/Close Button").onClick.SetListener(Close);
		_go.GetButton("Panel/Footer/Cancel").onClick.SetListener(Close);
		_go.GetButton("Panel/Amount/Plus").onClick.SetListener(OnPlusClick);
		_go.GetButton("Panel/Amount/Minus").onClick.SetListener(OnMinusClick);
		_go.GetButton("Panel/Footer/Ok").onClick.SetListener(OnConfirm);
		RefreshContents();
	}

	protected override void ReleaseOnPop()
	{
		building = null;
		onOk = null;
	}

	private void OnPlusClick()
	{
		_qtyToDestroy = MathUtil.Clamp(_qtyToDestroy + InventoryModule.ProduceClickMoveQty(), 0, _qtyBefore);
		RefreshContents();
	}

	private void OnMinusClick()
	{
		_qtyToDestroy = MathUtil.Clamp(_qtyToDestroy - InventoryModule.ProduceClickMoveQty(), 0, _qtyBefore);
		RefreshContents();
	}

	private void RefreshContents()
	{
		_go.SetText("Panel/Title", Loc.Get("ui.resdestroy.header"));
		_go.SetText("Panel/Top", Loc.Get("ui.resdestroy.top", "res", res.GetIconAndName()));
		ResourceAndQty resourceAndQty = new ResourceAndQty(res.resid, _qtyBefore - _qtyToDestroy);
		_go.SetText("Panel/Bottom", Loc.Get("ui.resdestroy.bottom", "res", resourceAndQty.MakeQuantityXLocString()));
		_go.SetText("Panel/Amount/Number", Loc.FormatNumber(_qtyToDestroy));
		_go.GetButton("Panel/Amount/Plus").interactable = _qtyToDestroy < _qtyBefore;
		_go.GetButton("Panel/Amount/Minus").interactable = _qtyToDestroy > 0;
		_go.GetButton("Panel/Footer/Ok").interactable = _qtyToDestroy > 0;
	}

	private void OnConfirm()
	{
		ResourceAndQty obj = new ResourceAndQty(res.resid, _qtyToDestroy);
		Action<ResourceAndQty> action = onOk;
		Close();
		action(obj);
	}
}

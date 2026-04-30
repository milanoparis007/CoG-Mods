using System;
using Game.Core;
using Game.Services;
using Game.Services.Audio;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Convo;

public class ViewItemPicker : ViewContents
{
	private class Order
	{
		public MfgItem item;

		public int maxdelta;

		public bool isPlayerBuying;

		public Resource res;

		public OrderInventory building;

		public OrderInventory vehicle;

		public Entity biz;

		public Order(BuySellElement elt, bool isPlayerBuying, VisitState visit)
		{
			item = elt.item;
			maxdelta = (int)elt.qty;
			this.isPlayerBuying = isPlayerBuying;
			biz = visit.biz;
			res = elt.item.FindResource();
			building = new OrderInventory(res, isBuilding: true, visit.building.components.modules.inventory);
			vehicle = new OrderInventory(res, isBuilding: false, visit.vehicle.components.modules.inventory);
		}

		public int GetCappedDelta(bool toBuilding, int delta)
		{
			OrderInventory source = (toBuilding ? vehicle : building);
			OrderInventory target = (toBuilding ? building : vehicle);
			return (int)FindResourceDeltaCapped(source, target, delta).delta;
		}

		public void Increment(bool toBuilding, int delta)
		{
			int num = (toBuilding ? delta : (-delta));
			building.afterQty += (Fixnum)num;
			vehicle.afterQty -= (Fixnum)num;
		}

		public Price GetUnitPriceAbs(CrewAssignment crew)
		{
			return BuySellUtils.FindPriceNegotiatedAbs(Game.ctx.players.Human, biz, res, isPlayerBuying);
		}

		public Price GetResultingPlayerMoneyDelta(CrewAssignment crew)
		{
			return GetUnitPriceAbs(crew) * building.Delta;
		}

		internal (Fixnum delta, bool wontfit) FindResourceDeltaCapped(OrderInventory source, OrderInventory target, Fixnum delta)
		{
			delta = Fixnum.Min(source.afterQty, delta);
			if (isPlayerBuying && source.isVehicle)
			{
				delta = Fixnum.Min(delta, source.Delta);
			}
			if (!isPlayerBuying && source.isBuilding)
			{
				delta = Fixnum.Min(delta, source.Delta);
			}
			Fixnum fixnum = target.HowManyMoreResourcesCanFit();
			delta = Fixnum.Min(delta, fixnum);
			bool item = fixnum <= 0;
			if (isPlayerBuying && source.isBuilding)
			{
				Fixnum fixnum2 = source.beforeQty - maxdelta;
				Fixnum b = source.afterQty - fixnum2;
				delta = Fixnum.Min(delta, b);
			}
			if (!isPlayerBuying && source.isVehicle)
			{
				Fixnum b2 = target.beforeQty + maxdelta - target.afterQty;
				delta = Fixnum.Min(delta, b2);
			}
			return (delta: delta, wontfit: item);
		}
	}

	private class OrderInventory
	{
		public Resource res;

		public bool isBuilding;

		public InventoryModule module;

		public Fixnum beforeQty;

		public Fixnum afterQty;

		public bool isVehicle => !isBuilding;

		public Fixnum Delta => afterQty - beforeQty;

		public OrderInventory(Resource res, bool isBuilding, InventoryModule module)
		{
			this.res = res;
			this.isBuilding = isBuilding;
			this.module = module;
			beforeQty = module.data.Get(res).qty;
			afterQty = beforeQty;
		}

		public Volume GetOrderVolume(int additionalDelta = 0)
		{
			Volume volume = module.CalculateUsedCapacity();
			Fixnum fixnum = Delta + additionalDelta;
			Volume volume2 = res.unitdef.volume * fixnum;
			return volume + volume2;
		}

		public int HowManyMoreResourcesCanFit()
		{
			Fixnum fixnum = module.CalculateAvailableCapacity().cubicfeet + res.FindTotalVolume(beforeQty).cubicfeet - res.FindTotalVolume(afterQty).cubicfeet;
			int num = (int)res.FindTotalVolume(1).cubicfeet;
			if (num <= 0)
			{
				return 0;
			}
			return (int)(fixnum / num);
		}

		public float GetOrderVolumeAsPercent(int additionalDelta = 0)
		{
			return (float)(GetOrderVolume(additionalDelta).cubicfeet / module.config.capacity.cubicfeet);
		}

		public bool CanFitMoreIntoInventory(int delta)
		{
			float orderVolumeAsPercent = GetOrderVolumeAsPercent(delta);
			if (orderVolumeAsPercent >= 0f)
			{
				return orderVolumeAsPercent <= 1f;
			}
			return false;
		}

		public bool CanRemoveFromInventory(int delta)
		{
			return afterQty - delta >= 0;
		}
	}

	private const string BUTTON_CLOSE = "One Time/Cancel";

	private const string BUTTON_OFFER = "One Time/Offer";

	private const string OT_TITLE = "One Time/Title";

	private const string OT_PLAYER = "One Time/Container/Player Details";

	private const string OT_OWNER = "One Time/Container/Owner Details";

	private const string OT_BTN_TOPLAYER = "One Time/Container/Qty Buttons/To Player";

	private const string OT_BTN_TOBUILDING = "One Time/Container/Qty Buttons/To Owner";

	private const string OT_FOOTER = "One Time/Footer";

	private const string DEET_TOP = "Top";

	private const string DEET_QTY = "Qty";

	private const string DEET_BOTTOM = "Bottom";

	private ConvoDataBuySell _data;

	private Action<QtyAndDir> _ok;

	private Action _cancel;

	private Order _order;

	private GameObject _detailsToBldg;

	private GameObject _detailsToCrew;

	private TextMeshProUGUI _qtyToBldg;

	private TextMeshProUGUI _qtyToCrew;

	private TextMeshProUGUI _inventoryBldg;

	private TextMeshProUGUI _inventoryCrew;

	private Button _bToBldg;

	private Button _bToVehicle;

	public ViewItemPicker(ConvoState state, Action<QtyAndDir> ok, Action cancel)
	{
		_data = state.data as ConvoDataBuySell;
		_ok = ok;
		_cancel = cancel;
	}

	public override GameObject CreatePanel(GameObject container)
	{
		return UnityEngine.Object.Instantiate(_dialog.Go.GetChild("Templates/Convo Item Picker"), container.transform);
	}

	public override void OnAfterInitialize()
	{
		base.OnAfterInitialize();
		_order = new Order(_data.elt, _data.playerBuys, _dialog.Model.visit);
		SetUpCommon();
		SetUpDetails("One Time/Container/Owner Details", building: true, ref _detailsToBldg, ref _qtyToBldg, ref _inventoryBldg, ref _bToBldg);
		SetUpDetails("One Time/Container/Player Details", building: false, ref _detailsToCrew, ref _qtyToCrew, ref _inventoryCrew, ref _bToVehicle);
		PlayUISound.UpdateEffectOn(_panel, "One Time/Offer", SFXType.ClickBuySellConfirm);
		PlayUISound.UpdateEffectOn(_bToBldg.gameObject, "", SFXType.ClickButtonDown);
		PlayUISound.UpdateEffectOn(_bToVehicle.gameObject, "", SFXType.ClickButtonUp);
		RefreshValues();
	}

	private void SetUpCommon()
	{
		_panel.GetButton("One Time/Cancel").onClick.SetListener(OnCancel);
		_panel.GetButton("One Time/Offer").onClick.SetListener(OnOfferButton);
		_panel.SetText("One Time/Title", _data.playerBuys ? Loc.Get("ui.itempicker.one-time.buy") : Loc.Get("ui.itempicker.one-time.sell"));
	}

	private void SetUpDetails(string detailsName, bool building, ref GameObject detail, ref TextMeshProUGUI qtyText, ref TextMeshProUGUI inventoryText, ref Button button)
	{
		_data.FindResource();
		string text = ((building && _order.isPlayerBuying) ? Loc.Get("ui.itempicker.biz-stock") : ((building && !_order.isPlayerBuying) ? Loc.Get("ui.itempicker.offer") : Loc.Get("ui.itempicker.car-inv")));
		detail = _panel.GetChild(detailsName);
		detail.SetText("Top", text);
		qtyText = detail.GetText("Qty");
		inventoryText = detail.GetText("Bottom");
		button = _panel.GetButton(building ? "One Time/Container/Qty Buttons/To Owner" : "One Time/Container/Qty Buttons/To Player");
		button.onClick.SetListener(delegate
		{
			OnArrowClick(building);
		});
	}

	private void OnArrowClick(bool toBuilding)
	{
		int delta = InventoryModule.ProduceClickMoveQty();
		int cappedDelta = _order.GetCappedDelta(toBuilding, delta);
		if (cappedDelta != 0)
		{
			_order.Increment(toBuilding, cappedDelta);
		}
		RefreshValues();
	}

	private void OnCancel()
	{
		_cancel();
	}

	private void OnOfferButton()
	{
		QtyAndDir obj = new QtyAndDir(_order.building.Delta.Abs, _order.building.Delta > 0);
		_ok(obj);
	}

	private void RefreshValues()
	{
		RefreshPanels(_order.building, _order.vehicle, 1);
		RefreshPanels(_order.vehicle, _order.building, 1);
		UpdateFooter();
		_panel.GetButton("One Time/Offer").interactable = CanBuyOrSell();
		string sourceText = (_data.playerBuys ? (Loc.Get("ui.trade.buyonce") + " " + Loc.Get("ui.trade.buyonce.text")) : (Loc.Get("ui.trade.sellonce") + " " + Loc.Get("ui.trade.sellonce.text")));
		_panel.GetChild("One Time/Offer").GetChildText().SetText(sourceText);
		_panel.GetChild("One Time/Cancel").GetChildText().SetText(Loc.Get("button.cancel"));
	}

	private void RefreshPanels(OrderInventory source, OrderInventory target, int delta)
	{
		UpdateQty(target);
		UpdateInventoryState(target);
		(Fixnum, bool) tuple = _order.FindResourceDeltaCapped(source, target, delta);
		bool enabled = tuple.Item1 > 0;
		UpdateButtonState(target, enabled, tuple.Item2);
	}

	private void UpdateQty(OrderInventory inv)
	{
		TextMeshProUGUI textMeshProUGUI = (inv.isBuilding ? _qtyToBldg : _qtyToCrew);
		if (inv.isVehicle)
		{
			Fixnum afterQty = inv.afterQty;
			textMeshProUGUI.SetText(Loc.Get("ui.qty", "qty", afterQty));
		}
		else
		{
			Fixnum fixnum = (_order.isPlayerBuying ? (_order.maxdelta + inv.Delta) : inv.Delta);
			textMeshProUGUI.SetText(Loc.Get("ui.qty", "qty", fixnum));
		}
	}

	private void UpdateInventoryState(OrderInventory target)
	{
		if (target.isVehicle)
		{
			string text = Loc.Percentage(target.GetOrderVolumeAsPercent());
			string sourceText = Loc.Get("ui.amt-full", "percentage", text);
			_inventoryCrew.SetText(sourceText);
		}
		else
		{
			int maxdelta = _order.maxdelta;
			string qtyAndUnits = _order.res.unitdef.GetQtyAndUnits(maxdelta);
			string sourceText2 = (_order.isPlayerBuying ? Loc.Get("ui.trade.sellonce.up-to", "qtyString", qtyAndUnits) : Loc.Get("ui.trade.buyonce.up-to", "qtyString", qtyAndUnits));
			_inventoryBldg.SetText(sourceText2);
		}
	}

	private void UpdateFooter()
	{
		string text = Loc.Price(_order.GetResultingPlayerMoneyDelta(_data.crew));
		string text3;
		if (_order.isPlayerBuying)
		{
			string text2 = Loc.Money(_order.vehicle.module.data.money);
			text3 = Loc.Get("ui.trade.buyonce.costvcash", "price", text, "cash", text2);
		}
		else
		{
			text3 = Loc.Get("ui.trade.sellonce.sales", "price", text);
		}
		_panel.SetText("One Time/Footer", text3);
	}

	private void UpdateButtonState(OrderInventory target, bool enabled, bool wontfit)
	{
		Button obj = (target.isBuilding ? _bToBldg : _bToVehicle);
		string lockey = (wontfit ? "ui.itemorder.movefail.mo" : "ui.itemorder.move.mo");
		if (_order.isPlayerBuying && _order.vehicle.Delta <= 0 && target.isBuilding)
		{
			enabled = false;
		}
		if (!_order.isPlayerBuying && _order.building.Delta <= 0 && !target.isBuilding)
		{
			enabled = false;
		}
		obj.GetComponent<TextMouseoverContext>().lockey = lockey;
		obj.interactable = enabled;
	}

	private bool CanBuyOrSell()
	{
		if (_order.vehicle.Delta.IsZero)
		{
			return false;
		}
		Price resultingPlayerMoneyDelta = _order.GetResultingPlayerMoneyDelta(_data.crew);
		if (!Game.ctx.players.Human.finances.CanChangeMoneyOnCrew(_dialog.Model.visit, resultingPlayerMoneyDelta))
		{
			return false;
		}
		return true;
	}
}

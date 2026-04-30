using System;
using Game.Services;
using Game.Services.Audio;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Sim.Modules;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Convo;

public class ViewMoneyPicker : ViewContents
{
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

	private ConvoDataGamblingCollect _data;

	private Action<QtyAndDir> _ok;

	private Action _cancel;

	private Fixnum startBldg;

	private Fixnum startVeh;

	private Fixnum delta;

	private GameObject _detailsToBldg;

	private GameObject _detailsToCrew;

	private TextMeshProUGUI _qtyToBldg;

	private TextMeshProUGUI _qtyToCrew;

	private TextMeshProUGUI _inventoryBldg;

	private TextMeshProUGUI _inventoryCrew;

	private Button _bToBldg;

	private Button _bToVehicle;

	public ViewMoneyPicker(ConvoDataGamblingCollect data, Action<QtyAndDir> ok, Action cancel)
	{
		_data = data;
		_ok = ok;
		_cancel = cancel;
	}

	public override GameObject CreatePanel(GameObject container)
	{
		return UnityEngine.Object.Instantiate(_dialog.Go.GetChild("Templates/Convo Money Picker"), container.transform);
	}

	public override void OnAfterInitialize()
	{
		base.OnAfterInitialize();
		startBldg = ModulesUtil.GetInventory(_dialog.Model.visit.building).data.money.cash;
		startVeh = ModulesUtil.GetInventory(_dialog.Model.visit.vehicle).data.money.cash;
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
		_panel.SetText("One Time/Title", Loc.Get("ui.moneypicker.money-header"));
	}

	private void SetUpDetails(string detailsName, bool building, ref GameObject detail, ref TextMeshProUGUI qtyText, ref TextMeshProUGUI inventoryText, ref Button button)
	{
		string text = (building ? Loc.Get("ui.moneypicker.building-inventory") : Loc.Get("ui.moneypicker.vehicle-inventory"));
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
		int num = InventoryModule.ProduceClickMoveQty();
		int num2 = (toBuilding ? num : (-num));
		delta += (Fixnum)num2;
		FixDelta();
		RefreshValues();
	}

	private void OnCancel()
	{
		_cancel();
	}

	private void OnOfferButton()
	{
		QtyAndDir obj = new QtyAndDir(delta.Abs, delta.IsPositive);
		_ok(obj);
	}

	private void FixDelta()
	{
		delta = MathUtil.Clamp(delta, -startBldg, startVeh);
	}

	private void RefreshValues()
	{
		RefreshPanels();
		EmptyUnused();
		_panel.GetButton("One Time/Offer").interactable = true;
		string sourceText = Loc.Get("ui.trade.transfer");
		_panel.GetChild("One Time/Offer").GetChildText().SetText(sourceText);
		_panel.GetChild("One Time/Cancel").GetChildText().SetText(Loc.Get("button.cancel"));
	}

	private void RefreshPanels()
	{
		_qtyToBldg.SetText(Loc.Get("ui.qty", "qty", Loc.Money(startBldg + delta)));
		_qtyToCrew.SetText(Loc.Get("ui.qty", "qty", Loc.Money(startVeh - delta)));
		UpdateButtonStates();
	}

	private void EmptyUnused()
	{
		_panel.SetText("One Time/Footer", "");
		_inventoryBldg.SetText("");
		_inventoryCrew.SetText("");
	}

	private void UpdateButtonStates()
	{
		if (startBldg + delta == 0)
		{
			_bToVehicle.interactable = false;
		}
		else
		{
			_bToVehicle.interactable = true;
		}
		if (startVeh - delta == 0)
		{
			_bToBldg.interactable = false;
		}
		else
		{
			_bToBldg.interactable = true;
		}
	}
}

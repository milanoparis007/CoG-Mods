using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Audio;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Session.Picks;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI.Session.Deliveries;

public class DeliveriesEditView
{
	private class HideMouseoverOnClick : MonoBehaviour
	{
		public DeliveriesEditView view;

		private void Update()
		{
			if (view != null && !(view._editDest == null) && view._editDest.IsExpanded)
			{
				Game.serv.mouseovers.OnMouseOut(MouseoverType.DeliveryDestinationDropDown);
			}
		}
	}

	public class DestinationDropdownMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverTR;

		protected override string ProduceText()
		{
			Entity entity = Game.ctx.hud.deliveries.CurrentDest();
			if (entity == null)
			{
				return "";
			}
			if (!entity.data.building.controlled.pid.IsHumanPlayer)
			{
				return BuildingUtil.FindBuildingName(entity);
			}
			return BuildingUtil.GetBackModuleName(entity.Id) ?? BuildingUtil.FindBuildingName(entity);
		}
	}

	public const string EDIT_PANEL = "Edit Panel";

	public const string EDIT_DESC = "Edit Panel/Info/Description";

	public const string EDIT_ITEM_ROW = "Edit Panel/Item";

	public const string EDIT_ITEM_TEXT = "Edit Panel/Item/Text";

	public const string EDIT_ITEM_DROPDOWN = "Edit Panel/Item/List";

	public const string EDIT_DEST_GOTOBTN = "Edit Panel/Dest/Goto";

	public const string EDIT_DEST_DROPDOWN = "Edit Panel/Dest/List";

	public const string EDIT_DEST_ICONS = "Edit Panel/Dest/Icons";

	public const string EDIT_ACTION_TEXT = "Edit Panel/Action/Text";

	public const string EDIT_ACTION_DROPDOWN = "Edit Panel/Action/List";

	public const string EDIT_AMT_ROW = "Edit Panel/Amt";

	public const string EDIT_AMT_DROPDOWN = "Edit Panel/Amt/Type";

	public const string EDIT_AMT_NUMBER = "Edit Panel/Amt/Number";

	public const string EDIT_AMT_PLUSBTN = "Edit Panel/Amt/Plus";

	public const string EDIT_AMT_MINUSBTN = "Edit Panel/Amt/Minus";

	public const string EDIT_CHECKBOX = "Edit Panel/Dest/Checkbox";

	public const string EDIT_CHECKBOX_TEXT = "Edit Panel/Dest/Checkbox/Text";

	public const string EDIT_BTN_OK = "Edit Panel/Buttons/OK";

	public const string EDIT_BTN_OK_NEXT = "Edit Panel/Buttons/OK Next";

	public const string EDIT_BTN_OK_ADD = "Edit Panel/Buttons/OK Add";

	public const string EDIT_BTN_DELETE = "Edit Panel/Buttons/Delete";

	public const string EDIT_BTN_CANCEL = "Edit Panel/Buttons/Cancel";

	public const string PREV_TEXT = "Edit Panel/Footer/Previous";

	public const string PREV_GOTO = "Edit Panel/Footer/Previous Goto";

	public const string NEXT_TEXT = "Edit Panel/Footer/Next";

	public const string NEXT_GOTO = "Edit Panel/Footer/Next Goto";

	public const string PREV_EDIT = "Edit Panel/Footer/Edit Previous";

	public const string NEXT_EDIT = "Edit Panel/Footer/Edit Next";

	private DeliveriesEditModel _model;

	private DeliveriesDialog _dialog;

	private GameObject _go;

	private GameObject _editPanel;

	private GameObject _itemRow;

	private GameObject _amtRow;

	private TMP_Dropdown _editDest;

	private TMP_Dropdown _editAction;

	private TMP_Dropdown _editItem;

	private TMP_Dropdown _editAmt;

	public bool IsEditVisible => _editPanel.activeSelf;

	public int StepIndex => _model.stepindex;

	public void InitializeEdit(DeliveriesDialog dialog, GameObject go)
	{
		_dialog = dialog;
		_go = go;
		_editPanel = _go.GetChild("Edit Panel");
		_itemRow = _go.GetChild("Edit Panel/Item");
		_amtRow = _go.GetChild("Edit Panel/Amt");
		_editAction = _go.GetChild<TMP_Dropdown>("Edit Panel/Action/List");
		_editAction.onValueChanged.SetListener(OnActionChanged);
		_editItem = _go.GetChild<TMP_Dropdown>("Edit Panel/Item/List");
		_editItem.onValueChanged.SetListener(OnItemChanged);
		_editDest = _go.GetChild<TMP_Dropdown>("Edit Panel/Dest/List");
		_editDest.onValueChanged.SetListener(OnDestChanged);
		_editAmt = _go.GetChild<TMP_Dropdown>("Edit Panel/Amt/Type");
		_editAmt.onValueChanged.SetListener(OnAmtChanged);
		_go.GetButton("Edit Panel/Amt/Plus").onClick.SetListener(OnAmtPlusClick);
		_go.GetButton("Edit Panel/Amt/Minus").onClick.SetListener(OnAmtMinusClick);
		_go.GetButton("Edit Panel/Buttons/OK").onClick.SetListener(OnOkButtonClick);
		_go.GetButton("Edit Panel/Footer/Edit Previous").onClick.SetListener(OnOkPrevButtonClick);
		_go.GetButton("Edit Panel/Footer/Edit Next").onClick.SetListener(OnOkNextButtonClick);
		_go.GetButton("Edit Panel/Buttons/OK Add").onClick.SetListener(OnOkAddButtonClick);
		_go.GetButton("Edit Panel/Buttons/Cancel").onClick.SetListener(OnCancelButtonClick);
		_go.GetButton("Edit Panel/Buttons/Delete").onClick.SetListener(OnDeleteButtonClick);
		_go.GetButton("Edit Panel/Dest/Goto").onClick.SetListener(OnGotoButtonClick);
		_go.GetToggle("Edit Panel/Dest/Checkbox").onValueChanged.SetListener(OnCheckboxChanged);
		_go.SetText("Edit Panel/Action/Text", Loc.Get("ui.deliveries.select"));
		_go.SetText("Edit Panel/Item/Text", "");
		Game.serv.mouseovers.Register(MouseoverType.DeliveryDestinationDropDown, new DestinationDropdownMouseover());
		_editPanel.GetOrAddComponent<HideMouseoverOnClick>().view = this;
		PlayUISound.UpdateEffectOn(_go, "Edit Panel/Amt/Plus", SFXType.ClickButtonUp);
		PlayUISound.UpdateEffectOn(_go, "Edit Panel/Amt/Minus", SFXType.ClickButtonDown);
		_editPanel.SetActive(value: false);
	}

	public void ReleaseEdit()
	{
		_editPanel.DestroyComponentIfAdded<HideMouseoverOnClick>();
		Game.serv.mouseovers.Unregister(MouseoverType.DeliveryDestinationDropDown);
		if (IsEditVisible)
		{
			FinishEdit(commit: false);
		}
		_itemRow = (_amtRow = null);
		_editDest = (_editAction = (_editItem = (_editAmt = null)));
		_go = null;
	}

	public void EditDest(Entity building)
	{
		int index = _model.destinations.FindIndex((DeliveriesEditModel.Destination item) => item.building == building);
		_model.Select(_model.destinations, index);
		_model.RegenerateModel(actions: false, items: false, destinations: false, amts: true);
		RefreshView();
		OnGotoButtonClick();
	}

	internal void StartEdit(AutomationSequence seq, AutomationStep step, int index)
	{
		_model = new DeliveriesEditModel();
		_model.InitializeModelFromStep(seq, step, index);
		_editPanel.SetActive(value: true);
		RefreshView();
		bool interactable = _model.GetNext() != null;
		bool interactable2 = _model.GetPrevious() != null;
		_go.SetText("Edit Panel/Footer/Previous", Loc.Get("ui.deliveries.steps.prev", "description", AutomationUtil.Describe(_model.GetPrevious())));
		_go.GetButton("Edit Panel/Footer/Previous Goto").interactable = interactable2;
		_go.GetButton("Edit Panel/Footer/Previous Goto").onClick.SetListener(delegate
		{
			PersonInfoUtil.TweenCameraToEntity(_model.GetPrevious().target);
		});
		_go.SetText("Edit Panel/Footer/Next", Loc.Get("ui.deliveries.steps.next", "description", AutomationUtil.Describe(_model.GetNext())));
		_go.GetButton("Edit Panel/Footer/Next Goto").interactable = interactable;
		_go.GetButton("Edit Panel/Footer/Next Goto").onClick.SetListener(delegate
		{
			PersonInfoUtil.TweenCameraToEntity(_model.GetNext().target);
		});
		_ = _model.stepindex;
		_ = _model.sequence.steps.Count;
	}

	private void FinishEdit(bool commit, bool editNext = false, bool addNew = false, bool editPrev = false)
	{
		if (commit)
		{
			_dialog.OnStepUpdated(_model.stepindex, _model.current);
		}
		int stepindex = _model.stepindex;
		_model = null;
		_editPanel.SetActive(value: false);
		Game.ctx.overlays.HideAnyOverlay();
		if (editNext)
		{
			_dialog.OnEditNext(stepindex);
		}
		else if (editPrev)
		{
			_dialog.OnEditPrev(stepindex);
		}
		else if (addNew)
		{
			_dialog.OnEditNew(stepindex);
		}
	}

	public void OnBeforeHide()
	{
		if (IsEditVisible)
		{
			FinishEdit(commit: false);
		}
	}

	public string GetStepEditProgress()
	{
		return _model.GetStepEditProgress();
	}

	public string GetDestinationPips()
	{
		(Entity building, BuildingPickData data) destinationPickData = GetDestinationPickData();
		Entity item = destinationPickData.building;
		BuildingPickData item2 = destinationPickData.data;
		List<string> otherSequenceNames = GetOtherSequenceNames(item);
		string text = "";
		if (otherSequenceNames != null && otherSequenceNames.Count > 0)
		{
			text = text + Loc.Get("ui.deliveries.got-others-pip.icon") + " ";
		}
		if (item2.showpips)
		{
			text += BasePickUtil.GeneratePipIcons(item2.showpips, item2.pips);
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return null;
	}

	public string GetDestinationPipsDescription()
	{
		(Entity building, BuildingPickData data) destinationPickData = GetDestinationPickData();
		Entity item = destinationPickData.building;
		BuildingPickData item2 = destinationPickData.data;
		List<string> otherSequenceNames = GetOtherSequenceNames(item);
		string text = "";
		if (otherSequenceNames != null && otherSequenceNames.Count > 0)
		{
			text = text + Loc.Get("ui.deliveries.got-others-pip.text") + "\n";
			text = text + otherSequenceNames.SelectToString((string name) => Loc.Get("ui.deliveries.got-others-pip.line", "item", name), "\n") + "\n";
		}
		if (item2.showpips)
		{
			text += BasePickUtil.GeneratePipDescriptions(item2.pips);
		}
		return text.Trim();
	}

	private (Entity building, BuildingPickData data) GetDestinationPickData()
	{
		Entity entity = _model.destinations.selected?.building;
		if (entity == null)
		{
			return (building: null, data: default(BuildingPickData));
		}
		BuildingAndBusinessData data = BuildingUtil.FindDataForBuilding(entity);
		return (building: entity, data: BuildingPickData.GenerateBuildingButtonData(data));
	}

	private List<string> GetOtherSequenceNames(Entity target)
	{
		if (target != null)
		{
			return (from s in Game.ctx.players.Human.automation.GetAllSequences()
				where s != _model.sequence
				where s.ContainsTarget(target.Id)
				select s.name).ToList();
		}
		return null;
	}

	private void OnGotoButtonClick()
	{
		Entity entity = _model.destinations.selected?.building;
		if (entity != null)
		{
			PersonInfoUtil.TweenCameraToEntity(entity);
		}
	}

	private void OnActionChanged(int index)
	{
		_model.Select(_model.actions, index);
		_model.RegenerateModel(actions: false, items: true);
		RefreshView();
	}

	private void OnItemChanged(int index)
	{
		_model.Select(_model.items, index);
		_model.RegenerateModel(actions: false, items: false, destinations: true);
		RefreshView();
		OnGotoButtonClick();
	}

	private void OnDestChanged(int index)
	{
		_model.Select(_model.destinations, index);
		_model.RegenerateModel(actions: false, items: false, destinations: false, amts: true);
		RefreshView();
		OnGotoButtonClick();
	}

	public Entity CurrentDest()
	{
		return _model.destinations.selected?.building;
	}

	public AutoAction CurrentAction()
	{
		return _model.actions.selected.type;
	}

	public Label CurrentItem()
	{
		if (!_model.items.selected.IsRes)
		{
			return Label.NULL;
		}
		return _model.items.selected.res.resid;
	}

	public AmtChoiceType CurrentAmt()
	{
		return _model.amts.selected.type;
	}

	private void OnAmtChanged(int index)
	{
		_model.Select(_model.amts, index);
		RefreshAmtNumberAndButtons();
	}

	private void OnAmtPlusClick()
	{
		_model.SetAmtNumber(MathUtil.ClampMin(_model.amtnumber + InventoryModule.ProduceClickMoveQty(), 0));
		RefreshAmtNumberAndButtons();
	}

	private void OnAmtMinusClick()
	{
		_model.SetAmtNumber(MathUtil.ClampMin(_model.amtnumber - InventoryModule.ProduceClickMoveQty(), 0));
		RefreshAmtNumberAndButtons();
	}

	private void OnOkButtonClick()
	{
		FinishEdit(commit: true);
	}

	private void OnOkNextButtonClick()
	{
		FinishEdit(commit: true, editNext: true);
	}

	private void OnOkPrevButtonClick()
	{
		FinishEdit(commit: true, editNext: false, addNew: false, editPrev: true);
	}

	private void OnOkAddButtonClick()
	{
		FinishEdit(commit: true, editNext: false, addNew: true);
	}

	private void OnCancelButtonClick()
	{
		FinishEdit(commit: false);
	}

	private void OnDeleteButtonClick()
	{
		_dialog.OnStepDelete(_model.stepindex);
		FinishEdit(commit: false);
	}

	private void OnCheckboxChanged(bool val)
	{
		switch (_model.actions.selected?.type ?? AutoAction.None)
		{
		case AutoAction.Buy:
			_model.SetBoolean(val);
			break;
		case AutoAction.Sell:
			_model.SetBoolean(null, val);
			break;
		}
	}

	private void RefreshView()
	{
		UpdateDropdown(_editAction, _model.actions);
		UpdateDropdown(_editItem, _model.items);
		UpdateDropdown(_editDest, _model.destinations);
		RefreshDestinationPips();
		bool num = _model.IsFirstHalfValid();
		List<Entity> destinations = _model.destinations.Select((DeliveriesEditModel.Destination item) => item.building).ToList();
		if (num)
		{
			Game.ctx.overlays.ShowDeliveriesOverlay(destinations);
		}
		else
		{
			Game.ctx.overlays.HideAnyOverlay();
		}
		UpdateDropdown(_editAmt, _model.amts);
		RefreshAmtNumberAndButtons();
		RefreshCheckbox();
	}

	private void RefreshCheckbox()
	{
		AutoAction autoAction = _model.actions.selected?.type ?? AutoAction.None;
		bool flag = autoAction == AutoAction.Buy || autoAction == AutoAction.Sell;
		_go.GetToggle("Edit Panel/Dest/Checkbox").gameObject.SetActive(flag);
		if (flag)
		{
			switch (autoAction)
			{
			case AutoAction.Buy:
				_go.SetText("Edit Panel/Dest/Checkbox/Text", Loc.Get("ui.deliveries.skip-full.icon"));
				_go.GetChild("Edit Panel/Dest/Checkbox").GetComponentInChildren<TextMouseoverContext>().lockey = "ui.deliveries.skip-full.mo";
				_go.GetToggle("Edit Panel/Dest/Checkbox").SetIsOnWithoutNotify(_model.skipBuy);
				break;
			case AutoAction.Sell:
				_go.SetText("Edit Panel/Dest/Checkbox/Text", Loc.Get("ui.deliveries.skip-empty.icon"));
				_go.GetChild("Edit Panel/Dest/Checkbox").GetComponentInChildren<TextMouseoverContext>().lockey = "ui.deliveries.skip-empty.mo";
				_go.GetToggle("Edit Panel/Dest/Checkbox").SetIsOnWithoutNotify(_model.skipSell);
				break;
			}
		}
	}

	private void RefreshDestinationPips()
	{
		_go.SetTextOrHide("Edit Panel/Dest/Icons", GetDestinationPips());
	}

	private void UpdateDropdown<T>(TMP_Dropdown dropdown, DeliveriesEditModel.DropdownModel<T> model) where T : DeliveriesEditModel.DropdownItem
	{
		dropdown.ClearOptions();
		dropdown.AddOptions(model.SelectIntoNewList((T e) => e.text));
		dropdown.SetValueWithoutNotify(model.selectedIndex);
		dropdown.interactable = dropdown.options.Count > 0;
	}

	private void RefreshAmtNumberAndButtons()
	{
		DeliveriesEditModel.AmtChoice selected = _model.amts.selected;
		bool value = selected != null && (selected.type == AmtChoiceType.Amount || selected.type == AmtChoiceType.AllBut || selected.type == AmtChoiceType.EnsureAmount);
		_go.SetText("Edit Panel/Amt/Number", Loc.FormatNumber(_model.amtnumber));
		_go.SetActive("Edit Panel/Amt/Number", value);
		_go.SetActive("Edit Panel/Amt/Plus", value);
		_go.SetActive("Edit Panel/Amt/Minus", value);
		_go.GetButton("Edit Panel/Amt/Minus").interactable = _model.amtnumber > 0;
		RefreshDescriptionAndOKButton();
	}

	private void RefreshDescriptionAndOKButton()
	{
		bool interactable = _model.IsDataValid();
		_go.SetText("Edit Panel/Info/Description", Loc.Get("ui.deliveries.step-no", "stepNo", _model.stepindex + 1, "desc", AutomationUtil.Describe(_model.current)));
		_go.GetButton("Edit Panel/Buttons/OK").interactable = interactable;
		_go.GetButton("Edit Panel/Buttons/OK Add").interactable = interactable;
		_go.GetButton("Edit Panel/Footer/Edit Previous").interactable = interactable;
		_go.GetButton("Edit Panel/Footer/Edit Next").interactable = interactable;
	}
}

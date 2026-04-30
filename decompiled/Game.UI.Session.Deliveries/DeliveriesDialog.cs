using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Session.Crew;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Session.Deliveries;

public class DeliveriesDialog : BaseHUDDialog, IKeyboardHandler
{
	private class CardContext : MonoBehaviour
	{
		public int index;

		public AutomationStep step;

		public bool isAddCard;

		public CardContext Set(int index, AutomationStep step, bool isAddCard)
		{
			this.index = index;
			this.step = step;
			this.isAddCard = isAddCard;
			return this;
		}
	}

	public class DeliveryStepEditOkMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverTR;

		protected override string ProduceText()
		{
			Button componentInParent = go.GetComponentInParent<Button>();
			if (!(componentInParent != null) || !componentInParent.interactable)
			{
				return Game.ctx.hud.deliveries.GetStepEditProgress();
			}
			return go.GetComponent<TextMouseoverContext>().GetText();
		}
	}

	public class DeliveryStepEditIconsMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverTR;

		protected override string ProduceText()
		{
			return Game.ctx.hud.deliveries.GetStepEditIconsDescription();
		}
	}

	public const string TMPL_CARD_ADD = "Templates/Deliveries Add";

	public const string TMPL_CARD_ITEM = "Templates/Deliveries Card";

	public const string BTN_CLOSE = "Panel/Close";

	public const string INFO_INPUT_FIELD = "Panel/Info/Name";

	public const string INFO_PERSON_BUTTON = "Panel/Info/First";

	public const string INFO_PERSON_TEXT = "Panel/Info/First Text";

	public const string INFO_VEHICLE_BUTTON = "Panel/Info/Second";

	public const string INFO_VEHICLE_TEXT = "Panel/Info/Second Text";

	public const string INFO_STATUS = "Panel/Info/Status";

	public const string INFO_BUTTON_START = "Panel/Info/Buttons/Start";

	public const string INFO_BUTTON_STOP = "Panel/Info/Buttons/Stop";

	public const string INFO_BUTTON_CREW_REMOVE = "Panel/Info/Buttons/Clear Crew";

	public const string EDIT_ORDER_BUTTON = "Panel/Edit Order Button";

	public const string FOOTER_BUTTON_CLEAR = "Panel/Footer/Buttons/Clear Steps";

	public const string FOOTER_BUTTON_DELETE = "Panel/Footer/Buttons/Delete Job";

	public const string CONTAINER = "Panel/Scroll View Items/Viewport/Content";

	private GameObject _tmplAdd;

	private GameObject _tmplItem;

	private GameObject _container;

	private AutomationID _auto;

	private DeliveriesEditView _edit;

	private TMP_InputField _input;

	private KeyboardHandler _keyhandler;

	private bool _inEditOrderMode;

	private int _selectedEditOrder;

	private AutomationExecutor AutoExec = Game.ctx.players.Human.automation;

	private const string CARD_TEXT = "Text";

	private const string CARD_ICON = "Icon";

	private const string CARD_BTN_UP = "Up";

	private const string CARD_BTN_DOWN = "Down";

	private const string CARD_BTN_NEXT = "Next";

	private const string CARD_BTN_SNOOZE = "Snooze";

	private const string EDIT_OVERLAY = "Edit Overlay";

	private const string EDIT_TEXT = "Edit Overlay/Text";

	private const string EDIT_ICON = "Edit Overlay/Icon";

	private const string EDIT_ITEM_DECO = "Edit Overlay/Deco";

	private const string EDIT_MOVE_TOP = "Edit Overlay/Add To Top";

	private const string EDIT_MOVE_BOTTOM = "Edit Overlay/Add To Bottom";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Left;

	public override UIReference UIReference => UIElements.HUDDeliveries;

	internal override void Initialize()
	{
		base.Initialize();
		_tmplAdd = _go.GetChild("Templates/Deliveries Add");
		_tmplItem = _go.GetChild("Templates/Deliveries Card");
		_container = _go.GetChild("Panel/Scroll View Items/Viewport/Content");
		_edit = new DeliveriesEditView();
		_edit.InitializeEdit(this, _go);
		_input = _go.GetChild<TMP_InputField>("Panel/Info/Name");
		_input.text = "";
		_input.onSubmit.SetListener(delegate(string val)
		{
			OnNameSubmit(val);
		});
		Game.ctx.events.AddListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnEnded, OnHumanPlayerTurnEnded);
		Game.serv.mouseovers.Register(MouseoverType.DeliveryStepEditOkButton, new DeliveryStepEditOkMouseover());
		Game.serv.mouseovers.Register(MouseoverType.DeliveryStepEditIcons, new DeliveryStepEditIconsMouseover());
		_go.GetButton("Panel/Close").onClick.SetListener(Close);
		_go.GetButton("Panel/Info/Buttons/Start").onClick.SetListener(OnStart);
		_go.GetButton("Panel/Info/Buttons/Stop").onClick.SetListener(OnStop);
		_go.GetButton("Panel/Info/Buttons/Clear Crew").onClick.SetListener(DoRemoveCrew);
		_go.GetButton("Panel/Footer/Buttons/Clear Steps").onClick.SetListener(OnClearSteps);
		_go.GetButton("Panel/Footer/Buttons/Delete Job").onClick.SetListener(OnDeleteJob);
	}

	internal override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnEnded, OnHumanPlayerTurnEnded);
		Game.serv.mouseovers.Unregister(MouseoverType.DeliveryStepEditOkButton);
		Game.serv.mouseovers.Unregister(MouseoverType.DeliveryStepEditIcons);
		_edit.ReleaseEdit();
		_edit = null;
		_tmplItem = (_tmplAdd = (_container = null));
		base.Release();
	}

	public KeyboardHandler GetKeyHandler()
	{
		return _keyhandler;
	}

	private void AddKeyHandler()
	{
		_keyhandler = new CapturingKeyboardHandler(() => TextFocused(), KeyboardHandler.Priority.HighNonModalDialog);
		Game.serv.keyboard.PushHandler(this);
	}

	private void RemoveKeyHandler()
	{
		Game.serv.keyboard.RemoveHandler(this);
		_keyhandler = null;
	}

	private bool TextFocused()
	{
		bool flag = _input.isFocused;
		TMP_InputField[] componentsInChildren = _container.GetComponentsInChildren<TMP_InputField>();
		foreach (TMP_InputField tMP_InputField in componentsInChildren)
		{
			flag = flag || tMP_InputField.isFocused;
		}
		return flag;
	}

	private void OnNameSubmit(string value)
	{
		AutoExec.SetName(_auto, value.Sanitize());
		EventSystem.current.SetSelectedGameObject(null);
		GetAutomationSequence()?.SendAutomationChangedEvent(PlayerID.HumanPlayer);
	}

	private void OnCurrentActiveChanged(SessionEvent _)
	{
		Close();
	}

	private void OnHumanPlayerTurnEnded(SessionEvent _)
	{
		Close();
	}

	private void Close()
	{
		if (base.IsShowing)
		{
			Hide();
		}
	}

	public override void Show()
	{
		Logger.Error("Do not use Show() for deliveries, use ", "ShowFor");
		base.Show();
	}

	public void ShowFor(AutomationID id)
	{
		if (id.IsValid)
		{
			_auto = id;
			base.Show();
			RefreshContainer();
		}
	}

	protected override void OnAfterShow()
	{
		base.OnAfterShow();
		AddKeyHandler();
	}

	protected override void OnBeforeHide()
	{
		base.OnBeforeHide();
		_selectedEditOrder = -1;
		_inEditOrderMode = false;
		Game.ctx.overlays.HideAnyOverlay();
		RemoveKeyHandler();
		_edit.OnBeforeHide();
		OnNameSubmit(_input.text);
		Game.ctx.overlays.arrows.HideArrowsForAutomation();
		_auto = AutomationID.INVALID;
	}

	private bool IsRunning()
	{
		return AutoExec.GetAutoOrNull(_auto).IsAutoActive;
	}

	public bool HasCrew()
	{
		return GetAutomationSequence().FindCrew().IsInVehicle;
	}

	private void OnStart()
	{
		Game.ctx.sfx.PlayAutomationChange(start: true);
		AutoExec.ToggleExecution(_auto, activate: true);
		RefreshContainer();
	}

	private void OnStop()
	{
		Game.ctx.sfx.PlayAutomationChange(start: false);
		AutoExec.ToggleExecution(_auto, activate: false);
		RefreshContainer();
	}

	private void DoRemoveCrew()
	{
		if (IsRunning())
		{
			Game.ctx.sfx.PlayAutomationChange(start: false);
			AutoExec.ToggleExecution(_auto, activate: false);
		}
		AutoExec.ClearAutomationCrew(_auto);
		RefreshContents();
	}

	public Entity CurrentDest()
	{
		return _edit.CurrentDest();
	}

	public AutoAction CurrentAction()
	{
		return _edit.CurrentAction();
	}

	public Label CurrentItem()
	{
		return _edit.CurrentItem();
	}

	public AmtChoiceType CurrentAmt()
	{
		return _edit.CurrentAmt();
	}

	public void EditDest(Entity building)
	{
		_edit.EditDest(building);
	}

	public bool IsEditing()
	{
		return _edit.IsEditVisible;
	}

	public int GetStepIndex()
	{
		return _edit.StepIndex;
	}

	private void OnClearSteps()
	{
		OkPopup.ShowOkCancel(Loc.Get("ui.deliveries.confirm.clear-steps"), delegate
		{
			if (IsRunning())
			{
				AutoExec.ToggleExecution(_auto, activate: false);
			}
			AutoExec.RemoveAllSteps(_auto);
			RefreshContents();
		}, delegate
		{
		});
	}

	private void OnDeleteJob()
	{
		OkPopup.ShowOkCancel(Loc.Get("ui.deliveries.confirm.clear"), delegate
		{
			AutomationID auto = _auto;
			bool num = IsRunning();
			bool flag = HasCrew();
			Close();
			if (num)
			{
				AutoExec.ToggleExecution(auto, activate: false);
			}
			if (flag)
			{
				AutoExec.ClearAutomationCrew(auto);
			}
			AutoExec.RemoveAllSteps(auto);
			AutoExec.DestroyAutomationSequence(auto);
		}, delegate
		{
		});
	}

	public AutomationSequence GetAutomationSequence()
	{
		return AutoExec.GetAutoOrNull(_auto);
	}

	private CrewAssignment FindCrewOrDefault()
	{
		return AutoExec.GetAutoOrNull(_auto)?.FindCrew() ?? CrewAssignment.EMPTY;
	}

	protected override void RefreshContents()
	{
		base.RefreshContents();
		RefreshHeader();
		RefreshContainer();
	}

	private void RefreshHeader()
	{
		_input.text = AutoExec.GetName(_auto);
		CrewAssignment crewAssignment = FindCrewOrDefault();
		Entity peep = crewAssignment.GetPeep();
		Entity vehicle = crewAssignment.GetVehicle();
		CrewInfoGen.ButtonConfig config = CrewInfoGen.FetchSpriteButtonConfig(peep);
		config.addType = CrewInfoGen.ButtonConfig.AddType.Crew;
		CrewInfoGen.UpdateCrewButton(_go.GetChild("Panel/Info/First"), config);
		_go.SetButtonListener("Panel/Info/First", OnManagementClick);
		string text = ((peep != null) ? PersonInfoUtil.GeneratePeepName(peep, showRank: false) : Loc.Get("ui.deliveries.header.no-driver"));
		_go.SetTextOrHide("Panel/Info/First Text", text);
		config = CrewInfoGen.FetchSpriteButtonConfig(vehicle);
		config.addType = CrewInfoGen.ButtonConfig.AddType.Vehicle;
		config.show = config.sprite != null;
		CrewInfoGen.UpdateCrewButton(_go.GetChild("Panel/Info/Second"), config);
		_go.GetButton("Panel/Info/Second").interactable = false;
		string text2 = ((vehicle != null) ? ModulesUtil.DescribeVehicleAndCapacity(vehicle) : null);
		_go.SetTextOrHide("Panel/Info/Second Text", text2);
	}

	private void RefreshContainer()
	{
		AutomationSequence seq = GetAutomationSequence();
		_container.DestroyAllChildren();
		_container.EnsureChildCount(seq.steps.Count, _tmplItem);
		_container.InitializeChildren(seq.steps, InitStepCard);
		MakeAddCard(_container);
		_container.WalkChildren(delegate(Transform tr)
		{
			RefreshCard(tr.gameObject, seq);
		}, recursive: false);
		RefreshEditOrderButton();
		RefreshStartStopAndCrew();
		RefreshArrows();
	}

	private void RefreshEditOrderButton()
	{
		_go.GetButton("Panel/Edit Order Button").onClick.SetListener(ToggleEditOrderMode);
		_go.GetButton("Panel/Edit Order Button").interactable = GetAutomationSequence().steps.Count > 1;
	}

	private void ToggleEditOrderMode()
	{
		_inEditOrderMode = !_inEditOrderMode;
		_selectedEditOrder = -1;
		RefreshContainer();
	}

	private void RefreshArrows()
	{
		Game.ctx.overlays.arrows.RefreshArrowsForAutomation(FindCrewOrDefault());
	}

	private void RefreshStartStopAndCrew()
	{
		CrewAssignment crewAssignment = FindCrewOrDefault();
		bool isInVehicle = crewAssignment.IsInVehicle;
		bool flag = crewAssignment.GetVehicle()?.components.mobile.IsJunk() ?? false;
		AutomationSequence automationSequence = GetAutomationSequence();
		string text = (automationSequence.IsAutoActive ? Loc.Get("ui.deliveries.status.running") : (flag ? Loc.Get("ui.deliveries.status.stopped-junk") : Loc.Get("ui.deliveries.status.broken")));
		_go.SetText("Panel/Info/Status", text);
		int num;
		int num2;
		if (isInVehicle && !flag)
		{
			num = ((automationSequence.steps.Count > 1) ? 1 : 0);
			if (num != 0)
			{
				num2 = (automationSequence.IsAutoNotActive ? 1 : 0);
				goto IL_0098;
			}
		}
		else
		{
			num = 0;
		}
		num2 = 0;
		goto IL_0098;
		IL_0098:
		bool interactable = (byte)num2 != 0;
		bool interactable2 = num != 0 && automationSequence.IsAutoActive;
		_go.GetButton("Panel/Info/Buttons/Start").interactable = interactable;
		_go.GetButton("Panel/Info/Buttons/Stop").interactable = interactable2;
		_go.SetActive("Panel/Info/Buttons/Clear Crew", isInVehicle);
	}

	private void RefreshFooter()
	{
		AutomationSequence automationSequence = GetAutomationSequence();
		_go.GetButton("Panel/Footer/Buttons/Clear Steps").interactable = automationSequence.steps.Count > 0;
	}

	private void OnManagementClick()
	{
		string message = Loc.Get("ui.deliveries.select.mo");
		List<EntityID> eids = (from c in Game.ctx.players.Human.crew.GetLiving()
			select c.peepId into eid
			where eid.IsValid && !Game.ctx.players.Human.schemes.IsInScheme(eid)
			select eid).ToList();
		Game.serv.ui.AddPopup(new EntitySelectionPopup(eids, DescribePeep, message, OnPeepSelection, null));
		static EntitySelectionPopup.EntityDescription DescribePeep(Entity peep)
		{
			CrewAssignment crewForPeep = Game.ctx.players.Human.crew.GetCrewForPeep(peep.Id);
			bool flag = !crewForPeep.IsInVehicle;
			bool flag2 = Game.ctx.players.Human.automation.HasAutomation(crewForPeep);
			string buttonTextOverride = (flag ? Loc.Get("ui.deliveries.crewassign.novehicle") : (flag2 ? Loc.Get("ui.deliveries.crewassign.busy") : Loc.Get("ui.deliveries.crewassign.valid")));
			return new EntitySelectionPopup.EntityDescription
			{
				message = peep.data.person.FullName,
				sprite = HUDUtil.GetCrewSprite(peep),
				interactable = !(flag || flag2),
				buttonTextOverride = buttonTextOverride
			};
		}
		void OnPeepSelection(EntityID peepId)
		{
			CrewAssignment crewForPeep = Game.ctx.players.Human.crew.GetCrewForPeep(peepId);
			AutoExec.SetAutomationCrew(_auto, crewForPeep);
			RefreshContents();
		}
	}

	private void MakeAddCard(GameObject container)
	{
		Object.Instantiate(_tmplAdd, container.transform).GetOrAddComponent<CardContext>().Set(-1, null, isAddCard: true);
	}

	private void InitStepCard(int i, GameObject card, AutomationStep step)
	{
		card.GetOrAddComponent<CardContext>().Set(i, step, isAddCard: false);
	}

	private void RefreshCard(GameObject card, AutomationSequence seq)
	{
		Button button = card.GetButton();
		CardContext ctx = card.GetComponent<CardContext>();
		if (ctx.isAddCard)
		{
			button.onClick.SetListener(OnAddClick);
			return;
		}
		button.onClick.SetListener(delegate
		{
			OnCardClick(ctx);
		});
		Button button2 = card.GetButton("Up");
		button2.onClick.SetListener(delegate
		{
			OnUpDownClick(ctx, -1);
		});
		button2.interactable = ctx.index > 0;
		Button button3 = card.GetButton("Down");
		button3.onClick.SetListener(delegate
		{
			OnUpDownClick(ctx, 1);
		});
		button3.interactable = ctx.index < seq.steps.Count - 1;
		Button button4 = card.GetButton("Next");
		button4.onClick.SetListener(delegate
		{
			SetNext(ctx);
		});
		button4.interactable = ctx.step.enabled;
		Button button5 = card.GetButton("Snooze");
		button5.onClick.SetListener(delegate
		{
			ToggleSnooze(ctx);
		});
		UIUtil.SetChildText(text: ctx.step.enabled ? Loc.Get("ui.deliveries.mute-step.icon") : Loc.Get("ui.deliveries.unmute-step.icon"), dialog: button5.gameObject);
		card.GetChild<TMP_InputField>("Icon").onSubmit.SetListener(delegate(string reqPos)
		{
			OnInputUpdate(ctx, reqPos);
		});
		RefreshCardText(ctx, seq);
		RefreshEditMode(card, ctx, seq);
	}

	private void RefreshEditMode(GameObject card, CardContext ctx, AutomationSequence seq)
	{
		bool flag = _selectedEditOrder != -1 && _selectedEditOrder == ctx.index;
		bool flag2 = _selectedEditOrder != -1;
		card.GetChild("Edit Overlay").SetActive(_inEditOrderMode);
		card.GetButton("Edit Overlay").onClick.SetListener(delegate
		{
			ToggleSelectedEdit(ctx);
		});
		card.GetButton("Edit Overlay").interactable = flag || (!flag && !flag2);
		card.GetImage("Edit Overlay").color = (flag ? new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Color32(170, 226, byte.MaxValue, byte.MaxValue));
		card.GetImage("Edit Overlay/Deco").color = (flag ? new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Color32(170, 170, 170, byte.MaxValue));
		card.GetButton("Edit Overlay/Add To Top").onClick.SetListener(delegate
		{
			Place(ctx, 0);
		});
		card.GetButton("Edit Overlay/Add To Bottom").onClick.SetListener(delegate
		{
			Place(ctx, 1);
		});
		card.GetChild("Edit Overlay/Add To Top").SetActive(!flag && flag2);
		card.GetChild("Edit Overlay/Add To Bottom").SetActive(!flag && flag2);
	}

	private void ToggleSelectedEdit(CardContext ctx)
	{
		if (_selectedEditOrder == -1)
		{
			_selectedEditOrder = ctx.index;
		}
		else
		{
			_selectedEditOrder = -1;
		}
		RefreshContainer();
	}

	private void OnInputUpdate(CardContext ctx, string reqPos)
	{
		AutomationSequence automationSequence = GetAutomationSequence();
		if (!int.TryParse(reqPos, out var result))
		{
			RefreshContainer();
			return;
		}
		int max = automationSequence.steps.Count - 1;
		int b = MathUtil.Clamp(result - 1, 0, max);
		AutoExec.MoveStep(_auto, ctx.index, b);
		RefreshContainer();
	}

	private void Place(CardContext ctx, int delta)
	{
		int selectedEditOrder = _selectedEditOrder;
		int b = MathUtil.Clamp(ctx.index + delta, 0, GetAutomationSequence().steps.Count - 1);
		AutoExec.MoveStep(_auto, selectedEditOrder, b);
		_selectedEditOrder = -1;
		RefreshContainer();
	}

	private void RefreshCardText(CardContext ctx, AutomationSequence seq)
	{
		string text = AutomationUtil.Describe(ctx.step, 1);
		if (ctx.step != null)
		{
			text = TextUtil.ColorDisabledIf(!ctx.step.enabled, text);
		}
		ctx.gameObject.SetText("Text", text);
		ctx.gameObject.SetText("Edit Overlay/Text", text);
		string text2 = ((ctx.step == null) ? "" : ((!ctx.step.enabled) ? Loc.Get("ui.deliveries.mute-step.icon") : ((ctx.index == seq.nextstep) ? Loc.Get("ui.deliveries.next-step.icon") : Loc.FormatNumber(ctx.index + 1))));
		ctx.gameObject.SetText("Edit Overlay/Icon", text2);
		ctx.gameObject.GetChild<TMP_InputField>("Icon").SetTextWithoutNotify(text2);
	}

	private void OnUpDownClick(CardContext ctx, int delta)
	{
		AutoExec.SwapSteps(_auto, ctx.index, ctx.index + delta);
		RefreshContainer();
	}

	private void SetNext(CardContext ctx)
	{
		AutomationSequence automationSequence = GetAutomationSequence();
		Game.ctx.players.Human.commands.FlushQueue(automationSequence.FindCrew().peepId, cancelActive: true);
		automationSequence.SetStep(ctx.index);
		automationSequence.SendAutomationChangedEvent(PlayerID.HumanPlayer);
		RefreshContainer();
	}

	private void ToggleSnooze(CardContext ctx)
	{
		AutomationSequence automationSequence = GetAutomationSequence();
		automationSequence.SetStepEnabled(ctx.index, !automationSequence.IsStepEnabled(ctx.index));
		automationSequence.SendAutomationChangedEvent(PlayerID.HumanPlayer);
		RefreshContainer();
	}

	private void OnAddClick()
	{
		AutoExec.AddAutoStep(_auto, new AutomationStep());
		RefreshContainer();
		AutomationSequence automationSequence = GetAutomationSequence();
		CardContext component = _container.transform.GetChild(automationSequence.steps.Count - 1).gameObject.GetComponent<CardContext>();
		OnCardClick(component);
	}

	private void OnAddFromPrevStep(int step)
	{
		AutomationSequence automationSequence = GetAutomationSequence();
		AutomationStep automationStep = automationSequence.steps[step];
		MovedItems items = new MovedItems
		{
			iscash = automationStep.items.iscash,
			res = automationStep.items.res,
			type = AmtChoiceType.Amount,
			qty = 0
		};
		AutoExec.AddAutoStep(_auto, new AutomationStep
		{
			target = automationStep.target,
			action = automationStep.action,
			items = items,
			enabled = automationStep.enabled,
			skipIfEmptyOnSell = automationStep.skipIfEmptyOnSell,
			skipIfFullOnBuy = automationStep.skipIfFullOnBuy
		});
		AutoExec.MoveStep(automationSequence.id, automationSequence.steps.Count - 1, step + 1);
		RefreshContainer();
		CardContext component = _container.transform.GetChild(step + 1).gameObject.GetComponent<CardContext>();
		OnCardClick(component);
	}

	private void OnCardClick(CardContext ctx)
	{
		_edit.StartEdit(GetAutomationSequence(), ctx.step, ctx.index);
	}

	public string GetStepEditProgress()
	{
		return _edit.GetStepEditProgress();
	}

	public string GetStepEditIconsDescription()
	{
		return _edit.GetDestinationPipsDescription();
	}

	internal void OnStepUpdated(int index, AutomationStep newstep)
	{
		AutoExec.ReplaceStep(_auto, index, newstep);
		UpdateCardContext(index, newstep);
		RefreshArrows();
		if (newstep.target.IsValid)
		{
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingDeliveryChanged, newstep.target, PlayerID.HumanPlayer));
		}
	}

	private void UpdateCardContext(int index, AutomationStep newstep)
	{
		_container.transform.WalkChildren(delegate(Transform tr)
		{
			CardContext component = tr.gameObject.GetComponent<CardContext>();
			if (component.index == index)
			{
				component.step = newstep;
				RefreshCardText(component, GetAutomationSequence());
			}
		}, recursive: false);
	}

	internal void OnStepDelete(int index)
	{
		AutoExec.RemoveStep(_auto, index);
		RefreshContents();
	}

	internal void OnEditNew(int step)
	{
		OnAddFromPrevStep(step);
	}

	internal void OnEditNext(int current)
	{
		AutomationSequence automationSequence = GetAutomationSequence();
		int index = MathUtil.Modulus(current + 1, automationSequence.steps.Count);
		OnEdit(index);
	}

	internal void OnEditPrev(int current)
	{
		AutomationSequence automationSequence = GetAutomationSequence();
		int index = MathUtil.Modulus(current - 1, automationSequence.steps.Count);
		OnEdit(index);
	}

	internal void OnEdit(int index)
	{
		CardContext component = _container.transform.GetChild(index).gameObject.GetComponent<CardContext>();
		OnCardClick(component);
	}
}

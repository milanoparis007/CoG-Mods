using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Actions;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Crew;

public sealed class CrewDialog : BaseHUDDialog
{
	private GameObject _cardTemplate;

	private GameObject _sectionTemplate;

	private GameObject _cmdButtonTmpl;

	private GameObject _allSections;

	private List<CrewCardContext> _allCards;

	private ToggleGroup _toggleGroup;

	private CrewCardContext.Config _cardConfig;

	private List<CrewCardSectionDef> _sectionDefs;

	public Dictionary<CrewCardType, bool> sectionEditMode = new Dictionary<CrewCardType, bool>();

	private CrewCardContext editSelected;

	private List<object> _freezerequests = new List<object>();

	private const string TMPL_CREW_CARD = "Templates/Crew Card";

	private const string TMPL_CREW_SECTION = "Templates/Crew Section";

	private const string TMPL_COMMAND_BTN = "Templates/Crew Command Button";

	private const string HEADER_TEXT = "Header/Text";

	private const string HEADER_BUTTON = "Header/Edit";

	private const string HEADER_BOTTOM = "Header/Bottom Deco";

	private const string CONTAINER = "Scroll View List/Viewport/Content";

	private const string TEMPLATES = "Templates";

	private const string SECTION_HEADER_TEXT = "Header/Text";

	private const string SECTION_CONTAINER = "Cards";

	private const string SECTION_EDIT_BUTTON = "Header/Edit Button";

	private const string SECTION_BG = "Header/BG/Pattern";

	public static string ETH_BG_PATH = "UI Images/Decos/Ind 06";

	public static string DEFAULT_BG_PATH = "UI Images/Decos/Hud 09";

	private bool _isProcessingToggleValueChanged;

	public static string TOP_DECO_PARENT = "Header/Top Deco";

	public static string TOP_DECO = "Header/Top Deco/Top Chrome";

	public static string BOTTOM_DECO_PARENT = "Header/Bottom Deco";

	public static string BOTTOM_DECO = "Header/Bottom Deco/Bottom Chrome";

	public override bool ShowAtStartup => true;

	public override TweenType Tween => TweenType.Left;

	public override UIReference UIReference => UIElements.CrewDialog;

	private Dictionary<CrewCardType, List<CrewCardInfoInitData>> OrderingDictionary => Game.ctx.players.Human.crew.OrderingDictionary;

	internal GameObject CommandButtonTemplate => _cmdButtonTmpl;

	internal override void Initialize()
	{
		base.Initialize();
		_sectionDefs = new List<CrewCardSectionDef>
		{
			new CrewCardSectionDef(CrewCardType.CrewMuscle, Loc.Get("ui.crewinfo.crewtype.muscle"), ComparePeeps),
			new CrewCardSectionDef(CrewCardType.CrewManagerOrBuilding, Loc.Get("ui.crewinfo.crewtype.building"), CompareBuildings),
			new CrewCardSectionDef(CrewCardType.CrewScheming, Loc.Get("ui.crewinfo.crewtype.in-scheme"), ComparePeeps),
			new CrewCardSectionDef(CrewCardType.CrewJobs, Loc.Get("ui.crewinfo.crewtype.deliveries"), CompareAutomation),
			new CrewCardSectionDef(CrewCardType.CrewUnassigned, Loc.Get("ui.crewinfo.crewtype.none-crew"), ComparePeeps),
			new CrewCardSectionDef(CrewCardType.VehicleUnassigned, Loc.Get("ui.crewinfo.crewtype.none-vehicle"), CompareVehicle)
		};
		_cardTemplate = _go.GetChild("Templates/Crew Card");
		_sectionTemplate = _go.GetChild("Templates/Crew Section");
		_cmdButtonTmpl = _go.GetChild("Templates/Crew Command Button");
		_allSections = _go.GetChild("Scroll View List/Viewport/Content");
		_toggleGroup = _allSections.GetComponent<ToggleGroup>();
		_allCards = new List<CrewCardContext>();
		_cardConfig = new CrewCardContext.Config(_toggleGroup);
		_allSections.SetActive(value: true);
		_allSections.DestroyAllChildren();
		_go.GetChild("Templates").SetActive(value: false);
		EnableEthReplacements();
		Game.serv.mouseovers.Register(MouseoverType.CrewCardContents, new CrewCardContentsMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CrewCardPoints, new CrewCardPointsMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CrewCardCommand, new CrewCardCommandMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CrewCardPerson, new CrewCardPersonMouseover());
		Game.ctx.scriptevents.AddListener(GameScriptEventType.ScriptAfterStarted, OnScriptChanged);
		Game.ctx.scriptevents.AddListener(GameScriptEventType.ScriptAfterStopped, OnScriptChanged);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnPlayerTurn);
		Game.ctx.events.AddListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandStarted, OnPlayerCrewSingle);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandExecutedOneTurn, OnPlayerCrewSingle);
		Game.ctx.events.AddListener(SessionEventType.CrewHealthChanged, OnPlayerCrewSingle);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleHealthChanged, OnPlayerCrewSingle);
		Game.ctx.events.AddListener(SessionEventType.CrewActionsChanged, OnPlayerCrewSingle);
		Game.ctx.events.AddListener(SessionEventType.CrewFedArrest, OnPlayerCrewSingle);
		Game.ctx.events.AddListener(SessionEventType.CrewAutomationChanged, OnPlayerAutomationSingle);
		Game.ctx.events.AddListener(SessionEventType.PlayerAutomationAddedSequence, OnPlayerAutomationAdded);
		Game.ctx.events.AddListener(SessionEventType.PlayerAutomationReassignedSequence, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.PlayerAutomationRemovedSequence, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingClearControlImmediate, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.BuildingConstructionStateChanged, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberAdded, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberKilled, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleCreated, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleRemoved, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleReassigned, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.CrewBuildingReassigned, OnPlayerCrewAll);
		Game.ctx.events.AddListener(SessionEventType.SchemeUpdated, OnPlayerCrewAll);
		InitializeDictionaries();
		RecreateAllCards();
		RefreshCards();
	}

	internal override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingClearControlImmediate, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingConstructionStateChanged, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberAdded, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberKilled, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleCreated, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleRemoved, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleReassigned, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewBuildingReassigned, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.SchemeUpdated, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewAutomationChanged, OnPlayerAutomationSingle);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerAutomationAddedSequence, OnPlayerAutomationAdded);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerAutomationReassignedSequence, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerAutomationRemovedSequence, OnPlayerCrewAll);
		Game.ctx.events.RemoveListener(SessionEventType.CrewFedArrest, OnPlayerCrewSingle);
		Game.ctx.events.RemoveListener(SessionEventType.CrewActionsChanged, OnPlayerCrewSingle);
		Game.ctx.events.RemoveListener(SessionEventType.CrewHealthChanged, OnPlayerCrewSingle);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleHealthChanged, OnPlayerCrewSingle);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandExecutedOneTurn, OnPlayerCrewSingle);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandStarted, OnPlayerCrewSingle);
		Game.ctx.events.RemoveListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnPlayerTurn);
		Game.ctx.scriptevents.RemoveListener(GameScriptEventType.ScriptAfterStarted, OnScriptChanged);
		Game.ctx.scriptevents.RemoveListener(GameScriptEventType.ScriptAfterStopped, OnScriptChanged);
		Game.serv.mouseovers.Unregister(MouseoverType.CrewCardCommand);
		Game.serv.mouseovers.Unregister(MouseoverType.CrewCardContents);
		Game.serv.mouseovers.Unregister(MouseoverType.CrewCardPoints);
		Game.serv.mouseovers.Unregister(MouseoverType.CrewCardPerson);
		_cardConfig = null;
		_allCards.Clear();
		_toggleGroup = null;
		_allSections.DestroyAllChildren();
		_allSections = null;
		_sectionDefs = null;
		_cardTemplate = (_sectionTemplate = (_cmdButtonTmpl = null));
		base.Release();
	}

	protected override void RefreshContents()
	{
		base.RefreshContents();
		RefreshCards();
	}

	private void OnPlayerTurn(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RefreshCards();
		}
	}

	private void InitializeDictionaries()
	{
		foreach (CrewCardSectionDef sectionDef in _sectionDefs)
		{
			if (!OrderingDictionary.ContainsKey(sectionDef.type))
			{
				OrderingDictionary.Add(sectionDef.type, new List<CrewCardInfoInitData>());
			}
			sectionEditMode[sectionDef.type] = false;
		}
	}

	private void RecreateAllCards()
	{
		if (!IsFrozen())
		{
			RefreshHeader();
			_allCards.Clear();
			_allSections.EnsureChildCount(_sectionDefs.Count, _sectionTemplate);
			for (int i = 0; i < _sectionDefs.Count; i++)
			{
				GameObject gameObject = _allSections.transform.GetChild(i).gameObject;
				RecreateSection(gameObject, _sectionDefs[i]);
			}
			_allSections.ResetAllChildScrollViews();
			ForceRebuildLayoutImmediate();
			RepositionBottomChrome();
		}
	}

	private void RecreateSection(GameObject section, CrewCardSectionDef def)
	{
		List<CrewCardInfoInitData> infos = GenerateCardDataFor(def.type).ToList();
		WriteToSectionData(infos, def);
		if (OrderingDictionary[def.type].Count > 0)
		{
			section.SetActive(value: true);
			section.SetText("Header/Text", def.name);
			GameObject container = section.GetChild("Cards");
			section.GetButton("Header/Edit Button").onClick.SetListener(delegate
			{
				ToggleSectionEditMode(def.type, container);
			});
			section.GetButton("Header/Edit Button").gameObject.SetActive(HasEnoughForEditing());
			RecreateSectionCards(container, def.type);
		}
		else
		{
			section.SetActive(value: false);
		}
		var (sprite, flag) = TryFindEthBGReplacement();
		if (sprite != null)
		{
			Image image = section.GetImage("Header/BG/Pattern");
			section.SetImage("Header/BG/Pattern", sprite);
			if (flag)
			{
				image.pixelsPerUnitMultiplier = 4f;
			}
			else
			{
				image.pixelsPerUnitMultiplier = 3f;
			}
		}
		bool HasEnoughForEditing()
		{
			if (def.type != CrewCardType.CrewJobs)
			{
				return OrderingDictionary[def.type].Count > 1;
			}
			return OrderingDictionary[def.type].Count > 2;
		}
	}

	private (Sprite, bool) TryFindEthBGReplacement()
	{
		Label ethnicity = Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
		bool item = true;
		if (!PlayerCrew.HasEthPackAndIsEth(ethnicity))
		{
			return (Resources.Load<Sprite>(DEFAULT_BG_PATH), false);
		}
		Sprite sprite = Resources.Load<Sprite>(ETH_BG_PATH + ethnicity.ToString().ToUpper());
		if (sprite == null)
		{
			sprite = Resources.Load<Sprite>(DEFAULT_BG_PATH);
			item = false;
		}
		return (sprite, item);
	}

	private void ToggleSectionEditMode(CrewCardType type, GameObject container)
	{
		DoToggleInDict();
		UpdatePaddingSpacingAfterToggle();
		Game.ctx.selection.ClearActive();
		editSelected = null;
		RefreshCards();
		ForceRebuildLayoutImmediate();
		RepositionBottomChrome();
		void DoToggleInDict()
		{
			foreach (CrewCardSectionDef sectionDef in _sectionDefs)
			{
				if (sectionDef.type != type)
				{
					sectionEditMode[sectionDef.type] = false;
				}
				if (sectionDef.type == type)
				{
					sectionEditMode[sectionDef.type] = !sectionEditMode[sectionDef.type];
				}
			}
		}
		void UpdatePaddingSpacingAfterToggle()
		{
			for (int i = 0; i < _sectionDefs.Count; i++)
			{
				VerticalLayoutGroup component = _allSections.transform.GetChild(i).gameObject.GetChild("Cards").GetComponent<VerticalLayoutGroup>();
				component.spacing = 0f;
				component.padding.bottom = 0;
				component.padding.top = 36;
			}
			if (sectionEditMode[type])
			{
				VerticalLayoutGroup component2 = container.GetComponent<VerticalLayoutGroup>();
				component2.spacing = 2f;
				component2.padding.bottom = 2;
				component2.padding.top = 38;
			}
		}
	}

	private void ClearEditMode()
	{
		for (int i = 0; i < _sectionDefs.Count; i++)
		{
			VerticalLayoutGroup component = _allSections.transform.GetChild(i).gameObject.GetChild("Cards").GetComponent<VerticalLayoutGroup>();
			component.spacing = 0f;
			component.padding.bottom = 0;
			component.padding.top = 36;
			sectionEditMode[_sectionDefs[i].type] = false;
		}
		editSelected = null;
		RefreshCards();
		ForceRebuildLayoutImmediate();
		RepositionBottomChrome();
	}

	public bool IsMovingCard()
	{
		return GetMovingCard() != null;
	}

	public CrewCardContext GetMovingCard()
	{
		return editSelected;
	}

	public void Place(CrewCardContext clicked, int delta)
	{
		CrewCardContext movingCard = GetMovingCard();
		CrewCardType type = movingCard.data.type;
		CrewCardInfoInitData item = new CrewCardInfoInitData(clicked.data);
		CrewCardInfoInitData item2 = new CrewCardInfoInitData(movingCard.data);
		OrderingDictionary[type].Remove(item2);
		int index = MathUtil.Clamp(OrderingDictionary[clicked.data.type].IndexOf(item) + delta, 0, OrderingDictionary[type].Count);
		OrderingDictionary[type].Insert(index, item2);
		editSelected = null;
		RecreateAllCards();
	}

	public void ToggleEditSelected(CrewCardContext clicked)
	{
		if (editSelected == null)
		{
			editSelected = clicked;
		}
		else
		{
			editSelected = null;
		}
	}

	public bool IsCardEditSelected(CrewCardContext card)
	{
		return editSelected == card;
	}

	private void RecreateSectionCards(GameObject cardContainer, CrewCardType type)
	{
		List<CrewCardInfoInitData> list = OrderingDictionary[type];
		cardContainer.EnsureChildCount(list.Count, _cardTemplate);
		for (int i = 0; i < list.Count; i++)
		{
			GameObject gameObject = cardContainer.transform.GetChild(i).gameObject;
			CrewCardContext crewCardContext = CrewCardContext.MakeCard(_cardConfig, gameObject);
			_allCards.Add(crewCardContext);
			CrewCardInfo data = new CrewCardInfo(list[i]);
			crewCardContext.Reinitialize(data);
		}
	}

	private void WriteToSectionData(IEnumerable<CrewCardInfoInitData> infos, CrewCardSectionDef def)
	{
		List<CrewCardInfoInitData> section = OrderingDictionary[def.type];
		Func<CrewCardInfoInitData, CrewCardInfoInitData, bool> comparator = def.comparator;
		AddNewItemsAndUpdate();
		RemoveItemsNotInNewData();
		MaybeEnsureAddDeliveryCardIsLast();
		void AddNewItemsAndUpdate()
		{
			foreach (CrewCardInfoInitData info in infos)
			{
				int num = section.FindIndex((CrewCardInfoInitData x) => comparator(x, info));
				if (num != -1)
				{
					section[num] = info;
				}
				else
				{
					section.Add(info);
				}
			}
		}
		void MaybeEnsureAddDeliveryCardIsLast()
		{
			if (def.type == CrewCardType.CrewJobs)
			{
				CrewCardInfoInitData item = section.Find((CrewCardInfoInitData x) => x.automation.id == AutomationID.INVALID.id);
				section.Remove(item);
				section.Add(item);
			}
		}
		void RemoveItemsNotInNewData()
		{
			List<CrewCardInfoInitData> list = new List<CrewCardInfoInitData>();
			foreach (CrewCardInfoInitData info in section)
			{
				if (infos.ToList().FindIndex((CrewCardInfoInitData x) => comparator(x, info)) == -1)
				{
					list.Add(info);
				}
			}
			foreach (CrewCardInfoInitData item2 in list)
			{
				section.Remove(item2);
			}
		}
	}

	public void RefreshCards()
	{
		if (IsFrozen())
		{
			return;
		}
		RefreshHeader();
		foreach (CrewCardContext allCard in _allCards)
		{
			allCard.RefreshCard();
		}
	}

	private void RefreshHeader()
	{
		PlayerCrew crew = Game.ctx.players.Human.crew;
		int num = (int)crew.CrewGrowth.currentCap;
		int livingCrewCount = crew.LivingCrewCount;
		string text = Loc.Get("ui.crewlist.header.currmax", "current", livingCrewCount, "max", num);
		_go.SetText("Header/Text", text);
		_go.SetButtonListener("Header/Edit", OnHeaderEditClick);
	}

	private void OnHeaderEditClick()
	{
		Game.ctx.selection.ClearActive();
		Game.ctx.hud.crewinfolist.Show();
	}

	private void OnPlayerCrewAll(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RecreateAllCards();
		}
	}

	private void OnPlayerCrewSingle(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			CrewCardContext crewCardContext = FindCardFor(sev.eid);
			if (crewCardContext != null)
			{
				crewCardContext.RefreshCard();
			}
		}
	}

	private void OnPlayerAutomationAdded(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RecreateAllCards();
			CrewCardContext crewCardContext = _allCards.Last((CrewCardContext c) => c.data.type == CrewCardType.CrewJobs && c.data.automation.IsValid);
			if (crewCardContext != null)
			{
				SelectCard(crewCardContext, fromCardClick: true);
			}
		}
	}

	private void OnPlayerAutomationSingle(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer && sev.ctx is AutomationSequence automationSequence)
		{
			CrewCardContext crewCardContext = FindCardFor(automationSequence.id);
			if (crewCardContext != null)
			{
				crewCardContext.RefreshCard();
			}
		}
	}

	private void OnScriptChanged(GameScriptEvent ev)
	{
		if (ev.data.type == GameScriptType.CarNavigation)
		{
			CrewCardContext crewCardContext = FindCardFor(ev.eid);
			if (crewCardContext != null)
			{
				crewCardContext.RefreshCard();
			}
		}
	}

	internal void OnToggleValueChanged(bool isOn, CrewCardContext ctx)
	{
		if (_isProcessingToggleValueChanged)
		{
			return;
		}
		try
		{
			_isProcessingToggleValueChanged = true;
			if (isOn)
			{
				SelectCard(ctx, fromCardClick: true);
			}
			else
			{
				DeselectCard(ctx, fromCardClick: true);
			}
		}
		finally
		{
			_isProcessingToggleValueChanged = false;
		}
	}

	private void OnCurrentActiveChanged(SessionEvent sev)
	{
		if (_isProcessingToggleValueChanged)
		{
			return;
		}
		try
		{
			_isProcessingToggleValueChanged = true;
			if (sev.ctx is Entity entity)
			{
				DeselectCard(FindCardFor(entity.Id), fromCardClick: false);
			}
			Entity entity2 = sev.eid.FindEntity();
			if (entity2 != null)
			{
				SelectCard(FindCardFor(entity2.Id), fromCardClick: false);
			}
		}
		finally
		{
			_isProcessingToggleValueChanged = false;
		}
	}

	private void DeselectCard(CrewCardContext ctx, bool fromCardClick)
	{
		if (fromCardClick)
		{
			Game.ctx.selection.ClearActive();
		}
		SetCardState(ctx, isOn: false);
	}

	private void SelectCard(CrewCardContext ctx, bool fromCardClick)
	{
		if (fromCardClick)
		{
			Entity entity = ((ctx == null) ? null : ctx.data.FindSelectionTargetOnBoard());
			if (entity != null)
			{
				Game.ctx.selection.SetActive(entity);
			}
			else
			{
				Game.ctx.selection.ClearActive();
			}
			ctx.data.gen.OnCardClick();
		}
		SetCardState(ctx, isOn: true);
		ClearEditMode();
	}

	private CrewCardContext FindCardFor(EntityID target)
	{
		if (target.IsNotValid)
		{
			return null;
		}
		foreach (CrewCardContext allCard in _allCards)
		{
			EntityID entityID = allCard.data.FindSelectionTargetOnBoard()?.Id ?? EntityID.INVALID;
			if (entityID.IsValid && entityID == target)
			{
				return allCard;
			}
			EntityID peepId = allCard.data.crew.peepId;
			if (peepId.IsValid && peepId == target)
			{
				return allCard;
			}
		}
		return null;
	}

	private CrewCardContext FindCardFor(AutomationID id)
	{
		foreach (CrewCardContext allCard in _allCards)
		{
			if (AutomationID.Equals(allCard.data.automation, id))
			{
				return allCard;
			}
		}
		return null;
	}

	private void SetCardState(CrewCardContext ctx, bool isOn)
	{
		if (!(ctx == null))
		{
			ctx.toggle.SetIsOnWithoutNotify(isOn);
			ctx.RefreshCard();
			ForceRebuildLayoutImmediate();
			RepositionBottomChrome();
			if (isOn && ctx.data.type == CrewCardType.CrewMuscle)
			{
				Game.ctx.sfx.PlayCrewSelect();
			}
		}
	}

	private void RepositionBottomChrome()
	{
		float yoffset = 0f;
		RectTransform container = _allSections.GetComponent<RectTransform>();
		RectTransform bar = _go.GetChild("Header/Bottom Deco").GetComponent<RectTransform>();
		bar.anchoredPosition = bar.anchoredPosition.SetY(container.sizeDelta.y * -1f + yoffset);
		TimerUtil.RunNextFrame(delegate
		{
			bar.anchoredPosition = bar.anchoredPosition.SetY(container.sizeDelta.y * -1f + yoffset);
		});
	}

	private void ForceRebuildLayoutImmediate()
	{
		_allSections.ForceRebuildLayoutImmediate();
		_allSections.ForceRebuildLayoutImmediate();
		_allSections.ForceRebuildLayoutImmediate();
	}

	public IEnumerable<CrewCardInfoInitData> GenerateCardDataFor(CrewCardType type)
	{
		PlayerInfo player = Game.ctx.players.Human;
		switch (type)
		{
		case CrewCardType.CrewMuscle:
			foreach (CrewAssignment item2 in player.crew.AllCrew)
			{
				if (item2.IsNotDead && item2.IsInVehicle)
				{
					bool num = player.automation.HasAutomation(item2);
					bool flag2 = player.schemes.IsInScheme(item2.GetPeep());
					if (!num && !flag2)
					{
						yield return new CrewCardInfoInitData(type, item2);
					}
				}
			}
			break;
		case CrewCardType.CrewScheming:
			foreach (CrewAssignment item3 in player.crew.AllCrew)
			{
				if (item3.IsNotDead && player.schemes.IsInScheme(item3.GetPeep()))
				{
					yield return new CrewCardInfoInitData(type, item3);
				}
			}
			break;
		case CrewCardType.CrewJobs:
			foreach (AutomationSequence allSequence in player.automation.GetAllSequences())
			{
				if (allSequence.FindCrew().GetPeep() == null || !player.schemes.IsInScheme(allSequence.FindCrew().GetPeep()))
				{
					yield return new CrewCardInfoInitData(type, allSequence.FindCrew(), null, null, allSequence.id);
				}
			}
			yield return new CrewCardInfoInitData(type);
			break;
		case CrewCardType.CrewUnassigned:
			foreach (CrewAssignment item4 in player.crew.AllCrew)
			{
				bool flag = player.schemes.IsInScheme(item4.GetPeep());
				if (item4.IsNotDead && item4.IsNotAssigned && !flag)
				{
					yield return new CrewCardInfoInitData(type, item4);
				}
			}
			break;
		case CrewCardType.CrewManagerOrBuilding:
			foreach (EntityID item5 in player.territory.GetAllControlledBuildingsUnsafe())
			{
				Entity item = ModulesUtil.GetManagerOrNull(item5.FindEntity()).manager;
				CrewAssignment? crew = ((item != null) ? new CrewAssignment?(player.crew.GetCrewForPeep(item.Id)) : ((CrewAssignment?)null));
				if ((crew.HasValue && !player.schemes.IsInScheme(crew.Value.GetPeep())) || !crew.HasValue)
				{
					yield return new CrewCardInfoInitData(type, building: item5, crew: crew);
				}
			}
			break;
		case CrewCardType.VehicleUnassigned:
			foreach (EntityID allUnassignedVehicle in player.crew.AllUnassignedVehicles)
			{
				yield return new CrewCardInfoInitData(type, null, null, allUnassignedVehicle);
			}
			break;
		}
	}

	public bool IsFrozen()
	{
		return _freezerequests.Count > 0;
	}

	public void AddFreezeRequest(object obj)
	{
		_freezerequests.Add(obj);
	}

	public void RemoveFreezeRequest(object obj)
	{
		_freezerequests.Remove(obj);
		if (!IsFrozen())
		{
			RecreateAllCards();
		}
	}

	public bool ComparePeeps(CrewCardInfoInitData a, CrewCardInfoInitData b)
	{
		return a.crew.peepId == b.crew.peepId;
	}

	public bool CompareBuildings(CrewCardInfoInitData a, CrewCardInfoInitData b)
	{
		return a.building == b.building;
	}

	public bool CompareAutomation(CrewCardInfoInitData a, CrewCardInfoInitData b)
	{
		return a.automation.Equals(b.automation);
	}

	public bool CompareVehicle(CrewCardInfoInitData a, CrewCardInfoInitData b)
	{
		return a.emptyVehicle == b.emptyVehicle;
	}

	public void EnableEthReplacements()
	{
		GameObject ethChild = _go.GetEthChild(TOP_DECO, GetEthnicityString());
		_go.GetChild(TOP_DECO_PARENT).SetActiveOnlyOneChild(ethChild);
		GameObject ethChild2 = _go.GetEthChild(BOTTOM_DECO, GetEthnicityString());
		_go.GetChild(BOTTOM_DECO_PARENT).SetActiveOnlyOneChild(ethChild2);
		static string GetEthnicityString()
		{
			if (!PlayerCrew.HasEthPackForCurrEth())
			{
				return "";
			}
			return Game.ctx.session.scenario.newgamepars.playerdetails.player.ethnicity.ToString().ToUpper();
		}
	}
}

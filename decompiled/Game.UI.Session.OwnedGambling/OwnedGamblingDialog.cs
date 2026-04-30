using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Session.Convo;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedGambling;

public sealed class OwnedGamblingDialog : HUDView<OwnedGamblingModel, OwnedGamblingDialog, OwnedGamblingController>
{
	private GameObject _tmplButton;

	private GameObject _tmplAddAmenity;

	private GameObject _tmplAmenityCard;

	private GameObject _tmplGamblerCard;

	private GameObject _toggles;

	private GameObject _amenties;

	public const string MODULE_BUTTONS_PARENT = "Background/Modules";

	public const string MODULE_BUTTONS = "Background/Modules/Buttons";

	public const string TMPL_CONTAINER = "Templates";

	public const string TMPL_NEARBY_CREW = "Templates/Nearby Crew";

	public const string TMPL_INVCARD = "Templates/Inventory Card";

	public const string TMPL_MODULE_TOGGLE = "Templates/Module Toggle";

	public const string TMPL_AMENITY = "Templates/Amenity Card";

	public const string TMPL_AMENITY_ADD = "Templates/Amenity Add Card";

	public const string TMPL_GAMBLER_CARD = "Templates/Gambler Card";

	public const string PANEL_CLOSE = "Panel/Close";

	public const string PANEL_MODULE_NAME = "Panel/Header/Module Name";

	public const string PANEL_MODULE_ICON = "Panel/Header/Module Icon";

	public const string PANEL_LEFT = "Panel/Header/Left";

	public const string PANEL_RIGHT = "Panel/Header/Right";

	public const string PANEL_DAMAGE_BOX = "Panel/Damage";

	public const string PANEL_DAMAGE_TEXT = "Panel/Damage/Text";

	public const string PANEL_MONEY_TEXT = "Panel/Header/Money Text";

	public const string PANEL_POP_TEXT = "Panel/Header/Pop Text";

	public const string PANEL_STATUS_TEXT = "Panel/Header/Description";

	public const string BTN_UPGRADE = "Panel/Header/Upgrade";

	public const string BTN_MOVECASH = "Panel/Header/Move Cash";

	public const string PANEL_MANAGER_FRAME = "Panel/Header/Manager/Portrait";

	public const string PANEL_MANAGER_BUTTON = "Panel/Header/Manager/Button";

	public const string PANEL_MANAGER_IMAGE = "Panel/Header/Manager/Portrait/Portrait";

	public const string PANEL_MANAGER_NAME = "Panel/Header/Manager Name";

	public const string AMENITIES_SCROLL_LIST = "Landing/Scroll View List";

	public const string AMENITIES_CONTAINER = "Landing/Scroll View List/Viewport/Content";

	public const string PANEL_FOOTER_LIST = "Panel/Footer/Scroll View List/Viewport/Content";

	public const string NEARBY_CREW_PORTRAIT = "Background/Basic Portrait/Portrait";

	public const string NEARBY_CREW_NAME = "Text";

	public const string ADD_BUTTON = "Add Amenity Button";

	public const string CARD_TITLE = "Title";

	public const string CARD_BANNER = "Banner/Image";

	public const string CARD_BACKGROUND = "Background";

	public const string CARD_DESTROY = "Destroy";

	public const string CARD_STATUS_ICON = "Status Icon";

	public const string CARD_TURN_SUMMARY = "Turn Summary";

	public const string CARD_BUILD_TIME = "Build Line";

	public const string CARD_GAMBLERS_HEADER = "Gamblers Header";

	public const string CARD_GAMBLERS_LIST = "Gamblers";

	public const string GCARD_TEXT = "Text";

	public const string GCARD_CASH = "Money";

	public const string GCARD_GOTO_BTN = "Buttons/Goto";

	public const string GCARD_BAN_BTN = "Buttons/Ban";

	public const string GCARD_PORTRAIT = "Portrait/Portrait";

	public const string GCARD_DEBT_OVERLAY = "Portrait/Debt";

	public static string TOP_DECO_PARENT = "Background/Modules/Top";

	public static string TOP_DECO = "Background/Modules/Top/Top Chrome";

	public static string BOTTOM_DECO_PARENT = "Background/Modules/Bottom";

	public static string BOTTOM_DECO = "Background/Modules/Bottom/Bottom Chrome";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.HUDOwnedGambling;

	private GamblingSettings GamblingSettings => Game.serv.globals.settings.gambling;

	private PlayerGambling HumanGambling => Game.ctx.players.Human.gambling;

	internal void Show(VisitState visit)
	{
		base.Controller.SetModel(visit);
		base.Show();
	}

	public override void Show()
	{
	}

	internal override void Initialize()
	{
		base.Initialize();
		_go.SetButtonListener("Panel/Close", base.Controller.OnCloseButton);
		_go.SetButtonListener("Panel/Header/Right", delegate
		{
			base.Controller.OnSwitchBuildingButton(1);
		});
		_go.SetButtonListener("Panel/Header/Left", delegate
		{
			base.Controller.OnSwitchBuildingButton(-1);
		});
		_tmplButton = _go.GetChild("Templates/Module Toggle");
		_tmplAmenityCard = _go.GetChild("Templates/Amenity Card");
		_tmplAddAmenity = _go.GetChild("Templates/Amenity Add Card");
		_tmplGamblerCard = _go.GetChild("Templates/Gambler Card");
		_toggles = _go.GetChild("Background/Modules/Buttons");
		_amenties = _go.GetChild("Landing/Scroll View List/Viewport/Content");
		_toggles.GetComponent<ToggleGroup>().allowSwitchOff = true;
		_go.GetChild("Templates").SetActive(value: false);
		Game.ctx.events.AddListener(SessionEventType.BuildingHealthChanged, OnBuildingHealthChanged);
	}

	internal override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.BuildingHealthChanged, OnBuildingHealthChanged);
		base.Release();
	}

	internal GameObject GetTmpl(string path)
	{
		return _go.GetChild(path);
	}

	protected override void OnAfterShow()
	{
		base.OnAfterShow();
		Game.ctx.sfx.PlayOwnedBizShow(base.Model.visit);
		_toggles.GetComponent<ToggleGroup>().allowSwitchOff = false;
		_go.GetChild("Landing/Scroll View List").ResetAllChildScrollViews();
		Game.serv.mouseovers.Register(MouseoverType.GamblingAmenity, new GamblingAmenityMouseover());
		Game.serv.mouseovers.Register(MouseoverType.GamblingAmenityStats, new GamblingAmenityStatsMouseover());
		Game.serv.mouseovers.Register(MouseoverType.GamblingDebtor, new GamblingDebtorMouseover());
		Game.serv.mouseovers.Register(MouseoverType.GamblingAmenityStatus, new AmenityStatusMouseover());
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOwnedBizOpened, PlayerID.HumanPlayer);
	}

	protected override void OnBeforeHide()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.GamblingAmenity);
		Game.serv.mouseovers.Unregister(MouseoverType.GamblingDebtor);
		Game.serv.mouseovers.Unregister(MouseoverType.GamblingAmenityStats);
		Game.serv.mouseovers.Unregister(MouseoverType.GamblingAmenityStatus);
		if (_toggles != null)
		{
			_toggles.GetComponent<ToggleGroup>().allowSwitchOff = true;
		}
		base.Controller?.OnBeforeViewHide();
		base.OnBeforeHide();
	}

	private void OnBuildingHealthChanged(SessionEvent sev)
	{
		if (base.IsShowing && base.Model.visit.building != null && sev.eid == base.Model.visit.building.Id)
		{
			RefreshContents();
		}
	}

	internal void ControllerRequestsFullRefresh()
	{
		RefreshContents();
	}

	internal void ControllerRequestsModuleTab(ModuleSlot slotdef)
	{
		ModuleToggleContext moduleToggleContext = FindModuleToggleFor(slotdef);
		if (moduleToggleContext != null)
		{
			moduleToggleContext.gameObject.GetComponentInChildren<Toggle>().isOn = true;
		}
	}

	protected override void RefreshContents()
	{
		RefreshMoneyDisplay();
		RefreshModuleButtons();
		RefreshAmenities();
		RefreshHeaderAndManager();
		RefreshDamage();
		RefreshFooter();
	}

	private PlayerGambling.GamblingHouseStatus GetGamblingHouseStatus()
	{
		return base.Model.visit.GetPlayer().gambling.GetGamblingHouseStatus(base.Model.visit.building);
	}

	private void RefreshMoneyDisplay()
	{
		PlayerGambling.GamblingHouseStatus gamblingHouseStatus = GetGamblingHouseStatus();
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		if (gamblingHouseStatus.IsOpenForBusiness)
		{
			stringBuilder.AppendLine(Loc.IconLine(valid: true, Loc.Get("ui.ownedcasino.status.ok")));
		}
		if (!gamblingHouseStatus.isManagerPresent)
		{
			stringBuilder.AppendLine(Loc.IconLine(valid: false, Loc.Get("ui.ownedcasino.status.needmanager")));
		}
		if (!gamblingHouseStatus.isNotDamaged)
		{
			stringBuilder.AppendLine(Loc.IconLine(valid: false, Loc.Get("ui.ownedcasino.status.damaged")));
		}
		stringBuilder.AppendLine(Loc.IconLine(gamblingHouseStatus.isCashSufficient, Loc.Get("ui.ownedcasino.money", "needed", Loc.Money(gamblingHouseStatus.cashNeededToOperate))));
		_go.SetText("Panel/Header/Description", stringBuilder.ToStringAndReturnToPool());
		Fixnum fixnum = base.Model.Module.data.amenities.SelectAndSum((AmenityData data) => data.lastTurnResults.Delta.cash);
		_go.SetText("Panel/Header/Money Text", Loc.Get("ui.ownedcasino.money.number", "money", Loc.Money(gamblingHouseStatus.cashCurrent), "delta", TextUtil.ColorGreenRed(fixnum, Loc.Price(fixnum))));
		ModQuery q = base.Model.Module.MakeManagerBasedModQuery(Game.ctx.players.Human, base.Model.visit.building);
		int potentialGamblingCustomers = BuildingUtil.GetPotentialGamblingCustomers(base.Model.visit.building, q);
		_go.SetText("Panel/Header/Pop Text", Loc.FormatNumber(potentialGamblingCustomers));
		bool valueOrDefault = (ModulesUtil.FindModuleDef(base.Model.Module.config.gambling.upgradeModule) as GamblingModuleConfig)?.gambling?.installVisReqs.AllPass(base.Model.visit, default(ConvoButtonState)) == true;
		_go.SetButtonListener("Panel/Header/Upgrade", delegate
		{
			base.Controller.OnUpgradeButton();
		});
		_go.GetButton("Panel/Header/Upgrade").interactable = valueOrDefault && gamblingHouseStatus.isManagerPresent;
		_go.SetButtonListener("Panel/Header/Move Cash", delegate
		{
			FakePressConversationToggle();
		});
		_go.GetButton("Panel/Header/Move Cash").interactable = gamblingHouseStatus.isManagerPresent;
	}

	private void RefreshAmenities()
	{
		_amenties.DestroyAllChildren();
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(base.Model.visit.building);
		List<AmenityData> amenities = base.Model.Module.data.amenities;
		Fixnum fixnum = base.Model.Module.config.gambling.amenityCount.Evaluate(Game.ctx.players.Human.PID, entity, entity);
		_amenties.EnsureChildCount(amenities, _tmplAmenityCard);
		_amenties.InitializeChildren(amenities, RefreshAmenityCard);
		for (int i = amenities.Count; i < fixnum; i++)
		{
			Object.Instantiate(_tmplAddAmenity, _amenties.transform).GetButton("Add Amenity Button").onClick.AddListener(delegate
			{
				base.Controller.OnAddAmenityClick();
			});
		}
	}

	private void RefreshAmenityCard(int count, GameObject card, AmenityData data)
	{
		AmenityDef def = GamblingSettings.FindAmenityById(data.defID);
		ModQuery query = base.Model.Module.MakeManagerBasedModQuery(Game.ctx.players.Human, base.Model.visit.building);
		card.GetOrAddComponent<AmenityMouseoverCtx>().Set(base.Model, def, data);
		card.SetImageOrHide("Banner/Image", ModulesUIUtil.FindLargeBannerOrNull(def.banner));
		card.SetText("Title", Loc.Get(def.locname));
		card.GetButton("Destroy").onClick.SetListener(AskToDestroyAmenity);
		card.SetText("Turn Summary", MakeSummary(data));
		card.SetText("Status Icon", MakeStatusIconLine(data, query));
		int numSlots = (int)def.behavior.slots.slotCount.Evaluate(query);
		int count2 = data.gamblers.Count;
		int count3 = MathUtil.ClampMin(numSlots - count2, 0);
		List<EntityID> list = new List<EntityID>(data.gamblers);
		list.AddTimes(count3);
		GameObject child = card.GetChild("Gamblers");
		child.EnsureChildCount(list, _tmplGamblerCard);
		child.InitializeChildren(list, RefreshGamblerCard);
		card.SetText("Gamblers Header", MakeGamblersHeader());
		bool flag = !data.IsEnabled(Game.ctx.clock.Now);
		card.GetChild("Turn Summary").SetActive(!flag);
		card.GetChild("Status Icon").SetActive(!flag);
		card.GetChild("Build Line").SetActive(flag);
		if (flag)
		{
			card.SetText("Build Line", DescribeEnable(data, Game.ctx.clock.Now).text);
		}
		void AskToDestroyAmenity()
		{
			Game.serv.ui.AddPopup(CreateConfirmDestroyPopup(data, def, Loc.Get(base.Model.Module.config.common.display.locname)));
		}
		string MakeGamblersHeader()
		{
			bool flag2 = !data.IsEnabled(Game.ctx.clock.Now);
			if (numSlots > 0)
			{
				if (!flag2)
				{
					return Loc.Get("ui.ownedcasino.amenity.gamblers");
				}
				return Loc.Get("ui.ownedcasino.amenity.gamblers.under-construction");
			}
			base.Model.Module.config.gambling.aoeRadius.Evaluate(query);
			int potentialGamblingCustomers = BuildingUtil.GetPotentialGamblingCustomers(base.Model.visit.building, query);
			Dictionary<AmenityData, int> numCustomersForAmenities = BuildingUtil.GetNumCustomersForAmenities(base.Model.visit.building, query);
			int count4 = numCustomersForAmenities.Count;
			int num = numCustomersForAmenities.FindOrDefault(data);
			Fixnum? fixnum = def.behavior?.maxCustomers?.Evaluate(query);
			if (!flag2)
			{
				if (def.behavior.type != AmenityDef.AmenityBehavior.BehaviorType.Mod)
				{
					return Loc.Get("ui.ownedcasino.amenity.customers", "numPlayers", num, "maxPlayers", fixnum, "potentialPlayers", potentialGamblingCustomers, "amenityCount", count4);
				}
				return Loc.Get("ui.ownedcasino.amenity.mod");
			}
			return Loc.Get("ui.ownedcasino.amenity.customers.under-construction");
		}
		string MakeStatusIconLine(AmenityData data2, ModQuery query2)
		{
			PlayerGambling gambling = Game.ctx.players.Human.gambling;
			bool flag2 = gambling.AmenityIsFunded(base.Model.visit.building, data2, query2);
			PlayerGambling.GamblingHouseStatus gamblingHouseStatus = gambling.GetGamblingHouseStatus(base.Model.visit.building);
			string text = ((flag2 & gamblingHouseStatus.isManagerPresent & gamblingHouseStatus.isNotDamaged) ? Loc.Get("ui.ownedcasino.icon.amenity-on") : Loc.Get("ui.ownedcasino.icon.amenity-off"));
			return Loc.Get("ui.ownedcasino.amenity.status", "icon", text);
		}
		static string MakeSummary(AmenityData amenityData)
		{
			Price delta = amenityData.lastTurnResults.Delta;
			return Loc.Get("ui.ownedcasino.last-turn", "money", TextUtil.ColorGreenRed(delta.cash, Loc.Price(delta)));
		}
	}

	public static (string text, int daysleft) DescribeEnable(AmenityData data, SimTime time)
	{
		int num = data.enableTime.days - time.days;
		int num2 = Game.ctx.clock.DaysToTurnsRoundedUp(num);
		return (text: TextUtil.ColorWrap(Loc.Get("module.ui-util.describe.full", "daysLeft", num, "turnsLeft", num2), ColorConstants.TEXT_HEX_CONSTRUCTION), daysleft: num);
	}

	private OkCancelPopup CreateConfirmDestroyPopup(AmenityData data, AmenityDef def, string moduleNameString)
	{
		return new OkCancelPopup(Loc.Get("ui.ownedcasino.amenity-remove", "amenity", Loc.Get(def.locname), "module", moduleNameString), delegate
		{
			base.Controller.DestroyAmenity(data);
		}, delegate
		{
		});
	}

	private void RefreshGamblerCard(int count, GameObject card, EntityID gamblerId)
	{
		if (!gamblerId.IsValid)
		{
			card.SetText("Text", Loc.Get("ui.ownedcasino.amenity.empty-slot"));
			card.SetText("Money", "");
			card.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite());
			card.SetActive("Portrait/Debt", value: false);
			card.SetActive("Buttons/Goto", value: false);
			card.SetActive("Buttons/Ban", value: false);
			card.GetButton().interactable = false;
			return;
		}
		Entity gambler = gamblerId.FindEntity();
		GamblerState state = HumanGambling.FindGamblerState(gambler);
		DebtorMouseoverCtx ctx;
		if (state != null)
		{
			Fixnum maxcredit = (state.DebtIsDue ? HumanGambling.FindCurrentDebtLevel(state) : HumanGambling.FindNextDebtLevel(state)).EvaluateCashForGambler(PlayerID.HumanPlayer, gambler);
			PlayerGambling.GamblingHouseStatus gamblingHouseStatus = GetGamblingHouseStatus();
			ctx = card.GetOrAddComponent<DebtorMouseoverCtx>();
			ctx.Set(gamblerId, state.cash.cash, state.cashDeltaLastTurn.cash, maxcredit);
			(string, string) tuple = MakeDescription();
			card.SetText("Text", tuple.Item1);
			card.SetText("Money", tuple.Item2);
			card.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite(gambler));
			card.SetActive("Portrait/Debt", !state.CanKeepGambling);
			card.SetActive("Buttons/Goto", state.DebtIsDue);
			card.SetActive("Buttons/Ban", state.cash.cash >= 0 && gamblingHouseStatus.isManagerPresent);
			card.SetButtonListener("Buttons/Goto", delegate
			{
				PersonInfoUtil.TweenCameraToEntity(gambler);
			});
			card.SetButtonListener("Buttons/Ban", delegate
			{
				base.Controller.BanGamblerConvoFromButton(gambler.Id);
			});
			card.GetButton().onClick.AddListener(delegate
			{
				base.Controller.OpenGamblerFamily(gamblerId);
			});
		}
		(string text, string money) MakeDescription()
		{
			string firstName = gambler.data.person.FirstName;
			string lastName = gambler.data.person.LastName;
			string text = TextUtil.ColorRedIf(ctx.debt < 0, Loc.Money(ctx.debt));
			string item;
			string item2;
			if (state.DebtIsDue)
			{
				item = Loc.Get("ui.ownedcasino.regular.indebt", "firstname", firstName, "lastname", lastName);
				item2 = Loc.Get("ui.ownedcasino.regular.money.0", "money", text);
			}
			else
			{
				string text2 = ((ctx.lastturn > 0) ? "↑" : ((ctx.lastturn < 0) ? "↓" : ""));
				string message = (Loc.FormatNumberPlusMinus(ctx.lastturn) + " " + text2).Trim();
				string text3 = TextUtil.ColorGreenRed(ctx.lastturn, message);
				item = Loc.Get("ui.ownedcasino.regular.playing", "firstname", firstName, "lastname", lastName);
				item2 = Loc.Get("ui.ownedcasino.regular.money", "money", text, "lastturn", text3);
			}
			return (text: item, money: item2);
		}
	}

	private void RefreshModuleButtons()
	{
		List<IModule> allSlotsUnsafe = base.Model.visit.building.components.modules.GetAllSlotsUnsafe();
		EnableEthReplacements();
		allSlotsUnsafe = SuppressInventory(allSlotsUnsafe);
		_toggles.EnsureChildCount(allSlotsUnsafe.Count, _tmplButton);
		_toggles.InitializeChildren(allSlotsUnsafe, InitializeModuleButton);
		ModuleToggleUtils.MakeManagerToggle(_toggles.transform, _tmplButton, base.Model.visit, selected: false).transform.SetAsFirstSibling();
		ModuleToggleUtils.MakeCornerToggle(_toggles.transform, _tmplButton, base.Model.visit);
		_toggles.transform.GetChild(1).GetComponentInChildren<Toggle>().SetIsOnWithoutNotify(value: true);
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

	private void FakePressConversationToggle()
	{
		_toggles.transform.GetChild(0).GetComponentInChildren<Toggle>().isOn = true;
	}

	private List<IModule> SuppressInventory(List<IModule> slots)
	{
		return slots.Where((IModule x) => !(x is InventoryModule)).ToList();
	}

	private void InitializeModuleButton(int i, GameObject card, IModule _)
	{
		ModuleToggleUtils.InitializeModuleToggleCard(i, card, base.Model.visit, delegate(GameObject c)
		{
			base.Controller.OnModuleButtonClick(c);
		});
	}

	private ModuleToggleContext FindModuleToggleFor(ModuleSlot slotdef)
	{
		foreach (Transform item in _toggles.transform)
		{
			ModuleToggleContext component = item.GetComponent<ModuleToggleContext>();
			if (component != null && component.slotdef == slotdef)
			{
				return component;
			}
		}
		return null;
	}

	private void RefreshHeaderAndManager()
	{
		Entity manager = ModulesUtil.MakeModuleQuery(base.Model.visit.building).manager;
		List<EntityID> allControlledBuildingsUnsafe = Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe();
		int num = allControlledBuildingsUnsafe.FindIndex((EntityID x) => x == base.Model.visit.building.Id);
		_go.SetActive("Panel/Header/Right", num < allControlledBuildingsUnsafe.Count - 1);
		_go.SetActive("Panel/Header/Left", num > 0);
		_go.SetText("Panel/Header/Module Name", base.Model.Module.config.common.display.GetName());
		_go.SetText("Panel/Header/Module Icon", Loc.Get(base.Model.Module.config.common.display.locicon));
		_go.SetActive("Panel/Header/Manager/Portrait", manager != null);
		bool flag = base.Controller.CanChangeManager(manager);
		_go.SetText("Panel/Header/Manager Name", (manager == null) ? Loc.Get("ui.ownedcasino.manager-none") : Loc.Get("ui.ownedcasino.manager", "name", manager.data.person.FullName));
		_go.GetButton("Panel/Header/Manager/Button").interactable = flag;
		_go.GetChild("Panel/Header/Manager/Button").GetOrAddComponent<ViewDescribeModule.ManagerButtonContext>().Set(PlayerID.HumanPlayer, manager?.Id ?? EntityID.INVALID);
		_go.GetChild("Panel/Header/Manager/Button").SetChildText(TextUtil.ColorEnabledIf(flag, "+"));
		_go.SetImage("Panel/Header/Manager/Portrait/Portrait", HUDUtil.GetCrewSprite(manager));
		_go.SetButtonListener("Panel/Header/Manager/Button", delegate
		{
			base.Controller.OnManagerEditButton();
		});
	}

	private void RefreshDamage()
	{
		(bool valid, bool damaged, Fixnum current, Fixnum max) buildingHealth = BuildingUtil.GetBuildingHealth(base.Model.visit.building);
		bool item = buildingHealth.valid;
		bool item2 = buildingHealth.damaged;
		Fixnum item3 = buildingHealth.current;
		bool flag = item && item2;
		_go.SetActive("Panel/Damage", flag);
		if (flag)
		{
			string text = Loc.Percentage(item3 / 100);
			string text2 = Loc.Get("ui.ownedbiz.condition", "percent", text);
			bool num = ModulesUtil.GetManagerOrNull(base.Model.visit.building).manager != null;
			string text3 = ((!num) ? Loc.Get("ui.ownedbiz.condition.no-mgmt") : Loc.Get("ui.ownedbiz.condition.mgmt"));
			string text4 = TextUtil.ColorRedIfNot(num, text2 + " - " + text3);
			_go.SetText("Panel/Damage/Text", text4);
		}
	}

	private void RefreshFooter()
	{
		GameObject child = _go.GetChild("Templates/Nearby Crew");
		GameObject child2 = _go.GetChild("Panel/Footer/Scroll View List/Viewport/Content");
		ToggleGroup togglegroup = child2.GetComponentInChildren<ToggleGroup>();
		List<CrewAssignment> data = Game.ctx.players.Human.crew.FindAllDriversAtNode(base.Model.visit.GetBldgNodeID());
		child2.EnsureChildCount(data, child);
		child2.InitializeChildren(data, delegate(int i, GameObject card, CrewAssignment crew)
		{
			InitializeCrewCard(togglegroup, card, crew);
		});
	}

	private void InitializeCrewCard(ToggleGroup group, GameObject card, CrewAssignment crew)
	{
		Toggle componentInChildren = card.GetComponentInChildren<Toggle>();
		componentInChildren.group = group;
		componentInChildren.SetIsOnWithoutNotify(crew.peepId == base.Model.visit.peep.Id);
		componentInChildren.onValueChanged.SetListener(delegate(bool on)
		{
			if (on)
			{
				ProcessCrewClick(crew);
			}
		});
		PersonData person = crew.GetPeep().data.person;
		card.SetText("Text", Loc.Get("ui.ownedbiz.crewcard.name", "name", person.FirstName, "last", person.LastName));
		card.SetImageOrHide("Background/Basic Portrait/Portrait", HUDUtil.GetCrewSprite(crew.GetPeep()));
	}

	private void ProcessCrewClick(CrewAssignment crew)
	{
		base.Controller.OnSwitchCrew(crew);
	}
}

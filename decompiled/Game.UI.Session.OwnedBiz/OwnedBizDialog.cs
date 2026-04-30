using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedBiz;

public sealed class OwnedBizDialog : HUDView<OwnedBizModel, OwnedBizDialog, OwnedBizController>
{
	private List<SubviewEntry> _subviews;

	private int _subcurrent;

	private GameObject _tmplButton;

	private GameObject _toggles;

	public const string PANEL_CLOSE = "Panel/Close";

	public const string PANEL_LEFT = "Panel/Left";

	public const string PANEL_RIGHT = "Panel/Right";

	public const string MODULE_BUTTONS_PARENT = "Background/Modules";

	public const string MODULE_BUTTONS = "Background/Modules/Buttons";

	public const string VIEW_ADD = "View Add Module";

	public const string VIEW_DESCRIBE = "View Describe Module";

	public const string VIEW_INVENTORY = "View Inventory";

	public const string VIEW_FRONTPAGE = "View Front Page";

	public const string TMPL_CONTAINER = "Templates";

	public const string TMPL_NEARBY_CREW = "Templates/Nearby Crew";

	public const string TMPL_INVCARD = "Templates/Inventory Card";

	public const string TMPL_MODULE_TOGGLE = "Templates/Module Toggle";

	private bool _firstView = true;

	public const string PANEL_BIZNAME = "Panel/Name";

	public const string PANEL_DAMAGE_BOX = "Panel/Damage";

	public const string PANEL_DAMAGE_TEXT = "Panel/Damage/Text";

	public const string PANEL_OWNER_IMAGE = "Panel/Header/Owner/Portrait";

	public const string PANEL_OWNER_NAME = "Panel/Header/Owner Name";

	public const string PANEL_PLAYER_IMAGE = "Panel/Header/Player/Portrait";

	public const string PANEL_PLAYER_NAME = "Panel/Header/Player Name";

	public const string PANEL_FOOTER_LIST = "Panel/Footer/Scroll View List/Viewport/Content";

	public const string PANEL_BG = "Panel/Header Background";

	public const string NEARBY_CREW_PORTRAIT = "Background/Basic Portrait/Portrait";

	public const string NEARBY_CREW_NAME = "Text";

	public static string BG_PATH = "UI Images/Decos/Ind 06";

	public static string TOP_DECO_PARENT = "Background/Modules/Top";

	public static string TOP_DECO = "Background/Modules/Top/Top Chrome";

	public static string BOTTOM_DECO_PARENT = "Background/Modules/Bottom";

	public static string BOTTOM_DECO = "Background/Modules/Bottom/Bottom Chrome";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.HUDOwnedBiz;

	internal SubviewEntry CurrentSubview
	{
		get
		{
			if (_subcurrent < 0)
			{
				return null;
			}
			return _subviews[_subcurrent];
		}
	}

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
		_go.SetButtonListener("Panel/Right", delegate
		{
			base.Controller.OnSwitchBuildingButton(1);
		});
		_go.SetButtonListener("Panel/Left", delegate
		{
			base.Controller.OnSwitchBuildingButton(-1);
		});
		_tmplButton = _go.GetChild("Templates/Module Toggle");
		_toggles = _go.GetChild("Background/Modules/Buttons");
		_toggles.GetComponent<ToggleGroup>().allowSwitchOff = true;
		_subviews = new List<SubviewEntry>
		{
			new ViewAddModule(ViewType.ViewAddModule, _go.GetChild("View Add Module")),
			new ViewDescribeModule(ViewType.ViewDescribeModule, _go.GetChild("View Describe Module")),
			new ViewInventory(ViewType.ViewInventory, _go.GetChild("View Inventory")),
			new ViewFrontPage(ViewType.ViewFrontPage, _go.GetChild("View Front Page"))
		};
		_subviews.ForEach(delegate(SubviewEntry e)
		{
			e.Initialize(base.Controller);
		});
		ShowSubview(ViewType.None);
		_go.GetChild("Templates").SetActive(value: false);
		Game.serv.mouseovers.Register(MouseoverType.ModuleButton, new ModuleToggleMouseover());
		Game.serv.mouseovers.Register(MouseoverType.InventoryCard, new InventoryCardMouseover());
		Game.serv.mouseovers.Register(MouseoverType.ModuleDescCapsule, new ModuleDescCapsuleMouseover());
		Game.serv.mouseovers.Register(MouseoverType.ModuleDescExpansion, new ModuleDescExpansionMouseover());
		Game.serv.mouseovers.Register(MouseoverType.ModuleDescManager, new ModuleDescManagerMouseover());
		Game.ctx.events.AddListener(SessionEventType.BuildingHealthChanged, OnBuildingHealthChanged);
	}

	internal override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.BuildingHealthChanged, OnBuildingHealthChanged);
		Game.serv.mouseovers.Unregister(MouseoverType.ModuleDescManager);
		Game.serv.mouseovers.Unregister(MouseoverType.ModuleDescExpansion);
		Game.serv.mouseovers.Unregister(MouseoverType.ModuleDescCapsule);
		Game.serv.mouseovers.Unregister(MouseoverType.InventoryCard);
		Game.serv.mouseovers.Unregister(MouseoverType.ModuleButton);
		ShowSubview(ViewType.None);
		_subviews.ForEach(delegate(SubviewEntry e)
		{
			e.Release();
		});
		_subviews.Clear();
		_toggles = null;
		_tmplButton = null;
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
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOwnedBizOpened, PlayerID.HumanPlayer);
	}

	protected override void OnBeforeHide()
	{
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

	internal void ControllerRequestsFullRefresh(bool resetSubview)
	{
		RefreshContents(resetSubview);
	}

	internal void ControllerRequestsSubviewRefresh()
	{
		CurrentSubview?.RefreshSubview();
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
		RefreshContents(resetSubview: true);
	}

	private void RefreshContents(bool resetSubview)
	{
		if (resetSubview)
		{
			ShowSubview(ViewType.None);
			RefreshModuleButtons();
			SelectFirstView();
		}
		else
		{
			CurrentSubview?.RefreshSubview();
		}
		RefreshHeader();
		RefreshDamage();
		RefreshFooter();
	}

	private void SelectFirstView()
	{
		Transform transform = _toggles.transform;
		if (_firstView)
		{
			ShowSubview(ViewType.ViewFrontPage);
			_firstView = false;
		}
		else if (transform.childCount > 1)
		{
			GameObject gameObject = null;
			gameObject = ((!KeyUtil.IsShiftDown) ? transform.GetChild(1).gameObject : transform.GetChild(2).gameObject);
			gameObject.GetComponentInChildren<Toggle>().isOn = true;
			base.Controller.OnModuleButtonClick(gameObject);
		}
	}

	private void RefreshModuleButtons()
	{
		List<IModule> allSlotsUnsafe = base.Model.visit.building.components.modules.GetAllSlotsUnsafe();
		EnableModuleEthReplacements();
		_toggles.EnsureChildCount(allSlotsUnsafe.Count, _tmplButton);
		_toggles.InitializeChildren(allSlotsUnsafe, InitializeModuleButton);
		ModuleToggleUtils.MakeOwnerToggle(_toggles.transform, _tmplButton, base.Model.visit, selected: false).transform.SetAsFirstSibling();
		ModuleToggleUtils.MakeCornerToggle(_toggles.transform, _tmplButton, base.Model.visit);
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

	private void RefreshHeader()
	{
		_go.SetText("Panel/Name", base.Model.visit.biz.data.biz.bizname);
		List<EntityID> allControlledBuildingsUnsafe = Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe();
		int num = allControlledBuildingsUnsafe.FindIndex((EntityID x) => x == base.Model.visit.building.Id);
		_go.SetActive("Panel/Right", num < allControlledBuildingsUnsafe.Count - 1);
		_go.SetActive("Panel/Left", num > 0);
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		_go.SetImage("Panel/Header/Player/Portrait", HUDUtil.GetCrewSprite(playerPeep));
		string text = Loc.Get("ui.ownedbiz.control", "group", Game.ctx.players.Human.social.FindPlayerGroupNameColorized());
		_go.SetText("Panel/Header/Player Name", text);
		Entity npc = base.Model.visit.npc;
		var (text2, text3) = NameUtils.GetNpcNameAndRelationshipToPlayer(npc, base.Model.visit.pid);
		_go.SetImage("Panel/Header/Owner/Portrait", HUDUtil.GetCrewSprite(npc));
		string text4 = Loc.Get("ui.ownedbiz.peep", "name", text2, "rel", text3);
		_go.SetText("Panel/Header/Owner Name", text4);
		Sprite sprite = TryFindEthBGReplacement();
		if (sprite != null)
		{
			_go.SetImage("Panel/Header Background", sprite);
		}
	}

	private Sprite TryFindEthBGReplacement()
	{
		Label ethnicity = Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
		if (!PlayerCrew.HasEthPackAndIsEth(ethnicity))
		{
			return null;
		}
		Sprite sprite = Resources.Load<Sprite>(BG_PATH + ethnicity.ToString().ToUpper());
		if (sprite == null)
		{
			sprite = Resources.Load<Sprite>(BG_PATH);
		}
		return sprite;
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

	public void EnableModuleEthReplacements()
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

	internal void ShowSubview(ViewType type)
	{
		_subcurrent = -1;
		for (int i = 0; i < _subviews.Count; i++)
		{
			SubviewEntry subviewEntry = _subviews[i];
			if (subviewEntry.type == type)
			{
				_subcurrent = i;
				subviewEntry.go.SetActive(value: true);
				subviewEntry.OnActivated();
			}
			else
			{
				subviewEntry.OnDeactivated();
				subviewEntry.go.SetActive(value: false);
			}
		}
	}

	internal bool IsShowingSubview(ViewType type)
	{
		SubviewEntry currentSubview = CurrentSubview;
		if (currentSubview == null)
		{
			return false;
		}
		return currentSubview.type == type;
	}
}

using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Mouseovers;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public class OrgChartPopup : BasePopup
{
	private const string CLOSE_BUTTON = "Close";

	private const string END_SCHEME = "End Scheme Button";

	private const string THRONE_BUTTON = "Page/Scroll View List/Viewport/Content/ThroneButton";

	private Xorshift _rng;

	private GameObject _tmplPeepBlock;

	private GameObject _tmplRoleColumn;

	private GameObject _tmplAddRoleButton;

	private VisitState _visit;

	private CrewAssignment bossCrew;

	public const string OUTFIT_NAME = "Header/BG/Outfit Name";

	public const string OUTFIT_BG = "Header/BG";

	public const string GAME_INFO = "Header/Dek";

	public const string BOSS_CAT = "Page/Scroll View List/Viewport/Content/Boss";

	public const string SPECIALIST_CAT = "Page/Scroll View List/Viewport/Content/Specialists";

	public const string ROLE_COLUMNS = "Page/Scroll View List/Viewport/Content/Specialists/Roles";

	public const string HEADER_BAR = "Page/Scroll View List/Viewport/Content/Specialists/Header/Top Divider";

	public const string ROLE_HEADER_TITLE = "Page/Scroll View List/Viewport/Content/Specialists/Header/Title";

	public const string MUSCLE_CAT = "Page/Scroll View List/Viewport/Content/Grunts/Muscle/Members";

	public const string CAPTAINS_CAT = "Page/Scroll View List/Viewport/Content/Grunts/Captains/Members";

	public const string GRUNTS_PANEL = "Page/Scroll View List/Viewport/Content/Grunts";

	public const string TEMPLATE_PEEP = "Templates/Peep Block";

	public const string TEMPLATE_COLUMN = "Templates/Role Column";

	public const string TEMPLATE_ADD = "Templates/Add To Role";

	public const string TOP_DIVIDER = "Page/Scroll View List/Viewport/Content/Top Divider";

	public const string BOTTOM_DIVIDER = "Page/Scroll View List/Viewport/Content/Top Divider";

	public const string CONTENT_CONTAINER = "Page/Scroll View List/Viewport/Content";

	public const int BAR_MAX_WIDTH = 930;

	public const int TICKER_WIDTH = 1;

	public readonly Label CAPTAIN_LEVELUP = new Label("levelup-captain");

	public const string CREW_PORTRAIT = "BG/Display/Portrait";

	public const string CREW_INTERACT = "BG/Buttons/Interact Crew";

	public const string CREW_INTERACT_TEXT = "BG/Buttons/Interact Crew/Text";

	public const string CREW_INSPECT = "BG/Buttons/Inspect Crew";

	public const string RELATIONSHIPS = "BG/Buttons/Relationships Crew";

	public const string DETAIL_TOP = "BG/Detail Top";

	public const string DETAIL_BOT = "BG/Detail Bot";

	public const string NAME = "Name";

	public const string UP_TICK = "Up Tick";

	public const string ROLE_TITLE = "BG/Title";

	public const string ROLE_TITLE_BG = "BG";

	public const string ROLE_ICON = "Icon/Text";

	public const string ROLE_ICON_BG = "Icon";

	public const string PEEPS_CONTAINER = "Peeps";

	public const string SPACING = "Spacing";

	public const string ADD = "Add To Role";

	public PlayerCrew HumanCrew => Game.ctx.players.Human.crew;

	public override UIReference UIReference => UIElements.OrgChartPopup;

	protected override void InitializeOnPush()
	{
		_tmplPeepBlock = _go.GetChild("Templates/Peep Block");
		_tmplRoleColumn = _go.GetChild("Templates/Role Column");
		_tmplAddRoleButton = _go.GetChild("Templates/Add To Role");
		bossCrew = HumanCrew.GetCrewForPlayerPeep();
		_visit = new VisitState(bossCrew, Game.ctx.clock.Now, Game.ctx.players.Human.PID);
		RefreshDialog();
		Game.serv.mouseovers.Register(MouseoverType.OrgChartCrew, new OrgCrewPeepMouseover());
		Game.serv.mouseovers.Register(MouseoverType.OrgChartRole, new OrgRoleAddMouseover());
		_go.GetButton("Page/Scroll View List/Viewport/Content/ThroneButton").onClick.AddListener(GoToThrone);
		_go.GetButton("Close").onClick.AddListener(Close);
		Game.ctx.events.AddListener(SessionEventType.CrewRoleGiven, RefreshDialogFromSev);
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.OrgChartRole);
		Game.serv.mouseovers.Unregister(MouseoverType.OrgChartCrew);
		_go.GetButton("Page/Scroll View List/Viewport/Content/ThroneButton").onClick.RemoveListener(GoToThrone);
		_go.GetButton("Close").onClick.RemoveListener(Close);
		Game.ctx.events.RemoveListener(SessionEventType.CrewRoleGiven, RefreshDialogFromSev);
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new PopupHandlerWithTabSupport();
	}

	public void RefreshDialogFromSev(SessionEvent _)
	{
		RefreshDialog();
	}

	public void RefreshDialog()
	{
		RefreshHeader();
		RefreshCrew();
		GameObject child = _go.GetChild("Page/Scroll View List/Viewport/Content");
		ForceRebuildLayoutImmediate(child);
		ForceRebuildLayoutImmediate(child);
		bool isSet = Game.ctx.players.Human.throne.GetThroneStyle().IsSet;
		_go.GetButton("Page/Scroll View List/Viewport/Content/ThroneButton").interactable = isSet;
		_go.GetChild("Page/Scroll View List/Viewport/Content/ThroneButton").GetComponent<MouseoverTrigger>().enabled = !isSet;
		_go.GetChild("Page/Scroll View List/Viewport/Content/ThroneButton").SetActive(PlayerThrone.CanSeeThrone());
	}

	public void RefreshHeader()
	{
		string playerGroupName = Game.ctx.players.Human.social.PlayerGroupName;
		string cityName = Game.ctx.session.custommap.CityName;
		string text = Loc.FormatDateLong(Game.ctx.clock.Now);
		_panel.SetText("Header/BG/Outfit Name", playerGroupName);
		ForceRebuildLayoutImmediate(_panel.GetChild("Header/BG"));
		_panel.SetText("Header/Dek", Loc.Get("ui.orgchart.dek", "city", cityName, "time", text));
	}

	public void RefreshCrew()
	{
		RefreshBoss();
		RefreshRoles();
		RefreshGrunts();
	}

	public void RefreshBoss()
	{
		List<CrewAssignment> data = new List<CrewAssignment> { bossCrew };
		GameObject child = _panel.GetChild("Page/Scroll View List/Viewport/Content/Boss");
		child.DestroyAllChildren();
		child.EnsureChildCount(data, _tmplPeepBlock);
		child.InitializeChildren(data, InitPeepBlock);
		ForceRebuildLayoutImmediate(child);
	}

	public void RefreshRoles()
	{
		List<RoleDef> list = new List<RoleDef>();
		foreach (RoleDef roleDef in Game.serv.globals.settings.people.social.crew.roleSettings.roleDefs)
		{
			if (roleDef.visReqs.AllPass(_visit))
			{
				list.Add(roleDef);
			}
		}
		GameObject child = _panel.GetChild("Page/Scroll View List/Viewport/Content/Specialists");
		GameObject child2 = _panel.GetChild("Page/Scroll View List/Viewport/Content/Specialists/Roles");
		GameObject child3 = _panel.GetChild("Page/Scroll View List/Viewport/Content/Specialists/Header/Top Divider");
		int num = 930 / list.Count / 2 - 1;
		RectTransform rect = child3.GetRect();
		rect.offsetMin = new Vector2(num, rect.offsetMin.y);
		rect.offsetMax = new Vector2(-num, rect.offsetMax.y);
		child2.EnsureChildCount(list, _tmplRoleColumn);
		child2.InitializeChildren(list, InitRoleColumn);
		ForceRebuildLayoutImmediate(child);
	}

	public void RefreshGrunts()
	{
		List<CrewAssignment> data = (from x in HumanCrew.GetLiving()
			where x.GetPeep()?.data.agent?.xp?.GetCrewRole() == null && x.peepId != bossCrew.peepId && x.GetPeep()?.components.agent?.IsCaptain() == true
			select x).ToList();
		GameObject child = _panel.GetChild("Page/Scroll View List/Viewport/Content/Grunts/Captains/Members");
		child.DestroyAllChildren();
		child.EnsureChildCount(data, _tmplPeepBlock);
		child.InitializeChildren(data, InitPeepBlock);
		List<CrewAssignment> data2 = (from x in HumanCrew.GetLiving()
			where x.GetPeep()?.data.agent?.xp?.GetCrewRole() == null && x.peepId != bossCrew.peepId && !(x.GetPeep()?.components.agent?.IsCaptain() ?? true)
			select x).ToList();
		GameObject child2 = _panel.GetChild("Page/Scroll View List/Viewport/Content/Grunts/Muscle/Members");
		child2.DestroyAllChildren();
		child2.EnsureChildCount(data2, _tmplPeepBlock);
		child2.InitializeChildren(data2, InitPeepBlock);
		GameObject child3 = _panel.GetChild("Page/Scroll View List/Viewport/Content/Grunts");
		ForceRebuildLayoutImmediate(child);
		ForceRebuildLayoutImmediate(child2);
		ForceRebuildLayoutImmediate(child3);
	}

	public void InitPeepBlock(int i, GameObject card, CrewAssignment crew)
	{
		if (card.GetChild("BG/Buttons/Inspect Crew") == null)
		{
			return;
		}
		bool flag = crew.peepId == bossCrew.peepId;
		bool flag2 = crew.GetPeep()?.data.agent?.xp?.GetCrewRole() != null;
		bool flag3 = Game.ctx.players.Human.crew.IsOnBoard(crew) || Game.ctx.players.Human.schemes.IsInScheme(crew.GetPeep().Id);
		card.GetChild("BG/Buttons/Inspect Crew").SetActive(crew.IsValid);
		card.GetChild("BG/Buttons/Interact Crew").SetActive(crew.IsValid && flag2 && flag3);
		card.GetChild("BG/Buttons/Relationships Crew").SetActive(value: true);
		card.GetChild("Name").SetActive(flag2 || flag);
		card.GetChild("BG/Detail Top").SetActive(flag);
		card.GetChild("BG/Detail Bot").SetActive(flag);
		card.GetChild("Up Tick").SetActive(i != 0 && flag2);
		if (crew.IsValid)
		{
			card.SetImage("BG/Display/Portrait", Game.ctx.hud.portraits.GetSpriteFor(crew.GetPeep()));
			card.GetChild("BG/Display/Portrait").GetOrAddComponent<OrgCrewPeepMouseoverCtx>().Set(crew.peepId);
			bool flag4 = Game.ctx.players.Human.schemes.GetSchemeForCrew(crew.peepId) != null;
			card.SetText("Name", Loc.Get("ui.orgchart.peep-block.name", "name", crew.GetPeep().data.person.nickname ?? crew.GetPeep().data.person.FirstName));
			card.SetText("BG/Buttons/Interact Crew/Text", flag4 ? Loc.Get("ui.orgchart.peep-block.has-scheme") : Loc.Get("ui.orgchart.peep-block.no-scheme"));
			card.SetButtonListener("BG/Buttons/Relationships Crew", delegate
			{
				Game.ctx.hud.personInfo.Show(crew.GetPeep());
				Close();
			});
			card.SetButtonListener("BG/Buttons/Inspect Crew", delegate
			{
				OpenCrewPeepInspect(crew);
			});
			card.SetButtonListener("BG/Buttons/Interact Crew", delegate
			{
				Game.ctx.players.Human.schemes.CrewInteract(crew.GetPeep());
				Close();
			});
		}
	}

	public void InitRoleColumn(int i, GameObject card, RoleDef role)
	{
		card.SetText("BG/Title", Loc.Get(role.loctitle));
		card.SetText("Icon/Text", Loc.Get(role.locicon));
		GameObject child = card.GetChild("Peeps");
		OrgRoleAddMouseoverCtx orAddComponent = card.GetChild("BG").GetOrAddComponent<OrgRoleAddMouseoverCtx>();
		OrgRoleAddMouseoverCtx orAddComponent2 = card.GetChild("Icon").GetOrAddComponent<OrgRoleAddMouseoverCtx>();
		orAddComponent.Set(role.id);
		orAddComponent2.Set(role.id);
		List<CrewAssignment> list = (from x in HumanCrew.GetLiving()
			where x.GetPeep().data.agent?.xp?.GetCrewRole() == role
			select x).ToList();
		_ = list.Count;
		child.EnsureChildCount(list, _tmplPeepBlock);
		child.InitializeChildren(list, InitPeepBlock);
		PlayerCrew humanCrew = Game.ctx.players.Human.crew;
		GameObject child2 = card.GetChild("Add To Role");
		child2.GetButton().onClick.SetListener(delegate
		{
			Game.serv.ui.AddPopup(new RoleAssignPopup(role));
		});
		child2.GetButton().interactable = humanCrew.AllCrew.Where((CrewAssignment x) => humanCrew.IsValidPromotionChoice(x)).Count() > 0;
		child2.GetOrAddComponent<OrgRoleAddMouseoverCtx>().Set(role.id);
		ForceRebuildLayoutImmediate(card);
	}

	public void GoToThrone()
	{
		Game.serv.ui.AddPopup(new ThronePopup());
		Close();
	}

	public void OpenCrewPeepInspect(CrewAssignment crew)
	{
		if (Game.serv.ui.ContainsPopup<CrewPeepInspectPopup>() && !(Game.serv.ui.TopPopupUnsafe is CrewPeepInspectPopup))
		{
			Game.serv.ui.RemovePopup<CrewPeepInspectPopup>();
		}
		Game.serv.ui.AddPopup(new CrewPeepInspectPopup(crew.peepId));
	}

	private void ForceRebuildLayoutImmediate(GameObject panel)
	{
		panel.ForceRebuildLayoutImmediate();
		panel.ForceRebuildLayoutImmediate();
		panel.ForceRebuildLayoutImmediate();
	}
}

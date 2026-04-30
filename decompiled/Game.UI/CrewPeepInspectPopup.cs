using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Services.Store;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Mouseovers;
using Game.UI.Session;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI;

public class CrewPeepInspectPopup : BasePopup
{
	private const string CLOSE_BUTTON = "Close/";

	private const string NEXT_BUTTON = "Footer/NextCrew";

	private const string PREV_BUTTON = "Footer/PrevCrew";

	private const string INTERACT_BUTTON = "Footer/Interact Buttons/Interact";

	private const string ORGCHART_BUTTON = "Footer/Interact Buttons/Org Chart";

	private const string RELATIONSHIP_BUTTON = "Footer/Interact Buttons/Relationships";

	private const string LEVELUP_BUTTON = "Footer/Interact Buttons/Levelup";

	private const string TEMPLATE_STAT_CAT = "Templates/Stat Category";

	private const string TEMPLATE_STAT_ITEM = "Templates/Stat Item";

	private Xorshift _rng;

	private PortraitInfo _portraitInfo;

	private Sprite _portraitSprite;

	private List<PortraitMakerService.Piece> _availableBusts;

	private List<PortraitMakerService.Piece> _availableHats;

	private int bustIndex;

	private int hatIndex;

	private Entity peep;

	public GameObject _templateStatCat;

	public GameObject _templateStatItem;

	private const string FILE_NO = "Header/Crew Number/Id";

	private const string CITY = "Header/City";

	private const string SIGNATURE = "Footer/Officer Signature/Signature";

	public const string SIGNATURE_PATH = "UI Images/Decos/CrewRecord_OfficerSig";

	private const string NAME = "Character/Biography/Basic Info/Name/Value";

	private const string ALIAS = "Character/Biography/Basic Info/Alias/Value";

	private const string RELATIONSHIPS = "Character/Biography/Basic Info/Relationship/Value";

	private const string NATIONALITY = "Character/Biography/Basic Info/Nationality/Value";

	private const string AGE = "Character/Biography/Basic Info/Age/Value";

	private const string HEIGHT = "Character/Biography/Basic Info/Height/Value";

	private const string TRAITS = "Character/Biography/Basic Info/Traits/Value";

	private const string OCCUPATION = "Character/Biography/Basic Info/Occupation/Value";

	private const string PORTRAIT = "Character/Biography/Mugshot/Portrait/Portrait";

	private const string OUTFIT_NAME = "Tab/Outfit Name";

	public int HEIGHT_LIMIT_LOW_SHORT = 58;

	public int HEIGHT_LIMIT_LOW_AVG = 63;

	public int HEIGHT_LIMIT_HIGH_AVG = 72;

	public int HEIGHT_LIMIT_HIGH_TALL = 84;

	public readonly Label SHORT_TRAIT = new Label("trait short");

	public readonly Label TALL_TRAIT = new Label("trait tall");

	private const string BUST_INC = "Character/Biography/Mugshot/Customize/Bust Increment";

	private const string BUST_DEC = "Character/Biography/Mugshot/Customize/Bust Decrement";

	private const string HAT_INC = "Character/Biography/Mugshot/Customize/Hat Increment";

	private const string HAT_DEC = "Character/Biography/Mugshot/Customize/Hat Decrement";

	private const string CONFIRM = "Character/Biography/Mugshot/Customize/Confirm";

	private const string STAT_HISTORY_CONTAINER = "Character/Criminal Info/Viewport/Content/HistoryStats";

	private const string CAT_TITLE = "Title";

	private const string STAT_ITEM_CONTAINER = "Stats";

	private const string STAT_NAME = "Title";

	private const string STAT_VALUE = "Value";

	private const string LEVELUP_DESCRIPTIONS = "Character/Criminal Info/Viewport/Content/Levelups";

	private const string TEMPLATE_LEVELUP = "Templates/Levelup";

	private const string LEVELUP_ICON = "Icon";

	private const string LEVELUP_NAME = "Name";

	private const string LEVELUP_VALUE = "Value";

	public Entity CrewMember => peep;

	public override UIReference UIReference => UIElements.CrewPeepInspectPopup;

	public CrewPeepInspectPopup(EntityID crewPeep)
	{
		peep = crewPeep.FindEntity();
	}

	protected override void InitializeOnPush()
	{
		_templateStatCat = _go.GetChild("Templates/Stat Category");
		_templateStatItem = _go.GetChild("Templates/Stat Item");
		RefreshDialog();
		Game.serv.mouseovers.Register(MouseoverType.CrewInspectTraits, new InspectTraitsMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CrewInspectRole, new InspectRoleMouseover());
		_go.GetButton("Footer/Interact Buttons/Interact").onClick.AddListener(OnCrewInteractClick);
		_go.GetButton("Close/").onClick.AddListener(Close);
		_go.GetButton("Footer/PrevCrew").onClick.AddListener(PrevCrewClick);
		_go.GetButton("Footer/NextCrew").onClick.AddListener(NextCrewClick);
		_go.GetButton("Footer/Interact Buttons/Org Chart").onClick.AddListener(OnOrgChartClick);
		_go.GetButton("Footer/Interact Buttons/Relationships").onClick.AddListener(OnRelClick);
		_go.GetButton("Footer/Interact Buttons/Levelup").onClick.AddListener(OnLevelupClick);
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.CrewInspectTraits);
		Game.serv.mouseovers.Unregister(MouseoverType.CrewInspectRole);
		_go.GetButton("Footer/Interact Buttons/Interact").onClick.RemoveListener(OnCrewInteractClick);
		_go.GetButton("Close/").onClick.RemoveListener(Close);
		_go.GetButton("Footer/PrevCrew").onClick.RemoveListener(PrevCrewClick);
		_go.GetButton("Footer/NextCrew").onClick.RemoveListener(NextCrewClick);
		_go.GetButton("Footer/Interact Buttons/Org Chart").onClick.RemoveListener(OnOrgChartClick);
		_go.GetButton("Footer/Interact Buttons/Relationships").onClick.RemoveListener(OnRelClick);
		_go.GetButton("Footer/Interact Buttons/Levelup").onClick.RemoveListener(OnLevelupClick);
		_templateStatCat = (_templateStatItem = null);
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new PopupHandlerWithTabSupport();
	}

	private void StartGame()
	{
	}

	public void RefreshDialog()
	{
		InitializeHeaderAndFooter();
		InitializeCrewInfo();
		InitializeCustomizeTab();
		InitializeCrewLevelups();
		InitializeCrewHistoryStats();
	}

	private void InitializeHeaderAndFooter()
	{
		_panel.SetText("Header/Crew Number/Id", (CrewMember.Id.id % 1000).ToString());
		_panel.SetText("Header/City", Loc.Get("ui.crewinspect.city", "city", Game.ctx.session.mapconfig.CityName.ToUpper()));
		_panel.SetImage("Footer/Officer Signature/Signature", FindSignatureOrNull((int)CrewMember.Id.id % 10));
	}

	public static Sprite FindSignatureOrNull(int sigNum)
	{
		return Resources.Load<Sprite>("UI Images/Decos/CrewRecord_OfficerSig" + sigNum);
	}

	private void InitializeCrewInfo()
	{
		_portraitInfo = Game.serv.serializer.instance.Clone(peep.data.person.portrait);
		_portraitSprite = Game.serv.portraits.GenerateBlankPortrait(_portraitInfo);
		string playerGroupName = Game.ctx.players.Human.social.PlayerGroupName;
		_go.SetText("Tab/Outfit Name", playerGroupName);
		_go.SetImage("Character/Biography/Mugshot/Portrait/Portrait", _portraitSprite);
		RefreshPortraitOptions();
		RefreshPortraitsDisplay();
		RefreshButtons();
		PersonData person = peep.data.person;
		PersonInfoUtil.Overview overview = PersonInfoUtil.GenerateOverview(peep, details: false);
		string occupationText = GetOccupationText();
		_panel.SetText("Character/Biography/Basic Info/Name/Value", person.FirstName + " " + person.LastName);
		_panel.SetText("Character/Biography/Basic Info/Alias/Value", person.nickname ?? "");
		_panel.SetText("Character/Biography/Basic Info/Relationship/Value", Loc.RelationshipToYou(overview.relToHuman?.type ?? RelationshipType.None, person.Gender));
		_panel.SetText("Character/Biography/Basic Info/Nationality/Value", Game.serv.globals.settings.ethnicities.FindEthnicityDef(person.Ethnicity).loc.GetEthnicity());
		_panel.SetText("Character/Biography/Basic Info/Age/Value", person.GetAge(Game.ctx.clock.Now).YearsInt.ToString());
		_panel.SetText("Character/Biography/Basic Info/Height/Value", GetCrewHeight());
		_panel.SetText("Character/Biography/Basic Info/Occupation/Value", occupationText);
		IEnumerable<Trait> allTraits = peep.components.person.GetAllTraits();
		string text = string.Join("", allTraits.Select((Trait t) => t.GetLocIcon()));
		_panel.SetText("Character/Biography/Basic Info/Traits/Value", text);
	}

	public string GetOccupationText()
	{
		RoleDef roleDef = CrewMember.data.agent.xp?.GetCrewRole();
		if (roleDef != null)
		{
			return Loc.Get(Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(roleDef.id).loctitle);
		}
		if (CrewMember.components.agent.IsBoss().pass)
		{
			return Loc.Get("ui.crewinspect.role.boss");
		}
		if (CrewMember.components.agent.IsCaptain())
		{
			return Loc.Get("ui.crewinspect.role.captain");
		}
		return Loc.Get("ui.crewinspect.role.muscle");
	}

	public string GetCrewHeight()
	{
		bool flag = CrewMember.data.person.traitIds.Contains(SHORT_TRAIT);
		bool flag2 = CrewMember.data.person.traitIds.Contains(TALL_TRAIT);
		int num = (flag ? HEIGHT_LIMIT_LOW_SHORT : (flag2 ? HEIGHT_LIMIT_HIGH_AVG : HEIGHT_LIMIT_LOW_AVG));
		int num2 = (flag2 ? HEIGHT_LIMIT_HIGH_TALL : (flag ? HEIGHT_LIMIT_LOW_AVG : HEIGHT_LIMIT_HIGH_AVG));
		double d = (double)num + (double)(num2 - num) * Math.Abs(Math.Sin(CrewMember.Id.id));
		return Loc.Length(new Length((int)Math.Floor(d)));
	}

	private void InitializeCustomizeTab()
	{
		_panel.SetButtonListener("Character/Biography/Mugshot/Customize/Bust Increment", delegate
		{
			SelectBust(1);
		});
		_panel.SetButtonListener("Character/Biography/Mugshot/Customize/Bust Decrement", delegate
		{
			SelectBust(-1);
		});
		_panel.SetButtonListener("Character/Biography/Mugshot/Customize/Hat Increment", delegate
		{
			SelectHat(1);
		});
		_panel.SetButtonListener("Character/Biography/Mugshot/Customize/Hat Decrement", delegate
		{
			SelectHat(-1);
		});
		_panel.SetButtonListener("Character/Biography/Mugshot/Customize/Confirm", UpdatePortrait);
	}

	private void RefreshPortraitsDisplay()
	{
		Game.serv.portraits.ResetPortrait(_portraitSprite);
		Game.serv.portraits.PopulateCompositePortrait(_portraitInfo, _portraitSprite);
	}

	private void RefreshPortraitOptions()
	{
		(List<PortraitMakerService.Piece> faces, List<PortraitMakerService.Piece> busts, List<PortraitMakerService.Piece> hats) allPortraitPiecesForCustom = Game.serv.portraits.GetAllPortraitPiecesForCustom(peep.data.person.Gender, peep.data.person.Skin);
		List<PortraitMakerService.Piece> item = allPortraitPiecesForCustom.busts;
		List<PortraitMakerService.Piece> item2 = allPortraitPiecesForCustom.hats;
		_availableBusts = item;
		bustIndex = _availableBusts.FindIndex((PortraitMakerService.Piece x) => x.asset == _portraitInfo.bust);
		_availableHats = item2;
		hatIndex = _availableHats.FindIndex((PortraitMakerService.Piece x) => x.asset == _portraitInfo.hat);
		if (hatIndex == -1)
		{
			hatIndex = _availableHats.Count;
		}
	}

	private void SelectBust(int delta)
	{
		bustIndex = MathUtil.Modulus(bustIndex + delta, _availableBusts.Count);
		_portraitInfo.bust = _availableBusts[bustIndex].asset;
		RefreshPortraitsDisplay();
		RefreshButtons();
	}

	private void SelectHat(int delta)
	{
		List<PackID> installedPacks = Game.serv.store.FindAllInstalledPacks().ToList();
		PersonData person = peep.data.person;
		hatIndex = MathUtil.Modulus(hatIndex + delta, _availableHats.Count + 1);
		string hat = ((hatIndex == _availableHats.Count) ? null : _availableHats[hatIndex].asset);
		string text = ((hatIndex == _availableHats.Count) ? null : _availableHats[hatIndex].hatmaskOverride);
		_portraitInfo.hat = hat;
		_portraitInfo.hatmask = text ?? PortraitMakerService.HAT_MASKS.FindOrNull(person.Gender, _portraitInfo.hash, "HAT_MASK", installedPacks, person.Ethnicity);
		RefreshPortraitsDisplay();
		RefreshButtons();
	}

	private void RefreshButtons()
	{
		PortraitInfo portrait = peep.data.person.portrait;
		bool flag = portrait.bust == _portraitInfo.bust;
		bool flag2 = portrait.hat == _portraitInfo.hat;
		_go.GetChild("Character/Biography/Mugshot/Customize/Confirm").SetActive(!(flag && flag2));
		bool flag3 = Game.ctx.players.Human.crew.IsOnBoard(CrewMember) || Game.ctx.players.Human.schemes.IsInScheme(CrewMember);
		_go.GetChild("Footer/Interact Buttons/Interact").SetActive(CrewMember.data.agent?.xp?.GetCrewRole() != null && flag3);
		_go.GetChild("Footer/Interact Buttons/Levelup").SetActive(CrewMember.components.agent.CanShowLevelupPopup());
		SchemeData schemeForCrew = Game.ctx.players.Human.schemes.GetSchemeForCrew(CrewMember);
		_go.SetText("Footer/Interact Buttons/Interact/Text", (schemeForCrew == null) ? Loc.Get("ui.crewinspect.buttons.interact.speak") : Loc.Get("ui.crewinspect.buttons.interact.scheme"));
	}

	private void UpdatePortrait()
	{
		peep.data.person.portrait = Game.serv.serializer.instance.Clone(_portraitInfo);
		Sprite spriteFor = Game.ctx.hud.portraits.GetSpriteFor(peep);
		Game.serv.portraits.ResetPortrait(spriteFor);
		Game.serv.portraits.PopulateCompositePortrait(_portraitInfo, spriteFor);
		RefreshButtons();
	}

	private void InitializeCrewHistoryStats()
	{
		List<CrewSettings.StatCategory> data = Game.serv.globals.settings.people.social.crew.statCategories.Where((CrewSettings.StatCategory x) => ShouldShowCategory(x)).ToList();
		GameObject child = _panel.GetChild("Character/Criminal Info/Viewport/Content/HistoryStats");
		child.EnsureChildCount(data, _templateStatCat);
		child.InitializeChildren(data, InitStatCategory);
		bool ShouldShowCategory(CrewSettings.StatCategory cat)
		{
			foreach (CrewStats entry in cat.entries)
			{
				if (CrewMember.data.agent.crewHistoryStats[entry] != 0)
				{
					return true;
				}
			}
			return false;
		}
	}

	private void InitStatCategory(int i, GameObject card, CrewSettings.StatCategory cat)
	{
		card.SetText("Title", Loc.Get(cat.locname));
		List<CrewStats> data = cat.entries.Where((CrewStats x) => CrewMember.data.agent.crewHistoryStats[x] != 0).ToList();
		GameObject child = card.GetChild("Stats");
		child.EnsureChildCount(data, _templateStatItem);
		child.InitializeChildren(data, InitStatItem);
	}

	private void InitStatItem(int i, GameObject card, CrewStats stat)
	{
		card.SetText("Title", Loc.GetCrewStatName(stat));
		card.SetText("Value", Loc.GetCrewStatUnit(stat, CrewMember.data.agent.crewHistoryStats[stat]));
	}

	private void InitializeCrewLevelups()
	{
		List<PersonInfoUtil.LevelupInfo> data = (peep.components.agent?.GetLevelupsSortedOrNull())?.Select((XP.Levelup l) => PersonInfoUtil.LevelupInfo.Make(l, detailed: false)).ToList() ?? new List<PersonInfoUtil.LevelupInfo>();
		GameObject child = _go.GetChild("Templates/Levelup");
		GameObject child2 = _go.GetChild("Character/Criminal Info/Viewport/Content/Levelups");
		child2.EnsureChildCount(data, child);
		child2.InitializeChildren(data, InitLevelup);
	}

	private void InitLevelup(int i, GameObject card, PersonInfoUtil.LevelupInfo info)
	{
		LevelupChain levelupChain = info.levelup.FindLevelupChain();
		card.SetText("Icon", Loc.Get(levelupChain.locicon));
		card.SetText("Name", Loc.Get(levelupChain.locname));
		card.SetText("Value", info.levelup.level.ToString());
	}

	private void NextCrewClick()
	{
		ChangeCrew(1);
	}

	private void PrevCrewClick()
	{
		ChangeCrew(-1);
	}

	private void ChangeCrew(int delta)
	{
		List<CrewAssignment> list = Game.ctx.players.Human.crew.GetLiving().ToList();
		int num = list.IndexOf(peep.components.agent.FindCrewAssignment());
		peep = list[MathUtil.Modulus(num + delta, list.Count)].GetPeep();
		RefreshDialog();
	}

	private void OnCrewInteractClick()
	{
		SchemeData schemeForCrew = Game.ctx.players.Human.schemes.GetSchemeForCrew(CrewMember);
		if (schemeForCrew == null)
		{
			Game.ctx.players.Human.schemes.CrewInteract(CrewMember);
			Close();
		}
		else
		{
			Game.serv.ui.AddPopup(new SchemePopup(schemeForCrew));
			Close();
		}
	}

	private void OnOrgChartClick()
	{
		if (Game.serv.ui.ContainsPopup<OrgChartPopup>() && !(Game.serv.ui.TopPopupUnsafe is OrgChartPopup))
		{
			Game.serv.ui.RemovePopup<OrgChartPopup>();
		}
		Game.serv.ui.AddPopup(new OrgChartPopup());
		Close();
	}

	private void OnRelClick()
	{
		Game.ctx.hud.personInfo.Show(CrewMember);
		Close();
	}

	private void OnLevelupClick()
	{
		CrewMember.components.agent.ShowLevelupPopup();
		Close();
	}
}

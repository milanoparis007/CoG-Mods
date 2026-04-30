using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.UI;

public class PoliticianInfoPopup : BasePopup
{
	private const string CLOSE_BUTTON = "Close/";

	private const string INTERACT_BUTTON = "Footer/Interact Buttons/Interact";

	private const string RELATIONSHIP_BUTTON = "Footer/Interact Buttons/Relationships";

	private const string LOCATION_BUTTON = "Footer/Interact Buttons/Location";

	private Entity peep;

	private const string POLITICIAN_NAME = "Header/Title";

	private const string PORTRAIT = "Character/Biography/Portrait/Portrait";

	private const string ARCHETYPE_NAME = "Character/Biography/Archetype/Value";

	private const string ARCHETYPE_DESC = "Character/Biography/Archetype/Description";

	private const string ETHNICITY_NAME = "Character/Biography/Ethnicity/Value";

	private const string ETHNICITY_DESC = "Character/Biography/Ethnicity/Description";

	private const string SPONSOR_NAME = "Character/Biography/Sponsor/Value";

	private const string SPONSOR_DESC = "Character/Biography/Sponsor/Description";

	private const string POLITICAL_HISTORY_NAME = "Character/Biography/Political History/Value";

	private const string POLITICAL_HISTORY_DESC = "Character/Biography/Political History/Description";

	private const string TRAITS_NAME = "Character/Biography/Traits/Value";

	private const string TRAITS_DESC = "Character/Biography/Traits/Description";

	public override UIReference UIReference => UIElements.PoliticianInfoPopup;

	public PoliticianInfoPopup(EntityID crewPeep)
	{
		peep = crewPeep.FindEntity();
	}

	protected override void InitializeOnPush()
	{
		RefreshDialog();
		_go.GetButton("Close/").onClick.AddListener(Close);
		_go.GetButton("Footer/Interact Buttons/Relationships").onClick.AddListener(OnRelClick);
		_go.GetButton("Footer/Interact Buttons/Location").onClick.AddListener(OnLocationClick);
	}

	protected override void ReleaseOnPop()
	{
		_go.GetButton("Close/").onClick.RemoveListener(Close);
		_go.GetButton("Footer/Interact Buttons/Relationships").onClick.RemoveListener(OnRelClick);
		_go.GetButton("Footer/Interact Buttons/Location").onClick.RemoveListener(OnLocationClick);
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
		RefreshHeader();
		RefreshCrewInfo();
		RefreshButtons();
	}

	private void RefreshHeader()
	{
		_go.SetText("Header/Title", peep.data.person.FullName);
	}

	private void RefreshCrewInfo()
	{
		PoliticianData politicianData = Game.ctx.simman.politics.GetPoliticianData(peep.Id);
		_go.SetImage("Character/Biography/Portrait/Portrait", HUDUtil.GetCrewSprite(peep));
		PoliticsSettings.PoliticalArchetype politicalArchetype = Game.serv.globals.settings.politics.FindPoliticalArchetype(politicianData.archetypeId);
		_go.SetText("Character/Biography/Archetype/Value", Loc.Get(politicalArchetype.locname));
		_go.SetText("Character/Biography/Archetype/Description", Loc.Get(politicalArchetype.locdesc));
		EthnicityDef ethDef = peep.data.person.GetEthDef();
		_go.SetText("Character/Biography/Ethnicity/Value", Loc.Get(ethDef.loc.adjEthnicity));
		_go.SetText("Character/Biography/Ethnicity/Description", Loc.Get("ui.politician-info.ethnicity-effect"));
		bool flag = Game.ctx.simman.politics.IsPoliticianIncumbent(peep.Id);
		string text = (flag ? Loc.Get("ui.politician-info.is-incumbent", "won", politicianData.incumbencies) : Loc.Get("ui.politician-info.not-incumbent", "won", politicianData.incumbencies));
		_go.SetText("Character/Biography/Political History/Value", text);
		_go.SetText("Character/Biography/Political History/Description", Loc.Get(GetIncumbencyKey(flag, politicianData.incumbencies)));
		_go.SetText("Character/Biography/Traits/Value", PersonInfoUtil.GenerateTraitsIcons(peep));
		_go.SetText("Character/Biography/Traits/Description", PersonInfoUtil.GeneratePoliticalTraitsParagraph(peep));
	}

	private string GetIncumbencyKey(bool isIncumbent, int incumbencies)
	{
		if (isIncumbent)
		{
			return "ui.politician-info.incumbent.desc";
		}
		if (incumbencies >= 1)
		{
			return "ui.politician-info.won-before.desc";
		}
		return "ui.politician-info.never-won.desc";
	}

	private void RefreshButtons()
	{
		PoliticianData politicianData = Game.ctx.simman.politics.GetPoliticianData(peep.Id);
		_go.GetChild("Footer/Interact Buttons/Location").SetActive(politicianData.location.IsValid);
	}

	private void OnRelClick()
	{
		Game.ctx.hud.personInfo.Show(peep);
		Close();
	}

	private void OnLocationClick()
	{
		PersonInfoUtil.TweenCameraToEntity(Game.ctx.simman.politics.GetPoliticianData(peep.Id).location);
		Close();
	}
}

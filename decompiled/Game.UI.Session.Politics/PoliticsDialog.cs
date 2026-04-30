using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Session.OwnedBiz;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Politics;

public sealed class PoliticsDialog : HUDView<PoliticsModel, PoliticsDialog, PoliticsController>
{
	private GameObject _tmplButton;

	private GameObject _tmplCandidate;

	private GameObject _tmplEvent;

	private GameObject _toggles;

	private GameObject _tmplVoteBreakdown;

	private GameObject _tmplLosers;

	private GameObject _tmplAddCandidate;

	private GameObject _tmplPrevStages;

	private GameObject _tmplNextStages;

	private GameObject _tmplShortVoteBreakdown;

	public const string MODULE_BUTTONS_PARENT = "Background/Modules";

	public const string MODULE_BUTTONS = "Background/Modules/Buttons";

	public const string TMPL_CONTAINER = "Templates";

	public const string TMPL_NEARBY_CREW = "Templates/Nearby Crew";

	public const string TMPL_MODULE_TOGGLE = "Templates/Module Toggle";

	public const string TMPL_VOTE_BREAKDOWN = "Templates/Votes";

	public const string TMPL_EVENT_ITEM = "Templates/Event";

	public const string TMPL_CANDIDATE = "Templates/Candidate";

	public const string TMPL_LOSER = "Templates/Loser Info";

	public const string TMPL_ADD_CANDIDATE = "Templates/Add Candidate";

	public const string TMPL_UPCOMING_STAGE = "Templates/Coming Stage";

	public const string TMPL_PAST_STAGE = "Templates/Past Stage";

	public const string TMPL_VOTES_SHORT = "Templates/Votes Short";

	public const string PANEL_CLOSE = "Panel/Close";

	public const string PANEL_FOOTER_LIST = "Panel/Footer/Scroll View List/Viewport/Content";

	public const string NEARBY_CREW_PORTRAIT = "Background/Basic Portrait/Portrait";

	public const string NEARBY_CREW_NAME = "Text";

	public const string CANDIDATES_CONTAINER = "Panel/Campaign Subview/Election Info/Candidate Scroll List/Viewport/Content";

	public const string NOMINATE_BUTTON = "Panel/Campaign Subview/Nominate Button";

	public const string SHOP_BUTTON = "Panel/Finish Subview/Shop";

	public const string EVENT_LOG_CONTAINER = "Panel/Campaign Subview/Election Info/Event Log/Event Log Scroll List/Viewport/Content";

	public const string EVENT_LOG_WHOLE = "Panel/Campaign Subview/Election Info/Event Log";

	public const string ELECTION_CAMPAIGN_SUBVIEW = "Panel/Campaign Subview";

	public const string ELECTION_FINISH_SUBVIEW = "Panel/Finish Subview";

	public const string PREDICTED_WINNER_TEXT = "Panel/Campaign Subview/Predicted Winner/Header";

	public const string PREDICTED_WINNER_TOTALS = "Panel/Campaign Subview/Predicted Winner/Totals";

	public const string BAR = "Percentage Bar";

	public const string TEXT = "Percentage Bar/Text";

	public const string ERROR_BAR = "Percentage Bar/Error Bar";

	public const string NAME = "Name";

	public const string HEADER_BG = "Header";

	public const string CANDIDATE_NAME = "Header/Name";

	public const string CANDIDATE_WARCHEST = "Header/Warchest";

	public const string CANDIDATE_SPONSOR = "Buttons/Nominate Button";

	public const string CANDIDATE_INTERACT = "Buttons/Interact Button";

	public const string CANDIDATE_IS_SPONSORED = "Header/Is Sponsored";

	public const string CANDIDATE_PORTRAIT = "Info/Portrait/Portrait";

	public const string CANDIDATE_VOTES = "Info/Votes";

	public const string CANDIDATE_ARCHETYPE = "Info/Archetype";

	public const string CANDIDATE_TRAITS = "Info/Traits";

	public const string CANDIDATE_PEDIGREE = "Info/Pedigree";

	public const string CANDIDATE_ETHNICITY = "Info/Ethnicity";

	public const string INFO_TEXT = "/Text";

	public const string EVENT_STRING = "Text";

	public const string EVENT_PORTRAIT = "Portrait/Portrait";

	public const string EVENT_BY_SPONSORED = "Is Sponsored";

	public const string WINNER_PORTRAIT = "Panel/Finish Subview/Winner Banner/Winner Portrait/Portrait";

	public const string WINNER_NAME = "Panel/Finish Subview/Winner Banner/Winner Name/Text";

	public const string LOSERS_CONTAINER = "Panel/Finish Subview/Losers/Viewport/Content";

	public const string WINNER_YEAR = "Panel/Finish Subview/Winner Banner/Election Year/Text";

	public const string VOTES_CONTAINER = "Panel/Finish Subview/Winner Banner/Totals";

	public const string INFLUENCE_CHANGE = "Panel/Finish Subview/Influence Gain";

	public const string INFLUENCE_CHANGE_TEXT = "Panel/Finish Subview/Influence Gain/Text";

	public const string WINNER_SPONSOR_INDICATOR = "Panel/Finish Subview/Winner Banner/Is Sponsored";

	public const string LOSER_PORTRAIT = "Portrait/Portrait";

	public const string LOSER_NAME = "Name";

	public const string LOSER_VOTES = "Votes";

	public const string LOSER_COLOR = "Loser Color";

	public const string WARD_NAME = "Panel/Header/Ward Name";

	public const string STAGE_BAR = "Panel/Header/Bar";

	public const string WEEKS_LEFT = "Panel/Header/Bar/Weeks Left";

	public const string CURRENT_STAGE_TEXT = "Panel/Header/Bar/Before/Current Stage/Name";

	public const string NEXT_STAGE_TEXT = "Panel/Header/Bar/After/Next Stage/Name";

	public const string NEXT_STAGE = "Panel/Header/Bar/After/Next Stage";

	public const string STAGES_PAST_TOTAL = "Panel/Header/Bar/Before/Passed Stages";

	public const string STAGES_TO_COME = "Panel/Header/Bar/After/Coming Stages";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.HUDPolitics;

	internal void Show(VisitState visit)
	{
		base.Controller.SetModel(visit);
		if (base.Model.ThisWard.ElectionOngoing)
		{
			RefreshContents();
		}
		base.Show();
	}

	public override void Show()
	{
	}

	internal override void Initialize()
	{
		base.Initialize();
		_go.SetButtonListener("Panel/Close", base.Controller.OnCloseButton);
		_tmplButton = _go.GetChild("Templates/Module Toggle");
		_tmplEvent = _go.GetChild("Templates/Event");
		_tmplVoteBreakdown = _go.GetChild("Templates/Votes");
		_tmplCandidate = _go.GetChild("Templates/Candidate");
		_tmplLosers = _go.GetChild("Templates/Loser Info");
		_tmplAddCandidate = _go.GetChild("Templates/Add Candidate");
		_tmplNextStages = _go.GetChild("Templates/Coming Stage");
		_tmplPrevStages = _go.GetChild("Templates/Past Stage");
		_tmplShortVoteBreakdown = _go.GetChild("Templates/Votes Short");
		_toggles = _go.GetChild("Background/Modules/Buttons");
		_toggles.GetComponent<ToggleGroup>().allowSwitchOff = true;
		Game.serv.mouseovers.Register(MouseoverType.PoliticsSponsor, new SponsorButtonMouseover());
		Game.serv.mouseovers.Register(MouseoverType.PoliticianCard, new PoliticianCardMouseover());
	}

	internal override void Release()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.PoliticsSponsor);
		Game.serv.mouseovers.Unregister(MouseoverType.PoliticianCard);
		base.Release();
	}

	internal GameObject GetTmpl(string path)
	{
		return _go.GetChild(path);
	}

	protected override void OnAfterShow()
	{
		base.OnAfterShow();
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
		RefreshHeader();
		ToggleSubviews();
		if (base.Model.ThisWard.currElection != null)
		{
			RefreshPredicted();
			RefreshCandidates();
			RefreshEventLog();
		}
		if (base.Model.ThisWard.currElection == null || !base.Model.ThisWard.currElection.IsUnresolved)
		{
			RefreshResult();
		}
		RefreshModuleButtons();
		RefreshFooter();
	}

	public void ToggleSubviews()
	{
		_ = base.Model.ThisWard.currElection?.stage;
		if (base.Model.ThisWard.currElection == null || !base.Model.ThisWard.currElection.IsUnresolved)
		{
			_go.GetChild("Panel/Finish Subview").SetActive(value: true);
			_go.GetChild("Panel/Campaign Subview").SetActive(value: false);
		}
		else
		{
			_go.GetChild("Panel/Finish Subview").SetActive(value: false);
			_go.GetChild("Panel/Campaign Subview").SetActive(value: true);
		}
	}

	private void RefreshPredicted()
	{
		Election.ElectionResult electionResult = (base.Model.ThisWard.ElectionOngoing ? base.Model.ThisWard.currElection.displayModel : base.Model.ThisWard.mostRecentElectionResult);
		_go.SetText("Panel/Campaign Subview/Predicted Winner/Header", Loc.Get("ui.politics.predicted-winner", "name", electionResult.winner.FindEntity().data.person.FullName));
		_go.GetChild("Panel/Campaign Subview/Predicted Winner/Totals").EnsureChildCount(electionResult.stats.ToList(), _tmplVoteBreakdown);
		_go.GetChild("Panel/Campaign Subview/Predicted Winner/Totals").InitializeChildren(electionResult.stats.ToList(), InitializeVoteTotal);
	}

	private void InitializeVoteTotal(int count, GameObject card, KeyValuePair<EntityID, Election.CandidateInfo> votes)
	{
		Election.ElectionResult electionResult = (base.Model.ThisWard.ElectionOngoing ? base.Model.ThisWard.currElection.displayModel : base.Model.ThisWard.mostRecentElectionResult);
		Fixnum percentage = new Fixnum((float)votes.Value.votes / (float)electionResult.stats.Select((KeyValuePair<EntityID, Election.CandidateInfo> x) => x.Value.votes).Sum());
		int baseSamplingNumber = Game.serv.globals.settings.politics.elections.baseSamplingNumber;
		Fixnum samplingError = ((base.Model.ThisWard.ElectionOngoing && base.Model.ThisWard.currElection.IsUnresolved) ? base.Model.ThisWard.currElection.CalculateSamplingError(votes.Key, baseSamplingNumber) : ((Fixnum)0));
		InitializeBar();
		TryInitializeName();
		void InitializeBar()
		{
			RectTransform obj = card.GetChild("Percentage Bar").transform as RectTransform;
			obj.sizeDelta = obj.sizeDelta.SetX(((float)percentage + (float)samplingError / 2f) * 100f);
			card.GetChild("Percentage Bar").GetComponent<Image>().color = ColorConstants.PoliticianColors[votes.Value.colorIndex].primary;
			InitializeError();
		}
		void InitializeError()
		{
			if (samplingError != 0)
			{
				card.SetText("Percentage Bar/Text", Loc.Get("ui.politics.votetotal-percent", "percentage", Loc.Percentage(percentage), "plusOrMinus", Loc.Percentage(samplingError)));
			}
			else
			{
				card.SetText("Percentage Bar/Text", Loc.Percentage(percentage));
			}
			RectTransform obj = card.GetChild("Percentage Bar/Error Bar").transform as RectTransform;
			obj.sizeDelta = obj.sizeDelta.SetX((float)samplingError * 100f);
		}
		void TryInitializeName()
		{
			GameObject gameObject = card.transform.FindGameObject("Name", recursive: true);
			string text = (votes.Value.sponsor.IsHumanPlayer ? (Loc.Get("ui.politics.sponsored-indicator") + " ") : "");
			if (gameObject != null)
			{
				card.GetText("Percentage Bar/Text").color = ColorConstants.PoliticianColors[votes.Value.colorIndex].dark;
				card.SetText("Name", text + votes.Key.FindEntity().data.person.LastName);
			}
		}
	}

	private void RefreshCandidates()
	{
		GameObject child = _go.GetChild("Panel/Campaign Subview/Election Info/Candidate Scroll List/Viewport/Content");
		Election.ElectionResult obj = (base.Model.ThisWard.ElectionOngoing ? base.Model.ThisWard.currElection.displayModel : base.Model.ThisWard.mostRecentElectionResult);
		List<KeyValuePair<EntityID, Election.CandidateInfo>> data = obj.stats.OrderByDescending((KeyValuePair<EntityID, Election.CandidateInfo> x) => x.Value.votes).ToList();
		child.DestroyAllChildren();
		child.EnsureChildCount(data, _tmplCandidate);
		child.InitializeChildren(data, RefreshCandidate);
		if (base.Model.ThisWard.ElectionOngoing && base.Model.ThisWard.currElection.stage == Election.ElectionStage.Nomination && !base.Model.ThisWard.currElection.HasSponsoredACandidate(PlayerID.HumanPlayer) && base.Model.ThisWard.currElection.allCandidates.Count < 3)
		{
			Object.Instantiate(_tmplAddCandidate, child.transform).GetButton().onClick.SetListener(base.Controller.OnNominateButtonClick);
		}
	}

	private void RefreshCandidate(int count, GameObject card, KeyValuePair<EntityID, Election.CandidateInfo> votes)
	{
		PoliticianData politicianData = Game.ctx.simman.politics.GetPoliticianData(votes.Key);
		PoliticsSettings.PoliticalArchetype politicalArchetype = Game.serv.globals.settings.politics.FindPoliticalArchetype(politicianData.archetypeId);
		bool isIncumbent = votes.Key == base.Model.ThisWard.currentPolitician;
		string incumbentText = (isIncumbent ? Loc.Get("ui.politics.candidate.incumbency.is-incumbent") : Loc.Get("ui.politics.candidate.incumbency.not"));
		EthnicityDef ethDef = votes.Key.FindEntity().data.person.GetEthDef();
		card.GetChild("Header/Is Sponsored").SetActive(votes.Value.sponsor.IsHumanPlayer);
		InitializeHeader();
		card.SetImage("Info/Portrait/Portrait", HUDUtil.GetCrewSprite(votes.Key.FindEntity()));
		InitializeInfoBox("Info/Pedigree", Loc.Get("ui.politics.candidate.pedigree", "num", politicianData.incumbencies), PoliticianCardCtx.CtxType.PedigreeInfo);
		InitializeInfoBox("Info/Traits", PersonInfoUtil.GenerateTraitsIcons(votes.Key.FindEntity()), PoliticianCardCtx.CtxType.TraitsInfo);
		InitializeInfoBox("Info/Archetype", Loc.Get(politicalArchetype.locname), PoliticianCardCtx.CtxType.ArchetypeInfo);
		InitializeInfoBox("Info/Ethnicity", Loc.Get(ethDef.loc.adjEthnicity) + " " + Loc.Get(ethDef.loc.icon), PoliticianCardCtx.CtxType.EthnicityInfo);
		InitializeSponsorButton();
		InitializeInteractButton();
		InitializeVoteTotal(count, card.GetChild("Info/Votes"), votes);
		bool CanPayForSponsor()
		{
			return Game.ctx.simman.politics.GetSponsorPrice(votes.Key) <= ModulesUtil.GetInventory(base.Model.visit.vehicle).data.money.cash;
		}
		void InitializeHeader()
		{
			card.GetImage("Header").color = ColorConstants.PoliticianColors[votes.Value.colorIndex].dark;
			card.SetText("Header/Name", Loc.Get("ui.politics.candidate.header", "name", votes.Key.FindEntity().data.person.FullName, "incumbency", incumbentText));
			card.SetText("Header/Warchest", Loc.Get("ui.politics.candidate.warchest", "value", Loc.Money(votes.Value.warchest)));
			card.GetChild("Header/Name").GetOrAddComponent<PoliticianCardCtx>().Set(PoliticianCardCtx.CtxType.PedigreeInfo, votes.Key);
		}
		void InitializeInfoBox(string path, string contents, PoliticianCardCtx.CtxType type)
		{
			card.GetImage(path).color = ColorConstants.PoliticianColors[votes.Value.colorIndex].alpha;
			card.SetText(path + "/Text", contents);
			card.GetChild(path + "/Text").GetOrAddComponent<PoliticianCardCtx>().Set(type, votes.Key);
		}
		void InitializeInteractButton()
		{
			bool active = base.Model.ThisWard.ElectionOngoing && (base.Model.ThisWard.currElection.GetSponsoredCandidate(PlayerID.HumanPlayer) == votes.Key || isIncumbent);
			card.GetChild("Buttons/Interact Button").SetActive(active);
			card.GetButton("Buttons/Interact Button").onClick.SetListener(delegate
			{
				base.Controller.OnCandidateInteractButtonClick(votes.Key);
			});
		}
		void InitializeSponsorButton()
		{
			bool active = base.Model.ThisWard.ElectionOngoing && base.Model.ThisWard.currElection.stage == Election.ElectionStage.Nomination && !base.Model.ThisWard.currElection.HasSponsoredACandidate(PlayerID.HumanPlayer) && base.Model.ThisWard.currElection.allCandidates.Count > 1;
			card.GetChild("Buttons/Nominate Button").SetActive(active);
			card.GetChild("Buttons/Nominate Button").GetOrAddComponent<SponsorCtx>().Set(votes.Key, isNomination: false);
			card.GetButton("Buttons/Nominate Button").onClick.SetListener(delegate
			{
				base.Controller.OnCandidateSponsorButtonClick(votes.Key);
			});
			card.GetButton("Buttons/Nominate Button").interactable = base.Model.visit.vehicle != null && CanPayForSponsor();
		}
	}

	private void RefreshEventLog()
	{
		GameObject child = _go.GetChild("Panel/Campaign Subview/Election Info/Event Log/Event Log Scroll List/Viewport/Content");
		GameObject child2 = _go.GetChild("Panel/Campaign Subview/Election Info/Event Log");
		Election.ElectionResult electionResult = (base.Model.ThisWard.ElectionOngoing ? base.Model.ThisWard.currElection.displayModel : base.Model.ThisWard.mostRecentElectionResult);
		bool flag = electionResult.events.Count > 0;
		child2.SetActive(flag);
		if (flag)
		{
			child.EnsureChildCount(electionResult.events, _tmplEvent);
			child.InitializeChildren(electionResult.events, RefreshEvent);
		}
	}

	private void RefreshEvent(int count, GameObject card, Election.ElectionEvent eventData)
	{
		Election.ElectionResult electionResult = (base.Model.ThisWard.ElectionOngoing ? base.Model.ThisWard.currElection.displayModel : base.Model.ThisWard.mostRecentElectionResult);
		card.SetText("Text", eventData.desc);
		card.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite(eventData.candidate.FindEntity()));
		card.GetChild("Is Sponsored").SetActive(electionResult.stats[eventData.candidate].sponsor.IsHumanPlayer);
	}

	public void RefreshResult()
	{
		Election.ElectionResult result = (base.Model.ThisWard.ElectionOngoing ? base.Model.ThisWard.currElection.displayModel : base.Model.ThisWard.mostRecentElectionResult);
		if (!result.winner.IsNotValid)
		{
			_go.SetImage("Panel/Finish Subview/Winner Banner/Winner Portrait/Portrait", HUDUtil.GetCrewSprite(result.winner.FindEntity()));
			_go.SetText("Panel/Finish Subview/Winner Banner/Winner Name/Text", result.winner.FindEntity().data.person.FullName);
			_go.SetText("Panel/Finish Subview/Winner Banner/Election Year/Text", result.year.ToDate().Year.ToString());
			_go.GetChild("Panel/Finish Subview/Winner Banner/Is Sponsored").SetActive(result.stats[result.winner].sponsor.IsHumanPlayer);
			GameObject child = _go.GetChild("Panel/Finish Subview/Losers/Viewport/Content");
			List<KeyValuePair<EntityID, Election.CandidateInfo>> data = result.stats.Where((KeyValuePair<EntityID, Election.CandidateInfo> x) => x.Key != result.winner).ToList();
			child.EnsureChildCount(data, _tmplLosers);
			child.InitializeChildren(data, InitializeLoser);
			GameObject child2 = _go.GetChild("Panel/Finish Subview/Winner Banner/Totals");
			child2.EnsureChildCount(result.stats.ToList(), _tmplShortVoteBreakdown);
			child2.InitializeChildren(result.stats.ToList(), InitializeVoteTotal);
			Fixnum humanInfluenceFrom = Game.ctx.simman.politics.GetHumanInfluenceFrom(result.winner);
			_go.GetChild("Panel/Finish Subview/Influence Gain").SetActive(humanInfluenceFrom > 0);
			_go.SetText("Panel/Finish Subview/Influence Gain/Text", Loc.Get("ui.politics.result.influence-change", "influence", humanInfluenceFrom));
		}
	}

	public void InitializeLoser(int count, GameObject card, KeyValuePair<EntityID, Election.CandidateInfo> candidate)
	{
		(Color, Color, Color) tuple = ColorConstants.PoliticianColors[candidate.Value.colorIndex];
		card.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite(candidate.Key.FindEntity()));
		card.SetText("Name", Loc.Get("ui.politics.result-loser", "ordinal", Loc.GetPluralized("ui.numberplacement", count + 2), "name", TextUtil.ColorWrap(candidate.Key.FindEntity().data.person.FullName, tuple.Item1)));
		card.GetImage("Loser Color").color = tuple.Item3;
		InitializeVoteTotal(count, card.GetChild("Votes"), candidate);
	}

	private void RefreshModuleButtons()
	{
		_toggles.DestroyAllChildren();
		ModuleToggleUtils.MakePoliticianToggle(_toggles.transform, _tmplButton, base.Model.visit, selected: false).transform.SetAsFirstSibling();
		ModuleToggleUtils.MakePoliticsToggle(_toggles.transform, _tmplButton, base.Model.visit, selected: true);
		ModuleToggleUtils.MakeCornerToggle(_toggles.transform, _tmplButton, base.Model.visit);
		_toggles.transform.GetChild(1).GetComponentInChildren<Toggle>().SetIsOnWithoutNotify(value: true);
	}

	private void FakePressConversationToggle()
	{
		_toggles.transform.GetChild(0).GetComponentInChildren<Toggle>().isOn = true;
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
		_go.SetText("Panel/Header/Ward Name", base.Model.ThisWard.WardName);
		RefreshStageTracker();
	}

	private void RefreshStageTracker()
	{
		Election currElection = base.Model.ThisWard.currElection;
		bool isLastStage = currElection != null && currElection.stage == Election.ElectionStage.Legislation;
		TryHidingElementsIfNeeded();
		if (base.Model.ThisWard.ElectionOngoing)
		{
			InitializeCurrentAndNextStageText();
			InitializeBarAndStagesPastOrComing();
		}
		void InitializeBarAndStagesPastOrComing()
		{
			_go.SetText("Panel/Header/Bar/Weeks Left", Loc.Get("ui.politics.election-stage.weeks-left", "weeks", base.Model.ThisWard.currElection.GetWeeksLeftToNextStage()));
			_go.GetChild("Panel/Header/Bar/Before/Passed Stages").EnsureChildCount((int)(base.Model.ThisWard.currElection.stage - 1), _tmplPrevStages);
			_go.GetChild("Panel/Header/Bar/After/Coming Stages").EnsureChildCount(MathUtil.ClampMin((int)(4 - base.Model.ThisWard.currElection.stage), 0), _tmplNextStages);
		}
		void InitializeCurrentAndNextStageText()
		{
			_go.SetText("Panel/Header/Bar/Before/Current Stage/Name", Loc.GetCampaignStage(base.Model.ThisWard.currElection.stage));
			if (!isLastStage)
			{
				_go.SetText("Panel/Header/Bar/After/Next Stage/Name", Loc.GetCampaignStage(base.Model.ThisWard.currElection.stage + 1));
			}
		}
		void TryHidingElementsIfNeeded()
		{
			_go.GetChild("Panel/Header/Bar").SetActive(base.Model.ThisWard.ElectionOngoing);
			_go.GetChild("Panel/Header/Bar/After/Next Stage").SetActive(!isLastStage);
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

	public Fixnum GetPercent(int votes, Election.ElectionResult result)
	{
		return new Fixnum((float)votes / (float)result.stats.Select((KeyValuePair<EntityID, Election.CandidateInfo> x) => x.Value.votes).Sum());
	}
}

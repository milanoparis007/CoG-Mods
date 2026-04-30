using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Mouseovers;
using Game.UI.Session;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public class SchemePopup : BasePopup
{
	private const string CLOSE_BUTTON = "Close Button";

	private const string EMERGENCY_STOP_SCHEME = "Stop Scheme";

	private const string END_SCHEME = "End Scheme Button";

	private const string OK_BUTTON = "Footer/OK";

	private const string NEXT_BUTTON = "Footer/NextCrew";

	private const string PREV_BUTTON = "Footer/PrevCrew";

	private Xorshift _rng;

	private SchemeData scheme;

	private int displayIndex;

	public const string SCHEME_TITLE = "Panel/Title";

	public const string SCHEME_DESCRIPTION = "Panel/Description";

	public const string SCHEME_IMAGE = "Panel/Image";

	public const string STAGE_TITLE = "Panel/Stage Title/Width Cap/Content/Text";

	public const string INFO_CONTAINER = "Panel/Scroll View List/Viewport/Content";

	public const string CHAPTER_INFO = "Panel/Scroll View List/Viewport/Content/Chapter Info";

	public const string SCHEME_OPTIONS = "Panel/Scroll View List/Viewport/Content/Choices";

	public const string CHAPTER_TABS = "Panel/Chapters/Tabs";

	public const string CHAPTER_OVERALL_CONTAINER = "Panel/Chapters";

	public const string TEMPLATE_DECISION_CARD = "Templates/Decision Card";

	public const string TEMPLATE_TAB_CARD = "Templates/Stage Toggle";

	public const string CREW_PORTRAIT = "Portrait/Portrait";

	public const string SUCCESS_SUFFIX = ".success";

	public const string FAIL_SUFFIX = ".fail";

	public const string OPTION_TEXT = "Text";

	public const string OPTION_ICON = "Icon";

	public const string OPTION_COST = "Cost";

	public const string OPTION_GRANT = "Grants";

	public const string TAB_TEXT = "Text";

	public const string PROGRESS_INFO = "Panel/Progress Info";

	public const string PROGRESS_TEXT = "Panel/Progress Info/Days Left";

	public const string PROGRESS_BAR = "Panel/Progress Info/Progress Bar";

	public const int PROGRESS_BAR_SLIDER_WIDTH = 300;

	public SchemeSettings Settings => Game.serv.globals.settings.schemes;

	public Entity CrewMember => scheme.crewAssigned.FindEntity();

	public string CrewName => CrewMember.data.person.FullName;

	public override UIReference UIReference => UIElements.SchemePopup;

	public SchemePopup(SchemeData scheme)
	{
		this.scheme = scheme;
		displayIndex = -1;
	}

	protected override void InitializeOnPush()
	{
		RefreshDialog();
		RefreshTabs();
		_go.GetButton("End Scheme Button").onClick.AddListener(EndScheme);
		_go.GetButton("Stop Scheme").onClick.AddListener(OnEmergencyEndSchemeClick);
		_go.GetButton("Close Button").onClick.AddListener(Close);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnTurnPass);
		Game.serv.mouseovers.Register(MouseoverType.SchemeDecision, new DecisionMouseover());
		Game.serv.mouseovers.Register(MouseoverType.SchemeTab, new TabMouseover());
	}

	protected override void ReleaseOnPop()
	{
		_go.GetButton("End Scheme Button").onClick.RemoveListener(EndScheme);
		_go.GetButton("Stop Scheme").onClick.RemoveListener(OnEmergencyEndSchemeClick);
		_go.GetButton("Close Button").onClick.RemoveListener(Close);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnTurnPass);
		Game.serv.mouseovers.Unregister(MouseoverType.SchemeDecision);
		Game.serv.mouseovers.Unregister(MouseoverType.SchemeTab);
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new PopupHandlerWithTabSupport();
	}

	public void RefreshDialog()
	{
		RefreshCrewInfo();
		RefreshSchemeInfo();
		RefreshSelections();
		RefreshProgress();
		_go.ResetAllChildScrollViews(toStart: false);
		ForceRebuildLayoutImmediate(_go.GetChild("Panel/Scroll View List/Viewport/Content"));
		ForceRebuildLayoutImmediate(_go.GetChild("Panel/Chapters"));
	}

	public void RefreshCrewInfo()
	{
		if (CrewMember.data.person.IsAlive)
		{
			_go.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite(CrewMember));
		}
	}

	public void RefreshSchemeInfo()
	{
		SchemeDef schemeDef = Settings.FindSchemeById(scheme.schemeID);
		ChapterDef chapterDef = Settings.FindChapterById(scheme.currentChapter.chapterID);
		_go.SetText("Panel/Title", Loc.Get(schemeDef.display.loctitle));
		_go.SetText("Panel/Description", Loc.Get(schemeDef.display.locdesc, "name", CrewName));
		string chapterDescriptionString;
		if (displayIndex == -1)
		{
			_go.SetText("Panel/Stage Title/Width Cap/Content/Text", Loc.Get("ui.scheme.stage-title", "title", Loc.Get(chapterDef.loctitle), "number", scheme.schemeChapterHistory.Count + 1));
			chapterDescriptionString = GetChapterDescriptionString(chapterDef, scheme.currentChapter.succeeded, scheme.currentChapter.finishTime);
		}
		else
		{
			HistoryItem item = scheme.schemeChapterHistory[displayIndex];
			ChapterDef chapterDef2 = Settings.FindChapterById(item.chapterId);
			_go.SetText("Panel/Stage Title/Width Cap/Content/Text", Loc.Get("ui.scheme.stage-title", "title", Loc.Get(chapterDef2.loctitle), "number", displayIndex + 1));
			chapterDescriptionString = GetChapterDescriptionString(item);
		}
		_go.SetText("Panel/Scroll View List/Viewport/Content/Chapter Info", chapterDescriptionString);
		_go.SetImage("Panel/Image", ModulesUIUtil.FindLargeBannerOrNull(chapterDef.bannerImage));
	}

	public string GetChapterDescriptionString(HistoryItem item)
	{
		return GetChapterDescriptionString(Settings.FindChapterById(item.chapterId), item.success, item.decTime, history: true, item.chosenDec);
	}

	public string GetChapterDescriptionString(ChapterDef chapterDef, bool succeeded, SimTime decTime, bool history = false, string chosenText = null)
	{
		if (scheme.epilogueText != null && !history)
		{
			return scheme.epilogueText;
		}
		string text = "";
		string[] array = new string[2] { "name", CrewName };
		string text2 = text;
		object[] obj = new object[2] { "text", null };
		string locstartic = chapterDef.locstartic;
		object[] replacements = array;
		obj[1] = Loc.Get(locstartic, replacements);
		text = text2 + Loc.Get("ui.scheme.ic-formatting", obj) + "\n\n";
		text = text + Loc.Get(chapterDef.locstartoc, "name", CrewName) + "\n\n";
		if (scheme.IsWaitingForDecision() || history)
		{
			text = text + Loc.Get("ui.scheme.date-formatting", "text", Loc.FormatDateLong(decTime)) + "\n\n";
			if (succeeded)
			{
				text = text + Loc.Get(chapterDef.locendoc + ".success", "name", CrewName) + "\n\n";
				string text3 = text;
				object[] obj2 = new object[2] { "text", null };
				string locendicsuccess = chapterDef.locendicsuccess;
				replacements = array;
				obj2[1] = Loc.Get(locendicsuccess, replacements);
				text = text3 + Loc.Get("ui.scheme.ic-formatting", obj2) + "\n\n";
			}
			else
			{
				text = text + Loc.Get(chapterDef.locendoc + ".fail", "name", CrewName) + "\n\n";
				string text4 = text;
				object[] obj3 = new object[2] { "text", null };
				string locendicfail = chapterDef.locendicfail;
				replacements = array;
				obj3[1] = Loc.Get(locendicfail, replacements);
				text = text4 + Loc.Get("ui.scheme.ic-formatting", obj3) + "\n\n";
			}
		}
		if (chosenText != null)
		{
			string text5 = text;
			object[] obj4 = new object[2] { "text", null };
			replacements = array;
			obj4[1] = Loc.Get(chosenText, replacements);
			text = text5 + Loc.Get("ui.scheme.dec-formatting", obj4);
		}
		return text;
	}

	public void RefreshSelections()
	{
		_go.GetChild("Panel/Scroll View List/Viewport/Content/Choices").DestroyAllChildren();
		GameObject child = _go.GetChild("Templates/Decision Card");
		SchemeDef schemeDef = Settings.FindSchemeById(scheme.schemeID);
		ChapterDef chapterDef = Settings.FindChapterById(scheme.currentChapter.chapterID);
		ChapterData currentChapter = scheme.currentChapter;
		_ = scheme.epilogueText;
		BuildingAndBusinessData bbdata = (scheme.overallTarget.IsValid ? BuildingUtil.FindDataForBuilding(scheme.overallTarget) : default(BuildingAndBusinessData));
		VisitState visit = new VisitState(CrewMember.components.agent.FindCrewAssignment(), bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		visit.npc = Game.ctx.players.Human.crew.GetCrewForPlayerPeep().GetPeep();
		if (scheme.IsWaitingForDecision() && displayIndex == -1)
		{
			List<Decision> list = Game.ctx.players.Human.schemes.GenerateExtraSelections(chapterDef, schemeDef.id, currentChapter.succeeded);
			list.AddRange(currentChapter.succeeded ? chapterDef.successChoices : chapterDef.failChoices);
			List<Decision> list2 = list.Where((Decision x) => x.visReqs?.AllPass(visit) ?? true).ToList();
			if ((currentChapter.succeeded && chapterDef.randomizeChoiceDisplay == ChapterDef.RandomizeStyle.RandomizeSuccess) || (!currentChapter.succeeded && chapterDef.randomizeChoiceDisplay == ChapterDef.RandomizeStyle.RandomizeFail) || chapterDef.randomizeChoiceDisplay == ChapterDef.RandomizeStyle.RandomizeBoth)
			{
				list2 = new List<Decision> { CrewMember.data.ident.rng.PickElement(list2) };
			}
			_go.GetChild("Panel/Scroll View List/Viewport/Content/Choices").EnsureChildCount(list2, child);
			_go.GetChild("Panel/Scroll View List/Viewport/Content/Choices").InitializeChildren(list2, RefreshSelectionCard);
		}
		else
		{
			_go.GetChild("Panel/Scroll View List/Viewport/Content/Choices").DestroyAllChildren();
		}
	}

	public void RefreshSelectionCard(int index, GameObject card, Decision choice)
	{
		string[] array = new string[2] { "name", CrewName };
		bool flag = Game.ctx.players.Human.schemes.CanPayDecisionCost(choice);
		string text = choice.overrideText;
		if (text == null)
		{
			string locoption = choice.locoption;
			object[] replacements = array;
			text = Loc.Get(locoption, replacements);
		}
		card.SetText("Text", TextUtil.ColorEnabledIf(flag, text));
		card.SetActive("Icon", choice.nextState.IsSet);
		Button button = card.GetButton();
		button.onClick.SetListener(delegate
		{
			OnSelectionClick(choice);
		});
		button.interactable = flag;
		card.GetOrAddComponent<DecisionCtx>().Set(choice);
		if (choice.cost != null)
		{
			string text2 = "";
			foreach (ResOrCash item in choice.cost)
			{
				if (item.IsCash)
				{
					text2 = text2 + Loc.Money(-item.money) + " ";
				}
				if (item.IsResource)
				{
					string[] obj = new string[5] { text2, null, null, null, null };
					ResourceAndQty raq = item.raq;
					obj[1] = Loc.Get(raq.FindResource().locicon);
					obj[2] = " ";
					obj[3] = item.raq.qty.ToString();
					obj[4] = " ";
					text2 = string.Concat(obj);
				}
			}
			string text3 = TextUtil.ColorEnabledIf(flag, Loc.Get("ui.scheme.selections.formatting", "text", Loc.Get("ui.scheme.selections.cost", "cost", text2)));
			card.SetText("Cost", text3);
		}
		if (choice.grants == null)
		{
			return;
		}
		GrantContext ctx = new GrantContext(new VisitState(CrewMember.components.agent.FindCrewAssignment(), Game.ctx.clock.Now, PlayerID.HumanPlayer));
		string text4 = "";
		foreach (VisitGrant grant in choice.grants)
		{
			text4 = text4 + grant.Describe(ctx) + "\n";
		}
		card.SetText("Grants", TextUtil.ColorEnabledIf(flag, Loc.Get("ui.scheme.selections.formatting", "text", text4)));
	}

	public void RefreshTabs()
	{
		GameObject child = _go.GetChild("Templates/Stage Toggle");
		GameObject child2 = _go.GetChild("Panel/Chapters/Tabs");
		child2.EnsureChildCount(scheme.schemeChapterHistory, child);
		child2.InitializeChildren(scheme.schemeChapterHistory, RefreshChapterTab);
		GameObject gameObject = Object.Instantiate(child, child2.transform);
		ChapterDef chapterDef = Settings.FindChapterById(scheme.currentChapter.chapterID);
		gameObject.SetText("Text", Loc.Get("ui.scheme.chapter-tab", "num", scheme.schemeChapterHistory.Count + 1));
		gameObject.GetToggle().onValueChanged.SetListener(delegate(bool val)
		{
			SelectChapter(-1, val);
		});
		gameObject.GetOrAddComponent<TabCtx>().Set(chapterDef, scheme.schemeChapterHistory.Count);
	}

	public void RefreshChapterTab(int index, GameObject card, HistoryItem choice)
	{
		ChapterDef chapterDef = Settings.FindChapterById(choice.chapterId);
		card.SetText("Text", Loc.Get("ui.scheme.chapter-tab", "num", index + 1));
		card.GetToggle().onValueChanged.SetListener(delegate(bool val)
		{
			SelectChapter(index, val);
		});
		card.GetOrAddComponent<TabCtx>().Set(chapterDef, index);
	}

	public void OnSelectionClick(Decision choice)
	{
		if (choice.nextState.IsNotSet && choice.earlyEnd)
		{
			Game.serv.ui.AddPopup(new OkCancelPopup(Loc.Get("ui.scheme.cancel.confirm"), delegate
			{
				Game.ctx.players.Human.schemes.ProcessDecision(scheme.schemeID, choice);
				RefreshTabs();
				displayIndex = -1;
				RefreshDialog();
			}, delegate
			{
			}));
		}
		else
		{
			Game.ctx.players.Human.schemes.ProcessDecision(scheme.schemeID, choice);
			RefreshTabs();
			displayIndex = -1;
			RefreshDialog();
		}
	}

	public void OnTurnPass(SessionEvent sev)
	{
		if (scheme.DecisionPopThisTurn())
		{
			RefreshTabs();
			displayIndex = -1;
		}
		RefreshDialog();
	}

	public void RefreshProgress()
	{
		bool flag = scheme.IsWaitingForDecision();
		ChapterDef chapterDef = Settings.FindChapterById(scheme.currentChapter.chapterID);
		bool flag2 = scheme.epilogueText != null;
		_go.GetChild("Panel/Progress Info").SetActive(!flag && !flag2);
		if (!flag && !flag2)
		{
			ModQuery query = new ModQuery(PlayerID.HumanPlayer, scheme.crewAssigned, CrewMember.components.agent.GetNode().id);
			int deltadays = (scheme.currentChapter.finishTime - Game.ctx.clock.Now).deltadays;
			_go.SetText("Panel/Progress Info/Days Left", Loc.Get("ui.scheme.progress.days-left", "days", deltadays));
			float num = (float)chapterDef.timeCostDays.Evaluate(query);
			float num2 = MathUtil.ClampMin((num - (float)deltadays) / num, 0.05f);
			RectTransform obj = _go.GetChild("Panel/Progress Info/Progress Bar").transform as RectTransform;
			obj.sizeDelta = obj.sizeDelta.SetX(300f * num2);
		}
		_go.GetChild("End Scheme Button").SetActive(flag2 && displayIndex == -1);
	}

	public void OnEmergencyEndSchemeClick()
	{
		Game.serv.ui.AddPopup(new OkCancelPopup(Loc.Get("ui.scheme.cancel.confirm"), delegate
		{
			EmergencyEndScheme();
		}, delegate
		{
		}));
	}

	public void EmergencyEndScheme()
	{
		Game.ctx.players.Human.schemes.EndCurrentChapter(scheme.schemeID);
		Game.ctx.players.Human.schemes.EndScheme(scheme);
		Close();
	}

	public void EndScheme()
	{
		Game.ctx.players.Human.schemes.EndScheme(scheme);
		Close();
	}

	public void SelectChapter(int chapter, bool toggleOn)
	{
		if (toggleOn)
		{
			displayIndex = chapter;
			RefreshDialog();
		}
	}

	private void ForceRebuildLayoutImmediate(GameObject panel)
	{
		panel.ForceRebuildLayoutImmediate();
		panel.ForceRebuildLayoutImmediate();
		panel.ForceRebuildLayoutImmediate();
	}
}

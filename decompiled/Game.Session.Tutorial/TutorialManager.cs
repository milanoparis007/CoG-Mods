using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Filesystem;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Quests;
using Game.UI.Session.Popups;
using Game.UI.Session.Tutorial;
using SomaSim.SION;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public sealed class TutorialManager : AbstractSessionManager, IAnimatingManager, ISessionManager, ISaveLoadProvider
{
	public static readonly string QUEST_TUTORIAL_MONEY = "tutorial-money";

	public static readonly string QUEST_TUTORIAL_CROCKS = "tutorial-crocks";

	public static readonly string QUEST_TUTORIAL_TERRITORY = "tutorial-territory";

	public static readonly string QUEST_TUTORIAL_CREW = "tutorial-crew";

	public static readonly string QUEST_TUTORIAL_BUILDING = "tutorial-building";

	public static readonly string QUEST_NY_POLITICS_STARTER = "politics-starter";

	private Dictionary<SessionEventType, Action<SessionEvent>> _eventCallbacks;

	private TutorialManagerData _data = new TutorialManagerData();

	private LessonState _lessons = new LessonState();

	private List<GameObject> _highlights = new List<GameObject>();

	private bool _canShowTutorial;

	private bool _canShowHints;

	private bool _isManagerEnabled;

	private const string METGOON = "tut.event.goon";

	private const string METGANG = "tut.event.gang";

	private const string METCOP = "tut.event.cop";

	private const string METFED = "tut.event.fed";

	private const string CASINO_AVAILABLE = "tut.event.gambling";

	private const string TICKER_LEVELUP = "tut.event.levelup";

	private const string TICKER_VEHICLE_JUNK = "tut.event.vehicle-junk";

	private const string OUTPOST_CAN_EXPAND = "tut.event.outpost-can-expand";

	private const string OUTPOST_URGENT = "tut.event.outpost-urgent";

	private const string UI_HIGHLIGHT = "Board/UI Highlight";

	public TutorialContext Context => _data.ctx;

	private bool CanProcessEvents
	{
		get
		{
			if (_canShowHints && !ShowingFrancineDialog)
			{
				return Game.ctx.clock.CurrentTurn > 3;
			}
			return false;
		}
	}

	public bool ShowingLesson => _lessons.active != null;

	public bool ShowingTutorial
	{
		get
		{
			if (_lessons.active == null)
			{
				return _lessons.lessons.Count > 0;
			}
			return true;
		}
	}

	public bool ShowingFrancineDialog => Game.ctx.hud.francine.IsShowing;

	public bool IsPlayingTutorialCity => Game.ctx.scenario.newgamepars.playerdetails.tutorial;

	private bool InTutorialAndFinalLessonNotReached
	{
		get
		{
			if (IsPlayingTutorialCity)
			{
				return Game.serv.saveload.prefs.game.CanShowTutorial;
			}
			return false;
		}
	}

	public bool AreQuestReqsSuppressed
	{
		get
		{
			if (ShowingLesson)
			{
				return _lessons.active.InhibitsQuestRequests;
			}
			return false;
		}
	}

	public bool AreOutpostFailsSuppressed => IsPlayingTutorialCity;

	public bool AreGoonTrespassChecksSuppressed => ShowingTutorial;

	public bool AreInjuriesSuppressed => InTutorialAndFinalLessonNotReached;

	public bool AreConvoReqsSuppressed => InTutorialAndFinalLessonNotReached;

	public bool ArePoliceRaidsSuppressed => InTutorialAndFinalLessonNotReached;

	public bool IsThroneRoomSuppressed => InTutorialAndFinalLessonNotReached;

	public Resource TutorialResource => Resource.Find((Label)"home-brew");

	private Dictionary<SessionEventType, Action<SessionEvent>> MakeEventCallbacks()
	{
		return new Dictionary<SessionEventType, Action<SessionEvent>>(new SessionEventTypeComparer())
		{
			{
				SessionEventType.UIInsufficientActionPoints,
				delegate(SessionEvent ev)
				{
					TestAndShow(ev, "tut.event.actionpts");
				}
			},
			{
				SessionEventType.UIInsufficientMovementPoints,
				delegate(SessionEvent ev)
				{
					TestAndShow(ev, "tut.event.movementpts");
				}
			},
			{
				SessionEventType.HumanPlayerMetPlayer,
				delegate(SessionEvent ev)
				{
					TestAndDefer(ev, OnHumanMetPlayer);
				}
			},
			{
				SessionEventType.UITickerShown,
				delegate(SessionEvent ev)
				{
					TestAndDefer(ev, OnTickerShown);
				}
			},
			{
				SessionEventType.CrewMemberAttacked,
				delegate(SessionEvent ev)
				{
					TestAndDefer(ev, OnCrewAttacked);
				}
			},
			{
				SessionEventType.UIProductionComplete,
				delegate(SessionEvent ev)
				{
					TestAndShow(ev, "tut.event.prod.complete");
				}
			},
			{
				SessionEventType.UIProductionStalled,
				delegate(SessionEvent ev)
				{
					TestAndShow(ev, "tut.event.prod.stalled");
				}
			}
		};
	}

	public override void OnInitializeDone()
	{
		base.OnInitializeDone();
		GamePreferences game = Game.serv.saveload.prefs.game;
		_canShowHints = game.CanShowHints;
		_canShowTutorial = game.CanShowTutorial;
		_isManagerEnabled = _canShowHints || _canShowTutorial;
		if (!_isManagerEnabled)
		{
			return;
		}
		Game.ctx.events.AddListener(SessionEventType.QuestMarkedWaitingImmediate, OnQuestWaiting);
		Game.ctx.events.AddListener(SessionEventType.UIHUDDialogClosed, OnHUDDialogClosed);
		_eventCallbacks = MakeEventCallbacks();
		foreach (KeyValuePair<SessionEventType, Action<SessionEvent>> eventCallback in _eventCallbacks)
		{
			Game.ctx.events.AddListener(eventCallback.Key, eventCallback.Value);
		}
	}

	public override void OnReleased()
	{
		ClearHighlights();
		if (_isManagerEnabled)
		{
			foreach (KeyValuePair<SessionEventType, Action<SessionEvent>> eventCallback in _eventCallbacks)
			{
				Game.ctx.events.RemoveListener(eventCallback.Key, eventCallback.Value);
			}
			_eventCallbacks = null;
			Game.ctx.events.RemoveListener(SessionEventType.QuestMarkedWaitingImmediate, OnQuestWaiting);
			Game.ctx.events.RemoveListener(SessionEventType.UIHUDDialogClosed, OnHUDDialogClosed);
		}
		base.OnReleased();
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		if (_canShowTutorial)
		{
			TryActivateNextLesson();
		}
	}

	internal void OnHUDDialogClosed(SessionEvent sev)
	{
		ClearHighlights();
	}

	internal void SetContext(TutorialContext context)
	{
		_data.ctx = context;
	}

	internal void OnQuestWaiting(SessionEvent sev)
	{
		if (!(sev.ctx is QuestUUID uuid))
		{
			return;
		}
		QuestWaitingRecord questWaitingRecord = Game.ctx.quests.FindWaitingQuestUnsafe(uuid);
		if (questWaitingRecord?.questid == QUEST_TUTORIAL_TERRITORY && Game.ctx.quests.FindQuestDefinition(questWaitingRecord.questid)?.choices != null)
		{
			PersonData person = Context.Francine.data.person;
			Gender g = person.g;
			object[] replacements = new string[2] { "fr", person.FullName };
			string msg = Loc.GetGendered("tut.fr.missiondone", g, replacements);
			TimerUtil.RunAfterTime(delegate
			{
				OkPopup.ShowOk(msg, delegate
				{
				});
			}, 1f);
		}
	}

	private void OnHumanMetPlayer(SessionEvent ev)
	{
		PlayerInfo other;
		if (ev.ctx is PlayerID pid)
		{
			other = pid.FindPlayer();
			if (other.IsJustGoon && CanShowHint("tut.event.goon"))
			{
				Show("tut.event.goon");
			}
			if (other.IsJustGang && CanShowHint("tut.event.gang"))
			{
				Show("tut.event.gang");
			}
			if (other.IsJustCop && CanShowHint("tut.event.cop"))
			{
				Show("tut.event.cop");
			}
		}
		void Show(string key)
		{
			string text = other.social.FindPlayerGroupNameColorized();
			string[] replacements = new string[2] { "groupname", text };
			ShowHint(key, replacements);
		}
	}

	private void OnTickerShown(SessionEvent ev)
	{
		if (ev.ctx is TickerData tickerData)
		{
			if (tickerData.icon.key == TickerIcon.CASINO_AVAILABLE.key)
			{
				ShowHint("tut.event.gambling");
			}
			if (tickerData.icon.key == TickerIcon.LEVELUP.key)
			{
				ShowHint("tut.event.levelup");
			}
			if (tickerData.icon.key == TickerIcon.OUTPOST_CAN_EXPAND.key)
			{
				ShowHint("tut.event.outpost-can-expand");
			}
			if (tickerData.icon.key == TickerIcon.OUTPOST_URGENT.key)
			{
				ShowHint("tut.event.outpost-urgent");
			}
			if (tickerData.icon.key == TickerIcon.VEHICLE_JUNK.key)
			{
				ShowHint("tut.event.vehicle-junk");
			}
		}
	}

	private void OnCrewAttacked(SessionEvent ev)
	{
		if (ev.pid.IsHumanPlayer)
		{
			ShowHint("tut.event.crew-injured");
		}
	}

	public bool CanShowHint(string key)
	{
		return !Game.serv.saveload.progress.hints.ContainsKey(key);
	}

	public void DontShowHintAgain(string key)
	{
		Game.serv.saveload.progress.hints[key] = true;
		Game.serv.saveload.SaveProgress();
	}

	private void TestAndShow(SessionEvent ev, string key)
	{
		TestAndShow(ev, null, key);
	}

	private void TestAndShow(SessionEvent ev, Func<SessionEvent, bool> pred, string key, Action ondone = null)
	{
		if (CanProcessEvents && ev.pid.IsHumanPlayer && CanShowHint(key) && (pred == null || pred(ev)))
		{
			ShowHint(key);
			ondone?.Invoke();
		}
	}

	private void TestAndDefer(SessionEvent ev, Action<SessionEvent> fn)
	{
		if (CanProcessEvents && ev.pid.IsHumanPlayer)
		{
			fn(ev);
		}
	}

	private Sprite GetTutorialSpriteIfExists(string name)
	{
		if (name != null)
		{
			return Game.ctx.hud.uisprites.tutorial.Find(name);
		}
		return null;
	}

	internal void OnSessionStart()
	{
		if (_isManagerEnabled && Game.ctx.IsSessionFromNewGame)
		{
			Game.serv.ui.AddPopup(new NewGameIntroPopup(DoCameraSwoopAndShowFrancine));
		}
		else
		{
			DoCameraSwoopAndShowFrancine();
		}
	}

	private void DoCameraSwoopAndShowFrancine()
	{
		if (Game.serv.globals.settings.general.debug.skipSwoop)
		{
			MaybeStartTutorialSequence();
			return;
		}
		WorldPos humanPos = Game.ctx.players.Human.crew.GetCrewForIndex(0).GetVehicle()?.data.mobile.worldpos ?? Game.ctx.players.Human.territory.GetHeadquartersNode().pos;
		TimerUtil.RunAfterTime(delegate
		{
			Game.serv.camera.SetPosition(humanPos, CameraTween.AtStartup(3f));
			Game.serv.camera.SetRotation(-10f, CameraTween.AtStartup(3f));
			Game.serv.camera.SetPitch(20f, CameraTween.AtStartup(3f));
		}, 0.5f);
		TimerUtil.RunAfterTime(delegate
		{
			Game.serv.camera.SetZoom(50f, CameraTween.AtStartup(3f));
		}, 1.5f);
		TimerUtil.RunAfterTime(delegate
		{
			Game.ctx?.players?.AdvanceToNextCrew(zoom: false);
		}, 4f);
		TimerUtil.RunAfterTime(delegate
		{
			MaybeStartTutorialSequence();
		}, 4.5f);
	}

	internal void HideFrancineDialog()
	{
		Game.ctx.hud.francine.Hide();
	}

	internal void ShowLesson(BaseLesson lesson, int step, string message, string spritename, FrancineDialog.Anchor anchor, FrancineDialog.ButtonType type)
	{
		string text = Loc.FormatNumber(lesson.LessonNumber);
		string text2 = Loc.FormatNumber(step);
		string text3 = Loc.FormatNumber(lesson.StepsCount);
		string header = Loc.Get("tut.fr.header", "num", text, "title", lesson.GetLessonTitle(), "i", text2, "n", text3);
		ShowHelper(lesson.LocRoot, header, message, spritename, anchor, type, isHint: false);
	}

	private void ShowHint(string key, string[] replacements = null, string spritename = null, FrancineDialog.Anchor anchor = FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType type = FrancineDialog.ButtonType.Continue)
	{
		ShowHelper(key, Loc.Get("tut.event.title"), Loc.Get(key, replacements), spritename, anchor, type, isHint: true);
	}

	private void ShowHelper(string key, string header, string message, string spritename, FrancineDialog.Anchor anchor, FrancineDialog.ButtonType type, bool isHint)
	{
		if (_isManagerEnabled && CanShowHint(key))
		{
			Sprite tutorialSpriteIfExists = GetTutorialSpriteIfExists(spritename);
			FrancineDialog.ShowData data = new FrancineDialog.ShowData
			{
				key = key,
				header = header,
				message = message,
				sprite = tutorialSpriteIfExists,
				hint = isHint
			};
			Game.ctx.hud.francine.Show(data, anchor, type);
		}
	}

	public void TryStartManualLesson(int lesson)
	{
		if (_lessons.lastFinished < lesson)
		{
			BaseLesson baseLesson = (from l in LessonsCollection.GetManuallyStartedLessons()
				where l.LessonNumber == lesson
				select l).FirstOrDefault();
			if (baseLesson != null && _lessons?.lessons != null)
			{
				_lessons.lessons.Insert(0, baseLesson);
			}
		}
	}

	private void MaybeStartTutorialSequence()
	{
		if (Game.ctx?.tutorial != null && !Game.ctx.IsSessionFromSaveFile && _canShowTutorial && _isManagerEnabled && Game.ctx.scenario.newgamepars.playerdetails.tutorial)
		{
			_lessons.lessons.AddRange(LessonsCollection.GetLinearLessonSequence());
			Game.ctx.seasons.OverrideHour(10f);
		}
	}

	private void TryActivateNextLesson()
	{
		if (_lessons.CanActivate && !(_lessons.TimeSinceLastUpdate.TotalSeconds < 1.0))
		{
			_lessons.StartNextLesson();
		}
	}

	internal void SkipCurrentLesson()
	{
		_lessons.active.OnSkip();
		_lessons.StopActiveLesson();
	}

	public static string MakeMultiLineBlurb(string keyroot, string[] replacements, Entity subject)
	{
		Gender gender = subject.data.person.Gender;
		int num = 1;
		List<string> list = new List<string>();
		while (true)
		{
			string text = keyroot + num++;
			string key = text + Loc.RelSuffix(RelationshipType.None, gender);
			object obj;
			if (!Game.serv.loc.HasKey(key))
			{
				if (!Game.serv.loc.HasKey(text))
				{
					obj = null;
				}
				else
				{
					object[] replacements2 = replacements;
					obj = Loc.Get(text, replacements2);
				}
			}
			else
			{
				object[] replacements2 = replacements;
				obj = Loc.Get(key, replacements2);
			}
			string text2 = (string)obj;
			if (text2 == null)
			{
				break;
			}
			list.Add(text2);
		}
		return string.Join("\n\n", list);
	}

	public HighlightAnimation SetHighlight(string uniqueName, string path, string message = null, int? childIndex = null, int forceY = 0)
	{
		ClearHighlights();
		return AddHighlight(uniqueName, path, message, childIndex, forceY);
	}

	public HighlightAnimation SetHighlight(GameObject target, string message = null)
	{
		ClearHighlights();
		return AddHighlight(target, message);
	}

	public HighlightAnimation AddHighlight(string uniqueName, string path, string message = null, int? childIndex = null, int forceY = 0)
	{
		GameObject child = Game.serv.ui.GetSessionUICanvas().gameObject.GetChild(uniqueName);
		if (child == null)
		{
			return null;
		}
		GameObject gameObject = child.GetChild(path);
		if (gameObject == null)
		{
			return null;
		}
		if (childIndex.HasValue)
		{
			gameObject = gameObject.transform.GetChild(childIndex.Value).gameObject;
		}
		return AddHighlight(gameObject, message, forceY);
	}

	public HighlightAnimation AddHighlight(GameObject target, string message = null, int forceY = 0)
	{
		try
		{
			GameObject container = Game.serv.mouseovers.GetContainer();
			GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load("Board/UI Highlight") as GameObject, container.transform);
			gameObject.SetTextOrHide("Text", message);
			_highlights.Add(gameObject);
			HighlightAnimation orAddComponent = gameObject.GetOrAddComponent<HighlightAnimation>();
			orAddComponent.SetTarget(target);
			if (forceY > 0)
			{
				orAddComponent.startSize.y = forceY;
			}
			return orAddComponent;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public void ClearHighlights()
	{
		if (_highlights == null)
		{
			return;
		}
		foreach (GameObject highlight in _highlights)
		{
			UnityEngine.Object.Destroy(highlight);
		}
		_highlights.Clear();
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(_data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(TutorialManagerData result)
		{
			_data = result;
		});
		yield break;
	}
}

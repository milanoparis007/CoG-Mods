using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Platform;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Achievements;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Heatmaps;
using Game.Session.Overlays;
using Game.Session.Player;
using Game.Session.Quests;
using Game.Session.Setup;
using Game.Session.Sim;
using Game.Session.Tutorial;
using Game.UI;
using Game.UI.Session;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session;

public class SessionContext
{
	private struct StateProgress
	{
		public int CountDone;

		public int CountAll;

		public bool Ready;

		public StateProgress(bool ready)
		{
			Ready = ready;
			CountAll = 1;
			CountDone = (ready ? 1 : 0);
		}
	}

	public enum SavingStatus
	{
		None,
		Serializing,
		Writing,
		Finished
	}

	public SessionPrepResults session;

	public ScenarioConfig scenario;

	public SetupOrchestrator setup;

	public DebugConsoleManager console;

	public SessionEventBus events;

	public ScriptEventBus scriptevents;

	public GameClock clock;

	public ResourceManager resManager;

	public EntityManager entityman;

	public BoardManager board;

	public ModelManager models;

	public HeatmapManager heatmaps;

	public OverlayManager overlays;

	public GoalTracker goals;

	public QuestManager quests;

	public TutorialManager tutorial;

	public SimulationManager simman;

	public TransitManager transit;

	public AllPlayersManager players;

	public AchievementManager achievements;

	public BotlManager botl;

	public MapDisplayManager mapdisplay;

	public FogOfWarManager fogofwar;

	public SeasonManager seasons;

	public SelectionManager selection;

	public SFXManager sfx;

	public VFXManager vfx;

	public HUDManager hud;

	public KeyboardShortcutManager shortcuts;

	private List<ISessionManager> _all;

	private List<IGlobalTurnSetHandler> _turnAdvancedHandlers;

	private List<ISystemTurnHandler> _systemTurnHandlers;

	private List<IAnimatingManager> _allAnimating;

	private List<ICityGenManager> _allCityGen;

	private ISingletonBoardInitManager _setupOrchestrator;

	private IPlayerTurnHandler _playerTurnHandler;

	private ISingletonTurnSource _turnSource;

	private ISingletonAnimationSource _animSource;

	private SessionInitStatus _initStatus;

	private SessionState _state;

	private Stopwatch _stateStopwatch = new Stopwatch();

	public const int PLAYERS_PER_FRAME = 10;

	public SavingStatus Saving;

	public SessionState State => _state;

	public bool HasSaveFile => session.savefile != null;

	public bool IsInitializing => _state == SessionState.InitializeStarted;

	public bool IsBoardInit => _state == SessionState.BoardInitStarted;

	public bool IsCityGen => _state == SessionState.CityGenStarted;

	public bool IsCityGenDone => _state == SessionState.CityGenDone;

	public bool IsPreInteractiveInitDone => _state == SessionState.PreInteractiveDone;

	public bool IsPreInteractiveAIGenDone => _state == SessionState.PreInteractiveAIGenDone;

	public bool IsInteractive => _state == SessionState.Interactive;

	public bool IsPreReleaseDone => _state == SessionState.PreReleased;

	public bool IsReleaseDone => _state == SessionState.Released;

	public bool IsSessionFromSaveFile => HasSaveFile;

	public bool IsSessionFromNewGame => !HasSaveFile;

	public bool CanAutosaveGame
	{
		get
		{
			if (IsInteractive)
			{
				return !Game.ctx.tutorial.ShowingTutorial;
			}
			return false;
		}
	}

	public bool IsSaving => Saving != SavingStatus.None;

	public SessionContext()
	{
		_initStatus = new SessionInitStatus();
		_state = SessionState.None;
	}

	private void SetState(SessionState before, SessionState after)
	{
		SetState(before, null, after);
	}

	private void SetState(SessionState before, Action<ISessionManager> fn, SessionState after)
	{
		_stateStopwatch.Stop();
		_ = _stateStopwatch.ElapsedMilliseconds;
		_stateStopwatch.Restart();
		if (fn != null)
		{
			_all.ForEach(fn);
		}
		_state = after;
		Game.serv.events.SendImmediate(ServiceEventType.SessionContextStateChange);
	}

	public void Initialize(SessionPrepResults sessionData)
	{
		session = sessionData;
		scenario = sessionData.scenario;
		_initStatus.Set(SessionState.InitializeStarted, 0, 1);
		TypeUtils.MakeMemberInstances<ISessionManager>(this);
		_all = TypeUtils.GetMemberInstances<ISessionManager>(this).ToList();
		_allAnimating = _all.WhereTypeIs<IAnimatingManager>().ToList();
		_allCityGen = _all.WhereTypeIs<ICityGenManager>().ToList();
		_turnSource = _all.WhereTypeIs<ISingletonTurnSource>().Single();
		_animSource = _all.WhereTypeIs<ISingletonAnimationSource>().Single();
		_turnAdvancedHandlers = _all.WhereTypeIs<IGlobalTurnSetHandler>().ToList();
		_systemTurnHandlers = _all.WhereTypeIs<ISystemTurnHandler>().ToList();
		_playerTurnHandler = _all.WhereTypeIs<IPlayerTurnHandler>().Single();
		_setupOrchestrator = _all.WhereTypeIs<ISingletonBoardInitManager>().Single();
		SetState(SessionState.None, delegate(ISessionManager mgr)
		{
			mgr.OnInitializeStarted();
		}, SessionState.InitializeStarted);
	}

	public void Release()
	{
		SetState(SessionState.Interactive, delegate(ISessionManager mgr)
		{
			mgr.OnPreRelease();
		}, SessionState.PreReleased);
		_initStatus.Set(SessionState.PreReleased, 1, 1);
		_all.Reverse();
		SetState(SessionState.PreReleased, delegate(ISessionManager mgr)
		{
			mgr.OnReleased();
		}, SessionState.Released);
		SetState(SessionState.Released, delegate(ISessionManager mgr)
		{
			mgr.OnDestroyed();
		}, SessionState.Destroyed);
		_all.Clear();
		_all = null;
		_allAnimating = null;
		_allCityGen = null;
		_turnSource = null;
		_animSource = null;
		_turnAdvancedHandlers = null;
		_systemTurnHandlers = null;
		_playerTurnHandler = null;
		_setupOrchestrator = null;
		TypeUtils.RemoveMemberInstances<ISessionManager>(this);
		scenario = null;
		session = null;
	}

	private StateProgress FindProgress<T>(List<T> elements, Func<T, bool> filter)
	{
		int num = elements.Count();
		int num2 = elements.Count(filter);
		return new StateProgress
		{
			CountAll = num,
			CountDone = num2,
			Ready = (num == num2)
		};
	}

	public void MaybeTransitionState()
	{
		StateProgress stateProgress = default(StateProgress);
		switch (_state)
		{
		case SessionState.InitializeStarted:
			stateProgress = FindProgress(_all, (ISessionManager mgr) => mgr.IsInitializingDone);
			if (stateProgress.Ready)
			{
				SetState(SessionState.InitializeStarted, delegate(ISessionManager mgr)
				{
					mgr.OnInitializeDone();
				}, SessionState.InitializeDone);
			}
			break;
		case SessionState.InitializeDone:
			_setupOrchestrator.OnBoardInitStarted();
			SetState(SessionState.InitializeDone, SessionState.BoardInitStarted);
			break;
		case SessionState.BoardInitStarted:
			stateProgress = new StateProgress(_setupOrchestrator.IsBoardInitDone);
			if (stateProgress.Ready)
			{
				_setupOrchestrator.OnBoardInitDone();
				SetState(SessionState.BoardInitStarted, SessionState.BoardInitDone);
			}
			break;
		case SessionState.BoardInitDone:
			if (HasSaveFile)
			{
				SetState(SessionState.BoardInitDone, SessionState.CityGenDone);
				break;
			}
			_allCityGen.ForEach(delegate(ICityGenManager mgr)
			{
				mgr.OnCityGenStarted();
			});
			SetState(SessionState.BoardInitDone, SessionState.CityGenStarted);
			break;
		case SessionState.CityGenStarted:
			stateProgress = new StateProgress(_setupOrchestrator.IsCityGenDone);
			if (stateProgress.Ready)
			{
				_allCityGen.ForEach(delegate(ICityGenManager mgr)
				{
					mgr.OnCityGenDone();
				});
				SetState(SessionState.CityGenStarted, SessionState.CityGenDone);
			}
			break;
		case SessionState.CityGenDone:
			SetState(SessionState.CityGenDone, delegate(ISessionManager mgr)
			{
				mgr.OnPreInteractive();
			}, SessionState.PreInteractiveDone);
			break;
		case SessionState.PreInteractiveDone:
			SetState(SessionState.PreInteractiveDone, delegate(ISessionManager mgr)
			{
				mgr.OnPreInteractiveAIGen();
			}, SessionState.PreInteractiveAIGenDone);
			break;
		case SessionState.PreInteractiveAIGenDone:
			SetState(SessionState.PreInteractiveAIGenDone, delegate(ISessionManager mgr)
			{
				mgr.OnInteractive();
			}, SessionState.Interactive);
			break;
		}
		if (stateProgress.CountAll > 0)
		{
			_initStatus.Set(_state, stateProgress.CountDone, stateProgress.CountAll);
		}
	}

	internal void SetProcGenFailed()
	{
		_initStatus.procGenFailed = true;
	}

	internal bool IsProcGenFailed()
	{
		return _initStatus.procGenFailed;
	}

	public void Update()
	{
		MaybeTransitionState();
		if (!_initStatus.procGenFailed)
		{
			if (_state == SessionState.CityGenStarted)
			{
				FrameUpdateDuringCityGen();
			}
			if (_state == SessionState.Interactive)
			{
				FrameUpdateDuringInteractive();
				FrameUpdateAnimations();
			}
		}
	}

	private void FrameUpdateDuringCityGen()
	{
		_turnSource.AdvancePlayerOrSystem();
		_turnSource.AdvanceGlobalGameTurn();
		foreach (ICityGenManager item in _allCityGen)
		{
			try
			{
				item.OnCityGenTurn();
			}
			catch (Exception)
			{
			}
		}
	}

	private void FrameUpdateDuringInteractive()
	{
		if (IsPlayerTurnStillGoing())
		{
			return;
		}
		for (int i = 0; i < 10; i++)
		{
			AdvanceToNextPlayerOrSystem();
			if (IsPlayerTurnStillGoing())
			{
				break;
			}
		}
	}

	private bool IsPlayerTurnStillGoing()
	{
		if (clock.State.pid.IsAnyPlayer)
		{
			return !_playerTurnHandler.IsPlayerTurnDone();
		}
		return false;
	}

	private void AdvanceToNextPlayerOrSystem()
	{
		if (clock.State.pid.IsAnyPlayer)
		{
			bool isHumanPlayer = clock.State.pid.IsHumanPlayer;
			_playerTurnHandler.OnPlayerTurnEnded();
			if (isHumanPlayer)
			{
				Game.ctx.events.SendImmediate(SessionEventType.HumanPlayerTurnEnded);
			}
		}
		if (_turnSource.AdvancePlayerOrSystem())
		{
			_turnSource.AdvanceGlobalGameTurn();
			foreach (IGlobalTurnSetHandler turnAdvancedHandler in _turnAdvancedHandlers)
			{
				turnAdvancedHandler.OnGlobalTurnSetAdvanced();
			}
		}
		if (clock.State.pid.IsAnyPlayer)
		{
			_playerTurnHandler.OnPlayerTurnStarted();
		}
		else
		{
			foreach (ISystemTurnHandler systemTurnHandler in _systemTurnHandlers)
			{
				try
				{
					systemTurnHandler.OnSystemTurn();
				}
				catch (Exception)
				{
				}
			}
		}
		SessionEventType type = (clock.State.pid.IsHumanPlayer ? SessionEventType.HumanPlayerTurnStarted : (clock.State.pid.IsAnyPlayer ? SessionEventType.OtherPlayerTurnStarted : SessionEventType.SystemTurnStarted));
		Game.ctx.events.EnqueueOnce(type);
	}

	private void FrameUpdateAnimations()
	{
		float unityFrameDelta = Math.Max(0f, Time.deltaTime);
		GameAnimUpdate anim = _animSource.AdvanceGameAnim(unityFrameDelta);
		foreach (IAnimatingManager item in _allAnimating)
		{
			item.UpdateAnimations(anim);
		}
	}

	public IEnumerator SerializeGameState(Hashtable result)
	{
		events.Flush();
		foreach (SaveLoadUtils.MemberProvider<ISaveObserver> item in SaveLoadUtils.GetEachOfType<ISaveObserver>(this))
		{
			item.provider.OnBeforeSave();
		}
		yield return null;
		SaveLoadUtils.SaveMembersByNameInParallel(this, result);
		Game.ctx.scenario.savefileversion = GameSettings.version;
		result["scenario"] = Game.serv.serializer.instance.Serialize(scenario, specifyValueTypes: false);
		result["mapconfig"] = Game.serv.serializer.instance.Serialize(session.mapconfig, specifyValueTypes: false);
	}

	public static ScenarioConfig ParseScenario(Hashtable data)
	{
		return Game.serv.serializer.instance.Deserialize<ScenarioConfig>(data["scenario"]);
	}

	public static MapConfig ParseMapConfigOrNull(Hashtable data)
	{
		if (!data.ContainsKey("mapconfig"))
		{
			return null;
		}
		return Game.serv.serializer.instance.Deserialize<MapConfig>(data["mapconfig"]);
	}

	public IEnumerator LoadFromSaveFile(Hashtable data)
	{
		yield return Game.serv.sequencer.StartCoroutine(SaveLoadUtils.LoadMembersByNameCoroutine(this, data));
		foreach (SaveLoadUtils.MemberProvider<ILoadObserver> item in SaveLoadUtils.GetEachOfType<ILoadObserver>(this))
		{
			item.provider.OnAfterManagerLoad();
		}
		foreach (SaveLoadUtils.MemberProvider<IEntityLoadProvider> item2 in SaveLoadUtils.GetEachOfType<IEntityLoadProvider>(this))
		{
			yield return Game.serv.sequencer.StartCoroutine(item2.provider.DelayedEntityLoad());
		}
		foreach (SaveLoadUtils.MemberProvider<ILoadObserver> item3 in SaveLoadUtils.GetEachOfType<ILoadObserver>(this))
		{
			item3.provider.OnAfterEntityLoad();
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.OnGameBecomeInteractive);
	}

	public void AutosaveGame()
	{
		if (!CanAutosaveGame)
		{
			Logger.Warning("Autosave not available at this point");
			return;
		}
		SaveFileMetadata data = MakeMetadata(autosave: true);
		OnSaveSelected(data);
	}

	private void OnSaveSelected(SaveFileMetadata data)
	{
		Game.serv.sequencer.StartCoroutineTask(SaveGame(0f, data));
	}

	public void ShowSaveDialog()
	{
		Game.serv.ui.AddPopup(SaveLoadPopup.MakeForSaving(MakeMetadata(autosave: false), OnSaveSelected));
	}

	private SaveFileMetadata MakeMetadata(bool autosave)
	{
		PlayerInfo human = Game.ctx.players.Human;
		return new SaveFileMetadata(autosave, Game.ctx.session.mapconfig.CityName, human.social.PlayerFullName, human.social.PlayerGroupName, human.crew.LivingCrewCount, human.territory.OwnedNodeCount, human.finances.GetMoneyTotal().cash.IntFloor(), Game.ctx.scenario.rngseed, Game.ctx.clock.Now.ToDate().ToLocalTime(), DateTime.Now, human.crew.GetCrewForPlayerPeep().GetPeep().data.person.eth);
	}

	private void SetSavingStatus(SavingStatus newStatus)
	{
		Saving = newStatus;
		Game.ctx.events.SendImmediate(SessionEventType.OnSavingStatusChanged);
	}

	private IEnumerator SaveGame(float? waitseconds, SaveFileMetadata meta)
	{
		clock.PauseAnimations(this);
		SetSavingStatus(SavingStatus.Serializing);
		if (waitseconds.HasValue)
		{
			yield return new WaitForSeconds(waitseconds.Value);
		}
		else
		{
			yield return null;
		}
		Hashtable hashtable = new Hashtable();
		using (new BlockStopwatch("SERIALIZING"))
		{
			yield return Game.serv.sequencer.StartCoroutine(SerializeGameState(hashtable));
		}
		SetSavingStatus(SavingStatus.Writing);
		using (new BlockStopwatch("SAVING FILE"))
		{
			yield return Game.serv.saveload.SaveGame(meta, new SaveFileContents(hashtable));
		}
		SetSavingStatus(SavingStatus.Finished);
		SetSavingStatus(SavingStatus.None);
		clock.UnpauseAnimations(this);
	}

	public void QuitGame()
	{
		if (IsSaving)
		{
			Logger.Error("Tried to quit game in the middle of saving");
			return;
		}
		while (Game.serv.screens.Peek().GetType() != typeof(GSMainMenu))
		{
			Game.serv.screens.Pop();
		}
	}

	public void RequestQuit()
	{
		if (!IsSaving)
		{
			Game.serv.ui.AddPopup(new SaveQuitPopup());
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class GameClock : AbstractSessionManager, ISingletonTurnSource, ISessionManager, ISingletonAnimationSource, ISaveLoadProvider
{
	private GameClockPersistedData _data = new GameClockPersistedData();

	private List<object> _animPausers;

	public Listeners<bool> OnAnimPaused;

	public static readonly object UI_PAUSE_SENTINEL = new object();

	public float AnimSpeedMultiplier { get; private set; }

	public GameAnimUpdate LastGameAnimUpdate => _data.anim;

	public GameTurnUpdate State => _data.state;

	public GameAnimUpdate AnimState => _data.anim;

	public PlayerID CurrentPlayer => _data.state.pid;

	public int CurrentTurn => _data.state.turn;

	public SimTime Now => _data.state.now;

	public SimTime Previous => _data.state.previous;

	public bool SkipEnd => _data.skip;

	public bool AreAnimationsPaused => _animPausers.Count > 0;

	public bool AreAnimationsPausedByUI => AreAnimationsPausedBy(UI_PAUSE_SENTINEL);

	private GeneratorSettings Settings => Game.serv.globals.settings.general.generator;

	public SimTime FirstDayOfProcGen => new SimTime(Settings.procGenYears.from, 0);

	public SimTime LastDayOfProcGen => new SimTime(Settings.procGenYears.to, Settings.startDayOfYear);

	private int DaysPerTurnInteractive => Settings.daysPerTurnInteractive;

	private int DaysPerTurnCityGenMax => Settings.daysPerTurnCityGenMax;

	public int DaysPerTurn
	{
		get
		{
			if (!Game.ctx.IsInteractive)
			{
				if (!Game.ctx.IsCityGen)
				{
					return 0;
				}
				return GetDaysLeftForCityGenTurn();
			}
			return DaysPerTurnInteractive;
		}
	}

	public void AllowPostgame()
	{
		_data.skip = true;
	}

	public void DisallowPostgame()
	{
		_data.skip = false;
	}

	public Fixnum DaysToTurns(Fixnum days)
	{
		if (DaysPerTurn != 0)
		{
			return days / DaysPerTurn;
		}
		return 0;
	}

	public int DaysToTurnsRoundedUp(Fixnum days)
	{
		return DaysToTurns(days).IntCeiling();
	}

	public int TurnsToDays(int turns)
	{
		return DaysPerTurn * turns;
	}

	public SimTime TurnsToSimTime(int turns)
	{
		return _data.state.turnOneDate.IncrementDays(TurnsToDays(turns));
	}

	public bool IsMonthOneOf(List<int> months)
	{
		int month = Now.ToDate().Month;
		return months.ContainsFast(month);
	}

	public SimTime GetFirstTurnOfMonthThisYear(int month)
	{
		SimTime result = _data.state.firstTurnThisYearDate;
		while (result.ToDate().Month != month)
		{
			result = result.IncrementTurns(1);
		}
		if (result.YearsInt != Game.ctx.clock.Now.YearsInt)
		{
			result = result.IncrementYears(Game.ctx.clock.Now.YearsInt - result.YearsInt);
		}
		return result;
	}

	public override void OnInitializeDone()
	{
		_data.state = new GameTurnUpdate
		{
			now = FirstDayOfProcGen,
			previous = FirstDayOfProcGen,
			turn = 0
		};
		_data.anim = new GameAnimUpdate();
		_data.skip = false;
		AnimSpeedMultiplier = 1f;
		AbstractSessionManager.InitializeSubmanagers(this);
		OnAnimPaused = new Listeners<bool>();
		_animPausers = new List<object>();
	}

	public override void OnPreInteractiveAIGen()
	{
		base.OnPreInteractiveAIGen();
		if (Game.ctx.IsSessionFromNewGame)
		{
			_data.state.turn = 1;
			_data.state.turnOneDate = Game.ctx.clock.Now;
			_data.state.firstTurnThisYearDate = Game.ctx.clock.Now;
		}
		Game.ctx.seasons.Update();
	}

	public override void OnReleased()
	{
		OnAnimPaused.Clear();
		_animPausers.Clear();
		AbstractSessionManager.ReleaseSubmanagers(this);
	}

	public GameAnimUpdate AdvanceGameAnim(float unityFrameDelta)
	{
		if (AreAnimationsPaused)
		{
			_data.anim.frameDeltaSeconds = 0f;
		}
		else
		{
			float num = unityFrameDelta * AnimSpeedMultiplier;
			_data.anim.frameDeltaSeconds = num;
			_data.anim.cumulativeSeconds += num;
		}
		return _data.anim;
	}

	public bool AdvancePlayerOrSystem()
	{
		PlayerID pid = (Game.ctx.IsInteractive ? FindNext(_data.state.pid) : PlayerID.System);
		_data.state.pid = pid;
		return pid.IsSystem;
	}

	public PlayerID FindNext(PlayerID current)
	{
		short num = (short)(current.id + 1);
		int totalPlayers = Game.ctx.players.data.totalPlayers;
		if (num < totalPlayers)
		{
			return new PlayerID(num);
		}
		return PlayerID.System;
	}

	public void AdvanceGlobalGameTurn()
	{
		GameTurnUpdate state = _data.state;
		int num = state.daysPerTurn;
		if (Game.ctx.IsCityGen)
		{
			num = GetDaysLeftForCityGenTurn();
		}
		else if (Game.ctx.IsInteractive)
		{
			num = DaysPerTurnInteractive;
		}
		else
		{
			Logger.Error("AdvanceGameClock called in unexpected context, neither city gen nor interactive");
		}
		state.turn++;
		state.previous = state.now;
		state.daysPerTurn = num;
		state.now = state.now.IncrementDays(num);
		if (state.previous.ToDate().Year != state.now.ToDate().Year)
		{
			state.firstTurnThisYearDate = state.now;
		}
		if (Game.ctx.IsInteractive)
		{
			Game.serv.stats.LogEvent("game_turn", state.turn);
		}
		Game.ctx.seasons.Update();
	}

	internal bool CheatForceTurn(int turns)
	{
		GameTurnUpdate state = _data.state;
		if (turns < state.turn)
		{
			return false;
		}
		state.turn = turns;
		state.now = state.turnOneDate.IncrementDays(TurnsToDays(turns));
		state.previous = state.now.IncrementDays(-state.daysPerTurn);
		return true;
	}

	public bool AreAnimationsPausedBy(object pauser)
	{
		return _animPausers.Contains(pauser);
	}

	public void PauseAnimations(object pauser)
	{
		bool areAnimationsPaused = AreAnimationsPaused;
		if (!_animPausers.Contains(pauser))
		{
			_animPausers.Add(pauser);
		}
		if (!areAnimationsPaused && AreAnimationsPaused)
		{
			OnAnimPaused.Invoke(arg: true);
		}
	}

	public bool UnpauseAnimations(object pauser)
	{
		bool areAnimationsPaused = AreAnimationsPaused;
		bool result = _animPausers.Remove(pauser);
		if (areAnimationsPaused && !AreAnimationsPaused)
		{
			OnAnimPaused.Invoke(arg: false);
		}
		return result;
	}

	public int GetDaysLeftForCityGenTurn()
	{
		return MathUtil.ClampMax(LastDayOfProcGen.days - Now.days, DaysPerTurnCityGenMax);
	}

	public SimTimeSpan GetTimeSinceProcGen()
	{
		return new SimTimeSpan(Now.days - LastDayOfProcGen.days);
	}

	public void ToggleSpeedPaused()
	{
		ToggleSpeedPaused(resetSpeed: false);
	}

	public void ToggleSpeedPaused(bool resetSpeed)
	{
		if (resetSpeed)
		{
			AnimSpeedMultiplier = 1f;
		}
		if (AreAnimationsPausedBy(UI_PAUSE_SENTINEL))
		{
			UnpauseAnimations(UI_PAUSE_SENTINEL);
		}
		else
		{
			PauseAnimations(UI_PAUSE_SENTINEL);
		}
	}

	public void SetSpeedNormal()
	{
		AnimSpeedMultiplier = 1f;
		UnpauseAnimations(UI_PAUSE_SENTINEL);
	}

	public void SetSpeedFast()
	{
		AnimSpeedMultiplier = 10f;
		UnpauseAnimations(UI_PAUSE_SENTINEL);
	}

	public void SetSpeedDebug()
	{
		AnimSpeedMultiplier = 80f;
		UnpauseAnimations(UI_PAUSE_SENTINEL);
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(_data));
	}

	public IEnumerator Load(Hashtable rawdata)
	{
		SaveLoadUtils.DeserializeSingleKey(rawdata, "data", delegate(GameClockPersistedData result)
		{
			_data = result;
		});
		yield break;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Session.Crew;
using Game.UI.Session.Popups;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class AgentComponent : BaseComponent
{
	public readonly int NICKNAME_LEVEL = 3;

	public float CurrentHealthAsFraction => (float)Fixnum.Clamp(CurrentHealth / MaxHealth(), 0, 1);

	public bool HasHealthPointsLeft => _entity.data.agent.health > 0;

	public Fixnum CurrentHealth => _entity.data.agent.health;

	public bool IsWounded => CurrentHealth < MaxHealth();

	public NodeID NodeID => _entity.data.agent.nid;

	public int ActionsRemaining => _entity.data.agent.actionsLeft;

	public bool HasActionsRemaining => _entity.data.agent.actionsLeft > 0;

	public int MovesRemaining => _entity.data.agent.movesLeft;

	public bool HasMovesRemaining => _entity.data.agent.movesLeft > 0;

	public bool HasActionsOrMovesRemaining
	{
		get
		{
			if (!HasActionsRemaining)
			{
				return HasMovesRemaining;
			}
			return true;
		}
	}

	public bool HasActionsAndMovesRemaining
	{
		get
		{
			if (HasActionsRemaining)
			{
				return HasMovesRemaining;
			}
			return false;
		}
	}

	public bool HasAnyLevelups
	{
		get
		{
			XP xp = _entity.data.agent.xp;
			if (xp == null)
			{
				return false;
			}
			return xp.levelups.Count > 0;
		}
	}

	public override void OnAfterEntityCreated(bool loaded)
	{
		base.OnAfterEntityCreated(loaded);
		if (!loaded)
		{
			Refill();
			ModValue maxCrewHealth = Game.serv.globals.settings.people.combatSettings.maxCrewHealth;
			_entity.data.agent.health = maxCrewHealth.Evaluate(new ModQuery(PlayerID.INVALID));
		}
	}

	public void OnDeath()
	{
		AgentData agent = _entity.data.agent;
		agent.actionsLeft = 0;
		agent.movesLeft = 0;
		agent.nid = NodeID.INVALID;
		agent.health = 0;
	}

	public void SetPlayer(PlayerID pid)
	{
		_entity.data.agent.pid = pid;
	}

	public void SetIntroducer(EntityID introId)
	{
		_entity.data.agent.introducer = introId;
	}

	private Fixnum MaxHealth()
	{
		return Game.serv.globals.settings.people.combatSettings.maxCrewHealth.Evaluate(_entity.data.agent.pid);
	}

	public Fixnum IncrementHealth(Fixnum delta)
	{
		return SetHealth(_entity.data.agent.health + delta);
	}

	public Fixnum SetHealth(Fixnum points)
	{
		_entity.data.agent.health = Fixnum.Clamp(points, 0, MaxHealth());
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewHealthChanged, _entity.Id, _entity.data.agent.pid));
		return points;
	}

	public CombatSettings.HealthInfo FindHealthInfo()
	{
		return Game.serv.globals.settings.people.combatSettings.FindCrewHealthInfo(CurrentHealth);
	}

	public bool IsInjured()
	{
		return !FindHealthInfo().canwork;
	}

	public void SetNodeID(NodeID nodeId)
	{
		_entity.data.agent.nid = nodeId;
	}

	public Node GetNode()
	{
		return _entity.data.agent.nid.FindNode();
	}

	public bool CanConsumeActions(int actions)
	{
		return _entity.data.agent.actionsLeft >= actions;
	}

	public bool CanConsumeMoves(int moves)
	{
		return _entity.data.agent.movesLeft >= moves;
	}

	private bool ConsumeActions(int actions)
	{
		if (CanConsumeActions(actions))
		{
			_entity.data.agent.actionsLeft -= actions;
			return true;
		}
		Logger.Warning($"Trying to consume {actions} moves, only {ActionsRemaining} left");
		_entity.data.agent.actionsLeft = 0;
		return false;
	}

	private bool ConsumeMoves(int moves)
	{
		if (CanConsumeMoves(moves))
		{
			_entity.data.agent.movesLeft -= moves;
			return true;
		}
		Logger.Warning($"Trying to consume {moves} moves, only {MovesRemaining} left");
		_entity.data.agent.movesLeft = 0;
		return false;
	}

	public bool CanPay(CrewCost cost)
	{
		if (CanConsumeActions(cost.actions))
		{
			return CanConsumeMoves(cost.moves);
		}
		return false;
	}

	public bool DoPay(CrewCost cost, string debug = null)
	{
		bool num = ConsumeActions(cost.actions);
		bool flag = ConsumeMoves(cost.moves);
		if (_entity.data.agent.pid == PlayerID.HumanPlayer)
		{
			SessionEvent ev = new SessionEvent(SessionEventType.CrewActionsChanged, _entity.Id, PlayerID.HumanPlayer);
			Game.ctx.events.EnqueueOnce(ev);
		}
		return num && flag;
	}

	public void ConsumeAllPoints(bool actions = true, bool moves = true)
	{
		if (actions)
		{
			_entity.data.agent.actionsLeft = 0;
		}
		if (moves)
		{
			_entity.data.agent.movesLeft = 0;
		}
	}

	public void ConsumeAllPointsAndStop()
	{
		ConsumeAllPoints();
		_entity.data.agent.pid.FindPlayer().commands.FlushQueue(_entity.Id, cancelActive: true);
	}

	internal void CheatSetActionsAndMoves(int actions, int moves)
	{
		_entity.data.agent.actionsLeft = actions;
		_entity.data.agent.movesLeft = moves;
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewActionsChanged, _entity.Id, PlayerID.HumanPlayer));
	}

	internal void CheatAddActionsAndMoves(int actions, int moves)
	{
		_entity.data.agent.actionsLeft += actions;
		_entity.data.agent.movesLeft += moves;
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewActionsChanged, _entity.Id, PlayerID.HumanPlayer));
	}

	public (int moves, int actions) GetMovesAndActionsPerTurn()
	{
		AgentConfig agent = _entity.config.agent;
		ModQuery query = MakeModQueryForPointsEvaluation();
		return new ValueTuple<int, int>(item2: (int)agent.actionsPerTurn.Evaluate(query), item1: (int)agent.movesPerTurn.Evaluate(query));
	}

	public (string moves, string actions) ExplainMovesAndActionsPerTurn(bool addHeader, bool includeZeros)
	{
		AgentConfig agent = _entity.config.agent;
		ModQuery query = MakeModQueryForPointsEvaluation();
		return new ValueTuple<string, string>(item2: agent.actionsPerTurn.Explain(query, addHeader, includeZeros), item1: agent.movesPerTurn.Explain(query, addHeader, includeZeros));
	}

	internal void Refill()
	{
		(int moves, int actions) movesAndActionsPerTurn = GetMovesAndActionsPerTurn();
		int item = movesAndActionsPerTurn.moves;
		int item2 = movesAndActionsPerTurn.actions;
		AgentData agent = _entity.data.agent;
		agent.actionsLeft = item2;
		agent.movesLeft = item;
	}

	internal PlayerInfo GetPlayer()
	{
		return _entity.data.agent.pid.FindPlayer();
	}

	internal CrewAssignment FindCrewAssignment()
	{
		return GetPlayer().crew.GetCrewForPeep(_entity.Id);
	}

	internal Entity FindCrewBossPeep()
	{
		return GetPlayer()?.social.GetPlayerPeep();
	}

	public (bool pass, PlayerID pid) IsBoss()
	{
		PlayerID pid = _entity.data.agent.pid;
		if (pid.IsNotAnyPlayer)
		{
			return (pass: false, pid: pid);
		}
		return (pass: pid.FindPlayer().social.PlayerPeepId == _entity.Id, pid: pid);
	}

	private ModQuery MakeModQueryForPointsEvaluation()
	{
		AgentData agent = _entity.data.agent;
		return new ModQuery(agent.pid, _entity.Id, _entity.Id, agent.nid);
	}

	public (int value, string explanation) FindXPFor(XPSource source, bool explain = false)
	{
		ModQuery query = MakeModQueryForPointsEvaluation();
		ModValue points = Game.serv.globals.settings.skills.experience.GetPoints(source);
		int item = (int)(points?.Evaluate(query) ?? ((Fixnum)0));
		string item2 = ((!explain) ? null : points?.Explain(query, addHeader: false));
		return (value: item, explanation: item2);
	}

	public int GetXP()
	{
		return _entity.data.agent.xp?.current ?? 0;
	}

	public void AddXP(XPSource source)
	{
		SetXP(GetXP() + FindXPFor(source).value, source);
	}

	public void AddXP(int delta)
	{
		SetXP(GetXP() + delta);
	}

	public void SetXP(int value, XPSource? source = null)
	{
		AgentData agent = _entity.data.agent;
		agent.xp = agent.xp ?? new XP();
		agent.xp.current = value;
	}

	public int GetLevel(Label id)
	{
		return _entity.data.agent.xp?.GetLevelupLevel(id) ?? 0;
	}

	public List<XP.Levelup> GetLevelupsSortedOrNull()
	{
		XP xp = _entity.data.agent.xp;
		if (xp?.levelups == null || xp.levelups.Count == 0)
		{
			return null;
		}
		List<LevelupChain> levelups = Game.serv.globals.settings.skills.experience.levelups;
		List<XP.Levelup> list = new List<XP.Levelup>(xp.levelups.Count);
		using ListPool<Label>.PooledBlockList pooledBlockList = ListPool<Label>.Allocate();
		foreach (XP.Levelup levelup in xp.levelups)
		{
			pooledBlockList.Add(levelup.id);
		}
		foreach (LevelupChain item in levelups)
		{
			int num = pooledBlockList.IndexOf(item.id);
			if (num >= 0)
			{
				list.Add(xp.levelups[num]);
			}
		}
		return list;
	}

	private int GetLastLevelupThreshold()
	{
		return _entity.data.agent.xp?.lastThreshold ?? 0;
	}

	private bool HasXPToGainLevelup()
	{
		int xP = GetXP();
		if (xP == 0)
		{
			return false;
		}
		return xP >= FindXPForNextLevelup().nextXP;
	}

	public (int nextXP, string explanation) FindXPForNextLevelup(bool explain = false)
	{
		ExperienceSettings.Tuning tuning = Game.serv.globals.settings.skills.experience.tuning;
		ModQuery query = MakeModQueryForPointsEvaluation();
		float num = (float)tuning.thresholdMultiplier.Evaluate(query);
		float num2 = (float)tuning.firstThreshold.Evaluate(query);
		int num3 = GetLastLevelupThreshold() + 1;
		int item = ((!(num > 1f)) ? ((int)(num2 * (float)num3)) : ((int)((double)num2 * (1.0 - Math.Pow(num, num3)) / (double)(1f - num))));
		string item2 = (explain ? tuning.thresholdMultiplier.Explain(query, addHeader: false) : null);
		return (nextXP: item, explanation: item2);
	}

	public bool CanShowLevelupPopup()
	{
		if (HasXPToGainLevelup())
		{
			return AreAnyLevelupsAvailable();
		}
		return false;
	}

	public void ShowLevelupPopup()
	{
		if (CanShowLevelupPopup())
		{
			List<LevelupDescription> rows = GetAvailableLevelups(explain: true).ToList();
			Game.serv.ui.AddPopup(new LevelupPopup(_entity, rows, GrantLevelup));
		}
	}

	private void GrantLevelup(LevelupDescription desc)
	{
		XP xp = _entity.data.agent.xp;
		xp.GetLevelupLevel(desc.levelup.id);
		xp.lastThreshold++;
		xp.SetLevelupLevel(desc.levelup.id, desc.nextLevel);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewLevelUpsChanged, _entity.Id, _entity.data.agent.pid));
		Game.ctx.selection.ClearActive();
		Game.ctx.simman.hints.ShowHintPhotoRandom(SFXType.EventSkillLearned, desc.levelup.hint);
		if (desc.levelup.id == GetCaptainLevelupId())
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.PROMOTE, TickerTitle.PROMOTE, Loc.Get("ui.tickers.captain-promote", "name", _entity.data.person.FullName), _entity.Id);
		}
	}

	private Label GetCaptainLevelupId()
	{
		return Game.serv.globals.settings.skills.experience.GetCaptainLevelup().id;
	}

	public bool IsCaptain()
	{
		return GetLevel(GetCaptainLevelupId()) > 0;
	}

	public string WrapWithRankIcon(string name)
	{
		if (IsBoss().pass)
		{
			return name + " " + Loc.Get("ui.crew-boss");
		}
		if (IsCaptain())
		{
			return name + " " + Loc.Get("ui.crew-captain");
		}
		return name;
	}

	public bool AreAnyLevelupsAvailable()
	{
		return GetAvailableLevelups(explain: false).Any();
	}

	public IEnumerable<LevelupDescription> GetAvailableLevelups(bool explain)
	{
		AgentData data = _entity.data.agent;
		PlayerInfo player = data.pid.FindPlayer();
		CrewAssignment crewForPeep = player.crew.GetCrewForPeep(_entity.Id);
		VisitState visit = MakeVisitStateForLevelup(crewForPeep);
		return Game.serv.globals.settings.skills.experience.levelups.Select(MakeDescription).Where(NotMaxedOutYet).Where(PassesReqs);
		LevelupDescription MakeDescription(LevelupChain levelup)
		{
			int levelupLevel = data.xp.GetLevelupLevel(levelup.id);
			int num = levelupLevel + 1;
			LevelupDescription levelupDescription = new LevelupDescription
			{
				levelup = levelup,
				icon = levelup.GetIcon(),
				currentLevel = levelupLevel,
				nextLevel = num
			};
			if (explain)
			{
				levelupDescription.message = Loc.Get("ui.personinfo.levelups.available", "name", levelup.GetName(), "num", Loc.FormatNumber(num), "desc", levelup.GetDesc());
			}
			return levelupDescription;
		}
		VisitState MakeVisitStateForLevelup(CrewAssignment _crew)
		{
			_crew.GetVehicle();
			Entity building = _crew.GetBuilding();
			BuildingAndBusinessData bbdata = ((building != null) ? BuildingUtil.FindDataForBuilding(building) : default(BuildingAndBusinessData));
			return new VisitState(_crew, bbdata, Game.ctx.clock.Now, player.PID);
		}
		static bool NotMaxedOutYet(LevelupDescription desc)
		{
			return desc.nextLevel <= desc.levelup.highest;
		}
		bool PassesReqs(LevelupDescription desc)
		{
			return desc.levelup.visreqs?.AllPass(visit) ?? true;
		}
	}

	public void HandleLevelups()
	{
		if (CanShowLevelupPopup())
		{
			ShowLevelupTicker();
		}
		if (CanGainNickname())
		{
			ShowLevelupNickname();
		}
	}

	public void ShowLevelupTicker()
	{
		if (CanShowLevelupPopup() && AreAnyLevelupsAvailable())
		{
			string fullName = _entity.data.person.FullName;
			string message = Loc.Get("ui.personinfo.levelups.ticker", "name", fullName);
			Game.ctx.hud.tickers.AddLevelupTicker(message, _entity.Id);
		}
	}

	public bool CanGainNickname()
	{
		AgentData agent = _entity.data.agent;
		if (agent.nickname == AgentData.NickStatus.None && (agent.xp?.GetSumOfLevels() ?? 0) >= NICKNAME_LEVEL && Game.ctx.clock.Now >= Game.ctx.clock.LastDayOfProcGen.IncrementDays(62))
		{
			agent.nickname = AgentData.NickStatus.Eligible;
		}
		return agent.nickname == AgentData.NickStatus.Eligible;
	}

	public void ShowLevelupNickname()
	{
		if (!CanGainNickname())
		{
			return;
		}
		Game.serv.ui.AddPopup(new RenamePopup(_entity.Id, delegate(string nick)
		{
			_entity.components.person.SetNickname(nick);
			_entity.data.agent.nickname = AgentData.NickStatus.Selected;
			PlayerSocial social = Game.ctx.players.Human.social;
			if (social.IsBoss(_entity))
			{
				social.SetBossInfo(_entity, rerollGroupName: false);
			}
			SessionEvent ev = new SessionEvent(SessionEventType.CrewActionsChanged, _entity.Id, PlayerID.HumanPlayer);
			Game.ctx.events.EnqueueOnce(ev);
		}, delegate
		{
			_entity.data.agent.nickname = AgentData.NickStatus.Selected;
		}));
	}

	public void IncrementStat(CrewStats key, int delta)
	{
		_entity.data.agent.crewHistoryStats.Increment(key, delta, skipMissingKey: true);
	}

	public void DecrementStat(CrewStats key, int delta)
	{
		_entity.data.agent.crewHistoryStats.Decrement(key, delta, skipMissingKey: true);
	}

	public Dictionary<CrewStats, int> GetMostRelevantCrewStats()
	{
		PlayerID pid = _entity.data.agent.pid;
		List<CrewAssignment> rawcrew = Game.ctx.players.GetPlayerData(pid).crew.rawcrew;
		int count = rawcrew.Count;
		Dictionary<CrewStats, int> statAverages = Enum.GetValues(typeof(CrewStats)).Cast<CrewStats>().ToDictionary((CrewStats key) => key, (CrewStats key) => 0);
		foreach (CrewAssignment item in rawcrew)
		{
			Dictionary<CrewStats, int> crewHistoryStats = item.GetPeep().data.agent.crewHistoryStats;
			statAverages = (from x in statAverages.Concat(crewHistoryStats)
				group x by x.Key).ToDictionary((IGrouping<CrewStats, KeyValuePair<CrewStats, int>> x) => x.Key, (IGrouping<CrewStats, KeyValuePair<CrewStats, int>> x) => x.Sum((KeyValuePair<CrewStats, int> y) => y.Value));
		}
		foreach (CrewStats item2 in statAverages.Keys.ToList())
		{
			statAverages[item2] /= count;
		}
		return _entity.data.agent.crewHistoryStats.OrderByDescending((KeyValuePair<CrewStats, int> x) => x.Value / (statAverages[x.Key] + 1)).Take(3).ToDictionary((KeyValuePair<CrewStats, int> x) => x.Key, (KeyValuePair<CrewStats, int> x) => x.Value);
	}

	public void RememberArrestAtThisTime()
	{
		_entity.data.agent.lastArrestTime = Game.ctx.clock.Now;
	}

	public void RememberInjuryAtThisTime()
	{
		_entity.data.agent.lastInjuryTime = Game.ctx.clock.Now;
	}

	public void RememberCrewSalary(Fixnum expected, Fixnum paid)
	{
		AgentData agent = _entity.data.agent;
		agent.expectedSalary = expected;
		agent.lastPaidSalary = paid;
	}

	public bool WasCrewUnpaidLastTurn()
	{
		AgentData agent = _entity.data.agent;
		if (agent.expectedSalary.IsNotZero)
		{
			return agent.lastPaidSalary != agent.expectedSalary;
		}
		return false;
	}

	public int? DaysSinceLastInjury()
	{
		return DaysSinceActionOrNull(_entity.data.agent.lastInjuryTime);
	}

	public int? DaysSinceLastArrest()
	{
		return DaysSinceActionOrNull(_entity.data.agent.lastArrestTime);
	}

	public int? DaysSinceActionOrNull(SimTime actionTime)
	{
		SimTime now = Game.ctx.clock.Now;
		if (actionTime.days == 0)
		{
			return null;
		}
		if (actionTime.days > now.days)
		{
			return null;
		}
		return now.Subtract(actionTime).deltadays;
	}

	public bool LastInjuryHappenedThisTurn()
	{
		int? num = DaysSinceLastInjury();
		if (num.HasValue)
		{
			return num.Value == 0;
		}
		return false;
	}
}

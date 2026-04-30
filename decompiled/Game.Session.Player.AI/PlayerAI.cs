using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class PlayerAI : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	public UnitsAdvisor units;

	public SocialAdvisor social;

	public SafehouseAdvisor safehouse;

	public TerritoryAdvisor territory;

	public BusinessAdvisor business;

	public PrecinctAdvisor precinct;

	public FedsAdvisor feds;

	public GoonAdvisor goon;

	public AttackAdvisor attack;

	public CombatAdvisor combat;

	private List<AdvisorRequest> _requests = new List<AdvisorRequest>();

	private List<AIAdvisor> _advisors = new List<AIAdvisor>();

	public PlayerAIData Data => _data.ai;

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "grant-free-skills", CheatGrantFreeSkills));
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "propose-meeting-tiedhouse", CheatProposeMeetingTruce));
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "send-feds-at-crew", CheatSendFeds));
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "arrest-somebody", CheatArrestSomebody));
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "errare-humanum-est", CheatError));
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "log-events", CheatLogEvents));
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "log-decisions", CheatLogDecisions));
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "log-relationships", CheatLogRelationships));
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	public IEnumerable<NPCPersonalityAspect> EnumeratePersonalityAspects()
	{
		return _data.ai.aspects.Select(Game.serv.globals.settings.npc.FindAspectByID);
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitNewGame, OnGameStarted);
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitLoadedGame, OnGameStarted);
	}

	public override void OnPreRelease()
	{
		base.OnPreRelease();
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitLoadedGame, OnGameStarted);
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitNewGame, OnGameStarted);
		_advisors.ForEach(delegate(AIAdvisor a)
		{
			a.Release();
		});
	}

	private void OnGameStarted(SessionEvent sev)
	{
		NPCDefinition npcDef = _player.GetNpcDef();
		if (npcDef != null)
		{
			InitializeAdvisors(npcDef);
		}
	}

	private void InitializeAdvisors(NPCDefinition def)
	{
		_data.ai.InitializePersonality(_player);
		NPCPersonalityDefinition personalityDef = _data.ai.GetPersonalityDef();
		if (personalityDef != null)
		{
			def = NPCDefinition.UnifyLabelsWithPersonality(def, personalityDef);
		}
		if (def.units.IsSet)
		{
			units = new UnitsAdvisor(this, def);
		}
		if (def.social.IsSet)
		{
			social = new SocialAdvisor(this, def);
		}
		if (def.safehouse.IsSet)
		{
			safehouse = new SafehouseAdvisor(this, def);
		}
		if (def.territory.IsSet)
		{
			territory = new TerritoryAdvisor(this, def);
		}
		if (def.business.IsSet)
		{
			business = new BusinessAdvisor(this, def);
		}
		if (def.precinct.IsSet)
		{
			precinct = new PrecinctAdvisor(this, def);
		}
		if (def.feds.IsSet)
		{
			feds = new FedsAdvisor(this, def);
		}
		if (def.goon.IsSet)
		{
			goon = new GoonAdvisor(this, def);
		}
		if (def.attack.IsSet)
		{
			attack = new AttackAdvisor(this, def);
		}
		if (def.combat.IsSet)
		{
			combat = new CombatAdvisor(this, def);
		}
		_advisors = (from a in TypeUtils.GetMemberInstances<AIAdvisor>(this)
			where a != null
			select a).ToList();
		_advisors.ForEach(delegate(AIAdvisor a)
		{
			a.Initialize();
		});
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		Data.waitForEndOfTurn = ShouldWaitForTurn();
		if (!_player.territory.IsSafehouseVanquished && (!_player.IsHuman || _player.DebugHumanAsAI))
		{
			ClearAdvisorRequests();
			OnTurnUpdate();
			ProduceAdvisorRequests();
			AssignRequestsToAvailableCrew();
			MaybeAssignFallbackTasks();
		}
	}

	public void OnPlayerTurnEnded()
	{
	}

	private bool ShouldWaitForTurn()
	{
		if (!_player.IsHuman)
		{
			return false;
		}
		int runOnlyAIUntil = Game.serv.globals.settings.general.debug.runOnlyAIUntil;
		int yearsInt = Game.ctx.clock.Now.YearsInt;
		return runOnlyAIUntil <= 0 || yearsInt >= runOnlyAIUntil;
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		if (!Data.waitForEndOfTurn)
		{
			return PlayerTurnStatus.TurnFinished;
		}
		return PlayerTurnStatus.TurnWaitingForInput;
	}

	public void MarkPlayerTurnAsDone()
	{
		Data.waitForEndOfTurn = false;
	}

	private void ClearAdvisorRequests()
	{
		_requests.Clear();
	}

	private void OnTurnUpdate()
	{
		foreach (AIAdvisor advisor in _advisors)
		{
			advisor.OnTurnUpdate();
		}
	}

	public void ReadCallback(string message, EntityID building)
	{
		if (message != null && message == "will-steal")
		{
			if (building.FindEntity().data.building.outpost.IsHumanPlayer)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.WILL_STEAL, TickerTitle.DEFAULT, Loc.Get("ui.tickers.outpost.will-steal"), building, TickerPersistType.Persist);
			}
		}
		else
		{
			Logger.Warning("No callback for message: " + message + " in ReadCallback");
		}
	}

	private bool IsRequestAssignedTo(EntityID entityId)
	{
		foreach (AdvisorRequest request in _requests)
		{
			if (request.assignedTo == entityId)
			{
				return true;
			}
		}
		return false;
	}

	private void ProduceAdvisorRequests()
	{
		foreach (AIAdvisor advisor in _advisors)
		{
			advisor.ProduceRequests(_requests);
		}
		_requests.StableSort((AdvisorRequest a, AdvisorRequest b) => b.priority - a.priority);
	}

	private void AssignRequestsToAvailableCrew()
	{
		foreach (AdvisorRequest request in _requests)
		{
			bool flag = false;
			if (!flag)
			{
				flag = TryDispatchToAssigned(request);
			}
			if (!flag)
			{
				flag = TryDispatchToAvailable(request);
			}
			request.advisor.OnRequestDispatched(request, flag);
			AILog.LogRequest(flag, _pid, request);
		}
	}

	private bool TryDispatchToAssigned(AdvisorRequest request)
	{
		EntityID assignedTo = request.assignedTo;
		if (!assignedTo.IsValid)
		{
			return false;
		}
		if (_player.commands.PeepHasTask(assignedTo))
		{
			return false;
		}
		ScriptDispatcher.RunScript(request.script, _pid, assignedTo.FindEntity(), request.variables);
		return true;
	}

	private bool TryDispatchToAvailable(AdvisorRequest request)
	{
		EntityID eligibleCrewMember = units.GetEligibleCrewMember();
		if (eligibleCrewMember.IsNotValid || eligibleCrewMember.FindEntity().components.agent.IsInjured())
		{
			return false;
		}
		ScriptDispatcher.RunScript(request.script, _pid, eligibleCrewMember.FindEntity(), request.variables);
		request.assignedTo = eligibleCrewMember;
		return true;
	}

	private void MaybeAssignFallbackTasks()
	{
		using ListPool<EntityID>.PooledBlockList pooledBlockList = ListPool<EntityID>.Allocate();
		_player.crew.PopulateWithIDs(pooledBlockList, onlyLiving: true);
		foreach (EntityID item in pooledBlockList)
		{
			MaybeAssignFallbackTask(item);
		}
	}

	private void MaybeAssignFallbackTask(EntityID peepId)
	{
		Entity entity = peepId.FindEntity();
		if (entity.data.person.IsAlive && !IsRequestAssignedTo(peepId))
		{
			Game.serv.globals.settings.npc.fallback.RunRules(entity, _pid);
		}
	}

	internal void OnAddedDemand(Demand demand)
	{
		foreach (AIAdvisor advisor in _advisors)
		{
			if (demand.IsStateCompliant)
			{
				advisor.OnCompliance(demand);
			}
			if (demand.IsStateDefiant)
			{
				advisor.OnDefiance(demand);
			}
		}
	}

	private string CheatGrantFreeSkills(string[] arg)
	{
		string text = "";
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (TryGrantFreeSkills(item))
			{
				text += $"{item.PID}/{item.social.PlayerLastName} ";
			}
		}
		return "Granted free skills to: " + text;
		static bool TryGrantFreeSkills(PlayerInfo p)
		{
			if (!p.IsGangOrGoon)
			{
				return false;
			}
			if (p.crew.IsCrewDefeated)
			{
				return false;
			}
			return p.ai.safehouse?.ForceAddNextSkill() ?? false;
		}
	}

	private string CheatProposeMeetingTruce(string[] args)
	{
		if (args.Length != 3 || !short.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid id", args, 2, "<pid>");
		}
		PlayerID playerID = new PlayerID(result);
		ConvoInitiative.Topic topic = ConvoInitiative.Topic.TruceRequest;
		playerID.FindPlayer()?.ai?.social?.StartConvoInitiative(PlayerID.HumanPlayer, topic, EntityID.INVALID);
		return $"Player {playerID} will propose a meeting of type {topic}";
	}

	private string CheatSendFeds(string[] args)
	{
		if (args.Length != 3 || !int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 2, "<crew-index>");
		}
		CrewAssignment crewForIndex = Game.ctx.players.Human.crew.GetCrewForIndex(result);
		if (crewForIndex.IsNotValid || !crewForIndex.IsInVehicle)
		{
			return "Invalid crew index, crew not in venicle!";
		}
		Node node = crewForIndex.GetPeep().components.agent.GetNode();
		IEnumerable<PlayerID> values = Game.ctx.players.all.Where((PlayerInfo player) => player.IsJustFed).Select(delegate(PlayerInfo player)
		{
			player.ai.feds.RequestInvestigation(node);
			return player.PID;
		});
		return $"Feds responding to node {node}: " + string.Join(", ", values);
	}

	private string CheatArrestSomebody(string[] args)
	{
		if (args.Length != 3 || !short.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid player id", args, 2, "<enemy-player-id>");
		}
		PlayerInfo player = new PlayerID(result).FindPlayer();
		if (!player.IsGangOrGoon)
		{
			return $"Invalid player {player.PID} type, must be goon or gang, got {player.PlayerType}";
		}
		List<CrewAssignment> list = (from crew2 in player.crew.GetLiving()
			where Game.ctx.simman.cops.CanBeArrested(player, crew2)
			select crew2).ToList();
		if (list.Count == 0)
		{
			return "Nobody to arrest for " + player.social.FindPlayerGroupNameColorized();
		}
		CrewAssignment crew = list.FirstOrDefaultFast();
		Game.ctx.simman.cops.StartArrest(player, crew);
		return "Arrested " + crew.GetPeep().data.person.FullName + " from " + player.social.FindPlayerGroupNameColorized();
	}

	private string CheatError(string[] arg)
	{
		Game.serv.stats.LogException("Sic transit gloria mundi");
		return "To err is human";
	}

	private string CheatLogDecisions(string[] arg)
	{
		return "Wrote AI event log: " + AILog.ProduceLog(decisions: true, entries: false, relationships: false);
	}

	private string CheatLogEvents(string[] arg)
	{
		return "Wrote AI event log: " + AILog.ProduceLog(decisions: false, entries: true, relationships: false);
	}

	private string CheatLogRelationships(string[] arg)
	{
		return "Wrote AI event log: " + AILog.ProduceLog(decisions: false, entries: false, relationships: true);
	}
}

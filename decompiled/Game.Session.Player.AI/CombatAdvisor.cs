using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.Commands;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public class CombatAdvisor : AIAdvisor
{
	public struct Trespasser
	{
		public PlayerInfo player;

		public Entity peep;
	}

	private CombatAdvisorData _data;

	private CombatAdvisorConfig _def;

	private CombatAdvisorConfig.AggroDef _aggroDef;

	private CombatAdvisorConfig.JointWarDef _jointWarDef;

	public const int SCORE_FOR_FORCE_AGGRO = -15;

	public bool HasHitmanTarget => _data.hitman != null;

	public CombatAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = Game.serv.globals.settings.npc.FindAdvisorConfig<CombatAdvisorConfig>(def.combat);
		_aggroDef = _def.aggro;
		_jointWarDef = _def.jointWarDef;
		_data = _manager.Data.combat;
	}

	public override void Initialize()
	{
		base.Initialize();
		Game.ctx.events.AddListener(SessionEventType.GangWarAction, OnGangWarAction);
	}

	public override void Release()
	{
		base.Release();
		Game.ctx.events.RemoveListener(SessionEventType.GangWarAction, OnGangWarAction);
	}

	public override void OnTurnUpdate()
	{
		UpdateExpiredRequests();
		UpdateTrespassers();
		UpdateAggro();
		UpdateRequestsAfterAggro();
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
	}

	private void UpdateTrespassers()
	{
		if (Game.ctx.tutorial.AreGoonTrespassChecksSuppressed)
		{
			return;
		}
		using ListPool<Trespasser>.PooledBlockList pooledBlockList = ListPool<Trespasser>.Allocate();
		CheckAllTerritoryForTrespassers(pooledBlockList);
		foreach (Trespasser item in pooledBlockList)
		{
			_player.ai.social.RememberSocialActionOnMe(SocialConstants.GANG_TRESPASS, item.player.PID);
		}
	}

	private void CheckAllTerritoryForTrespassers(List<Trespasser> enemies)
	{
		foreach (NodeID ownedNodeId in _manager.PlayerInfo.territory.OwnedNodeIds)
		{
			foreach (EntityID item in Game.ctx.transit.GetAllAgentsAtNodeUnsafe(ownedNodeId))
			{
				Entity entity = item.FindEntity();
				PlayerInfo player = entity.components.agent.GetPlayer();
				if (player == null || player.PID == _pid || player.IsCopOrFed || player.PID == PlayerID.System)
				{
					continue;
				}
				AgentComponent agent = entity.components.agent;
				if (agent != null && agent.FindCrewAssignment().IsInVehicle)
				{
					Demand demand = Game.ctx.simman.demands.FindOrNull(player.PID, _pid);
					if (demand == null || !demand.IsStateCompliant)
					{
						enemies.Add(new Trespasser
						{
							peep = entity,
							player = player
						});
					}
				}
			}
		}
	}

	public bool IsAggroOnAnybody()
	{
		return _data.HasAggroAny();
	}

	public bool IsAggroAnyType(PlayerID pid)
	{
		return _data.HasAggro(pid);
	}

	public bool IsAggroAndTruce(PlayerID pid)
	{
		return _data.GetAggroOrNull(pid)?.hasTruce ?? false;
	}

	public bool IsAggroWithoutTruce(PlayerID pid)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(pid);
		if (aggroOrNull != null)
		{
			return !aggroOrNull.hasTruce;
		}
		return false;
	}

	public (bool isAggro, bool hasTruce) GetAggroAndTruce(PlayerID pid)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(pid);
		return (isAggro: aggroOrNull != null, hasTruce: aggroOrNull?.hasTruce ?? false);
	}

	public bool IsAttackAllowed(PlayerID pid)
	{
		return IsAggroWithoutTruce(pid);
	}

	public bool IsAttackNotAllowed(PlayerID pid)
	{
		return !IsAttackAllowed(pid);
	}

	public (bool isAggro, int days) GetAggroLengthDays(PlayerID pid)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(pid);
		if (aggroOrNull == null)
		{
			return (isAggro: false, days: 0);
		}
		int deltadays = (Game.ctx.clock.Now - aggroOrNull.started).deltadays;
		return (isAggro: true, days: deltadays);
	}

	public (bool hasTruce, SimTime expires) GetTruceExpirationIfExists(PlayerID other)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(other);
		if (aggroOrNull == null)
		{
			return (hasTruce: false, expires: default(SimTime));
		}
		return (hasTruce: true, expires: aggroOrNull.truceEnd);
	}

	public (bool isAggro, SimTime time, bool ready) GetMinTimeToStartTruce(PlayerID other)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(other);
		if (aggroOrNull == null)
		{
			return (isAggro: false, time: default(SimTime), ready: false);
		}
		Fixnum fixnum = _aggroDef.truce.reqAggroDayz.Evaluate(_pid);
		SimTime simTime = aggroOrNull.started.IncrementDays(fixnum.IntFloor());
		bool item = simTime <= Game.ctx.clock.Now;
		return (isAggro: true, time: simTime, ready: item);
	}

	public bool CanAskForTruce(PlayerID pid)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(pid);
		if (aggroOrNull == null)
		{
			return false;
		}
		if (aggroOrNull.hasTruce)
		{
			return false;
		}
		if (_aggroDef.truce == null)
		{
			return false;
		}
		PlayerInfo playerInfo = pid.FindPlayer();
		if (playerInfo.IsAnyAIPlayer)
		{
			CombatAdvisor combatAdvisor = playerInfo.ai?.combat;
			if (combatAdvisor == null)
			{
				return false;
			}
			if (!combatAdvisor.IsAggroWithoutTruce(_pid))
			{
				return false;
			}
			if (combatAdvisor._aggroDef?.truce == null)
			{
				return false;
			}
		}
		if (!GetMinTimeToStartTruce(pid).ready)
		{
			return false;
		}
		if (aggroOrNull.nextTruceAskTime > Game.ctx.clock.Now)
		{
			return false;
		}
		return true;
	}

	internal string DebugGetAggroScores()
	{
		return _data.aggro.SelectToString((CombatAdvisorData.AggroEntry e) => $"Aggro on {e.pid}: {e.lastScore} pts", " - ");
	}

	public void UpdateAggro()
	{
		if (_data.HasAggroAny() && _player.crew.IsCrewDefeated)
		{
			_data.RemoveAllAggro();
			return;
		}
		var (start, stop) = FindAggroThresholds();
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			PlayerID pID = item.PID;
			if (!(pID == _pid) && !pID.IsNotAnyPlayer && !item.IsCopOrFed)
			{
				var (canAggro, score) = ComputeAggroScoreFor(pID);
				UpdateAggroFor(pID, canAggro, score, start, stop);
			}
		}
	}

	private (bool valid, Fixnum result) ComputeAggroScoreFor(PlayerID pid)
	{
		Fixnum fixnum = Fixnum.ZERO;
		if (_player.crew.IsCrewDefeated)
		{
			return (valid: false, result: fixnum);
		}
		if (!_player.meetings.IsPlayerMet(pid))
		{
			return (valid: false, result: fixnum);
		}
		Demand demand = Game.ctx.simman.demands.FindOrNull(pid, _pid);
		if (demand != null && demand.IsStateCompliant)
		{
			return (valid: false, result: fixnum);
		}
		fixnum += _player.social.EvaluateRelationshipFromPlayerTo(pid);
		CombatAdvisorData.HitmanEntry hitman = _data.hitman;
		if (hitman != null && hitman.target == pid)
		{
			fixnum += _data.hitman.aggroRelBonus;
		}
		if (_data.jointWars.Find((CombatAdvisorData.JointWarEntry x) => x.target == pid) != null && fixnum > -15)
		{
			fixnum = -15;
		}
		return (valid: true, result: fixnum);
	}

	private void UpdateAggroFor(PlayerID pid, bool canAggro, Fixnum score, Fixnum start, Fixnum stop)
	{
		bool flag = false;
		var (flag2, flag3) = GetAggroAndTruce(pid);
		if (!canAggro && flag2)
		{
			RemoveAggroOn(pid);
			flag = true;
		}
		if (canAggro && score >= stop && flag2 && !flag3)
		{
			RemoveAggroOn(pid);
			flag = true;
		}
		if (canAggro && score <= start && !flag2)
		{
			AddAggroOn(pid, Fixnum.ZERO);
			MaybeShowFeedbackOnAggro(pid);
			flag = true;
		}
		if (canAggro)
		{
			_data.UpdateAggroScore(pid, score);
		}
		if (flag && pid.IsHumanPlayer)
		{
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	private void AddAggroOn(PlayerID pid, Fixnum score)
	{
		_data.AddAggroHelper(pid, score);
		PlayerSocial.DebugLogAIHistory(_pid, pid, null, null, "ai", "aggro-start");
		if (pid.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_WAR_START, TickerTitle.GANG_WAR_START, Loc.Get("ui.tickers.gangwar.start", "groupname", _player.social.FindPlayerGroupNameColorized()), _player.territory.GetHeadquartersNode().id);
		}
	}

	private void RemoveAggroOn(PlayerID pid)
	{
		_data.RemoveAggroHelper(pid);
		PlayerSocial.DebugLogAIHistory(_pid, pid, null, null, "ai", "aggro-end");
		if (pid.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_WAR_END, TickerTitle.GANG_WAR_END, Loc.Get("ui.tickers.gangwar.end", "groupname", _player.social.FindPlayerGroupNameColorized()), _player.territory.GetHeadquartersNode().id);
		}
	}

	internal CombatAdvisorConfig.AggroDef GetAggroDef()
	{
		return _aggroDef;
	}

	private void UpdateExpiredRequests()
	{
		SimTime now = Game.ctx.clock.Now;
		foreach (CombatAdvisorData.AggroEntry item in _data.aggro)
		{
			if (item.hasTruce && item.truceEnd < now)
			{
				EndTruceSymmetric(_pid, item.pid);
			}
		}
		for (int num = _data.jointWars.Count() - 1; num >= 0; num--)
		{
			CombatAdvisorData.JointWarEntry jointWarEntry = _data.jointWars[num];
			if (jointWarEntry.agreedEnd < now)
			{
				if (!jointWarEntry.fulfilled && !_pid.IsHumanPlayer)
				{
					ProcessJointWarBreakingActionBy(jointWarEntry.requested);
				}
				else
				{
					EndJointWarSymmetric(_pid, jointWarEntry.requested, jointWarEntry.target);
				}
			}
		}
	}

	private void UpdateRequestsAfterAggro()
	{
		foreach (CombatAdvisorData.AggroEntry item in _data.aggro)
		{
			if (CanAskForTruce(item.pid))
			{
				MaybeAskForTruce(item.pid);
			}
			if (CanAskForJointWarAgainst(item.pid))
			{
				MaybeAskForJointWarAgainst(item.pid);
			}
		}
	}

	public (Fixnum lengthDays, Fixnum acceptCost, Fixnum acceptTruceProb) CalculateForAcceptingTruce(PlayerID sender, PlayerID recipient)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAcceptingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAcceptingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAcceptingGangCooperation.Item1, evaluatorAndTargetForAcceptingGangCooperation.Item2, evaluatorAndTargetForAcceptingGangCooperation.Item3);
		return (lengthDays: _aggroDef.truce.lengthDayz.Evaluate(query), acceptCost: _aggroDef.truce.acceptCost.Evaluate(query), acceptTruceProb: _aggroDef.truce.acceptTruceProb.Evaluate(query));
	}

	private (Fixnum askForTruceCrewDelta, Fixnum askForTruceProb, Fixnum askCooldownDays) CalculateForAskingTruce(PlayerID sender, PlayerID recipient)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAskingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAskingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAskingGangCooperation.Item1, evaluatorAndTargetForAskingGangCooperation.Item2, evaluatorAndTargetForAskingGangCooperation.Item3);
		return (askForTruceCrewDelta: _aggroDef.truce.askForTruceMaxCrewDelta.Evaluate(query), askForTruceProb: _aggroDef.truce.askForTruceProb.Evaluate(query), askCooldownDays: _aggroDef.truce.askCooldownDayz.Evaluate(query));
	}

	public (Fixnum lengthDays, Fixnum acceptCost, Fixnum acceptTruceProb) CalculateForAcceptingJointWar(PlayerID sender, PlayerID recipient, PlayerID target)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAcceptingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAcceptingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAcceptingGangCooperation.Item1, evaluatorAndTargetForAcceptingGangCooperation.Item2, evaluatorAndTargetForAcceptingGangCooperation.Item3);
		bool flag = recipient.FindPlayer().social.EvaluateRelationshipFromPlayerTo(target) > _jointWarDef.lockOutRel.Evaluate(query) || recipient.FindPlayer().ai.combat.GetAggroAndTruce(target).hasTruce;
		return (lengthDays: _jointWarDef.lengthDayz.Evaluate(query), acceptCost: _jointWarDef.acceptCost.Evaluate(query), acceptTruceProb: flag ? ((Fixnum)0) : _jointWarDef.acceptJointWarProb.Evaluate(query));
	}

	private (Fixnum askForJointWarCrewDelta, Fixnum askForJointWarProb, Fixnum askCooldownDays) CalculateForAskingJointWar(PlayerID sender, PlayerID recipient)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAskingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAskingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAskingGangCooperation.Item1, evaluatorAndTargetForAskingGangCooperation.Item2, evaluatorAndTargetForAskingGangCooperation.Item3);
		return (askForJointWarCrewDelta: _jointWarDef.askForJointWarCrewMinimum.Evaluate(query), askForJointWarProb: _jointWarDef.askForJointWarProb.Evaluate(query), askCooldownDays: _jointWarDef.askCooldownDayz.Evaluate(query));
	}

	private void MaybeAskForTruce(PlayerID other)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(other);
		(Fixnum askForTruceCrewDelta, Fixnum askForTruceProb, Fixnum askCooldownDays) tuple = CalculateForAskingTruce(_pid, other);
		Fixnum item = tuple.askForTruceCrewDelta;
		Fixnum item2 = tuple.askForTruceProb;
		Fixnum item3 = tuple.askCooldownDays;
		aggroOrNull.nextTruceAskTime = Game.ctx.clock.Now.IncrementDays(item3.IntFloor());
		int num;
		if (DoesCrewDeltaPass(item))
		{
			num = (_data.rng.CheckProbability(item2) ? 1 : 0);
			if (num != 0 && other.IsHumanPlayer && !_player.ai.social.HasConvoInitiative)
			{
				_player.ai.social.StartConvoInitiative(other, ConvoInitiative.Topic.TruceRequest, EntityID.INVALID);
			}
		}
		else
		{
			num = 0;
		}
		if (num == 0 || !other.IsAIPlayer)
		{
			return;
		}
		SocialAdvisor socialAdvisor = other.FindPlayer()?.ai?.social;
		if (socialAdvisor != null)
		{
			var (flag, days) = socialAdvisor.ComputeRequestParametersForAIAskingUs(_pid, ConvoInitiative.Topic.TruceRequest);
			if (flag)
			{
				StartTruceSymmetric(_pid, _player.social.PlayerPeepId, other, days);
			}
		}
		bool DoesCrewDeltaPass(Fixnum maxDelta)
		{
			int livingCrewCount = _player.crew.LivingCrewCount;
			int livingCrewCount2 = other.FindPlayer().crew.LivingCrewCount;
			return livingCrewCount - livingCrewCount2 <= maxDelta;
		}
	}

	public static void StartTruceSymmetric(PlayerID askingPlayer, EntityID askingPeep, PlayerID agreeingPlayer, int days)
	{
		SimTime end = Game.ctx.clock.Now.IncrementDays(days);
		StartTruceOneWay(askingPlayer, agreeingPlayer, end, askingPeep);
		StartTruceOneWay(agreeingPlayer, askingPlayer, end, askingPeep);
	}

	private static void StartTruceOneWay(PlayerID from, PlayerID to, SimTime end, EntityID crewpeep)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.combat.StartTruceOneWay(to, end, crewpeep);
		}
	}

	private void StartTruceOneWay(PlayerID other, SimTime end, EntityID crewpeep)
	{
		_data.UpdateTruce(other, hasTruce: true, end);
		_player.social.GetRelationshipFromPlayerTo(other)?.AddBuff(BuffConstants.RELBUFF_GANG_AGREEMENT, crewpeep);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Truce with {other} until {end}");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "truce-start");
		if (!other.IsHumanPlayer)
		{
			return;
		}
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_AGREEMENT_START, TickerTitle.GANG_AGREEMENT_START, Loc.Get("ui.tickers.gangtruce.start", "groupname", _player.social.FindPlayerGroupNameColorized(), "date", Loc.FormatDateLong(end)), _player.territory.GetHeadquartersNode().id);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			CancelAnyAttacks(item.peepId);
		}
		void CancelAnyAttacks(EntityID peepId)
		{
			if (_player.commands.PeepHasTask(peepId) && _player.commands.EnumerateCommands(peepId).Any((Command c) => IsAttackCommand(c)))
			{
				_player.commands.FlushQueue(peepId, cancelActive: true);
			}
		}
		static bool IsAttackCommand(Command c)
		{
			if (!(c is CommandAttack))
			{
				return c is AICommandAttackBuilding;
			}
			return true;
		}
	}

	public static void EndTruceSymmetric(PlayerID askingPlayer, PlayerID agreeingPlayer)
	{
		EndTruceOneWay(askingPlayer, agreeingPlayer);
		EndTruceOneWay(agreeingPlayer, askingPlayer);
	}

	private static void EndTruceOneWay(PlayerID from, PlayerID to)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.combat.EndTruceOneWay(to);
		}
	}

	private void EndTruceOneWay(PlayerID other)
	{
		_data.UpdateTruce(other, hasTruce: false, null);
		_player.social.GetRelationshipFromPlayerTo(other)?.RemoveBuff(BuffConstants.RELBUFF_GANG_AGREEMENT);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Truce with {other} ENDED");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "truce-end");
		if (other.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_TRUCE_END, TickerTitle.GANG_TRUCE_END, Loc.Get("ui.tickers.gangtruce.end", "groupname", _player.social.FindPlayerGroupNameColorized()), _player.territory.GetHeadquartersNode().id);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	internal void ProcessTruceBreakingActionBy(PlayerID attacker)
	{
		var (flag, flag2) = GetAggroAndTruce(attacker);
		if (flag && flag2)
		{
			EndTruceSymmetric(attacker, _pid);
			_player.ai.social.RememberSocialActionOnMe(SocialConstants.GANG_BROKE_AGREEMENT, attacker);
		}
	}

	internal void ProcessBeingAttacked(PlayerID attacker, Entity peep)
	{
		_player.commands.FlushQueue(peep.Id, cancelActive: true);
	}

	private bool CanAskForJointWarAgainst(PlayerID target)
	{
		if (_jointWarDef == null || _jointWarDef.acceptCost == null)
		{
			return false;
		}
		(bool, bool) aggroAndTruce = GetAggroAndTruce(target);
		if (!aggroAndTruce.Item1 || aggroAndTruce.Item2)
		{
			return false;
		}
		if (!HasPotentialJointWarAlly(target))
		{
			return false;
		}
		if (!target.FindPlayer().IsJustGang && !target.FindPlayer().IsHuman)
		{
			return false;
		}
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(target);
		SimTime now = Game.ctx.clock.Now;
		if (aggroOrNull.started.IncrementDays((int)_jointWarDef.reqAggroDayz.Evaluate(_pid)) > now || aggroOrNull.nextJointWarAskTime > now)
		{
			return false;
		}
		return true;
	}

	private void MaybeAskForJointWarAgainst(PlayerID target)
	{
		CombatAdvisorData.AggroEntry aggroOrNull = _data.GetAggroOrNull(target);
		Xorshift rng = Game.ctx.scenario.MakeSeededRng<CombatAdvisor>();
		PlayerID ally = rng.PickElement(FindPotentialJointWarAlly(target));
		(Fixnum askForJointWarCrewDelta, Fixnum askForJointWarProb, Fixnum askCooldownDays) tuple = CalculateForAskingJointWar(_pid, ally);
		Fixnum item = tuple.askForJointWarCrewDelta;
		Fixnum item2 = tuple.askForJointWarProb;
		Fixnum item3 = tuple.askCooldownDays;
		aggroOrNull.nextTruceAskTime = Game.ctx.clock.Now.IncrementDays(item3.IntFloor());
		int num;
		if (DoesCrewMinimumPass(item))
		{
			num = (_data.rng.CheckProbability(item2) ? 1 : 0);
			if (num != 0 && ally.IsHumanPlayer && !_player.ai.social.HasConvoInitiative)
			{
				_player.ai.social.StartConvoInitiative(ally, ConvoInitiative.Topic.JointWar, target.FindPlayer().social.GetPlayerPeep().Id);
			}
		}
		else
		{
			num = 0;
		}
		if (num == 0 || !ally.IsAIPlayer)
		{
			return;
		}
		SocialAdvisor socialAdvisor = ally.FindPlayer()?.ai?.social;
		if (socialAdvisor != null)
		{
			var (flag, days) = socialAdvisor.ComputeRequestParametersForAIAskingUs(_pid, ConvoInitiative.Topic.TruceRequest);
			if (flag)
			{
				StartJointWarSymmetric(_pid, _player.social.PlayerPeepId, ally, days, target);
			}
		}
		bool DoesCrewMinimumPass(Fixnum minCrew)
		{
			return ally.FindPlayer().crew.LivingCrewCount >= minCrew;
		}
	}

	private List<PlayerID> FindPotentialJointWarAlly(PlayerID target)
	{
		return (from x in _player.meetings.GetPlayersAlreadyMet()
			where (x.FindPlayer().IsJustGang || x.FindPlayer().IsHuman) && !GetAggroAndTruce(x).isAggro && !x.FindPlayer().ai.combat.GetAggroAndTruce(_pid).isAggro && !x.FindPlayer().crew.IsCrewDefeated && !HasJointWar(x, target) && _pid != x
			select x).ToList();
	}

	private bool HasPotentialJointWarAlly(PlayerID target)
	{
		return FindPotentialJointWarAlly(target).Count > 0;
	}

	public static void StartJointWarSymmetric(PlayerID askingPlayer, EntityID askingPeep, PlayerID agreeingPlayer, int days, PlayerID targetPlayer)
	{
		SimTime end = Game.ctx.clock.Now.IncrementDays(days);
		StartJointWarOneWay(askingPlayer, agreeingPlayer, end, askingPeep, targetPlayer);
		StartJointWarOneWay(agreeingPlayer, askingPlayer, end, askingPeep, targetPlayer);
	}

	private static void StartJointWarOneWay(PlayerID from, PlayerID to, SimTime end, EntityID crewpeep, PlayerID targetPlayer)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.combat.StartJointWarOneWay(to, end, crewpeep, targetPlayer);
		}
	}

	private void StartJointWarOneWay(PlayerID other, SimTime end, EntityID crewpeep, PlayerID targetPlayer)
	{
		if (!_player.meetings.GetPlayersAlreadyMet().Contains(targetPlayer))
		{
			_player.meetings.MarkPlayersAsMutuallyMet(targetPlayer, introduceLeadersToCrew: true);
		}
		_data.jointWars.Add(new CombatAdvisorData.JointWarEntry(other, targetPlayer, end));
		_player.social.GetRelationshipFromPlayerTo(other)?.AddBuff(BuffConstants.RELBUFF_GANG_AGREEMENT, crewpeep);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Join war with {other} until {end} against {targetPlayer}");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "joint-war");
		if (other.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_AGREEMENT_START, TickerTitle.GANG_AGREEMENT_START, Loc.Get("ui.tickers.jointwar.start", "allyname", _player.social.FindPlayerGroupNameColorized(), "targetname", targetPlayer.FindPlayer().social.FindPlayerGroupNameColorized(), "date", Loc.FormatDateLong(end)), _player.territory.GetHeadquartersNode().id);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	public static void EndJointWarSymmetric(PlayerID askingPlayer, PlayerID agreeingPlayer, PlayerID target)
	{
		EndJointWarOneWay(askingPlayer, agreeingPlayer, target);
		EndJointWarOneWay(agreeingPlayer, askingPlayer, target);
	}

	private static void EndJointWarOneWay(PlayerID from, PlayerID to, PlayerID target)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.combat.EndJointWarOneWay(to, target);
		}
	}

	private void EndJointWarOneWay(PlayerID other, PlayerID target)
	{
		_data.jointWars.Remove(GetJointWarData(other, target));
		_player.social.GetRelationshipFromPlayerTo(other)?.RemoveBuff(BuffConstants.RELBUFF_GANG_AGREEMENT);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Truce with {other} ENDED");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "truce-end");
		if (other.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_TRUCE_END, TickerTitle.GANG_TRUCE_END, Loc.Get("ui.tickers.jointwar.end", "allyname", _player.social.FindPlayerGroupNameColorized(), "targetname", target.FindPlayer().social.FindPlayerGroupNameColorized()), _player.territory.GetHeadquartersNode().id);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	public bool HasJointWar(PlayerID with, PlayerID against)
	{
		return _data.jointWars.Find((CombatAdvisorData.JointWarEntry x) => x.requested == with && x.target == against) != null;
	}

	public CombatAdvisorData.JointWarEntry GetJointWarData(PlayerID with, PlayerID against)
	{
		return _data.jointWars.Find((CombatAdvisorData.JointWarEntry x) => x.requested == with && x.target == against);
	}

	internal void ProcessJointWarBreakingActionBy(PlayerID attacker)
	{
		foreach (CombatAdvisorData.JointWarEntry item in _data.jointWars.Where((CombatAdvisorData.JointWarEntry x) => x.requested == attacker).ToList())
		{
			EndJointWarSymmetric(item.requested, _pid, item.target);
			_player.ai.social.RememberSocialActionOnMe(SocialConstants.GANG_BROKE_AGREEMENT, attacker);
		}
	}

	internal void OnGangWarAction(SessionEvent sessionEvent)
	{
		PlayerID attacker = sessionEvent.pid;
		if (!(sessionEvent.ctx is PlayerID playerID))
		{
			return;
		}
		IEnumerable<CombatAdvisorData.JointWarEntry> enumerable = _data.jointWars.Where((CombatAdvisorData.JointWarEntry x) => x.requested == attacker && !x.fulfilled);
		if (enumerable.Count() == 0)
		{
			return;
		}
		foreach (CombatAdvisorData.JointWarEntry item in enumerable)
		{
			if (item.target == playerID)
			{
				item.fulfilled = true;
			}
		}
	}

	public void AddHitmanTarget(PlayerID employer, PlayerID target)
	{
		_data.hitman = new CombatAdvisorData.HitmanEntry
		{
			employer = employer,
			target = target,
			started = Game.ctx.clock.Now
		};
		UpdateAggro();
	}

	public void RemoveHitmanTarget()
	{
		_data.hitman = null;
		UpdateAggro();
	}

	public override void OnCompliance(Demand demand)
	{
		if (IsAggroAnyType(demand.source))
		{
			RemoveAggroOn(demand.source);
		}
		if (demand.source.IsHumanPlayer)
		{
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	private void MaybeShowFeedbackOnAggro(PlayerID pid)
	{
		if (pid.IsHumanPlayer && _player.IsJustGang && _aggroDef.photos != null)
		{
			Game.serv.ui.AddPopup(new PhotoPopup(_aggroDef.photos, null));
		}
	}

	private (Fixnum start, Fixnum stop) FindAggroThresholds()
	{
		ModQuery query = new ModQuery(_pid);
		Fixnum item = _aggroDef.start.Evaluate(query);
		Fixnum item2 = _aggroDef.stop.Evaluate(query);
		return (start: item, stop: item2);
	}
}

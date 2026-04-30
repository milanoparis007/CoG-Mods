using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.Commands;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class TerritoryAdvisor : AIAdvisor
{
	private TerritoryAdvisorConfig _def;

	private TerritoryAdvisorData _data;

	private TerritoryPotentials _gen;

	private TerritoryAdvisorConfig.ExpansionTreatyDef _expansionTreatyDef;

	private TerritoryAdvisorConfig.OutpostAgreementDef _outpostAgreementDef;

	public TerritoryAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = FindAdvisorConfig<TerritoryAdvisorConfig>(def.territory);
		_data = _manager.Data.territory;
		_gen = new TerritoryPotentials();
		_gen.Initialize(manager.PID, _def);
		_expansionTreatyDef = _def.expansionTreaty;
		_outpostAgreementDef = _def.outpostAgreementDef;
		Game.ctx.events.AddListener(SessionEventType.OutpostStealFailed, OnOutpostAttacked);
		Game.ctx.events.AddListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
	}

	public override void Release()
	{
		_gen.Release();
		_gen = null;
		Game.ctx.events.RemoveListener(SessionEventType.OutpostStealFailed, OnOutpostAttacked);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
		base.Release();
	}

	public override void OnTurnUpdate()
	{
		CallWithCooldown(FindOutpostToStart, ref _data.nextOutpostCheck, _def.growth.firstCheckDayz, _def.growth.cooldownDayz);
		CallWithCooldown(FindOutpostToSteal, ref _data.nextStealCheck, _def.stealingOutpost.firstCheckDayz, _def.stealingOutpost.cooldownDayzAfterCheck);
		UpdateExpiredTreaties();
		FindOutpostToVisit();
		UpdateNextNodeToExplore();
		UpdateNextBuildingToScopeOut();
	}

	private void FindOutpostToStart()
	{
		if (_data.startOutpost.IsNotValid)
		{
			_data.startOutpost = FindOutpostToStartHelper();
			if (_data.startOutpost.IsValid)
			{
				AILog.LogAIDecision(_pid, this, $"Start outpost at {_data.startOutpost.FindEntity()}");
			}
		}
	}

	private EntityID FindOutpostToStartHelper()
	{
		if (_player.outposts.IsAnyOutpostExpanding())
		{
			return EntityID.INVALID;
		}
		if (IsInStolenCooldown(_player.outposts.LastStolenTime))
		{
			return EntityID.INVALID;
		}
		NodeID nodeId = _gen.PickBestNode();
		if (!nodeId.IsValid)
		{
			return EntityID.INVALID;
		}
		return PickBuildingToMoveInto(nodeId);
	}

	private bool IsInStolenCooldown(SimTime lastStolenTime)
	{
		if (lastStolenTime.IsMinDate)
		{
			return false;
		}
		Fixnum fixnum = _def.growth.cooldownDayzAfterStolen.Evaluate(new ModQuery(_pid));
		return lastStolenTime.IncrementDays((int)fixnum) > Game.ctx.clock.Now;
	}

	private void FindOutpostToVisit()
	{
		if (_data.visitOutpost.IsNotValid)
		{
			(_data.visitOutpost, _) = FindOutpostToVisitHelper();
		}
		_ = _data.visitOutpost.IsValid;
	}

	private (EntityID eid, bool support) FindOutpostToVisitHelper()
	{
		using ListPool<EntityID>.PooledBlockList pooledBlockList = ListPool<EntityID>.Allocate();
		foreach (OutpostEntry item in _player.outposts.GetOutpostEntriesUnsafe())
		{
			if (_player.outposts.IsOutpostReadyForCollection(item.outpostId))
			{
				if (item.money.NeedsSupport)
				{
					return (eid: item.outpostId.buildingId, support: true);
				}
				pooledBlockList.Add(item.outpostId.buildingId);
			}
		}
		return (eid: (pooledBlockList.Count > 0) ? _data.rng.PickElement(pooledBlockList) : EntityID.INVALID, support: false);
	}

	private EntityID PickBuildingToMoveInto(NodeID nodeId)
	{
		Node node = nodeId.FindNode();
		if (node.owner.IsSet)
		{
			return EntityID.INVALID;
		}
		return node.interesting.FirstOrDefaultFast();
	}

	private void UpdateNextNodeToExplore()
	{
		Node node = _data.nextNodeToExplore.FindNode();
		if (node != null && node.known.Get(_pid))
		{
			_data.nextNodeToExplore = NodeID.INVALID;
		}
		if (_data.nextNodeToExplore.IsValid)
		{
			return;
		}
		Node headquartersNode = _player.territory.GetHeadquartersNode();
		Node found = null;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(headquartersNode, int.MaxValue, delegate(Node n)
		{
			if (!n.known.Get(_pid))
			{
				found = n;
			}
		}, null, null, (Node n) => found != null, onlyBizNodes: true);
		if (found != null)
		{
			float nextNodeProbPerTurn = FindNodeProbability(headquartersNode, found);
			_data.nextNodeToExplore = found.id;
			_data.nextNodeProbPerTurn = nextNodeProbPerTurn;
		}
	}

	private float FindNodeProbability(Node homeNode, Node targetNode)
	{
		float magnitude = (homeNode.pos - targetNode.pos).Magnitude;
		ModQuery query = new ModQuery(_pid, targetNode);
		float num = (float)_def.exploration.maxDistance.Evaluate(query);
		if (magnitude > num)
		{
			return 0f;
		}
		float num2 = (float)_def.exploration.nearDistance.Evaluate(query);
		float num3 = (float)_def.exploration.farDistance.Evaluate(query);
		float num4 = (float)_def.exploration.nearProb.Evaluate(query);
		float num5 = (float)_def.exploration.farProb.Evaluate(query);
		if (magnitude > num3)
		{
			return num5;
		}
		if (magnitude < num2)
		{
			return num4;
		}
		return MathUtil.ConvertRange(magnitude, num2, num3, num4, num5);
	}

	private void UpdateNextBuildingToScopeOut()
	{
		Entity entity = _data.nextBuildingToScope.FindEntity();
		if (entity != null && _player.territory.IsScoped(entity))
		{
			_data.nextBuildingToScope = EntityID.INVALID;
		}
		if (!_data.nextBuildingToScope.IsValid)
		{
			Node headquartersNode = _player.territory.GetHeadquartersNode();
			Node node = null;
			Entity target = null;
			Game.ctx.board.nodes.VisitNeighborhoodBFS(headquartersNode, int.MaxValue, delegate(Node n)
			{
				(target, node) = FindTargetToScopeAtNode(n);
			}, null, null, (Node _) => target != null, onlyBizNodes: true);
			if (target != null)
			{
				float nextBuildingProbPerTurn = FindScopeOutProbability(target, node);
				_data.nextBuildingToScope = target.Id;
				_data.nextBuildingProbPerTurn = nextBuildingProbPerTurn;
			}
		}
	}

	private (Entity target, Node node) FindTargetToScopeAtNode(Node node)
	{
		if (node.known.Get(_pid))
		{
			foreach (EntityID item in node.interesting)
			{
				Entity entity = item.FindEntity();
				if (!_player.territory.IsScoped(entity))
				{
					return (target: entity, node: node);
				}
			}
		}
		return (target: null, node: null);
	}

	private float FindScopeOutProbability(Entity target, Node node)
	{
		ModQuery query = new ModQuery(_pid, target.Id, node.id);
		return (float)_def.scopeout.scopeProb.Evaluate(query);
	}

	private void FindOutpostToSteal()
	{
		if (!_data.nextOutpostToSteal.outpostId.IsValid)
		{
			_data.nextOutpostToSteal = FindOutpostToStealHelper();
			if (_data.nextOutpostToSteal.outpostId.IsValid)
			{
				PlayerID outpost = _data.nextOutpostToSteal.outpostId.FindBuilding().data.building.outpost;
				AILog.LogAIDecision(_pid, this, $"Steal outpost from {outpost} at {_data.nextOutpostToSteal.outpostId.FindBuilding()}");
			}
		}
	}

	private OutpostToSteal FindOutpostToStealHelper()
	{
		ModQuery query = new ModQuery(_pid);
		List<OutpostToSteal> list = null;
		float probability = (float)_def.stealingOutpost.stealingProbEnemy.Evaluate(query);
		float probability2 = (float)_def.stealingOutpost.stealingProbNonEnemy.Evaluate(query);
		bool flag = _data.rng.CheckProbability(probability);
		bool flag2 = _data.rng.CheckProbability(probability2);
		using ListPool<OutpostToSteal>.PooledBlockList pooledBlockList = ListPool<OutpostToSteal>.Allocate();
		using ListPool<OutpostToSteal>.PooledBlockList pooledBlockList2 = ListPool<OutpostToSteal>.Allocate();
		FindEverybodysOutposts(pooledBlockList, pooledBlockList2);
		using (ListPool<WorldPos>.PooledBlockList pooledBlockList3 = ListPool<WorldPos>.Allocate())
		{
			ProduceOurOutpostPositions(pooledBlockList3);
			if (list == null && flag)
			{
				FilterByDistanceToOurs(pooledBlockList, pooledBlockList3);
				FilterByAgreements(pooledBlockList);
				if (pooledBlockList.Count > 0)
				{
					list = pooledBlockList;
				}
			}
			if (list == null && flag2)
			{
				FilterByDistanceToOurs(pooledBlockList2, pooledBlockList3);
				FilterByAgreements(pooledBlockList2);
				if (pooledBlockList2.Count > 0)
				{
					list = pooledBlockList2;
				}
			}
		}
		return _data.rng.PickElementOrDefault(list);
	}

	private void FindEverybodysOutposts(List<OutpostToSteal> enemies, List<OutpostToSteal> randos)
	{
		Fixnum fixnum = _def.stealingOutpost.relThresholdNonEnemy.Evaluate(new ModQuery(_pid));
		foreach (PlayerInfo item4 in Game.ctx.players.all)
		{
			if (item4 == _player || item4.PID.IsNotAnyPlayer)
			{
				continue;
			}
			(bool isAggro, bool hasTruce) aggroAndTruce = _player.ai.combat.GetAggroAndTruce(item4.PID);
			bool item = aggroAndTruce.isAggro;
			bool item2 = aggroAndTruce.hasTruce;
			bool flag = item && !item2;
			if (item2 || (!flag && _player.social.EvaluateRelationshipFromPlayerTo(item4.PID) > fixnum) || DemandedThatWeStopStealingOutposts(item4.PID))
			{
				continue;
			}
			foreach (OutpostEntry item5 in item4.outposts.GetOutpostEntriesUnsafe())
			{
				if (item4.outposts.CanRemoveOutpostByStealing(item5.outpostId))
				{
					OutpostToSteal item3 = new OutpostToSteal(item4.PID, item5.outpostId, flag);
					(item3.isEnemy ? enemies : randos).Add(item3);
				}
			}
		}
	}

	private void OnOutpostAttacked(SessionEvent sessionEvent)
	{
		EntityID eid = sessionEvent.eid;
		PlayerID pid = sessionEvent.pid;
		if (!(eid == EntityID.INVALID) && !(pid != base.PID))
		{
			_data.outpostToDefend = eid;
		}
	}

	private bool DemandedThatWeStopStealingOutposts(PlayerID pid)
	{
		Demand demand = Game.ctx.simman.demands.FindOrNull(pid, _pid);
		if (demand != null && demand.IsStateCompliant)
		{
			return demand.HasDemandSet(Demand.Type.StopStealingOutpost);
		}
		return false;
	}

	private void ProduceOurOutpostPositions(List<WorldPos> list)
	{
		list.Add(_player.territory.GetHeadquartersNode().pos);
		foreach (OutpostEntry item in _player.outposts.GetOutpostEntriesUnsafe())
		{
			list.Add(item.OutpostNode.nodeId.FindNode().pos);
		}
	}

	private void FilterByDistanceToOurs(List<OutpostToSteal> list, List<WorldPos> ours)
	{
		float maxDistance = (float)_def.stealingOutpost.maxDistanceToOurOutpost.Evaluate(new ModQuery(_pid));
		for (int num = list.Count - 1; num >= 0; num--)
		{
			OutpostToSteal target = list[num];
			if (!IsNearOurOutposts(target, ours, maxDistance))
			{
				list.RemoveAt(num);
			}
		}
	}

	private void FilterByAgreements(List<OutpostToSteal> list)
	{
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (HasOutpostAgreementWith(list[num].playerId))
			{
				list.RemoveAt(num);
			}
		}
	}

	private bool IsNearOurOutposts(OutpostToSteal target, List<WorldPos> ours, float maxDistance)
	{
		WorldPos worldpos = target.outpostId.FindBuilding().data.board.worldpos;
		foreach (WorldPos our in ours)
		{
			if ((our - worldpos).Magnitude <= maxDistance)
			{
				return true;
			}
		}
		return false;
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		results.AddIfNotNull(ProduceSingleRequestRivals());
		results.AddIfNotNull(ProduceSingleRequestUpkeep());
	}

	private AdvisorRequest ProduceSingleRequestRivals()
	{
		AdvisorRequest advisorRequest = null;
		if (advisorRequest == null && _data.outpostToDefend.FindEntity() != null)
		{
			EntityID andClear = _data.GetAndClear(ref _data.outpostToDefend);
			advisorRequest = new AdvisorRequest(this, ScriptNames.PROTECT_OUTPOST, AdvisorRequest.Priority.DefendOutpost, new Deictics
			{
				targetBuilding = andClear,
				number = 5
			});
		}
		if (advisorRequest == null && _data.nextOutpostToSteal.outpostId.IsValid)
		{
			OutpostToSteal andClear2 = _data.GetAndClear(ref _data.nextOutpostToSteal);
			advisorRequest = new AdvisorRequest(this, ScriptNames.OUTPOST_STEAL, AdvisorRequest.Priority.AttackAggroTarget, new Deictics
			{
				targetBuilding = andClear2.outpostId.buildingId,
				number = 1
			});
		}
		return advisorRequest;
	}

	private AdvisorRequest ProduceSingleRequestUpkeep()
	{
		AdvisorRequest advisorRequest = null;
		if (advisorRequest == null && _data.startOutpost.IsValid)
		{
			EntityID andClear = _data.GetAndClear(ref _data.startOutpost);
			advisorRequest = new AdvisorRequest(this, ScriptNames.OUTPOST_START, AdvisorRequest.Priority.StartOutpost, new Deictics
			{
				targetBuilding = andClear
			});
		}
		if (advisorRequest == null && _data.visitOutpost.IsValid)
		{
			EntityID andClear2 = _data.GetAndClear(ref _data.visitOutpost);
			advisorRequest = new AdvisorRequest(this, ScriptNames.OUTPOST_VISIT, AdvisorRequest.Priority.SupportOutpost, new Deictics
			{
				targetBuilding = andClear2
			});
		}
		if (advisorRequest == null && _data.nextBuildingToScope.IsValid)
		{
			float nextBuildingProbPerTurn = _data.nextBuildingProbPerTurn;
			if (_data.rng.CheckProbability(nextBuildingProbPerTurn))
			{
				Node node = _data.GetAndClear(ref _data.nextBuildingToScope).FindEntity().components.board.GetNode();
				advisorRequest = new AdvisorRequest(this, ScriptNames.SCOPE_BUILDING, AdvisorRequest.Priority.ScopeOutBuilding, new Deictics
				{
					targetNode = node.id
				});
			}
		}
		if (advisorRequest == null && _data.nextNodeToExplore.IsValid)
		{
			float nextNodeProbPerTurn = _data.nextNodeProbPerTurn;
			if (_data.rng.CheckProbability(nextNodeProbPerTurn))
			{
				NodeID andClear3 = _data.GetAndClear(ref _data.nextNodeToExplore);
				advisorRequest = new AdvisorRequest(this, ScriptNames.EXPLORE_NODE, AdvisorRequest.Priority.ExploreNode, new Deictics
				{
					targetNode = andClear3
				});
			}
		}
		return advisorRequest;
	}

	public void PeepAtOutpost(EntityID peepId, EntityID buildingId, string message)
	{
		switch (message)
		{
		case "start":
			StartOutpost(peepId, buildingId);
			return;
		case "visit":
			VisitOutpost(peepId, buildingId);
			return;
		case "maybe-steal":
			ExecuteStealOutpost(peepId, buildingId);
			return;
		}
		Logger.Warning("Unknown outpost message", message);
	}

	private void StartOutpost(EntityID peepId, EntityID buildingId)
	{
		Entity entity = buildingId.FindEntity();
		if (entity == null)
		{
			Logger.Warning("Missing outpost for peep", peepId);
		}
		else if (entity.components.building.IsOutpost)
		{
			Logger.Warning("Cannot double-start outpost", entity);
		}
		else
		{
			_player.outposts.SetOutpost(entity);
			AILog.LogMilestone(_pid, buildingId, $"{_pid} started outpost, count = {_player.outposts.GetOutpostEntriesUnsafe().Count}");
		}
	}

	private void VisitOutpost(EntityID peepId, EntityID buildingId)
	{
		Entity entity = _player.crew.FindVehicleAssignedToPeep(peepId).FindEntity();
		if (entity == null)
		{
			Logger.Warning("Vehicle not found for peep", peepId, _pid);
			return;
		}
		OutpostID outpost = new OutpostID(buildingId);
		if (!_player.outposts.CanCrewCollectFromOutpost(entity, outpost))
		{
			_player.finances.GetMoney(entity);
			MoneyStatus moneyStatus = _player.outposts.FindOutpostCollectionStatus(outpost);
			if (moneyStatus == null)
			{
				_ = (Fixnum)0;
			}
			else
			{
				_ = moneyStatus.Delta;
			}
		}
		else
		{
			_player.outposts.DoCollectFromOutpost(entity, outpost);
		}
	}

	private void ExecuteStealOutpost(EntityID peepId, EntityID buildingId)
	{
		VisitState visitState = new VisitState(CrewAssignment.EMPTY, BuildingUtil.FindDataForBuilding(buildingId.FindEntity()), Game.ctx.clock.Now, _pid);
		List<EntityID> list = BoardUtil.RivalBlocking(visitState);
		if (list.Count != 0)
		{
			if (visitState.GetBldgNode().owner.pid == PlayerID.HumanPlayer)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_START, TickerTitle.DEFAULT, Loc.Get("ui.tickers.outpost.steal-protected"));
				foreach (EntityID item in list)
				{
					item.FindEntity()?.components.agent.IncrementStat(CrewStats.FrontsProtected, 1);
				}
			}
			TryAttackOnStealFail(peepId, buildingId);
			return;
		}
		Entity entity = buildingId.FindEntity();
		PlayerID outpost = entity.data.building.outpost;
		if (outpost.IsNotAnyPlayer || outpost == _pid)
		{
			return;
		}
		OutpostID outpostID = new OutpostID(buildingId);
		PlayerOutposts outposts = outpost.FindPlayer().outposts;
		if (outposts.GetOutpostEntryUnsafe(outpostID) != null)
		{
			outposts.RemoveOutpost(outpostID, PlayerOutposts.RemovalReason.Stolen, _pid, peepId, removeMeAsNodeOwner: true);
			if (outpost.IsHumanPlayer)
			{
				Game.ctx.vfx.PlayOneShotPFXOverBuilding(PFXType.LoseTerritoryFX, entity, outpost, 3f);
			}
			AILog.LogMilestone(_pid, buildingId, $"{_pid} stole outpost from {outpost}: {entity}");
			PlayerSocial.DebugLogAIHistory(_pid, outpost, entity, entity?.components?.board?.GetNode(), "ai", "stole-targets-front");
			Fixnum fixnum = _def.stealingOutpost.cooldownDayzAfterStolen.Evaluate(new ModQuery(_pid));
			_data.nextStealCheck = Game.ctx.clock.Now.IncrementDays((int)fixnum);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.GangWarAction, _pid.FindPlayer().crew.GetCrewForPlayerPeep().peepId, _pid, outpost));
		}
	}

	private void TryAttackOnStealFail(EntityID peepId, EntityID buildingId)
	{
		AttackAdvisor attack = _player.ai.attack;
		NodeID nodeID = buildingId.FindEntity().components.board.GetNodeID();
		Entity entity = peepId.FindEntity();
		Entity entity2 = attack.FindFirstAggroTargetAt(nodeID.FindNode(), nodeID.FindNode());
		if (attack.CanAttackForAggro(entity2))
		{
			Game.ctx.simman.combat.PerformAICombat(entity.components.agent.FindCrewAssignment(), entity2.components.agent.FindCrewAssignment());
		}
	}

	private void UpdateExpiredTreaties()
	{
		SimTime now = Game.ctx.clock.Now;
		for (int num = _data.nonExpansionTreaties.Count() - 1; num >= 0; num--)
		{
			TerritoryAdvisorData.NonExpansionTreaty nonExpansionTreaty = _data.nonExpansionTreaties[num];
			if (nonExpansionTreaty.endOfTreaty < now)
			{
				EndExpansionTreatySymmetric(_pid, nonExpansionTreaty.pid);
			}
		}
		for (int num2 = _data.outpostAgreements.Count() - 1; num2 >= 0; num2--)
		{
			TerritoryAdvisorData.OutpostAgreementEntry outpostAgreementEntry = _data.outpostAgreements[num2];
			if (outpostAgreementEntry.agreedEnd < now)
			{
				EndOutpostStealAgreementSymmetric(_pid, outpostAgreementEntry.with);
			}
		}
	}

	public void TryRequestTreatyWith(PlayerID pid)
	{
		if (CanAskForExpansionTreaty(pid))
		{
			MaybeAskForExpansionTreaty(pid);
		}
	}

	public void TryRequestOutpostAgreementWith(PlayerID pid)
	{
		if (CanAskForOutpostStealAgreementWith(pid))
		{
			MaybeAskForOutpostStealAgreementWith(pid);
		}
	}

	public (Fixnum lengthDays, Fixnum acceptCost, Fixnum acceptTruceProb) CalculateForAcceptingExpansionTreaty(PlayerID sender, PlayerID recipient)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAcceptingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAcceptingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAcceptingGangCooperation.Item1, evaluatorAndTargetForAcceptingGangCooperation.Item2, evaluatorAndTargetForAcceptingGangCooperation.Item3);
		return (lengthDays: _expansionTreatyDef.lengthDayz.Evaluate(query), acceptCost: _expansionTreatyDef.acceptCost.Evaluate(query), acceptTruceProb: _expansionTreatyDef.acceptTreatyProb.Evaluate(query));
	}

	private (Fixnum askForTreatyProb, Fixnum askForTreatyCooldowndays) CalculateForAskingExpansionTreaty(PlayerID sender, PlayerID recipient)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAskingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAskingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAskingGangCooperation.Item1, evaluatorAndTargetForAskingGangCooperation.Item2, evaluatorAndTargetForAskingGangCooperation.Item3);
		return (askForTreatyProb: _expansionTreatyDef.askForTreatyProb.Evaluate(query), askForTreatyCooldowndays: _expansionTreatyDef.askCooldownDayz.Evaluate(query));
	}

	public static void StartExpansionTreatySymmetric(PlayerID askingPlayer, EntityID askingPeep, PlayerID agreeingPlayer, int days)
	{
		SimTime end = Game.ctx.clock.Now.IncrementDays(days);
		StartExpansionTreatyOneWay(askingPlayer, agreeingPlayer, end, askingPeep);
		StartExpansionTreatyOneWay(agreeingPlayer, askingPlayer, end, askingPeep);
	}

	public static void StartExpansionTreatyOneWay(PlayerID from, PlayerID to, SimTime end, EntityID crewpeep)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.territory.StartExpansionTreatyOneWay(to, end, crewpeep);
		}
	}

	private void StartExpansionTreatyOneWay(PlayerID other, SimTime end, EntityID crewpeep)
	{
		_data.nonExpansionTreaties.Add(new TerritoryAdvisorData.NonExpansionTreaty(other, end));
		_player.social.GetRelationshipFromPlayerTo(other)?.AddBuff(BuffConstants.RELBUFF_GANG_AGREEMENT, crewpeep);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Expansion treaty with {other} until {end}");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "expansion-treaty-started");
		if (!other.IsHumanPlayer)
		{
			return;
		}
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_AGREEMENT_START, TickerTitle.GANG_AGREEMENT_START, Loc.Get("ui.tickers.expansiontreaty.start", "groupname", _player.social.FindPlayerGroupNameColorized(), "data", Loc.FormatDateLong(end)), _player.territory.GetHeadquartersNode().id);
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			CancelAnyOutpostStarts(item.peepId);
		}
		void CancelAnyOutpostStarts(EntityID peepId)
		{
			if (_player.commands.PeepHasTask(peepId) && _player.commands.EnumerateCommands(peepId).Any((Command c) => IsOutpostCommand(c)))
			{
				_player.commands.FlushQueue(peepId, cancelActive: true);
			}
		}
		static bool IsOutpostCommand(Command c)
		{
			return c is AICommandAtOutpost;
		}
	}

	private bool CanAskForExpansionTreaty(PlayerID pid)
	{
		(bool, bool) aggroAndTruce = _player.ai.combat.GetAggroAndTruce(pid);
		if (aggroAndTruce.Item1 && !aggroAndTruce.Item2)
		{
			return false;
		}
		if (HasExpansionTreatyWith(pid))
		{
			return false;
		}
		if (_expansionTreatyDef == null)
		{
			return false;
		}
		PlayerInfo playerInfo = pid.FindPlayer();
		if (playerInfo.IsAnyAIPlayer)
		{
			TerritoryAdvisor territoryAdvisor = playerInfo.ai?.territory;
			if (territoryAdvisor == null)
			{
				return false;
			}
			(bool, bool) aggroAndTruce2 = _player.ai.combat.GetAggroAndTruce(pid);
			if (aggroAndTruce2.Item1 && !aggroAndTruce2.Item2)
			{
				return false;
			}
			if (territoryAdvisor._expansionTreatyDef == null)
			{
				return false;
			}
		}
		return true;
	}

	private void MaybeAskForExpansionTreaty(PlayerID other)
	{
		bool num = RandomExtensions.CheckProbability(probability: CalculateForAskingExpansionTreaty(_pid, other).askForTreatyProb, rng: _data.rng);
		if (num && other.IsHumanPlayer && !_player.ai.social.HasConvoInitiative)
		{
			_player.ai.social.StartConvoInitiative(other, ConvoInitiative.Topic.ExpansionHalt, EntityID.INVALID);
		}
		if (!num || !other.IsAIPlayer)
		{
			return;
		}
		SocialAdvisor socialAdvisor = other.FindPlayer()?.ai?.social;
		if (socialAdvisor != null)
		{
			var (flag, days) = socialAdvisor.ComputeRequestParametersForAIAskingUs(_pid, ConvoInitiative.Topic.TruceRequest);
			if (flag)
			{
				StartExpansionTreatySymmetric(_pid, _player.social.PlayerPeepId, other, days);
			}
		}
	}

	public static void EndExpansionTreatySymmetric(PlayerID askingPlayer, PlayerID agreeingPlayer)
	{
		EndExpansionTreatyOneWay(askingPlayer, agreeingPlayer);
		EndExpansionTreatyOneWay(agreeingPlayer, askingPlayer);
	}

	private static void EndExpansionTreatyOneWay(PlayerID from, PlayerID to)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.territory.EndExpansionTreatyOneWay(to);
		}
	}

	private void EndExpansionTreatyOneWay(PlayerID other)
	{
		_data.nonExpansionTreaties.Remove(GetExpansionTreatyWith(other));
		_player.social.GetRelationshipFromPlayerTo(other)?.RemoveBuff(BuffConstants.RELBUFF_GANG_AGREEMENT);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Treat with {other} ENDED");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "expansion-treat-ended");
		if (other.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_TRUCE_END, TickerTitle.GANG_TRUCE_END, Loc.Get("ui.tickers.expansiontreaty.end", "groupname", _player.social.FindPlayerGroupNameColorized()), _player.territory.GetHeadquartersNode().id);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	internal void ProcessExpansionTreatyBreakingActionBy(PlayerID attacker)
	{
		bool num = HasExpansionTreatyWith(attacker);
		_ = attacker.IsHumanPlayer;
		if (num)
		{
			EndExpansionTreatySymmetric(attacker, _pid);
			_player.ai.social.RememberSocialActionOnMe(SocialConstants.GANG_BROKE_AGREEMENT, attacker);
		}
	}

	public bool HasExpansionTreatyWith(PlayerID other)
	{
		return _data.nonExpansionTreaties.Select((TerritoryAdvisorData.NonExpansionTreaty x) => x.pid).Contains(other);
	}

	public TerritoryAdvisorData.NonExpansionTreaty GetExpansionTreatyWith(PlayerID other)
	{
		return _data.nonExpansionTreaties.Find((TerritoryAdvisorData.NonExpansionTreaty x) => x.pid == other);
	}

	public static void StartOutpostStealAgreementSymmetric(PlayerID askingPlayer, EntityID askingPeep, PlayerID agreeingPlayer, int days)
	{
		SimTime end = Game.ctx.clock.Now.IncrementDays(days);
		StartOutpostStealAgreementOneWay(askingPlayer, agreeingPlayer, end, askingPeep);
		StartOutpostStealAgreementOneWay(agreeingPlayer, askingPlayer, end, askingPeep);
	}

	private static void StartOutpostStealAgreementOneWay(PlayerID from, PlayerID to, SimTime end, EntityID crewpeep)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.territory.StartOutpostStealAgreementOneWay(to, end, crewpeep);
		}
	}

	private void StartOutpostStealAgreementOneWay(PlayerID other, SimTime end, EntityID crewpeep)
	{
		_data.outpostAgreements.Add(new TerritoryAdvisorData.OutpostAgreementEntry(other, end));
		_player.social.GetRelationshipFromPlayerTo(other)?.AddBuff(BuffConstants.RELBUFF_GANG_AGREEMENT, crewpeep);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Outpost agreement with {other} until {end}");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "outpost-agreement");
		if (other.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_AGREEMENT_START, TickerTitle.GANG_AGREEMENT_START, Loc.Get("ui.tickers.outpost-steal-agreement.start", "groupname", _player.social.FindPlayerGroupNameColorized(), "date", Loc.FormatDateLong(end)), _player.territory.GetHeadquartersNode().id);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	public static void EndOutpostStealAgreementSymmetric(PlayerID askingPlayer, PlayerID agreeingPlayer)
	{
		EndOutpostStealAgreementOneWay(askingPlayer, agreeingPlayer);
		EndOutpostStealAgreementOneWay(agreeingPlayer, askingPlayer);
	}

	private static void EndOutpostStealAgreementOneWay(PlayerID from, PlayerID to)
	{
		if (from.IsAIPlayer)
		{
			from.FindPlayer().ai.territory.EndOutpostStealAgreementOneWay(to);
		}
	}

	private void EndOutpostStealAgreementOneWay(PlayerID other)
	{
		_data.outpostAgreements.Remove(GetOutpostAgreementWith(other));
		_player.social.GetRelationshipFromPlayerTo(other)?.RemoveBuff(BuffConstants.RELBUFF_GANG_AGREEMENT);
		AILog.LogMilestone(_pid, EntityID.INVALID, $"Outpost agreement with {other} ENDED");
		PlayerSocial.DebugLogAIHistory(_pid, other, null, null, "ai", "outpost-agreement");
		if (other.IsHumanPlayer)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_TRUCE_END, TickerTitle.GANG_TRUCE_END, Loc.Get("ui.tickers.outpost-steal-agreement.end", "groupname", _player.social.FindPlayerGroupNameColorized()), _player.territory.GetHeadquartersNode().id);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerAggroChanged, EntityID.INVALID, _pid));
		}
	}

	internal void ProcessOutpostStealAgreementBreakingActionBy(PlayerID attacker)
	{
		if (HasOutpostAgreementWith(attacker))
		{
			EndOutpostStealAgreementSymmetric(attacker, _pid);
			_player.ai.social.RememberSocialActionOnMe(SocialConstants.GANG_BROKE_AGREEMENT, attacker);
		}
	}

	private bool CanAskForOutpostStealAgreementWith(PlayerID target)
	{
		if (_outpostAgreementDef == null || _outpostAgreementDef.lengthDayz == null)
		{
			return false;
		}
		(bool, bool) aggroAndTruce = _player.ai.combat.GetAggroAndTruce(target);
		if (aggroAndTruce.Item1 && !aggroAndTruce.Item2)
		{
			return false;
		}
		if (HasOutpostAgreementWith(target))
		{
			return false;
		}
		return true;
	}

	private void MaybeAskForOutpostStealAgreementWith(PlayerID target)
	{
		var (minTerritorySize, probability) = CalculateForAskingOutpostAgreement(_pid, target);
		int num;
		if (DoesTerritoryMeetMinSize(minTerritorySize))
		{
			num = (_data.rng.CheckProbability(probability) ? 1 : 0);
			if (num != 0 && target.IsHumanPlayer && !_player.ai.social.HasConvoInitiative)
			{
				_player.ai.social.StartConvoInitiative(target, ConvoInitiative.Topic.StolenOutpost, target.FindPlayer().social.GetPlayerPeep().Id);
			}
		}
		else
		{
			num = 0;
		}
		if (num == 0 || !target.IsAIPlayer)
		{
			return;
		}
		SocialAdvisor socialAdvisor = target.FindPlayer()?.ai?.social;
		if (socialAdvisor != null)
		{
			var (flag, days) = socialAdvisor.ComputeRequestParametersForAIAskingUs(_pid, ConvoInitiative.Topic.TruceRequest);
			if (flag)
			{
				StartOutpostStealAgreementSymmetric(_pid, _player.social.PlayerPeepId, target, days);
			}
		}
		bool DoesTerritoryMeetMinSize(Fixnum fixnum)
		{
			return target.FindPlayer().territory.OwnedNodeCount >= fixnum;
		}
	}

	public bool HasOutpostAgreementWith(PlayerID with)
	{
		return GetOutpostAgreementWith(with) != null;
	}

	public TerritoryAdvisorData.OutpostAgreementEntry GetOutpostAgreementWith(PlayerID with)
	{
		return _data.outpostAgreements.Find((TerritoryAdvisorData.OutpostAgreementEntry x) => x.with == with);
	}

	public (Fixnum lengthDays, Fixnum acceptCost, Fixnum acceptTruceProb) CalculateForAcceptingOutpostAgreement(PlayerID sender, PlayerID recipient)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAcceptingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAcceptingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAcceptingGangCooperation.Item1, evaluatorAndTargetForAcceptingGangCooperation.Item2, evaluatorAndTargetForAcceptingGangCooperation.Item3);
		return (lengthDays: _outpostAgreementDef.lengthDayz.Evaluate(query), acceptCost: _outpostAgreementDef.acceptCost.Evaluate(query), acceptTruceProb: _outpostAgreementDef.acceptOutpostAgreementProb.Evaluate(query));
	}

	private (Fixnum askForTruceCrewDelta, Fixnum askForTruceProb) CalculateForAskingOutpostAgreement(PlayerID sender, PlayerID recipient)
	{
		(PlayerID, EntityID, EntityID) evaluatorAndTargetForAskingGangCooperation = PlayerSocial.GetEvaluatorAndTargetForAskingGangCooperation(sender, recipient);
		ModQuery query = new ModQuery(evaluatorAndTargetForAskingGangCooperation.Item1, evaluatorAndTargetForAskingGangCooperation.Item2, evaluatorAndTargetForAskingGangCooperation.Item3);
		return (askForTruceCrewDelta: _outpostAgreementDef.askForOutpostAgreementMinTerritorySize.Evaluate(query), askForTruceProb: _outpostAgreementDef.askForOutpostAgreementProb.Evaluate(query));
	}

	private void OnTerritoryChanged(SessionEvent sev)
	{
		if (!(sev.pid != _manager.PID) && !_data.startOutpost.IsNotValid && _data.startOutpost.FindEntity().components.board.GetNode().owner.Is(_manager.PID))
		{
			_data.startOutpost = EntityID.INVALID;
		}
	}

	public int GetDistanceForOutpostAgreement()
	{
		return _def.expansionTreaty.expansionTreatyDistance;
	}
}

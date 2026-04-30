using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public class AttackAdvisor : AIAdvisor
{
	private struct CrewAndTarget
	{
		public Entity crew;

		public Entity target;
	}

	private AttackAdvisorData _data;

	private AttackAdvisorConfig _def;

	private readonly ModValue EveryMonthOrSo = new ModValue
	{
		value = 30
	};

	public AttackAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = Game.serv.globals.settings.npc.FindAdvisorConfig<AttackAdvisorConfig>(def.attack);
		_data = _manager.Data.attack;
		if (_data.nextDropCheck.IsMinDate)
		{
			int deltaDays = _data.rng.Generate(0, 30);
			_data.nextDropCheck = Game.ctx.clock.Now.IncrementDays(deltaDays);
		}
	}

	public override void OnTurnUpdate()
	{
		TryStartCoordinatedAttack();
		CallWithCooldown(TryWeaponDrops, ref _data.nextDropCheck, EveryMonthOrSo, EveryMonthOrSo);
		if (_def.buildingAttack != null)
		{
			CallWithCooldown(TryPickBuilding, ref _data.nextBuildingCheck, _def.buildingAttack.firstCheckDayz, _def.buildingAttack.checkDayz);
		}
		if (_def.forcedClosure != null)
		{
			CallWithCooldown(TryForceClosure, ref _data.nextClosureCheck, _def.forcedClosure.firstCheckDayz, _def.forcedClosure.checkDayz);
		}
		if (_def.sellOutToFeds != null)
		{
			CallWithCooldown(TrySellOutToFeds, ref _data.nextSellOutToFedsCheck, _def.sellOutToFeds.firstCheckDayz, _def.sellOutToFeds.checkDayz);
		}
		if (_def.coordinatedAttack != null)
		{
			CallWithCooldown(TryPickCoord, ref _data.nextCoordCheck, _def.coordinatedAttack.firstCheckDayz, _def.coordinatedAttack.checkDayz);
		}
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		if (_data.nextCoordTarget.IsValid)
		{
			int maxTurns = 3;
			CoordinatedAttackTarget target = _data.GetAndClear(ref _data.nextCoordTarget);
			List<CrewAssignment> list = FindCrewForCoordAttackIfSufficient();
			if (list.Count > 0)
			{
				_data.coordState = new CoordinatedAttackState(target.enemy, target.rallyPoint, maxTurns)
				{
					attackingCrew = list.Select((CrewAssignment crew) => crew.peepId).ToList()
				};
				List<AdvisorRequest> list2 = list.Select((CrewAssignment crew) => new AdvisorRequest(this, ScriptNames.WAIT_AT_RALLY_POINT, AdvisorRequest.Priority.AttackAggroCoordinated, new Deictics
				{
					targetBuilding = target.rallyPoint,
					number = maxTurns + 1
				})).ToList();
				foreach (AdvisorRequest item in list2)
				{
					AILog.LogAIDecision(_pid, this, $"Sending {item.assignedTo} to gang up on {item.pid}");
				}
				results.AddRange(list2);
			}
		}
		if (_data.nextBuildingTarget.IsValid)
		{
			GenericAttackTarget andClear = _data.GetAndClear(ref _data.nextBuildingTarget);
			results.Add(new AdvisorRequest(this, ScriptNames.ATTACK_BUILDING, AdvisorRequest.Priority.AttackAggroBuilding, new Deictics
			{
				targetBuilding = andClear.building,
				targetPlayer = andClear.enemy
			}));
		}
		if (_data.nextClosureTarget.IsValid)
		{
			GenericAttackTarget andClear2 = _data.GetAndClear(ref _data.nextClosureTarget);
			results.Add(new AdvisorRequest(this, ScriptNames.FORCE_CLOSE_BUSINESS, AdvisorRequest.Priority.AttackAggroBuilding, new Deictics
			{
				targetBuilding = andClear2.building,
				targetPlayer = andClear2.enemy
			}));
		}
		if (!_player.ai.combat.IsAggroOnAnybody())
		{
			return;
		}
		List<AdvisorRequest> list3 = (from data in FindNearbyAggroTargets()
			select new AdvisorRequest(this, ScriptNames.ATTACK_TARGET, data.crew.Id, AdvisorRequest.Priority.AttackAggroTarget, new Deictics
			{
				targetPeep = data.target.Id
			})).ToList();
		foreach (AdvisorRequest item2 in list3)
		{
			AILog.LogAIDecision(_pid, this, "Attacking " + item2.variables.targetPeep.DebugString);
		}
		results.AddRange(list3);
	}

	private void TryStartCoordinatedAttack()
	{
		if (_data.coordState != null)
		{
			bool flag = _data.coordState.attackingCrew.All((EntityID peepId) => AtRallyPoint(peepId));
			bool flag2 = _data.coordState.rallyExpires <= Game.ctx.clock.Now;
			if (flag || flag2)
			{
				ExecuteOrCancelCoordinatedAttack(flag);
				_data.coordState = null;
			}
		}
		bool AtRallyPoint(EntityID peepId)
		{
			return _data.coordState.rallyPointNodeId == peepId.FindEntity().data.agent.nid;
		}
		void ExecuteOrCancelCoordinatedAttack(bool sendToAttack)
		{
			EntityID entityID = EntityID.INVALID;
			if (sendToAttack)
			{
				entityID = FindTargetEnemyPeep(_data.coordState.targetPlayer, _data.coordState.rallyPointNodeId);
			}
			if (entityID.IsValid)
			{
				AILog.LogAIDecision(_pid, this, $"Group attack target chosen: {_data.coordState.targetPlayer} => {entityID}");
			}
			else
			{
				AILog.LogAIDecision(_pid, this, $"Group attack expired for target {_data.coordState.targetPlayer}");
			}
			if (entityID.IsValid && _data.coordState.targetPlayer.IsHumanPlayer)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_ATTACK, TickerTitle.GANG_ATTACK, Loc.Get("ui.tickers.gangattack", "groupname", _player.social.FindPlayerGroupNameColorized()), _data.coordState.rallyPointNodeId);
			}
			foreach (EntityID item in _data.coordState.attackingCrew)
			{
				_player.commands.FlushQueue(item, cancelActive: true);
				if (sendToAttack && entityID.IsValid)
				{
					ScriptDispatcher.RunScript(ScriptNames.ATTACK_TARGET, _pid, item.FindEntity(), new Deictics
					{
						targetPeep = entityID
					});
				}
			}
		}
		static EntityID FindTargetEnemyPeep(PlayerID targetPlayer, NodeID rallyPoint)
		{
			IEnumerable<CrewAssignment> living = targetPlayer.FindPlayer().crew.GetLiving();
			WorldPos pos = rallyPoint.FindNode().pos;
			EntityID result = EntityID.INVALID;
			float num = float.MaxValue;
			foreach (CrewAssignment item2 in living)
			{
				float magnitude = (item2.GetPeep().data.agent.nid.FindNode().pos - pos).Magnitude;
				if (magnitude < num)
				{
					num = magnitude;
					result = item2.peepId;
				}
			}
			return result;
		}
	}

	private bool CanOtherPlayerBeTargeted(PlayerInfo other)
	{
		if (other.IsHuman || other.IsJustGang)
		{
			return _player.ai.combat.IsAttackAllowed(other.PID);
		}
		return false;
	}

	private bool CheckOurPrereqs(Fixnum minCrew, Fixnum prob)
	{
		if (_player.crew.LivingCrewCount < minCrew)
		{
			return false;
		}
		if (!_data.rng.CheckProbability(prob))
		{
			return false;
		}
		return true;
	}

	private bool CheckEnemyPrereqs(PlayerInfo other, Fixnum maxRel)
	{
		return other.social.EvaluateRelationshipFromPlayerTo(_pid) <= maxRel;
	}

	private void TryPickBuilding()
	{
		ModQuery q = new ModQuery(_pid, _player.territory.GetHeadquartersNode().id);
		AttackAdvisorConfig.GenericAttack def = _def.buildingAttack;
		Fixnum maxRel = def.maxRel.Evaluate(q);
		Fixnum minCrew = def.minCrewSize.Evaluate(q);
		Fixnum prob = def.checkProbability.Evaluate(q);
		if (!CheckOurPrereqs(minCrew, prob))
		{
			return;
		}
		using (ListPool<GenericAttackTarget>.PooledBlockList pooledBlockList = ListPool<GenericAttackTarget>.Allocate())
		{
			foreach (PlayerInfo item in Game.ctx.players.all)
			{
				FindBuildingTargets(item, pooledBlockList);
			}
			_data.nextBuildingTarget = _data.rng.PickElementOrDefault(pooledBlockList);
			if (_data.nextBuildingTarget.IsValid)
			{
				AILog.LogAIDecision(_pid, this, $"Attacking building {_data.nextBuildingTarget.building} ({_data.nextBuildingTarget.enemy})");
			}
		}
		void FindBuildingTargets(PlayerInfo other, List<GenericAttackTarget> results)
		{
			if (!CanOtherPlayerBeTargeted(other) || !CheckEnemyPrereqs(other, maxRel))
			{
				return;
			}
			Fixnum fixnum = def.maxDistance.Evaluate(q);
			WorldPos pos = _player.territory.GetHeadquartersNode().pos;
			foreach (EntityID item2 in other.territory.GetAllControlledBuildingsUnsafe())
			{
				Entity entity = item2.FindEntity();
				if (!entity.components.building.IsSafehouse && !((Fixnum)(entity.data.board.worldpos - pos).Magnitude > fixnum))
				{
					results.Add(new GenericAttackTarget
					{
						building = entity.Id,
						enemy = other.PID
					});
				}
			}
		}
	}

	private void TryForceClosure()
	{
		ModQuery query = new ModQuery(_pid, _player.territory.GetHeadquartersNode().id);
		AttackAdvisorConfig.GenericAttack forcedClosure = _def.forcedClosure;
		Fixnum maxRel = forcedClosure.maxRel.Evaluate(query);
		Fixnum minCrew = forcedClosure.minCrewSize.Evaluate(query);
		Fixnum prob = forcedClosure.checkProbability.Evaluate(query);
		if (!CheckOurPrereqs(minCrew, prob))
		{
			return;
		}
		using (ListPool<PlayerID>.PooledBlockList pooledBlockList = ListPool<PlayerID>.Allocate())
		{
			PopulateEnemies(pooledBlockList);
			if (pooledBlockList.Count == 0)
			{
				return;
			}
			using ListPool<GenericAttackTarget>.PooledBlockList pooledBlockList2 = ListPool<GenericAttackTarget>.Allocate();
			PopulateTargets(pooledBlockList, pooledBlockList2);
			_data.nextClosureTarget = _data.rng.PickElementOrDefault(pooledBlockList2);
			if (_data.nextClosureTarget.IsValid)
			{
				AILog.LogAIDecision(_pid, this, $"Forcing closure {_data.nextBuildingTarget.building} ({_data.nextBuildingTarget.enemy})");
			}
		}
		void FilterBusinessesThatTradeWithEnemies(List<Entity> bizzes, List<PlayerID> enemies, List<GenericAttackTarget> targets)
		{
			foreach (Entity bizze in bizzes)
			{
				BizComponent biz = bizze.components.biz;
				Entity entity = null;
				BizComponent.TradeRestrictions? tradeRestrictions = null;
				foreach (PlayerID enemy in enemies)
				{
					if (biz.HasAnyTradeHistory(enemy))
					{
						tradeRestrictions = tradeRestrictions ?? biz.FindTradeRestrictions(_pid);
						if (!tradeRestrictions.Value.IsLocked)
						{
							entity = entity ?? BuildingUtil.FindBuildingForBiz(bizze);
							targets.Add(new GenericAttackTarget
							{
								building = entity.Id,
								enemy = enemy
							});
						}
					}
				}
			}
		}
		void PopulateEnemies(List<PlayerID> enemies)
		{
			foreach (PlayerInfo item in Game.ctx.players.all)
			{
				if (CanOtherPlayerBeTargeted(item) && CheckEnemyPrereqs(item, maxRel))
				{
					enemies.Add(item.PID);
				}
			}
		}
		void PopulateTargets(List<PlayerID> enemies, List<GenericAttackTarget> targets)
		{
			WorldPos pos = _player.territory.GetHeadquartersNode().pos;
			Fixnum fixnum = _def.forcedClosure.maxDistance.Evaluate(_pid);
			using ListPool<Entity>.PooledBlockList bizzes = ListPool<Entity>.Allocate();
			PopulateTradingBusinesses(Game.ctx.board.nodes.FindAndSortNodesInRadius(pos, (float)fixnum, sort: false), bizzes);
			FilterBusinessesThatTradeWithEnemies(bizzes, enemies, targets);
		}
		static void PopulateTradingBusinesses(List<Node> nodes, List<Entity> bizzes)
		{
			foreach (Node node in nodes)
			{
				foreach (EntityID item2 in node.interesting)
				{
					Entity entity = BuildingUtil.FindBizForBuilding(item2.FindEntity());
					if (entity?.components.biz?.HasAnyTradeHistory() == true)
					{
						bizzes.Add(entity);
					}
				}
			}
		}
	}

	private void TrySellOutToFeds()
	{
		ModQuery query = new ModQuery(_pid, _player.territory.GetHeadquartersNode().id);
		AttackAdvisorConfig.GenericAttack sellOutToFeds = _def.sellOutToFeds;
		Fixnum maxRel = sellOutToFeds.maxRel.Evaluate(query);
		Fixnum minCrew = sellOutToFeds.minCrewSize.Evaluate(query);
		Fixnum prob = sellOutToFeds.checkProbability.Evaluate(query);
		if (!CheckOurPrereqs(minCrew, prob))
		{
			return;
		}
		using (ListPool<PlayerID>.PooledBlockList pooledBlockList = ListPool<PlayerID>.Allocate())
		{
			foreach (PlayerInfo item in Game.ctx.players.all)
			{
				FindCoordTargets(item, pooledBlockList);
			}
			if (pooledBlockList.Count != 0)
			{
				PlayerID target = _data.rng.PickElement(pooledBlockList);
				if (target.IsAnyPlayer)
				{
					AskCopsToSendFedsTo(target);
				}
			}
		}
		static bool AlreadyHasCrewInJailOrPrison(PlayerInfo other)
		{
			IEnumerable<ArrestEntry> source = Game.ctx.simman.cops.FindAllArrestsFor(other.PID);
			IEnumerable<ImprisonedEntry> source2 = Game.ctx.simman.cops.FindAllImprisonedFor(other.PID);
			if (!source.Any())
			{
				return source2.Any();
			}
			return true;
		}
		void AskCopsToSendFedsTo(PlayerID playerID)
		{
			PlayerInfo playerInfo = CopUtil.FindPrecinctOrNull(_player.territory.GetHeadquartersNode());
			if (playerInfo != null && playerInfo.ai.precinct.GetFedHintStatus().available)
			{
				playerInfo.ai.precinct.StartFedHintAI(playerID, 1f);
				AILog.LogAIDecision(_pid, this, $"Asking {playerInfo.PID} to send feds to {playerID}");
			}
		}
		void FindCoordTargets(PlayerInfo other, List<PlayerID> results)
		{
			if (CanOtherPlayerBeTargeted(other) && CheckEnemyPrereqs(other, maxRel) && !AlreadyHasCrewInJailOrPrison(other))
			{
				results.Add(other.PID);
			}
		}
	}

	private void TryPickCoord()
	{
		ModQuery q = new ModQuery(_pid, _player.territory.GetHeadquartersNode().id);
		AttackAdvisorConfig.CoordinatedAttack def = _def.coordinatedAttack;
		Fixnum maxRel = def.maxRel.Evaluate(q);
		Fixnum minCrew = def.minCrewSize.Evaluate(q);
		Fixnum prob = def.checkProbability.Evaluate(q);
		if (!CheckOurPrereqs(minCrew, prob))
		{
			return;
		}
		using (ListPool<CoordinatedAttackTarget>.PooledBlockList pooledBlockList = ListPool<CoordinatedAttackTarget>.Allocate())
		{
			foreach (PlayerInfo item in Game.ctx.players.all)
			{
				FindCoordTargets(item, pooledBlockList);
			}
			_data.nextCoordTarget = _data.rng.PickElementOrDefault(pooledBlockList);
			if (_data.nextCoordTarget.IsValid)
			{
				AILog.LogAIDecision(_pid, this, $"Coordinated attack on {_data.nextCoordTarget.enemy} with rally at {_data.nextCoordTarget.rallyPoint}");
			}
		}
		void FindCoordTargets(PlayerInfo other, List<CoordinatedAttackTarget> results)
		{
			if (CanOtherPlayerBeTargeted(other) && CheckEnemyPrereqs(other, maxRel))
			{
				WorldPos pos = other.territory.GetHeadquartersNode().pos;
				Fixnum fixnum = def.maxDistance.Evaluate(q);
				Entity entity = null;
				foreach (EntityID item2 in _player.territory.GetAllControlledBuildingsUnsafe())
				{
					Entity entity2 = item2.FindEntity();
					Fixnum fixnum2 = (Fixnum)(entity2.data.board.worldpos - pos).Magnitude;
					if (fixnum2 < fixnum)
					{
						fixnum = fixnum2;
						entity = entity2;
					}
				}
				if (entity != null)
				{
					results.Add(new CoordinatedAttackTarget
					{
						enemy = other.PID,
						rallyPoint = entity.Id
					});
				}
			}
		}
	}

	private List<CrewAssignment> FindCrewForCoordAttackIfSufficient()
	{
		ModQuery query = new ModQuery(_pid, _player.territory.GetHeadquartersNode().id);
		Fixnum b = _def.coordinatedAttack.minCrewSize.Evaluate(query);
		Fixnum fixnum = _def.coordinatedAttack.minHealth.Evaluate(query);
		List<CrewAssignment> list = new List<CrewAssignment>();
		foreach (CrewAssignment item in _player.crew.AllCrew)
		{
			if (item.IsInVehicle && !item.IsDead && !(item.GetPeep().data.agent.health < fixnum))
			{
				list.Add(item);
			}
		}
		b = Fixnum.Max(1, b);
		if (list.Count >= b)
		{
			_data.rng.Shuffle(list);
			while (list.Count > b)
			{
				list.RemoveLast();
			}
		}
		else
		{
			list.Clear();
		}
		return list;
	}

	private void TryWeaponDrops()
	{
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			if (item.IsInVehicle)
			{
				TryWeaponDrop(item);
			}
		}
	}

	private void TryWeaponDrop(CrewAssignment c)
	{
		InventoryModule inventory = c.GetVehicle().components.modules.inventory;
		foreach (Label item in _def.GetWeaponDropsAtCurrentTime(_data.rng))
		{
			WeaponConfig weaponConfig = Game.ctx.simman.combat.FindWeaponConfig(item);
			if (weaponConfig == null)
			{
				Logger.Warning("Unknown weapon id: ", item);
			}
			else if (TryGrantOne(weaponConfig.FindResource()))
			{
				TryRemoveSurplus();
			}
		}
		bool TryGrantOne(Resource res)
		{
			if (inventory.data.Get(res).qty > 0 || inventory.HowManyResourcesCanFit(res) < 1)
			{
				return false;
			}
			inventory.data.Increment(res.resid, 1);
			AILog.LogAIDecision(_pid, this, $"Get weapon {res.resid} for {c.GetPeep().data.person.FullName}");
			return true;
		}
		void TryRemoveSurplus()
		{
			WeaponConfig fists = Game.ctx.simman.combat.GetFistsWeapon();
			List<WeaponConfig> list = Game.ctx.simman.combat.FindAllWeaponsSorted(c);
			if (list.Count > 3)
			{
				WeaponConfig weaponConfig2 = list.FindLast((WeaponConfig cfg) => cfg != fists);
				if (weaponConfig2 != null)
				{
					Resource resource = weaponConfig2.FindResource();
					inventory.data.Get(resource);
					inventory.data.Increment(resource.resid, -1);
				}
			}
		}
	}

	private IEnumerable<CrewAndTarget> FindNearbyAggroTargets()
	{
		IEnumerable<CrewAssignment> living = _player.crew.GetLiving();
		foreach (CrewAssignment item in living)
		{
			if (!item.IsInVehicle)
			{
				continue;
			}
			Entity peep = item.GetPeep();
			Node node = peep.components.agent.GetNode();
			if (node != null)
			{
				Entity entity = FindNearbyAggroTarget(peep, node);
				if (entity != null)
				{
					yield return new CrewAndTarget
					{
						crew = item.GetPeep(),
						target = entity
					};
				}
			}
		}
	}

	private Entity FindNearbyAggroTarget(Entity peep, Node node)
	{
		ModQuery query = new ModQuery(_pid, EntityID.INVALID, peep.Id, node.id);
		Fixnum fixnum = _def.opportunisticAttack.minHealth.Evaluate(query);
		if (peep.data.agent.health < fixnum)
		{
			return null;
		}
		float maxdist = (float)_def.opportunisticAttack.maxDistance.Evaluate(query);
		int maxNodes = 50;
		Entity target = null;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(node, maxNodes, delegate(Node n)
		{
			target = FindFirstAggroTargetAt(node, n);
		}, (Node n) => (n.pos - node.pos).Magnitude < maxdist, null, (Node _) => target != null, onlyBizNodes: true);
		return target;
	}

	public Entity FindFirstAggroTargetAt(Node origin, Node node)
	{
		foreach (EntityID item in Game.ctx.transit.GetAllAgentsAtNodeUnsafe(node.id))
		{
			Entity entity = item.FindEntity();
			if (CanAttackForAggro(entity))
			{
				return entity;
			}
		}
		return null;
	}

	public bool CanAttackForAggro(Entity target)
	{
		AgentData agentData = target?.data.agent;
		AgentComponent agentComponent = target?.components.agent;
		if (agentData == null)
		{
			return false;
		}
		if (agentData.pid == _pid)
		{
			return false;
		}
		if (agentData.pid.IsNotAnyPlayer)
		{
			return false;
		}
		if (_player.ai.combat.IsAttackNotAllowed(agentData.pid))
		{
			return false;
		}
		if (!target.data.person.IsAlive)
		{
			return false;
		}
		if (agentData.nid.IsNotValid)
		{
			return false;
		}
		if (!agentComponent.FindCrewAssignment().IsInVehicle)
		{
			return false;
		}
		return true;
	}
}

using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Setup;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public class BusinessAdvisor : AIAdvisor
{
	private BusinessCandidateCache _cache;

	private BusinessAdvisorConfig _def;

	private BusinessAdvisorData _data;

	private const string MSG_CONTROL = "control";

	private const string MSG_CONTROL_GAMBLING = "control-gambling";

	private const string SMALL_DEN = "gambling-den-small";

	private List<string> gambling_modules = new List<string> { "gambling-den-small" };

	public BusinessAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = FindAdvisorConfig<BusinessAdvisorConfig>(def.business);
		_data = _manager.Data.business;
		_cache = new BusinessCandidateCache(_player, _def);
		_cache.PopulateCacheAtStartup();
		Game.ctx.events.AddListener(SessionEventType.PlayerTerritoryChanged, OnPlayerTerritoryChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnPlayerControlledChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingClearControlImmediate, OnPlayerControlledChanged);
		Game.ctx.events.AddListener(SessionEventType.BuildingModuleInstalled, OnModuleInstalled);
		Game.ctx.events.AddListener(SessionEventType.BuildingDeliveryChanged, OnDeliveryChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerSkillsChangedImmediate, OnPlayerSkillsChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerScopedOutBuilding, OnPlayerScopedOutBuilding);
	}

	public override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerTerritoryChanged, OnPlayerTerritoryChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnPlayerControlledChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingClearControlImmediate, OnPlayerControlledChanged);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingModuleInstalled, OnModuleInstalled);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingDeliveryChanged, OnDeliveryChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerSkillsChangedImmediate, OnPlayerSkillsChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerScopedOutBuilding, OnPlayerScopedOutBuilding);
		base.Release();
	}

	public override void OnTurnUpdate()
	{
		CallWithCooldown(MaybeFindNextPickup, ref _data.nextPickupCheck, _def.trades.firstCheckDayz, _def.trades.cooldownDayz);
		CallWithCooldown(delegate
		{
			MaybeTakeOverBuilding();
		}, ref _data.nextBldgCheck, _def.buildings.firstCheckDayz, _def.buildings.cooldownDayz);
		CallWithCooldown(delegate
		{
			MaybeTakeOverBuilding(casinoTakeover: true);
		}, ref _data.nextCasinoCheck, _def.casinos.firstCheckDayz, _def.casinos.cooldownDayz);
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		results.AddIfNotNull(ProduceSingleTakeoverRequest());
		int num = _def.trades.reqsPerTurn.Evaluate(new ModQuery(_pid)).IntFloor();
		for (int i = 0; i < num; i++)
		{
			results.AddIfNotNull(ProduceSingleBuySellRequest());
		}
	}

	private AdvisorRequest ProduceSingleTakeoverRequest()
	{
		if (_data.nextTakeoverTarget.IsValid)
		{
			return new AdvisorRequest(this, ScriptNames.SETUP_CONTROLLED, AdvisorRequest.Priority.TakeoverBuilding, new Deictics
			{
				targetBuilding = _data.GetAndClear(ref _data.nextTakeoverTarget)
			});
		}
		if (_data.nextGamblingTakeoverTarget.IsValid)
		{
			return new AdvisorRequest(this, ScriptNames.SETUP_CONTROLLED_GAMBLING, AdvisorRequest.Priority.TakeoverBuilding, new Deictics
			{
				targetBuilding = _data.GetAndClear(ref _data.nextGamblingTakeoverTarget)
			});
		}
		return null;
	}

	private AdvisorRequest ProduceSingleBuySellRequest()
	{
		if (_data.nextPickupList.Count != 0)
		{
			BusinessAdvisorData.Pickup pickup = _data.nextPickupList.RemoveAndReturn(0);
			Label script = (pickup.item.consumed ? ScriptNames.BUY_ITEM_SCRIPT : ScriptNames.SELL_ITEM_SCRIPT);
			return new AdvisorRequest(this, script, AdvisorRequest.Priority.BuySellItem, new Deictics
			{
				targetItem = new ResourceAndQty(pickup.item.id, 0),
				targetBuilding = pickup.building,
				mySafehouse = pickup.playerBuilding
			});
		}
		return null;
	}

	private void MaybeTakeOverBuilding(bool casinoTakeover = false)
	{
		float probability = (float)(casinoTakeover ? _def.casinos.setupProbPerCheck : _def.buildings.setupProbPerCheck).Evaluate(new ModQuery(_pid));
		if (!_data.rng.CheckProbability(probability) || !ShouldExpandControlled())
		{
			return;
		}
		EntityID entityID = ((!casinoTakeover) ? FindTakeoverTarget().buildingId : FindResidenceTakeoverTarget().buildingId);
		if (entityID.IsValid)
		{
			if (casinoTakeover)
			{
				_data.nextGamblingTakeoverTarget = entityID;
			}
			else
			{
				_data.nextTakeoverTarget = entityID;
			}
			AILog.LogAIDecision(_pid, this, $"Take building at {_data.nextTakeoverTarget.FindEntity()}");
		}
	}

	private bool ShouldExpandControlled()
	{
		BusinessAdvisorConfig.BuildingsConfig buildings = _def.buildings;
		int num = _player.territory.CountControlledBuildings();
		if (num == 0)
		{
			return false;
		}
		ModQuery query = new ModQuery(_pid);
		Fixnum fixnum = (Fixnum)((float)_player.territory.OwnedNodeCount / (float)num);
		Fixnum fixnum2 = buildings.nodesPerControlledBuilding.Evaluate(query);
		return fixnum > fixnum2;
	}

	private (EntityID buildingId, Node node) FindTakeoverTarget()
	{
		List<NodeID> list = new List<NodeID>(_player.territory.FindAllNodesWithPotentialBuildings());
		if (list.Count == 0)
		{
			return (buildingId: EntityID.INVALID, node: null);
		}
		Node node = _data.rng.PickElement(list).FindNode();
		return (buildingId: node.potential, node: node);
	}

	private (EntityID buildingId, Node node) FindResidenceTakeoverTarget()
	{
		List<NodeID> allOwnedNodesUnsafe = _player.territory.GetAllOwnedNodesUnsafe();
		if (allOwnedNodesUnsafe.Count == 0)
		{
			return (buildingId: EntityID.INVALID, node: null);
		}
		Node node = _data.rng.PickElement(allOwnedNodesUnsafe).FindNode();
		List<Entity> list = new List<Entity>();
		node.FindAllBuildings(list);
		list = list.Where((Entity x) => _player.gambling.CanBecomeGamblingHouse(x)).ToList();
		if (list.Count > 0)
		{
			return (buildingId: _data.rng.PickElement(list).Id, node: node);
		}
		return (buildingId: EntityID.INVALID, node: null);
	}

	private void MaybeFindNextPickup()
	{
		float probability = (float)_def.trades.probPerCheck.Evaluate(new ModQuery(_pid));
		if (_data.rng.CheckProbability(probability))
		{
			_data.nextPickupList = FindSuitableDelivery();
		}
	}

	private List<BusinessAdvisorData.Pickup> FindSuitableDelivery()
	{
		List<BusinessAdvisorData.Pickup> list = new List<BusinessAdvisorData.Pickup>();
		if (_cache.controlled.Count == 0)
		{
			return list;
		}
		using ListPool<BusinessCandidateCache.ScopedOut>.PooledBlockList pooledBlockList = ListPool<BusinessCandidateCache.ScopedOut>.Allocate();
		_cache.FindAllScopedBuildingsToTradeWith(pooledBlockList);
		if (pooledBlockList.Count == 0)
		{
			return list;
		}
		using ListPool<BusinessAdvisorData.Pickup>.PooledBlockList pooledBlockList2 = ListPool<BusinessAdvisorData.Pickup>.Allocate();
		using (ListPool<MfgItem>.PooledBlockList pooledBlockList3 = ListPool<MfgItem>.Allocate())
		{
			foreach (BusinessCandidateCache.Controlled value in _cache.controlled.Values)
			{
				_cache.FillWithUnlockedItems(value.allItems, pooledBlockList3);
				if (pooledBlockList3.Count == 0)
				{
					continue;
				}
				foreach (MfgItem item in pooledBlockList3)
				{
					TryFindSourcesFor(item, pooledBlockList, value.building, pooledBlockList2);
				}
			}
		}
		switch (pooledBlockList2.Count)
		{
		case 1:
			list.Add(pooledBlockList2.FirstOrDefaultFast());
			break;
		default:
		{
			List<float> weights = pooledBlockList2.SelectIntoNewList((BusinessAdvisorData.Pickup p) => (!p.humanTrades) ? 1f : 10f);
			Fixnum fixnum = _def.trades.reqsPerTurn.Evaluate(new ModQuery(_pid));
			for (int num = 0; num < fixnum; num++)
			{
				list.Add(_data.rng.PickElement(pooledBlockList2, weights));
			}
			break;
		}
		case 0:
			break;
		}
		return list;
	}

	private void TryFindSourcesFor(MfgItem item, List<BusinessCandidateCache.ScopedOut> bizzes, Entity mine, List<BusinessAdvisorData.Pickup> results)
	{
		foreach (BusinessCandidateCache.ScopedOut bizze in bizzes)
		{
			foreach (MfgItem unlockedItem in bizze.unlockedItems)
			{
				if (unlockedItem.id == item.id && unlockedItem.consumed != item.consumed)
				{
					bool humanTrades = BuildingUtil.FindBizForBuilding(bizze.building).components.biz.HasAnyTradeHistory(PlayerID.HumanPlayer);
					results.Add(new BusinessAdvisorData.Pickup
					{
						building = bizze.building.Id,
						playerBuilding = mine.Id,
						item = item,
						humanTrades = humanTrades
					});
				}
			}
		}
	}

	public Fixnum FindTradeEfficiency()
	{
		return _def.trades.tradeEfficiency.Evaluate(_pid);
	}

	private void OnPlayerTerritoryChanged(SessionEvent ev)
	{
		if (ev.pid == _pid)
		{
			_cache.UpdatePlayerControlledBuildings();
		}
	}

	private void OnPlayerControlledChanged(SessionEvent ev)
	{
		if (ev.pid == _pid)
		{
			_cache.UpdatePlayerControlledBuildings();
		}
	}

	private void OnModuleInstalled(SessionEvent ev)
	{
		if (ev.pid == _pid)
		{
			_cache.UpdatePlayerControlledBuildings();
		}
	}

	private void OnPlayerSkillsChanged(SessionEvent ev)
	{
		if (ev.pid == _pid)
		{
			_cache.UpdateOnSkillUnlock();
		}
	}

	private void OnDeliveryChanged(SessionEvent ev)
	{
		if (ev.pid == _pid)
		{
			_cache.UpdateOnBuildingStateChange(ev.eid.FindEntity());
		}
	}

	private void OnPlayerScopedOutBuilding(SessionEvent ev)
	{
		if (ev.pid == _pid)
		{
			Entity building = ev.eid.FindEntity();
			_cache.UpdateOnBuildingStateChange(building);
		}
	}

	public void OnExecuteAIBuySellComplete(EntityID buildingId, EntityID peepId)
	{
		Entity entity = buildingId.FindEntity();
		NodeID nodeID = entity.components.board.GetNodeID();
		ModQuery q = new ModQuery(_pid, buildingId, peepId, nodeID);
		TryTakeTiedHouse(entity, q);
	}

	private void TryTakeTiedHouse(Entity building, ModQuery q)
	{
		Entity entity = BuildingUtil.FindBizForBuilding(building);
		var (flag, playerID) = entity.components.biz.GetTiedHouseStatus();
		if ((flag && playerID == _pid) || building.components.board.GetNode().owner.Get().IsAnyPlayer)
		{
			return;
		}
		SimTime now = Game.ctx.clock.Now;
		if (now < _data.tiedHouseCooldownExpiration)
		{
			return;
		}
		int deltaDays = _def.trades.tiedHouseCooldownDayz.Evaluate(q).IntCeiling();
		_data.tiedHouseCooldownExpiration = now.IncrementDays(deltaDays);
		float probability = (float)_def.trades.tiedHouseProbability.Evaluate(q);
		if (_data.rng.CheckProbability(probability))
		{
			if (!flag)
			{
				TakeNewTiedHouse(building, entity);
			}
			else if (playerID.IsHumanPlayer)
			{
				AskHumanForTiedHouse(building, entity, playerID);
			}
		}
	}

	private void TakeNewTiedHouse(Entity building, Entity biz)
	{
		biz.components.biz.SetTiedHouse(_pid, FindTiedHouseDurationDays(building));
		biz.components.biz.GetTiedHouseStatus();
		AILog.LogMilestone(_pid, building.Id, $"{_pid} set a new tied house at {building}");
	}

	private void AskHumanForTiedHouse(Entity building, Entity biz, PlayerID pid)
	{
		if (!_player.ai.social.HasConvoInitiative)
		{
			_player.ai.social.StartConvoInitiative(pid, ConvoInitiative.Topic.TiedHouse, building.Id);
		}
	}

	public void FinishTakingHumanTiedHouse(VisitState visit, Entity biz, int days, Price price)
	{
		PlayerID pid = visit.pid;
		Price delta = -price;
		biz.components.biz.ClearTiedHouse();
		biz.components.biz.SetTiedHouse(_pid, days);
		pid.FindPlayer().finances.DoChangeMoneyOnCrew(visit, delta, MoneyReason.Other);
	}

	private int FindTiedHouseDurationDays(Entity building)
	{
		Node node = building.components.board.GetNode();
		ModQuery query = new ModQuery(_pid, building.Id, node.id);
		return _def.trades.tiedHouseCooldownDayz.Evaluate(query).IntCeiling();
	}

	public void AddTiedHouseConvoMemory(PlayerID other, EntityID crewpeep, EntityID bizOwner)
	{
		Relationship item = _player.social.FindOrMakeRelationshipsWith(other).to;
		SocialHistoryData orCreateHistory = item.GetOrCreateHistory();
		orCreateHistory.ExpireSpecificAction(SocialConstants.HEARD_ABOUT_TIED_HOUSE_CONVO);
		orCreateHistory.InformOfSocialAction(SocialConstants.HEARD_ABOUT_TIED_HOUSE_CONVO, item, bizOwner, crewpeep, QuestUUID.EMPTY, inferred: false);
	}

	public SocialActionInfo? FindTiedHouseConvoMemory(PlayerID other)
	{
		SocialHistoryData socialHistoryData = _player.social.GetRelationshipFromPlayerTo(other)?.GetHistoryOrNull();
		if (socialHistoryData == null)
		{
			return null;
		}
		int num = socialHistoryData.IndexOf(SocialConstants.HEARD_ABOUT_TIED_HOUSE_CONVO);
		if (num < 0)
		{
			return null;
		}
		return socialHistoryData.items[num];
	}

	internal void TradeCallbackAtBusiness(EntityID buildingId, string message)
	{
		switch (message)
		{
		case "control":
			TryTakeOver(buildingId.FindEntity());
			break;
		case "control-gambling":
			TryTakeOverCasino(buildingId.FindEntity());
			break;
		default:
			Logger.Warning($"Unknown message passed to {this}: {message}");
			break;
		}
	}

	private void TryTakeOver(Entity building)
	{
		Node node = building.components.board.GetNode();
		if (_player.territory.IsOwnerOfNode(node))
		{
			CrewAssignment crew = _player.crew.FindFirstCrewAtLocation(node.id);
			if (!crew.IsNotValid)
			{
				PlayerTerritory.TakeoverData td = _player.territory.FindTakeoverDataForAI(building.Id);
				_player.territory.PerformTakeover(crew, td);
				AILog.LogMilestone(_pid, building.Id, $"{_pid} took control of {building} at {node}");
			}
		}
	}

	private void TryTakeOverCasino(Entity building)
	{
		Node node = building.components.board.GetNode();
		if (_player.territory.IsOwnerOfNode(node) && !_player.crew.FindFirstCrewAtLocation(node.id).IsNotValid && _player.gambling.CanBecomeGamblingHouse(building))
		{
			Entity aICasinoManager = CreatePlayers.CheatGenerateCrewForPlayer(_player);
			_player.territory.ScopeOutAndTakeOverResidence(building);
			_player.gambling.InstallGamblingModule(building, new Label(_data.rng.PickElement(gambling_modules)));
			building.components.residence.SetAICasinoManager(aICasinoManager);
		}
	}
}

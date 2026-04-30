using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerTerritory : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	public enum ClaimTestResult
	{
		OK,
		NotEnoughRespect,
		NotYetScoped,
		AlreadyOwnedByThisPlayer
	}

	public struct TakeoverData
	{
		public EntityID buildingId;

		public EntityID candidateId;

		public Price cost;

		public bool IsValid
		{
			get
			{
				if (buildingId.IsValid)
				{
					return candidateId.IsValid;
				}
				return false;
			}
		}

		internal TakeoverData(EntityID buildingId, EntityID candidateId, Price cost)
		{
			this.buildingId = buildingId;
			this.candidateId = candidateId;
			this.cost = cost;
		}

		public Entity FindBuilding()
		{
			return buildingId.FindEntity();
		}

		public Entity FindCandidate()
		{
			return candidateId.FindEntity();
		}
	}

	private PlayerTerritoryDisplay _display;

	private PlayerTerritoryData _territory;

	private PlayerPotentialCache _cachedPotentials;

	private HeatAndRespect _heatAndRespect;

	private const int MAX_NODES_IN_GRANT = 300;

	public PlayerColor colorInfo => _territory.color;

	public int OwnedNodeCount => _territory.ownedNodes.Count;

	public List<NodeID> OwnedNodeIds => _territory.ownedNodes;

	public SimTime LastExpansionTime => _territory.lastTerritoryExpansion;

	public bool IsSafehouseVanquished
	{
		get
		{
			if (_territory.safehouseData != null)
			{
				return _territory.safehouseData.raided;
			}
			return true;
		}
	}

	public bool IsSafehouseReadyToClear
	{
		get
		{
			if (_territory.safehouseData != null)
			{
				return _territory.safehouseData.raided;
			}
			return false;
		}
	}

	public SafehouseData SafehouseData => _territory.safehouseData;

	public EntityID Safehouse => _territory.safehouseData?.safehouse ?? EntityID.INVALID;

	public EntityID Station => _data.ai.precinct?.stationId ?? EntityID.INVALID;

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		_display = new PlayerTerritoryDisplay();
		_display.Initialize(this);
		_cachedPotentials = new PlayerPotentialCache();
		_cachedPotentials.Initialize(_player);
		_heatAndRespect = new HeatAndRespect();
		_heatAndRespect.Initialize(this);
		Game.ctx.events.AddListener(SessionEventType.SafehouseRemoved, MaybeClearSafehouseData);
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitLoadedGame, UpdateCachesAfterLoad);
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_territory = _data.territory;
		if (Game.ctx.IsSessionFromNewGame)
		{
			_territory.SetColor(this);
		}
	}

	public override void OnPreRelease()
	{
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitLoadedGame, UpdateCachesAfterLoad);
		Game.ctx.events.RemoveListener(SessionEventType.SafehouseRemoved, MaybeClearSafehouseData);
		_display.Release();
		_display = null;
		_cachedPotentials.Release();
		_cachedPotentials = null;
		base.OnPreRelease();
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		AutoScopeOutScavengeableSafehouse();
		MaybeClearRaidedSafehouse();
		ProcessDamagedBuildings();
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	private void UpdateCachesAfterLoad(SessionEvent ev)
	{
		_cachedPotentials.OnLoad();
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		Game.ctx.console.Add(this, new DebugConsoleEntry("territory", "make-all-mine", CheatAddAllTerritory));
		Game.ctx.console.Add(this, new DebugConsoleEntry("territory", "add-at-crew", CheatAddTerritoryCrew));
		Game.ctx.console.Add(this, new DebugConsoleEntry("territory", "remove-at-crew", CheatRemoveTerritoryCrew));
		Game.ctx.console.Add(this, new DebugConsoleEntry("territory", "set-building-health", CheatSetBuildingHealth));
		Game.ctx.console.Add(this, new DebugConsoleEntry("scopeout", "all", CheatScopeOutAll));
		Game.ctx.console.Add(this, new DebugConsoleEntry("scopeout", "none", CheatScopeOutNone));
		Game.ctx.console.Add(this, new DebugConsoleEntry("respect", "increment-at-crew", CheatChangeRespect));
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	internal List<EntityID> GetAllControlledBuildingsUnsafe()
	{
		return _territory.controlledBuildings;
	}

	internal List<NodeID> GetAllOwnedNodesUnsafe()
	{
		return _territory.ownedNodes;
	}

	public int CountControlledBuildings()
	{
		return _territory.controlledBuildings.Count;
	}

	public IEnumerable<Entity> FindAllControlledBuildingsWithTag(Label tag)
	{
		return from eid in _territory.controlledBuildings
			select eid.FindEntity() into e
			where e.components.building.HasTag(tag)
			select e;
	}

	public Entity FindFirstControlledBuildingWithTag(Label tag)
	{
		return FindAllControlledBuildingsWithTag(tag).FirstOrDefault();
	}

	public ClaimTestResult CanClaimOwnership(Node node)
	{
		if (IsNodeOwnedByPlayer(node, _pid))
		{
			return ClaimTestResult.AlreadyOwnedByThisPlayer;
		}
		if (node.CanBeScopedOut(_pid))
		{
			return ClaimTestResult.NotYetScoped;
		}
		if (!HasNonZeroRespect(node))
		{
			return ClaimTestResult.NotEnoughRespect;
		}
		return ClaimTestResult.OK;
	}

	public static bool IsNodeOwnedByAnyone(Node node)
	{
		return node.owner.Get().IsAnyPlayer;
	}

	public static bool IsNodeOwnedByPlayer(Node node, PlayerID pid)
	{
		return node.owner.Is(pid);
	}

	public static PlayerID GetNodeOwner(Node node)
	{
		return node.owner.Get();
	}

	public bool IsOwnerOfNode(Node node)
	{
		return node?.owner.Is(_pid) ?? false;
	}

	public bool IsOwnerOfNode(Entity building)
	{
		return IsOwnerOfNode(building.components.board.GetNode());
	}

	private void SetNodeOwner(Node node, bool external = false)
	{
		node.owner.Set(_pid, Game.ctx.clock.Now);
		UpdateNodeDataOnTerritoryChange();
		ScopeOutNodeIfBecameNewOwner(node);
		Game.ctx.events.EnqueueOnce(SessionEvent.Make(SessionEventType.PlayerTerritoryChanged, _pid));
	}

	private void ClearNodeOwner(Node node, PlayerID instigator, bool external = false)
	{
		PlayerID pid = node.owner.Get();
		_player.outposts.OnClearNodeOwner(node, instigator);
		Entity entity = FindControlledBuildingAtNode(node);
		if (entity != null)
		{
			ClearControlledAndResetBiz(entity, !external, attacked: false);
		}
		node.owner.Clear(Game.ctx.clock.Now);
		UpdateNodeDataOnTerritoryChange();
		Game.ctx.events.EnqueueOnce(SessionEvent.Make(SessionEventType.PlayerTerritoryChanged, pid));
	}

	private void UpdateNodeDataOnTerritoryChange()
	{
		_territory.ownedNodes.Clear();
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			if (item.owner.Is(_pid))
			{
				_territory.ownedNodes.Add(item.id);
			}
		}
		_cachedPotentials.OnTerritoryChanged();
	}

	public IEnumerable<Node> GetAllKnownNodesExpensive()
	{
		return from node in Game.ctx.board.nodes.GetAllNodesUnsafe()
			where node.known.Get(_pid)
			select node;
	}

	private void PopulateNodesWithControlledBuildings(List<Node> results)
	{
		foreach (EntityID controlledBuilding in _territory.controlledBuildings)
		{
			Entity entity = controlledBuilding.FindEntity();
			results.Add(entity.components.board.GetNode());
		}
	}

	public void SetLastExpansionTime(SimTime now)
	{
		_territory.lastTerritoryExpansion = now;
	}

	public int GetDaysSinceLastExpansion(SimTime now)
	{
		return now.days - _territory.lastTerritoryExpansion.days;
	}

	private void SetControlled(Entity building)
	{
		if (building != null && building.components.building != null)
		{
			building.components.building.SetControlledBy(_pid);
			_territory.controlledBuildings.Add(building.Id);
			Fixnum value = BuildingSettings.EvaluateMaxBuildingHealth(building, _pid);
			SetHealth(building, value);
		}
	}

	private void ClearControlledInternal(Entity building)
	{
		if (building != null && building.components.building != null)
		{
			building.components.building.ClearControlledBy();
			_territory.controlledBuildings.Remove(building.Id);
			building.components.building.RemoveHealth();
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerBuildingClearControlImmediate, building.Id, _pid));
		}
	}

	public bool IsControlled(Entity building)
	{
		return building.components.building.IsControlledBy(_pid);
	}

	private Entity FindControlledBuildingAtNode(Node node)
	{
		foreach (EntityID controlledBuilding in _territory.controlledBuildings)
		{
			Entity entity = controlledBuilding.FindEntity();
			if (entity.components.board.GetNodeID() == node.id)
			{
				return entity;
			}
		}
		return null;
	}

	public bool IsPlayersTiedHouse(Entity biz)
	{
		return biz.components.biz.IsTiedTo(_pid);
	}

	public void PayForNewTiedHouse(VisitState visit, Entity biz, Price price, int days)
	{
		_player.finances.DoChangeMoneyOnCrew(visit, price, MoneyReason.Other);
		biz.components.biz.SetTiedHouse(_pid, days);
	}

	public void PayToTakeOverTiedHouse(VisitState visit, Entity biz, Price price, int days)
	{
		biz.components.biz.ClearTiedHouse();
		PayForNewTiedHouse(visit, biz, price, days);
	}

	private void SetScoped(Entity building)
	{
		if (building.components.police != null && building.data.police.copPlayerId != PlayerID.System)
		{
			_player.meetings.MarkPlayersAsMutuallyMet(building.data.police.copPlayerId, introduceLeadersToCrew: true);
		}
		building.components.building.SetScopedBy(_pid, scoped: true);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerScopedOutBuilding, building.Id, _pid));
	}

	private void ClearScoped(Entity building)
	{
		building.components.building.SetScopedBy(_pid, scoped: false);
	}

	public bool AreAnyScoped(Node node)
	{
		foreach (EntityID item in node.interesting)
		{
			if (!item.IsNotValid && IsScoped(item.FindEntity()))
			{
				return true;
			}
		}
		return false;
	}

	public bool AreAllScoped(Node node)
	{
		foreach (EntityID item in node.interesting)
		{
			if (!item.IsNotValid && !IsScoped(item.FindEntity()))
			{
				return false;
			}
		}
		return true;
	}

	public bool IsScoped(Entity building)
	{
		return building.components.building.IsScopedBy(_pid);
	}

	public bool ScopeOutReserved(EntityID buildingid)
	{
		return _territory.scopeReservations.Contains(buildingid);
	}

	public bool ReserveScopeOut(EntityID buildingid)
	{
		if (_territory.scopeReservations.Contains(buildingid))
		{
			return false;
		}
		_territory.scopeReservations.Add(buildingid);
		return true;
	}

	public void UnreserveScopeOut(EntityID buildingid)
	{
		if (!_territory.scopeReservations.Remove(buildingid))
		{
			Logger.Warning("Double unreserved a building to scope out?");
		}
	}

	public void ScopeOutAndMeetOwner(Entity building, bool procgen, EntityID crew, bool setControlled)
	{
		ScopeOutBuilding(building, procgen, setControlled);
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
		if (entity != null)
		{
			_player.social.MeetBuildingOwner(entity.Id, procgen, crew);
		}
	}

	public void ScopeOutBuilding(Entity building, bool procgen, bool setControlled)
	{
		SetScoped(building);
		if (setControlled)
		{
			SetControlled(building);
		}
	}

	public void ScopeOutAndTakeOverResidence(Entity building)
	{
		ScopeOutBuilding(building, procgen: true, setControlled: true);
	}

	public Node GetHeadquartersNode(bool ignoreWarnings = false)
	{
		if (Safehouse.IsValid)
		{
			return Safehouse.FindEntity()?.components.board.GetNode();
		}
		if (_player.IsJustCop)
		{
			return Station.FindEntity()?.components.board.GetNode();
		}
		if (_player.IsJustFed)
		{
			return _player.ai?.feds?.GetFakeHeadquartersNodeID().FindNode();
		}
		return null;
	}

	public void PerformRaid(Entity safehouse)
	{
		if (SafehouseUtils.CanRaidSafehouse(safehouse, _pid))
		{
			safehouse.components.building.SafehouseOwner.FindPlayer().territory.SafehouseData.SetSafehouseForScavenging();
		}
	}

	public void RemoveGoonSafehouse()
	{
		SafehouseUtils.ClearSafehouseAndTerritory(_player, _territory.safehouseData.safehouse);
	}

	private void MaybeClearRaidedSafehouse()
	{
		if (IsSafehouseReadyToClear)
		{
			SafehouseUtils.ClearSafehouseAndTerritory(_player, Safehouse);
		}
	}

	private void MaybeClearSafehouseData(SessionEvent sev)
	{
		if (sev.pid == _pid)
		{
			_territory.safehouseData = null;
		}
	}

	private void AutoScopeOutScavengeableSafehouse()
	{
		if (!_player.IsGangOrGoon || !_player.crew.IsCrewDefeated || !Safehouse.IsValid)
		{
			return;
		}
		PlayerInfo human = Game.ctx.players.Human;
		Entity entity = Safehouse.FindEntity();
		if (!human.territory.IsScoped(entity))
		{
			Node node = entity.components?.board?.GetNode();
			if (node != null && human.meetings.IsNodeKnown(node))
			{
				human.territory.ScopeOutAndMeetOwner(entity, procgen: false, human.social.PlayerPeepId, setControlled: false);
				entity.components.building.TogglePickSuppression(value: false);
			}
		}
	}

	public void RecomputeHeatAndRespect(Node node, bool forceCurrent = false)
	{
		_heatAndRespect.RecomputeRespect(node, forceCurrent);
		_heatAndRespect.RecomputeHeat(node, forceCurrent);
	}

	public void RecomputeRespect(Node node, bool forceCurrent = false)
	{
		_heatAndRespect.RecomputeRespect(node, forceCurrent);
	}

	public void RecomputeHeat(Node node, bool forceCurrent = false)
	{
		_heatAndRespect.RecomputeHeat(node, forceCurrent);
	}

	public void AddRespectBuff(Node node, Label id, EntityID crewpeep, ModQuery query)
	{
		_heatAndRespect.AddRespectBuff(node, id, crewpeep, query);
	}

	public void AddHeatBuff(Node node, Label id, EntityID crewpeep, ModQuery query)
	{
		_heatAndRespect.AddHeatBuff(node, id, crewpeep, query);
	}

	public void RemoveRespectBuff(Node node, Label id)
	{
		_heatAndRespect.RemoveRespectBuff(node, id);
	}

	public void RemoveHeatBuff(Node node, Label id)
	{
		_heatAndRespect.RemoveHeatBuff(node, id);
	}

	public bool ContainsRespectBuff(Node node, Label id)
	{
		return _heatAndRespect.ContainsRespectBuff(node, id);
	}

	public bool ContainsHeatBuff(Node node, Label id)
	{
		return _heatAndRespect.ContainsHeatBuff(node, id);
	}

	public void ProduceRespectFromNeighbors(List<(Node, Fixnum)> total, Fixnum vIn, Fixnum vOut)
	{
		_heatAndRespect.RespectFromNeighbors(total, vIn, vOut, _territory.ownedNodes);
	}

	public static void ClearNodeOwner(Node node, PlayerID owner, PlayerID instigator)
	{
		owner.FindPlayer().territory.ClearNodeOwner(node, instigator, external: true);
	}

	public static void SetNodeOwner(Node node, PlayerID owner)
	{
		owner.FindPlayer().territory.SetNodeOwner(node, external: true);
	}

	private bool HasNonZeroRespect(Node node)
	{
		Respect orNull = node.respect.GetOrNull(_pid);
		if (orNull == null)
		{
			return false;
		}
		ModQuery query = new ModQuery(_pid, node);
		return orNull.CalculateBaseValue(query) > 0;
	}

	public IEnumerable<NodeID> FindAllNodesWithPotentialBuildings()
	{
		return _cachedPotentials.GetElementsUnsafe();
	}

	public TakeoverData FindTakeoverData(VisitState visit)
	{
		return FindTakeoverData(visit, EntityID.INVALID, EntityID.INVALID);
	}

	public TakeoverData FindTakeoverData(VisitState visit, EntityID forceBuilding, EntityID forceManager)
	{
		EntityID buildingId = (forceBuilding.IsValid ? forceBuilding : FindPotentialBuildingForTakeover(visit));
		EntityID candidateId = (forceManager.IsValid ? forceManager : FindPeepCandidateForTakeover(visit));
		var (flag, cost) = FindPotentialTakeoverCost(visit, buildingId);
		if (!flag)
		{
			buildingId = EntityID.INVALID;
		}
		return new TakeoverData(buildingId, candidateId, cost);
	}

	internal TakeoverData FindTakeoverDataForAI(EntityID buildingId)
	{
		EntityID candidateId = PlayerSocial.FindAIConnectionToOwnBiz(_player);
		return new TakeoverData(buildingId, candidateId, Price.ZERO);
	}

	public TakeoverData FindTakeoverDataForDistrictGrant(Label districtTag)
	{
		if (districtTag.IsNotSet)
		{
			return default(TakeoverData);
		}
		DistrictConfig firstDistrictForTag = Game.ctx.session.mapconfig.map.GetFirstDistrictForTag(districtTag);
		if (firstDistrictForTag == null)
		{
			return default(TakeoverData);
		}
		Node node = Game.ctx.board.nodes.FindNearestNodeAround(firstDistrictForTag.start, 100f);
		EntityID buildingId = FindExternalBuildingForTakeover(node);
		EntityID candidateId = PlayerSocial.FindAIConnectionToOwnBiz(_player);
		return new TakeoverData(buildingId, candidateId, Price.ZERO);
		EntityID FindExternalBuildingForTakeover(Node source)
		{
			EntityID result = EntityID.INVALID;
			Game.ctx.board.nodes.VisitNeighborhoodBFS(node, int.MaxValue, delegate(Node n)
			{
				result = n.potential;
			}, null, null, (Node n) => result.IsValid, onlyBizNodes: true);
			return result;
		}
	}

	public EntityID FindPeepCandidateForTakeover(VisitState visit)
	{
		return PlayerSocial.FindRelativeToOwnBiz(visit);
	}

	public EntityID FindPotentialBuildingForTakeover(VisitState visit, bool checkNeighbors = true)
	{
		Node node = visit.building.components.board.GetNode();
		if (node == null)
		{
			return EntityID.INVALID;
		}
		using (ListPool<Node>.PooledBlockList pooledBlockList = ListPool<Node>.Allocate())
		{
			PopulateNodesWithControlledBuildings(pooledBlockList);
			EntityID result = FindPotentialBuildingIfInTerritory(node, pooledBlockList);
			if (result.IsValid)
			{
				return result;
			}
			if (checkNeighbors)
			{
				NodeEdgeID[] edges = node.edges;
				for (int i = 0; i < edges.Length; i++)
				{
					NodeEdgeID edgeId = edges[i];
					if (!edgeId.IsNotValid)
					{
						Node node2 = edgeId.FindEdge().FindOtherNode(node);
						EntityID result2 = FindPotentialBuildingIfInTerritory(node2, pooledBlockList);
						if (result2.IsValid)
						{
							return result2;
						}
					}
				}
			}
		}
		return EntityID.INVALID;
	}

	private EntityID FindPotentialBuildingIfInTerritory(Node node, List<Node> excluded)
	{
		if (!IsOwnerOfNode(node) || excluded.Contains(node) || !_cachedPotentials.GetElementsUnsafe().Contains(node.id))
		{
			return EntityID.INVALID;
		}
		return node.potential;
	}

	private (bool valid, Price price) FindPotentialTakeoverCost(VisitState visit, EntityID buildingId)
	{
		if (buildingId.IsNotValid)
		{
			return (valid: false, price: Price.ZERO);
		}
		Entity entity = BuildingUtil.FindBizForBuilding(buildingId);
		if (entity?.config?.biz?.playerTakeoverDisabled == true)
		{
			return (valid: false, price: Price.ZERO);
		}
		int num = entity.config.biz.playerTakeoverCost?.Evaluate(visit.MakeOwnerModQuery()).RoundCoarse() ?? 0;
		int num2 = MathUtil.ClampMin(_territory.controlledBuildings.Count - 1, 1);
		int num3 = num * num2;
		return (valid: true, price: new Price(num3));
	}

	public Entity PerformTakeover(CrewAssignment crew, TakeoverData td)
	{
		BusinessTracker businesses = Game.ctx.simman.businesses;
		Entity entity = td.FindBuilding();
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(entity);
		PlayerID pid = entity.data.building.controlled.Get();
		if (pid.IsValid)
		{
			Game.ctx.players.WithID(pid).territory.ClearControlledAndResetBiz(entity, refreshRespect: false, attacked: false);
		}
		if (td.cost.IsNonZero)
		{
			Price delta = td.cost * -1;
			_player.finances.DoChangeMoney(crew.GetVehicle(), delta, MoneyReason.QuestDemand);
		}
		if (td.candidateId.IsValid)
		{
			businesses.ClearOwner(buildingAndBusinessData.biz, shutdown: false);
			businesses.ForceAssignOwner(buildingAndBusinessData.biz, td.FindCandidate());
			_player.outposts.ToggleBuildingTakeoverFlag(td.candidateId, set: true);
		}
		SetScoped(entity);
		SetControlled(entity);
		Node node = entity.components.board.GetNode();
		node.potential = EntityID.INVALID;
		_heatAndRespect.RecomputeRespect(node, force: true);
		entity.components.modules.RemoveBackroomModules();
		entity.components.modules.SetEnabledOn(Game.ctx.clock.Now);
		_player.outposts.StopPayingTributeIfPaying(buildingAndBusinessData.biz, buildingAndBusinessData.building, crew.peepId);
		LearnAboutResourcesFromTakeover(buildingAndBusinessData.building);
		_cachedPotentials.OnTakeover();
		if (_player.IsHuman)
		{
			Game.ctx.simman.hints.ShowBuildingGainedHint();
		}
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerBuildingTakeoverImmediate, entity.Id, _pid));
		Game.ctx.players.Human.crew.CrewGrowth.OnPlayerTurnStarted(Game.ctx.players.Human.crew);
		PlayerSocial.DebugLogAIHistory(_pid, _pid, entity, node, "ai", "building-added");
		return entity;
	}

	private void LearnAboutResourcesFromTakeover(Entity building)
	{
		IEnumerable<Label> enumerable = ModulesUtil.GetInventory(building)?.data?.contents?.Select((ResourceAndQty raq) => raq.id);
		IEnumerable<Label> enumerable2 = null;
		if (building.components.modules.FindFrontroomModule() != null)
		{
			ModuleQuery _ = new ModuleQuery(_pid, building, building.components.board.GetNodeID(), null, null);
			enumerable2 = ModulesUtil.FindAllResourcesProducedByBizModules(building, _);
		}
		List<Resource> list = new List<Resource>();
		if (enumerable != null)
		{
			list.AddRange(enumerable.Select((Label id) => Resource.Find(id)));
		}
		if (enumerable2 != null)
		{
			list.AddRange(enumerable2.Select((Label id) => Resource.Find(id)));
		}
		PlayerSkills skills = _player.skills;
		foreach (Resource item in list)
		{
			if (!skills.HasResourceUnlocked(item.resid))
			{
				skills.UnlockResource(item.resid, startup: false);
			}
		}
	}

	public void ClearControlledAndResetBiz(Entity building, bool refreshRespect, bool attacked)
	{
		_player.automation.RemoveBuildingFromDeliveries(building);
		building.components.modules.RemoveBackroomModules();
		ModulesUtil.ClearInventoryResourcesAndCash(ModulesUtil.GetInventory(building), _player);
		Entity item = ModulesUtil.GetManagerOrNull(building).manager;
		if (item != null)
		{
			_player.crew.UnassignCrewFromBuilding(item.Id);
		}
		Entity entity = BuildingUtil.FindBizForBuilding(building);
		if (entity != null)
		{
			BusinessTracker businesses = Game.ctx.simman.businesses;
			businesses.ClearOwner(entity, shutdown: false);
			businesses.RefreshOwnerAfterLosingControlled(entity, building);
		}
		if (_player.gambling.IsOwnedGamblingHouse(building))
		{
			_player.gambling.RemoveGamblingModule(building);
		}
		ClearControlledInternal(building);
		PlayerSocial.DebugLogAIHistory(_pid, _pid, building, building?.components?.board?.GetNode(), "ai", "building-removed");
		if (refreshRespect)
		{
			Node node = building.components.board.GetNode();
			_player.territory.AddHeatBuff(node, BuffConstants.HEAT_BUILDING_DESTROYED, EntityID.INVALID, new ModQuery(_pid, node));
			RecomputeHeatAndRespect(node, forceCurrent: true);
		}
		if (_player.IsHuman)
		{
			Game.ctx.simman.hints.ShowBuildingLostHint();
			if (!attacked)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BLDG_DAMAGE_DESTROYED, TickerTitle.BLDG_DAMAGE_DESTROYED, Loc.Get("ui.tickers.building-clear-control"), building.Id);
			}
		}
	}

	public void PerformGrantMarkKnown(Node source, int count, int distance)
	{
		PlayerMeetings viz = _player.meetings;
		PerformGrantCallback(source, count, distance, delegate(Node node)
		{
			if (viz.IsNodeKnown(node))
			{
				return false;
			}
			viz.MarkNodeAsKnown(node, expectedSeen: true, instant: true);
			return true;
		});
	}

	public void PerformGrantMarkScoped(EntityID crew, Node source, int count, int distance)
	{
		PlayerMeetings viz = _player.meetings;
		PerformGrantCallback(source, count, distance, delegate(Node node)
		{
			if (!viz.IsNodeKnown(node))
			{
				return false;
			}
			if (node.interesting.Count == 0 || AreAllScoped(node))
			{
				return false;
			}
			ScopeOutNode(node, crew);
			return true;
		});
	}

	private void ScopeOutNode(Node node, EntityID crew)
	{
		foreach (EntityID item in node.interesting)
		{
			Entity entity = item.FindEntity();
			if (entity != null && !IsScoped(entity))
			{
				ScopeOutBuildingWithFeedback(entity, crew);
			}
		}
	}

	private void ScopeOutNodeIfBecameNewOwner(Node node)
	{
		PlayerMeetings meetings = _player.meetings;
		if (!meetings.IsNodeKnown(node))
		{
			meetings.MarkNodeAsKnown(node, expectedSeen: true, instant: false);
		}
		ScopeOutNode(node, EntityID.INVALID);
	}

	private void PerformGrantCallback(Node source, int maxCount, int maxDistance, Predicate<Node> p)
	{
		WorldPos sourcepos = source.pos;
		int count = 0;
		int maxDistSq = maxDistance * maxDistance;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(source, 300, delegate(Node candidate)
		{
			if (candidate != source && count < maxCount && p(candidate))
			{
				count++;
			}
		}, delegate(Node node)
		{
			if (count >= maxCount)
			{
				return false;
			}
			if ((node.pos - sourcepos).MagnitudeSquared > (float)maxDistSq)
			{
				return false;
			}
			return node.HasRoad ? true : false;
		}, (NodeEdge edge) => !edge.IsRail);
	}

	public void ScopeOutBuildingWithFeedback(Entity building, EntityID crew)
	{
		ScopeOutAndMeetOwner(building, procgen: false, crew, setControlled: false);
		Game.ctx.vfx.PlayOneShotPFX(PFXType.ScopeOutFX, building.data.board.worldpos, _pid, 2f);
		crew.FindEntity()?.components.agent.IncrementStat(CrewStats.BizScouted, 1);
		if (_pid.IsHumanPlayer)
		{
			if (BuildingUtil.FindBizForBuilding(building) != null || building.components.building.IsSafehouse)
			{
				Game.ctx.hud.tickers.AddTickerScopeOut(building.Id);
			}
			Game.ctx.sfx.PlayScopeOutBiz();
		}
	}

	public void AddToProductionHistory(ResourceAndQtyList list)
	{
		if (list?.data == null)
		{
			return;
		}
		_territory.productionHistory = _territory.productionHistory ?? new ResourceAndQtyList();
		foreach (ResourceAndQty datum in list.data)
		{
			if (datum.qty > 0)
			{
				_territory.productionHistory.Increment(datum);
			}
		}
	}

	public Fixnum FindInProductionHistory(Label resId)
	{
		return _territory.productionHistory?.Get(resId) ?? Fixnum.ZERO;
	}

	public Fixnum FindInStorageTotal(Label resId)
	{
		Fixnum total = 0;
		Add(_territory.controlledBuildings);
		Add(_player.crew.AllVehicles);
		return total;
		void Add(IEnumerable<EntityID> eids)
		{
			foreach (EntityID eid in eids)
			{
				InventoryModule inventory = ModulesUtil.GetInventory(eid);
				if (inventory != null)
				{
					total += inventory.data.Get(resId).qty;
				}
			}
		}
	}

	public bool IsBuildingDamaged(Entity building)
	{
		return building.components.building.HasDamage();
	}

	public bool CanBeDamaged(Entity building)
	{
		if (building.components.building.IsControlledByAnyPlayer())
		{
			return !building.components.building.IsSafehouse;
		}
		return false;
	}

	public bool CanBeRepaired(Entity building)
	{
		if (IsBuildingDamaged(building))
		{
			if (!_pid.IsAIPlayer)
			{
				return ModulesUtil.GetManagerOrNull(building).manager != null;
			}
			return true;
		}
		return false;
	}

	public bool CanBeDestroyed(Entity building)
	{
		if (IsBuildingDamaged(building) && building.components.building.GetHealthDataOrNull().current <= 0)
		{
			return !building.components.building.IsSafehouse;
		}
		return false;
	}

	public Fixnum GetHealth(Entity building)
	{
		return building.components.building.GetHealth();
	}

	private (bool healthDecreased, bool wasDestroyed) SetHealth(Entity building, Fixnum value)
	{
		BuildingComponent building2 = building.components.building;
		BuildingData.Health healthDataOrAdd = building2.GetHealthDataOrAdd();
		Fixnum fixnum = Fixnum.Clamp(value, 0, healthDataOrAdd.max);
		bool item = fixnum < healthDataOrAdd.current;
		building2.SetHealth(fixnum);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.BuildingHealthChanged, building.Id, _pid));
		bool flag = fixnum <= 0;
		if (flag)
		{
			if (CanBeDestroyed(building))
			{
				ClearControlledAndResetBiz(building, refreshRespect: true, attacked: true);
			}
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.BuildingHealthZero, building.Id, _pid));
		}
		return (healthDecreased: item, wasDestroyed: flag);
	}

	private void ShowPhotos(Entity building, bool humanAttacked, bool wasDestroyed)
	{
		BuildingSettings buildingSettings = Game.serv.globals.settings.people.buildingSettings;
		List<PhotoConfig> list = ((humanAttacked && wasDestroyed) ? buildingSettings.humanDestroyedPhotos : ((humanAttacked && !wasDestroyed) ? buildingSettings.humanAttackedPhotos : ((!humanAttacked && wasDestroyed) ? buildingSettings.gangDestroyedPhotos : ((!humanAttacked && !wasDestroyed) ? buildingSettings.gangAttackedPhotos : null))));
		if (list != null)
		{
			Game.serv.ui.AddPopup(new PhotoPopup(list, delegate
			{
				PersonInfoUtil.TweenCameraToEntity(building);
			}));
		}
	}

	public void ProcessBuildingAttack(Entity building, PlayerID attacker)
	{
		Fixnum fixnum = BuildingSettings.EvaluateAttackPoints(building, attacker);
		Game.ctx.vfx.PlayOneShotPFX(PFXType.AttackFX, building.data.board.worldpos, attacker, 2f);
		Fixnum value = GetHealth(building) + fixnum;
		var (flag, flag2) = SetHealth(building, value);
		if (_pid.IsHumanPlayer)
		{
			if (flag2)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BLDG_DAMAGE_DESTROYED, TickerTitle.BLDG_DAMAGE_DESTROYED, Loc.Get("ui.tickers.building-damage-destroyed"), building.Id);
			}
			if (!flag2 && flag)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BLDG_DAMAGE_TAKEN, TickerTitle.BLDG_DAMAGE_TAKEN, Loc.Get("ui.tickers.building-damage-taken"), building.Id);
			}
		}
		if (_pid.IsHumanPlayer || attacker.IsHumanPlayer)
		{
			ShowPhotos(building, attacker.IsHumanPlayer, flag2);
			Game.ctx.sfx.PlayCombatHappened();
		}
		Label socialAction = (flag2 ? SocialConstants.GANG_BUILDING_DESTROY : SocialConstants.GANG_BUILDING_DAMAGE);
		attacker.FindPlayer().social.PerformSocialActionOn(socialAction, _pid, EntityID.INVALID);
		_player.ai?.combat?.ProcessTruceBreakingActionBy(attacker);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.GangWarAction, attacker.FindPlayer().crew.GetCrewForPlayerPeep().peepId, attacker, _player.PID));
	}

	private void ProcessDamagedBuildings()
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		(from b in GetAllControlledBuildingsUnsafe()
			select b.FindEntity() into b
			where IsBuildingDamaged(b)
			select b).SendTo(pooledBlockList);
		foreach (Entity item in pooledBlockList)
		{
			TryRepairBuilding(item);
		}
	}

	private void TryRepairBuilding(Entity building)
	{
		if (CanBeRepaired(building))
		{
			Fixnum fixnum = BuildingSettings.EvaluateHealingPoints(building, _pid);
			Fixnum value = GetHealth(building) + fixnum;
			SetHealth(building, value);
			BuildingData.Health healthDataOrNull = building.components.building.GetHealthDataOrNull();
			if (healthDataOrNull != null && healthDataOrNull.IsMax && _pid.IsHumanPlayer)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BLDG_DAMAGE_FIXED, TickerTitle.BLDG_DAMAGE_FIXED, Loc.Get("ui.tickers.building-damage-fixed"), building.Id);
				ModulesUtil.GetManagerOrNull(building).manager?.components.agent.IncrementStat(CrewStats.BuildingsRepaired, 1);
			}
		}
	}

	public void ForceCloseBusiness(Entity building, PlayerID enemyId, EntityID enemyPeep)
	{
		BusinessSettings.ForcedClosure forcedClosure = Game.serv.globals.settings.people.businessSettings.forcedClosure;
		Node node = building.components.board.GetNode();
		Entity entity = BuildingUtil.FindBizForBuilding(building);
		entity.components.biz.MarkForceClosed(_pid, enemyId);
		Entity entity2 = BuildingUtil.FindOwnerForBiz(entity);
		_player.social.AddBuffFrom(entity2.Id, forcedClosure.relBuffForOriginator);
		PlayerInfo playerInfo = enemyId.FindPlayer();
		if (playerInfo.IsHuman)
		{
			playerInfo.social.PerformSocialActionOn(forcedClosure.socialForHumanCause, entity2.Id, enemyPeep, Extend);
		}
		playerInfo.territory.AddRespectBuff(node, BuffConstants.RESPECT_FORCECLOSE, enemyPeep, new ModQuery(enemyId, node));
		_player.territory.AddHeatBuff(node, BuffConstants.HEAT_FORCECLOSE, enemyPeep, new ModQuery(_pid, node));
		if (_pid.IsHumanPlayer || playerInfo.IsHuman)
		{
			TickerIcon icon = (_pid.IsHumanPlayer ? TickerIcon.FORCECLOSE_HUMAN : TickerIcon.FORCECLOSE_GANG);
			TickerTitle title = (_pid.IsHumanPlayer ? TickerTitle.FORCECLOSE_HUMAN : TickerTitle.FORCECLOSE_GANG);
			string key = (_pid.IsHumanPlayer ? "ui.tickers.forceclose-human" : "ui.tickers.forceclose-gang");
			string text = (_pid.IsHumanPlayer ? playerInfo : _player).social.FindPlayerGroupNameColorized();
			Game.ctx.hud.tickers.AddTextTicker(icon, title, Loc.Get(key, "groupname", text, "name", entity2.data.person.FullName), building.Id);
		}
		HistoryLedgerItem Extend(HistoryLedgerItem info)
		{
			info.actor = enemyPeep;
			info.node = node?.id ?? default(NodeID);
			return info;
		}
	}

	internal void RemoveBuildingsAndTerritoryOnDefeat()
	{
		foreach (Entity item in _territory.controlledBuildings.SelectIntoNewList((EntityID id) => id.FindEntity()))
		{
			ClearControlledAndResetBiz(item, refreshRespect: true, attacked: false);
		}
		foreach (Node item2 in _territory.ownedNodes.SelectIntoNewList((NodeID id) => id.FindNode()))
		{
			ClearNodeOwner(item2, PlayerID.INVALID);
		}
	}

	internal void RemoveAllRecentTradeHistories()
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBizUnsafe())
		{
			if (item?.components.biz?.RemoveTradeHistoryWithPlayer(_pid) > 0)
			{
				Entity entity = BuildingUtil.FindBuildingForBiz(item);
				if (entity != null)
				{
					Game.ctx.events.EnqueueOnce(SessionEventType.BuildingTradeHistoryCleared, _pid, entity.Id);
				}
			}
		}
	}

	public int CountModuleAndChildrenInTerritory(Label bizConfig, bool mustBeTiedHouse = false, bool mustHaveTradeHistory = false, bool mustBeInTerritory = false)
	{
		int num = 0;
		EntityManager entityman = Game.ctx.entityman;
		Label label = bizConfig;
		foreach (EntityConfig item in entityman.FindCachedTemplatesByPrefix(new Label(label.ToString() + "*")))
		{
			foreach (Entity item2 in Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(item.Template))
			{
				if (!(!item2.components.biz.HasAnyTradeHistory(PlayerID.HumanPlayer) && mustHaveTradeHistory) && !(mustBeTiedHouse & !item2.components.biz.IsTiedTo(PlayerID.HumanPlayer)) && !(mustBeInTerritory & !BuildingUtil.FindBuildingForBiz(item2).components.board.GetNode().owner.Is(PlayerID.HumanPlayer)))
				{
					num++;
				}
			}
		}
		return num;
	}

	public bool CheckModuleOrChildrenInControlled(string moduleConfig)
	{
		foreach (EntityConfig item in Game.ctx.entityman.FindCachedTemplatesByPrefix(new Label(moduleConfig + "*")))
		{
			foreach (EntityID item2 in Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe())
			{
				if (item2.FindEntity().components.modules.HasModuleInstalled(item.Template))
				{
					return true;
				}
			}
		}
		return false;
	}

	private string CheatChangeRespect(string[] args)
	{
		if (args.Length != 4)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 3, "<crew-index> <respect-delta>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 3, "<crew-index> <respect-delta>");
		}
		if (!int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid respect delta", args, 3, "<crew-index> <respect-delta>");
		}
		CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
		if (!crewForIndex.IsValid)
		{
			return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 3, "<crew-index> <respect-delta>");
		}
		Node node = crewForIndex.peepId.FindEntity().components.agent.GetNode();
		_heatAndRespect.RespectFromCheatsIncrement(node, result2);
		return $"Changed node {node} respect for {_pid} by {result2} points.";
	}

	private string CheatAddTerritoryCrew(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index>");
		}
		if (int.TryParse(args[2], out var result))
		{
			CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
			if (crewForIndex.IsValid)
			{
				Node node = crewForIndex.peepId.FindEntity().components.agent.GetNode();
				SetNodeOwner(node);
				return $"Marked node {node} as owned by {_pid}.";
			}
		}
		return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 2, "<crew-index>");
	}

	private string CheatRemoveTerritoryCrew(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index>");
		}
		if (int.TryParse(args[2], out var result))
		{
			CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
			if (crewForIndex.IsValid)
			{
				Node node = crewForIndex.peepId.FindEntity().components.agent.GetNode();
				PlayerInfo playerInfo = node.owner.Get().FindPlayer();
				if (playerInfo != null)
				{
					playerInfo.territory.ClearNodeOwner(node, PlayerID.HumanPlayer);
					return $"Marked node {node} as not owned by anyone.";
				}
				return $"Invalid node {node}; it does not have an owner.";
			}
		}
		return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 2, "<crew-index>");
	}

	private string CheatSetBuildingHealth(string[] args)
	{
		if (args.Length != 4 || !int.TryParse(args[2], out var result) || !int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<entity-index> <health>\n(Use command 'find entity' to find the right entity via index)");
		}
		Entity entity = Game.ctx.entityman.FindByIndex(result);
		if (entity == null)
		{
			return "Failed to find entity with index " + result;
		}
		if (entity.components.building == null)
		{
			return "Expected a building, got invalid entity: " + entity;
		}
		PlayerID controllingPlayer = entity.components.building.GetControllingPlayer();
		if (controllingPlayer.IsNotAnyPlayer)
		{
			return "Building must be controlled by some player: " + entity;
		}
		controllingPlayer.FindPlayer().territory.SetHealth(entity, result2);
		return $"Set building health to {result2} for building {entity}";
	}

	private string CheatAddAllTerritory(string[] args)
	{
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			if (!IsNodeOwnedByAnyone(item) && item.HasRoad)
			{
				SetNodeOwner(item);
			}
		}
		return "Maked all nodes as owned.";
	}

	private string CheatScopeOutAll(string[] arg)
	{
		CheatScopeOut(scope: true);
		return "Marked all buildings as revealed";
	}

	private string CheatScopeOutNone(string[] arg)
	{
		CheatScopeOut(scope: false);
		return "Marked all buildings as unknown";
	}

	private void CheatScopeOut(bool scope)
	{
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			foreach (EntityID item2 in item.interesting)
			{
				bool flag = item2.FindEntity().components.building.IsScopedBy(_pid);
				if (scope && !flag)
				{
					SetScoped(item2.FindEntity());
				}
				if (!scope && flag)
				{
					ClearScoped(item2.FindEntity());
				}
			}
		}
	}
}

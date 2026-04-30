using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerOutposts : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	private struct DistEntry
	{
		public OutpostEntry entry;

		public float dist;
	}

	public enum RemovalReason
	{
		PlayerFiredOwner,
		PlayerNeglected,
		Stolen
	}

	private enum PumpStatus
	{
		NotActive,
		ActiveAndFinished,
		ActiveAndContinue
	}

	private const int MONTHS_TILL_WARNING = 2;

	private const int MONTHS_TILL_SHUTDOWN = 3;

	private PlayerOutpostsData _outposts;

	private List<EntityID> _tmp_alreadyPaid = new List<EntityID>();

	public static readonly Label BUILDING_TAKEOVER_FLAG = (Label)"building-takeover";

	public SimTime LastClosureTime => _outposts.lastClosureTime;

	public SimTime LastStolenTime => _outposts.lastStolenTime;

	private OutpostSettings OutpostSettings => Game.serv.globals.settings.people.social.outposts;

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		ProcessOutpostsOnTurnStart();
	}

	public void OnPlayerTurnEnded()
	{
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_outposts = _data.outposts;
	}

	public List<OutpostEntry> GetOutpostEntriesUnsafe()
	{
		return _outposts.outposts;
	}

	public OutpostEntry GetOutpostEntryUnsafe(OutpostID outpostId)
	{
		return _outposts.FindByOutpostID(outpostId);
	}

	public OutpostEntry GetOutpostEntryUnsafe(Entity building)
	{
		return _outposts.FindByOutpostBuilding(building);
	}

	public OutpostID GetOutpostAt(Entity building)
	{
		return _outposts.FindByOutpostBuilding(building)?.outpostId ?? OutpostID.INVALID;
	}

	public OutpostID GetOutpostAtNode(Node node)
	{
		return _outposts.FindByOutpostNode(node.id)?.outpostId ?? OutpostID.INVALID;
	}

	public int GetOutpostClosures(Entity building)
	{
		return _outposts.GetClosureCount(new OutpostID(building));
	}

	public bool HasOutpostNear(Node node)
	{
		return _outposts.HasOutpostsThatPumpNode(node.id);
	}

	public bool CurrentlyExpandingInto(Node node)
	{
		return _outposts.FindOutpostThatPumpsNodeRightNow(node.id).IsValid;
	}

	public OutpostID FindBestOutpostForTribute(Node node)
	{
		return (from e in _outposts.FindAllEntries(node.id)
			select e.entry).OrderByDescending(ScoreEntryForTribute).FirstOrDefault()?.outpostId ?? OutpostID.INVALID;
	}

	public (OutpostID outpost, float distance) FindClosestOutpostToNode(Node node)
	{
		DistEntry distEntry = default(DistEntry);
		if (_outposts.outposts != null)
		{
			distEntry = (from de in _outposts.outposts.Select(delegate(OutpostEntry e)
				{
					float magnitude = (e.OutpostNode.nodeId.FindNode().pos - node.pos).Magnitude;
					return new DistEntry
					{
						dist = magnitude,
						entry = e
					};
				})
				orderby de.dist
				select de).FirstOrDefault();
		}
		return (outpost: distEntry.entry?.outpostId ?? OutpostID.INVALID, distance: distEntry.dist);
	}

	private int ScoreEntryForTribute(OutpostEntry entry)
	{
		return (int)(((Fixnum?)entry.OutpostNode.nodeId.FindNode()?.respect.GetOrNull(_pid)?.current.scaled) ?? Fixnum.ZERO);
	}

	public OutpostEntry SetOutpost(Entity building)
	{
		building.components.building.SetOutpost(_pid);
		OutpostEntry outpostEntry = _outposts.Add(building, FindNodesForOutpost(building));
		PumpUpRespect(outpostEntry, 0, instant: true);
		ChangeHeatRespectAtOutpostStart(outpostEntry);
		ChangeRelBuffsAtOutpostStart(outpostEntry);
		List<PlayerID> playersMaybeWronged = new List<PlayerID>();
		Game.ctx.board.nodes.VisitNeighborhoodBFSMaxDistance(building.components.board.GetNode(), _player.ai.territory.GetDistanceForOutpostAgreement(), GetPlayerOwnerOfNode);
		foreach (PlayerID item in playersMaybeWronged)
		{
			item.FindPlayer().ai?.territory?.ProcessExpansionTreatyBreakingActionBy(_pid);
			if (!(item == PlayerID.HumanPlayer) && !(item == _pid))
			{
				item.FindPlayer().ai?.territory?.TryRequestTreatyWith(_pid);
			}
		}
		PlayerSocial.DebugLogAIHistory(_pid, _pid, building, building?.components?.board?.GetNode(), "ai", "front-added");
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerOutpostChanged, building.Id, _pid));
		return outpostEntry;
		void GetPlayerOwnerOfNode(Node x)
		{
			if (!playersMaybeWronged.Contains(x.owner.pid) && x.owner.pid.IsValid)
			{
				playersMaybeWronged.Add(x.owner.pid);
			}
		}
	}

	public bool CanRemoveOutpostByStealing(OutpostID outpost)
	{
		Entity entity = outpost.FindBuilding();
		if (entity.components.building.IsOutpostOf(_pid))
		{
			return !entity.components.building.IsSafehouseOf(_pid);
		}
		return false;
	}

	public void RemoveOutpost(OutpostID outpost, RemovalReason reason, PlayerID instigator, EntityID crewpeep, bool removeMeAsNodeOwner = false)
	{
		if (reason == RemovalReason.Stolen && !CanRemoveOutpostByStealing(outpost))
		{
			Logger.Warning("Safehouse outposts cannot be stolen");
			return;
		}
		Entity entity = outpost.FindBuilding();
		OutpostEntry outpostEntryUnsafe = GetOutpostEntryUnsafe(outpost);
		Node node = entity.components.board.GetNode();
		PlayerSocial.DebugLogAIHistory(_pid, _pid, entity, node, "ai", (reason == RemovalReason.Stolen) ? "front-stolen" : "front-removed");
		if (reason == RemovalReason.Stolen)
		{
			WarnPlayerAboutStolenOutpost(outpostEntryUnsafe, instigator);
		}
		foreach (Entity item in (from e in GetEachBizThatPaysOutpostExpensive(outpost)
			select e.building).ToList())
		{
			StopPayingTribute(item.Id, EntityID.INVALID);
		}
		ChangeHeatRespectAtOutpostRemoval(outpostEntryUnsafe);
		entity.components.building.ClearOutpost();
		_outposts.Remove(entity);
		_outposts.IncrementClosure(entity);
		_outposts.lastClosureTime = Game.ctx.clock.Now;
		if (reason == RemovalReason.Stolen)
		{
			_outposts.lastStolenTime = _outposts.lastClosureTime;
			instigator.FindPlayer().social.PerformSocialActionOn(SocialConstants.HAS_STOLEN_OUTPOST, _pid, crewpeep, Extend);
			_player.ai?.social?.ProcessAttackedBy(instigator);
			_player.ai?.territory?.TryRequestOutpostAgreementWith(instigator);
		}
		ChangeRelBuffsAtOutpostRemoval(outpostEntryUnsafe, reason, crewpeep);
		if (_player.IsHuman)
		{
			Game.ctx.sfx.PlayTerritoryChanged(gained: false);
		}
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerOutpostChanged, entity.Id, _pid));
		HistoryLedgerItem Extend(HistoryLedgerItem info)
		{
			info.actor = crewpeep;
			info.node = node?.id ?? default(NodeID);
			return info;
		}
	}

	public void RemoveOutpostsOnDefeat()
	{
		_ = _player.territory.IsSafehouseVanquished;
		foreach (OutpostID item in (from e in _player.outposts.GetOutpostEntriesUnsafe()
			select e.outpostId).ToList())
		{
			RemoveOutpost(item, RemovalReason.PlayerNeglected, _pid, EntityID.INVALID, removeMeAsNodeOwner: true);
		}
	}

	public (OutpostOwner owner, OutpostState state) GenerateOutpostInfo(Entity building)
	{
		(OutpostOwner owner, OutpostEntry humanentry) tuple = GenerateOutpostOwner(building);
		OutpostOwner item = tuple.owner;
		OutpostEntry item2 = tuple.humanentry;
		OutpostState item3 = ((item == OutpostOwner.OutpostHuman) ? GenerateHumanOutpostState(item2) : OutpostState.None);
		return (owner: item, state: item3);
	}

	private (OutpostOwner owner, OutpostEntry humanentry) GenerateOutpostOwner(Entity building)
	{
		if (building.components.building.IsOutpostNotOf(_pid) && building.components.building.IsScopedBy(_pid))
		{
			return (owner: OutpostOwner.OutpostAI, humanentry: null);
		}
		OutpostEntry outpostEntryUnsafe = GetOutpostEntryUnsafe(building);
		if (outpostEntryUnsafe != null)
		{
			return (owner: OutpostOwner.OutpostHuman, humanentry: outpostEntryUnsafe);
		}
		Entity entity = BuildingUtil.FindBizForBuilding(building);
		if (entity != null && IsBizPayingTribute(entity))
		{
			return (owner: OutpostOwner.ExtortedByHuman, humanentry: null);
		}
		return (owner: OutpostOwner.None, humanentry: null);
	}

	private OutpostState GenerateHumanOutpostState(OutpostEntry entry)
	{
		Fixnum delta = entry.money.Delta;
		if (delta < 0)
		{
			if (entry.money.months < 2)
			{
				return OutpostState.NeedToSupport;
			}
			return OutpostState.NeedToSupportUrgent;
		}
		if (delta > 0)
		{
			return OutpostState.NeedToCollect;
		}
		if (entry.pump.IsPumping)
		{
			return OutpostState.Expanding;
		}
		if (CanStartNewOutpostExpansion(entry.outpostId))
		{
			return OutpostState.CanExpand;
		}
		return OutpostState.Active;
	}

	private void ChangeHeatRespectAtOutpostStart(OutpostEntry entry)
	{
		PlayerTerritory territory = _player.territory;
		ModQuery query = new ModQuery(_pid, entry.OutpostNode.nodeId.FindNode());
		foreach (NodeEntry targetNode in entry.targetNodes)
		{
			Node node = targetNode.nodeId.FindNode();
			if (targetNode == entry.OutpostNode)
			{
				if (!territory.IsOwnerOfNode(node))
				{
					territory.AddHeatBuff(node, BuffConstants.HEAT_OUTPOST_EXP, EntityID.INVALID, query);
				}
				if (territory.ContainsRespectBuff(node, BuffConstants.RESPECT_AROUND_OUTPOST))
				{
					territory.RemoveRespectBuff(node, BuffConstants.RESPECT_AROUND_OUTPOST);
					targetNode.ClearPumpedAndExpansion();
				}
				territory.AddRespectBuff(node, BuffConstants.RESPECT_AT_OUTPOST, EntityID.INVALID, query);
				territory.RecomputeHeatAndRespect(node, forceCurrent: true);
			}
			else if (!territory.ContainsRespectBuff(node, BuffConstants.RESPECT_AT_OUTPOST))
			{
				territory.AddRespectBuff(node, BuffConstants.RESPECT_AROUND_OUTPOST, EntityID.INVALID, query);
			}
		}
	}

	private void ChangeHeatRespectAtOutpostRemoval(OutpostEntry entry)
	{
		PlayerTerritory territory = _player.territory;
		foreach (NodeEntry targetNode in entry.targetNodes)
		{
			Node node = targetNode.nodeId.FindNode();
			bool flag = false;
			if (targetNode.outpost)
			{
				territory.RemoveRespectBuff(node, BuffConstants.RESPECT_AT_OUTPOST);
				flag = true;
			}
			else if (_outposts.CountOutpostsThatPumpNode(targetNode.nodeId) <= 1 && territory.ContainsRespectBuff(node, BuffConstants.RESPECT_AROUND_OUTPOST))
			{
				territory.RemoveRespectBuff(node, BuffConstants.RESPECT_AROUND_OUTPOST);
				targetNode.ClearPumpedAndExpansion();
				flag = true;
			}
			if (flag)
			{
				territory.RecomputeRespect(node, forceCurrent: true);
			}
		}
	}

	private void ChangeRelBuffsAtOutpostStart(OutpostEntry entry)
	{
		Entity entity = BuildingUtil.FindOwnerForAnyBuilding(entry.outpostId.buildingId);
		if (entity == null)
		{
			Logger.Warning("Failed to find owner for outpost", entry.outpostId);
		}
		else
		{
			_player.social.RemoveBuffFrom(entity.Id, BuffConstants.RELBUFF_OUTPOST_FIRED);
			_player.social.RemoveBuffFrom(entity.Id, BuffConstants.RELBUFF_OUTPOST_NEG);
			_player.social.AddBuffFrom(entity.Id, BuffConstants.RELBUFF_OUTPOST_MANAGER);
		}
	}

	private void ChangeRelBuffsAtOutpostRemoval(OutpostEntry entry, RemovalReason reason, EntityID crewpeep)
	{
		Entity entity = BuildingUtil.FindOwnerForAnyBuilding(entry.outpostId.buildingId);
		if (entity == null)
		{
			Logger.Warning("Failed to find owner for outpost", entry.outpostId);
			return;
		}
		_player.social.RemoveBuffFrom(entity.Id, BuffConstants.RELBUFF_OUTPOST_MANAGER);
		if (_player.social.EvaluateRelationshipFromSourceToPlayer(entity.Id) < 0)
		{
			Node node = entry.OutpostNode.nodeId.FindNode();
			ModQuery query = new ModQuery(_pid, EntityID.INVALID, crewpeep, node.id);
			_player.territory.AddHeatBuff(node, BuffConstants.HEAT_OUTPOST_FIRED, crewpeep, query);
			_player.territory.RecomputeHeat(node, forceCurrent: true);
		}
		switch (reason)
		{
		case RemovalReason.PlayerNeglected:
			_player.social.AddBuffFrom(entity.Id, BuffConstants.RELBUFF_OUTPOST_NEG);
			{
				foreach (EntityID item in PlayerSocial.ProduceFamily(entity.Id, onlyclose: true))
				{
					Relationship relationshipFromSourceToPlayer = _player.social.GetRelationshipFromSourceToPlayer(item);
					if (relationshipFromSourceToPlayer != null && !_player.crew.IsCrew(item))
					{
						relationshipFromSourceToPlayer.AddBuff(BuffConstants.RELBUFF_OUTPOST_NEGFAM, crewpeep);
					}
				}
				break;
			}
		case RemovalReason.PlayerFiredOwner:
			_player.social.AddBuffFrom(entity.Id, BuffConstants.RELBUFF_OUTPOST_FIRED);
			break;
		}
	}

	private void MaybeCollectTributesAtEndOfMonth()
	{
		if (!Game.ctx.clock.State.DidCrossMonthBoundaries())
		{
			return;
		}
		_ = _pid.IsHumanPlayer;
		using (ListPool<OutpostID>.PooledBlockList pooledBlockList = ListPool<OutpostID>.Allocate())
		{
			_tmp_alreadyPaid.Clear();
			foreach (OutpostEntry outpost in _outposts.outposts)
			{
				CollectTributesFromBizToOutposts(outpost, _tmp_alreadyPaid);
				if (ProcessOutpostCosts(outpost))
				{
					pooledBlockList.Add(outpost.outpostId);
				}
			}
			if (Game.ctx.tutorial.AreOutpostFailsSuppressed)
			{
				pooledBlockList.Clear();
			}
			foreach (OutpostID item in pooledBlockList)
			{
				RemoveOutpost(item, RemovalReason.PlayerNeglected, _pid, EntityID.INVALID);
			}
		}
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerOutpostsAllUpdated, EntityID.INVALID, _pid));
	}

	private void EnsureAttachedBuffs()
	{
		if (!Game.ctx.clock.State.DidCrossMonthBoundaries())
		{
			return;
		}
		PlayerTerritory territory = _player.territory;
		foreach (OutpostEntry outpost in _outposts.outposts)
		{
			Node node = outpost.OutpostNode.nodeId.FindNode();
			ModQuery query = new ModQuery(_pid, node);
			foreach (NodeEntry targetNode in outpost.targetNodes)
			{
				Node node2 = targetNode.nodeId.FindNode();
				if (!territory.ContainsRespectBuff(node2, BuffConstants.RESPECT_AROUND_OUTPOST) && !territory.ContainsRespectBuff(node2, BuffConstants.RESPECT_AT_OUTPOST) && targetNode != outpost.OutpostNode)
				{
					territory.AddRespectBuff(node2, BuffConstants.RESPECT_AROUND_OUTPOST, EntityID.INVALID, query);
					targetNode.ClearPumpedAndExpansion();
				}
			}
			if (!territory.ContainsRespectBuff(outpost.OutpostNode.nodeId.FindNode(), BuffConstants.RESPECT_AT_OUTPOST))
			{
				territory.AddRespectBuff(node, BuffConstants.RESPECT_AT_OUTPOST, EntityID.INVALID, query);
			}
		}
	}

	internal void ToggleBuildingTakeoverFlag(EntityID ownerId, bool set)
	{
		Relationship item = _player.social.FindOrMakeRelationshipsWith(ownerId).from;
		if (set)
		{
			item.AddFlag(BUILDING_TAKEOVER_FLAG, null);
		}
		else
		{
			item.RemoveFlag(BUILDING_TAKEOVER_FLAG);
		}
		Game.ctx.events.SendImmediate(SessionEventType.PlayerRelFlagsChangedImmediate, _pid);
	}

	internal bool IsBuildingTakeoverFlagSet(EntityID ownerId)
	{
		return _player.social.GetRelationshipFromSourceToPlayer(ownerId)?.TestFlag(BUILDING_TAKEOVER_FLAG, expected: true) ?? false;
	}

	public bool IsBizPayingTribute(Entity biz)
	{
		return biz.components.biz.PaysTributeTo(_pid);
	}

	public bool IsBizRejectingTribute(Entity biz)
	{
		return biz.components.biz.RejectedTributeTo(_pid);
	}

	public void StartPayingTribute(EntityID buildingId, OutpostID outpost, Price cashamt, EntityID crew)
	{
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(buildingId);
		buildingAndBusinessData.biz.components.biz.StartPayingTributeTo(_pid, outpost, cashamt);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerTributeChanged, buildingId, _pid));
		Label buffId = (IsBuildingTakeoverFlagSet(buildingAndBusinessData.owner.Id) ? BuffConstants.RELBUFF_TRIBUTE_AFTER_HELPING : BuffConstants.RELBUFF_TRIBUTE_ANYONE);
		GetOrMakeRelWithOwner(buildingAndBusinessData.owner.Id).AddBuff(buffId, crew);
	}

	public void StopPayingTributeIfPaying(Entity biz, Entity building, EntityID crew)
	{
		if (IsBizPayingTribute(biz))
		{
			StopPayingTribute(building.Id, crew);
		}
	}

	public void StopPayingTribute(EntityID buildingId, EntityID crew)
	{
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(buildingId);
		buildingAndBusinessData.biz.components.biz.StopPayingTributeTo(_pid);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerTributeChanged, buildingId, _pid));
		Relationship orMakeRelWithOwner = GetOrMakeRelWithOwner(buildingAndBusinessData.owner.Id);
		RemoveAllMatchingBuffs(orMakeRelWithOwner, BuffConstants.RELBUFF_TRIBUTE_PREFIX);
		orMakeRelWithOwner.AddBuff(BuffConstants.RELBUFF_TRIBUTE_AFTERCANCEL, crew);
	}

	private Relationship GetOrMakeRelWithOwner(EntityID ownerId)
	{
		Relationship relationshipFromSourceToPlayer = _player.social.GetRelationshipFromSourceToPlayer(ownerId);
		if (relationshipFromSourceToPlayer != null)
		{
			return relationshipFromSourceToPlayer;
		}
		return _player.social.FindOrMakeRelationshipsWith(ownerId).from;
	}

	private void RemoveAllMatchingBuffs(Relationship rel, Label id)
	{
		foreach (BuffConfig definition in Game.serv.globals.settings.people.allBuffs.definitions)
		{
			if (definition.id.String.StartsWith(id.String))
			{
				rel.RemoveBuff(definition.id);
			}
		}
	}

	internal void OnClearNodeOwner(Node node, PlayerID instigator)
	{
		OutpostID outpostAtNode = GetOutpostAtNode(node);
		if (outpostAtNode.IsValid)
		{
			RemoveOutpost(outpostAtNode, RemovalReason.PlayerFiredOwner, instigator, EntityID.INVALID);
		}
		LoseAnyTributesAtNode(node);
	}

	private void LoseAnyTributesAtNode(Node node)
	{
		List<Entity> list = (from e in GetEachBizAtThisNode(node)
			where IsBizPayingTribute(e.biz)
			select e.building).ToList();
		foreach (Entity item in list)
		{
			StopPayingTribute(item.Id, EntityID.INVALID);
		}
		if (list.Count > 0)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.TRIBUTE_NOTICE, TickerTitle.TRIBUTE_PROBLEM, Loc.Get("ui.tickers.biz-tribute-lost"), node.id);
		}
	}

	public MoneyStatus FindOutpostCollectionStatus(OutpostID outpost)
	{
		return _outposts.FindByOutpostID(outpost).money;
	}

	public bool IsOutpostReadyForCollection(OutpostID outpost)
	{
		OutpostEntry outpostEntry = _outposts.FindByOutpostID(outpost);
		if (outpostEntry != null)
		{
			return outpostEntry.money.months > 0;
		}
		return false;
	}

	public bool CanCrewCollectFromOutpost(VisitState visit, OutpostID outpost)
	{
		return CanCrewCollectFromOutpost(visit.vehicle, outpost);
	}

	public bool CanCrewCollectFromOutpost(Entity vehicle, OutpostID outpost)
	{
		Fixnum cash = _outposts.FindByOutpostID(outpost)?.money.Delta ?? Fixnum.ZERO;
		return _player.finances.CanChangeMoney(vehicle, new Price(cash));
	}

	public void DoCollectFromOutpost(VisitState visit, OutpostID outpost)
	{
		DoCollectFromOutpost(visit.vehicle, outpost);
	}

	public void DoCollectFromOutpost(Entity vehicle, OutpostID outpost)
	{
		OutpostEntry outpostEntry = _outposts.FindByOutpostID(outpost);
		if (outpostEntry == null)
		{
			Logger.Warning("Outpost disappeared before we could collect: ", outpost.buildingId);
		}
		else
		{
			_player.finances.DoChangeMoney(vehicle, new Price(outpostEntry.money.Delta), MoneyReason.Tribute);
			outpostEntry.money.Reset();
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerTributeChanged, outpost.buildingId, _pid));
		}
	}

	public Fixnum FindRespectBuffFromOutposts(NodeID nodeId, bool current)
	{
		return _outposts.SumRespectPumpedAt(nodeId, current);
	}

	public bool IsOutpostExpanding(OutpostEntry entry)
	{
		return _outposts.IsExpanding(entry);
	}

	public bool IsOutpostExpanding(Entity building)
	{
		return _outposts.IsExpanding(building);
	}

	public bool IsAnyOutpostExpanding()
	{
		return _outposts.IsAnyExpanding();
	}

	private List<NodeID> FindNodesForOutpost(Entity building)
	{
		Node node = building.components.board.GetNode();
		List<NodeID> list = (from node2 in Game.ctx.board.nodes.FindNeighborNodes(node, IsValidTerritoryNode, IsNodeRoadConnected)
			select node2.id).ToList();
		list.Insert(0, node.id);
		return list;
	}

	private bool IsNodeRoadConnected(NodeEdge edge)
	{
		return edge.IsRoad;
	}

	private bool IsValidTerritoryNode(Node node)
	{
		if (!HasBuildings(node))
		{
			return IsHighway(node);
		}
		return true;
	}

	private bool HasBuildings(Node node)
	{
		if (node.contained != null)
		{
			return node.contained.Count > 0;
		}
		return false;
	}

	private bool IsHighway(Node node)
	{
		return node.IsConnectionNode;
	}

	public bool CanStartNewOutpostExpansion(Entity building)
	{
		return CanStartNewOutpostExpansion(new OutpostID(building));
	}

	public bool CanStartNewOutpostExpansion(OutpostID outpostId)
	{
		OutpostEntry outpostEntry = _outposts.FindByOutpostID(outpostId);
		if (outpostEntry != null)
		{
			return FindNextCornerForExpansion(outpostEntry).index >= 0;
		}
		return false;
	}

	public (Node node, OutpostSettings.ExpansionDef exp) PickBestExpansion(OutpostID outpostId, VisitState visit)
	{
		OutpostEntry outpostEntry = _outposts.FindByOutpostID(outpostId);
		int item = FindNextCornerForExpansion(outpostEntry).index;
		if (item < 0)
		{
			return (node: null, exp: null);
		}
		Node node = outpostEntry.targetNodes[item].nodeId.FindNode();
		List<OutpostSettings.ExpansionDef> list = OutpostSettings.GenerateMatchingExpansionDefs(visit);
		if (list.Count <= 1)
		{
			return (node: node, exp: list.FirstOrDefaultFast());
		}
		DateTime dateTime = Game.ctx.clock.Now.ToDate();
		uint salt = (uint)(node.id.index * 10000 + dateTime.Year * 100 + dateTime.Month);
		OutpostSettings.ExpansionDef item2 = Game.ctx.scenario.MakeSeededRng(salt).PickElement(list);
		return (node: node, exp: item2);
	}

	public bool DoStartNewOutpostExpansion(OutpostID outpostId, OutpostSettings.ExpansionDef def)
	{
		OutpostEntry outpostEntry = _outposts.FindByOutpostID(outpostId);
		if (outpostEntry == null)
		{
			return false;
		}
		if (outpostEntry.pump.IsPumping)
		{
			return false;
		}
		int item = FindNextCornerForExpansion(outpostEntry).index;
		if (item < 0)
		{
			return false;
		}
		outpostEntry.pump.Set(item, def.id);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerOutpostChanged, outpostId.buildingId, _pid));
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_STARTED_EXPAND, TickerTitle.OUTPOST_STARTED_EXPAND, Loc.Get("ui.tickers.outpost-exp"), outpostId.buildingId);
		return true;
	}

	internal IEnumerable<(string line, Price cost)> ExplainExpansions(OutpostID outpostId)
	{
		OutpostEntry outpostEntryUnsafe = GetOutpostEntryUnsafe(outpostId);
		if (outpostEntryUnsafe == null)
		{
			yield break;
		}
		ModQuery q = outpostEntryUnsafe.MakeOutpostQuery(_pid);
		foreach (NodeEntry targetNode in outpostEntryUnsafe.targetNodes)
		{
			if (targetNode.IsPumped && targetNode.expansionId.IsSet)
			{
				OutpostSettings.ExpansionDef expansionDef = OutpostSettings.FindExpansion(targetNode.expansionId);
				if (expansionDef != null)
				{
					Fixnum fixnum = expansionDef.monthlyCost.Evaluate(q);
					yield return (line: Loc.Get(expansionDef.locline), cost: fixnum);
				}
			}
		}
	}

	private void ProcessOutpostsOnTurnStart()
	{
		foreach (OutpostEntry outpost in _outposts.outposts)
		{
			PumpUpRespect(outpost);
		}
		MaybeCollectTributesAtEndOfMonth();
		EnsureAttachedBuffs();
	}

	private void PumpUpRespect(OutpostEntry entry)
	{
		PumpStatus pumpStatus = TryContinuePrevious(entry);
		if (pumpStatus != PumpStatus.ActiveAndContinue)
		{
			if (_player.IsAnyAIPlayer)
			{
				PickNextAndStartPumping(entry);
			}
			else
			{
				InformHumanAboutFinishedPumping(entry, pumpStatus);
			}
		}
	}

	private void InformHumanAboutFinishedPumping(OutpostEntry entry, PumpStatus status)
	{
		if (entry.pump.IsPumping && status == PumpStatus.ActiveAndFinished)
		{
			NodeEntry nodeEntry = entry.targetNodes[entry.pump.nodeIndex];
			_player.territory.RecomputeRespect(nodeEntry.nodeId.FindNode());
		}
		entry.pump.Reset();
		if (status == PumpStatus.ActiveAndFinished)
		{
			bool num = CanStartNewOutpostExpansion(entry.outpostId);
			EntityID buildingId = entry.outpostId.buildingId;
			if (num)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_CAN_EXPAND, TickerTitle.OUTPOST_CAN_EXPAND, Loc.Get("ui.tickers.outpost-expyes"), buildingId);
			}
			else
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_CANNOT_EXPAND, TickerTitle.OUTPOST_CANNOT_EXPAND, Loc.Get("ui.tickers.outpost-expno"), buildingId);
			}
		}
	}

	private void PickNextAndStartPumping(OutpostEntry entry)
	{
		entry.pump.Reset();
		entry.RecomputeNodePriorities(_pid);
		for (int i = 0; i < entry.targetNodes.Count; i++)
		{
			if (!SkipExpansionDueToTerritory(entry, i) && PumpUpRespect(entry, i, instant: false) && i != 0)
			{
				entry.pump.Set(i, Label.NULL);
				break;
			}
		}
	}

	private (bool success, int index) FindNextCornerForExpansion(OutpostEntry entry)
	{
		if (entry.pump.IsPumping)
		{
			return (success: false, index: -1);
		}
		for (int i = 0; i < entry.targetNodes.Count; i++)
		{
			if (!SkipExpansionDueToTerritory(entry, i) && PumpUpRespect(entry, i, instant: false, dryRun: true) && i != 0)
			{
				return (success: true, index: i);
			}
		}
		return (success: true, index: -1);
	}

	private bool SkipExpansionDueToTerritory(OutpostEntry entry, int i)
	{
		if (i == 0)
		{
			return false;
		}
		return entry.targetNodes[i].nodeId.FindNode().owner.IsSet;
	}

	private PumpStatus TryContinuePrevious(OutpostEntry entry)
	{
		if (!entry.pump.IsPumping)
		{
			return PumpStatus.NotActive;
		}
		int nodeIndex = entry.pump.nodeIndex;
		Label expId = entry.pump.expId;
		if (PumpUpRespect(entry, nodeIndex, instant: false))
		{
			entry.targetNodes[nodeIndex].SetExpansion(expId, _player.IsHuman);
			return PumpStatus.ActiveAndContinue;
		}
		return PumpStatus.ActiveAndFinished;
	}

	private bool PumpUpRespect(OutpostEntry entry, int index, bool instant, bool dryRun = false)
	{
		NodeEntry nodeEntry = entry.targetNodes[index];
		ModQuery q = new ModQuery(_pid, nodeEntry.nodeId);
		Fixnum fixnum = nodeEntry.current;
		Fixnum fixnum2 = nodeEntry.current;
		var (fixnum3, fixnum4) = _outposts.GetCurrentRespect(nodeEntry);
		var (fixnum5, fixnum6, fixnum7, fixnum8) = FindRespectTargets(index, OutpostSettings, q);
		if (fixnum4 < fixnum5 && instant)
		{
			fixnum2 = fixnum5 - fixnum3;
		}
		if (fixnum4 < fixnum5 && !instant)
		{
			fixnum2 += fixnum7;
		}
		if (fixnum3 >= fixnum6)
		{
			fixnum2 -= fixnum8;
		}
		_ = fixnum3 > fixnum6 * 2;
		if (fixnum2 < 0)
		{
			fixnum2 = 0;
		}
		if (instant)
		{
			fixnum = fixnum2;
		}
		if (!dryRun)
		{
			nodeEntry.SetOrClearPumped(fixnum, fixnum2);
		}
		return fixnum2 > fixnum;
	}

	private static (Fixnum target, Fixnum max, Fixnum deltaup, Fixnum deltadown) FindRespectTargets(int index, OutpostSettings settings, ModQuery q)
	{
		bool flag = index == 0;
		Fixnum fixnum = settings.pumpPerDayUp.Evaluate(q) * Game.ctx.clock.DaysPerTurn;
		Fixnum item = settings.pumpPerDayDown.Evaluate(q) * Game.ctx.clock.DaysPerTurn;
		Fixnum fixnum2 = Fixnum.Max(0, (flag ? settings.respTargetAtOutpost : settings.respTargetNearOutpost).Evaluate(q));
		Fixnum item2 = fixnum2 + fixnum;
		return (target: fixnum2, max: item2, deltaup: fixnum, deltadown: item);
	}

	private void CollectTributesFromBizToOutposts(OutpostEntry entry, List<EntityID> alreadyPaid)
	{
		foreach (NodeEntry targetNode in entry.targetNodes)
		{
			foreach (var item2 in GetEachBizAtThisNode(targetNode.nodeId.FindNode()))
			{
				Entity item = item2.biz;
				if (!alreadyPaid.Contains(item.Id))
				{
					CollectTributeFromSingleBizToOutpost(entry, item);
					alreadyPaid.Add(item.Id);
				}
			}
		}
	}

	private void CollectTributeFromSingleBizToOutpost(OutpostEntry entry, Entity biz)
	{
		if (biz != null && IsBizPayingTribute(biz))
		{
			Fixnum cash = biz.data.biz.tribute.amount.cash;
			entry.money.collected += cash;
		}
	}

	private bool ProcessOutpostCosts(OutpostEntry entry)
	{
		Fixnum fixnum = ComputeOutpostCost(entry);
		entry.money.expenses += fixnum;
		entry.money.months++;
		if (entry.money.Delta < 0 && entry.money.months == 3)
		{
			WarnPlayerAboutShutdown(entry);
			return true;
		}
		if (entry.money.Delta < 0 && entry.money.months == 2)
		{
			WarnPlayerAboutFutureShutdown(entry);
			return false;
		}
		return false;
	}

	public Price FindMonthlyOutpostCost(OutpostID outpostId)
	{
		return new Price(ComputeOutpostCost(_outposts.FindByOutpostID(outpostId)));
	}

	private Fixnum ComputeOutpostCost(OutpostEntry entry)
	{
		ModQuery modQuery = entry.MakeOutpostQuery(_pid);
		Fixnum zERO = Fixnum.ZERO;
		OutpostSettings outpostSettings = OutpostSettings;
		int i = 0;
		for (int count = entry.targetNodes.Count; i < count; i++)
		{
			NodeEntry nodeEntry = entry.targetNodes[i];
			if (i == 0)
			{
				zERO += outpostSettings.monthlyCost.Evaluate(modQuery);
			}
			else if (nodeEntry.IsPumped && nodeEntry.expansionId.IsSet)
			{
				zERO += (outpostSettings.FindExpansion(nodeEntry.expansionId)?.FindMonthlyCost(modQuery) ?? Price.ZERO).cash;
			}
		}
		return zERO;
	}

	private void WarnPlayerAboutFutureShutdown(OutpostEntry entry)
	{
		if (_player.IsHuman)
		{
			Game.ctx.sfx.PlayWarning();
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_URGENT, TickerTitle.OUTPOST_NOTICE, Loc.Get("ui.tickers.outpost.shutdown-soon"), entry.outpostId.buildingId);
		}
	}

	private void WarnPlayerAboutShutdown(OutpostEntry entry)
	{
		if (_player.IsHuman)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_URGENT, TickerTitle.OUTPOST_SHUTDOWN, Loc.Get("ui.tickers.outpost.shutdown-done"), entry.outpostId.buildingId);
		}
	}

	private void WarnPlayerAboutStolenOutpost(OutpostEntry entry, PlayerID thief)
	{
		if (_player.IsHuman)
		{
			string text = thief.FindPlayer().social.FindPlayerGroupNameColorized();
			string message = Loc.Get("ui.tickers.outpost.shutdown-stolen", "groupname", text);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_STOLEN, TickerTitle.OUTPOST_STOLEN, message, entry.outpostId.buildingId, TickerPersistType.Persist);
		}
	}

	private Price EvalCost(ModValue value, VisitState visit)
	{
		return new Price(value.Evaluate(visit.MakeOwnerModQuery()));
	}

	public Price FindMonthlyTribute(VisitState visit)
	{
		return EvalCost(OutpostSettings.tributePerBiz, visit);
	}

	private static IEnumerable<(Entity building, Entity biz)> GetEachBizAtThisNode(Node node)
	{
		foreach (EntityID item in node.interesting)
		{
			Entity entity = item.FindEntity();
			if (entity != null)
			{
				Entity entity2 = BuildingUtil.FindBizForBuilding(entity);
				if (entity2 != null)
				{
					yield return (building: entity, biz: entity2);
				}
			}
		}
	}

	private static IEnumerable<(Entity building, Entity biz)> GetEachBizThatPaysOutpostExpensive(OutpostID outpost)
	{
		return from biz in Game.ctx.entityman.GetCachedEntitiesBizUnsafe()
			where biz.data.biz.tribute != null && biz.data.biz.tribute.outpost == outpost
			select (BuildingUtil.FindBuildingForBiz(biz), biz: biz);
	}
}

using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.AI;
using Game.Session.Player.Commands;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player.KB;

public class PlayerKB : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	private struct QueryContext
	{
		public VisitState buildingVisit;

		public Node cornerNode;
	}

	private PlayerKBCache _cache = new PlayerKBCache();

	private UISettings Settings => Game.serv.globals.ui;

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		if (_pid.IsHumanPlayer)
		{
			AddEventListeners();
		}
	}

	public override void OnPreRelease()
	{
		base.OnPreRelease();
		if (_pid.IsHumanPlayer)
		{
			RemoveEventListeners();
		}
	}

	private void AddEventListeners()
	{
		foreach (SessionEventType item in Settings.FindEventsThatResetQueries())
		{
			Game.ctx.events.AddListener(item, OnResetEvent);
		}
	}

	private void RemoveEventListeners()
	{
		foreach (SessionEventType item in Settings.FindEventsThatResetQueries())
		{
			Game.ctx.events.RemoveListener(item, OnResetEvent);
		}
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		if (loaded && _pid.IsHumanPlayer)
		{
			Game.ctx.events.EnqueueOnce(SessionEventType.PlayerKBChanged, _pid);
		}
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		if (Game.ctx.clock.State.pid.IsHumanPlayer)
		{
			Clear();
			Game.ctx.events.EnqueueOnce(SessionEventType.PlayerKBChanged, _pid);
		}
	}

	public void OnPlayerTurnEnded()
	{
		if (Game.ctx.clock.State.pid.IsHumanPlayer)
		{
			Clear();
		}
	}

	public bool IsEmpty()
	{
		if (_cache.cornerData.Count == 0)
		{
			return _cache.buildingData.Count == 0;
		}
		return false;
	}

	public void Clear()
	{
		_cache.ClearAll();
	}

	private void OnResetEvent(SessionEvent sev)
	{
		if (sev.pid != _pid)
		{
			return;
		}
		List<Label> list = Settings.FindQueriesResetByEvent(sev.type);
		if (list == null || list.Count == 0)
		{
			Logger.Warning("Missing handlers for subscribed reset event", sev.type);
			return;
		}
		foreach (Label item in list)
		{
			ResetAll(item);
		}
	}

	private void ResetAll(Label qid)
	{
		foreach (KeyValuePair<EntityID, PlayerKBCache.BuildingEntries> buildingDatum in _cache.buildingData)
		{
			ResetBuilding(qid, buildingDatum.Value);
		}
		foreach (KeyValuePair<NodeID, PlayerKBCache.CornerEntries> cornerDatum in _cache.cornerData)
		{
			ResetCorner(qid, cornerDatum.Value);
		}
	}

	private void ResetBuilding(Label qid, PlayerKBCache.BuildingEntries e)
	{
		if (e.Remove(qid))
		{
			PropagateToCorner(qid, e.GetNodeID());
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerKBChanged, _pid);
	}

	private void ResetCorner(Label qid, PlayerKBCache.CornerEntries c)
	{
		c.Remove(qid);
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerKBChanged, _pid);
	}

	private void PropagateToCorner(Label _1, NodeID _2)
	{
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerKBChanged, _pid);
	}

	internal void FlushAllForBuilding(Entity building)
	{
		_cache.ClearEntriesForBuildingIfExist(building.Id);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerKBChangedOnBuilding, building.Id, _pid));
	}

	public bool QReqShouldStart(EntityID building)
	{
		return GetStatusForBuilding(building, PlayerKBQueryNames.QREQ_SHOULD_START).passed;
	}

	public bool QReqShouldFinish(EntityID building)
	{
		return GetStatusForBuilding(building, PlayerKBQueryNames.QREQ_SHOULD_FINISH).passed;
	}

	public bool HealShouldPrompt(VisitState state)
	{
		Entity entity = state.crew.peepId.FindEntity();
		if (entity == null)
		{
			return false;
		}
		bool num = entity.data.agent.health < 100;
		bool isEnabled = HumanCommandValidator.FindValidator(CommandType.Heal).Validate(state.pid, state.crew).IsEnabled;
		return num && isEnabled;
	}

	public bool ShouldShowPip(EntityID building, Label queryId)
	{
		return GetStatusForBuilding(building, queryId).showpip;
	}

	public KBResult GetStatusForCorner(Node node, Label queryId)
	{
		if (!Game.ctx.IsInteractive)
		{
			return default(KBResult);
		}
		PlayerKBCache.CornerEntries entriesForCorner = _cache.GetEntriesForCorner(node.id);
		KBResult result = entriesForCorner.Get(queryId);
		if (result.IsValid)
		{
			return result;
		}
		using (ListPool<KBResult>.PooledBlockList pooledBlockList = ListPool<KBResult>.Allocate())
		{
			RunQueries(MakeKBQueryContext(node), queryId, pooledBlockList);
			foreach (KBResult item in pooledBlockList)
			{
				if (item.IsValid)
				{
					entriesForCorner.Upsert(item);
				}
			}
		}
		return entriesForCorner.Get(queryId);
	}

	public KBResult GetStatusForBuilding(EntityID buildingId, Label queryId)
	{
		if (!Game.ctx.IsInteractive)
		{
			return default(KBResult);
		}
		PlayerKBCache.BuildingEntries entriesForBuilding = _cache.GetEntriesForBuilding(buildingId);
		KBResult result = entriesForBuilding.Get(queryId);
		if (result.IsValid)
		{
			return result;
		}
		using (ListPool<KBResult>.PooledBlockList pooledBlockList = ListPool<KBResult>.Allocate())
		{
			RunQueries(MakeKBQueryContext(buildingId), queryId, pooledBlockList);
			foreach (KBResult item in pooledBlockList)
			{
				if (item.IsValid)
				{
					entriesForBuilding.Upsert(item);
				}
			}
		}
		return entriesForBuilding.Get(queryId);
	}

	private void RunQueries(QueryContext ctx, Label queryId, List<KBResult> results)
	{
		UIQuery uIQuery = Game.serv.globals.ui.playerQueries.FindOrNull(queryId);
		if (uIQuery == null)
		{
			Logger.Warning("Invalid player query id", queryId);
			return;
		}
		switch (uIQuery.type)
		{
		case UIQuery.Type.ConvoQuery:
		{
			bool flag = queryId == PlayerKBQueryNames.QREQ_SHOULD_FINISH || queryId == PlayerKBQueryNames.QREQ_SHOULD_START || queryId == PlayerKBQueryNames.OWNER_ACTIVE_QUEST || queryId == PlayerKBQueryNames.OWNER_EXPIRED_QUEST;
			if (ctx.buildingVisit != null && ctx.buildingVisit.building.components.building.GetBuildingType() != BuildingComponent.BuildingTypeFlags.Business && !flag)
			{
				results.Add(ShowIfPassing(queryId, pass: false));
				break;
			}
			bool pass = uIQuery.visreqs == null || uIQuery.visreqs.AllPass(ctx.buildingVisit);
			results.Add(ShowIfPassing(queryId, pass));
			break;
		}
		case UIQuery.Type.BuildingFn:
			RunBuildingTest(ctx.buildingVisit, queryId, uIQuery, results);
			break;
		case UIQuery.Type.CornerFn:
			RunCornerTest(ctx.cornerNode, queryId, uIQuery, results);
			break;
		}
	}

	private void RunBuildingTest(VisitState visit, Label queryId, UIQuery q, List<KBResult> results)
	{
		switch (q.testfn)
		{
		case UIQuery.Test.QreqStart:
		{
			bool flag3 = visit.building.components.building.IsSafehouseOrControlledNotBy(PlayerID.HumanPlayer);
			bool pass2 = Game.ctx.quests.Requests.ShouldPresentRequestStart(visit) && !flag3;
			results.Add(ShowIfPassing(queryId, pass2));
			break;
		}
		case UIQuery.Test.QreqFinish:
		{
			bool flag4 = visit.building.components.building.IsSafehouseOrControlledNotBy(PlayerID.HumanPlayer);
			bool pass3 = Game.ctx.quests.Requests.ShouldPresentRequestFinish(visit) && !flag4;
			results.Add(ShowIfPassing(queryId, pass3));
			break;
		}
		case UIQuery.Test.ProductionStalled:
		{
			bool pass = ((visit.building?.components.modules?.LastModuleResult).GetValueOrDefault() & ModuleResult.MfgOutOfInputs) != 0;
			results.Add(ShowIfPassing(queryId, pass));
			break;
		}
		case UIQuery.Test.BuildingDamaged:
		{
			PlayerInfo player = visit.GetPlayer();
			bool flag = player.territory.IsControlled(visit.building) && player.territory.IsBuildingDamaged(visit.building);
			bool flag2 = flag && !player.territory.CanBeRepaired(visit.building);
			results.Add(ShowIfPassing(queryId, flag && flag2));
			break;
		}
		case UIQuery.Test.CopPaidOff:
		{
			PlayerInfo player3 = visit.GetPlayer();
			if (visit.building.config.police != null)
			{
				bool pass5 = visit.building.data.police.copPlayerId.FindPlayer().ai.precinct.HasDonationFrom(player3.PID) == DonationState.PaidOff;
				results.Add(ShowIfPassing(queryId, pass5));
			}
			break;
		}
		case UIQuery.Test.HealPrompt:
		{
			PlayerInfo player2 = visit.GetPlayer();
			bool flag5 = false;
			bool isHumanPlayer = visit.building.data.building.controlled.pid.IsHumanPlayer;
			foreach (CrewAssignment item in player2.crew.AllCrew)
			{
				if (item.IsNotDead && item.peepId.FindEntity().data.agent.health <= 50)
				{
					flag5 = true;
				}
			}
			results.Add(ShowIfPassing(queryId, flag5 && isHumanPlayer));
			break;
		}
		case UIQuery.Test.CasinoOutOfCash:
		{
			GamblingModule gamblingModule = visit.building.components.modules?.gambling;
			if (gamblingModule == null)
			{
				results.Add(ShowIfPassing(queryId, pass: false));
				break;
			}
			Fixnum fixnum = gamblingModule.FindMinimumOperationalValue(visit);
			Fixnum cash = ModulesUtil.GetInventory(visit.building).data.money.cash;
			bool pass4 = fixnum > cash;
			results.Add(ShowIfPassing(queryId, pass4));
			break;
		}
		case UIQuery.Test.CanadaRelated:
		{
			Entity entity = BuildingUtil.FindBizOwnerForBuilding(visit.building);
			Relationship relationshipFromSourceToPlayer = _player.social.GetRelationshipFromSourceToPlayer(entity?.Id ?? EntityID.INVALID);
			if (relationshipFromSourceToPlayer == null)
			{
				results.Add(ShowIfPassing(queryId, pass: false));
				break;
			}
			{
				foreach (Label cANADA_RELBUFF in BuffConstants.CANADA_RELBUFFS)
				{
					if (relationshipFromSourceToPlayer.HasBuff(cANADA_RELBUFF))
					{
						results.Add(ShowIfPassing(queryId, pass: true));
						break;
					}
				}
				break;
			}
		}
		case UIQuery.Test.BizRecentTradeHuman:
			MakeBizRecentTradeResults(visit, human: true, results);
			break;
		case UIQuery.Test.BizRecentTradeAi:
			MakeBizRecentTradeResults(visit, human: false, results);
			break;
		case UIQuery.Test.FrontDataUpdate:
			MakeOutpostResults(visit, results);
			break;
		case UIQuery.Test.DeliveryUpdate:
			MakeDeliveryUpdateResults(visit, results);
			break;
		case UIQuery.Test.ControlledUpdate:
			MakeControlledUpdateResults(visit, results);
			break;
		case UIQuery.Test.ReseventUpdate:
			MakeResEventResults(visit, results);
			break;
		default:
			Logger.Warning("Unknown Building Test", q.testfn, queryId);
			break;
		}
	}

	private void MakeOutpostResults(VisitState visit, List<KBResult> results)
	{
		(OutpostOwner owner, OutpostState state) tuple = visit.GetPlayer().outposts.GenerateOutpostInfo(visit.building);
		OutpostOwner item = tuple.owner;
		OutpostState item2 = tuple.state;
		bool flag = item == OutpostOwner.ExtortedByHuman;
		bool passed = item == OutpostOwner.OutpostHuman;
		bool flag2 = item == OutpostOwner.OutpostAI;
		results.Add(new KBResult(PlayerKBQueryNames.BIZ_EXTORTED, valid: true, flag, flag));
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_IS_AI, valid: true, flag2, flag2));
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_IS_HUMAN, valid: true, passed, showpip: false));
		bool flag3 = item2 == OutpostState.NeedToSupportUrgent;
		bool flag4 = item2 == OutpostState.NeedToSupport;
		bool flag5 = item2 == OutpostState.NeedToCollect;
		bool flag6 = item2 == OutpostState.Expanding;
		bool flag7 = item2 == OutpostState.CanExpand;
		bool flag8 = item2 == OutpostState.Active;
		bool flag9 = flag3;
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_SUPPORT_URGENT, valid: true, flag3, flag9));
		bool flag10 = !flag9 && flag4;
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_SUPPORT, valid: true, flag4, flag10));
		bool flag11 = !flag10 && !flag9 && flag5;
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_COLLECT, valid: true, flag5, flag11));
		bool flag12 = !flag9 && !flag10 && !flag11 && flag6;
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_EXPANDING, valid: true, flag6, flag12));
		bool flag13 = !flag9 && !flag10 && !flag11 && flag7;
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_CAN_EXPAND, valid: true, flag7, flag13));
		bool showpip = !flag9 && !flag10 && !flag11 && !flag12 && !flag13 && flag8;
		results.Add(new KBResult(PlayerKBQueryNames.OUTPOST_ACTIVE, valid: true, flag8, showpip));
	}

	private void MakeDeliveryUpdateResults(VisitState visit, List<KBResult> results)
	{
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		DeliveryData.RememberedDelivery? rememberedDelivery = visit.building?.components.delivery?.GetLastHumanDelivery(Game.ctx.clock.Now);
		if (rememberedDelivery.HasValue)
		{
			DeliveryData.RememberedDelivery value = rememberedDelivery.Value;
			flag2 = value.insufficient;
			flag3 = value.failed;
			flag = !flag2 && !flag3;
		}
		bool flag4 = flag3;
		results.Add(new KBResult(PlayerKBQueryNames.BIZ_DELIVERY_FAILED, valid: true, flag3, flag4));
		bool flag5 = !flag4 && flag2;
		results.Add(new KBResult(PlayerKBQueryNames.BIZ_DELIVERY_INSUFFICIENT, valid: true, flag2, flag5));
		bool showpip = !flag4 && !flag5 && flag;
		results.Add(new KBResult(PlayerKBQueryNames.BIZ_DELIVERY_SUCCESS, valid: true, flag, showpip));
	}

	private void MakeControlledUpdateResults(VisitState visit, List<KBResult> results)
	{
		bool flag = false;
		bool flag2 = false;
		if (visit.building.components.building.IsControlledBy(PlayerID.HumanPlayer))
		{
			foreach (IBizModule bizModule in ModulesUtil.GetBizModules(visit.building))
			{
				if (!flag2)
				{
					List<AddModuleDef> list = ModulesUtil.FindUpgradesOrNull(bizModule, visit, visit.time);
					flag2 = list != null && list.Count > 0;
				}
			}
			foreach (IModule item in visit.building.components.modules.GetAllSlotsUnsafe())
			{
				if (!flag)
				{
					flag = item == null;
				}
			}
		}
		results.Add(ShowIfPassing(PlayerKBQueryNames.CTRL_EMPTY_SLOT, flag));
		results.Add(ShowIfPassing(PlayerKBQueryNames.CTRL_UPGRADE_MODULE, flag2));
	}

	private void MakeResEventResults(VisitState visit, List<KBResult> results)
	{
		bool pass = Game.ctx.simman.resevents.CanPlayerAttendThisTurn(visit.building);
		results.Add(ShowIfPassing(PlayerKBQueryNames.RESEVENT_READY, pass));
	}

	private void MakeBizRecentTradeResults(VisitState visit, bool human, List<KBResult> results)
	{
		BizComponent bizComponent = visit.biz?.components.biz;
		if (human)
		{
			bool pass = false;
			results.Add(ShowIfPassing(PlayerKBQueryNames.BIZ_RECENT_TRADE_HUMAN, pass));
		}
		else
		{
			bool pass2 = bizComponent?.HasAnyTradeHistoryWithAI() ?? false;
			results.Add(ShowIfPassing(PlayerKBQueryNames.BIZ_RECENT_TRADE_AI, pass2));
		}
	}

	private void RunCornerTest(Node node, Label queryId, UIQuery q, List<KBResult> results)
	{
		switch (q.testfn)
		{
		case UIQuery.Test.TerritoryUpdate:
			MakeTerritoryResults(node, results);
			return;
		case UIQuery.Test.HeatUpdate:
			MakeHeatResults(node, results);
			return;
		}
		Logger.Warning("Unknown Corner Test", q.testfn, queryId);
	}

	private void MakeTerritoryResults(Node node, List<KBResult> results)
	{
		PlayerID pid = node.owner.pid;
		bool isAnyPlayer = pid.IsAnyPlayer;
		bool isHumanPlayer = pid.IsHumanPlayer;
		bool isAIPlayer = pid.IsAIPlayer;
		bool isHumanPlayer2 = node.owner.lastpid.IsHumanPlayer;
		bool num = node.owner.updated.days == Game.ctx.clock.Now.days;
		bool flag = num && !isHumanPlayer2 && isHumanPlayer;
		bool flag2 = num && isHumanPlayer2 && !isAIPlayer && !isHumanPlayer;
		bool flag3 = num && isHumanPlayer2 && isAIPlayer;
		bool pass = !isAnyPlayer && node.respect.IsAnyPositive() && Game.ctx.players.Human.outposts.CurrentlyExpandingInto(node);
		results.Add(ShowIfPassing(PlayerKBQueryNames.CORNER_TERRITORY_EXPANDED, flag));
		results.Add(ShowIfPassing(PlayerKBQueryNames.CORNER_TERRITORY_CONTRACTED, flag2));
		results.Add(ShowIfPassing(PlayerKBQueryNames.CORNER_TERRITORY_STOLEN, flag3));
		results.Add(ShowIfPassing(PlayerKBQueryNames.CORNER_TERRITORY_PUMPING, pass));
		bool showpip = isAnyPlayer && !(flag || flag2 || flag3);
		results.Add(new KBResult(PlayerKBQueryNames.CORNER_TERRITORY_OWNED, valid: true, isAnyPlayer, showpip));
	}

	private void MakeHeatResults(Node node, List<KBResult> results)
	{
		RaidChecker.RaidPossibility raidPossibility = RaidChecker.CalculateRaidPossibility(node, PlayerID.HumanPlayer);
		bool flag = raidPossibility.goalHeat > raidPossibility.currentHeat;
		bool flag2 = raidPossibility.goalHeat <= raidPossibility.currentHeat;
		bool heatNearOrAboveRaidThreshold = raidPossibility.heatNearOrAboveRaidThreshold;
		bool heatAboveRaidThreshold = raidPossibility.heatAboveRaidThreshold;
		bool recentlyRaided = raidPossibility.recentlyRaided;
		bool flag3 = heatAboveRaidThreshold;
		results.Add(new KBResult(PlayerKBQueryNames.CORNER_HEAT_COPS_SOON, valid: true, heatAboveRaidThreshold, flag3));
		bool flag4 = recentlyRaided && !flag3;
		results.Add(new KBResult(PlayerKBQueryNames.CORNER_HEAT_COPS_RECENT, valid: true, recentlyRaided, flag4));
		bool flag5 = heatNearOrAboveRaidThreshold && flag;
		bool showpip = flag5 && !flag4 && !flag3;
		results.Add(new KBResult(PlayerKBQueryNames.CORNER_HEAT_UP, valid: true, flag5, showpip));
		bool flag6 = heatNearOrAboveRaidThreshold && flag2;
		bool showpip2 = flag6 && !flag4 && !flag3;
		results.Add(new KBResult(PlayerKBQueryNames.CORNER_HEAT_DOWN, valid: true, flag6, showpip2));
	}

	private KBResult ShowIfPassing(Label queryId, bool pass)
	{
		return new KBResult(queryId, valid: true, pass, pass);
	}

	private KBResult ShowIfFailing(Label queryId, bool pass)
	{
		return new KBResult(queryId, valid: true, pass, !pass);
	}

	private QueryContext MakeKBQueryContext(EntityID buildingId)
	{
		return new QueryContext
		{
			buildingVisit = new VisitState(CrewAssignment.EMPTY, BuildingUtil.FindDataForBuilding(buildingId), Game.ctx.clock.Now, _pid)
		};
	}

	private QueryContext MakeKBQueryContext(Node node)
	{
		return new QueryContext
		{
			cornerNode = node
		};
	}
}

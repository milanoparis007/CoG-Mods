using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class PrecinctAdvisor : AIAdvisor
{
	private PrecinctAdvisorData _data;

	private PrecinctAdvisorConfig _def;

	private const float MAX_SPAN = 28f;

	public EntityID StationBuilding => _data.stationId;

	public PrecinctID PrecinctID => _data.precinctId;

	private PoliceSettings PoliceSettings => Game.serv.globals.settings.people.social.police;

	private bool HasStation => _data.stationId.IsValid;

	public PrecinctAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = Game.serv.globals.settings.npc.FindAdvisorConfig<PrecinctAdvisorConfig>(def.precinct);
		_data = _manager.Data.precinct;
		foreach (EntityID officer in GetOfficers())
		{
			_data.GetBeat(officer).ResetNextBeat();
		}
	}

	public NodeID GetPrecinctBuildingNodeID()
	{
		return _data.stationId.FindEntity().components.board.GetNodeID();
	}

	public string GetPrecinctName(bool colorized = true, float brightness = 0.5f)
	{
		string text = Loc.Get("pattern-precinct", "number", Loc.FormatNumber(_data.precinctId.id));
		if (colorized)
		{
			text = _player.social.WrapInPlayerColor(text, brightness);
		}
		return text;
	}

	public override void OnTurnUpdate()
	{
		TryExpireDonations();
		ModValue raidCheckCooldownDayz = RaidChecker.Settings.raidCheckCooldownDayz;
		CallWithCooldown(TryNextRaidTarget, ref _data.nextRaidCheck, raidCheckCooldownDayz, raidCheckCooldownDayz);
	}

	private void TryNextRaidTarget()
	{
		if (HasStation && _data.nextRaidTarget.IsNotValid)
		{
			_data.nextRaidTarget = RaidChecker.FindRaidTarget(this, _data);
		}
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		var (entity, node) = FindCarToCollect();
		if (entity != null)
		{
			results.Add(new AdvisorRequest(this, ScriptNames.COP_COLLECT, AdvisorRequest.Priority.PoliceCollectVehicle, new Deictics
			{
				targetNode = node.id,
				targetVehicle = entity.Id
			}));
		}
		if (_data.nextRaidTarget.IsValid)
		{
			RaidTarget andClear = _data.GetAndClear(ref _data.nextRaidTarget);
			AILog.LogAIDecision(_pid, this, $"Cop raid at {andClear.nid}");
			results.Add(new AdvisorRequest(this, ScriptNames.COP_RAID, AdvisorRequest.Priority.PoliceOrFedVisit, new Deictics
			{
				targetNode = andClear.nid,
				number = andClear.duration.deltadays
			}));
		}
		int i = 0;
		for (int count = GetOfficers().Count; i < count; i++)
		{
			results.Add(new AdvisorRequest(this, ScriptNames.COP_BEAT));
		}
	}

	private (Entity carToCollect, Node nodeLocation) FindCarToCollect()
	{
		using ListPool<EntityID>.PooledBlockList pooledBlockList = ListPool<EntityID>.Allocate();
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			pooledBlockList.AddRange(item.crew.AllScavengeableCars);
		}
		if (pooledBlockList.Count != 0)
		{
			_data.rng.Shuffle(pooledBlockList);
			foreach (EntityID item2 in pooledBlockList)
			{
				Entity entity = item2.FindEntity();
				Node node = Game.ctx.board.nodes.FindNearestNodeAround(entity.data.mobile.worldpos, 10f);
				if (node != null && node.precinctId == _data.precinctId)
				{
					return (carToCollect: entity, nodeLocation: node);
				}
			}
		}
		return (carToCollect: null, nodeLocation: null);
	}

	private Entity GetStation()
	{
		return _data.stationId.FindEntity();
	}

	private List<EntityID> GetOfficers()
	{
		return GetStation()?.data.police.officers ?? new List<EntityID>();
	}

	public IEnumerable<NodeID> EnumerateAllBeatNodesInPrecinct()
	{
		return _data.copbeats.SelectMany((CopBeat entry) => entry.nodes);
	}

	public NodeID GetNextBeatNode(EntityID officer)
	{
		if (!GetOfficers().Contains(officer))
		{
			return NodeID.INVALID;
		}
		CopBeat beat = _data.GetBeat(officer);
		if (beat == null || beat.nodes.Count == 0)
		{
			return NodeID.INVALID;
		}
		int num = 0;
		bool flag = false;
		while (!flag)
		{
			num = beat.GetAndIncrementNextIndex();
			flag = num == 0 || !BlockedFromVisiting(beat.nodes[num]);
		}
		return beat.nodes[num];
		bool BlockedFromVisiting(NodeID nid)
		{
			PlayerID payer = nid.FindNode().owner.Get();
			if (!payer.IsAnyPlayer)
			{
				return false;
			}
			DonationState donationState = HasDonationFrom(payer);
			if (donationState != DonationState.PaidOff)
			{
				return donationState == DonationState.WaitingForRefresh;
			}
			return true;
		}
	}

	public void MarkRaidStart(NodeID nodeId, EntityID officerId)
	{
		Node node = nodeId.FindNode();
		_data.lastRaid = Game.ctx.clock.Now;
		node.GetRaidOrAdd().MarkRaidStart(officerId, nodeId);
		TryArrestsAtNode(node);
		TryAttackCasino(node);
		ProduceRaidEffects(node, officerId);
		RunBusinessIntroductions(node, officerId);
	}

	public void MarkRaidEnd(NodeID nodeId, EntityID officerId)
	{
		nodeId.FindNode().GetRaidOrAdd().MarkRaidEnd(officerId);
	}

	public List<PhotoConfig> GetArrestPhotos()
	{
		return _def.arrestPhotos;
	}

	private bool TryArrestsAtNode(Node node)
	{
		List<EntityID> allAgentsAtNodeUnsafe = Game.ctx.transit.GetAllAgentsAtNodeUnsafe(node.id);
		using (ListPool<EntityID>.PooledBlockList pooledBlockList = ListPool<EntityID>.Allocate())
		{
			foreach (EntityID item in allAgentsAtNodeUnsafe)
			{
				Entity entity = item.FindEntity();
				if (!CopUtil.IsCop(item) && entity.data.person.IsAlive && !PeepBossDonated(entity) && entity.data.agent.pid.FindPlayer().crew.GetCrewForPeep(entity.Id).IsInVehicle)
				{
					pooledBlockList.Add(item);
				}
			}
			foreach (EntityID item2 in pooledBlockList)
			{
				ArrestPeep(item2, node);
			}
			return pooledBlockList.Count > 0;
		}
		bool PeepBossDonated(Entity peep)
		{
			PlayerID payer = peep?.data.agent?.pid ?? PlayerID.INVALID;
			DonationState donationState = HasDonationFrom(payer);
			bool flag = donationState == DonationState.PaidOff || donationState == DonationState.WaitingForRefresh;
			return payer.IsValid && flag;
		}
	}

	private void ArrestPeep(EntityID eid, Node node, bool throwTicker = true)
	{
		Entity entity = eid.FindEntity();
		PlayerInfo player = entity.components.agent.GetPlayer();
		CrewAssignment crew = entity.components.agent.FindCrewAssignment();
		player.commands.FlushQueue(eid, cancelActive: true);
		string text = "";
		Entity vehicle = crew.GetVehicle();
		if (vehicle != null)
		{
			text = ModulesUtil.DescribeInventory(vehicle, addHeader: false);
			ModulesUtil.ClearInventoryResourcesAndCash(ModulesUtil.GetInventory(crew), player);
			NodeID id = player.territory.GetHeadquartersNode().id;
			Game.ctx.transit.SetAgentAtNode(id, eid.FindEntity());
			Game.ctx.transit.TeleportCarToNode(crew.GetVehicle(), id);
			vehicle.components.mobile.UpdateHealthFrom(crew, VehicleHealthSource.FromArrest);
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerVizChanged);
		ModQuery query = new ModQuery(player.PID);
		node.heat.GetOrAdd(player.PID).AddBuff(BuffConstants.HEAT_COP_ARREST, query, crew.peepId);
		entity.components.agent.RememberArrestAtThisTime();
		if (player.IsHuman)
		{
			if (entity.components.agent.IsBoss().pass)
			{
				Game.ctx.events.SendImmediate(SessionEventType.BossArrested);
			}
			List<PhotoConfig> arrestPhotos = GetArrestPhotos();
			Game.serv.ui.AddPopup(new PhotoPopup(arrestPhotos, SFXType.EventPoliceRaid, delegate
			{
				ZoomToHelper(player.territory.GetHeadquartersNode().pos);
			}));
			if (throwTicker)
			{
				string message = Loc.Get("ui.tickers.crew-arrested", "name", entity.data.person.FullName, "describe", text);
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_UPDATE, TickerTitle.COP_UPDATE, message, crew.targetId);
			}
			entity.components.agent.IncrementStat(CrewStats.Arrested, 1);
		}
	}

	private static void ZoomToHelper(WorldPos pos)
	{
		TimerUtil.RunNextFrame(delegate
		{
			HUDUtil.GoTo(pos, zoomIn: true, showFx: true);
		});
	}

	private static void ProduceRaidEffects(Node node, EntityID officerId)
	{
		PlayerInfo playerInfo = CopUtil.FindPrecinctOrNull(officerId);
		if (Game.ctx.players.Human.meetings.IsPlayerMet(playerInfo.PID))
		{
			string message = Loc.Get("ui.tickers.cop-descend", "location", node.GetCornerNameShort());
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_UPDATE, TickerTitle.COP_UPDATE, message, node.id);
			Game.ctx.vfx.PlayOneShotPFX(PFXType.AttackFX, node.pos, PlayerID.HumanPlayer, 1f);
		}
	}

	private static void RunBusinessIntroductions(Node node, EntityID officerId)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindAllBuildings(pooledBlockList);
		foreach (Entity item in pooledBlockList)
		{
			Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(item);
			if (entity != null)
			{
				Game.ctx.simman.rels.GetOrMakeSymmetrical(officerId, entity.Id, RelationshipType.Acquaintance, warnOnExisting: false);
			}
			PlayerInfo playerInfo = SafehouseUtils.FindAnySafehouseAtNode(node)?.components.building.SafehouseOwner.FindPlayer();
			if (playerInfo != null && playerInfo.IsJustGoon)
			{
				playerInfo.ai.Data.goon.rewards?.RaidGoonsLootReward();
			}
		}
	}

	public void TryAttackCasino(Node node)
	{
		Entity entity = FindCasinoAtNodeOrNull(node);
		if (entity != null)
		{
			PlayerID pid = entity.data.building.controlled.pid;
			Entity entity2 = BuildingUtil.FindOwnerOrManagerForAnyBuilding(entity);
			ArrestPeep(entity2.Id, node, throwTicker: false);
			Game.ctx.players.WithID(pid).gambling.ProcessCasinoRaid(entity);
		}
	}

	public static Entity FindCasinoAtNodeOrNull(Node node)
	{
		List<Entity> list = new List<Entity>();
		node.FindAllBuildings(list);
		foreach (Entity item in list)
		{
			if (item.components.modules?.gambling != null)
			{
				return item;
			}
		}
		return null;
	}

	public static void GiveStationToAI(PlayerAI ai, EntityID station)
	{
		PrecinctAdvisorData precinct = ai.Data.precinct;
		PoliceStationComponent police = station.FindEntity().components.police;
		if (police != null && police.HasStation)
		{
			precinct.stationId = station;
			precinct.precinctId = police.entity.data.police.precinctID;
		}
	}

	public static void AssignBeatToOfficer(PlayerAI ai, EntityID officer, List<NodeID> nodes)
	{
		PrecinctAdvisorData precinct = ai.Data.precinct;
		_ = precinct.precinctId;
		foreach (NodeID node in nodes)
		{
			_ = node.FindNode().precinctId;
		}
		if (precinct.HasBeat(officer))
		{
			precinct.copbeats.Remove(precinct.GetBeat(officer));
		}
		precinct.copbeats.Add(new CopBeat(officer, nodes));
	}

	private void TryExpireDonations()
	{
		SimTime now = Game.ctx.clock.Now;
		for (int num = _data.donations.Count - 1; num >= 0; num--)
		{
			CopDonation copDonation = _data.donations[num];
			if (copDonation.expiration < now)
			{
				Relationship item = _player.social.FindOrMakeRelationshipsWith(copDonation.payer).to;
				if (!item.HasBuff(BuffConstants.RELBUFF_COP_AFTERDONATION))
				{
					item.AddBuff(BuffConstants.RELBUFF_COP_AFTERDONATION, EntityID.INVALID);
					if (copDonation.payer.IsHumanPlayer)
					{
						ShowDonationTicker("ui.tickers.cop-donation-expired", copDonation, DonationState.WaitingForRefresh);
					}
				}
			}
			if (copDonation.expiration.IncrementDays(28) < now)
			{
				if (copDonation.payer.IsHumanPlayer)
				{
					ShowDonationTicker("ui.tickers.cop-dono-angry", copDonation, DonationState.NotPaidOff);
				}
				RemoveDonation(copDonation.payer);
			}
		}
	}

	public bool IsBlockingTradesWith(PlayerID visitor)
	{
		return HasDonationFrom(visitor) == DonationState.NotPaidOff;
	}

	public DonationState HasDonationFrom(PlayerID payer)
	{
		CopDonation copDonation = FindDonationFrom(payer);
		if (copDonation == null)
		{
			return DonationState.NotPaidOff;
		}
		if (copDonation.expiration > Game.ctx.clock.Now)
		{
			return DonationState.PaidOff;
		}
		return DonationState.WaitingForRefresh;
	}

	public CopDonation FindDonationFrom(PlayerID payer)
	{
		foreach (CopDonation donation in _data.donations)
		{
			if (donation.payer == payer)
			{
				return donation;
			}
		}
		return null;
	}

	private void AddDonation(PlayerID payer, int days, Price price, bool quiet = false)
	{
		if (HasDonationFrom(payer) == DonationState.PaidOff || HasDonationFrom(payer) == DonationState.WaitingForRefresh)
		{
			RemoveDonation(payer);
		}
		SimTime expiration = Game.ctx.clock.Now.IncrementDays(days);
		CopDonation copDonation = new CopDonation(payer, price, expiration);
		_data.donations.Add(copDonation);
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			_player.commands.FlushQueue(item.peepId, cancelActive: true);
		}
		if (payer.IsHumanPlayer && !quiet)
		{
			ShowDonationTicker("ui.tickers.cop-donation", copDonation, DonationState.PaidOff);
		}
	}

	private void RemoveDonation(PlayerID payer)
	{
		CopDonation item = FindDonationFrom(payer);
		_data.donations.Remove(item);
	}

	internal (Price price, int days) ProduceDonationInfo(VisitState visit)
	{
		return ProduceDonationInfo(visit.GetPlayer());
	}

	private (Price price, int days) ProduceDonationInfo(PlayerInfo other)
	{
		ModQuery query = new ModQuery(other.PID, _player.social.PlayerPeepId, other.social.PlayerPeepId);
		CopDonation copDonation = FindDonationFrom(Game.ctx.players.Human.PID);
		int num = RaidChecker.Settings.donationCost.Evaluate(query).RoundCoarse();
		float num2 = (float)num * PoliceSettings.premium;
		if (copDonation != null)
		{
			_ = copDonation.expiration;
			int deltadays = (copDonation.expiration.IncrementDays(28) - Game.ctx.clock.Now).deltadays;
			num2 = MathUtil.Interpolate((28f - (float)MathUtil.ClampMin(deltadays, 0)) / 28f, 0f, num2);
		}
		int num3 = num + (int)num2;
		return new ValueTuple<Price, int>(item2: RaidChecker.Settings.donationDayz.Evaluate(query).IntCeiling(), item1: num3);
	}

	internal void StartHumanDonation(VisitState visit, int days, Price price)
	{
		PlayerInfo player = visit.GetPlayer();
		if (player.finances.CanChangeMoneyOnCrew(visit, price))
		{
			player.finances.DoChangeMoneyOnCrew(visit, price, MoneyReason.Bribe);
			Game.ctx.events.SendImmediate(SessionEventType.AchieveCopDonation);
			FinalizeDonation(player, visit, price, days, quiet: false);
		}
	}

	internal void StartAIDonation(PlayerInfo other)
	{
		var (price, days) = ProduceDonationInfo(other);
		FinalizeDonation(other, null, price, days, quiet: true);
	}

	private void FinalizeDonation(PlayerInfo other, VisitState visit, Price price, int days, bool quiet)
	{
		AddDonation(other.PID, days, price, quiet);
		_player.social.FindOrMakeRelationshipsWith(other.PID).to.AddBuff(crewpeep: visit?.crew.peepId ?? EntityID.INVALID, buffId: BuffConstants.RELBUFF_COP_DONATION);
	}

	private void ShowDonationTicker(string key, CopDonation donation, DonationState state)
	{
		string playerFullName = _player.social.PlayerFullName;
		string playerGroupName = _player.social.PlayerGroupName;
		SimTime expiration = donation.expiration;
		EntityID playerPeepId = _player.social.PlayerPeepId;
		string message = Loc.Get(key, "name", playerFullName, "precinctname", playerGroupName, "expiration", Loc.FormatDateLong(expiration));
		switch (state)
		{
		case DonationState.PaidOff:
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_UPDATE, TickerTitle.COP_UPDATE, message, playerPeepId);
			break;
		case DonationState.WaitingForRefresh:
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_DONO_EXPIRED, TickerTitle.COP_UPDATE, message, playerPeepId);
			break;
		case DonationState.NotPaidOff:
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_DONO_ANGRY, TickerTitle.COP_UPDATE, message, playerPeepId, TickerPersistType.Persist);
			break;
		}
	}

	public (bool available, SimTime expiration) GetFedHintStatus()
	{
		SimTime expiration = _data.fedhints.expiration;
		return (available: expiration <= Game.ctx.clock.Now, expiration: expiration);
	}

	public SimTimeSpan GetFedHintCooldown()
	{
		return SimTimeSpan.FromDays(PoliceSettings.feds.hintCooldownDayz.Evaluate(_pid).IntFloor());
	}

	public void ProduceAllGangsForFedHint(PlayerID inquirer, List<PlayerID> results)
	{
		foreach (PlayerID item in _player.meetings.GetPlayersAlreadyMet())
		{
			if (item == inquirer)
			{
				continue;
			}
			PlayerInfo playerInfo = item.FindPlayer();
			if (playerInfo.IsJustGang || playerInfo.IsHuman)
			{
				bool num = playerInfo.crew.LivingCrewCount > 0;
				var (flag, flag2) = playerInfo.ai.combat.GetAggroAndTruce(inquirer);
				if (num && flag && !flag2)
				{
					results.Add(item);
				}
			}
		}
	}

	public PlayerID PickRandomGangForFedHint(PlayerID inquirer)
	{
		using ListPool<PlayerID>.PooledBlockList pooledBlockList = ListPool<PlayerID>.Allocate();
		ProduceAllGangsForFedHint(inquirer, pooledBlockList);
		return _data.rng.PickElementOrDefault(pooledBlockList);
	}

	public void StartFedHintHuman(PlayerID target)
	{
		Node node = StartFedHint(target);
		string text = target.FindPlayer()?.social.FindPlayerGroupNameColorized();
		Game.ctx.events.SendImmediate(SessionEventType.AchieveFedHint);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_UPDATE, TickerTitle.COP_UPDATE, Loc.Get("ui.tickers.fed-hint", "groupname", text), node.id);
	}

	public bool StartFedHintAI(PlayerID target, float prob)
	{
		bool num = _data.rng.CheckProbability(prob);
		if (num)
		{
			StartFedHint(target);
		}
		return num;
	}

	private Node StartFedHint(PlayerID target)
	{
		SimTimeSpan fedHintCooldown = GetFedHintCooldown();
		_data.fedhints.lastHint = Game.ctx.clock.Now;
		_data.fedhints.expiration = _data.fedhints.lastHint.Increment(fedHintCooldown);
		Node node = FindFirstFeds().ai.feds.RequestInvestigation(target);
		AILog.LogAIDecision(_pid, this, $"Asking feds to investigate {target} at {node}");
		return node;
	}

	private PlayerInfo FindFirstFeds()
	{
		return Game.ctx.players.all.First((PlayerInfo p) => p.IsJustFed);
	}

	internal void StartHumanPayoff(VisitState visit, EntityID arrested, Price price)
	{
		PlayerInfo player = visit.GetPlayer();
		if (player.finances.CanChangeMoneyOnCrew(visit, price))
		{
			player.finances.DoChangeMoneyOnCrew(visit, price, MoneyReason.Bribe);
			ArrestEntry arrestEntry = Game.ctx.simman.cops.FindArrestOrNull(arrested);
			if (arrestEntry != null)
			{
				arrestEntry.paidOff = true;
			}
		}
	}
}

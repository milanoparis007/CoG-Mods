using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public class CopTracker : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>, ISaveLoadProvider
{
	public CopTrackerPersistedData data;

	private PoliceSettings.Feds FedsSettings = Game.serv.globals.settings.people.social.police.feds;

	public void Initialize(SimulationManager manager)
	{
		data = new CopTrackerPersistedData();
		data.rng = Game.ctx.scenario.MakeSeededRng<CopTrackerPersistedData>();
	}

	public void Release()
	{
	}

	public void OnSystemTurn()
	{
		foreach (ImprisonedEntry item in data.fedImprisoned.Where((ImprisonedEntry e) => e.endDate <= Game.ctx.clock.Now).ToList())
		{
			ProcessRelease(item);
		}
		foreach (ArrestEntry item2 in data.fedArrests.Where((ArrestEntry e) => e.trialDate <= Game.ctx.clock.Now).ToList())
		{
			ProcessTrial(item2);
		}
	}

	public void InformOfStationInstallation(Entity station)
	{
		data.stationIDs.Add(station.Id);
		PrecinctID precinctID = new PrecinctID((short)(++data.stationCount));
		if (station?.data?.police != null)
		{
			station.data.police.precinctID = precinctID;
		}
	}

	public EntityID StationForPrecinct(PrecinctID precinctId)
	{
		foreach (EntityID stationID in data.stationIDs)
		{
			if (stationID.FindEntity().data.police.precinctID.Equals(precinctId))
			{
				return stationID;
			}
		}
		return EntityID.INVALID;
	}

	public IEnumerable<ArrestEntry> FindAllArrestsFor(PlayerID pid)
	{
		return data.fedArrests.Where((ArrestEntry e) => e.playerId == pid);
	}

	public bool IsArrested(EntityID peepId)
	{
		return FindArrestOrNull(peepId) != null;
	}

	public ArrestEntry FindArrestOrNull(EntityID peepId)
	{
		foreach (ArrestEntry fedArrest in data.fedArrests)
		{
			if (fedArrest.peepId == peepId)
			{
				return fedArrest;
			}
		}
		return null;
	}

	public CrewAssignment TryArrestSomeCrew(PlayerInfo player, Entity building)
	{
		using ListPool<CrewAssignment>.PooledBlockList pooledBlockList = ListPool<CrewAssignment>.Allocate();
		pooledBlockList.AddRange(player.crew.GetLiving());
		var (playerID, entity) = ModulesUtil.GetManagerOrNull(building);
		if (entity != null && playerID == player.PID)
		{
			pooledBlockList.Insert(0, player.crew.GetCrewForPeep(entity.Id));
		}
		foreach (CrewAssignment item in pooledBlockList)
		{
			if (CanBeArrested(player, item))
			{
				StartArrest(player, item);
				return item;
			}
		}
		return CrewAssignment.EMPTY;
	}

	public bool CanBeArrested(PlayerInfo player, CrewAssignment crew)
	{
		if (crew.IsInSomewhere)
		{
			return crew.peepId != player.social.PlayerPeepId;
		}
		return false;
	}

	public void StartArrest(PlayerInfo player, CrewAssignment crew)
	{
		if (player.IsHuman)
		{
			StartHumanArrest(player, crew);
		}
		else
		{
			StartAIArrest(player, crew);
		}
	}

	public void StartAIArrest(PlayerInfo player, CrewAssignment crew)
	{
		Entity vehicle = crew.GetVehicle();
		Game.ctx.simman.peoplegen.MarkAsDead(crew.GetPeep(), Game.ctx.clock.Now);
		player.crew.RemoveScavengeableCar(vehicle.Id);
		if (IsInteresting(player))
		{
			ShowNewspaper("newspaper-headline.trial-ai", player, crew.peepId);
		}
	}

	public void StartHumanArrest(PlayerInfo player, CrewAssignment crew)
	{
		Node node = crew.GetPeep().components.agent.GetNode();
		player.crew.RemoveCrewFromBoard(crew, PlayerCrewData.OffBoardReason.Arrested);
		Fixnum fixnum = FedsSettings.arrestDayz.Evaluate(player.PID, node);
		int num = FedsSettings.arrestBribe.Evaluate(player.PID, node).RoundCoarse();
		SimTime simTime = Game.ctx.clock.Now.IncrementDays(fixnum.IntFloor());
		data.fedArrests.Add(new ArrestEntry
		{
			peepId = crew.peepId,
			playerId = player.PID,
			trialDate = simTime,
			payOffCost = num,
			paidOff = false
		});
		Game.ctx.events.EnqueueOnce(SessionEventType.CrewFedArrest, player.PID, crew.peepId);
		ShowTicker("ui.tickers.feds-raid-humancrew", crew);
		ShowNewspaper("newspaper-headline.trial-human", player, crew.peepId, simTime);
	}

	public void EndArrest(PlayerInfo player, CrewAssignment crew, bool released)
	{
		ArrestEntry item = FindArrestOrNull(crew.peepId);
		data.fedArrests.Remove(item);
		if (released)
		{
			player.crew.ReturnCrewToBoard(crew);
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.CrewFedArrest, player.PID, crew.peepId);
	}

	private bool IsInteresting(PlayerInfo player)
	{
		if (!player.IsHuman)
		{
			return Game.ctx.players.Human.meetings.IsPlayerMet(player.PID);
		}
		return true;
	}

	private void ShowTicker(string key, CrewAssignment crew, int years = 0)
	{
		string fullName = crew.GetPeep().data.person.FullName;
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_UPDATE, TickerTitle.COP_UPDATE, Loc.Get(key, "crewname", fullName, "years", years), default(TickerTarget), TickerPersistType.Persist);
	}

	private void ShowNewspaper(string key, PlayerInfo player, EntityID peepId, SimTime date = default(SimTime), int years = 0)
	{
		PhotoConfig value = FedsSettings.trialPhotos.FirstOrDefaultFast();
		string header = Loc.Get(key, "crewname", peepId.FindEntity().data.person.FullName, "groupname", player.social.PlayerGroupName, "years", years, "date", Loc.FormatDateShort(date));
		Game.serv.ui.AddPopup(new NewspaperPopup(header, value));
	}

	private void ProcessTrial(ArrestEntry record)
	{
		PlayerInfo playerInfo = record.playerId.FindPlayer();
		CrewAssignment crewForPeep = playerInfo.crew.GetCrewForPeep(record.peepId);
		Fixnum probability = (playerInfo.IsHuman ? FedsSettings.trialReleaseProbHuman : FedsSettings.trialReleaseProbGang).Evaluate(record.playerId);
		int num;
		if (!record.paidOff)
		{
			num = (data.rng.CheckProbability(probability) ? 1 : 0);
			if (num == 0)
			{
				goto IL_00b6;
			}
		}
		else
		{
			num = 1;
		}
		EndArrest(playerInfo, crewForPeep, released: true);
		if (playerInfo.IsHuman)
		{
			string key = (record.paidOff ? "newspaper-headline.release-paidoff" : "newspaper-headline.release-random");
			ShowNewspaper(key, playerInfo, crewForPeep.peepId);
			ShowTicker("ui.tickers.feds-release", crewForPeep);
		}
		goto IL_00b6;
		IL_00b6:
		if (num == 0)
		{
			int years = FedsSettings.prisonTimeYears.Evaluate(playerInfo.PID).IntFloor();
			EndArrest(playerInfo, crewForPeep, released: false);
			StartImprisonment(playerInfo, crewForPeep, years);
		}
	}

	private void ProcessRelease(ImprisonedEntry record)
	{
		PlayerInfo playerInfo = record.playerId.FindPlayer();
		CrewAssignment crewForPeep = playerInfo.crew.GetCrewForPeep(record.peepId);
		EndImprisonment(playerInfo, crewForPeep);
	}

	public IEnumerable<ImprisonedEntry> FindAllImprisonedFor(PlayerID pid)
	{
		return data.fedImprisoned.Where((ImprisonedEntry e) => e.playerId == pid);
	}

	public bool IsImprisoned(EntityID peepId)
	{
		return FindImprisonedOrNull(peepId) != null;
	}

	public bool IsArrestedOrImprisoned(EntityID peepId)
	{
		if (!IsArrested(peepId))
		{
			return IsImprisoned(peepId);
		}
		return true;
	}

	public ImprisonedEntry FindImprisonedOrNull(EntityID peepId)
	{
		foreach (ImprisonedEntry item in data.fedImprisoned)
		{
			if (item.peepId == peepId)
			{
				return item;
			}
		}
		return null;
	}

	private void StartImprisonment(PlayerInfo player, CrewAssignment crew, int years)
	{
		SimTime now = Game.ctx.clock.Now;
		SimTime endDate = now.IncrementYears(years);
		data.fedImprisoned.Add(new ImprisonedEntry
		{
			playerId = player.PID,
			peepId = crew.peepId,
			years = years,
			startDate = now,
			endDate = endDate
		});
		player.crew.ChangeOffBoardReason(crew.peepId, PlayerCrewData.OffBoardReason.Jailed);
		if (player.IsHuman)
		{
			ShowNewspaper("newspaper-headline.startprison", player, crew.peepId, default(SimTime), years);
			ShowTicker("ui.tickers.feds-startprison", crew, years);
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.CrewFedArrest, player.PID, crew.peepId);
	}

	private void EndImprisonment(PlayerInfo player, CrewAssignment crew)
	{
		ImprisonedEntry imprisonedEntry = FindImprisonedOrNull(crew.peepId);
		data.fedImprisoned.Remove(imprisonedEntry);
		player.crew.ReturnCrewToBoard(crew);
		if (player.IsHuman)
		{
			ShowNewspaper("newspaper-headline.endprison", player, crew.peepId, default(SimTime), imprisonedEntry.years);
			ShowTicker("ui.tickers.feds-endprison", crew, imprisonedEntry.years);
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.CrewFedArrest, player.PID, crew.peepId);
	}

	public List<EntityID> FindAllArrestedOrImprisonedFor(PlayerID pid)
	{
		IEnumerable<EntityID> first = from e in FindAllArrestsFor(pid)
			select e.peepId;
		IEnumerable<EntityID> second = from e in FindAllImprisonedFor(pid)
			select e.peepId;
		return first.Concat(second).ToList();
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable indata)
	{
		SaveLoadUtils.DeserializeSingleKey(indata, "data", delegate(CopTrackerPersistedData result)
		{
			data = result;
		});
		yield break;
	}
}

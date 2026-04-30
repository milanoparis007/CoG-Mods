using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class ResEventManager : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>
{
	private ResidentialEventSettings Settings => Game.serv.globals.settings.people.residentialEvents;

	public void Initialize(SimulationManager manager)
	{
	}

	public void Release()
	{
	}

	public void OnSystemTurn()
	{
		UpdateAllResEvents();
	}

	private void UpdateAllResEvents()
	{
		if (!Game.ctx.IsInteractive)
		{
			return;
		}
		List<Entity> list = (from building in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe()
			where building.components.residence?.IsEventHostingActive ?? false
			select building).ToList();
		int num = 0;
		foreach (Entity item in list)
		{
			ResEventData resEventOrNull = item.components.residence.GetResEventOrNull();
			if (resEventOrNull.IsExpired)
			{
				RestartEvent(item, resEventOrNull);
				num++;
			}
			if (IsHappeningThisTurn(item) && CanPlayerAttendThisTurn(item))
			{
				PostTicker(item, start: true);
			}
		}
	}

	private void RestartEvent(Entity building, ResEventData data)
	{
		ResidentialEventConfig config = data.GetConfig();
		Xorshift rng = building.data.ident.rng;
		int waitTurns = config.FindWaitTurns(PlayerID.HumanPlayer);
		bool flag = config.ShouldSkip(PlayerID.HumanPlayer, rng);
		data.Start(waitTurns, flag);
		if (!flag)
		{
			PostTicker(building, start: false);
		}
	}

	private void PostTicker(Entity building, bool start)
	{
		ResidentialEventConfig config = GetData(building).GetConfig();
		string key = (start ? config.tickerstart : config.tickerinfo);
		string text = building.components.residence.GetNpcResident()?.data.person.FullName;
		if (start)
		{
			Game.ctx.sfx.PlayPartyImminent();
		}
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.EVENT_UPDATE, TickerTitle.EVENT_UPDATE, Loc.Get(key, "name", text), building.Id);
	}

	public void AssignResEventHost(Label eventId, Entity peep, Entity building, PlayerID pid)
	{
		building.components.residence.SetResEventHost(eventId, peep);
		peep.components.person.SetResAssignment(building);
		if (!building.components.building.IsScopedBy(PlayerID.HumanPlayer))
		{
			PlayerInfo human = Game.ctx.players.Human;
			human.meetings.MarkNodeAsKnown(building.components.board.GetNode(), expectedSeen: true, instant: false);
			human.territory.ScopeOutBuilding(building, procgen: false, setControlled: false);
		}
		pid.FindPlayer().social.AddBuffFrom(peep.Id, BuffConstants.TICKET_NPCBOOST);
	}

	public void UnassignResEventHost(Label eventId, Entity peep, Entity building)
	{
		building.components.residence.ClearResEventHost();
		peep.components.person.ClearResAssignment();
	}

	public ResEventData GetData(Entity building)
	{
		return building?.components.residence?.GetResEventOrNull();
	}

	public bool IsHappeningThisTurn(Entity building)
	{
		return GetData(building)?.IsThisTurn ?? false;
	}

	public bool CanPlayerAttendThisTurn(Entity building)
	{
		if (IsHappeningThisTurn(building))
		{
			return GetData(building).IsAttendanceRegistered;
		}
		return false;
	}

	public ResEventData SetAttendenceRegistered(VisitState visit)
	{
		ResEventData data = GetData(visit.building);
		data.SetAttendanceRegistered();
		Price delta = data.FindPrice(visit.pid);
		visit.GetPlayer().finances.DoChangeMoneyOnCrew(visit, delta, MoneyReason.QuestDemand);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingResEventHappened, visit.building.Id, visit.pid));
		return data;
	}

	public ResEventData SetAttendanceDone(VisitState visit)
	{
		ResEventData data = GetData(visit.building);
		ResidentialEventResultConfig result = PickResult(visit, data);
		ResEventResultData attendanceDone = GenerateResultData(visit, result);
		data.SetAttendanceDone(attendanceDone);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingResEventHappened, visit.building.Id, visit.pid));
		return data;
	}

	public ResidentialEventResultConfig PickResult(VisitState visit, ResEventData data)
	{
		Label id = PickResultOrNone(visit, data);
		if (id.IsNotSet)
		{
			id = ResidentialEventResultConfig.FALLBACK_ID;
		}
		return Settings.FindResult(id);
	}

	private Label PickResultOrNone(VisitState visit, ResEventData data)
	{
		List<Label> list = new List<Label>();
		List<float> list2 = new List<float>();
		foreach (KeyValuePair<Label, float> result in data.GetConfig().results)
		{
			ResidentialEventResultConfig residentialEventResultConfig = Settings.FindResult(result.Key);
			if (residentialEventResultConfig.visreqs == null || residentialEventResultConfig.visreqs.AllPass(visit))
			{
				list.Add(result.Key);
				list2.Add(result.Value);
			}
		}
		if (list.Count == 0)
		{
			return Label.NULL;
		}
		return visit.building.data.ident.rng.PickElement(list, list2);
	}

	public ResEventData ProcessChosenResult(VisitState visit)
	{
		ResEventData data = GetData(visit.building);
		if ((data?.chosenResult?.resultId).HasValue)
		{
			data.chosenResult.GetConfig()?.AcceptResult(visit, data);
			PersonInfoUtil.TweenCameraToEntity(data.chosenResult.targetNpc.id);
		}
		return data;
	}

	public ResEventCandidate? FindPossibleResEventToCreate(PlayerID pid, Entity introducer)
	{
		PlayerInfo playerInfo = pid.FindPlayer();
		Entity playerPeep = playerInfo.social.GetPlayerPeep();
		IRandom identityRNGUnchanging = introducer.components.ident.GetIdentityRNGUnchanging();
		Entity entity = FindPotentialHost(playerPeep, introducer, identityRNGUnchanging);
		if (entity == null)
		{
			return null;
		}
		Entity entity2 = FindPotentialLocation(introducer, identityRNGUnchanging);
		if (entity2 == null)
		{
			return null;
		}
		ResidentialEventConfig residentialEventConfig = FindPotentialEvent(playerInfo, introducer, identityRNGUnchanging);
		if (residentialEventConfig == null)
		{
			return null;
		}
		return new ResEventCandidate
		{
			building = entity2.Id,
			hostNpc = entity.Id,
			eventId = residentialEventConfig.id
		};
	}

	private Entity FindPotentialHost(Entity playerPeep, Entity introducer, IRandom rng)
	{
		List<Entity> list = SocQ.FindPeople(playerPeep, introducer, IsUnknownResEventHostCandidate).ToList();
		return rng.PickElementOrDefault(list);
		static bool IsUnknownResEventHostCandidate(Entity player, Entity other, Entity candidate)
		{
			if (!SocQ.IsNotKnownToPlayer(player, other, candidate))
			{
				return false;
			}
			PersonData person = candidate.data.person;
			if (!person.IsAlive)
			{
				return false;
			}
			SimTime now = Game.ctx.clock.Now;
			if (person.GetAge(now).YearsInt < 20)
			{
				return false;
			}
			if (person.IsEmployed)
			{
				return false;
			}
			PlayerInfo playerInfo = candidate.data.agent.pid.FindPlayer();
			if (playerInfo != null && (playerInfo.IsHuman || playerInfo.IsGangOrGoon))
			{
				return false;
			}
			return true;
		}
	}

	private Entity FindPotentialLocation(Entity introducer, IRandom rng)
	{
		Node node = BuildingUtil.FindBuildingForBizOwner(introducer)?.components.board.GetNode();
		if (node == null)
		{
			return null;
		}
		ListPool<Entity>.PooledBlockList results = ListPool<Entity>.Allocate();
		try
		{
			int resSearchNearbyCorners = Settings.resSearchNearbyCorners;
			Game.ctx.board.nodes.VisitNeighborhoodBFS(node, resSearchNearbyCorners, ProcessNode);
			return rng.PickElementOrDefault(results);
		}
		finally
		{
			if (results != null)
			{
				((IDisposable)results).Dispose();
			}
		}
		void ProcessNode(Node n)
		{
			foreach (EntityID item in n.contained)
			{
				Entity entity = item.FindEntity();
				ResidenceComponent residenceComponent = entity?.components.residence;
				if (residenceComponent != null && residenceComponent.IsEventHostingSpace && !residenceComponent.IsEventHostingActive)
				{
					results.Add(entity);
					break;
				}
			}
		}
	}

	private ResidentialEventConfig FindPotentialEvent(PlayerInfo player, Entity introducer, IRandom rng)
	{
		CrewAssignment crewForPlayerPeep = player.crew.GetCrewForPlayerPeep();
		BuildingAndBusinessData bbdata = BuildingUtil.FindDataForOwner(introducer.Id);
		VisitState visit = new VisitState(crewForPlayerPeep, bbdata, Game.ctx.clock.Now, player.PID);
		List<ResidentialEventConfig> list = Settings.events.Where((ResidentialEventConfig e) => e.visreqs == null || e.visreqs.AllPass(visit)).ToList();
		return rng.PickElementOrDefault(list);
	}

	private ResEventResultData GenerateResultData(VisitState visit, ResidentialEventResultConfig result)
	{
		ResEventResultData resEventResultData = result?.GenerateResultData(visit);
		if (resEventResultData == null)
		{
			resEventResultData = new ResEventResultData
			{
				resultId = result.id
			};
		}
		return resEventResultData;
	}
}

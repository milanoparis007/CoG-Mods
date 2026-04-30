using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Board;

public static class BoardUtil
{
	public static WorldPos? FindBoardPositionFor(EntityID eid)
	{
		return FindBoardInfoFor(eid.FindEntity()).worldpos;
	}

	public static WorldPos? FindBoardPositionFor(Entity entity)
	{
		return FindBoardInfoFor(entity).worldpos;
	}

	public static (Entity boardEntity, WorldPos? worldpos) FindBoardInfoFor(EntityID eid)
	{
		return FindBoardInfoFor(eid.FindEntity());
	}

	public static (Entity boardEntity, WorldPos? worldpos) FindBoardInfoFor(Entity entity)
	{
		if (entity == null)
		{
			return (boardEntity: null, worldpos: null);
		}
		if (entity.data.board != null)
		{
			return (boardEntity: entity, worldpos: entity.data.board.worldpos);
		}
		if (entity.data.mobile != null)
		{
			return (boardEntity: entity, worldpos: entity.data.mobile.worldpos);
		}
		if (entity.data.person != null)
		{
			Entity entity2 = BuildingUtil.FindBuildingForTargetPerson(entity);
			if (entity2 != null)
			{
				return (boardEntity: entity2, worldpos: entity2.data.board.worldpos);
			}
		}
		if (entity.data.biz != null)
		{
			Entity entity3 = BuildingUtil.FindBuildingForBiz(entity);
			if (entity3 != null)
			{
				return (boardEntity: entity3, worldpos: entity3.data.board.worldpos);
			}
		}
		if (entity.data.person != null)
		{
			Entity entity4 = PersonComponent.FindResidenceAssignment(entity);
			if (entity4 != null)
			{
				return (boardEntity: entity4, worldpos: entity4.data.board.worldpos);
			}
		}
		if (entity.data.person != null)
		{
			GamblerState gamblerState = Game.ctx.players.Human.gambling.FindGamblerState(entity);
			if (gamblerState != null)
			{
				Entity entity5 = gamblerState.FindGamblingHouse();
				return (boardEntity: entity5, worldpos: entity5.data.board.worldpos);
			}
		}
		if (entity.data.agent != null)
		{
			CrewAssignment crewAssignment = entity.components.agent.FindCrewAssignment();
			if (crewAssignment.IsInSomewhere)
			{
				Entity target = crewAssignment.GetTarget();
				BoardData board = target.data.board;
				WorldPos? item = ((board != null) ? new WorldPos?(board.worldpos) : target.data.mobile?.worldpos);
				return (boardEntity: target, worldpos: item);
			}
		}
		if (entity.data.agent != null)
		{
			Node node = entity.data.agent.nid.FindNode();
			if (node != null)
			{
				return (boardEntity: entity, worldpos: node.pos);
			}
		}
		AgentData agent = entity.data.agent;
		if (agent != null)
		{
			_ = agent.pid;
			if (true)
			{
				PlayerInfo playerInfo = entity.data.agent.pid.FindPlayer();
				if (playerInfo.IsJustCop)
				{
					Entity entity6 = playerInfo.ai.precinct.StationBuilding.FindEntity();
					return (boardEntity: entity6, worldpos: entity6?.data.board?.worldpos);
				}
				if (playerInfo.IsJustFed)
				{
					Node node2 = playerInfo.ai.feds.GetFakeHeadquartersNodeID().FindNode();
					return (boardEntity: null, worldpos: node2.pos);
				}
			}
		}
		return (boardEntity: null, worldpos: null);
	}

	public static List<EntityID> RivalBlocking(VisitState visit)
	{
		List<EntityID> allAgentsAtNodeUnsafe = Game.ctx.transit.GetAllAgentsAtNodeUnsafe(visit.GetBldgNodeID());
		List<EntityID> list = new List<EntityID>();
		PlayerID playerID = visit.building?.components.building?.OutpostOwner ?? PlayerID.INVALID;
		foreach (EntityID item in allAgentsAtNodeUnsafe)
		{
			if (item.FindEntity().data.agent.pid == playerID)
			{
				list.Add(item);
			}
		}
		return list;
	}
}

using System.Diagnostics;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct ModQuery
{
	public PlayerID pid;

	public EntityID targetId;

	public EntityID crewPeepId;

	public NodeID nodeId;

	public SimTime time;

	private string DebugString => $"ModQuery {pid} / {targetId} / {nodeId} at {time}";

	public ModQuery(PlayerID pid, EntityID targetId, EntityID crewPeepId, NodeID nodeId, SimTime time)
	{
		this.pid = pid;
		this.targetId = targetId;
		this.crewPeepId = crewPeepId;
		this.nodeId = nodeId;
		this.time = time;
	}

	public ModQuery(PlayerID pid, EntityID targetId, EntityID crewPeepId, NodeID nodeId)
		: this(pid, targetId, crewPeepId, nodeId, Game.ctx.clock.Now)
	{
	}

	public ModQuery(PlayerID pid, EntityID targetId, EntityID crewPeepId)
		: this(pid, targetId, crewPeepId, NodeID.INVALID, Game.ctx.clock.Now)
	{
	}

	public ModQuery(PlayerID pid, EntityID targetId, NodeID nodeId)
		: this(pid, targetId, EntityID.INVALID, nodeId, Game.ctx.clock.Now)
	{
	}

	public ModQuery(PlayerID pid, NodeID nodeId)
		: this(pid, EntityID.INVALID, EntityID.INVALID, nodeId, Game.ctx.clock.Now)
	{
	}

	public ModQuery(PlayerID pid, Node node)
		: this(pid, EntityID.INVALID, EntityID.INVALID, node.id, Game.ctx.clock.Now)
	{
	}

	public ModQuery(PlayerID pid)
		: this(pid, EntityID.INVALID, EntityID.INVALID, NodeID.INVALID, Game.ctx.clock.Now)
	{
	}

	public ModQuery SetCrew(EntityID newCrewPeepId)
	{
		return new ModQuery(pid, targetId, newCrewPeepId, nodeId, time);
	}

	public PlayerInfo FindPlayer()
	{
		return pid.FindPlayer();
	}

	public Entity FindTarget()
	{
		return targetId.FindEntity();
	}

	public Entity FindCrewPeep()
	{
		return crewPeepId.FindEntity();
	}

	public Node FindNode()
	{
		return nodeId.FindNode();
	}

	private static (bool isPerson, bool isBuilding, bool isBiz) FindTargetType(Entity entity)
	{
		bool item = entity.components.person != null;
		bool item2 = entity.components.building != null;
		bool item3 = entity.components.biz != null;
		return (isPerson: item, isBuilding: item2, isBiz: item3);
	}

	public Entity FindBuildingForTarget()
	{
		Entity entity = FindTarget();
		var (flag, flag2, flag3) = FindTargetType(entity);
		if (!flag2)
		{
			if (!flag)
			{
				if (!flag3)
				{
					return null;
				}
				return BuildingUtil.FindBuildingForBiz(entity);
			}
			return BuildingUtil.FindBuildingForTargetPerson(entity);
		}
		return entity;
	}
}

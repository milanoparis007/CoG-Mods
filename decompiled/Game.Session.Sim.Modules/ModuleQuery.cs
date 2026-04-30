using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.Session.Sim.Modules;

public struct ModuleQuery
{
	public PlayerID pid;

	public Entity container;

	public NodeID nodeId;

	public Entity npc;

	public Entity manager;

	public bool OwnerIsHumanPlayer => pid.IsHumanPlayer;

	public bool OwnerIsAIPlayer => pid.IsAIPlayer;

	public bool OwnerExists => pid.IsAnyPlayer;

	public bool IsBuilding => container.components.building != null;

	public bool IsVehicle => container.components.mobile != null;

	public ModuleQuery(PlayerID pid, Entity container, NodeID nodeId, Entity npc, Entity manager)
	{
		this.pid = pid;
		this.container = container;
		this.nodeId = nodeId;
		this.npc = npc;
		this.manager = manager;
	}

	public ModQuery MakeManagerModQuery()
	{
		EntityID targetId = manager?.Id ?? npc?.Id ?? EntityID.INVALID;
		return new ModQuery(pid, targetId, nodeId);
	}
}

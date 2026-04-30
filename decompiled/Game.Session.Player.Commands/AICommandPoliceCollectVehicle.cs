using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public class AICommandPoliceCollectVehicle : InstantAICommand
{
	public NodeID nodeId;

	public EntityID vehicleId;

	public AICommandPoliceCollectVehicle()
	{
	}

	public AICommandPoliceCollectVehicle(PlayerID pid, EntityID eid, NodeID nodeId, EntityID vehicleId)
		: base(pid, CommandType.PoliceOrFedVisit, eid)
	{
		this.nodeId = nodeId;
		this.vehicleId = vehicleId;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		PlayerInfo playerInfo = vehicleId.FindEntity()?.data.mobile?.pid.FindPlayer();
		if (playerInfo != null)
		{
			playerInfo.crew.RemoveScavengeableCar(vehicleId);
			ConsumePeepActions();
		}
	}
}

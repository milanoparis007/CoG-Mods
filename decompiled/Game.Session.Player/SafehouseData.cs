using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player;

public class SafehouseData
{
	public PlayerID pid;

	public EntityID safehouse = EntityID.INVALID;

	public List<EntityID> raidReservations;

	public bool raided;

	public SimTime lastThiefLootDrop = SimTime.MIN_DATE;

	public SafehouseData()
	{
	}

	public SafehouseData(PlayerID pid)
	{
		this.pid = pid;
	}

	public void SetSafehouse(Entity building)
	{
		safehouse = building.Id;
		building.components.building.SetSafehouseOwner(pid);
	}

	public void SetSafehouseForScavenging()
	{
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.SafehouseRaided, safehouse, pid));
		raided = true;
	}
}

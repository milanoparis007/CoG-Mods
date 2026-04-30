using Game.Core;
using Game.Session.Player;

namespace Game.Session.Data;

public struct GrantContext
{
	public PlayerID pid;

	public VisitState visit;

	public QuestUUID quuid;

	public EntityID crewTarget;

	public EntityID buildingTarget;

	public EntityID vehicleTarget;

	public PlayerInfo GetPlayer()
	{
		return pid.FindPlayer();
	}

	public GrantContext(VisitState visit)
		: this(visit.pid, visit, QuestUUID.EMPTY)
	{
	}

	public GrantContext(VisitState visit, QuestUUID quuid)
		: this(visit.pid, visit, quuid)
	{
	}

	public GrantContext(PlayerID pid, VisitState visit, QuestUUID quuid)
	{
		this.pid = pid;
		this.visit = visit;
		this.quuid = quuid;
		crewTarget = EntityID.INVALID;
		buildingTarget = EntityID.INVALID;
		vehicleTarget = EntityID.INVALID;
	}

	public GrantReq FindFulfilledReqs()
	{
		GrantReq grantReq = GrantReq.Nothing;
		if (pid.IsValid)
		{
			grantReq |= GrantReq.PlayerID;
		}
		if (visit != null)
		{
			grantReq |= GrantReq.VisitState;
		}
		if (visit?.peep != null)
		{
			grantReq |= GrantReq.VisitPeep;
		}
		if (visit?.vehicle != null)
		{
			grantReq |= GrantReq.VisitVehicle;
		}
		if (visit?.npc != null)
		{
			grantReq |= GrantReq.VisitNpc;
		}
		if (visit?.building != null)
		{
			grantReq |= GrantReq.VisitBuilding;
		}
		if (quuid.IsSet)
		{
			grantReq |= GrantReq.QuestUUID;
		}
		return grantReq;
	}
}

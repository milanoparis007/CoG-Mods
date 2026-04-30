using System.Diagnostics;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player;

[DebuggerDisplay("{DebugString}")]
public struct CrewAssignment
{
	public static readonly CrewAssignment EMPTY;

	public EntityID peepId;

	public EntityID targetId;

	public CrewType type;

	public bool IsValid => peepId.IsValid;

	public bool IsNotValid => peepId.IsNotValid;

	public bool IsInVehicle
	{
		get
		{
			if (targetId.IsValid)
			{
				return type == CrewType.InVehicle;
			}
			return false;
		}
	}

	public bool IsInBuilding
	{
		get
		{
			if (targetId.IsValid)
			{
				return type == CrewType.InBuilding;
			}
			return false;
		}
	}

	public bool IsInSomewhere
	{
		get
		{
			if (!IsInVehicle)
			{
				return IsInBuilding;
			}
			return true;
		}
	}

	public bool IsNotAssigned
	{
		get
		{
			if (targetId.IsNotValid)
			{
				return type == CrewType.NotAssigned;
			}
			return false;
		}
	}

	public bool IsDead => type == CrewType.Dead;

	public bool IsNotDead => type != CrewType.Dead;

	public EntityID VehicleID
	{
		get
		{
			if (type != CrewType.InVehicle)
			{
				return EntityID.INVALID;
			}
			return targetId;
		}
	}

	public EntityID BuildingID
	{
		get
		{
			if (type != CrewType.InBuilding)
			{
				return EntityID.INVALID;
			}
			return targetId;
		}
	}

	private string DebugString => ToString();

	public CrewAssignment(EntityID peepId)
	{
		this.peepId = peepId;
		targetId = EntityID.INVALID;
		type = CrewType.NotAssigned;
	}

	public CrewAssignment SetPeep(EntityID newPeep)
	{
		return new CrewAssignment
		{
			peepId = newPeep,
			targetId = targetId,
			type = type
		};
	}

	public CrewAssignment SetVehicle(EntityID newVehicle)
	{
		return new CrewAssignment
		{
			peepId = peepId,
			targetId = newVehicle,
			type = CrewType.InVehicle
		};
	}

	public CrewAssignment SetBuilding(EntityID newBuilding)
	{
		return new CrewAssignment
		{
			peepId = peepId,
			targetId = newBuilding,
			type = CrewType.InBuilding
		};
	}

	public CrewAssignment SetNotAssigned()
	{
		return new CrewAssignment
		{
			peepId = peepId,
			targetId = EntityID.INVALID,
			type = CrewType.NotAssigned
		};
	}

	public CrewAssignment SetDead()
	{
		return new CrewAssignment
		{
			peepId = peepId,
			targetId = EntityID.INVALID,
			type = CrewType.Dead
		};
	}

	public Entity GetPeep()
	{
		return peepId.FindEntity();
	}

	public Entity GetTarget()
	{
		return targetId.FindEntity();
	}

	public Entity GetVehicle()
	{
		return VehicleID.FindEntity();
	}

	public Entity GetBuilding()
	{
		return BuildingID.FindEntity();
	}

	public override string ToString()
	{
		return $"CREW {peepId}/{type}:{targetId}";
	}
}

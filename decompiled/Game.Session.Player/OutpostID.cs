using System;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player;

public struct OutpostID : IEquatable<OutpostID>
{
	public static readonly OutpostID INVALID;

	public EntityID buildingId;

	public bool IsValid => buildingId.IsValid;

	public bool IsNotValid => buildingId.IsNotValid;

	public OutpostID(Entity building)
		: this(building.Id)
	{
	}

	public OutpostID(EntityID buildingId)
	{
		this.buildingId = buildingId;
	}

	public Entity FindBuilding()
	{
		return buildingId.FindEntity();
	}

	public static bool Equals(OutpostID a, OutpostID b)
	{
		return EntityID.Equals(a.buildingId, b.buildingId);
	}

	public static bool operator ==(OutpostID a, OutpostID b)
	{
		return Equals(a, b);
	}

	public static bool operator !=(OutpostID a, OutpostID b)
	{
		return !Equals(a, b);
	}

	public bool Equals(OutpostID other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is OutpostID b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return buildingId.GetHashCode();
	}
}

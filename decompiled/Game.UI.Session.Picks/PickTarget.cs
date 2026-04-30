using System;
using Game.Core;
using Game.Session.Assets;
using Game.Session.Entities;

namespace Game.UI.Session.Picks;

public struct PickTarget : IEquatable<PickTarget>
{
	public EntityID eid;

	public VFXManager.WorldArrowHandle arrow;

	public bool IsValid => eid.IsValid;

	public bool IsNotValid => eid.IsNotValid;

	public PickTarget(EntityID target)
	{
		this = default(PickTarget);
		eid = target;
	}

	public PickTarget(EntityID target, VFXManager.WorldArrowHandle arrow)
	{
		this = default(PickTarget);
		eid = target;
		this.arrow = arrow;
	}

	public Entity FindEntity()
	{
		return eid.FindEntity();
	}

	public static implicit operator PickTarget(EntityID eid)
	{
		return new PickTarget(eid);
	}

	public static implicit operator PickTarget(Entity e)
	{
		return new PickTarget(e?.Id ?? EntityID.INVALID);
	}

	public static bool Equals(PickTarget a, PickTarget b)
	{
		if (a.eid == b.eid)
		{
			return a.arrow.Equals(b.arrow);
		}
		return false;
	}

	public bool Equals(PickTarget t)
	{
		return Equals(this, t);
	}

	public override bool Equals(object obj)
	{
		if (obj is PickTarget b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return eid.GetHashCode();
	}
}

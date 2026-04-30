using System;
using Game.Core;

namespace Game.Session.Data;

public struct TickerTarget : IEquatable<TickerTarget>
{
	public static readonly TickerTarget INVALID;

	public EntityID entityId;

	public NodeID nodeId;

	public bool IsSet => !Equals(this, INVALID);

	public bool IsNotSet => Equals(this, INVALID);

	public TickerTarget(EntityID entityId)
	{
		this = default(TickerTarget);
		this.entityId = entityId;
	}

	public TickerTarget(NodeID nodeId)
	{
		this = default(TickerTarget);
		this.nodeId = nodeId;
	}

	public static implicit operator TickerTarget(EntityID entityId)
	{
		return new TickerTarget(entityId);
	}

	public static implicit operator TickerTarget(NodeID nodeId)
	{
		return new TickerTarget(nodeId);
	}

	public static bool Equals(TickerTarget a, TickerTarget b)
	{
		if (EntityID.Equals(a.entityId, b.entityId))
		{
			return NodeID.Equals(a.nodeId, b.nodeId);
		}
		return false;
	}

	public bool Equals(TickerTarget other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is TickerTarget b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return entityId.GetHashCode() ^ nodeId.GetHashCode();
	}
}

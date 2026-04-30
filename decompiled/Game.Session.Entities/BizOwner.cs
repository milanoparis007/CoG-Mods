using System;
using System.Diagnostics;
using Game.Core;

namespace Game.Session.Entities;

[DebuggerDisplay("{DebugString}")]
public struct BizOwner : IEquatable<BizOwner>
{
	public enum Type
	{
		NotValid,
		Real,
		Fake
	}

	public static BizOwner INVALID;

	public Type type;

	public EntityID id;

	public Label eth;

	public bool IsReal => type == Type.Real;

	public bool IsFake => type == Type.Fake;

	public bool IsOwnerSet => type != Type.NotValid;

	public bool IsOwnerNotSet => type == Type.NotValid;

	private string DebugString => ToString();

	public static BizOwner MakeReal(Entity e)
	{
		return new BizOwner
		{
			type = Type.Real,
			id = e.Id,
			eth = e.data.person.eth
		};
	}

	public static BizOwner MakeFake(Label eth)
	{
		return new BizOwner
		{
			type = Type.Fake,
			id = EntityID.INVALID,
			eth = eth
		};
	}

	public static bool Equals(BizOwner a, BizOwner b)
	{
		if (a.type == b.type && a.id == b.id)
		{
			return a.eth == b.eth;
		}
		return false;
	}

	public bool Equals(BizOwner other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is BizOwner a)
		{
			return Equals(a, this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (int)((uint)id.GetHashCode() ^ (uint)type) ^ eth.Index;
	}

	public override string ToString()
	{
		if (IsReal)
		{
			string arg = id.FindEntity()?.data.person.InfoString() ?? "??";
			return $"Owner {arg}/{id}/{eth}";
		}
		return $"Owner {type}/{id}/{eth}";
	}
}

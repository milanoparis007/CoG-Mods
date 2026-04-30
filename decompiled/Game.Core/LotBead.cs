using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public class LotBead
{
	public WorldPos pos;

	public float deg;

	public EntityID leftId;

	public EntityID rightId;

	public NodeID nodeOwner;

	public GridTransform Transform => new GridTransform(pos, deg);

	private string DebugString => ToString();

	public LotBead()
	{
	}

	public LotBead(GridTransform transform, NodeID owner)
		: this(transform.pos, transform.deg, owner)
	{
	}

	public LotBead(WorldPos pos, float deg, NodeID owner)
	{
		this.pos = pos;
		this.deg = deg;
		nodeOwner = owner;
	}

	public EntityID GetEntity(bool left)
	{
		if (!left)
		{
			return rightId;
		}
		return leftId;
	}

	public bool HasEntity(EntityID id)
	{
		if (!(leftId == id))
		{
			return rightId == id;
		}
		return true;
	}

	public bool HasEntity(bool left)
	{
		if (!left)
		{
			return rightId.IsValid;
		}
		return leftId.IsValid;
	}

	public bool IsLeft(EntityID id)
	{
		return leftId == id;
	}

	public bool IsRight(EntityID id)
	{
		return rightId == id;
	}

	public void ClearEntity(EntityID id, bool left)
	{
		if (left)
		{
			if (leftId == id)
			{
				leftId = EntityID.INVALID;
			}
			else
			{
				Logger.Warning("Double clearing left entity that is not attached to bead");
			}
		}
		else if (rightId == id)
		{
			rightId = EntityID.INVALID;
		}
		else
		{
			Logger.Warning("Double clearing right entity that is not attached to bead");
		}
	}

	public void SetEntity(EntityID id, bool left)
	{
		if (left)
		{
			if (leftId.IsNotValid)
			{
				leftId = id;
			}
			else
			{
				Logger.Warning("Double setting left entity that is not attached to bead");
			}
		}
		else if (rightId.IsNotValid)
		{
			rightId = id;
		}
		else
		{
			Logger.Warning("Double setting right entity that is not attached to bead");
		}
	}

	public override string ToString()
	{
		return $"BEAD {pos}, {deg} deg, {nodeOwner}, left: {leftId}, right: {rightId}";
	}
}

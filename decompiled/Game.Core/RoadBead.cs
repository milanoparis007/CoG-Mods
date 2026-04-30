using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public class RoadBead : TransitBead
{
	public RoadBeadPlacement placementStyle;

	public EntityID id;

	public bool canMakeBridge;

	public GridTransform Transform => new GridTransform(pos, deg);

	private string DebugString => ToString();

	public RoadBead()
	{
	}

	public RoadBead(GridTransform transform, RoadBeadPlacement placement)
		: this(transform.pos, transform.deg, placement)
	{
	}

	public RoadBead(WorldPos pos, float deg, RoadBeadPlacement placement)
	{
		base.pos = pos;
		base.deg = deg;
		placementStyle = placement;
	}

	public EntityID GetEntity()
	{
		return id;
	}

	public bool HasEntity(EntityID id)
	{
		return this.id == id;
	}

	public override string ToString()
	{
		return $"ROADBEAD {pos}, {deg} deg, tile = {placementStyle}";
	}

	public void ClearEntity(EntityID id)
	{
		if (this.id == id)
		{
			this.id = EntityID.INVALID;
		}
		else
		{
			Logger.Warning("Double clearing entity that is not attached to road bead");
		}
	}

	public void SetEntity(EntityID newId)
	{
		if (id.IsNotValid)
		{
			id = newId;
		}
		else
		{
			Logger.Warning("Double setting entity that is not attached to road bead");
		}
	}
}

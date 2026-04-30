using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public class BridgeBead : TransitBead
{
	public RoadBeadPlacement placementStyle;

	public EntityID id;

	public bool canMakeBridge;

	public BridgeBead()
	{
	}

	public BridgeBead(GridTransform transform, RoadBeadPlacement placement)
		: this(transform.pos, transform.deg, placement)
	{
	}

	public BridgeBead(WorldPos pos, float deg, RoadBeadPlacement placement)
	{
		base.pos = pos;
		base.deg = deg;
		placementStyle = placement;
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

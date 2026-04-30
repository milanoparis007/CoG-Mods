using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewVehicleCondition : BaseDeltaMultiplierModifier
{
	public Test @is;

	public VehicleHealthType type;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override bool DoesPass(ModQuery query)
	{
		Entity vehicle = GetVehicle(query);
		bool flag = @is == Test.EqualTo;
		if (vehicle == null)
		{
			return !flag;
		}
		return vehicle.components.mobile.FindHealthInfo()?.category == type == flag;
	}

	public static Entity GetVehicle(ModQuery query)
	{
		Entity entity = query.FindTarget();
		if (entity == null)
		{
			return null;
		}
		if (entity.components.mobile != null)
		{
			return entity;
		}
		return query.FindPlayer().crew.FindVehicleAssignedToPeep(entity.Id).FindEntity();
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.crew-vehicle-condition", "delta", AbstractModifier.FormatDelta(delta));
	}
}

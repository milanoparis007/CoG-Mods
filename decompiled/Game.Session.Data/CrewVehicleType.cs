using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewVehicleType : BaseDeltaMultiplierModifier
{
	public Test @is;

	public AvatarType type;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override bool DoesPass(ModQuery query)
	{
		Entity vehicle = CrewVehicleCondition.GetVehicle(query);
		bool flag = @is == Test.EqualTo;
		if (vehicle == null)
		{
			return !flag;
		}
		return (vehicle?.config.mobile)?.type == type == flag;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.crew-vehicle-type", "delta", AbstractModifier.FormatDelta(delta));
	}
}

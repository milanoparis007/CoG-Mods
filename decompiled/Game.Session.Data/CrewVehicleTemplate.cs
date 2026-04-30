using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewVehicleTemplate : BaseDeltaMultiplierModifier
{
	public Test @is;

	public Label template;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override bool DoesPass(ModQuery query)
	{
		Entity vehicle = CrewVehicleCondition.GetVehicle(query);
		bool flag = @is == Test.EqualTo;
		if (vehicle == null)
		{
			return !flag;
		}
		return vehicle?.config.Template == template == flag;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.crew-vehicle-template", "delta", AbstractModifier.FormatDelta(delta));
	}
}

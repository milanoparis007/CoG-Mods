using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CapacityOverrunMod : BaseModifier
{
	public PlayerCrew.CapacityType type;

	public Fixnum perpoint;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey => GetLocKey(type);

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		int capacityOverrun = query.FindPlayer().crew.GetCapacityOverrun(type, overrun: true);
		return source + capacityOverrun * perpoint;
	}

	public static string GetLocKey(PlayerCrew.CapacityType type)
	{
		return type switch
		{
			PlayerCrew.CapacityType.Crew => "mod.capacity-overrun-crew", 
			PlayerCrew.CapacityType.Car => "mod.capacity-overrun-car", 
			PlayerCrew.CapacityType.Truck => "mod.capacity-overrun-truck", 
			PlayerCrew.CapacityType.AnyVehicle => "mod.capacity-overrun-vehicle", 
			_ => "mod.capacity-overrun-vehicle", 
		};
	}
}

using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerCapacity : VisitWithValueRequirement
{
	public PlayerCrew.CapacityType type;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).crew.GetCapacity(type);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get(GetLocKey(type)));
	}

	public static string GetLocKey(PlayerCrew.CapacityType type)
	{
		return type switch
		{
			PlayerCrew.CapacityType.Crew => "ui.requirements.capacity-crew", 
			PlayerCrew.CapacityType.Car => "ui.requirements.capacity-car", 
			PlayerCrew.CapacityType.Truck => "ui.requirements.capacity-truck", 
			PlayerCrew.CapacityType.AnyVehicle => "ui.requirements.capacity-vehicle", 
			_ => "ui.requirements.capacity-overrun-vehicle", 
		};
	}
}

using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerCapacityAvailable : VisitWithValueRequirement
{
	public PlayerCrew.CapacityType type;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).crew.GetCapacityOverrun(type, overrun: false);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get(GetLocKey(type)));
	}

	public static string GetLocKey(PlayerCrew.CapacityType type)
	{
		return type switch
		{
			PlayerCrew.CapacityType.Crew => "ui.requirements.capacity-available-crew", 
			PlayerCrew.CapacityType.Car => "ui.requirements.capacity-available-car", 
			PlayerCrew.CapacityType.Truck => "ui.requirements.capacity-available-truck", 
			PlayerCrew.CapacityType.AnyVehicle => "ui.requirements.capacity-available-vehicle", 
			_ => "ui.requirements.capacity-available-vehicle", 
		};
	}
}

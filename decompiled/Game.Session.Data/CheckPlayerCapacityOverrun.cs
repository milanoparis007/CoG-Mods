using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerCapacityOverrun : VisitWithValueRequirement
{
	public PlayerCrew.CapacityType type;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).crew.GetCapacityOverrun(type, overrun: true);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get(GetLocKey(type)));
	}

	public static string GetLocKey(PlayerCrew.CapacityType type)
	{
		return type switch
		{
			PlayerCrew.CapacityType.Crew => "ui.requirements.capacity-overrun-crew", 
			PlayerCrew.CapacityType.Car => "ui.requirements.capacity-overrun-car", 
			PlayerCrew.CapacityType.Truck => "ui.requirements.capacity-overrun-truck", 
			PlayerCrew.CapacityType.AnyVehicle => "ui.requirements.capacity-overrun-vehicle", 
			_ => "ui.requirements.capacity-overrun-vehicle", 
		};
	}
}

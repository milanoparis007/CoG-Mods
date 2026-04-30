using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckCrewPeepAssigned : AbstractVisitRequirement
{
	public enum TargetType
	{
		Me,
		NPC
	}

	public enum AssignmentType
	{
		Nothing,
		Vehicle,
		Building,
		VehicleOrBuilding
	}

	public enum VehicleType
	{
		Any,
		Car,
		Truck
	}

	public AssignmentType to;

	public VehicleType vehtype;

	public TargetType target;

	public override bool DoesPass(VisitState visit)
	{
		CrewAssignment? crewAssignment = ((target == TargetType.Me) ? new CrewAssignment?(visit.crew) : visit.npc.components?.agent.FindCrewAssignment());
		if (crewAssignment.Value.IsNotValid)
		{
			return false;
		}
		switch (to)
		{
		case AssignmentType.Building:
			return CheckInBuilding(visit, crewAssignment.Value);
		case AssignmentType.Vehicle:
			return CheckInVehicle(visit, crewAssignment.Value);
		case AssignmentType.VehicleOrBuilding:
			if (!CheckInVehicle(visit, crewAssignment.Value))
			{
				return CheckInBuilding(visit, crewAssignment.Value);
			}
			return true;
		default:
			return visit.crew.IsNotAssigned;
		}
	}

	public bool CheckInBuilding(VisitState visit, CrewAssignment crew)
	{
		return crew.IsInBuilding;
	}

	public bool CheckInVehicle(VisitState visit, CrewAssignment crew)
	{
		if (crew.IsInVehicle)
		{
			if (vehtype != VehicleType.Car)
			{
				if (vehtype != VehicleType.Truck)
				{
					return true;
				}
				return crew.GetVehicle().config.mobile.type == AvatarType.Truck;
			}
			return crew.GetVehicle().config.mobile.type == AvatarType.Car;
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(message: to switch
		{
			AssignmentType.Building => Loc.Get("ui.requirements.checkcrew.building"), 
			AssignmentType.Vehicle => (vehtype == VehicleType.Car) ? Loc.Get("ui.requirements.checkcrew.car") : ((vehtype == VehicleType.Truck) ? Loc.Get("ui.requirements.checkcrew.truck") : Loc.Get("ui.requirements.checkcrew.vehicle")), 
			AssignmentType.VehicleOrBuilding => Loc.Get("ui.requirements.checkcrew.working"), 
			_ => Loc.Get("ui.requirements.checkcrew.unassigned"), 
		}, passed: DoesPass(visit));
	}
}

using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandRemoveCar : InstantAICommand
{
	public AICommandRemoveCar()
	{
	}

	public AICommandRemoveCar(PlayerID pid, EntityID eid)
		: base(pid, CommandType.RemovePeepFromCar, eid)
	{
	}

	protected override void PerformTurnActions()
	{
		CrewAssignment crewForPeep = GetPlayer().crew.GetCrewForPeep(peepId);
		if (!crewForPeep.IsNotValid && crewForPeep.IsInVehicle)
		{
			GetPlayer().crew.UnassignCrewAndDestroyVehicle(peepId);
		}
	}
}

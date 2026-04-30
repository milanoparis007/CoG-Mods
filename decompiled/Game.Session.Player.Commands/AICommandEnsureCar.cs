using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public class AICommandEnsureCar : InstantAICommand
{
	public NodeID targetNode;

	public AICommandEnsureCar()
	{
	}

	public AICommandEnsureCar(PlayerID pid, EntityID eid, EntityID safehouse)
		: base(pid, CommandType.EnsurePeepHasCar, eid)
	{
		targetNode = safehouse.FindEntity()?.components.board?.GetNodeID() ?? NodeID.INVALID;
	}

	protected override void PerformTurnActions()
	{
		PlayerCrew crew = GetPlayer().crew;
		CrewAssignment crewForPeep = crew.GetCrewForPeep(peepId);
		if (!crewForPeep.IsNotValid && !crewForPeep.IsInVehicle)
		{
			Node node = targetNode.FindNode();
			Entity peep = peepId.FindEntity();
			crew.CreateVehicleAndAssignCrew(node, peep);
		}
	}
}

using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandBuildingCallback : InstantAICommand
{
	public EntityID buildingId;

	public string message;

	public AICommandBuildingCallback()
	{
	}

	public AICommandBuildingCallback(CommandType type, PlayerID pid, EntityID eid, EntityID building, string message)
		: base(pid, type, eid)
	{
		buildingId = building;
		this.message = message;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		switch (type)
		{
		case CommandType.InstallBackroom:
			GetPlayer().ai.safehouse.ProcessAddBackroomRequest(buildingId);
			break;
		case CommandType.TradeCallback:
			GetPlayer().ai.business.TradeCallbackAtBusiness(buildingId, message);
			break;
		case CommandType.RecordHarassment:
			GetPlayer().ai.goon?.RecordHarassment(buildingId, message);
			break;
		default:
			Logger.Warning($"Unknown command type for {this}: {type}");
			break;
		}
	}
}

using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandGenMessageCallBack : InstantAICommand
{
	public string message;

	public EntityID buildingID;

	public AICommandGenMessageCallBack()
	{
	}

	public AICommandGenMessageCallBack(PlayerID pid, EntityID eid, EntityID building, string message)
		: base(pid, CommandType.GenMessageCallback, eid)
	{
		this.message = message;
		buildingID = building;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		GetPlayer().ai.ReadCallback(message, buildingID);
	}
}

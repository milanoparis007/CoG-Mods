using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandLogMessage : InstantAICommand
{
	public string message;

	public AICommandLogMessage()
	{
	}

	public AICommandLogMessage(PlayerID pid, EntityID eid, string message)
		: base(pid, CommandType.LogMessage, eid)
	{
		this.message = message;
	}

	protected override void PerformTurnActions()
	{
	}
}

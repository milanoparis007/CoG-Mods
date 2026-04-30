using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandAtOutpost : InstantAICommand
{
	public EntityID buildingId;

	public string message;

	public AICommandAtOutpost()
	{
	}

	public AICommandAtOutpost(PlayerID pid, EntityID eid, EntityID buildingId, string message)
		: base(pid, CommandType.AtOutpost, eid)
	{
		this.message = message;
		this.buildingId = buildingId;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		GetPlayer().ai.territory.PeepAtOutpost(peepId, buildingId, message);
		ConsumePeepActions();
	}
}

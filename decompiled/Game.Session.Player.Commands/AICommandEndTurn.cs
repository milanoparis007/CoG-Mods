using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandEndTurn : InstantAICommand
{
	public AICommandEndTurn()
	{
	}

	public AICommandEndTurn(PlayerID pid, EntityID eid)
		: base(pid, CommandType.EndTurn, eid)
	{
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		ConsumePeepActions();
	}
}

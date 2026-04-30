using Game.Core;

namespace Game.Session.Player.Commands;

public class AICommandRefillCash : InstantAICommand
{
	public EntityID buildingId;

	public AICommandRefillCash()
	{
	}

	public AICommandRefillCash(PlayerID pid, EntityID eid, EntityID building)
		: base(pid, CommandType.RefillCash, eid)
	{
		buildingId = building;
	}

	protected override void PerformTurnActions()
	{
		base.PerformTurnActions();
		PlayerInfo player = GetPlayer();
		EntityID safehouse = player.territory.Safehouse;
		if (buildingId != safehouse)
		{
			Logger.Warning("Not at the safehouse", pid, peepId, buildingId);
		}
		else
		{
			player.ai.safehouse.RefillCashForPeep(peepId, buildingId);
		}
	}
}

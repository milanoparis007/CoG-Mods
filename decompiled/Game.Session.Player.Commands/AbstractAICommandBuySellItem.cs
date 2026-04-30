using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.Session.Player.Commands;

public abstract class AbstractAICommandBuySellItem : InstantAICommand
{
	public Label item;

	public EntityID building;

	public bool playerBuys;

	public AbstractAICommandBuySellItem()
	{
	}

	public AbstractAICommandBuySellItem(CommandType type, PlayerID pid, EntityID eid, Label item, EntityID building, bool playerBuys)
		: base(pid, type, eid)
	{
		this.item = item;
		this.building = building;
		this.playerBuys = playerBuys;
	}

	protected override void PerformTurnActions()
	{
		Entity entity = peepId.FindEntity();
		Node node = entity.data.agent.nid.FindNode();
		Node node2 = building.FindEntity().components.board.GetNode();
		if (node == node2)
		{
			BuySellUtils.ExecuteAIBuySell(pid.FindPlayer(), entity, building.FindEntity(), item, playerBuys);
			ConsumePeepActions();
		}
	}
}

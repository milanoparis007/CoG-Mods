using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.Session.Player.Commands;

public abstract class AbstractAICommandPickDropItem : InstantAICommand
{
	public Label item;

	public EntityID building;

	public bool peepPicksUp;

	public AbstractAICommandPickDropItem()
	{
	}

	public AbstractAICommandPickDropItem(CommandType type, PlayerID pid, EntityID eid, Label item, EntityID building, bool peepPicksUp)
		: base(pid, type, eid)
	{
		this.item = item;
		this.building = building;
		this.peepPicksUp = peepPicksUp;
	}

	protected override void PerformTurnActions()
	{
		Entity peep = peepId.FindEntity();
		Entity entity = building.FindEntity();
		BuySellUtils.ExecuteAIPickUpDropOff(pid.FindPlayer(), peep, entity, item, peepPicksUp);
		ConsumePeepActions();
	}
}

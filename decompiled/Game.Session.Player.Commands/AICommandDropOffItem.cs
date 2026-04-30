using Game.Core;

namespace Game.Session.Player.Commands;

public sealed class AICommandDropOffItem : AbstractAICommandPickDropItem
{
	public AICommandDropOffItem()
	{
	}

	public AICommandDropOffItem(PlayerID pid, EntityID eid, Label item, EntityID building)
		: base(CommandType.PickupItem, pid, eid, item, building, peepPicksUp: false)
	{
	}
}
